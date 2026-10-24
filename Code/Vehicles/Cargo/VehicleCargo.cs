namespace Sandbox;

/// <summary>
/// One big object stored in a vehicle's cargo: enough to put it back in the world as the same thing. Its per-instance
/// state (a tray's crop, a box's contents) is not here: that is saved by <see cref="Id"/> in the owner's record, which
/// the loading never touches, so the object comes back out with it.
/// </summary>
public sealed class CargoObject
{
	/// <summary>The <see cref="Placeable.Id"/>.</summary>
	public string Placeable { get; set; } = "";

	/// <summary>The object's permanent <see cref="PlacedObject.ObjectId"/>.</summary>
	public string Id { get; set; } = "";

	/// <summary>The buyer's Steam id, put back on the object.</summary>
	public ulong Owner { get; set; }
}

/// <summary>
/// A vehicle's cargo: a grid of slots built on the same item rules as a storage box (<see cref="ItemContainer"/>, the
/// same window), plus a short list of big objects (a storage box, a grow tray, ...) that cannot go in a bag and take no
/// grid slot. The option "Manage cargo" on the vehicle opens the window; a placed object near the vehicle offers
/// "Load into vehicle" (<see cref="PlacedObject"/>); the window unloads a slot or a big object to the ground behind the
/// car.
/// </summary>
/// <remarks>
/// The cargo is runtime only, like the vehicle: nothing saves it, and it is gone with the vehicle (a vehicle that is
/// removed out of bounds first spills it, <see cref="Spill"/>). Anyone within reach may put things in and take them out;
/// it has no owner. The grid lives in a <see cref="CrimeRecord"/> of its own that no player's save holds, so
/// <see cref="StorageRules"/> works on it unchanged. A big object loaded into a car keeps its record in
/// <c>placed_objects.json</c> (its last resting place), so a restart while it is aboard puts it back there rather than
/// losing it. Attached to every <see cref="FourWheelVehicle"/> at run time by <see cref="VehicleCargoSystem"/>.
/// </remarks>
public sealed class VehicleCargo : ItemContainer, IInteractionHandler
{
	const string OptionId = "manage_cargo";
	const string CargoId = "cargo";

	/// <summary>How far, in world units, a player may stand from the vehicle's box and still use its cargo.</summary>
	public const float ReachDistance = 250f;

	/// <summary>How far a big object may be from the vehicle's box and still be loaded into it.</summary>
	public const float LoadRange = 400f;

	/// <summary>The most big objects one cargo holds.</summary>
	public const int MaxBigObjects = 3;

	/// <summary>
	/// What the vehicle is called in the "Load into ..." list: "Buggy". Empty uses its object's name, capitalised.
	/// </summary>
	[Property]
	public string TypeName { get; set; } = "";

	/// <summary>How many slots wide the grid is.</summary>
	[Property, Range( 1, StorageRules.MaxColumns )]
	public int Columns { get; set; } = 6;

	/// <summary>How many slots high the grid is.</summary>
	[Property, Range( 1, StorageRules.MaxRows )]
	public int Rows { get; set; } = 4;

	// On the host: the grid in a record of its own, and the big objects aboard.
	readonly CrimeRecord grid = new();
	readonly List<CargoObject> objects = new();

	// On the machine of the player who asked: the big objects as the host last said.
	List<CargoObject> view = [];

	Rigidbody body;
	float still;
	Transform lastRest;
	bool hasRest;

	public override string Title => VehicleName;

	public override string ActionWord => "cargo";

	public override int ColumnCount => System.Math.Clamp( Columns, 1, StorageRules.MaxColumns );

	public override int RowCount => System.Math.Clamp( Rows, 1, StorageRules.MaxRows );

	public override float Reach => ReachDistance;

	protected override string TooFarText => $"Go to the {VehicleName.ToLowerInvariant()}.";

	/// <summary>
	/// The name the player reads for this vehicle.
	/// </summary>
	public string VehicleName =>
		!string.IsNullOrWhiteSpace( TypeName ) ? TypeName.Trim() : CargoNaming.Prettify( GameObject.Name );

