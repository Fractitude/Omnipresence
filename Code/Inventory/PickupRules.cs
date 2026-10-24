namespace Sandbox;

/// <summary>
/// One slot a plan writes: the slot, and the item it holds afterwards (a new object, never one that is in the bag).
/// </summary>
public readonly record struct SlotWrite( int Slot, ItemData Item );

/// <summary>
/// How a stack that arrives on its own (a pickup from the ground) fits into a bag, worked out on plain slot arrays:
/// no inventory, no network, nothing mutated. <see cref="PlayerInventory.AddPartial"/> applies the writes in one step, so
/// the count that goes in is exactly the count that comes out of the item.
/// <list type="bullet">
/// <item>A batch tops up the stacks of its kind that have room and that <see cref="StackRules.CanMerge(ItemData, ItemData)"/>
/// allows (same quality when the kind's quality matters, so no quality is ever averaged), then fills empty slots with new
/// stacks of at most the kind's maximum, keeping its own quality. It stops when nothing fits; part of it may go in.</item>
/// <item>An ordinary item takes one empty slot, all or nothing.</item>
/// </list>
/// </summary>
public static class PickupRules
{
	/// <summary>
	/// Plan putting <paramref name="item"/> into <paramref name="slots"/>. Returns how many of its count fit (0 when
	/// nothing does) and fills <paramref name="writes"/> with the slots to write. <paramref name="slots"/> and
	/// <paramref name="item"/> are not changed.
	/// </summary>
	public static int Plan( IReadOnlyList<ItemData> slots, ItemData item, List<SlotWrite> writes )
	{
		writes.Clear();

		if ( item is null || item.Count < 1 )
			return 0;

		if ( !item.IsBatch )
		{
			var empty = IndexOfEmpty( slots, 0 );

			if ( empty < 0 )
				return 0;

			writes.Add( new SlotWrite( empty, item.Clone() ) );
			return item.Count;
		}

		var left = item.Count;
		var cap = item.MaxStack;

		for ( var i = 0; i < slots.Count && left > 0; i++ )
		{
			if ( slots[i] is not { } there || !StackRules.CanMerge( there, item ) || there.Count >= cap )
				continue;

			var add = System.Math.Min( left, cap - there.Count );
			var topped = there.Clone();
			topped.Count = there.Count + add;
			writes.Add( new SlotWrite( i, topped ) );
			left -= add;
		}

		for ( var i = IndexOfEmpty( slots, 0 ); i >= 0 && left > 0; i = IndexOfEmpty( slots, i + 1 ) )
		{
			var add = System.Math.Min( left, cap );
			var fresh = item.Clone();
			fresh.Count = add;
			writes.Add( new SlotWrite( i, fresh ) );
			left -= add;
		}

		return item.Count - left;
	}

	static int IndexOfEmpty( IReadOnlyList<ItemData> slots, int from )
	{
		for ( var i = from; i < slots.Count; i++ )
		{
			if ( slots[i] is null )
				return i;
		}

		return -1;
	}
}
