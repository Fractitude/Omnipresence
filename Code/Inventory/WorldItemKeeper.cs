namespace Sandbox;

/// <summary>
/// Put on every world item by <see cref="WorldItemRegistry.Track"/>, on the host only (it is added after the network
/// spawn, so clients never get it). It carries the item's id, and tells the registry where the item came to rest: not
/// while it moves, but once it has been still for a moment, and again if it is pushed somewhere else later.
/// </summary>
[Hide]
public sealed class WorldItemKeeper : Component
{
	/// <summary>Seconds an item must be still before its place is written down.</summary>
	const float StillTime = 0.5f;

	/// <summary>Units per second below which an item counts as still.</summary>
	const float StillSpeed = 2f;

	/// <summary>How far an item must have moved from the saved place to be written again.</summary>
	const float MovedDistance = 1f;

	public string ItemId { get; set; } = "";

	/// <summary>Where the registry last heard that this lay.</summary>
	public Vector3 Rest { get; set; }

	Rigidbody body;
	float still;

	protected override void OnFixedUpdate()
	{
		if ( Networking.IsActive && !Networking.IsHost || string.IsNullOrEmpty( ItemId ) )
			return;

		body ??= Components.Get<Rigidbody>( FindMode.EverythingInSelfAndDescendants );

		// A body the Physgun froze does not move, whatever velocity it had when it was frozen.
		var speed = body.IsValid() && body.MotionEnabled ? body.Velocity.Length : 0f;

		if ( speed > StillSpeed )
		{
			still = 0;
			return;
		}

		still += Time.Delta;

		if ( still < StillTime || WorldPosition.Distance( Rest ) < MovedDistance )
			return;

		Rest = WorldPosition;
		WorldItemRegistry.SetPlace( ItemId, WorldPosition, WorldRotation.Angles() );
	}
}
