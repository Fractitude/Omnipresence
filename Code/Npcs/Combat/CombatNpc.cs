using Sandbox.Npcs.Layers;
using Sandbox.Npcs.Schedules;
using Sandbox.Npcs.Tasks;

namespace Sandbox.Npcs.CombatNpc;

/// <summary>
/// A combat NPC that searches for players, advances on them, fires in bursts, and repositions.
/// When friendly, can be recruited to follow a player and engages hostile NPCs instead.
/// </summary>
public class CombatNpc : Npc, Component.IPressable
{
	private static readonly string[] PainLines =
	{
		"Argh!",
		"They got me!",
		"I'm hit!",
		"Taking fire!",
		"Ugh!",
	};

	private static readonly string[] DeathLines =
	{
		"Tell them... I fought...",
		"Not like this...",
		"I can't...",
	};

	/// <summary>
	/// When true, this NPC is friendly to players and will follow them, engaging hostile NPCs.
	/// When false, this NPC targets players and friendly NPCs.
	/// </summary>
	[Property, Sync]
	public bool Friendly { get; set; } = false;

	/// <summary>
	/// The weapon this NPC uses to attack. Its NPC usage decides the engagement range and burst
	/// cadence - the same soldier fights differently with a shotgun than an SMG.
	/// </summary>
	[Property]
	public BaseSandboxWeapon Weapon { get; set; }

	/// <summary>
	/// Melee weapon prefabs, one of which this NPC carries as well (picked at random when it spawns). It switches to
	/// it when its gun is empty and it has no spare magazines left. Empty means no melee weapon. Declared before <see cref="WeaponChoices"/>:
	/// arming runs when that one is loaded, so this one has to be loaded first.
	/// </summary>
	[Property]
	public List<GameObject> MeleeChoices { get; set; } = [];

	/// <summary>
	/// Weapon prefabs to arm this NPC with - one is picked at random when it spawns, replacing
	/// <see cref="Weapon"/>. Empty keeps the prefab's weapon.
	/// </summary>
	[Property]
	public List<GameObject> WeaponChoices { get; set; } = [];

	/// <summary>
	/// The carried melee weapon, cloned from <see cref="MeleeChoices"/> when the NPC spawns.
	/// </summary>
	[Property]
	public BaseSandboxWeapon Melee { get; set; }

	/// <summary>
	/// Full magazines carried besides the one in the gun. Each reload uses one; with none left and the gun empty the
	/// NPC switches to its melee weapon.
	/// </summary>
	[Property, Group( "Balance" )]
	public int SpareMagazines { get; set; } = 2;

	/// <summary>
	/// How far this NPC looks for ammo and loose weapons once it is out of ammo.
	/// </summary>
	[Property, Group( "Balance" )]
	public float PickupRange { get; set; } = 900f;

	/// <summary>
	/// True while the melee weapon is in hand instead of the gun. Set by the host, followed on every peer.
	/// </summary>
	[Sync( SyncFlags.FromHost ), Change( nameof(OnUsingMeleeChanged) )]
	public bool UsingMelee { get; private set; }

	// The gun the NPC was armed with. Weapon is whichever of the gun and the melee weapon is in hand.
	private BaseSandboxWeapon _gun;

	// Pickups it could not reach or use, and when to look at them again
	private readonly Dictionary<GameObject, TimeUntil> _ignoredPickups = new();

	/// <summary>
	/// Skill multiplier on the weapon's NPC spread - 1 is a average shot, lower is more accurate.
	/// </summary>
	[Property, Group( "Balance" )]
	public float AimSpreadScale { get; set; } = 1f;

	/// <summary>
	/// Movement speed while engaging an enemy.
	/// </summary>
	[Property, Group( "Balance" ), Range( 90, 250f ), Step( 1 ), Sync]
	public float EngageSpeed { get; set; } = 180f;

	/// <summary>
	/// How far this NPC sees a hostile in the open.
	/// </summary>
	[Property, Group( "Balance" )]
	public float SightRange { get; set; } = 1500f;

