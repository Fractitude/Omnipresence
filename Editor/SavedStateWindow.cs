using System.Diagnostics;
using System.IO;
using System.Text;

namespace Omnipresence.Editor;

/// <summary>
/// A window to look at what the game saved and clear parts of it, for when a save is corrupted. Everything the game saves
/// goes to its data folder (<see cref="Sandbox.FileSystem.Data"/>); each row here is one part of that folder.
/// </summary>
/// <remarks>
/// Cleared files go to the Recycle Bin, so a wrong click can be undone. The data folder is outside the repository, so git
/// has none of it. If the Recycle Bin can't be used, nothing is deleted.
/// </remarks>
public sealed class SavedStateWindow : BaseWindow
{
	/// <summary>
	/// One part of the saved state: what it is in plain words, and the paths in the data folder that hold it.
	/// </summary>
	sealed record Entry( string Title, string Explanation, string[] Paths );

	const string ClearText = "Move selected to Recycle Bin";

	static SavedStateWindow instance;

	readonly HashSet<string> selected = [];
	ScrollArea list;
	Label summary;
	Button clearButton;
	bool wasPlaying;
	List<Entry> entries = [];

	public static void Open()
	{
		if ( instance.IsValid() )
		{
			instance.Refresh();
			instance.Focus();
			return;
		}

		instance = new SavedStateWindow();
		instance.Show();
	}

	SavedStateWindow()
	{
		Size = new Vector2( 640, 640 );
		MinimumSize = new Vector2( 480, 360 );
		WindowTitle = "Omnipresence: Saved State";
		SetWindowIcon( "delete_sweep" );

		Layout = Layout.Column();
		Layout.Margin = 8;
		Layout.Spacing = 8;

		var intro = Layout.Add( new Label( this )
		{
			WordWrap = true,
			Text = "Everything the game saves lives in its data folder. Tick the parts to clear, then press the button. "
			       + "Cleared files go to the Recycle Bin. The button is off while the game runs, since it would save its state back."
		} );
		intro.SetStyles( "color: #aaa;" );

		list = new ScrollArea( this );
		Layout.Add( list, 1 );

		summary = Layout.Add( new Label( this ) );
		summary.SetStyles( "color: #aaa;" );

		var footer = Layout.AddRow();
		footer.Spacing = 8;
		footer.Add( new Button( "Select all", "select_all", this ) { Clicked = () => SelectAll( true ) } );
		footer.Add( new Button( "Select none", "deselect", this ) { Clicked = () => SelectAll( false ) } );
		footer.Add( new Button( "Refresh", "refresh", this ) { Clicked = Refresh } );
		footer.AddStretchCell();
		clearButton = footer.Add( new Button.Primary( ClearText, "delete", this ) { Clicked = Ask } );

		Refresh();
	}

	/// <summary>
	/// Follow the game starting and stopping, so the button can switch off while it runs.
	/// </summary>
	[EditorEvent.Frame]
	void CheckPlaying()
	{
		if ( wasPlaying == Game.IsPlaying )
			return;

		wasPlaying = Game.IsPlaying;
		UpdateSummary();
	}

	/// <summary>
	/// Read the data folder again and rebuild the rows. What was ticked stays ticked, if it is still there.
	/// </summary>
	void Refresh()
	{
		entries = Collect();
		selected.RemoveWhere( x => entries.All( e => e.Title != x ) );

		list.Canvas = new Widget( list );
		list.Canvas.Layout = Layout.Column();
		list.Canvas.Layout.Margin = new Sandbox.UI.Margin( 8, 8, 16, 8 );
		list.Canvas.Layout.Spacing = 12;

		if ( entries.Count == 0 )
			list.Canvas.Layout.Add( new Label( "There is no saved state." ) );

		foreach ( var entry in entries )
			AddRow( entry );

		list.Canvas.Layout.AddStretchCell();
		UpdateSummary();
	}

	void AddRow( Entry entry )
	{
		var column = list.Canvas.Layout.AddColumn();
		column.Spacing = 2;

		var check = new Checkbox( entry.Title ) { Value = selected.Contains( entry.Title ) };
		check.Toggled = () =>
		{
			if ( check.Value )
				selected.Add( entry.Title );
			else
				selected.Remove( entry.Title );

			UpdateSummary();
		};
		column.Add( check );

		var explanation = column.Add( new Label( entry.Explanation ) { WordWrap = true } );
		explanation.SetStyles( "color: #aaa; margin-left: 24px;" );

		var files = column.Add( new Label( Describe( entry ) ) { WordWrap = true } );
		files.SetStyles( "color: #777; margin-left: 24px;" );
	}

	void SelectAll( bool on )
	{
		selected.Clear();

		if ( on )
			selected.UnionWith( entries.Select( x => x.Title ) );

		Refresh();
	}

	void UpdateSummary()
	{
		var chosen = entries.Where( x => selected.Contains( x.Title ) ).ToList();
		var bytes = chosen.Sum( x => Sizes( x ) );

		summary.Text = chosen.Count == 0 ? "Nothing selected." : $"{chosen.Count} selected, {FormatSize( bytes )}.";
		clearButton.Enabled = chosen.Count > 0 && !Game.IsPlaying;
		clearButton.Text = Game.IsPlaying ? ClearText + " (game must be stopped)" : ClearText;
	}

