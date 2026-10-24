namespace Sandbox;

/// <summary>
/// One big object the Depot sells: a thing the player places in the world by hand (with the Physgun), as opposed to an
/// item that goes in the bag.
/// </summary>
/// <param name="Id">The id the shop, the delivery and the world file use. Never changes once shipped.</param>
/// <param name="Name">The name the player reads.</param>
/// <param name="Description">One or two sentences of what it is for.</param>
/// <param name="Icon">A Material icon name for the shop row.</param>
/// <param name="Price">Dollars (cash), the real world's money.</param>
/// <param name="Prefab">The prefab path the host makes it from.</param>
/// <param name="DeliverSeconds">Game seconds from the order until it stands in the world.</param>
public sealed record Placeable(
	string Id, string Name, string Description, string Icon, long Price, string Prefab, long DeliverSeconds );

/// <summary>
/// What the Depot sells. Data only: a new placeable object is one more line here, with a prefab that has the functional
/// component on it (<see cref="StorageBox"/>, <see cref="GrowTray"/>, ...) and an <see cref="Interactable"/>. The
/// prices are a first guess; change them here.
/// </summary>
public static class PlaceableCatalog
{
	/// <summary>The most deliveries one player has on their way at once.</summary>
	public const int MaxInTransit = 6;

	/// <summary>
	/// Every object on sale, in the order the shop lists them. A property, not a static field: a hot reload keeps a
	/// static array of the assembly's own types, so an edited price would not show.
	/// </summary>
	public static IReadOnlyList<Placeable> All =>
	[
		new( "storage_box", "Storage Box", "A lockable-by-use crate with 24 slots. Keep what does not fit in your bag.",
			"inventory_2", 150, "crime/storage_box.prefab", 120 ),
		new( "grow_tray", "Grow Tray", "Plant seeds, wait, harvest. Each player has their own crop in it.",
			"grass", 400, "crime/grow_tray.prefab", 240 ),
		new( "press_bench", "Press Bench", "Press raw harvest into tabs and vials.",
			"precision_manufacturing", 600, "crime/press_bench.prefab", 300 ),
		new( "packing_table", "Packing Table", "Pack an order into a parcel ready for the mailbox.",
			"deployed_code", 350, "crime/packing_table.prefab", 180 ),
	];

	/// <summary>
	/// The object with this id, or null.
	/// </summary>
	public static Placeable Find( string id ) =>
		string.IsNullOrEmpty( id ) ? null : All.FirstOrDefault( x => x.Id == id );
}
