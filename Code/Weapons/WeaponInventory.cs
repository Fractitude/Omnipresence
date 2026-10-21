using Sandbox.Movement;

namespace Sandbox;

/// <summary>
/// The weapons a player carries, in hotbar slots. Put it on the player's <see cref="PlayerController"/>
/// GameObject. Holding, switching, ammo and drops are the engine <see cref="BaseInventoryComponent"/>;
/// this adds the hotbar input and tells the held weapon about jumps.
/// <para>
/// This is separate from <see cref="PlayerInventory"/>, the grid of carried items.
/// </para>
/// </summary>
public sealed class WeaponInventory : BaseInventoryComponent
{
	/// <summary>
	/// The weapon the player holds, or null.
	/// </summary>
	public BaseSandboxWeapon ActiveWeapon => ActiveItem as BaseSandboxWeapon;

	/// <summary>
	/// All weapons in the inventory, ordered by slot.
	/// </summary>
	public IEnumerable<BaseSandboxWeapon> Weapons => Items.OfType<BaseSandboxWeapon>();

	/// <summary>
	/// True while the player sits in a seat with their weapons put away. They can't draw one until they get up, and
	/// the weapon they held comes back then. A listener of <see cref="ISeatHolsterEvents"/> can keep the weapons out.
	/// </summary>
	public bool IsSeatHolstered { get; private set; }

	PlayerController controller;
	bool wasSeated;
	ISitTarget seat;
	BaseSandboxWeapon weaponBeforeSeat;

	protected override void OnStart()
	{
		controller = GetComponent<PlayerController>();

		// Without this move mode a player in a seat keeps walking: no sitting pose, and the eyes stay where they were.
		// Added here for the same reason as the loadout: the player prefab is never edited.
		if ( !GetComponent<SitMoveMode>( true ).IsValid() )
			AddComponent<SitMoveMode>();

		if ( Networking.IsHost && !IsProxy )
			_ = GiveLoadoutWhenReady();
	}

	/// <summary>
	/// The weapons a player spawns with, by name, separated by spaces: <c>m4a1 glock crowbar</c> gives
	/// <c>weapons/m4a1/m4a1.prefab</c> and so on. Saved, so set it once from the console. It lives here and not on the
	/// player prefab because editing <see cref="PlayerController"/> in the Inspector makes it throw.
	/// </summary>
	[ConVar( "omni.loadout", ConVarFlags.Saved )]
	public static string Loadout { get; set; } = "m4a1 glock crowbar physgun";

	/// <summary>
	/// The prefab of a weapon, from its short name like <c>m4a1</c>.
	/// </summary>
	public static string PrefabPath( string weapon ) => $"weapons/{weapon.ToLowerInvariant()}/{weapon.ToLowerInvariant()}.prefab";

	/// <summary>
	/// Hands out <see cref="Loadout"/> once the player controller has finished building the player. The engine's own
	/// <c>GiveOnStart</c> made the controller throw a NullReferenceException while it was being created, so the
	/// prefab leaves it off, and its <c>StartingItems</c> stay empty.
	/// </summary>
	async Task GiveLoadoutWhenReady()
	{
		await GameTask.DelaySeconds( 0.1f );

		if ( !this.IsValid() || Items.Any() )
			return;

		foreach ( var weapon in Loadout.Split( ' ', StringSplitOptions.RemoveEmptyEntries ) )
		{
			Pickup( PrefabPath( weapon ) );
		}
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		// A weapon writes its hold pose while it is active. With none in hand the last one sticks (a pistol grip while
		// holding a steering wheel), so empty hands are reset explicitly.
		if ( controller.IsValid() && controller.Renderer.IsValid() && !ActiveWeapon.IsValid() )
		{
			controller.Renderer.Set( "holdtype", "none" );
			controller.Renderer.Set( "holdtype_handedness", 0 );
		}

		if ( IsProxy || !controller.IsValid() ) return;

		// Sitting down at a computer puts the weapon away. It stays away after getting up, and no slot key
		// draws one while seated.
		var seated = Scene.GetAllComponents<NaviComputer>().Any( x => x.IsSeated );
		if ( seated && !wasSeated && ActiveWeapon.IsValid() )
			ForceHolster();

		wasSeated = seated;

		UpdatePerspective();

		if ( seated ) return;

		UpdateSeat();
		if ( IsSeatHolstered ) return;

		ReadInput();

		var weapon = ActiveWeapon;
		if ( !weapon.IsValid() ) return;

		if ( Input.Pressed( "Jump" ) && controller.IsOnGround )
			weapon.OnJump();
	}

