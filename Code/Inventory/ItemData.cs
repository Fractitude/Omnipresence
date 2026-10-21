namespace Sandbox;

/// <summary>
/// What a <see cref="PlayerInventory"/> keeps of an item: the facts the inventory shows. The item's GameObject is gone
/// while it is in the inventory. It is saved as JSON and synced from the host, so keep it to plain values.
/// </summary>
public sealed class ItemData
{
	public string Name { get; set; } = "";

	public string Description { get; set; } = "";

	/// <summary>
	/// The resource path of the icon's model, or empty for none.
	/// </summary>
	public string ModelPath { get; set; } = "";

	public static ItemData From( IItem item ) => new()
	{
		Name = item.ItemName ?? "",
		Description = item.ItemDescription ?? "",
		ModelPath = item.ItemModel?.ResourcePath ?? "",
	};
}
