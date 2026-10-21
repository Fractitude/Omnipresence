namespace Sandbox;

/// <summary>
/// Implement it on a component to make its GameObject something a player can keep in a <see cref="PlayerInventory"/>. It
/// describes the item to the inventory: its name, its text and the model drawn as its icon. <see cref="PickupItem"/> is
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
	/// The model drawn, spinning, as the item's icon. By default the first model renderer on the item or below it.
	/// </summary>
	Model ItemModel => (this as Component)?.Components.Get<ModelRenderer>( FindMode.EnabledInSelfAndDescendants )?.Model;
}
