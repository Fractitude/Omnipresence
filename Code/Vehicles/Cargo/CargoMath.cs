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
	/// Where a candidate spot is, relative to the vehicle: behind it, beside it, or above it.
	/// </summary>
	public enum UnloadKind
	{
		Rear,
		Side,
		Above,
	}

	/// <summary>
	/// A place to try when unloading: the world position of the middle of the thing's box on the floor (or, for
	/// <see cref="UnloadKind.Above"/>, in the air over the vehicle), and which side of the vehicle it is on
	/// (-1 the vehicle's right, +1 its left, 0 for behind and above).
	/// </summary>
	public readonly record struct UnloadCandidate( Vector3 Position, UnloadKind Kind, int Side );

	/// <summary>
	/// Every place worth trying to unload something with the given size (in the vehicle's own axes: x and y are half
	/// extents along and across it, z is the full height; the spots are the bottom middle of its box), in the order to try them: first behind the rear, in rows that go farther back, each row from the middle
	/// out; then beside the vehicle, left then right, centred on the cargo bed (<paramref name="bedCentreX"/>), the middle and a little either way along its
	/// length, then again farther out; and last one spot above the vehicle. A ground candidate never overlaps the vehicle's
	/// own box. The host drops each to the floor and tests it for room.
	/// </summary>
	public static List<UnloadCandidate> UnloadCandidates(
		Transform vehicle, BBox localBounds, Vector3 half, float bedCentreX, float gap = 20f )
	{
		var spots = new List<UnloadCandidate>();
		var lateralStep = half.y * 2f + gap * 0.5f;
		var z = localBounds.Mins.z;

		// Behind the rear, nearest row first.
		for ( var row = 0; row < 4; row++ )
		{
			var x = localBounds.Mins.x - gap - half.x - row * (half.x * 2f + gap * 0.5f);

			foreach ( var step in new[] { 0, 1, -1, 2, -2 } )
			{
				var local = new Vector3( x, localBounds.Center.y + step * lateralStep, z );
				spots.Add( new UnloadCandidate( vehicle.PointToWorld( local ), UnloadKind.Rear, 0 ) );
			}
		}

		// Beside the vehicle, centred on the bed first, then along its length, then farther out.
		var length = localBounds.Size.x;
		var along = new[]
		{
			bedCentreX,
			localBounds.Center.x,
			localBounds.Center.x + length * 0.3f,
			localBounds.Center.x - length * 0.3f,
		};

		for ( var ring = 0; ring < 3; ring++ )
		{
			var outward = localBounds.Size.y * 0.5f + half.y + gap + ring * lateralStep;

			foreach ( var x in along )
			{
				foreach ( var side in new[] { 1, -1 } )
				{
					var local = new Vector3( x, localBounds.Center.y + side * outward, z );
					spots.Add( new UnloadCandidate( vehicle.PointToWorld( local ), UnloadKind.Side, side ) );
				}
			}
		}

		// Everything blocked: over the vehicle, to land on it.
		var above = new Vector3( localBounds.Center.x, localBounds.Center.y, localBounds.Maxs.z + 20f );
		spots.Add( new UnloadCandidate( vehicle.PointToWorld( above ), UnloadKind.Above, 0 ) );
		return spots;
	}

	/// <summary>
	/// Whether a box of this size (half extents x and y, full height z) standing with the middle of its bottom at a world
	/// point and turned like the vehicle overlaps the vehicle's box (given in the vehicle's own space).
	/// </summary>
	public static bool Overlaps( Transform vehicle, BBox localBounds, Vector3 half, Vector3 world )
	{
		var local = vehicle.PointToLocal( world );

		return local.x + half.x > localBounds.Mins.x && local.x - half.x < localBounds.Maxs.x
		       && local.y + half.y > localBounds.Mins.y && local.y - half.y < localBounds.Maxs.y
		       && local.z + half.z > localBounds.Mins.z && local.z < localBounds.Maxs.z;
	}

	/// <summary>
	/// The gap between two boxes: 0 when they touch or overlap, else the length of the shortest line from one to the other.
	/// </summary>
	public static float DistanceBetween( BBox a, BBox b )
	{
		var dx = System.Math.Max( 0f, System.Math.Max( a.Mins.x - b.Maxs.x, b.Mins.x - a.Maxs.x ) );
		var dy = System.Math.Max( 0f, System.Math.Max( a.Mins.y - b.Maxs.y, b.Mins.y - a.Maxs.y ) );
		var dz = System.Math.Max( 0f, System.Math.Max( a.Mins.z - b.Maxs.z, b.Mins.z - a.Maxs.z ) );
		return System.MathF.Sqrt( dx * dx + dy * dy + dz * dz );
	}

	/// <summary>
	/// The middle of a box.
	/// </summary>
	public static Vector3 Centre( BBox bounds ) => (bounds.Mins + bounds.Maxs) * 0.5f;

	/// <summary>
	/// Whether an object may be loaded into a vehicle: within range of its box, and with a clear line between the middles
	/// of the two boxes (<paramref name="lineClear"/>, the host's trace).
	/// </summary>
	public static bool CanLoad( float distance, float range, bool lineClear ) => lineClear && distance <= range;

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
