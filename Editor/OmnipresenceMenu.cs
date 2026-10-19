using System.Diagnostics;
using System.IO;
using System.Text;

namespace Omnipresence.Editor;

/// <summary>
/// The "Omnipresence" menu in the editor: developer tools, like wiping a corrupted save. Everything the game saves goes
/// to its data folder (<see cref="Sandbox.FileSystem.Data"/>), so each item here clears part of that folder.
/// </summary>
/// <remarks>
/// Cleared files go to the Recycle Bin, so a wrong click can be undone. Git tracked files are deleted for good: git has
/// them. If the Recycle Bin can't be used, nothing is deleted.
/// </remarks>
public static class OmnipresenceMenu
{
	const string Root = "Omnipresence/Clear Saved State/";

	/// <summary>
	/// One thing the menu can clear: what it is in plain words, and the paths in the data folder that hold it.
	/// </summary>
	sealed record Target( string Title, string Explanation, string[] Paths );

	static Target Progress = new(
		"Player progress",
		"Every player's XP, level, money, credits, hardware inventory and installed hardware, blueprints and breached "
		+ "servers, packages on their way and payment history. Players start over as new.",
		["progress"] );

	static Target Clock = new(
		"World clock",
		"The game's time and date. The world starts again at 12:00 on day 1. Packages on their way and payment times "
		+ "are kept in Player progress against the old time, so clear that too if they look wrong.",
		["world.json"] );

	static Target Computers = new(
		"Navi computers",
		"Everything on the Navi computers and their drives: files, installed apps, settings, lock passwords and "
		+ "window layouts. Every computer boots as new. Messages and plugins are separate.",
		["navi/saves"] );

	static Target Messages = new(
		"Navi messages",
		"Every player's conversations in the Messages app. Game characters write to players again from the start.",
		["navi/messages.json"] );

	static Target Plugins = new(
		"Navi plugins",
		"The list of plugins the host installed and turned on. They have to be installed again.",
		["navi/plugins.json"] );

	[Menu( "Editor", Root + "Player progress..." )]
	public static void ClearProgress() => Ask( Progress );

	[Menu( "Editor", Root + "World clock..." )]
	public static void ClearClock() => Ask( Clock );

	[Menu( "Editor", Root + "Navi computers..." )]
	public static void ClearComputers() => Ask( Computers );

	[Menu( "Editor", Root + "Navi messages..." )]
	public static void ClearMessages() => Ask( Messages );

	[Menu( "Editor", Root + "Navi plugins..." )]
	public static void ClearPlugins() => Ask( Plugins );

	[Menu( "Editor", Root + "Everything..." )]
	public static void ClearEverything()
	{
		var data = Sandbox.FileSystem.Data;

		// Whatever is in the folder, including files no item above knows about.
		var everything = new Target(
			"Everything",
			"All of the above, and anything else the game or its libraries saved. The game starts as if it was never played.",
			data.FindDirectory( "/" ).Concat( data.FindFile( "/" ) ).ToArray() );

		Ask( everything );
	}

	/// <summary>
	/// Show what would be cleared and ask. Nothing happens until the player agrees.
	/// </summary>
	static void Ask( Target target )
	{
		var data = Sandbox.FileSystem.Data;
		var present = target.Paths.Where( x => data.FileExists( x ) || data.DirectoryExists( x ) ).ToList();

		if ( present.Count == 0 )
		{
			EditorUtility.DisplayDialog( $"Clear {target.Title}", $"There is nothing saved for {target.Title}." );
			return;
		}

		var text = new StringBuilder();
		text.Append( target.Explanation ).Append( "\n\nThis moves these to the Recycle Bin:\n" );

		foreach ( var path in present )
			text.Append( "  " ).Append( path ).Append( data.DirectoryExists( path ) ? "/" : "" ).Append( '\n' );

		if ( Game.IsPlaying )
			text.Append( "\nThe game is running. Stop it first, or it will write its state back.\n" );

		Dialog.AskConfirm( () => Clear( target, present ), text.ToString(), $"Clear {target.Title}", "Move to Recycle Bin" );
	}

	static void Clear( Target target, List<string> paths )
	{
		var data = Sandbox.FileSystem.Data;
		var toTrash = new List<string>();

		foreach ( var path in paths )
		{
			var full = Path.GetFullPath( data.GetFullPath( path ) );

			// Tracked files can be restored from git. A folder can hold untracked files too, so it always goes to the bin.
			if ( File.Exists( full ) && IsGitTracked( full ) )
			{
				File.Delete( full );
				Log.Info( $"Deleted {path}, it is tracked by git." );
				continue;
			}

			toTrash.Add( full );
		}

		if ( toTrash.Count > 0 && !MoveToRecycleBin( toTrash, out var error ) )
		{
			Log.Warning( $"Couldn't clear {target.Title}, nothing was deleted: {error}" );
			EditorUtility.DisplayDialog( $"Clear {target.Title}", $"Couldn't use the Recycle Bin, so nothing was deleted.\n\n{error}" );
			return;
		}

		Log.Info( $"Cleared {target.Title}: {paths.Count} item(s)." );
	}

	static bool IsGitTracked( string fullPath )
	{
		try
		{
			var info = new ProcessStartInfo( "git" )
			{
				WorkingDirectory = Path.GetDirectoryName( fullPath ),
				CreateNoWindow = true,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true
			};

			info.ArgumentList.Add( "ls-files" );
			info.ArgumentList.Add( "--" );
			info.ArgumentList.Add( fullPath );

			using var process = Process.Start( info );
			var output = process.StandardOutput.ReadToEnd();
			process.WaitForExit();

			return process.ExitCode == 0 && !string.IsNullOrWhiteSpace( output );
		}
		catch
		{
			// No git, or not in a repository: nothing is tracked.
			return false;
		}
	}

	/// <summary>
	/// Windows: PowerShell asks the shell to move each path to the Recycle Bin. Only an existing path is touched.
	/// </summary>
	static bool MoveToRecycleBin( List<string> fullPaths, out string error )
	{
		var script = new StringBuilder( "$ErrorActionPreference = 'Stop'; Add-Type -AssemblyName Microsoft.VisualBasic;" );

		foreach ( var path in fullPaths )
		{
			var quoted = "'" + path.Replace( "'", "''" ) + "'";

			script.Append( $"if (Test-Path -LiteralPath {quoted} -PathType Container) {{ " )
				.Append( $"[Microsoft.VisualBasic.FileIO.FileSystem]::DeleteDirectory({quoted}, 'OnlyErrorDialogs', 'SendToRecycleBin') }} " )
				.Append( $"elseif (Test-Path -LiteralPath {quoted}) {{ " )
				.Append( $"[Microsoft.VisualBasic.FileIO.FileSystem]::DeleteFile({quoted}, 'OnlyErrorDialogs', 'SendToRecycleBin') }};" );
		}

		try
		{
			var info = new ProcessStartInfo( "powershell" )
			{
				CreateNoWindow = true,
				UseShellExecute = false,
				RedirectStandardError = true
			};

			info.ArgumentList.Add( "-NoProfile" );
			info.ArgumentList.Add( "-NonInteractive" );
			info.ArgumentList.Add( "-Command" );
			info.ArgumentList.Add( script.ToString() );

			using var process = Process.Start( info );
			error = process.StandardError.ReadToEnd().Trim();
			process.WaitForExit();

			return process.ExitCode == 0;
		}
		catch ( System.Exception e )
		{
			error = e.Message;
			return false;
		}
	}
}
