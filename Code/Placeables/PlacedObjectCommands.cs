namespace Sandbox;

/// <summary>
/// Console commands for the Depot's objects. Editor only, on the host, like the other dev commands.
/// </summary>
public static class PlacedObjectCommands
{
	/// <summary>
	/// <c>placed_objects_clear</c> removes every Depot object from the scene, every purchase on its way, and the file.
	/// </summary>
	[ConCmd( "placed_objects_clear" )]
	public static void Clear()
	{
		if ( !Application.IsEditor || Networking.IsActive && !Networking.IsHost )
		{
			Log.Info( "Only in the editor, on the host" );
			return;
		}

		var removed = PlacedObjectRegistry.Wipe( Game.ActiveScene );
		Log.Info( $"Removed {removed} placed objects and cleared {PlacedObjectRegistry.FileName}" );
	}

	/// <summary>
	/// <c>placed_objects_deliver_now</c> makes every purchase on its way arrive at once. For testing.
	/// </summary>
	[ConCmd( "placed_objects_deliver_now" )]
	public static void DeliverNow()
	{
		if ( !Application.IsEditor || Networking.IsActive && !Networking.IsHost )
		{
			Log.Info( "Only in the editor, on the host" );
			return;
		}

		foreach ( var delivery in PlacedObjectRegistry.Data.Deliveries.Where( x => !x.Arrived ) )
			delivery.ArriveAt = WorldClock.Now;

		Game.ActiveScene?.GetSystem<PlacedObjectSystem>()?.Advance( WorldClock.Now );
	}
}