	/// <summary>
	/// Walking pace while patrolling.
	/// </summary>
	[Property, Group( "Balance" )]
	public float PatrolSpeed { get; set; } = 85f;

	/// <summary>
	/// Cautious walking pace - sweeping for a hostile we've lost, checking out a noise, strafing to a
	/// new position. Kept to a walk; only fighting and hot pursuit break into a jog.
	/// </summary>
	[Property, Group( "Balance" )]
	public float SearchSpeed { get; set; } = 85f;

	/// <summary>
	/// Each soldier moves up to this much faster or slower than the rest (0.05 = ±5%), so a squad
	/// doesn't move in lockstep. Keep it small - too much pushes a walk into a jog.
	/// </summary>
	[Property, Group( "Balance" )]
	public float SpeedVariation { get; set; } = 0.05f;

	// This soldier's own pace, from SpeedVariation
	private float _speedScale = 1f;

	private float Pace( float speed ) => speed * _speedScale;

	/// <summary>
	/// How long after the last intel on a hostile (seen, heard or called out) to keep searching for it.
	/// </summary>
	[Property, Group( "Balance" )]
	public float SearchTimeout { get; set; } = 8f;

	/// <summary>
	/// Allies of the same faction within this range are told where a spotted hostile is.
	/// </summary>
	[Property, Group( "Balance" )]
	public float CalloutRange { get; set; } = 1500f;

	/// <summary>
	/// Wander between random points when there's nothing going on, instead of holding a post.
	/// </summary>
	[Property, Group( "Balance" )]
	public bool Patrols { get; set; }

	[Property, Group( "Balance" )]
	public float PatrolRadius { get; set; } = 400f;

	/// <summary>
	/// How long the weapon stays up after the last sign of trouble before the NPC relaxes.
	/// </summary>
	[Property, Group( "Balance" )]
	public float RelaxDelay { get; set; } = 5f;

	/// <summary>
	/// Seconds (min, max) to react to a fresh contact when caught at ease - weapon lowered.
	/// </summary>
	[Property, Group( "Balance" )]
	public Vector2 ReactionTimeRelaxed { get; set; } = new( 1.0f, 1.6f );

	/// <summary>
	/// Seconds (min, max) to react to a fresh contact when already alert - weapon up, searching or fighting.
	/// </summary>
	[Property, Group( "Balance" )]
	public Vector2 ReactionTimeAlert { get; set; } = new( 0.5f, 0.9f );

	/// <summary>
	/// Seconds a weapon dropped on death lies around before it's removed, if nobody picks it up.
	/// </summary>
	[Property, Group( "Balance" )]
	public float DroppedWeaponLifetime { get; set; } = 20f;

	/// <summary>
	/// How far a friendly NPC will follow a player before stopping.
	/// </summary>
	[Property, Group( "Balance" )]
	public float FollowDistance { get; set; } = 150f;

	/// <summary>
	/// The player this friendly NPC is following, if any.
	/// </summary>
	[Sync]
	public GameObject Leader { get; set; }

	/// <summary>
	/// Where we last saw, heard or were told a hostile was. Null when we've got nothing to go on.
	/// </summary>
	public Vector3? LastKnownPosition { get; private set; }

	/// <summary>
	/// Which way that hostile was moving, so a search can head where it was going.
	/// </summary>
	public Vector3 LastKnownVelocity { get; private set; }

	/// <summary>
	/// Time since <see cref="LastKnownPosition"/> was last updated.
	/// </summary>
	public TimeSince TimeSinceIntel { get; private set; }

	private TimeSince _timeSinceCallout;

	// The post we hold when nothing's going on - where and which way we were spawned
	private Vector3 _post;
	private Vector3 _postForward;

	private TimeSince _timeSinceAlert;

	// Contact tracking: when we last saw a hostile, and when the current stretch of contact began
	private TimeSince _timeSinceSawHostile;
	private TimeSince _timeSinceContact;
	private float _contactReaction;

	// The last hostile we saw and where, so we notice when it's dead - and where its body is
	private GameObject _lastSeenHostile;
	private Vector3 _lastSeenPosition;
	private Vector3? _bodyPosition;
	private TimeSince _timeSinceKill;

