namespace Omnipresence.Editor;

/// <summary>
/// The body of the inspector's "Saved state" section: the parts of one computer's save that can be cleared on their own.
/// Same mechanism as <see cref="SavedStateWindow"/>: the old file goes to the Recycle Bin, so a wrong click can be
/// undone. A part that is only some of the computer's settings is reset by moving the whole file to the Recycle Bin and
/// writing it again without those settings.
/// </summary>
/// <remarks>
/// A computer is saved in every save slot (<c>navi/saves/{slot}/{key}.json</c>); the parts are cleared in each slot that
/// has them. Off while the game runs, since the computer would save its state back.
/// </remarks>
sealed class SavedStatePanel : Widget
{
	/// <summary>
	/// One part of the computer's save: what it is in plain words, and how to put it back to a new computer's value.
	/// </summary>
	sealed record Part( string Title, string Explanation, System.Action<NaviComputerSave, NaviComputerSave> Reset );

	const string DriveTitle = "Files";
	const string ClearText = "Move selected to Recycle Bin";

	static readonly Part[] Parts =
	[
		new( "Apps",
			"Installed apps and packages. The scene's apps will be installed again.",
			( s, d ) =>
			{
				s.InstalledApps = d.InstalledApps;
				s.ConfiguredApps = d.ConfiguredApps;
				s.InstalledPackages = d.InstalledPackages;
				s.TaskbarOrder = d.TaskbarOrder;
				s.StartFavorites = d.StartFavorites;
				s.InstalledAt = d.InstalledAt;
			} ),
		new( "Permissions",
			"Granted permissions and root apps.",
			( s, d ) =>
			{
				s.GrantedPermissions = d.GrantedPermissions;
				s.RootApps = d.RootApps;
				s.ReviewedApps = d.ReviewedApps;
			} ),
		new( "Notifications",
			"Notifications and muted apps.",
			( s, d ) =>
			{
				s.Notifications = d.Notifications;
				s.MutedNotificationApps = d.MutedNotificationApps;
				s.LastNotificationId = d.LastNotificationId;
				s.NotificationsReadUpTo = d.NotificationsReadUpTo;
			} ),
		new( "Lock",
			"The lock password or pattern.",
			( s, d ) =>
			{
				s.LockKind = d.LockKind;
				s.LockSalt = d.LockSalt;
				s.LockVerifier = d.LockVerifier;
			} ),
		new( "Appearance",
			"Theme, color scheme, wallpaper, etc.",
			( s, d ) =>
			{
				s.Theme = d.Theme;
				s.ColorScheme = d.ColorScheme;
				s.Wallpaper = d.Wallpaper;
				s.BlurLevel = d.BlurLevel;
				s.TransparencyLevel = d.TransparencyLevel;
				s.DisplayScale = d.DisplayScale;
			} ),
		new( "Window layout",
			"Window positions and snapping.",
			( s, d ) =>
			{
				s.WindowPlacements = d.WindowPlacements;
				s.SnapToScreenEdges = d.SnapToScreenEdges;
				s.SnapToWindows = d.SnapToWindows;
				s.SnapDistance = d.SnapDistance;
			} ),
	];

	readonly NaviComputer computer;
	/// <summary>
	/// About how many characters of the explanation fit on a line, and how tall a line is, to size the wrapped text.
	/// </summary>
	const int CharsPerLine = 52;
	const int LineHeight = 16;

	readonly HashSet<string> selected = [];
	SubHeader header;
	Widget body;
	bool open;
	Button clearButton;
	bool wasPlaying;
	List<string> titles = [];

	public SavedStatePanel( Widget parent, NaviComputer computer ) : base( parent )
	{
		this.computer = computer;

		Layout = Layout.Column();
		Layout.Spacing = 6;

		Rebuild();
	}

	[EditorEvent.Frame]
	void CheckPlaying()
	{
		if ( !this.IsValid() || wasPlaying == Game.IsPlaying )
			return;

		wasPlaying = Game.IsPlaying;
		UpdateSummary();
	}

	/// <summary>
	/// The save files this computer has, one per slot: its settings, and the files on its drive.
	/// </summary>
	(List<string> Settings, List<string> Drive) Find()
	{
		var data = Sandbox.FileSystem.Data;
		var settings = new List<string>();
		var drive = new List<string>();

		if ( !data.DirectoryExists( "navi/saves" ) )
			return (settings, drive);

		foreach ( var slot in data.FindDirectory( "navi/saves" ) )
		{
			var path = $"navi/saves/{slot}/{computer.SaveKey}.json";
			var drivePath = $"navi/saves/{slot}/drives/{computer.SaveKey}.json";

			if ( data.FileExists( path ) )
				settings.Add( path );

			if ( data.FileExists( drivePath ) )
				drive.Add( drivePath );
		}

		return (settings, drive);
	}

