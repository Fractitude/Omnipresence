namespace Omnipresence.Editor;

/// <summary>
/// A window that lists every Navi app the editor knows: the game's own, the Navi library's built-in ones and any downloaded
/// from the Store. Each row expands to show where the app is installed from and what its manifest asks for. Values are
/// read-only text boxes, so they can be selected and copied.
/// </summary>
public sealed class AppsWindow : BaseWindow
{
	/// <summary>
	/// An app as the window shows it.
	/// </summary>
	sealed record Entry( string Id, string Title, string Source, string Description, List<(string Name, string Value)> Details );

	static AppsWindow instance;

	readonly HashSet<string> expanded = [];
	readonly HashSet<string> collapsedSections = [];
	readonly Dictionary<string, RowView> rows = [];
	ScrollArea list;
	LineEdit search;
	Label summary;
	List<Entry> entries = [];

	public static void Open()
	{
		if ( instance.IsValid() )
		{
			instance.Refresh();
			instance.Focus();
			return;
		}

		instance = new AppsWindow();
		instance.Show();
	}

	AppsWindow()
	{
		Size = new Vector2( 720, 720 );
		MinimumSize = new Vector2( 480, 360 );
		WindowTitle = "Omnipresence: Apps";
		SetWindowIcon( "apps" );

		Layout = Layout.Column();
		Layout.Margin = 8;
		Layout.Spacing = 8;

		var top = Layout.AddRow();
		top.Spacing = 8;
		search = top.Add( new LineEdit( this ) { PlaceholderText = "Filter by name, id, source or path" }, 1 );
		search.TextEdited += _ => Rebuild();
		top.Add( new Button( "Expand all", "unfold_more", this ) { Clicked = () => SetAll( true ) } );
		top.Add( new Button( "Collapse all", "unfold_less", this ) { Clicked = () => SetAll( false ) } );
		top.Add( new Button( "Refresh", "refresh", this ) { Clicked = Refresh } );

		list = new ScrollArea( this );
		Layout.Add( list, 1 );

		summary = Layout.Add( new Label( this ) );
		summary.SetStyles( "color: #aaa;" );

		Refresh();
	}

	/// <summary>
	/// Read the apps again from the loaded resources, then rebuild the rows.
	/// </summary>
	void Refresh()
	{
		entries = Collect();
		Rebuild();
	}

	void Rebuild()
	{
		var filter = search.Value?.Trim() ?? "";

		var shown = entries
			.Where( x => filter.Length == 0 || Matches( x, filter ) )
			.ToList();

		rows.Clear();
		list.Canvas = new Widget( list );
		list.Canvas.Layout = Layout.Column();
		list.Canvas.Layout.Margin = new Sandbox.UI.Margin( 8, 8, 16, 8 );
		list.Canvas.Layout.Spacing = 8;

		if ( shown.Count == 0 )
			list.Canvas.Layout.Add( new Label( filter.Length > 0 ? "No apps matching query." : "No apps." ) );

		foreach ( var section in shown.GroupBy( x => x.Source ) )
			AddSection( section.Key, section.ToList() );

		list.Canvas.Layout.AddStretchCell();
		summary.Text = $"{shown.Count} of {entries.Count} apps.";
	}

	void AddSection( string source, List<Entry> apps )
	{
		var open = !collapsedSections.Contains( source );

		list.Canvas.Layout.AddSpacingCell( 6 );
		list.Canvas.Layout.Add( new Bar( list.Canvas, source.ToUpperInvariant(), apps.Count.ToString(),
			open ? "expand_more" : "chevron_right", true, () => ToggleSection( source ) ) );

		if ( !open )
			return;

		foreach ( var entry in apps )
			AddRow( entry );
	}

	void ToggleSection( string source )
	{
		if ( !collapsedSections.Remove( source ) )
			collapsedSections.Add( source );

		Rebuild();
	}

	void AddRow( Entry entry )
	{
		var open = expanded.Contains( entry.Id );

		var card = list.Canvas.Layout.Add( new Card( list.Canvas ) );
		card.Layout = Layout.Column();
		card.Layout.Margin = new Sandbox.UI.Margin( 6, 4, 8, 8 );
		card.Layout.Spacing = 2;

		var bar = card.Layout.Add( new Bar( card, entry.Title, null, open ? "expand_more" : "chevron_right", false,
			() => Toggle( entry.Id ) ) );

		if ( entry.Description.Length > 0 )
		{
			var description = card.Layout.Add( new Label( entry.Description, card ) { WordWrap = true } );
			description.SetStyles( "color: #9a9aa2; margin: 0 0 4px 30px;" );
		}

		var row = new RowView( entry, card, bar );
		rows[entry.Id] = row;
		card.Layout.Add( row.Details );

		if ( open )
			row.SetOpen( true );
	}

