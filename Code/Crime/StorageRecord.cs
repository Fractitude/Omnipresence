namespace Sandbox;

/// <summary>
/// One item of a storage box: the slot it sits in and the item itself. Absent from the list means the slot is empty.
/// </summary>
public sealed class StorageSlotRecord
{
	public int Slot { get; set; }

	public ItemData Item { get; set; }
}

/// <summary>
/// What one storage box of a player holds, by the id of the box object in the world (<see cref="StorageBox.BoxId"/>).
/// It is part of the player's <see cref="CrimeRecord"/> (<see cref="CrimeRecord.Storage"/>), so it is saved with the
/// player, works while they are away and is reset by a season wipe of the crime state.
/// </summary>
public sealed class StorageBoxRecord
{
	public string Id { get; set; } = "";

	/// <summary>The items, ordered by slot. Only filled slots are listed.</summary>
	public List<StorageSlotRecord> Slots { get; set; } = new();
}
