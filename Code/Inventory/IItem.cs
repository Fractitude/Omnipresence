namespace Sandbox;

/// <summary>
/// Implement it on a component to make its GameObject something a player can keep in a <see cref="PlayerInventory"/>. It
/// describes the item to the inventory: its name, its text and what is drawn as its icon. <see cref="PickupItem"/> is
/// a ready-made implementation that adds the Pick up option.
/// </summary>
public interface IItem
{
	/// <summary>
	/// The name the inventory shows.
	/// </summary>
	string ItemName { get; }

	/// <summary>
	/// The text the inventory shows when the player hovers the item.
	/// </summary>
	string ItemDescription => "";

	/// <summary>
	/// The model drawn, spinning, as the item's icon, when there is no <see cref="ItemPrefab"/>. By default the first model
	/// renderer on the item or below it.
	/// </summary>
	Model ItemModel => (this as Component)?.Components.Get<ModelRenderer>( FindMode.EnabledInSelfAndDescendants )?.Model;

	/// <summary>
	/// The prefab drawn, spinning, as the item's icon: the whole object with all its models. By default the nearest prefab
	/// the item belongs to; empty when it is not from a prefab.
	/// </summary>
	string ItemPrefab => ItemData.NearestPrefab( (this as Component)?.GameObject );
}
