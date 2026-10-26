namespace Sandbox;

/// <summary>
/// Where a delivered object is put down. A store order goes to a delivery point (an object tagged <see cref="Tag"/>)
/// within <see cref="Range"/> of the terminal it was bought at; a home order goes beside the buyer's mailbox (or one that
/// is not another player's), else near a spawn point. At each of those places the spot is searched on rings around it,
/// dropped to the floor and tested for room; when the ground there is taken the delivery is stacked on top of what
/// already stands, laid flat, so a place someone has blocked with an object still delivers.
/// </summary>
public static class DeliverySpot
{
	/// <summary>The tag a level places to say where a store delivery lands.</summary>
	public const string Tag = "delivery_point";

	/// <summary>How near a delivery point has to be to a terminal to count as the store's: 38 m.</summary>
	public const float Range = 1500f;

	/// <summary>The room an object needs on the ground (the biggest Depot object plus a margin).</summary>
	public static readonly BBox Hull = new( new Vector3( -50, -40, 0 ), new Vector3( 50, 40, 55 ) );

	/// <summary>
	/// How high above a place a delivery may be stacked before that place counts as full: five objects of the biggest
	/// size. It caps the tower at one bay, so a bay can be filled but not for ever, and the next bay is tried after it.
	/// </summary>
	public const float MaxStackRise = 300f;

	/// <summary>Lower than this above the floor, a "stack" is the floor itself.</summary>
	const float MinStackRise = 10f;

	/// <summary>
	/// How high above a place the stack search starts, from a full stack down to a car's roof. It starts lower when the
	/// box would begin inside something (a bay under a low roof), so a covered bay still stacks.
	/// </summary>
	static readonly float[] StackStarts = [355f, 240f, 140f, 80f];

	static readonly float[] Radii = [0f, 130f, 200f, 270f, 340f];

	const int Angles = 8;

	/// <summary>How far above or below the anchor a floor may be and still count.</summary>
	const float MaxStep = 24f;

	/// <summary>One place a delivery may land: where the search starts, how to describe it, and which way round.</summary>
	public readonly record struct DeliveryPlace( Vector3 Anchor, string Where, float Yaw );

	/// <summary>
	/// A free spot at the first place that has one: on the ground if any place has room, else stacked on what already
	/// stands there. Null when every place is full, which leaves a delivery waiting. <paramref name="taken"/> holds the
	/// spots chosen a moment ago in the same pass, which have no body yet to trace against.
	/// </summary>
	public static (Vector3 Position, string Where, float Yaw)? Find(
		Scene scene, IReadOnlyList<DeliveryPlace> places, IReadOnlyCollection<Vector3> taken = null )
	{
		if ( !scene.IsValid() || places is null )
			return null;

		// The ground at any place first: a delivery is better beside the one before it than on top of it.
		foreach ( var place in places )
		{
			if ( OnGround( scene, place.Anchor, taken ) is { } floor )
				return (floor, place.Where, System.Random.Shared.Next( 0, 360 ));
		}

		foreach ( var place in places )
		{
			if ( OnTop( scene, place.Anchor, place.Yaw, taken ) is { } floor )
				return (floor, $"on the stack {place.Where}", place.Yaw);
		}

		return null;
	}

	/// <summary>A spot for a home delivery: beside the buyer's mailbox, else near a spawn point.</summary>
	public static (Vector3 Position, string Where, float Yaw)? FindHome(
		Scene scene, ulong owner, IReadOnlyCollection<Vector3> taken = null ) =>
		Find( scene, HomePlaces( scene, owner ), taken );

	/// <summary>
	/// A spot for a store pickup: the delivery points within <paramref name="range"/> of <paramref name="around"/> (the
	/// terminal the order is placed at), nearest first.
	/// </summary>
	public static (Vector3 Position, string Where, float Yaw)? FindStore(
		Scene scene, Vector3 around, IReadOnlyCollection<Vector3> taken = null, float range = Range ) =>
		Find( scene, StorePlaces( scene, around, range ), taken );

	/// <summary>Whether a delivery point is near enough to a terminal for the Depot there to offer store pickup.</summary>
	public static bool HasPointNear( Scene scene, Vector3 around, float range = Range ) =>
		scene.IsValid() && scene.GetAllObjects( true ).Any(
			x => x.Tags.Has( Tag ) && x.WorldPosition.Distance( around ) <= range );