	IPressable.Tooltip? IPressable.GetTooltip( IPressable.Event e )
	{
		if ( !Friendly )
			return null;

		return Leader.IsValid()
			? new IPressable.Tooltip( "Stop following", "person_off", null )
			: new IPressable.Tooltip( "Follow me", "follow_the_signs", null );
	}

	bool IPressable.CanPress( IPressable.Event e ) => Friendly;

	bool IPressable.Press( IPressable.Event e )
	{
		ToggleFollow( e.Source.GameObject );
		return true;
	}

	[Rpc.Host]
	private void ToggleFollow( GameObject presserObject )
	{
		if ( !Friendly || !presserObject.IsValid() )
			return;

		var player = presserObject.Root.GetComponent<PlayerController>();
		if ( !player.IsValid() )
			return;

		Leader = Leader == player.GameObject ? null : player.GameObject;
		EndCurrentSchedule();
	}

	protected override void OnValidate()
	{
		base.OnValidate();

		// Runs when the NPC is cloned in, before it's network spawned - so the chosen gun goes out in
		// its spawn snapshot and every client sees the same one. Not in the editor or the cached
		// prefab itself, not on clients.
		if ( Game.IsPlaying && Scene.IsValid() && !Scene.IsEditor && Scene is not PrefabScene && Networking.IsHost && !GameObject.Network.Active )
		{
			ArmWithRandomWeapon();
			ArmWithMelee();
		}
	}

	/// <summary>
	/// Clone one of <see cref="MeleeChoices"/> onto the NPC, holstered.
	/// </summary>
	private void ArmWithMelee()
	{
		if ( MeleeChoices is not { Count: > 0 } )
			return;

		var prefab = MeleeChoices[Game.Random.Int( 0, MeleeChoices.Count - 1 )];
		if ( !prefab.IsValid() )
			return;

		var go = prefab.Clone( new CloneConfig { Parent = GameObject, Transform = global::Transform.Zero, StartEnabled = true } );
		var weapon = go?.GetComponent<BaseSandboxWeapon>( true );
		if ( !weapon.IsValid() )
		{
			go?.Destroy();
			Log.Warning( $"{DisplayName}: '{prefab.Name}' isn't a weapon prefab, no melee weapon." );
			return;
		}

		Melee?.GameObject.DestroyImmediate();
		Melee = weapon;
	}

	/// <summary>
	/// Put the melee weapon in hand. Host only; the gun is not used again.
	/// </summary>
	private void UseMelee()
	{
		if ( IsProxy || UsingMelee || !Melee.IsValid() )
			return;

		UsingMelee = true;
		ApplyHeld();
	}

	/// <summary>
	/// Back to the gun, after finding ammo for it. Host only.
	/// </summary>
	private void UseGun()
	{
		if ( IsProxy || !UsingMelee || !_gun.IsValid() )
			return;

		UsingMelee = false;
		ApplyHeld();
	}

	private void OnUsingMeleeChanged( bool oldValue, bool newValue ) => ApplyHeld();