	void Rebuild()
	{
		Layout.Clear( true );

		var (settings, drive) = Find();

		titles = Parts.Where( _ => settings.Count > 0 ).Select( x => x.Title ).ToList();

		if ( drive.Count > 0 )
			titles.Add( DriveTitle );

		selected.RemoveWhere( x => !titles.Contains( x ) );

		var key = Layout.Add( new Label( $"Save key: {computer.SaveKey}", this ) );
		key.SetStyles( "color: #7a7a84; padding: 4px 0 8px 0;" );

		if ( titles.Count == 0 )
		{
			var none = Layout.Add( new Label( "This computer has no saved state.", this ) );
			none.SetStyles( "color: #aaa;" );
		}

		if ( titles.Count > 0 )
		{
			header = Layout.Add( new SubHeader( this ) );

			body = Layout.Add( new Widget( this ) );
			body.Layout = Layout.Column();
			body.Layout.Spacing = 6;
			body.Layout.Margin = new Sandbox.UI.Margin( 4, 4, 16, 0 );
			body.Visible = open;

			var tools = body.Layout.AddRow();
			tools.Spacing = 6;
			tools.Add( new Button( "Select all", "select_all", this ) { Clicked = () => SelectAll( true ) } );
			tools.Add( new Button( "Select none", "deselect", this ) { Clicked = () => SelectAll( false ) } );
			tools.AddStretchCell();
			body.Layout.AddSpacingCell( 6 );

			foreach ( var part in Parts.Where( _ => settings.Count > 0 ) )
				AddRow( part.Title, part.Explanation );

			if ( drive.Count > 0 )
				AddRow( DriveTitle, "The files on the computer's drive." );

			body.Layout.AddSpacingCell( 8 );
			clearButton = body.Layout.Add( new Button.Primary( ClearText, "delete", this ) { Clicked = Ask } );
		}

		UpdateSummary();
	}

	void AddRow( string title, string explanation )
	{
		var layout = body.Layout;

		var check = new Tick( this, title, selected.Contains( title ) );
		check.Toggled = on =>
		{
			if ( on )
				selected.Add( title );
			else
				selected.Remove( title );

			UpdateSummary();
		};
		layout.Add( check );

		// A wrapped label doesn't report the height of its wrapped text, so it would be cut off. Give it the lines it needs.
		var lines = (explanation.Length + CharsPerLine - 1) / CharsPerLine;
		var text = layout.Add( new Label( explanation, this ) { WordWrap = true } );
		text.MinimumHeight = lines * LineHeight + 4;
		text.SetStyles( "color: #aaa; margin-left: 28px;" );
	}

	void SelectAll( bool on )
	{
		selected.Clear();

		if ( on )
			selected.UnionWith( titles );

		Rebuild();
	}

	void UpdateSummary()
	{
		header?.Update();

		if ( clearButton is null )
			return;

		clearButton.Enabled = selected.Count > 0 && !Game.IsPlaying;
		clearButton.Text = Game.IsPlaying ? ClearText + " (game must be stopped)" : ClearText;
	}

	void Ask()
	{
		if ( selected.Count == 0 )
			return;

		var text = $"These parts of computer '{computer.SaveKey}' are cleared. The old save goes to the Recycle Bin:\n\n  "
		           + string.Join( "\n  ", titles.Where( selected.Contains ) );

		Dialog.AskConfirm( Clear, text, "Clear saved state", "Move to Recycle Bin" );
	}

