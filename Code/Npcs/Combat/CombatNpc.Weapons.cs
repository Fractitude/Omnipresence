using Sandbox.Npcs.Tasks;

namespace Sandbox.Npcs.CombatNpc;

/// <summary>
/// What a combat NPC carries: up to <see cref="MaxGuns"/> guns, each with its own spare magazines, and one melee
/// weapon. It holds the best gun that still has ammo, takes the melee weapon out when none has, and goes after ammo
/// and loose weapons when it is out of ammo, or when a much better weapon is lying close by.
/// </summary>
public partial class CombatNpc
{
	/// <summary>
	/// Full magazines carried for the gun the NPC starts with. Each reload uses one. A gun picked up later comes with
	/// one, and ammo boxes add more.
	/// </summary>
	[Property, Group( "Balance" )]
	public int SpareMagazines { get; set; } = 2;

	/// <summary>
	/// How many guns it carries. A better one found with all hands full replaces the worst.
	/// </summary>
	[Property, Group( "Balance" )]
	public int MaxGuns { get; set; } = 3;

	/// <summary>
	/// How far it looks for ammo and loose weapons once it has nothing left to shoot with.
	/// </summary>
	[Property, Group( "Balance" )]
	public float PickupRange { get; set; } = 900f;

	/// <summary>
	/// How far it will go for a better weapon while it still has ammo.
	/// </summary>
	[Property, Group( "Balance" )]
	public float UpgradeRange { get; set; } = 500f;

	/// <summary>
	/// A loose weapon has to be this many times as strong as the best gun carried to be worth going for while it still
	/// has ammo.
	/// </summary>
	[Property, Group( "Balance" )]
	public float UpgradeFactor { get; set; } = 1.25f;

	/// <summary>
	/// Below this fraction of its health it goes for a health kit, if one is close by.
	/// </summary>
	[Property, Group( "Balance" ), Range( 0, 1 )]
	public float HealthKitBelow { get; set; } = 0.5f;

	/// <summary>
	/// The one melee weapon, cloned from <see cref="MeleePrefab"/> when the NPC spawns. Nothing else can add one.
	/// </summary>
	public MeleeWeapon Melee { get; private set; }

	/// <summary>
	/// True while the melee weapon is in hand instead of a gun. Set by the host, followed on every peer.
	/// </summary>
	[Sync( SyncFlags.FromHost ), Change( nameof(OnUsingMeleeChanged) )]
	public bool UsingMelee { get; private set; }

	// The gun in hand, or the last one that was. <see cref="Weapon"/> is whichever of it and the melee weapon is in hand.
	private BaseSandboxWeapon _gun;

	// Every gun carried, and how many spare magazines each has. Host only: the other peers only need what is in hand.
	private readonly List<BaseSandboxWeapon> _guns = [];
	private readonly Dictionary<BaseSandboxWeapon, int> _spares = new();

	// Its health when it spawned, what a health kit heals it up to
	private float _fullHealth;

	// Pickups it could not reach or use, and when to look at them again
	private readonly Dictionary<GameObject, TimeUntil> _ignoredPickups = new();

	/// <summary>
	/// Clone <see cref="MeleePrefab"/> onto the NPC, holstered.
	/// </summary>
	private void ArmWithMelee()
	{
		if ( !MeleePrefab.IsValid() )
			return;

		var go = GameObject.Clone( MeleePrefab, new CloneConfig { Parent = GameObject, Transform = global::Transform.Zero, StartEnabled = true } );
		var weapon = go?.GetComponent<MeleeWeapon>( true );
		if ( !weapon.IsValid() )
		{
			go?.Destroy();
			Log.Warning( $"{DisplayName}: '{MeleePrefab.ResourcePath}' isn't a melee weapon prefab, no melee weapon." );
			return;
		}

		Melee?.GameObject.DestroyImmediate();
		Melee = weapon;
	}

