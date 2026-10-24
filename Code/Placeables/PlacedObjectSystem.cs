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
	/// <summary>Seconds after the game starts before the objects are made, so the map is up.</summary>
	const float RestoreDelay = 1.5f;

	bool restored;
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
			if ( sinceStart < RestoreDelay )
				return;

			restored = true;
			var made = PlacedObjectRegistry.Restore( Scene );

			if ( made > 0 )
				Log.Info( $"Placed objects: restored {made}" );

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
		{
			var item = PlaceableCatalog.Find( delivery.Placeable );

			if ( item is null || DeliverySpot.Find( Scene, delivery.Owner, taken ) is not { } spot )
				continue;

			var yaw = Rotation.FromYaw( System.Random.Shared.Next( 0, 360 ) );

			if ( PlacedObjectRegistry.Create( item, delivery.Id, delivery.Owner, spot.Position, yaw, Scene ).IsValid() )
			{
				taken.Add( spot.Position );
				PlacedObjectRegistry.MarkArrived( delivery, spot.Where );
			}
		}

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

	public override void Dispose()
	{
		base.Dispose();

		// The scene is going: write what is waiting. Only for a system that worked (not a preview scene's).
		if ( restored )
			PlacedObjectRegistry.Flush();
	}
}
