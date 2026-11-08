namespace Sandbox;

/// <summary>
/// Where a bought car is put down: a delivery point (an object tagged <see cref="Tag"/>, with a <see cref="BoxCollider"/>
/// the size of the bay) within <see cref="Range"/> of the dealership's computer, that has nothing in it. The car is made
/// with the point's rotation, at the middle of its box and dropped onto the ground below it.
/// </summary>
public static class CarDelivery
{
	/// <summary>The tag a level puts on a bay. The Depot's store pickup uses the same one (<see cref="DeliverySpot.Tag"/>).</summary>
	public const string Tag = DeliverySpot.Tag;

	/// <summary>How near a delivery point has to be to the computer to be the dealership's, in units.</summary>
	public const float Range = 300f;

	/// <summary>The bottom of a bay is left out of the search by this much, so the ground under it is not "something in it".</summary>
	const float FloorMargin = 6f;

	/// <summary>How far above the ground the car is put down, so its wheels start clear of it.</summary>
	const float DropMargin = 2f;

	/// <summary>How far down from the middle of a bay the ground is looked for.</summary>
	const float GroundReach = 600f;

	/// <summary>Half the size of a bay that has no box collider: a car on its own.</summary>
	static readonly Vector3 DefaultHalfSize = new( 120, 60, 50 );

	/// <summary>The delivery points within <see cref="Range"/> of <paramref name="around"/>, nearest first.</summary>
	public static List<GameObject> PointsNear( Scene scene, Vector3 around ) =>
		!scene.IsValid()
			? []
			: scene.GetAllObjects( true )
				.Where( x => x.Tags.Has( Tag ) && x.WorldPosition.Distance( around ) <= Range )
				.OrderBy( x => x.WorldPosition.Distance( around ) )
				.ToList();

	/// <summary>
	/// The first free delivery point near the computer, or null. <paramref name="total"/> is how many points there are
	/// and <paramref name="free"/> how many of them are free, so a caller can tell "no bay" from "all taken".
	/// </summary>
	public static GameObject FindFree( Scene scene, Vector3 around, out int total, out int free )
	{
		var points = PointsNear( scene, around );
		var open = points.Where( x => IsFree( scene, x ) ).ToList();

		total = points.Count;
		free = open.Count;
		return open.FirstOrDefault();
	}

	/// <summary>
	/// Whether nothing is in the point's box: no body that moves, no player and no NPC. The ground, walls and anything
	/// fixed to the map don't count, nor does the point itself.
	/// </summary>
	public static bool IsFree( Scene scene, GameObject point )
	{
		var (center, half, rotation) = BoxOf( point );

		// The box's corners in the world, for a first search by the engine; the oriented box is checked after.
		var mins = center;
		var maxs = center;

		foreach ( var corner in Corners( half ) )
		{
			var world = center + rotation * corner;
			mins = Vector3.Min( mins, world );
			maxs = Vector3.Max( maxs, world );
		}

		mins = mins.WithZ( mins.z + FloorMargin );

		if ( maxs.z <= mins.z )
			return true;

		foreach ( var go in scene.FindInPhysics( new BBox( mins, maxs ) ) )
		{
			if ( !go.IsValid() || IsPartOf( go, point ) )
				continue;

			foreach ( var collider in go.Components.GetAll<Collider>( FindMode.EnabledInSelfAndDescendants ) )
			{
				if ( collider.IsTrigger || IsPartOf( collider.GameObject, point ) || !IsBody( collider ) )
					continue;

				if ( Overlaps( center, half, rotation, collider.GetWorldBounds() ) )
					return false;
			}
		}

		return true;
	}

	/// <summary>
	/// Where a car goes in a bay: the middle of the point's box, on the ground below it, and turned as the point is. The
	/// car's lowest part (<paramref name="lowest"/>, the bottom of its bounds in its own space) rests on the ground.
	/// </summary>
	public static Transform Placement( Scene scene, GameObject point, float lowest )
	{
		var (center, half, _) = BoxOf( point );

		var trace = scene.Trace
			.Ray( center, center + Vector3.Down * GroundReach )
			.IgnoreGameObjectHierarchy( point )
			.WithoutTags( "player", "npc" )
			.Run();

		// No ground below: the bottom of the box.
		var ground = trace.Hit ? trace.HitPosition.z : center.z - half.z;

		return new Transform( center.WithZ( ground - lowest + DropMargin ), point.WorldRotation );
	}

	/// <summary>The point's box in the world: its middle, half its size along each of its own axes, and its rotation.</summary>
	static (Vector3 Center, Vector3 Half, Rotation Rotation) BoxOf( GameObject point )
	{
		if ( point.Components.Get<BoxCollider>( FindMode.EverythingInSelfAndDescendants ) is not { } box )
			return (point.WorldPosition + point.WorldRotation * Vector3.Up * DefaultHalfSize.z, DefaultHalfSize, point.WorldRotation);

		var size = box.Scale * 0.5f * box.GameObject.WorldScale;

		return (box.WorldTransform.PointToWorld( box.Center ),
			new Vector3( System.MathF.Abs( size.x ), System.MathF.Abs( size.y ), System.MathF.Abs( size.z ) ),
			box.GameObject.WorldRotation);
	}

	static IEnumerable<Vector3> Corners( Vector3 half )
	{
		foreach ( var x in new[] { -1f, 1f } )
		foreach ( var y in new[] { -1f, 1f } )
		foreach ( var z in new[] { -1f, 1f } )
			yield return new Vector3( half.x * x, half.y * y, half.z * z );
	}

	/// <summary>Is this object the point or something on it?</summary>
	static bool IsPartOf( GameObject go, GameObject point )
	{
		for ( var current = go; current.IsValid(); current = current.Parent )
		{
			if ( current == point )
				return true;
		}

		return false;
	}

	/// <summary>
	/// A body that is "in the way": something that moves (a prop, a car), a player or an NPC. A collider that is fixed to
	/// the map is not.
	/// </summary>
	static bool IsBody( Collider collider ) =>
		collider.Rigidbody.IsValid() || collider.IsDynamic
		|| collider.GameObject.Root.Tags.Has( "player" ) || collider.GameObject.Root.Tags.Has( "npc" );

	/// <summary>
	/// Whether a world box overlaps the oriented box, tested along the oriented box's three axes (the world's three were
	/// the engine's search). Treats the bounds as the box that holds them, so it errs towards "taken".
	/// </summary>
	static bool Overlaps( Vector3 center, Vector3 half, Rotation rotation, BBox bounds )
	{
		var offset = rotation.Inverse * (bounds.Center - center);
		var reach = bounds.Size * 0.5f;

		var axes = new[] { rotation * Vector3.Forward, rotation * Vector3.Left, rotation * Vector3.Up };
		var distances = new[] { offset.x, offset.y, offset.z };
		var halves = new[] { half.x, half.y, half.z };

		for ( var i = 0; i < 3; i++ )
		{
			var axis = axes[i];
			var extent = System.MathF.Abs( axis.x ) * reach.x + System.MathF.Abs( axis.y ) * reach.y
				+ System.MathF.Abs( axis.z ) * reach.z;

			if ( System.MathF.Abs( distances[i] ) > halves[i] + extent )
				return false;
		}

		return true;
	}
}
