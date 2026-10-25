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
/// <para>
/// It also adds the option "Load into vehicle" to the object's <see cref="Interactable"/>: one row for each vehicle with a
/// cargo area within <see cref="VehicleCargo.LoadReach"/> (at most <see cref="MaxLoadOptions"/>, nearest first), labelled with
/// the vehicle's type name. The rows are fixed options <c>load_0</c>, <c>load_1</c>, ... that hide themselves while no
/// vehicle fills them, because an Interactable's list of options cannot change at run time.
/// </para>
/// </remarks>
public sealed class PlacedObject : Component, IInteractionHandler
{
	/// <summary>How many vehicles the load options list.</summary>
	public const int MaxLoadOptions = 4;

	const string LoadPrefix = "load_";

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
	List<VehicleCargo> near = [];
	TimeSince sinceNear = 1000;

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

	protected override void OnStart()
	{
		for ( var i = 0; i < MaxLoadOptions; i++ )
			InteractionSetup.EnsureOption( this, LoadPrefix + i, "Load into vehicle", "local_shipping" );
	}

	/// <summary>
	/// The vehicles this object can be loaded into, nearest first. Looked up at most four times a second: the crosshair
	/// asks for every option every frame.
	/// </summary>
	List<VehicleCargo> Vehicles()
	{
		if ( sinceNear > 0.25f )
		{
			sinceNear = 0;
			near = VehicleCargo.LoadTargets( this ).Take( MaxLoadOptions ).ToList();
		}

		return near;
	}

	static int LoadIndex( InteractionEvent e ) =>
		e.Option?.Id is { } id && id.StartsWith( LoadPrefix ) && int.TryParse( id[LoadPrefix.Length..], out var i ) ? i : -1;

	InteractionInfo? IInteractionHandler.Describe( InteractionEvent e )
	{
		if ( LoadIndex( e ) is not (>= 0 and var index) )
			return null;

		var vehicles = Vehicles();

		// No vehicle for this row: the empty label hides it.
		if ( index >= vehicles.Count )
			return new InteractionInfo( "" );

		var vehicle = vehicles[index];
		var meters = vehicle.ReachTo( GameObject ) / 39.37f;
		var label = $"Load into {vehicle.VehicleName}";

		return vehicle.HasObjectRoom
			? new InteractionInfo( label, "local_shipping", $"Put the {DisplayName} in its cargo. {meters:0.0} m from the cargo area." )
			: new InteractionInfo( label, "local_shipping", "Its cargo is full.", false );
	}

	void IInteractionHandler.OnInteract( InteractionEvent e )
	{
		if ( !Networking.IsHost || LoadIndex( e ) is not (>= 0 and var index) )
			return;

		// The host works the list out again from its own world, never from what the client saw.
		var vehicles = VehicleCargo.LoadTargets( this ).Take( MaxLoadOptions ).ToList();
		var crime = e.Interactor?.Components.Get<PlayerCrime>();

		if ( index >= vehicles.Count )
		{
			crime?.Tell( CrimeResult.Fail( "There is no vehicle there any more." ) );
			return;
		}

		crime?.Tell( vehicles[index].Load( this ) );
	}

	protected override void OnFixedUpdate()
	{
		if ( !Game.IsPlaying || Networking.IsActive && !Networking.IsHost || string.IsNullOrEmpty( ObjectId ) )
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
		PlacedObjectRegistry.SetPlace( ObjectId, WorldPosition, WorldRotation.Angles() );
	}
}