	/// <summary>
	/// The big objects aboard as this player's window last heard.
	/// </summary>
	public IReadOnlyList<CargoObject> ViewObjects => view;

	/// <summary>
	/// The big objects aboard. Only the host knows them.
	/// </summary>
	public IReadOnlyList<CargoObject> Objects => objects;

	/// <summary>
	/// The grid's record, for tests and the host's own bookkeeping.
	/// </summary>
	public CrimeRecord Grid => grid;

	public string GridId => CargoId;

	/// <summary>
	/// The vehicle's box in world space, wheels and all.
	/// </summary>
	BBox WorldBounds => GameObject.GetBounds();

	public override bool IsNear( Vector3 position ) =>
		CargoMath.DistanceToBounds( WorldBounds, position ) <= Reach;

	/// <summary>
	/// How far a point is from the vehicle's box.
	/// </summary>
	public float DistanceTo( Vector3 position ) => CargoMath.DistanceToBounds( WorldBounds, position );

	public override void ClearView()
	{
		base.ClearView();
		view = [];
	}

	protected override string ExtraJson() => Json.Serialize( objects );

	protected override void OnExtra( string json ) =>
		view = string.IsNullOrEmpty( json ) ? [] : Json.Deserialize<List<CargoObject>>( json ) ?? [];

	protected override (CrimeRecord Record, string Id) Contents( Asker who ) => (grid, CargoId);

	protected override void OnStart()
	{
		var option = InteractionSetup.EnsureOption(
			this, OptionId, "Manage cargo", "inventory_2", "Load and unload what the vehicle carries." );

		// The press only opens the window on the player's own screen.
		if ( option is not null )
			option.RunOn = InteractionSide.Local;
	}

	InteractionInfo? IInteractionHandler.Describe( InteractionEvent e ) =>
		e.Option?.Id == OptionId
			? new( "Manage cargo", "inventory_2", $"Load and unload the {VehicleName.ToLowerInvariant()}." )
			: null;

	void IInteractionHandler.OnInteract( InteractionEvent e )
	{
		if ( e.Option?.Id == OptionId && !Application.IsHeadless )
			StorageMenu.Open( this );
	}

	protected override void OnFixedUpdate()
	{
		if ( !Game.IsPlaying || Networking.IsActive && !Networking.IsHost )
			return;

		body ??= Components.Get<Rigidbody>();
		var speed = body.IsValid() ? body.Velocity.Length : 0f;

		if ( speed > 20f )
		{
			still = 0;
			return;
		}

		// Where the vehicle last stood still: where a spill goes if the vehicle falls out of the world.
		still += Time.Delta;

		if ( still > 1f && WorldItemRules.IsPlace( WorldPosition ) )
		{
			lastRest = WorldTransform;
			hasRest = true;
		}
	}

	// -- Vehicles near a point ----------------------------------------------------------------------------------------

	/// <summary>
	/// The vehicles with a cargo that are within <see cref="LoadRange"/> of a point, nearest first. The same list on every
	/// machine, so "the n-th" in an option means the same vehicle on the client and on the host.
	/// </summary>
	public static List<VehicleCargo> Near( Scene scene, Vector3 position )
	{
		if ( !scene.IsValid() )
			return [];

		var all = scene.GetAllComponents<VehicleCargo>().Where( x => x.IsValid() ).ToList();
		var order = CargoMath.NearestWithin( all.Select( x => x.DistanceTo( position ) ).ToList(), LoadRange );
		return order.Select( i => all[i] ).ToList();
	}

	// -- Loading ------------------------------------------------------------------------------------------------------

	/// <summary>
	/// Whether another big object fits.
	/// </summary>
	public bool HasObjectRoom => objects.Count < MaxBigObjects;

