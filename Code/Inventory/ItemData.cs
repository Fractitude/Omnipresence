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
	/// The resource path of the prefab drawn as the icon, or empty. It wins over <see cref="ModelPath"/>.
	/// </summary>
	public string PrefabPath { get; set; } = "";

	/// <summary>
	/// The resource path of the icon's model, or empty for none.
	/// </summary>
	public string ModelPath { get; set; } = "";

	/// <summary>
	/// The prefab the object was made from: its own, else the nearest parent's. Empty if it is not from a prefab.
	/// </summary>
	public static string NearestPrefab( GameObject go )
	{
		for ( ; go.IsValid(); go = go.Parent )
		{
			if ( !string.IsNullOrEmpty( go.PrefabInstanceSource ) )
				return go.PrefabInstanceSource;
		}

		return "";
	}

	public static ItemData From( IItem item ) => new()
	{
		Name = item.ItemName ?? "",
		Description = item.ItemDescription ?? "",
		PrefabPath = item.ItemPrefab ?? "",
		ModelPath = item.ItemModel?.ResourcePath ?? "",
	};
}