	void Clear()
	{
		var data = Sandbox.FileSystem.Data;
		var (settings, drive) = Find();
		var chosen = Parts.Where( x => selected.Contains( x.Title ) ).ToList();
		var toTrash = new List<string>();
		var rewrites = new List<(string Path, NaviComputerSave Save)>();

		try
		{
			foreach ( var path in settings.Where( _ => chosen.Count > 0 ) )
			{
				toTrash.Add( Full( path ) );

				// Every part chosen: nothing is left to write back.
				if ( chosen.Count == Parts.Length )
					continue;

				var save = data.ReadJson<NaviComputerSave>( path );
				var fresh = new NaviComputerSave();

				foreach ( var part in chosen )
					part.Reset( save, fresh );

				rewrites.Add( (path, save) );
			}

			if ( selected.Contains( DriveTitle ) )
				toTrash.AddRange( drive.Select( Full ) );
		}
		catch ( System.Exception e )
		{
			Log.Warning( $"Couldn't read the saved state, nothing was deleted: {e.Message}" );
			EditorUtility.DisplayDialog( "Clear saved state", $"Couldn't read the saved state, so nothing was deleted.\n\n{e.Message}" );
			return;
		}

		if ( toTrash.Count > 0 && !SavedStateWindow.MoveToRecycleBin( toTrash, out var error ) )
		{
			Log.Warning( $"Couldn't clear saved state, nothing was deleted: {error}" );
			EditorUtility.DisplayDialog( "Clear saved state", $"Couldn't use the Recycle Bin, so nothing was deleted.\n\n{error}" );
			return;
		}

		foreach ( var (path, save) in rewrites )
			data.WriteJson( path, save );

		Log.Info( $"Cleared saved state of computer '{computer.SaveKey}': {string.Join( ", ", titles.Where( selected.Contains ) )}." );
		selected.Clear();
		Rebuild();
	}

	static string Full( string path ) => System.IO.Path.GetFullPath( Sandbox.FileSystem.Data.GetFullPath( path ) );

	void Toggle()
	{
		open = !open;
		body.Visible = open;
		header.Update();
	}

	/// <summary>
	/// A rounded tick box with its title, drawn to match the inspector's sections.
	/// </summary>
	sealed class Tick : Widget
	{
		bool value;

		public System.Action<bool> Toggled;

		public Tick( Widget parent, string title, bool value ) : base( parent )
		{
			this.value = value;
			Title = title;
			FixedHeight = 24;
			Cursor = CursorShape.Finger;
		}

		string Title { get; }

		protected override void OnMouseClick( MouseEvent e )
		{
			value = !value;
			Update();
			Toggled?.Invoke( value );
		}

		protected override void OnPaint()
		{
			var box = new Rect( LocalRect.Left + 2, LocalRect.Top + 3, 18, 18 );

			if ( value )
			{
				Paint.ClearPen();
				Paint.SetBrush( Color.Parse( "#4a9eea" ) ?? Color.Magenta );
				Paint.DrawRect( box, 4 );

				Paint.SetPen( Color.White );
				Paint.DrawIcon( box, "check", 14, TextFlag.Center );
			}
			else
			{
				Paint.SetPen( Color.Parse( Paint.HasMouseOver ? "#8a9aaa" : "#5a5a64" ) ?? Color.Magenta );
				Paint.SetBrush( Color.Parse( "#1e1e22" ) ?? Color.Magenta );
				Paint.DrawRect( box.Shrink( 0.5f ), 4 );
			}

			Paint.SetDefaultFont( 9, 600 );
			Paint.SetPen( Color.Parse( "#e6eef7" ) ?? Color.Magenta );
			Paint.DrawText( new Rect( box.Right + 8, LocalRect.Top, LocalRect.Width - 32, LocalRect.Height ), Title, TextFlag.LeftCenter );
		}
	}

	/// <summary>
	/// The clickable line that opens and closes the list of parts.
	/// </summary>
	sealed class SubHeader : Widget
	{
		readonly SavedStatePanel owner;

		public SubHeader( SavedStatePanel owner ) : base( owner )
		{
			this.owner = owner;
			FixedHeight = 26;
			Cursor = CursorShape.Finger;
		}

		protected override void OnMouseClick( MouseEvent e ) => owner.Toggle();

		protected override void OnPaint()
		{
			var rect = LocalRect;
			var count = owner.selected.Count;

			Paint.ClearPen();
			Paint.SetBrush( Color.Parse( Paint.HasMouseOver ? "#34343a" : "#2e2e33" ) ?? Color.Magenta );
			Paint.DrawRect( rect, 4 );

			Paint.SetPen( Color.Parse( "#9ab0c4" ) ?? Color.Magenta );
			Paint.DrawIcon( new Rect( rect.Left + 6, rect.Top, 20, rect.Height ), owner.open ? "expand_more" : "chevron_right", 16, TextFlag.Center );

			Paint.SetDefaultFont( 9, 600 );
			Paint.SetPen( Color.Parse( "#e6eef7" ) ?? Color.Magenta );
			Paint.DrawText( new Rect( rect.Left + 30, rect.Top, rect.Width - 36, rect.Height ),
				count == 0 ? "Clear saved state" : $"Clear saved state ({count} selected)", TextFlag.LeftCenter );
		}
	}
}