	/// <summary>
	/// Take a placed object into the cargo: it leaves the world and its descriptor stays here. The host checks it is near
	/// and that there is room, then removes the object (its record in the world file stays, with its last resting place).
	/// Host only.
	/// </summary>
	public CrimeResult Load( PlacedObject placed )
	{
		if ( !Networking.IsHost || !placed.IsValid() || string.IsNullOrEmpty( placed.ObjectId )
		     || PlaceableCatalog.Find( placed.PlaceableId ) is not { } item )
			return CrimeResult.Fail( "Nothing happens." );

		if ( !HasObjectRoom )
			return CrimeResult.Fail( $"The {VehicleName.ToLowerInvariant()}'s cargo is full." );

		if ( DistanceTo( placed.WorldPosition ) > LoadRange )
			return CrimeResult.Fail( $"Move it closer to the {VehicleName.ToLowerInvariant()}." );

		if ( objects.Any( x => x.Id == placed.ObjectId ) )
			return CrimeResult.Fail( "It is already aboard." );

		objects.Add( new CargoObject { Placeable = item.Id, Id = placed.ObjectId, Owner = placed.BuyerId } );
		placed.GameObject.Destroy();
		return CrimeResult.Success( $"Loaded the {item.Name} into the {VehicleName.ToLowerInvariant()}." );
	}

	// -- Unloading ----------------------------------------------------------------------------------------------------

	/// <summary>
	/// Unload a big object to the ground behind the vehicle.
	/// </summary>
	[Rpc.Host]
	public void RequestUnloadObject( int index )
	{
		if ( Begin() is { } who )
			Reply( who, UnloadObject( index ) );
	}

	/// <summary>
	/// Unload the whole stack in a grid slot to the ground behind the vehicle.
	/// </summary>
	[Rpc.Host]
	public void RequestUnloadItem( int slot )
	{
		if ( Begin() is { } who )
			Reply( who, UnloadItem( slot ) );
	}

	/// <summary>
	/// Put the big object at this place in the list back in the world, behind the vehicle, as the same object (same
	/// prefab, same id) with a physics body. Nothing changes when there is no room behind the car. Host only.
	/// </summary>
	public CrimeResult UnloadObject( int index )
	{
		if ( !Networking.IsHost || index < 0 || index >= objects.Count )
			return CrimeResult.Fail( "Nothing happens." );

		var cargo = objects[index];

		if ( PlaceableCatalog.Find( cargo.Placeable ) is not { } item )
			return CrimeResult.Fail( "Nothing happens." );

		if ( RearSpot( DeliverySpot.Hull ) is not { } spot )
			return CrimeResult.Fail( "There is no room behind the vehicle." );

		var go = PlacedObjectRegistry.Create( item, cargo.Id, cargo.Owner, spot, Rotation.FromYaw( WorldRotation.Yaw() ),
			Scene );

		if ( !go.IsValid() )
			return CrimeResult.Fail( "It will not come out." );

		objects.RemoveAt( index );
		return CrimeResult.Success( $"Unloaded the {item.Name}." );
	}

	/// <summary>
	/// Put the stack in this grid slot on the ground behind the vehicle as one world item. Nothing changes when it cannot
	/// be made. Host only.
	/// </summary>
	public CrimeResult UnloadItem( int slot )
	{
		var box = StorageRules.Find( grid, CargoId );

		if ( !Networking.IsHost || StorageRules.At( box, slot ) is not { } item )
			return CrimeResult.Fail( "There is nothing in that slot." );

		if ( WorldItem.CountInScene( Scene ) >= WorldItem.MaxInScene )
			return CrimeResult.Fail( "There are too many things on the ground." );

		if ( RearSpot( ItemHull ) is not { } spot )
			return CrimeResult.Fail( "There is no room behind the vehicle." );

		// The item is made first and taken out of the grid second, so a failure loses nothing.
		if ( !WorldItem.Spawn( item.Clone(), spot, Rotation.FromYaw( WorldRotation.Yaw() ), Vector3.Zero ).IsValid() )
			return CrimeResult.Fail( "It will not come out." );

		StorageRules.Extract( grid, CargoId, slot );
		return CrimeResult.Success( item.Count > 1 ? $"Unloaded {item.Count} {item.Name}." : $"Unloaded {item.Name}." );
	}

