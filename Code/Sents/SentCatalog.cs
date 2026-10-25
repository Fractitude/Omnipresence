using System.Threading.Tasks;

namespace Sandbox;

/// <summary>
/// The list of sents the browser shows: what is searched for, the pages loaded so far and whether another page is
/// coming. The window (<see cref="SentBrowser"/>) only draws it. Static, because a panel's mouse events and its
/// component's updates are not always on the same instance.
/// </summary>
public static class SentCatalog
{
	/// <summary>
	/// A package in the list.
	/// </summary>
	public sealed record Entry( string Ident, string Title, string Org, string Thumb );

	/// <summary>
	/// A group in the sidebar: the packages that carry this tag. An empty tag is all of them.
	/// </summary>
	public sealed record Category( string Name, string Icon, string Tag );

	public static readonly string[] Modes = ["popular", "newest", "trending"];

	public static readonly Category[] Categories =
	[
		new( "All", "apps", "" ),
		new( "Vehicles", "directions_car", "vehicle" ),
		new( "Weapons", "gps_fixed", "weapon" ),
		new( "NPCs", "smart_toy", "npc" ),
		new( "Explosives", "local_fire_department", "explosives" ),
	];

	const int PageSize = 40;

	/// <summary>
	/// The packages loaded so far, in order.
	/// </summary>
	public static IReadOnlyList<Entry> Entries => entries;

	/// <summary>
	/// A page is being fetched.
	/// </summary>
	public static bool Loading { get; private set; }

	/// <summary>
	/// Raised on every change to what the list shows, for the window's redraw hash.
	/// </summary>
	public static int Version { get; private set; }

	/// <summary>
	/// How many searches from the first page have finished, so the window can go back to the top of a new list.
	/// </summary>
	public static int Searches { get; private set; }

	/// <summary>
	/// More packages exist than are loaded.
	/// </summary>
	public static bool HasMore => !Loading && entries.Count > 0 && entries.Count < total;

	public static string Sort { get; private set; } = "popular";
	public static string Tag { get; private set; } = "";

	public static string Filter
	{
		get => filter;
		set
		{
			if ( filter == value )
				return;

			filter = value;
			_ = Refresh();
		}
	}

	static List<Entry> entries = [];
	static string filter = "";
	static int total;
	static int generation;

	public static void SetSort( string mode )
	{
		Sort = mode;
		_ = Refresh();
	}

	public static void SetCategory( Category category )
	{
		if ( category.Tag == Tag )
			return;

		Tag = category.Tag;
		_ = Refresh();
	}

	/// <summary>
	/// Searches again from the first page. A search that is still loading is dropped.
	/// </summary>
	public static async Task Refresh()
	{
		var mine = ++generation;
		Loading = true;
		Version++;

		var page = await Fetch( 0 );

		if ( mine != generation )
			return;

		entries = page.Entries;
		total = page.Total ?? entries.Count;
		Loading = false;
		Searches++;
		Version++;
	}

	/// <summary>
	/// Adds the next page below the ones shown. A new search while it loads drops it.
	/// </summary>
	public static async Task LoadMore()
	{
		var mine = generation;
		Loading = true;
		Version++;

		var page = await Fetch( entries.Count );

		if ( mine != generation )
			return;

		var more = page.Entries.Where( x => entries.All( e => e.Ident != x.Ident ) ).ToList();
		entries = [.. entries, .. more];

		// A page with nothing new is the end, whatever the total said.
		total = more.Count == 0 ? entries.Count : page.Total ?? entries.Count;
		Loading = false;
		Version++;
	}

	static async Task<(List<Entry> Entries, int? Total)> Fetch( int skip )
	{
		try
		{
			var result = await Package.FindAsync( Query(), PageSize, skip );
			var found = result?.Packages?.Select( x => new Entry( x.FullIdent, x.Title, x.Org?.Title, x.Thumb ) ).ToList() ?? [];
			return (found, result?.TotalCount);
		}
		catch ( Exception e )
		{
			Log.Warning( $"sents: the search failed: {e.Message}" );
			return ([], null);
		}
	}

	// The cloud's search syntax: "#vehicle" finds a tag ("tag:vehicle" finds nothing).
	static string Query()
	{
		var query = $"sort:{Sort} type:sent";

		if ( !string.IsNullOrEmpty( Tag ) )
			query += $" #{Tag}";

		if ( !string.IsNullOrWhiteSpace( filter ) )
			query += $" {filter.Trim()}";

		return query;
	}
}