	/// <summary>
	/// Open or close one row in place. The rows aren't rebuilt, so the scroll position and the other rows stay put.
	/// </summary>
	void Toggle( string id )
	{
		var open = expanded.Add( id );

		if ( !open )
			expanded.Remove( id );

		if ( rows.TryGetValue( id, out var row ) )
			row.SetOpen( open );
	}

	void SetAll( bool on )
	{
		expanded.Clear();

		if ( on )
			expanded.UnionWith( entries.Select( x => x.Id ) );

		foreach ( var row in rows.Values )
			row.SetOpen( on );
	}

	/// <summary>
	/// The widgets of one app's row. The details are built the first time the row opens, then only shown and hidden.
	/// </summary>
	sealed class RowView
	{
		readonly Entry entry;
		readonly Bar bar;
		bool built;

		public Widget Details { get; }

		public RowView( Entry entry, Widget card, Bar bar )
		{
			this.entry = entry;
			this.bar = bar;

			Details = new Widget( card ) { Visible = false };
			Details.Layout = Layout.Column();
			Details.Layout.Spacing = 2;
		}

		public void SetOpen( bool open )
		{
			if ( open && !built )
				Build();

			Details.Visible = open;
			bar.Icon = open ? "expand_more" : "chevron_right";
		}

		void Build()
		{
			built = true;

			var divider = Details.Layout.Add( new Widget( Details ) { FixedHeight = 1 } );
			divider.SetStyles( "background-color: #3a3a40; border: none;" );
			Details.Layout.AddSpacingCell( 4 );

			foreach ( var (name, value) in entry.Details )
			{
				var row = Details.Layout.AddRow();
				row.Margin = new Sandbox.UI.Margin( 30, 1, 0, 1 );
				row.Spacing = 8;

				var caption = row.Add( new Label( name, Details ) { MinimumWidth = 110, MaximumWidth = 110 } );
				caption.SetStyles( "color: #7a7a84;" );

				var field = row.Add( new LineEdit( Details ) { Text = value, ReadOnly = true }, 1 );
				field.SetStyles( "background-color: #202024; border: 1px solid #333338; border-radius: 4px; color: #ddd; padding: 2px 6px;" );
				field.CursorPosition = 0;
			}
		}
	}

	static bool Matches( Entry entry, string filter ) =>
		entry.Title.Contains( filter, System.StringComparison.OrdinalIgnoreCase )
		|| entry.Id.Contains( filter, System.StringComparison.OrdinalIgnoreCase )
		|| entry.Source.Contains( filter, System.StringComparison.OrdinalIgnoreCase )
		|| entry.Details.Any( x => x.Value.Contains( filter, System.StringComparison.OrdinalIgnoreCase ) );

	/// <summary>
	/// Every loaded app manifest, including ones whose panel class doesn't load (those would be left out of the game's own
	/// app list, which is what makes them worth seeing here).
	/// </summary>
	static List<Entry> Collect() => ResourceLibrary.GetAll<NaviAppResource>()
		.Concat( NaviAppRegistry.All.Select( x => x.Resource ) )
		.Distinct()
		.Select( Describe )
		.OrderBy( x => SourceOrder( x.Source ) )
		.ThenBy( x => x.Title, System.StringComparer.OrdinalIgnoreCase )
		.ToList();

	static int SourceOrder( string source ) => source switch
	{
		"Game" => 0,
		"Library" => 1,
		_ => 2
	};

