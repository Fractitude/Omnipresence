namespace Sandbox;

/// <summary>
/// The pure geometry of a vehicle's cargo: how far a point is from a vehicle, and where things are put down behind it.
/// No scene, no traces; the host then drops each candidate to the floor and tests it for room.
/// </summary>
public static class CargoMath
{
	/// <summary>
	/// How far a point is from a box (0 inside it).
	/// </summary>
	public static float DistanceToBounds( BBox bounds, Vector3 point )
	{
		var closest = new Vector3(
			System.Math.Clamp( point.x, bounds.Mins.x, bounds.Maxs.x ),
			System.Math.Clamp( point.y, bounds.Mins.y, bounds.Maxs.y ),
			System.Math.Clamp( point.z, bounds.Mins.z, bounds.Maxs.z ) );

		return closest.Distance( point );
	}

	/// <summary>
	/// A world point behind a vehicle: past its rear (the back of <paramref name="localBounds"/>, -X in the vehicle's own
	/// space) by <paramref name="gap"/> plus half of <paramref name="depth"/>, so the object's front edge clears the
	/// vehicle, <paramref name="row"/> rows farther back for the next one, and <paramref name="lateral"/> units to the
	/// side. At the height of the vehicle's lowest point.
	/// </summary>
	public static Vector3 Behind(
		Transform vehicle, BBox localBounds, float depth, float gap, float lateral, int row = 0 )
	{
		var x = localBounds.Mins.x - gap - depth * 0.5f - row * (depth + gap);
		var local = new Vector3( x, localBounds.Center.y + lateral, localBounds.Mins.z );
		return vehicle.PointToWorld( local );
	}

	/// <summary>
	/// The places to try, nearest the middle of the rear first: straight behind, then alternately to each side, then the
	/// next row back. Each is clear of the vehicle's own box by at least <paramref name="gap"/>.
	/// </summary>
	public static List<Vector3> RearCandidates(
		Transform vehicle, BBox localBounds, float width, float depth, float gap = 40f )
	{
		var spots = new List<Vector3>();

		for ( var row = 0; row < 3; row++ )
		{
			foreach ( var step in new[] { 0, 1, -1, 2, -2 } )
				spots.Add( Behind( vehicle, localBounds, depth, gap, step * (width + gap * 0.5f), row ) );
		}

		return spots;
	}

	/// <summary>
	/// Whether a world point lies inside a box that rides on a vehicle (the vehicle's local bounds).
	/// </summary>
	public static bool IsInside( Transform vehicle, BBox localBounds, Vector3 world )
	{
		var local = vehicle.PointToLocal( world );

		return local.x >= localBounds.Mins.x && local.x <= localBounds.Maxs.x
		       && local.y >= localBounds.Mins.y && local.y <= localBounds.Maxs.y
		       && local.z >= localBounds.Mins.z && local.z <= localBounds.Maxs.z;
	}

	/// <summary>
	/// The indexes of the candidates within <paramref name="range"/>, nearest first. The order is the one every machine
	/// computes the same way, so an option "load into the n-th vehicle" means the same on the client and on the host.
	/// </summary>
	public static List<int> NearestWithin( IReadOnlyList<float> distances, float range ) => Enumerable
		.Range( 0, distances.Count )
		.Where( i => distances[i] <= range )
		.OrderBy( i => distances[i] )
		.ThenBy( i => i )
		.ToList();
}