	/// <summary>The delivery points within range of a terminal, nearest first: the store's bays.</summary>
	public static List<DeliveryPlace> StorePlaces( Scene scene, Vector3 around, float range = Range )
	{
		if ( !scene.IsValid() )
			return [];

		return scene.GetAllObjects( true )
			.Where( x => x.Tags.Has( Tag ) && x.WorldPosition.Distance( around ) <= range )
			.OrderBy( x => x.WorldPosition.Distance( around ) )
			.Select( x => new DeliveryPlace( x.WorldPosition, "at the delivery point", x.WorldRotation.Yaw() ) )
			.ToList();
	}

	/// <summary>Beside the buyer's mailbox (else any that is not another player's), then near a spawn point.</summary>
	public static List<DeliveryPlace> HomePlaces( Scene scene, ulong owner )
	{
		if ( !scene.IsValid() )
			return [];

		var places = new List<DeliveryPlace>();

		foreach ( var box in scene.GetAll<MailBox>().OrderBy( x => x.OwnerId == owner ? 0 : 1 ) )
		{
			if ( box.OwnerId != 0 && box.OwnerId != owner )
				continue;

			places.Add( new DeliveryPlace( box.WorldPosition,
				box.OwnerId == owner ? "beside your mailbox" : "beside the mailbox", box.WorldRotation.Yaw() ) );
		}

		foreach ( var spawn in scene.GetAll<SpawnPoint>() )
		{
			var yaw = spawn.WorldRotation.Yaw();
			places.Add( new DeliveryPlace( spawn.WorldPosition + Rotation.FromYaw( yaw ).Forward * 150f,
				"near your spawn point", yaw ) );
		}

		return places;
	}

	/// <summary>A spot on the floor on rings around the anchor (see <see cref="Radii"/>), or null.</summary>
	static Vector3? OnGround( Scene scene, Vector3 anchor, IReadOnlyCollection<Vector3> taken )
	{
		foreach ( var radius in Radii )
		{
			for ( var i = 0; i < (radius == 0f ? 1 : Angles); i++ )
			{
				var candidate = anchor + Rotation.FromYaw( i * 360f / Angles ).Forward * radius;

				if ( Free( scene, candidate, anchor.z ) is { } floor && !IsTaken( floor, taken ) )
					return floor;
			}
		}

		return null;
	}

	/// <summary>
	/// On top of whatever already stands at the anchor, laid flat (the anchor's way round, no tilt), if the box fits
	/// there and the top is within <see cref="MaxStackRise"/>. This is what keeps a place someone has blocked with an
	/// object usable: the next delivery goes on top of it.
	/// </summary>
	static Vector3? OnTop( Scene scene, Vector3 anchor, float yaw, IReadOnlyCollection<Vector3> taken )
	{
		var rotation = Rotation.FromYaw( yaw );

		foreach ( var start in StackStarts )
		{
			var trace = scene.Trace.Box( Hull, anchor + Vector3.Up * start, anchor + Vector3.Down * 40f )
				.Rotated( rotation )
				.WithoutTags( "trigger" );

			var top = trace.Run();

			// Nothing within reach, or the box begins inside something (a roof): start lower down.
			if ( !top.Hit || top.StartedSolid )
				continue;

			// A level top to stand on, high enough to be something rather than the floor itself.
			if ( top.Normal.z < 0.7f || top.EndPosition.z - anchor.z < MinStackRise )
				continue;

			var floor = AboveTaken( top.EndPosition + Vector3.Up * 2f, taken );

			if ( floor.z - anchor.z <= MaxStackRise && IsClear( scene, floor, Hull, rotation ) )
				return floor;
		}

		return null;
	}

	/// <summary>
	/// A spot raised over the ones chosen a moment ago in this pass: an object that was just made has no body yet for the
	/// next delivery to trace against, so without this two of them would be put in the same place.
	/// </summary>
	static Vector3 AboveTaken( Vector3 floor, IReadOnlyCollection<Vector3> taken )
	{
		if ( taken is null )
			return floor;

		for ( var pass = 0; pass < 8; pass++ )
		{
			var raised = floor;

			foreach ( var other in taken )
			{
				if ( System.Math.Abs( other.x - raised.x ) >= Hull.Size.x
				     || System.Math.Abs( other.y - raised.y ) >= Hull.Size.y )
					continue;

				if ( raised.z >= other.z - 2f && raised.z - other.z < Hull.Size.z + 2f )
					raised.z = other.z + Hull.Size.z;
			}

			if ( raised.z == floor.z )
				break;

			floor = raised;
		}

		return floor;
	}

