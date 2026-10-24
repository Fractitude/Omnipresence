namespace Sandbox;

/// <summary>
/// Console commands for the dropped items. Editor only, on the host, like the other dev commands.
/// </summary>
public static class WorldItemCommands
{
	/// <summary>
	/// <c>world_items_clear</c> removes every dropped item from the scene and from the world file.
	/// </summary>
	[ConCmd( "world_items_clear" )]
	public static void Clear()
	{
		if ( !Application.IsEditor || Networking.IsActive && !Networking.IsHost )
		{
			Log.Info( "Only in the editor, on the host" );
			return;
		}

		var removed = WorldItemRegistry.Wipe( Game.ActiveScene );
		Log.Info( $"Removed {removed} world items and cleared {WorldItemRegistry.FileName}" );
	}
}
