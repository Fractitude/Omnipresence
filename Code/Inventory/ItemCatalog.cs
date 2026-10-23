namespace Sandbox;

/// <summary>
/// One kind of batch item: what it is called and what it is worth.
/// </summary>
/// <param name="Id">The saved id. Never rename one.</param>
/// <param name="BasePrice">What a buyer pays for one at quality 50, in crypto. Zero if it isn't sold.</param>
/// <param name="CanDrop">False for items that must never leave the inventory except through their own rules.</param>
/// <param name="Icon">The icon glyph drawn in the inventory for an item with no model.</param>
/// <param name="Color">The icon's colour, like "#8be28b", or empty for the default.</param>
public sealed record ItemDef(
	string Id,
	string Name,
	string Description,
	long BasePrice,
	bool CanDrop = true,
	string Icon = "inventory_2",
	string Color = "" );

/// <summary>
/// The crime items: batches with a kind, a count and a quality (see <see cref="ItemData"/>). The first build has the
/// Static chain and the parcel. Names and texts live here, so a saved batch needs only its kind.
/// </summary>
public static class ItemCatalog
{
	public const string StaticCulture = "static_culture";
	public const string StaticRaw = "static_raw";
	public const string StaticTab = "static_tab";
	public const string Parcel = "parcel";

	/// <summary>The most one batch holds.</summary>
	public const int MaxCount = 9999;

	static ItemDef[] items =
	[
		new( StaticCulture, "Static culture", "A living culture, ready to plant in a grow tray.", 0,
			Icon: "biotech", Color: "#8be28b" ),
		new( StaticRaw, "Static (raw)", "Freshly harvested. Press it into tabs at a press bench.", 0,
			Icon: "grain", Color: "#5fcfb4" ),
		new( StaticTab, "Static tabs", "Pressed tabs of Static.", 10, Icon: "medication", Color: "#7fd1ff" ),
		new( Parcel, "Parcel", "Packed for an order. Send it from your mailbox. It can't be dropped.", 0,
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
	/// A short line for the batch: "x40, quality 72".
	/// </summary>
	public static string Describe( ItemData item ) =>
		item.Quality > 0 ? $"x{item.Count}, quality {item.Quality}" : $"x{item.Count}";
}