	static Entry Describe( NaviAppResource app )
	{
		var path = app.ResourcePath ?? "";
		var id = NaviAppRegistry.IdOf( app );
		var package = NaviStore.PackageOf( app );
		var panel = app.FindPanelType();

		var source = package is not null ? "Store"
			: path.Replace( '\\', '/' ).StartsWith( NaviAppRegistry.BuiltInFolder, System.StringComparison.OrdinalIgnoreCase ) ? "Library"
			: "Game";

		var details = new List<(string, string)>
		{
			("Id", id ?? "(none: the path has whitespace or '#')"),
			("Install path", path),
			("File on disk", FullPath( path )),
			("Source", package is not null ? $"Store package {package}"
				: NaviAppRegistry.IsPrepackaged( app ) ? $"{source} (prepackaged, a trusted folder)"
				: $"{source} (not in a trusted folder)"),
			("Panel type", panel is null ? $"{app.PanelType} (NOT FOUND in loaded code)" : panel.FullName),
			("Kind", app.IsSystem ? "System (asks for root)" : "App"),
			("Permissions", app.Permissions is { Count: > 0 } ? string.Join( ", ", app.Permissions ) : "none"),
			("Computer tags", app.ComputerTags is { Count: > 0 } ? string.Join( ", ", app.ComputerTags ) : "any computer"),
			("File types", app.FileTypes is { Count: > 0 } ? string.Join( ", ", app.FileTypes ) : "none"),
			("Windows", Windows( app )),
			("Shared", app.Shared ? "yes, runs on the host" : "no"),
		};

		return new Entry( id ?? path, string.IsNullOrWhiteSpace( app.Title ) ? path : app.Title, source, app.Description ?? "", details );
	}

	static string Windows( NaviAppResource app )
	{
		var windows = app.Windows?.Where( x => x is not null ).Select( x => x.Id ).ToList() ?? [];
		var main = app.Headless ? "headless" : "main window";

		return windows.Count == 0 ? main : $"{main}, {string.Join( ", ", windows )}";
	}

	/// <summary>
	/// Where the asset is on this machine, or the path again when the editor can't say (e.g. a downloaded package).
	/// </summary>
	static string FullPath( string path )
	{
		try
		{
			return string.IsNullOrEmpty( path ) ? "" : System.IO.Path.GetFullPath( global::Editor.FileSystem.Content.GetFullPath( path ) );
		}
		catch ( System.Exception )
		{
			return "(not on disk)";
		}
	}

	static Color Hex( string hex ) => Color.Parse( hex ) ?? Color.Magenta;

	/// <summary>
	/// The card an app sits in. It paints its own background, so the style doesn't spread to the widgets inside it.
	/// </summary>
	sealed class Card : Widget
	{
		public Card( Widget parent ) : base( parent )
		{
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
	}

	/// <summary>
	/// A clickable bar with an arrow: a section header (centered text on an accent band) or an app's title (left aligned).
	/// </summary>
	sealed class Bar : Widget
	{
		readonly string text;
		readonly string count;
		readonly bool section;
		readonly System.Action clicked;
		string icon;

		public string Icon
		{
			get => icon;
			set
			{
				icon = value;
				Update();
			}
		}

		public Bar( Widget parent, string text, string count, string icon, bool section, System.Action clicked ) : base( parent )
		{
			this.text = text;
			this.count = count;
			this.icon = icon;
			this.section = section;
			this.clicked = clicked;

			FixedHeight = section ? 34 : 28;
			Cursor = CursorShape.Finger;
		}

		protected override void OnMouseClick( MouseEvent e ) => clicked?.Invoke();

		protected override void OnPaint()
		{
			var rect = LocalRect;
			var hover = Paint.HasMouseOver;

			if ( section )
			{
				Paint.ClearPen();
				Paint.SetBrush( Hex( hover ? "#26384a" : "#1e2c3a" ) );
				Paint.DrawRect( rect, 4 );

				Paint.SetBrush( Hex( "#4a9eea" ) );
				Paint.DrawRect( new Rect( rect.Left, rect.Top, 4, rect.Height ), 2 );

				Paint.SetPen( Hex( "#9ab0c4" ) );
				Paint.DrawIcon( new Rect( rect.Left + 10, rect.Top, 24, rect.Height ), icon, 20, TextFlag.Center );

				Paint.SetDefaultFont( 10, 800 );
				Paint.SetPen( Hex( "#e6eef7" ) );
				Paint.DrawText( rect, count is null ? text : $"{text}   {count}", TextFlag.Center );
				return;
			}

			if ( hover )
			{
				Paint.ClearPen();
				Paint.SetBrush( Hex( "#35353b" ) );
				Paint.DrawRect( rect, 4 );
			}

			Paint.SetPen( Hex( "#9a9aa2" ) );
			Paint.DrawIcon( new Rect( rect.Left + 2, rect.Top, 24, rect.Height ), icon, 18, TextFlag.Center );

			Paint.SetDefaultFont( 10, 700 );
			Paint.SetPen( Hex( "#f0f0f2" ) );
			Paint.DrawText( new Rect( rect.Left + 30, rect.Top, rect.Width - 30, rect.Height ), text, TextFlag.LeftCenter );
		}
	}
}
