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
public sealed record ItemDef(
	string Id,
	string Name,
	string Description,
	long BasePrice,
	bool CanDrop = true,
	string Icon = "inventory_2",
	string Color = "",
	string Category = "" );

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

	/// <summary>The most one batch holds.</summary>
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
	/// The icon glyph for an item with no model: its kind's icon, or the plain box for an ordinary item.
	/// </summary>
	public static string IconFor( ItemData item ) => Find( item?.Kind )?.Icon ?? "inventory_2";

	/// <summary>
	/// The icon colour for an item, or empty for the default.
	/// </summary>
	public static string ColorFor( ItemData item ) => Find( item?.Kind )?.Color ?? "";

	/// <summary>
	/// A batch of a kind, or null for a kind that doesn't exist or a count below one. Count and quality are clamped.
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
			Count = System.Math.Min( count, MaxCount ),
			Quality = System.Math.Clamp( quality, 0, 100 ),
			OrderId = orderId ?? "",
		};
	}

	/// <summary>
	/// The details of a batch, one line each. An ordinary batch has one: "x40, quality 72". A parcel lists what is
	/// inside by name, like "Static tabs x10, quality 66", and has no line of its own (its count and quality mean
	/// nothing).
	/// </summary>
	public static IReadOnlyList<string> DescribeLines( ItemData item )
	{
		if ( item.Kind != Parcel )
			return [item.Quality > 0 ? $"x{item.Count}, quality {item.Quality}" : $"x{item.Count}"];

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