	/// <summary>
	/// Called once the NPC starts: find the weapons it was given, load the gun and put the right one in hand.
	/// </summary>
	private void StartWeapons()
	{
		_gun = Weapon;
		_fullHealth = Health;

		// Found on a client by what the spawn snapshot brought
		if ( !Melee.IsValid() )
			Melee = Components.GetAll<MeleeWeapon>( FindMode.EverythingInChildren ).FirstOrDefault();

		if ( !IsProxy )
		{
			// Holstered: no pickup model or physics until it is dropped
			if ( Melee.IsValid() )
				Melee.IsDropped = false;

			if ( _gun.IsValid() )
			{
				_guns.Add( _gun );
				_spares[_gun] = SpareMagazines;

				// A gun nobody was given through an inventory has no magazine and never runs dry. Load it, so the
				// NPC has to reload like anyone else (see ReloadWeapon).
				if ( _gun.UsesPrimaryClip )
					_gun.Clip1 = _gun.PrimaryClipSize;
			}
		}

		// Armed with only a melee weapon: it is in hand from the start
		if ( !_gun.IsValid() && Melee.IsValid() )
		{
			if ( !IsProxy )
				UsingMelee = true;

			ApplyHeld();
		}

		if ( _gun.IsValid() && Renderer.IsValid() )
		{
			_gun.CreateWorldModel( Renderer );

			// The weapon says how forgiving it is in NPC hands; we say how good we are with it.
			_gun.SpreadScale = _gun.Npc.SpreadScale * AimSpreadScale;

			if ( !IsProxy )
				Animation.SetHoldType( _gun.HoldType );
		}
	}

	//
	// What is carried
	//

	/// <summary>
	/// Spare magazines for a gun.
	/// </summary>
	public int SparesOf( BaseSandboxWeapon gun ) => gun.IsValid() && _spares.TryGetValue( gun, out var spares ) ? spares : 0;

	/// <summary>
	/// Use up one spare magazine for the reload of a gun. False if there is none.
	/// </summary>
	public bool TakeSpareMagazine( BaseSandboxWeapon gun )
	{
		if ( SparesOf( gun ) <= 0 )
			return false;

		_spares[gun]--;
		return true;
	}

	/// <summary>
	/// Can it still shoot this gun: rounds in it, or a spare magazine to reload from.
	/// </summary>
	private bool IsUsable( BaseSandboxWeapon gun )
	{
		return gun.IsValid() && (!gun.UsesPrimaryClip || gun.Clip1 > 0 || SparesOf( gun ) > 0);
	}

	/// <summary>
	/// How strong a gun is: what one shot does.
	/// </summary>
	private static float Rating( BaseSandboxWeapon gun ) => gun.Ballistics.Damage * Math.Max( gun.Ballistics.Pellets, 1 );

	/// <summary>
	/// Nothing left to shoot with: no gun, or every gun empty with no spare magazine for it.
	/// </summary>
	private bool IsOutOfAmmo => !_guns.Any( IsUsable );

	//
	// What is in hand
	//

	/// <summary>
	/// Put the melee weapon in hand. Host only.
	/// </summary>
	private void UseMelee()
	{
		if ( IsProxy || UsingMelee || !Melee.IsValid() )
			return;

		UsingMelee = true;
		ApplyHeld();
	}

	private void OnUsingMeleeChanged( bool oldValue, bool newValue ) => ApplyHeld();

	/// <summary>
	/// Make the world model, hold type and <see cref="Weapon"/> match <see cref="UsingMelee"/> and the gun in hand, on
	/// this peer.
	/// </summary>
	private void ApplyHeld( bool rethink = true )
	{
		// With no gun the melee weapon is all there is
		var melee = UsingMelee || !_gun.IsValid();
		BaseSandboxWeapon held = melee ? Melee : _gun;
		BaseSandboxWeapon other = melee ? _gun : Melee;

		if ( !held.IsValid() || !Renderer.IsValid() )
			return;

		if ( other.IsValid() && other.WorldModel.IsValid() )
			other.WorldModel.Destroy();

		if ( !held.WorldModel.IsValid() )
			held.CreateWorldModel( Renderer );

		Weapon = held;

		if ( !IsProxy )
		{
			Animation.SetHoldType( held.HoldType );

			if ( rethink )
				EndCurrentSchedule();
		}
	}

	/// <summary>
	/// Put a gun in hand, on every peer: the world model, hold type and aim. Host only.
	/// </summary>
	private void SetActiveGun( BaseSandboxWeapon gun, bool rethink )
	{
		if ( IsProxy || !gun.IsValid() || (gun == _gun && !UsingMelee) )
			return;

		UsingMelee = false;
		ActivateGun( gun, _gun );

		if ( rethink )
			EndCurrentSchedule();
	}

	[Rpc.Broadcast]
	private void ActivateGun( BaseSandboxWeapon gun, BaseSandboxWeapon previous )
	{
		if ( !gun.IsValid() )
			return;

		if ( previous.IsValid() && previous != gun && previous.WorldModel.IsValid() )
			previous.WorldModel.Destroy();

		_gun = gun;
		gun.SpreadScale = gun.Npc.SpreadScale * AimSpreadScale;

		ApplyHeld( rethink: false );
	}