	/// <summary>
	/// Sitting in a seat (a vehicle's, say) puts the weapon away, and getting out brings it back.
	/// </summary>
	void UpdateSeat()
	{
		var current = GetComponentInParent<ISitTarget>( false );
		if ( current == seat ) return;
		seat = current;

		if ( IsSeatHolstered )
		{
			var weapon = weaponBeforeSeat;
			weaponBeforeSeat = null;
			IsSeatHolstered = false;

			if ( weapon.IsValid() && weapon.Inventory == this )
				Switch( weapon );
		}

		if ( seat is Component seatComponent )
			HolsterForSeat( seatComponent.GameObject );
	}

	void HolsterForSeat( GameObject seatObject )
	{
		var holster = new SeatHolsterEvent { Player = controller, Seat = seatObject, Weapon = ActiveWeapon };
		ISeatHolsterEvents.PostToGameObject( GameObject, x => x.OnSeatHolster( holster ) );
		ISeatHolsterEvents.Post( x => x.OnSeatHolster( holster ) );

		if ( holster.Cancelled )
			return;

		weaponBeforeSeat = ActiveWeapon;
		IsSeatHolstered = true;
		ForceHolster();
	}

	/// <summary>
	/// First person draws the arms and the weapon on the camera (tagged <c>firstperson</c>), third person the player's
	/// own body with the world model. In third person the camera leaves the <c>firstperson</c> tag out and the view
	/// model is dropped, so effects start at the weapon in the hands and not at one floating behind the camera.
	/// </summary>
	void UpdatePerspective()
	{
		var camera = Scene.Camera;
		var thirdPerson = controller.ThirdPerson;

		if ( camera.IsValid() )
		{
			if ( thirdPerson )
				camera.RenderExcludeTags.Add( "firstperson" );
			else
				camera.RenderExcludeTags.Remove( "firstperson" );
		}

		var weapon = ActiveWeapon;
		if ( !weapon.IsValid() ) return;

		weapon.SetViewModelShown( !thirdPerson );
	}

	void ReadInput()
	{
		if ( Input.Pressed( "Drop" ) && ActiveWeapon.IsValid() )
			DropActiveWeapon();

		for ( var i = 0; i < 10; i++ )
		{
			if ( Input.Pressed( $"Slot{i}" ) )
				SelectSlot( i == 0 ? 9 : i - 1 );
		}

		if ( Input.Pressed( "SlotNext" ) ) CycleSlot( 1 );
		if ( Input.Pressed( "SlotPrev" ) ) CycleSlot( -1 );
	}

	void SelectSlot( int slot )
	{
		var weapon = GetSlot( slot ) as BaseSandboxWeapon;
		if ( weapon.IsValid() )
			Switch( weapon, allowHolster: true );
	}

	void CycleSlot( int direction )
	{
		var current = ActiveWeapon.IsValid() ? ActiveWeapon.Slot : -1;

		for ( var step = 1; step <= MaxSlots; step++ )
		{
			var slot = ((current + direction * step) % MaxSlots + MaxSlots) % MaxSlots;
			if ( GetSlot( slot ) is not BaseSandboxWeapon weapon ) continue;

			Switch( weapon );
			return;
		}
	}

	/// <summary>
	/// Called by the owning client to drop the held weapon.
	/// </summary>
	[Rpc.Host]
	void DropActiveWeapon()
	{
		if ( ActiveWeapon.IsValid() )
			Drop( ActiveWeapon );
	}
}
