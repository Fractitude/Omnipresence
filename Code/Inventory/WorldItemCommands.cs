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

	/// <summary>
	/// <c>world_items_reset_scene</c> forgets what was taken from the items placed by hand in the editor, in this scene, so
	/// they are all there again the next time it loads (restart play). Dropped items are left alone.
	/// </summary>
	[ConCmd( "world_items_reset_scene" )]
	public static void ResetScene()
	{
		if ( !Application.IsEditor || Networking.IsActive && !Networking.IsHost )
		{
			Log.Info( "Only in the editor, on the host" );
			return;
		}

		var removed = WorldItemRegistry.ResetHandPlaced( Game.ActiveScene );
		WorldItemRegistry.Flush();
		Log.Info( $"Forgot {removed} taken or changed hand-placed items of this scene. They come back when it loads again." );
	}
}