	/// <summary>
	/// Make the world model, hold type and <see cref="Weapon"/> match <see cref="UsingMelee"/>, on this peer.
	/// </summary>
	private void ApplyHeld( bool rethink = true )
	{
		// With no gun the melee weapon is all there is
		var melee = UsingMelee || !_gun.IsValid();
		var held = melee ? Melee : _gun;
		var other = melee ? _gun : Melee;

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
	/// Swap the prefab's weapon for a random one from <see cref="WeaponChoices"/>.
	/// </summary>
	private void ArmWithRandomWeapon()
	{
		if ( WeaponChoices is not { Count: > 0 } )
			return;

		var prefab = WeaponChoices[Game.Random.Int( 0, WeaponChoices.Count - 1 )];
		if ( !prefab.IsValid() )
			return;

		var go = prefab.Clone( new CloneConfig { Parent = GameObject, Transform = global::Transform.Zero, StartEnabled = true } );
		var weapon = go?.GetComponent<BaseSandboxWeapon>( true );
		if ( !weapon.IsValid() )
		{
			go?.Destroy();
			Log.Warning( $"{DisplayName}: '{prefab.Name}' isn't a weapon prefab, keeping the default weapon." );
			return;
		}

		Weapon?.GameObject.DestroyImmediate();
		Weapon = weapon;
	}

	protected override void OnStart()
	{
		base.OnStart();

		_speedScale = 1f + Game.Random.Float( -SpeedVariation, SpeedVariation );
		Senses.SightRange = SightRange;

		_post = WorldPosition;
		_postForward = WorldRotation.Forward;

		_gun = Weapon;

		// Found on a client by what the spawn snapshot brought
		if ( !Melee.IsValid() )
			Melee = Components.GetAll<BaseSandboxWeapon>( FindMode.EverythingInChildren ).FirstOrDefault( x => x is MeleeWeapon );

		// Holstered: no pickup model or physics until it is dropped
		if ( !IsProxy && Melee.IsValid() )
			Melee.IsDropped = false;

		// Armed with only a melee weapon: it is in hand from the start
		if ( !_gun.IsValid() && Melee.IsValid() )
		{
			if ( !IsProxy )
				UsingMelee = true;

			ApplyHeld();
		}

		if ( _gun.IsValid() && Renderer.IsValid() )
		{
			Weapon.CreateWorldModel( Renderer );

			// The weapon says how forgiving it is in NPC hands; we say how good we are with it.
			Weapon.SpreadScale = Weapon.Npc.SpreadScale * AimSpreadScale;

			// A gun nobody was given through an inventory has no magazine and never runs dry. Load it, so the
			// NPC has to reload like anyone else (see ReloadWeapon).
			if ( !IsProxy && Weapon.UsesPrimaryClip )
				Weapon.Clip1 = Weapon.PrimaryClipSize;

			if ( !IsProxy )
				Animation.SetHoldType( Weapon.HoldType );
		}
	}

	// Friendly soldiers fight alongside the player; the rest are enemies.
	public override string Faction => Friendly ? Factions.Ally : Factions.Enemy;

	protected override void SetupRelationships()
	{
		if ( Friendly )
		{
			Likes( Factions.Player );
			Hates( Factions.Enemy, Factions.Monster );
		}
		else
		{
			Hates( Factions.Player, Factions.Ally, Factions.Citizen );
		}
	}

	/// <summary>
	/// Remember where a hostile is (and which way it's heading).
	/// </summary>
	public void RememberHostile( Vector3 position, Vector3 velocity )
	{
		LastKnownPosition = position;
		LastKnownVelocity = velocity;
		TimeSinceIntel = 0;
	}

	/// <summary>
	/// Keep the last known position fresh from everything we sense, every think -- not just when a
	/// fight starts -- and tell nearby allies about anything we can see.
	/// </summary>
	protected override NpcAwareness GatherAwareness()
	{
		var awareness = base.GatherAwareness();

		TrackKill();

		var seen = Senses.GetBestTarget();
		if ( seen.IsValid() )
		{
			// First sight after a gap is a fresh contact - work out how long it takes us to react
			if ( _timeSinceSawHostile > 3f )
			{
				_timeSinceContact = 0;
				_contactReaction = GetReactionTime( seen );
			}

			_timeSinceSawHostile = 0;
			_lastSeenHostile = seen;
			_lastSeenPosition = seen.WorldPosition;
			RememberHostile( seen.WorldPosition, SensesLayer.GetVelocity( seen ) );
			CallOut( seen );
			return awareness;
		}

		if ( Senses.GetHeardHostile() is { } heard && heard.IsValid() )
		{
			RememberHostile( heard.WorldPosition, SensesLayer.GetVelocity( heard ) );
		}
		else if ( Senses.Disturbance is { } noise && noise.Source.IsValid() && GetDisposition( noise.Source ) == Disposition.Hostile )
		{
			// Heard a hostile shoot -- we know roughly where they are, not where they're going
			if ( TimeSinceIntel > 1f )
				RememberHostile( noise.Position, Vector3.Zero );
		}

		// Fresh intel (heard, or an ally's callout) gets an idle NPC moving
		if ( LastKnownPosition.HasValue && TimeSinceIntel < 0.5f )
			awareness |= NpcAwareness.HostileIntel;

		return awareness;
	}

	/// <summary>
	/// The last hostile we saw is gone - dead. If we were in the fight, remember where the body is so
	/// we can walk over and check, and stop hunting it.
	/// </summary>
	private void TrackKill()
	{
		if ( _lastSeenHostile is null || _lastSeenHostile.IsValid() )
			return;

		_lastSeenHostile = null;

		if ( _timeSinceSawHostile > 10f )
			return;

		_bodyPosition = _lastSeenPosition;
		_timeSinceKill = 0;

		// Intel that still points at it is stale - it's not going anywhere
		if ( LastKnownPosition is { } lastKnown && lastKnown.Distance( _lastSeenPosition ) < 200f )
			LastKnownPosition = null;
	}

	/// <summary>
	/// How long it takes to react to spotting a hostile: longer when caught at ease (the weapon has
	/// to come up) than when already alert, plus a little more to pick out something far away.
	/// </summary>
	private float GetReactionTime( GameObject hostile )
	{
		var range = Animation.WeaponLowered ? ReactionTimeRelaxed : ReactionTimeAlert;
		var distance = WorldPosition.Distance( hostile.WorldPosition );

		return Game.Random.Float( range.x, range.y ) + MathF.Min( 0.6f, distance / 2500f );
	}

	/// <summary>
	/// Tell allies of our faction nearby where a hostile is. They'll come and help search, or join
	/// the fight once they see it themselves.
	/// </summary>
	private void CallOut( GameObject hostile )
	{
		if ( _timeSinceCallout < 1f ) return;
		_timeSinceCallout = 0;

		var velocity = SensesLayer.GetVelocity( hostile );

		foreach ( var ally in Scene.GetAll<CombatNpc>() )
		{
			if ( ally == this || ally.Faction != Faction ) continue;
			if ( ally.WorldPosition.Distance( WorldPosition ) > CalloutRange ) continue;

			// They can see something themselves -- don't distract them
			if ( ally.Senses.IsValid() && ally.Senses.VisibleTargets.Count > 0 ) continue;

			ally.RememberHostile( hostile.WorldPosition, velocity );
		}
	}

	public override ScheduleBase GetSchedule()
	{
		var visible = Senses.GetBestTarget();

		if ( IsOutOfAmmo )
		{
			// Ammo or a loose weapon close by is worth fetching - unless the enemy is nearer than it is
			var pickup = FindPickup();
			if ( pickup.IsValid() && !(visible.IsValid() && WorldPosition.Distance( visible.WorldPosition ) < WorldPosition.Distance( pickup.WorldPosition )) )
			{
				var fetch = GetSchedule<CombatPickupSchedule>();
				fetch.Pickup = pickup;
				fetch.Speed = Pace( EngageSpeed );
				return fetch;
			}

			// Nothing to be had: hit them with the melee weapon instead
			if ( !UsingMelee && Melee.IsValid() )
				UseMelee();
		}
		else if ( UsingMelee )
		{
			// Found some ammo
			UseGun();
		}

		if ( visible.IsValid() && Weapon.IsValid() )
		{
			var engage = GetSchedule<CombatEngageSchedule>();
			engage.Target = visible;
			engage.Weapon = Weapon;
			engage.EngageSpeed = Pace( EngageSpeed );

			// Whatever's left of the reaction to a fresh contact - nothing once a fight's under way
			engage.ReactionTime = MathF.Max( 0f, _contactReaction - _timeSinceContact );
			engage.StrafeSpeed = Pace( SearchSpeed );
			return engage;
		}

		// Got them - walk over and check the body. No hurry, the fight's over.
		if ( _bodyPosition is { } body && _timeSinceKill < 20f && WorldPosition.Distance( body ) > 100f )
		{
			var check = GetSchedule<InvestigateSchedule>();
			check.Target = body;
			check.Speed = Pace( PatrolSpeed );
			return check;
		}

		// Lost them, heard them, or been told where they are -- go hunt. Jog after someone we've only
		// just lost sight of; otherwise it's a careful walk.
		if ( LastKnownPosition.HasValue && TimeSinceIntel < SearchTimeout && Weapon.IsValid() )
		{
			var search = GetSchedule<CombatSearchSchedule>();
			var hotPursuit = _lastSeenHostile.IsValid() && _timeSinceSawHostile < 5f;
			search.Speed = hotPursuit ? Pace( EngageSpeed ) : Pace( SearchSpeed );
			search.SweepSpeed = Pace( SearchSpeed );
			return search;
		}

		// Heard gunfire or some other disturbance but can't see the source -- go check it out.
		if ( Senses.Disturbance is { } disturbance )
		{
			var investigate = GetSchedule<InvestigateSchedule>();
			investigate.Target = disturbance.Position;
			investigate.Speed = Pace( SearchSpeed );
			return investigate;
		}

		// Combat and investigation above take priority; once clear, resume following our leader.
		if ( Friendly && Leader.IsValid() )
		{
			var follow = GetSchedule<FollowSchedule>();
			follow.Target = Leader;
			follow.FollowDistance = FollowDistance;
			return follow;
		}

		// Nothing going on - patrol if we're a patroller, otherwise hold our post
		if ( Patrols )
		{
			var patrol = GetSchedule<CombatPatrolSchedule>();
			patrol.PatrolRadius = PatrolRadius;
			patrol.Speed = Pace( PatrolSpeed );
			return patrol;
		}

		var idle = GetSchedule<CombatIdleSchedule>();
		idle.Post = _post;
		idle.PostForward = _postForward;
		idle.WalkSpeed = Pace( PatrolSpeed );
		return idle;
	}

	protected override void OnPutDown()
	{
		base.OnPutDown();

		// Someone put us here and turned us this way - this is our post now
		_post = WorldPosition;
		_postForward = WorldRotation.Forward;
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		if ( IsProxy )
			return;

		// Weapon up while anything's going on (searching, fighting) and for a while after - at ease otherwise
		if ( ActiveSchedule is { Priority: >= SchedulePriority.Alert } )
			_timeSinceAlert = 0;

		Animation.WeaponLowered = _timeSinceAlert > RelaxDelay;
	}

	protected override void OnHurt( in DamageInfo damage )
	{
		if ( damage.Attacker.IsValid() )
		{
			// Turn on whoever hurt us, even a former ally, and prioritise them as a target.
			// This is the runtime "turn nasty" path the crime/aggro systems reuse.
			SetDisposition( damage.Attacker, Disposition.Hostile, priority: 10 );

			// You can tell which way the shot came from -- go after whoever fired it.
			RememberHostile( damage.Attacker.WorldPosition, SensesLayer.GetVelocity( damage.Attacker ) );
		}

		if ( Health >= 1f && Speech.CanSpeak && Game.Random.Float() < 0.5f )
			Speech.TrySay( Game.Random.FromArray( PainLines ), 1.5f, tags: ["reaction", "pain"], priority: 2000 );

		// React immediately.
		EndCurrentSchedule();
	}

	protected override void Die( in DamageInfo damage )
	{
		if ( Speech.CanSpeak )
			Speech.TrySay( Game.Random.FromArray( DeathLines ), 2f, tags: ["reaction", "death"], priority: 3000 );

		DropWeapon();

		base.Die( damage );
	}

	/// <summary>
	/// Drop the held weapon into the world where the hand was, so it survives the NPC's death and can
	/// be picked up. If nobody takes it within <see cref="DroppedWeaponLifetime"/> it's cleaned up.
	/// </summary>
	/// <summary>
	/// Without a gun, or out of ammo on it with no spare magazines to reload from. An NPC that spawned without a gun is
	/// just one that has not found one yet.
	/// </summary>
	private bool IsOutOfAmmo => !_gun.IsValid() || (ReloadWeapon.IsEmpty( _gun ) && SpareMagazines <= 0);

	/// <summary>
	/// The nearest ammo box for the gun in hand, or loose weapon it can use, within <see cref="PickupRange"/>.
	/// </summary>
	private GameObject FindPickup()
	{
		GameObject best = null;
		var bestDistance = PickupRange;

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

		// Ammo is only any use for a gun it already has
		if ( _gun.IsValid() )
		{
			foreach ( var ammo in Scene.GetAll<AmmoPickup>() )
			{
				if ( ammo.AmmoType is not null && ammo.AmmoType == _gun.PrimaryAmmoType )
					Consider( ammo.GameObject );
			}
		}

		foreach ( var weapon in Scene.GetAll<BaseSandboxWeapon>() )
		{
			if ( CanTake( weapon ) )
				Consider( weapon.GameObject );
		}

		return best;
	}

	/// <summary>
	/// A loose gun, with rounds in it, that this NPC can use.
	/// </summary>
	private bool CanTake( BaseSandboxWeapon weapon )
	{
		return weapon.IsValid() && weapon != _gun && weapon != Melee
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
	/// Take an ammo box (counted as spare magazines for the gun) or a loose weapon (which becomes the gun, the old one
	/// is dropped). Host only. False if it could not be taken.
	/// </summary>
	public bool Collect( GameObject pickup )
	{
		if ( IsProxy || !pickup.IsValid() )
			return false;

		// As close as a player has to be to use it
		var reach = pickup.Components.Get<Interactable>( FindMode.EnabledInSelfAndDescendants )?.Range ?? 80f;
		if ( pickup.WorldPosition.Distance( WorldPosition ) > MathF.Max( reach, 60f ) )
			return false;

		if ( _gun.IsValid() && pickup.GetComponent<AmmoPickup>() is { } ammo )
		{
			var rounds = ammo.TakeByNpc( _gun );
			if ( rounds <= 0 )
				return false;

			SpareMagazines += Math.Max( 1, MathX.CeilToInt( rounds / (float)Math.Max( _gun.PrimaryClipSize, 1 ) ) );
			return true;
		}

		if ( pickup.GetComponent<BaseSandboxWeapon>() is { } weapon && CanTake( weapon ) )
		{
			var previous = _gun;

			// Never fired, or from a magazine counter that was never set: a full one. One spare magazine with it.
			if ( weapon.Clip1 < 0 )
				weapon.Clip1 = weapon.PrimaryClipSize;

			SpareMagazines = Math.Max( SpareMagazines, 1 );

			weapon.GameObject.SetParent( GameObject, false );
			weapon.LocalTransform = global::Transform.Zero;

			UsingMelee = false;
			AdoptWeapon( weapon, previous );

			// The empty one goes on the floor, where the NPC will not want it back
			DropWeapon( previous );
			return true;
		}

		return false;
	}

	/// <summary>
	/// Make this the NPC's gun on every peer: the world model, hold type and aim.
	/// </summary>
	[Rpc.Broadcast]
	private void AdoptWeapon( BaseSandboxWeapon weapon, BaseSandboxWeapon previous )
	{
		if ( !weapon.IsValid() )
			return;

		if ( previous.IsValid() && previous.WorldModel.IsValid() )
			previous.WorldModel.Destroy();

		_gun = weapon;
		weapon.SpreadScale = weapon.Npc.SpreadScale * AimSpreadScale;

		ApplyHeld( rethink: false );
	}

	private void DropWeapon()
	{
		DropWeapon( _gun );
		DropWeapon( Melee );
	}

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

	/// <summary>
	/// Remove a gun we dropped, unless a player has picked it up since. A player who picked it up and
	/// dropped it again owns it now, so that drop is theirs to keep.
	/// </summary>
	private static void RemoveIfUnclaimed( BaseSandboxWeapon weapon )
	{
		if ( !weapon.IsValid() || weapon.HasOwner || !weapon.IsDropped )
			return;

		weapon.GameObject.Destroy();
	}
}
