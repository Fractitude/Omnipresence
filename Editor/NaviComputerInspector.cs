namespace Omnipresence.Editor;

/// <summary>
/// The Inspector for <see cref="NaviComputer"/>: one panel for the whole computer. A live status card on top, then
/// collapsible sections that edit the computer, its <see cref="NaviInputCapture"/> and its <see cref="NaviDesktop"/>,
/// so none of the three components has to be opened. Whether a section is open is remembered between sessions.
/// </summary>
[CustomEditor( typeof( NaviComputer ) )]
public sealed class NaviComputerInspector : ComponentEditorWidget
{
	readonly List<Section> sections = [];

	public NaviComputerInspector( SerializedObject obj ) : base( obj )
	{
		SetSizeMode( SizeMode.Default, SizeMode.Default );

		Layout = Layout.Column();
		Layout.Spacing = 6;
		Layout.Margin = new Sandbox.UI.Margin( 0, 4, 0, 8 );

		Build();
	}

	void Build()
	{
		Layout.Clear( true );
		sections.Clear();

		var computer = SerializedObject.Targets.FirstOrDefault() as NaviComputer;

		if ( !computer.IsValid() )
			return;

		var desktop = computer.Components.Get<NaviDesktop>();
		var capture = computer.Components.Get<NaviInputCapture>();

		var computerObject = SerializedObject;
		var desktopObject = desktop.IsValid() ? desktop.GetSerialized() : null;
		var captureObject = capture.IsValid() ? capture.GetSerialized() : null;

		Layout.Add( new StatusCard( this, computer ) );

		var tools = Layout.AddRow();
		tools.Spacing = 6;
		tools.Add( new Button( "Expand all", "unfold_more", this ) { Clicked = () => SetAll( true ) } );
		tools.Add( new Button( "Collapse all", "unfold_less", this ) { Clicked = () => SetAll( false ) } );
		tools.AddStretchCell();

		Add( "Apps", "apps", false, () => Sheet( computerObject,
			nameof( NaviComputer.InstalledApps ), nameof( NaviComputer.InstalledPackages ),
			nameof( NaviComputer.PinnedApps ), nameof( NaviComputer.StartFavorites ), nameof( NaviComputer.ComputerTags ),
			nameof( NaviComputer.OnlyApps ), nameof( NaviComputer.SystemAppFolders ) ) );

		Add( "Permissions", "shield", false, () => Sheet( computerObject,
			nameof( NaviComputer.GrantedPermissions ), nameof( NaviComputer.RootApps ),
			nameof( NaviComputer.ReviewedApps ) ) );

		Add( "Files", "folder", false, () => Sheet( computerObject,
			nameof( NaviComputer.DriveName ), nameof( NaviComputer.DriveSizeMegabytes ) ) );

		Add( "Notifications", "notifications", false, () => Sheet( computerObject,
			nameof( NaviComputer.NotificationSound ), nameof( NaviComputer.MutedNotificationApps ) ) );

		Add( "Appearance", "palette", false, () => Sheet( computerObject,
			nameof( NaviComputer.Theme ), nameof( NaviComputer.ColorScheme ), nameof( NaviComputer.Wallpaper ),
			nameof( NaviComputer.BlurLevel ), nameof( NaviComputer.TransparencyLevel ),
			nameof( NaviComputer.DisplayScale ) ) );

		Add( "Windows", "web_asset", false, () =>
		{
			var sheet = new ControlSheet { IncludePropertyNames = true };
			Rows( sheet, computerObject,
				nameof( NaviComputer.SnapToScreenEdges ), nameof( NaviComputer.SnapToWindows ),
				nameof( NaviComputer.SnapDistance ) );

			if ( desktopObject is not null )
				Rows( sheet, desktopObject, nameof( NaviDesktop.MinWindowSize ), nameof( NaviDesktop.TooltipEdgeMargin ) );

			return Host( sheet );
		} );

		Add( "Seat and network", "event_seat", false, () => Sheet( computerObject,
			nameof( NaviComputer.RequireSeat ), nameof( NaviComputer.SeatTimeout ), nameof( NaviComputer.NetworkRange ),
			nameof( NaviComputer.OutOfRangeColor ) ) );

		if ( captureObject is not null )
		{
			Add( "Camera and cursor", "videocam", false, () => Sheet( captureObject,
				nameof( NaviInputCapture.FlyTime ), nameof( NaviInputCapture.Fill ),
				nameof( NaviInputCapture.FreeCursor ) ) );
		}

		Add( "Saved state", "save", false, () =>
		{
			var panel = new Widget( this ) { Layout = Layout.Column() };
			panel.Layout.Add( Sheet( computerObject, nameof( NaviComputer.SaveId ), nameof( NaviComputer.AutoSave ) ) );
			panel.Layout.Add( new SavedStatePanel( panel, computer ) );

			return panel;
		} );

		Add( "Other", "more_horiz", false, () => Sheet( computerObject, nameof( NaviComputer.SolidObject ) ) );

		Layout.AddStretchCell();
	}