	/// <summary>The room an item needs on the ground.</summary>
	static readonly BBox ItemHull = new( new Vector3( -14, -14, 0 ), new Vector3( 14, 14, 28 ) );

	/// <summary>
	/// A free floor spot behind the rear of the vehicle for something of this size, never inside the vehicle's box, or
	/// null when every candidate is blocked.
	/// </summary>
	public Vector3? RearSpot( BBox hull, IReadOnlyCollection<Vector3> taken = null )
	{
		var bounds = GameObject.GetLocalBounds();
		var level = WorldTransform.PointToWorld( new Vector3( 0, 0, bounds.Mins.z ) ).z;

		foreach ( var candidate in CargoMath.RearCandidates( WorldTransform, bounds, hull.Size.y, hull.Size.x ) )
		{
			if ( DeliverySpot.FindFloor( Scene, candidate, level, hull, 60f, GameObject ) is { } floor
			     && !CargoMath.IsInside( WorldTransform, bounds, floor ) && !DeliverySpot.IsTaken( floor, taken ) )
				return floor;
		}

		return null;
	}

	/// <summary>
	/// Put everything aboard on the ground near where the vehicle last stood, because the vehicle is about to be removed.
	/// Big objects come back as themselves; items drop as world items. What does not fit is lost, and says so in the log.
	/// Host only.
	/// </summary>
	public void Spill()
	{
		if ( !Networking.IsHost || !Scene.IsValid() )
			return;

		var at = hasRest ? lastRest : WorldTransform;
		var taken = new List<Vector3>();

		foreach ( var cargo in objects.ToList() )
		{
			if ( PlaceableCatalog.Find( cargo.Placeable ) is not { } item )
				continue;

			var spot = FindAround( at, DeliverySpot.Hull, taken );
			taken.Add( spot );
			PlacedObjectRegistry.Create( item, cargo.Id, cargo.Owner, spot, Rotation.FromYaw( at.Rotation.Yaw() ), Scene );
		}

		objects.Clear();

		foreach ( var entry in StorageRules.Find( grid, CargoId )?.Slots.ToList() ?? [] )
		{
			var spot = FindAround( at, ItemHull, taken );
			taken.Add( spot );
			WorldItem.Spawn( entry.Item.Clone(), spot, Rotation.Identity, Vector3.Zero );
		}

		grid.Storage.Clear();
	}

	/// <summary>
	/// A spot near a place on the floor: the first free of a few rings around it, else the place itself.
	/// </summary>
	Vector3 FindAround( Transform at, BBox hull, IReadOnlyCollection<Vector3> taken )
	{
		foreach ( var radius in new[] { 120f, 200f, 280f } )
		{
			for ( var i = 0; i < 8; i++ )
			{
				var candidate = at.Position + Rotation.FromYaw( i * 45f ).Forward * radius;

				if ( DeliverySpot.FindFloor( Scene, candidate, at.Position.z, hull, 60f ) is { } floor
				     && !DeliverySpot.IsTaken( floor, taken ) )
					return floor;
			}
		}

		return at.Position + Vector3.Up * 20f;
	}
}

/// <summary>
/// Names for the cargo list.
/// </summary>
public static class CargoNaming
{
	/// <summary>
	/// A GameObject's name as a vehicle's type name: "buggy" and "buggy (Clone)" become "Buggy", "dune_buggy" becomes
	/// "Dune buggy". Empty becomes "Vehicle".
	/// </summary>
	public static string Prettify( string name )
	{
		var text = (name ?? "").Replace( "(Clone)", "" ).Replace( '_', ' ' ).Replace( '-', ' ' ).Trim();

		return text.Length == 0 ? "Vehicle" : char.ToUpperInvariant( text[0] ) + text[1..];
	}
}
