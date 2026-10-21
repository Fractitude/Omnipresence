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

	PlayerController controller;

	protected override void OnStart()
	{
		controller = GetComponent<PlayerController>();

		if ( UsesLoadout && !GiveOnStart && Networking.IsHost && !IsProxy )
			_ = GiveLoadoutWhenReady();
	}

	/// <summary>
	/// Hands out the loadout once the player controller has finished building the player. The engine's own
	/// <c>GiveOnStart</c> made the controller throw a NullReferenceException while it was being created, so leave
	/// it off on the player prefab and turn <c>UsesLoadout</c> on.
	/// </summary>
	async Task GiveLoadoutWhenReady()
	{
		await GameTask.DelaySeconds( 0.1f );

		if ( this.IsValid() && !Items.Any() )
			GiveLoadout();
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		if ( IsProxy || !controller.IsValid() ) return;

		ReadInput();

		var weapon = ActiveWeapon;
		if ( !weapon.IsValid() ) return;

		if ( Input.Pressed( "Jump" ) && controller.IsOnGround )
			weapon.OnJump();
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
