using Sandbox;

namespace CuratedVehicles;

/// <summary>Stow the driver's weapon and its crosshair while seated, then restore the same item on exit.</summary>
public sealed class VehicleDriverEquipment : Component
{
	[Property, Hide, Sync( SyncFlags.FromHost )] public BaseInventoryComponent StowedInventory { get; set; }
	[Property, Hide, Sync( SyncFlags.FromHost )] public BaseInventoryItem StowedItem { get; set; }

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost ) return;
		var driver = GetComponent<BaseChair>()?.GetOccupant();
		var inventory = driver.IsValid() ? driver.GetComponent<BaseInventoryComponent>() : null;
		if ( inventory != StowedInventory )
		{
			Restore();
			StowedInventory = inventory;
			StowedItem = inventory?.ActiveItem;
		}
		if ( inventory.IsValid() && inventory.ActiveItem.IsValid() ) inventory.ForceHolster();
	}

	private void Restore()
	{
		if ( !Networking.IsHost ) return;
		if ( StowedInventory.IsValid() && !StowedInventory.ActiveItem.IsValid()
			&& StowedItem.IsValid() && StowedItem.Inventory == StowedInventory )
			StowedInventory.Switch( StowedItem );
		StowedItem = null;
		StowedInventory = null;
	}

	protected override void OnDisabled() => Restore();
	protected override void OnDestroy() => Restore();
}
