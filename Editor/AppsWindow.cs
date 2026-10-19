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

		var header = list.Canvas.Layout.AddRow();
		header.Margin = new Sandbox.UI.Margin( 0, 8, 0, 0 );

		var toggle = header.Add( new Button( $"{source.ToUpperInvariant()}  -  {apps.Count}", open ? "expand_more" : "chevron_right", this )
		{
			Clicked = () => ToggleSection( source )
		}, 1 );
		toggle.SetStyles( "background-color: transparent; border: none; border-bottom: 1px solid #444; border-radius: 0; "
		                  + "color: #9ab0c4; font-weight: 700; text-align: left; padding: 6px 4px;" );

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

		var card = list.Canvas.Layout.Add( new Widget( list.Canvas ) );
		card.SetStyles( "background-color: #2a2a2e; border: 1px solid #3a3a40; border-radius: 6px;" );
		card.Layout = Layout.Column();
		card.Layout.Margin = new Sandbox.UI.Margin( 8, 4, 8, 8 );
		card.Layout.Spacing = 2;

		var toggle = card.Layout.Add( new Button( entry.Title, open ? "expand_more" : "chevron_right", card )
		{
			Clicked = () => Toggle( entry.Id )
		} );
		toggle.SetStyles( "background-color: transparent; border: none; color: #eee; font-weight: 700; text-align: left; padding: 4px 2px;" );

		if ( entry.Description.Length > 0 )
		{
			var description = card.Layout.Add( new Label( entry.Description, card ) { WordWrap = true } );
			description.SetStyles( "color: #9a9aa2; background-color: transparent; margin: 0 0 4px 8px;" );
		}

		if ( !open )
			return;

		var divider = card.Layout.Add( new Widget( card ) { FixedHeight = 1 } );
		divider.SetStyles( "background-color: #3a3a40;" );
		card.Layout.AddSpacingCell( 4 );

		foreach ( var (name, value) in entry.Details )
		{
			var row = card.Layout.AddRow();
			row.Margin = new Sandbox.UI.Margin( 8, 1, 0, 1 );
			row.Spacing = 8;

			var caption = row.Add( new Label( name, card ) { MinimumWidth = 110, MaximumWidth = 110 } );
			caption.SetStyles( "color: #7a7a84; background-color: transparent;" );

			var field = row.Add( new LineEdit( card ) { Text = value, ReadOnly = true }, 1 );
			field.SetStyles( "background-color: #202024; border: 1px solid #333338; border-radius: 4px; color: #ddd; padding: 2px 6px;" );
			field.CursorPosition = 0;
		}
	}

	void Toggle( string id )
	{
		if ( !expanded.Remove( id ) )
			expanded.Add( id );

		Rebuild();
	}

	void SetAll( bool on )
	{
		expanded.Clear();

		if ( on )
			expanded.UnionWith( entries.Select( x => x.Id ) );

		Rebuild();
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
}