	/// <summary>
	/// Hold the strongest gun that still has ammo. Host only.
	/// </summary>
	private void ChooseGun( bool rethink )
	{
		var best = _guns.Where( IsUsable ).OrderByDescending( Rating ).FirstOrDefault();
		if ( best.IsValid() )
			SetActiveGun( best, rethink );
	}

	//
	// What to do about it
	//

	/// <summary>
	/// Called when the NPC picks what to do: look after its weapons. Returns an errand for ammo or a better weapon, or
	/// null to carry on.
	/// </summary>
	private ScheduleBase ChooseWeapons( GameObject visible )
	{
		_guns.RemoveAll( x => !x.IsValid() );

		// Hurt: a health kit comes before anything else
		var kit = Health < _fullHealth * HealthKitBelow ? FindHealthKit() : null;

		var pickup = IsOutOfAmmo ? FindPickup( needAmmo: true ) : null;

		if ( !IsOutOfAmmo )
		{
			// Something to shoot with: the best of it in hand, and a look for something better
			ChooseGun( rethink: true );
			pickup = FindPickup( needAmmo: false );
		}

		if ( kit.IsValid() )
			pickup = kit;

		// Worth the trip, unless the enemy is nearer than the thing is
		if ( pickup.IsValid() && !(visible.IsValid() && WorldPosition.Distance( visible.WorldPosition ) < WorldPosition.Distance( pickup.WorldPosition )) )
		{
			var fetch = GetSchedule<CombatPickupSchedule>();
			fetch.Pickup = pickup;
			fetch.Speed = Pace( EngageSpeed );
			return fetch;
		}

		// Nothing to be had: hit them with the melee weapon instead
		if ( IsOutOfAmmo && !UsingMelee && Melee.IsValid() )
			UseMelee();

		return null;
	}

	/// <summary>
	/// The nearest ammo box for a gun it carries, or loose weapon it can use. When it has nothing to shoot with that is
	/// anything within <see cref="PickupRange"/>; otherwise only a weapon much stronger than its best, within
	/// <see cref="UpgradeRange"/>.
	/// </summary>
	private GameObject FindPickup( bool needAmmo )
	{
		GameObject best = null;
		var bestDistance = needAmmo ? PickupRange : UpgradeRange;
		var bestRating = _guns.Where( x => x.IsValid() ).Select( Rating ).DefaultIfEmpty( 0f ).Max();

		void Consider( GameObject candidate )
		{
			if ( _ignoredPickups.TryGetValue( candidate, out var until ) && !until )
				return;

			var distance = WorldPosition.Distance( candidate.WorldPosition );
			if ( distance >= bestDistance )
				return;

			best = candidate;
			bestDistance = distance;
		}

		// Ammo is only any use for a gun it already has, and only wanted when it has run out
		if ( needAmmo )
		{
			var types = _guns.Where( x => x.IsValid() && x.PrimaryAmmoType is not null ).Select( x => x.PrimaryAmmoType ).ToHashSet();

			foreach ( var ammo in Scene.GetAll<AmmoPickup>() )
			{
				if ( ammo.AmmoType is not null && types.Contains( ammo.AmmoType ) )
					Consider( ammo.GameObject );
			}
		}

		foreach ( var weapon in Scene.GetAll<BaseSandboxWeapon>() )
		{
			if ( !CanTake( weapon ) )
				continue;

			if ( !needAmmo && Rating( weapon ) < bestRating * UpgradeFactor )
				continue;

			Consider( weapon.GameObject );
		}

		return best;
	}

	/// <summary>
	/// The nearest health kit within <see cref="PickupRange"/>.
	/// </summary>
	private GameObject FindHealthKit()
	{
		GameObject best = null;
		var bestDistance = PickupRange;

		foreach ( var kit in Scene.GetAll<HealthPickup>() )
		{
			if ( _ignoredPickups.TryGetValue( kit.GameObject, out var until ) && !until )
				continue;

			var distance = WorldPosition.Distance( kit.WorldPosition );
			if ( distance >= bestDistance )
				continue;

			best = kit.GameObject;
			bestDistance = distance;
		}

		return best;
	}

	/// <summary>
	/// A loose gun, with rounds in it, that this NPC can use.
	/// </summary>
	private bool CanTake( BaseSandboxWeapon weapon )
	{
		return weapon.IsValid() && !_guns.Contains( weapon )
			&& weapon is not MeleeWeapon
			&& weapon.IsDropped && !weapon.HasOwner
			&& weapon.UsableByNpcs && weapon.Npc.CanBePickedUp
			&& weapon.UsesPrimaryClip && weapon.Clip1 != 0
			&& !weapon.ShootInput.IsEnabled && !weapon.SecondaryInput.IsEnabled
			&& !weapon.GetComponentInParent<Npc>().IsValid();
	}

