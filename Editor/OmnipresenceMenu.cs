namespace Omnipresence.Editor;

/// <summary>
/// The "Omnipresence" menu in the editor: developer tools, like wiping a corrupted save.
/// </summary>
public static class OmnipresenceMenu
{
	[Menu( "Editor", "Omnipresence/Clear All Saved State..." )]
	public static void ClearSavedState()
	{
		var entries = SavedEntries();

		if ( entries.Count == 0 )
		{
			EditorUtility.DisplayDialog( "Clear all saved state", "There is no saved state." );
			return;
		}

		var running = Game.IsPlaying ? "\n\nThe game is running. Stop it first, or it will write its state back." : "";

		Dialog.AskConfirm(
			() => Clear( entries ),
			"This deletes everything the game saved: progress, world clock, Navi computers, messages and plugins.\n\n"
			+ string.Join( "\n", entries.Select( x => $"  {x}" ) )
			+ running
			+ "\n\nThis can't be undone.",
			"Clear all saved state",
			"Delete everything" );
	}

	/// <summary>
	/// Everything in the game's data folder: folders end with a slash.
	/// </summary>
	static List<string> SavedEntries()
	{
		var data = Sandbox.FileSystem.Data;

		return data.FindDirectory( "/" )
			.Select( x => x + "/" )
			.Concat( data.FindFile( "/" ) )
			.ToList();
	}

	static void Clear( List<string> entries )
	{
		var data = Sandbox.FileSystem.Data;

		foreach ( var entry in entries )
		{
			try
			{
				if ( entry.EndsWith( '/' ) )
					data.DeleteDirectory( entry.TrimEnd( '/' ), true );
				else
					data.DeleteFile( entry );
			}
			catch ( System.Exception e )
			{
				Log.Warning( $"Couldn't delete {entry}: {e.Message}" );
			}
		}

		Log.Info( $"Cleared saved state: {entries.Count} entries deleted." );
	}
}
