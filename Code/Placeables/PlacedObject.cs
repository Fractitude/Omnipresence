namespace Sandbox;

/// <summary>
/// Marks a big object the Depot sold (a storage box, a grow tray, ...) and gives it the one id everything saved about it
/// is keyed by. A scene-placed object has no such component and is known by its object id, which is stable in the
/// scene; a bought one is made at run time, so its object id changes every session. This id does not: it is made when
/// the order is placed, saved in <c>placed_objects.json</c>, and put back on the object whenever it is made again (a
/// restart, a car's cargo unloaded). <see cref="IdOf(GameObject)"/> is how <see cref="StorageBox"/> and
/// <see cref="GrowTray"/> find their id.
/// </summary>
/// <remarks>
/// Every machine has the component (the id and the type are synced from the host, because a client's tray text and
/// prompts read the id). Only the host tracks where the object comes to rest and writes it down, so a moved object is
/// still there next session. The buyer is kept for the ownership design (see <c>plans/ownership.md</c>) and grants
/// nothing: a box is still claimed by first use.
/// </remarks>
public sealed class PlacedObject : Component
{
	/// <summary>Seconds an object must be still before its place is written down.</summary>
	const float StillTime = 0.5f;

	/// <summary>Units per second below which an object counts as still.</summary>
	const float StillSpeed = 2f;

	/// <summary>How far an object must have moved from the saved place to be written again.</summary>
	const float MovedDistance = 1f;

	/// <summary>
	/// The id of this object, unique and permanent. Set by the host before the object is network-spawned.
	/// </summary>
	[Sync( SyncFlags.FromHost )]
	public string ObjectId { get; set; } = "";

	/// <summary>
	/// The <see cref="Placeable.Id"/> it was bought as.
	/// </summary>
	[Sync( SyncFlags.FromHost )]
	public string PlaceableId { get; set; } = "";

	/// <summary>
	/// The Steam id of the buyer, or 0.
	/// </summary>
	[Sync( SyncFlags.FromHost )]
	public ulong BuyerId { get; set; }

	/// <summary>
	/// What the object is, for menus: "Storage Box".
	/// </summary>
	public string DisplayName => PlaceableCatalog.Find( PlaceableId )?.Name ?? GameObject.Name;

	/// <summary>Where the registry last heard that this stood.</summary>
	public Vector3 Rest { get; set; }

	Rigidbody body;
	float still;

	/// <summary>
	/// The id that saves use for an object: its <see cref="ObjectId"/> if it is a placed object, else its object id. Look
	/// from the object itself or from a component on it.
	/// </summary>
	public static string IdOf( GameObject go )
	{
		if ( !go.IsValid() )
			return "";

		var placed = go.Components.Get<PlacedObject>( FindMode.EverythingInSelfAndParent );

		return placed.IsValid() && !string.IsNullOrEmpty( placed.ObjectId ) ? placed.ObjectId : go.Id.ToString();
	}

	/// <summary>
	/// The id of the object this component is on.
	/// </summary>
	public static string IdOf( Component component ) => IdOf( component?.GameObject );

	protected override void OnFixedUpdate()
	{
		if ( !Game.IsPlaying || Networking.IsActive && !Networking.IsHost || string.IsNullOrEmpty( ObjectId ) )
			return;

		body ??= Components.Get<Rigidbody>( FindMode.EverythingInSelfAndDescendants );

		var speed = body.IsValid() ? body.Velocity.Length : 0f;

		if ( speed > StillSpeed )
		{
			still = 0;
			return;
		}

		still += Time.Delta;

		if ( still < StillTime || WorldPosition.Distance( Rest ) < MovedDistance )
			return;

		Rest = WorldPosition;
		PlacedObjectRegistry.SetPlace( ObjectId, WorldPosition, WorldRotation.Angles() );
	}
}
