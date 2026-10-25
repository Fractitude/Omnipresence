namespace Sandbox;

/// <summary>
/// A pickup that gives the player some health. The host checks it when a player touches it.
/// </summary>
public sealed class HealthPickup : Component, Component.ITriggerListener, Component.ICollisionListener
{
	/// <summary>
	/// The pickup's collider, it is required
	/// </summary>
	[RequireComponent] public Collider Collider { get; set; }

	/// <summary>
	/// The sound to play when picking up this item
	/// </summary>
	[Property] public SoundEvent PickupSound { get; set; }

	/// <summary>
	/// How much health to give to the player
	/// </summary>
	[Property, Group( "Health" )] float HealthGive { get; set; } = 0;

	bool CanPickup( Player player ) => player.Health < player.MaxHealth || HealthGive <= 0;

	void TryPickup( GameObject other )
	{
		if ( !Networking.IsHost ) return;
		if ( GameObject.IsDestroyed ) return;

		var player = other.Root.GetComponent<Player>();
		if ( !player.IsValid() || !CanPickup( player ) )
			return;

		player.Health = (player.Health + HealthGive).Clamp( 0, player.MaxHealth );

		PlayPickupEffects( player );
		DestroyGameObject();
	}

	void ITriggerListener.OnTriggerEnter( GameObject other ) => TryPickup( other );

	void ICollisionListener.OnCollisionStart( Collision collision ) => TryPickup( collision.Other.GameObject );

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