	/// <summary>
	/// Whether a spot is too near one chosen a moment ago, which has no body in the physics world yet to be traced.
	/// </summary>
	public static bool IsTaken( Vector3 spot, IReadOnlyCollection<Vector3> taken ) =>
		taken is not null && taken.Any( x => System.Math.Abs( x.x - spot.x ) < Hull.Size.x
		                                      && System.Math.Abs( x.y - spot.y ) < Hull.Size.y );

	/// <summary>
	/// The candidate dropped to the floor, if the hull fits there and nothing stands in it. The floor must be within
	/// <paramref name="maxStep"/> of <paramref name="level"/> (not the top of a wall or a table). Anything under
	/// <paramref name="ignore"/> (a vehicle being unloaded) does not count as in the way.
	/// </summary>
	public static Vector3? FindFloor(
		Scene scene, Vector3 candidate, float level, BBox hull, float maxStep = MaxStep, GameObject ignore = null )
	{
		var from = candidate + Vector3.Up * 60f;
		var trace = scene.Trace.Box( hull, from, candidate + Vector3.Down * 200f ).WithoutTags( "trigger" );

		if ( ignore.IsValid() )
			trace = trace.IgnoreGameObjectHierarchy( ignore );

		var ground = trace.Run();

		if ( !ground.Hit || ground.StartedSolid || System.Math.Abs( ground.EndPosition.z - level ) > maxStep )
			return null;

		var floor = ground.EndPosition + Vector3.Up * 2f;
		var overlapTrace = scene.Trace.Box( hull, floor, floor ).WithoutTags( "trigger" );

		if ( ignore.IsValid() )
			overlapTrace = overlapTrace.IgnoreGameObjectHierarchy( ignore );

		return overlapTrace.Run().Hit ? null : floor;
	}

	/// <summary>
	/// Like <see cref="FindFloor"/> for a box turned by <paramref name="rotation"/>, and stricter: the floor under it is
	/// level and supported at its four bottom corners, and nothing at all (the world, the vehicle, players, other solid
	/// props) overlaps the box. Only the floor search ignores <paramref name="floorIgnore"/> (the vehicle's own wheels
	/// must not count as floor); the overlap test does not, so the vehicle's body is in the way like anything else.
	/// <paramref name="hull"/> has the middle of its bottom at the origin. Returns the spot, or null.
	/// </summary>
	public static Vector3? FindRoom(
		Scene scene, Vector3 candidate, float level, BBox hull, Rotation rotation, float maxStep, GameObject floorIgnore )
	{
		var down = scene.Trace.Box( hull, candidate + Vector3.Up * 60f, candidate + Vector3.Down * 200f )
			.Rotated( rotation )
			.WithoutTags( "trigger" );

		if ( floorIgnore.IsValid() )
			down = down.IgnoreGameObjectHierarchy( floorIgnore );

		var ground = down.Run();

		if ( !ground.Hit || ground.StartedSolid || ground.Normal.z < 0.7f
		     || System.Math.Abs( ground.EndPosition.z - level ) > maxStep )
			return null;

		var floor = ground.EndPosition + Vector3.Up * 2f;

		// Supported: floor within reach under each bottom corner, so nothing hangs over an edge.
		foreach ( var corner in new[] { (hull.Mins.x, hull.Mins.y), (hull.Mins.x, hull.Maxs.y), (hull.Maxs.x, hull.Mins.y),
			         (hull.Maxs.x, hull.Maxs.y) } )
		{
			var at = floor + rotation * new Vector3( corner.Item1 * 0.9f, corner.Item2 * 0.9f, 0 );
			var probe = scene.Trace.Ray( at + Vector3.Up * 8f, at + Vector3.Down * 24f ).WithoutTags( "trigger" );

			if ( floorIgnore.IsValid() )
				probe = probe.IgnoreGameObjectHierarchy( floorIgnore );

			if ( !probe.Run().Hit )
				return null;
		}

		return IsClear( scene, floor, hull, rotation ) ? floor : null;
	}

	/// <summary>
	/// Whether the box (middle of its bottom at <paramref name="at"/>, turned by <paramref name="rotation"/>) overlaps
	/// nothing solid.
	/// </summary>
	public static bool IsClear( Scene scene, Vector3 at, BBox hull, Rotation rotation ) =>
		!scene.Trace.Box( hull, at, at ).Rotated( rotation ).WithoutTags( "trigger" ).Run().Hit;

	static Vector3? Free( Scene scene, Vector3 candidate, float level ) => FindFloor( scene, candidate, level, Hull );
}