	/// <summary>
	/// Ask for a last yes, listing exactly what moves.
	/// </summary>
	void Ask()
	{
		var chosen = entries.Where( x => selected.Contains( x.Title ) ).ToList();

		if ( chosen.Count == 0 )
			return;

		var text = new StringBuilder( "These are moved to the Recycle Bin:\n\n" );

		foreach ( var entry in chosen )
			text.Append( "  " ).Append( entry.Title ).Append( ": " ).Append( string.Join( ", ", entry.Paths ) ).Append( '\n' );

		Dialog.AskConfirm( () => Clear( chosen ), text.ToString(), "Clear saved state", "Move to Recycle Bin" );
	}

	void Clear( List<Entry> chosen )
	{
		var data = Sandbox.FileSystem.Data;
		var toTrash = new List<string>();

		foreach ( var path in chosen.SelectMany( x => x.Paths ).Where( Exists ) )
			toTrash.Add( Path.GetFullPath( data.GetFullPath( path ) ) );

		if ( toTrash.Count > 0 && !MoveToRecycleBin( toTrash, out var error ) )
		{
			Log.Warning( $"Couldn't clear saved state, nothing was deleted: {error}" );
			EditorUtility.DisplayDialog( "Clear saved state", $"Couldn't use the Recycle Bin, so nothing was deleted.\n\n{error}" );
			return;
		}

		Log.Info( $"Cleared saved state: {string.Join( ", ", chosen.Select( x => x.Title ) )}." );
		selected.Clear();
		Refresh();
	}

	/// <summary>
	/// What is saved now: the parts that have something in the data folder, and anything no part knows about.
	/// </summary>
	static List<Entry> Collect()
	{
		var data = Sandbox.FileSystem.Data;
		var all = new List<Entry>();

		all.Add( new Entry(
			"Player progression",
			"Every player's XP, level, hacking XP and operation grades, money, credits, hardware inventory and installed "
			+ "hardware, blueprints, breached servers, packages on their way and payment history. Players start over as new.",
			data.FindFile( "progress", "*.json" ).Select( x => $"progress/{x}" ).ToArray() ) );

		all.Add( new Entry(
			"World clock",
			"The game's time and date. The world starts again at 12:00 on day 1. Packages on their way and payment "
			+ "times are saved in each player against the old time, so clear those players too if they look wrong.",
			["world.json"] ) );

		all.Add( new Entry(
			"Navi computers",
			"Everything on the Navi computers and their drives: files, installed apps, settings, lock passwords and "
			+ "window layouts. Every computer boots as new.",
			["navi/saves"] ) );

		all.Add( new Entry(
			"Navi messages",
			"Every player's conversations in the Messages app. Game characters write to players again from the start.",
			["navi/messages.json"] ) );

		all.Add( new Entry(
			"Navi plugins",
			"The list of plugins the host installed and turned on. They have to be installed again.",
			["navi/plugins.json"] ) );

		all = all.Where( x => x.Paths.Any( Exists ) ).ToList();

		// Whatever is left in the data folder that no row above covers.
		var known = all.SelectMany( x => x.Paths ).Concat( ["progress", "navi"] ).ToHashSet();
		var other = data.FindDirectory( "/" ).Concat( data.FindFile( "/" ) ).Where( x => !known.Contains( x ) ).ToList();

		if ( data.DirectoryExists( "progress" ) )
			other.AddRange( data.FindFile( "progress" ).Where( x => !x.EndsWith( ".json" ) ).Select( x => $"progress/{x}" ) );

		if ( data.DirectoryExists( "navi" ) )
		{
			var navi = data.FindDirectory( "navi" ).Select( x => $"navi/{x}" )
				.Concat( data.FindFile( "navi" ).Select( x => $"navi/{x}" ) )
				.Where( x => !known.Contains( x ) );

			other.AddRange( navi );
		}

		if ( other.Count > 0 )
		{
			all.Add( new Entry(
				"Other files",
				"Files in the data folder that nothing above covers. They are from an older version of the game or from "
				+ "something else that saves here.",
				other.ToArray() ) );
		}

		return all;
	}

	static bool Exists( string path ) => Sandbox.FileSystem.Data.FileExists( path ) || Sandbox.FileSystem.Data.DirectoryExists( path );

	static string Describe( Entry entry )
	{
		var present = entry.Paths.Where( Exists ).ToList();
		var bytes = Sizes( entry );

		return $"{string.Join( ", ", present )}  ({FormatSize( bytes )})";
	}

	static long Sizes( Entry entry ) => entry.Paths.Where( Exists ).Sum( x => SizeOf( Path.GetFullPath( Sandbox.FileSystem.Data.GetFullPath( x ) ) ) );

	static long SizeOf( string full )
	{
		if ( File.Exists( full ) )
			return new FileInfo( full ).Length;

		return Directory.Exists( full )
			? new DirectoryInfo( full ).EnumerateFiles( "*", SearchOption.AllDirectories ).Sum( x => x.Length )
			: 0;
	}

	static string FormatSize( long bytes ) => bytes switch
	{
		< 1024 => $"{bytes} B",
		< 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
		_ => $"{bytes / 1024.0 / 1024.0:0.#} MB"
	};

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
