namespace Sandbox;

/// <summary>
/// A box of ammo lying in the world. The player looks at it and presses Use to take the reserve ammo for a weapon; it is
/// not picked up by walking into it. The host gives the ammo, so a client's press is checked there.
/// </summary>
public sealed class AmmoPickup : Component, IInteractionHandler
{
	/// <summary>
	/// The sound to play when picking up this item
	/// </summary>
	[Property] public SoundEvent PickupSound { get; set; }

	/// <summary>
	/// The ammo resource this pickup gives ammo for. It goes straight to the player's shared pool for that resource.
	/// </summary>
	[Property, Group( "Ammo" )] public BaseAmmoResource AmmoType { get; set; }

	/// <summary>
	/// The quantity of ammo to give.
	/// </summary>
	[Property, Group( "Ammo" )] public int AmmoAmount { get; set; }

	protected override void OnStart()
	{
		if ( !Game.IsPlaying )
			return;

		_ = Components.Get<Interactable>( FindMode.EnabledInSelfAndDescendants )
			?? GameObject.Components.Create<Interactable>();

		InteractionSetup.EnsureOption( this, "take", "Take", "inventory_2", "" );
	}

	InteractionInfo? IInteractionHandler.Describe( InteractionEvent e )
	{
		var title = AmmoType?.Title;
		var label = AmmoAmount > 0 ? $"Take {AmmoAmount} {title}" : $"Take {title}";
		return new InteractionInfo( label.Trim(), "inventory_2" );
	}

	bool IInteractionHandler.CanInteract( InteractionEvent e )
	{
		if ( AmmoType is null || !e.Interactor.IsValid() )
			return false;

		var inventory = e.Interactor.GetComponent<WeaponInventory>();
		return inventory.IsValid() && inventory.GetAmmo( AmmoType ) < AmmoType.MaxReserve;
	}

	void IInteractionHandler.OnInteract( InteractionEvent e )
	{
		if ( !Networking.IsHost || GameObject.IsDestroyed || AmmoType is null )
			return;

		var inventory = e.Interactor.IsValid() ? e.Interactor.GetComponent<WeaponInventory>() : null;
		if ( !inventory.IsValid() )
			return;

		// GiveAmmo clamps to the resource's max and reports what actually went in.
		if ( inventory.GiveAmmo( AmmoType, AmmoAmount ) <= 0 )
			return;

		PlayPickupEffects( e.Interactor );
		GameObject.Destroy();
	}

	/// <summary>
	/// An NPC takes the box. It only wants ammo for the gun it holds, and has no inventory to put it in, so this gives
	/// back how many rounds there were and the NPC counts them itself. Host only. Zero if it was not taken.
	/// </summary>
	public int TakeByNpc( BaseCombatWeapon gun )
	{
		if ( !Networking.IsHost || GameObject.IsDestroyed || AmmoType is null || !gun.IsValid() || gun.PrimaryAmmoType != AmmoType )
			return 0;

		PlayPickupEffects( null );
		GameObject.Destroy();

		return Math.Max( AmmoAmount, 1 );
	}

	[Rpc.Broadcast]
	void PlayPickupEffects( GameObject player )
	{
		if ( Application.IsDedicatedServer )
			return;

		var snd = GameObject.PlaySound( PickupSound );
		if ( !snd.IsValid() )
			return;

		if ( player.IsValid() && !player.IsProxy )
			snd.SpacialBlend = 0;
	}
}
