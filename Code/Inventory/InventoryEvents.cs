namespace Sandbox;

public static partial class Global
{
	/// <summary>
	/// Inventory events, posted to the whole scene. Implement it on a component to react to a
	/// <see cref="PlayerInventory"/> without polling it.
	/// </summary>
	public interface IInventoryEvents : ISceneEvent<IInventoryEvents>
	{
		/// <summary>
		/// The slots changed. Posted on the host when it changes them, and on every other machine when the change arrives.
		/// </summary>
		void OnInventoryChanged( PlayerInventory inventory ) { }

		/// <summary>
		/// An item was dropped. Host only. The item is out of the inventory by then, and is a <see cref="WorldItem"/> on the ground.
		/// </summary>
		void OnItemDropped( PlayerInventory inventory, ItemData item ) { }
	}
}
