namespace Sandbox;

/// <summary>
/// A pickup that gives the player some health. The player looks at it and presses Use; walking into it does nothing.
/// </summary>
public sealed class HealthPickup : Component, IInteractionHandler
{
	/// <summary>
	/// The sound to play when picking up this item
	/// </summary>
	[Property] public SoundEvent PickupSound { get; set; }

	/// <summary>
	/// How much health to give to the player
	/// </summary>
	[Property, Group( "Health" )] float HealthGive { get; set; } = 0;

	bool CanPickup( Player player ) => player.Health < player.MaxHealth || HealthGive <= 0;

	protected override void OnStart()
	{
		if ( !Game.IsPlaying )
			return;

		// The model's collider is small next to the ammo boxes' 32 unit one, and the range counts from the collider's surface.
		// The extra reach makes the pickup usable from as far away as the ammo ones.
		var interactable = Components.Get<Interactable>( FindMode.EnabledInSelfAndDescendants )
			?? GameObject.Components.Create<Interactable>();
		interactable.Range = 80f;

		InteractionSetup.EnsureOption( this, "take", "Take", "healing", "" );
	}

	InteractionInfo? IInteractionHandler.Describe( InteractionEvent e ) =>
		new( HealthGive > 0 ? $"Take +{HealthGive:0} health" : "Take", "healing" );

	bool IInteractionHandler.CanInteract( InteractionEvent e ) =>
		e.Interactor.IsValid() && e.Interactor.GetComponent<Player>() is { } player && CanPickup( player );

	void IInteractionHandler.OnInteract( InteractionEvent e )
	{
		if ( !Networking.IsHost || GameObject.IsDestroyed )
			return;

		var player = e.Interactor.IsValid() ? e.Interactor.GetComponent<Player>() : null;
		if ( !player.IsValid() || !CanPickup( player ) )
			return;

		player.Health = (player.Health + HealthGive).Clamp( 0, player.MaxHealth );

		PlayPickupEffects( player );
		DestroyGameObject();
	}

	[Rpc.Broadcast]
	void PlayPickupEffects( Player player )
	{
		if ( Application.IsDedicatedServer ) return;

		var snd = GameObject.PlaySound( PickupSound );
		if ( !snd.IsValid() )
			return;

		if ( player.IsValid() && player.IsLocalPlayer )
			snd.SpacialBlend = 0;
	}
}
