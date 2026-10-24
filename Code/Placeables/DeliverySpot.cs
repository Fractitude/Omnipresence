namespace Sandbox;

/// <summary>
/// Where a delivered object is put down. In order: beside an object tagged <c>delivery_point</c> if the scene has one
/// (the level designer's choice), else beside the buyer's mailbox (or any mailbox), else in front of a spawn point. The
/// spot is searched on rings around the anchor, dropped to the floor and tested for room, so deliveries never stack
/// inside each other or in a wall.
/// </summary>
public static class DeliverySpot
{
	/// <summary>The tag a level places to say where deliveries land.</summary>
	public const string Tag = "delivery_point";

	/// <summary>The room an object needs on the ground (the biggest Depot object plus a margin).</summary>
	public static readonly BBox Hull = new( new Vector3( -50, -40, 0 ), new Vector3( 50, 40, 55 ) );

	static readonly float[] Radii = [0f, 130f, 200f, 270f, 340f];

	const int Angles = 8;

	/// <summary>How far above or below the anchor a floor may be and still count.</summary>
	const float MaxStep = 24f;

	/// <summary>
	/// A free spot for one object and a phrase saying where it is, or null when the scene has nowhere to put it.
	/// </summary>
	public static (Vector3 Position, string Where)? Find(
		Scene scene, ulong owner, IReadOnlyCollection<Vector3> taken = null )
	{
		if ( !scene.IsValid() )
			return null;

		foreach ( var (anchor, where, yaw) in Anchors( scene, owner ) )
		{
			if ( Search( scene, anchor, yaw, taken ) is { } spot )
				return (spot, where);
		}

		return null;
	}

	static IEnumerable<(Vector3 Anchor, string Where, float Yaw)> Anchors( Scene scene, ulong owner )
	{
		foreach ( var go in scene.GetAllObjects( true ).Where( x => x.Tags.Has( Tag ) ) )
			yield return (go.WorldPosition, "at the delivery point", go.WorldRotation.Yaw());

		var boxes = scene.GetAll<MailBox>().ToList();

		foreach ( var box in boxes.OrderBy( x => x.OwnerId == owner ? 0 : 1 ) )
		{
			if ( box.OwnerId != 0 && box.OwnerId != owner )
				continue;

			yield return (box.WorldPosition, box.OwnerId == owner ? "beside your mailbox" : "beside the mailbox",
				box.WorldRotation.Yaw());
		}

		foreach ( var spawn in scene.GetAll<SpawnPoint>() )
		{
			var yaw = spawn.WorldRotation.Yaw();
			yield return (spawn.WorldPosition + Rotation.FromYaw( yaw ).Forward * 150f, "near where you spawn", yaw);
		}
	}

	static Vector3? Search( Scene scene, Vector3 anchor, float yaw, IReadOnlyCollection<Vector3> taken )
	{
		foreach ( var radius in Radii )
		{
			for ( var i = 0; i < (radius == 0f ? 1 : Angles); i++ )
			{
				var direction = Rotation.FromYaw( yaw + i * 360f / Angles ).Forward;
				var candidate = anchor + direction * radius;

				if ( Free( scene, candidate, anchor.z ) is { } floor && !IsTaken( floor, taken ) )
					return floor;
			}
		}

		return null;
	}

	/// <summary>
	/// Whether a spot is too near one chosen a moment ago, which has no body in the physics world yet to be traced.
	/// </summary>
	public static bool IsTaken( Vector3 spot, IReadOnlyCollection<Vector3> taken ) =>
		taken is not null && taken.Any( x => System.Math.Abs( x.x - spot.x ) < Hull.Size.x
		                                      && System.Math.Abs( x.y - spot.y ) < Hull.Size.y );

	/// <summary>
	/// The candidate dropped to the floor, if the hull fits there and nothing stands in it.
	/// </summary>
	static Vector3? Free( Scene scene, Vector3 candidate, float level )
	{
		var from = candidate + Vector3.Up * 60f;
		var ground = scene.Trace.Box( Hull, from, candidate + Vector3.Down * 200f ).WithoutTags( "trigger" ).Run();

		// Not the top of a wall or a table: the floor is near the anchor's own level.
		if ( !ground.Hit || ground.StartedSolid || System.Math.Abs( ground.EndPosition.z - level ) > MaxStep )
			return null;

		var floor = ground.EndPosition + Vector3.Up * 2f;
		var overlap = scene.Trace.Box( Hull, floor, floor ).WithoutTags( "trigger" ).Run();

		return overlap.Hit ? null : floor;
	}
}
