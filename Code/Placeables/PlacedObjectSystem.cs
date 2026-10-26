namespace Sandbox;

/// <summary>
/// Drives the Depot on the host: restores the placed objects when the scene starts, makes each purchase arrive when its
/// time is up (whether or not the buyer is playing), tells buyers when theirs came, and writes the file a few seconds
/// after the last change and once more when the scene goes. Made in every scene by the engine.
/// </summary>
/// <remarks>
/// Host only. A <c>new Scene()</c> starts every system, so a system that is not in the active scene does nothing. A
/// restore and an arrival are both by id, so a hot reload could not duplicate anything.
/// </remarks>
public sealed class PlacedObjectSystem : GameObjectSystem<PlacedObjectSystem>
{
	/// <summary>How many saved things are made in one tick, so a long list does not hitch a frame. The first batch is made
	/// on the first tick the scene is ready.</summary>
	const int Batch = 50;

	bool restored;
	int total;
	int batches;
	float firstAt = -1f;
	readonly HashSet<string> skip = new();
	RealTimeSince sinceStart;
	RealTimeSince sinceTick;

	public PlacedObjectSystem( Scene scene ) : base( scene )
	{
		sinceStart = 0;
		Listen( Stage.StartUpdate, 1, Tick, "PlacedObjectSystem.Tick" );
	}

	void Tick()
	{
		if ( Scene != Game.ActiveScene || !Game.IsPlaying || Scene.IsEditor || Networking.IsActive && !Networking.IsHost )
			return;

		if ( !restored )
		{
			// Not a fixed wait: the first tick the scene is ready (its maps loaded, something to stand on).
			if ( !SceneReadiness.IsReady( Scene, sinceStart ) )
				return;

			var made = PlacedObjectRegistry.Restore( Scene, Batch, skip, out var attempted );
			total += made;
			batches++;

			if ( made > 0 && firstAt < 0f )
				firstAt = sinceStart;

			// Fewer tried than a batch holds: nothing is left.
			if ( attempted < Batch )
			{
				restored = true;

				if ( total > 0 )
					Log.Info(
						$"Placed objects: restored {total} in {batches} tick(s)"
						+ $", the first {firstAt * 1000f:0} ms and the last {sinceStart * 1000f:0} ms after the scene started" );
			}

			return;
		}

		if ( sinceTick < 1f )
			return;

		sinceTick = 0;

		if ( WorldClock.IsLoaded )
			Advance( WorldClock.Now );

		PlacedObjectRegistry.Tick();
	}

	/// <summary>
	/// Bring the deliveries up to <paramref name="now"/>: put down the ones whose time is up, and tell the buyers who
	/// are here about the ones that arrived. A delivery with nowhere to go waits and is tried again.
	/// </summary>
	public void Advance( long now )
	{
		var data = PlacedObjectRegistry.Data;
		var taken = new List<Vector3>();

		foreach ( var delivery in PlacedObjectRules.Due( data, now ) )
			Deliver( Scene, delivery, taken );

		foreach ( var delivery in data.Deliveries.Where( x => x.Arrived ).ToList() )
		{
			var buyer = Connection.All.FirstOrDefault( x => (ulong)x.SteamId == delivery.Owner );

			if ( buyer is null || PlayerCrime.For( buyer ) is not { Ready: true } crime )
				continue;

			var name = PlaceableCatalog.Find( delivery.Placeable )?.Name ?? "Your order";
			crime.Tell( CrimeResult.Success( $"Your {name} has arrived {delivery.Where}." ) );
			PlacedObjectRegistry.Told( delivery );
		}
	}

	/// <summary>
	/// Put one delivery down where its destination allows and write it down as arrived: a store pickup at a delivery
	/// point near the terminal it was bought at (nearest bay first), a home delivery beside the buyer's mailbox. Returns
	/// whether the object was made. Host only; <paramref name="taken"/> holds the spots chosen earlier in the same
	/// pass, which have no body yet.
	/// </summary>
	public static bool Deliver( Scene scene, PlacedDeliveryRecord delivery, List<Vector3> taken )
	{
		if ( delivery is null || !scene.IsValid() )
			return false;

		if ( PlaceableCatalog.Find( delivery.Placeable ) is not { } item )
			return false;

		var spot = delivery.Destination == PlacedObjectRules.DeliveryStore
			? DeliverySpot.FindStore( scene, delivery.Around, taken )
			: DeliverySpot.FindHome( scene, delivery.Owner, taken );

		if ( spot is not { } place )
			return false;

		var go = PlacedObjectRegistry.Create( item, delivery.Id, delivery.Owner, place.Position,
			Rotation.FromYaw( place.Yaw ), scene );

		if ( !go.IsValid() )
			return false;

		taken?.Add( place.Position );
		PlacedObjectRegistry.MarkArrived( delivery, place.Where );
		return true;
	}

	public override void Dispose()
	{
		base.Dispose();

		// The scene is going: write what is waiting. Only for a system that worked (not a preview scene's).
		if ( restored )
			PlacedObjectRegistry.Flush();
	}
}
