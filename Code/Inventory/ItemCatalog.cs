namespace Sandbox;

/// <summary>
/// One kind of batch item: what it is called and what it is worth.
/// </summary>
/// <param name="Id">The saved id. Never rename one.</param>
/// <param name="BasePrice">What a buyer pays for one at quality 50, in crypto. Zero if it isn't sold.</param>
/// <param name="CanDrop">False for items that must never leave the inventory except through their own rules.</param>
/// <param name="Icon">The icon glyph drawn in the inventory for an item with no model.</param>
/// <param name="Color">The icon's colour, like "#8be28b", or empty for the default.</param>
/// <param name="Category">The kind of product, like "Static", for the filters of the Hush orders. Empty for an item
/// that is not sold (a parcel).</param>
/// <param name="MaxStack">The most one stack of this kind holds. 1 (the default) means it never stacks, and cannot be
/// combined or split. The drugs' items set <see cref="ProductCatalog.StackSize"/>.</param>
public sealed record ItemDef(
	string Id,
	string Name,
	string Description,
	long BasePrice,
	bool CanDrop = true,
	string Icon = "inventory_2",
	string Color = "",
	string Category = "",
	int MaxStack = 1 );

/// <summary>
/// The crime items: batches with a kind, a count and a quality (see <see cref="ItemData"/>). The three items of each
/// drug come from the <see cref="ProductCatalog"/>; the parcel is here. Names and texts live in the definitions, so a
/// saved batch needs only its kind.
/// </summary>
public static class ItemCatalog
{
	public const string StaticCulture = "static_culture";
	public const string StaticRaw = "static_raw";
	public const string StaticTab = "static_tab";
	public const string HazeSeed = "haze_seed";
	public const string HazeRaw = "haze_raw";
	public const string HazeVial = "haze_vial";
	public const string Parcel = "parcel";

	/// <summary>
	/// A sanity bound for counts that are not stacks (an order's count, a delivery's, a parcel's contents lines) and
	/// for the total of one saved batch. A stack is limited by its kind: <see cref="MaxStackOf"/>.
	/// </summary>
	public const int MaxCount = 9999;

	// Cached by HotReloadCache, not held in a static field: a hot reload keeps a static holding the assembly's own
	// record type, so an edit to the table would never show until the editor restarted.
	static ItemDef[] items => HotReloadCache.Get( "ItemCatalog.items", BuildItems );

	static ItemDef[] BuildItems() =>
	[
		.. ProductCatalog.Items,
		new( Parcel, "Parcel", "Packed for an order. Send it from your mailbox.", 0,
			CanDrop: false, Icon: "redeem", Color: "#c9a27a" ),
	];

	public static IReadOnlyList<ItemDef> Items => items;

	public static ItemDef Find( string id ) => items.FirstOrDefault( x => x.Id == id );

	/// <summary>
	/// The most one stack of a kind holds: its <see cref="ItemDef.MaxStack"/>, and 1 for an unknown or empty kind (an
	/// ordinary item).
	/// </summary>
	public static int MaxStackOf( string kind ) =>
		string.IsNullOrEmpty( kind ) ? 1 : System.Math.Max( Find( kind )?.MaxStack ?? 1, 1 );

	/// <summary>
	/// Whether batches of a kind can be combined and split (<see cref="StackRules"/>): exactly when a stack holds more
	/// than one. A new item stacks by setting <see cref="ItemDef.MaxStack"/>.
	/// </summary>
	public static bool IsCombinable( string kind ) => MaxStackOf( kind ) > 1;

	/// <summary>
	/// The icon glyph for an item with no model: its kind's icon, or the plain box for an ordinary item.
	/// </summary>
	public static string IconFor( ItemData item ) => Find( item?.Kind )?.Icon ?? "inventory_2";

	/// <summary>
	/// The icon colour for an item, or empty for the default.
	/// </summary>
	public static string ColorFor( ItemData item ) => Find( item?.Kind )?.Color ?? "";

	/// <summary>
	/// A batch of a kind, or null for a kind that doesn't exist or a count below one. Quality is clamped, and so is the
	/// count, to the kind's stack size: for more, use <see cref="MakeStacks"/>.
	/// </summary>
	public static ItemData Make( string kind, int count, int quality, string orderId = "" )
	{
		if ( Find( kind ) is not { } def || count < 1 )
			return null;

		return new ItemData
		{
			Name = def.Name,
			Description = def.Description,
			Kind = def.Id,
			Count = System.Math.Min( count, def.MaxStack < 1 ? 1 : def.MaxStack ),
			Quality = System.Math.Clamp( quality, 0, 100 ),
			OrderId = orderId ?? "",
		};
	}

	/// <summary>
	/// As many full stacks of a kind as <paramref name="count"/> needs, then the remainder, all with the same quality.
	/// Empty for an unknown kind or a count below one. The total is cut to <see cref="MaxCount"/>.
	/// </summary>
	public static List<ItemData> MakeStacks( string kind, int count, int quality, string orderId = "" )
	{
		var stacks = new List<ItemData>();

		if ( Find( kind ) is null )
			return stacks;

		var left = System.Math.Min( count, MaxCount );
		var cap = MaxStackOf( kind );

		while ( left > 0 )
		{
			var part = System.Math.Min( left, cap );
			stacks.Add( Make( kind, part, quality, orderId ) );
			left -= part;
		}

		return stacks;
	}

	/// <summary>
	/// How many slots a count of a kind takes, one per full stack and one for the remainder.
	/// </summary>
	public static int SlotsFor( string kind, int count ) =>
		count < 1 ? 0 : (int)(((long)count + MaxStackOf( kind ) - 1) / MaxStackOf( kind ));

	/// <summary>
	/// The details of a batch, one line each. An ordinary batch has one: "x1", or "x12 / 256, quality 40" when the kind
	/// stacks. A parcel lists what is inside by name, like "Static tabs x10, quality 66", and has no line of its own
	/// (its count and quality mean nothing).
	/// </summary>
	public static IReadOnlyList<string> DescribeLines( ItemData item )
	{
		if ( item.Kind != Parcel )
		{
			var count = MaxStackOf( item.Kind ) > 1 ? $"x{item.Count} / {MaxStackOf( item.Kind )}" : $"x{item.Count}";
			return [item.Quality > 0 ? $"{count}, quality {item.Quality}" : count];
		}

		return (item.Contents ?? [])
			.Select( x => $"{Find( x.Kind )?.Name ?? x.Kind} x{x.Count}"
			              + (x.Quality > 0 ? $", quality {x.Quality}" : "") )
			.ToList();
	}

	/// <summary>
	/// <see cref="DescribeLines"/> as one text.
	/// </summary>
	public static string Describe( ItemData item ) => string.Join( "\n", DescribeLines( item ) );
}