	/// <summary>
	/// Leave this pickup alone for a while - it could not be reached or taken.
	/// </summary>
	public void IgnorePickup( GameObject pickup )
	{
		if ( !pickup.IsValid() )
			return;

		// Forget the ones that are gone, so this does not grow
		foreach ( var gone in _ignoredPickups.Keys.Where( x => !x.IsValid() ).ToList() )
			_ignoredPickups.Remove( gone );

		_ignoredPickups[pickup] = 20f;
	}

	/// <summary>
	/// Take an ammo box (spare magazines for a gun carried) or a loose weapon (carried with the others). Host only.
	/// False if it could not be taken.
	/// </summary>
	public bool Collect( GameObject pickup )
	{
		if ( IsProxy || !pickup.IsValid() )
			return false;

		// As close as a player has to be to use it
		var reach = pickup.Components.Get<Interactable>( FindMode.EnabledInSelfAndDescendants )?.Range ?? 80f;
		if ( pickup.WorldPosition.Distance( WorldPosition ) > MathF.Max( reach, 60f ) )
			return false;

		if ( pickup.GetComponent<HealthPickup>() is { } kit )
			return kit.TakeByNpc( this, _fullHealth );

		if ( pickup.GetComponent<AmmoPickup>() is { } ammo )
		{
			// To a gun that has none, if it carries one that uses this ammo
			var gun = _guns.Where( x => x.IsValid() && x.PrimaryAmmoType == ammo.AmmoType )
				.OrderBy( x => SparesOf( x ) + Math.Max( x.Clip1, 0 ) ).FirstOrDefault();

			var rounds = gun.IsValid() ? ammo.TakeByNpc( gun ) : 0;
			if ( rounds <= 0 )
				return false;

			_spares[gun] = SparesOf( gun ) + Math.Max( 1, MathX.CeilToInt( rounds / (float)Math.Max( gun.PrimaryClipSize, 1 ) ) );
			ChooseGun( rethink: false );
			return true;
		}

		if ( pickup.GetComponent<BaseSandboxWeapon>() is { } weapon && CanTake( weapon ) )
		{
			// Never fired, or from a magazine counter that was never set: a full one. One spare magazine with it.
			if ( weapon.Clip1 < 0 )
				weapon.Clip1 = weapon.PrimaryClipSize;

			// Full hands: the weakest of what it has goes, an empty one first
			var dropped = _guns.Count >= Math.Max( MaxGuns, 1 )
				? _guns.OrderBy( x => IsUsable( x ) ? 1 : 0 ).ThenBy( Rating ).FirstOrDefault()
				: null;

			weapon.GameObject.SetParent( GameObject, false );
			weapon.LocalTransform = global::Transform.Zero;

			// Carried: no pickup model or physics
			weapon.IsDropped = false;

			_guns.Add( weapon );
			_spares[weapon] = 1;
			weapon.SpreadScale = weapon.Npc.SpreadScale * AimSpreadScale;

			ChooseGun( rethink: false );

			if ( dropped.IsValid() )
				DropGun( dropped );

			return true;
		}

		return false;
	}

	/// <summary>
	/// Put a gun it carries on the floor, and leave it there.
	/// </summary>
	private void DropGun( BaseSandboxWeapon gun )
	{
		_guns.Remove( gun );
		_spares.Remove( gun );

		// If it was in hand, something else is by now. If nothing is, the hands are empty.
		if ( gun.WorldModel.IsValid() )
			gun.WorldModel.Destroy();

		DropWeapon( gun );
		IgnorePickup( gun.GameObject );
	}

	//
	// Dying
	//

	/// <summary>
	/// Everything carried lands on the floor.
	/// </summary>
	private void DropWeapons()
	{
		foreach ( var gun in _guns.ToList() )
			DropWeapon( gun );

		DropWeapon( Melee );
	}

	/// <summary>
	/// Drop a weapon into the world where the hand was, so it survives the NPC's death and can be picked up. If nobody
	/// takes it within <see cref="DroppedWeaponLifetime"/> it's cleaned up.
	/// </summary>
	private void DropWeapon( BaseSandboxWeapon weapon )
	{
		if ( !weapon.IsValid() )
			return;

		var position = weapon.WorldModel.IsValid()
			? weapon.WorldModel.WorldPosition
			: WorldPosition + Vector3.Up * 32f;

		weapon.DropIntoWorld( position, Vector3.Up * 50f );

		// The timer lives on the weapon - this NPC is about to be destroyed
		weapon.Invoke( DroppedWeaponLifetime, () => RemoveIfUnclaimed( weapon ) );
	}
}