	void Add( string title, string icon, bool openByDefault, System.Func<Widget> body )
	{
		var section = new Section( this, title, icon, openByDefault, body );
		sections.Add( section );
		Layout.Add( section );
	}

	void SetAll( bool open )
	{
		foreach ( var section in sections )
			section.SetOpen( open );
	}

	/// <summary>
	/// A sheet of editable rows, one per property name. Names the object doesn't have are skipped.
	/// </summary>
	static Widget Sheet( SerializedObject obj, params string[] names )
	{
		var sheet = new ControlSheet { IncludePropertyNames = true };
		Rows( sheet, obj, names );

		return Host( sheet );
	}

	static Widget Host( ControlSheet sheet )
	{
		var host = new Widget { Layout = Layout.Column() };
		host.Layout.Add( sheet );

		return host;
	}

	static void Rows( ControlSheet sheet, SerializedObject obj, params string[] names )
	{
		foreach ( var name in names )
		{
			var property = obj.FirstOrDefault( x => x.Name == name );

			if ( property is not null )
				sheet.AddRow( property );
		}
	}

	static Color Hex( string hex ) => Color.Parse( hex ) ?? Color.Magenta;

	/// <summary>
	/// A collapsible card: a clickable header, and a body built the first time it is opened.
	/// </summary>
	sealed class Section : Widget
	{
		readonly string title;
		readonly string icon;
		readonly string cookie;
		readonly System.Func<Widget> build;
		readonly Header header;
		readonly Widget body;
		bool built;

		public Section( Widget parent, string title, string icon, bool openByDefault, System.Func<Widget> build ) : base( parent )
		{
			this.title = title;
			this.icon = icon;
			this.build = build;
			cookie = $"navi.desktop.section.{title}";

			Layout = Layout.Column();
			Layout.Spacing = 0;

			header = Layout.Add( new Header( this ) );

			body = Layout.Add( new Widget( this ) );
			body.Layout = Layout.Column();
			body.Layout.Margin = new Sandbox.UI.Margin( 10, 8, 4, 8 );

			SetOpen( EditorCookie.Get( cookie, openByDefault ), false );
		}

		public bool IsOpen { get; private set; }

		public void SetOpen( bool open ) => SetOpen( open, true );

		void SetOpen( bool open, bool save )
		{
			IsOpen = open;

			if ( open && !built )
			{
				built = true;
				body.Layout.Add( build() );
			}

			body.Visible = open;
			header.Update();

			if ( save )
				EditorCookie.Set( cookie, open );
		}

		protected override void OnPaint()
		{
			Paint.ClearPen();
			Paint.SetBrush( Hex( "#2a2a2e" ) );
			Paint.DrawRect( LocalRect, 6 );

			Paint.SetPen( Hex( "#3a3a40" ) );
			Paint.ClearBrush();
			Paint.DrawRect( LocalRect.Shrink( 0.5f ), 6 );
		}

		sealed class Header : Widget
		{
			readonly Section owner;

			public Header( Section owner ) : base( owner )
			{
				this.owner = owner;
				FixedHeight = 30;
				Cursor = CursorShape.Finger;
			}

			protected override void OnMouseClick( MouseEvent e ) => owner.SetOpen( !owner.IsOpen );

			protected override void OnPaint()
			{
				var rect = LocalRect;

				Paint.ClearPen();
				Paint.SetBrush( Hex( Paint.HasMouseOver ? "#26384a" : "#1e2c3a" ) );
				Paint.DrawRect( rect, 5 );

				Paint.SetBrush( Hex( "#4a9eea" ) );
				Paint.DrawRect( new Rect( rect.Left, rect.Top, 4, rect.Height ), 2 );

				Paint.SetPen( Hex( "#9ab0c4" ) );
				Paint.DrawIcon( new Rect( rect.Left + 10, rect.Top, 22, rect.Height ), owner.icon, 18, TextFlag.Center );

				Paint.SetDefaultFont( 10, 700 );
				Paint.SetPen( Hex( "#e6eef7" ) );
				Paint.DrawText( new Rect( rect.Left + 38, rect.Top, rect.Width - 70, rect.Height ), owner.title, TextFlag.LeftCenter );

				Paint.SetPen( Hex( "#9ab0c4" ) );
				Paint.DrawIcon( new Rect( rect.Right - 28, rect.Top, 22, rect.Height ),
					owner.IsOpen ? "expand_more" : "chevron_right", 18, TextFlag.Center );
			}
		}
	}

	/// <summary>
	/// What the computer is doing right now. It repaints on every editor frame while the game runs, and shows the
	/// computer's saved setup otherwise.
	/// </summary>
	sealed class StatusCard : Widget
	{
		const float Row = 20;

		readonly NaviComputer computer;
		List<(string Name, string Value, string Color)> lines = [];

		public StatusCard( Widget parent, NaviComputer computer ) : base( parent )
		{
			this.computer = computer;
			Refresh();
		}

		[EditorEvent.Frame]
		public void Frame()
		{
			if ( this.IsValid() && Visible )
				Refresh();
		}

		void Refresh()
		{
			var next = Collect();

			if ( next.SequenceEqual( lines ) )
				return;

			lines = next;
			FixedHeight = 36 + lines.Count * Row + 10;
			Update();
		}

		List<(string Name, string Value, string Color)> Collect()
		{
			var list = new List<(string Name, string Value, string Color)>();

			if ( !computer.IsValid() )
				return list;

			if ( computer.Scene is not { IsEditor: false } )
			{
				list.Add( ("State", "Not running. Press Play to see it live.", "#9a9aa2") );
				list.Add( ("Installed", $"{computer.InstalledApps.Count} apps, {computer.PinnedApps.Count} pinned", "#ddd") );
				list.Add( ("Look", $"{computer.Theme}, {computer.ColorScheme}, {computer.Wallpaper}", "#ddd") );

				return list;
			}

			var state = computer.IsLocked ? "Locked" : computer.HasUser ? "In use" : "Idle";
			var stateColor = computer.IsLocked ? "#e8a33d" : computer.HasUser ? "#5ec97a" : "#9a9aa2";

			list.Add( ("State", state, stateColor) );
			list.Add( ("User", computer.User?.DisplayName ?? (computer.HasUser ? "Local player" : "Nobody"), "#ddd") );
			list.Add( ("Focused", computer.FocusedWindow is { } window ? string.IsNullOrWhiteSpace( window.Title ) ? window.App.Name : $"{window.App.Name}: {window.Title}" : "none", "#ddd") );
			list.Add( ("Running", $"{computer.Running.Count} apps, {computer.Running.Count( x => x.Tray is not null )} in tray, {computer.Running.Sum( x => x.DrawnWindows.Count() )} windows", "#ddd") );

			if ( computer.PermissionRequests.Count > 0 )
				list.Add( ("Waiting", $"{computer.PermissionRequests.Count} permission prompt(s)", "#e8a33d") );

			foreach ( var process in computer.Running )
			{
				var access = process.IsRoot ? "root"
					: process.Permissions.Count == 0 ? "no permissions"
					: $"{process.Permissions.Count} permissions";

				list.Add( ($"  {process.App.Name}", $"{process.DrawnWindows.Count()} window(s){(process.Tray is not null ? ", in tray" : "")}, {access}", "#9a9aa2") );
			}

			return list;
		}

		protected override void OnPaint()
		{
			var rect = LocalRect;

			Paint.ClearPen();
			Paint.SetBrush( Hex( "#202a24" ) );
			Paint.DrawRect( rect, 6 );

			Paint.SetBrush( Hex( "#5ec97a" ) );
			Paint.DrawRect( new Rect( rect.Left, rect.Top, 4, rect.Height ), 2 );

			Paint.SetPen( Hex( "#9fd8ae" ) );
			Paint.DrawIcon( new Rect( rect.Left + 10, rect.Top + 4, 24, 28 ), "desktop_windows", 20, TextFlag.Center );

			Paint.SetDefaultFont( 10, 800 );
			Paint.SetPen( Hex( "#e6f4ea" ) );
			Paint.DrawText( new Rect( rect.Left + 40, rect.Top + 4, rect.Width - 50, 28 ), "Navi computer", TextFlag.LeftCenter );

			var y = rect.Top + 36;

			foreach ( var (name, value, color) in lines )
			{
				Paint.SetDefaultFont( 9, 500 );
				Paint.SetPen( Hex( "#7a8a80" ) );
				Paint.DrawText( new Rect( rect.Left + 14, y, 100, Row ), name, TextFlag.LeftCenter );

				Paint.SetPen( Hex( color ) );
				Paint.DrawText( new Rect( rect.Left + 110, y, rect.Width - 120, Row ), value, TextFlag.LeftCenter );

				y += Row;
			}
		}
	}
}
