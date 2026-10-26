namespace Sandbox;

/// <summary>
/// The tidy order of a grid of slots: the items grouped by kind, the stacks of a kind put together where the game may
/// merge them, and everything packed from the first slot with no gaps. Works on plain <see cref="ItemData"/>: no bag,
/// no box, nothing mutated. <see cref="PlayerInventory.Sort"/> and <see cref="StorageRules.Sort"/> apply the result in
/// one step, so a tidy never changes a count or a quality, and never loses or doubles an item.
/// <list type="bullet">
/// <item>The groups are the catalogue's kinds in the catalogue's order (<see cref="ItemCatalog.Items"/>), so a drug's
/// seed, raw and product sit together; a kind the catalogue no longer has goes after them, and ordinary items come
/// last, by name.</item>
/// <item>Inside a group the better quality comes first, and a fuller stack before a partial one.</item>
/// <item>Two stacks are put together exactly when <see cref="StackRules.CanMerge(ItemData, ItemData)"/> would allow it
/// and they hold the same quality: the rule a pickup follows, so a tidy never averages a quality. They become whole
/// stacks of the kind's size and a remainder.</item>
/// </list>
/// </summary>
public static class InventorySort
{
	/// <summary>
	/// The items of <paramref name="slots"/> in their tidy order, the empties left out: the first one belongs in the
	/// first slot. The items themselves are handed back, so a caller can tell a tidy that changes nothing by comparing
	/// the order with what the slots hold; only a stack that is put together with another is a new object.
	/// </summary>
	public static List<ItemData> Order( IReadOnlyList<ItemData> slots )
	{
		var tidy = new List<ItemData>();

		if ( slots is null )
			return tidy;

		var groups = new Dictionary<MergeKey, List<ItemData>>();

		foreach ( var item in slots )
		{
			if ( item is null )
				continue;

			// A stack that never merges with anything is its own entry.
			if ( !StackRules.IsStackable( item ) )
			{
				tidy.Add( item );
				continue;
			}

			var key = new MergeKey( item.Kind, item.Quality, item.OrderId, item.To );

			if ( !groups.TryGetValue( key, out var group ) )
				groups[key] = group = [];

			group.Add( item );
		}

		foreach ( var group in groups.Values )
			AddMerged( tidy, group );

		// Sorted with the place each entry came from, so entries that compare equal keep theirs (List.Sort is not a
		// stable sort): sorting an already tidy grid again changes nothing.
		var ranked = tidy.Select( ( item, index ) => (Item: item, Index: index) ).ToList();

		ranked.Sort( ( a, b ) =>
		{
			var order = Compare( a.Item, b.Item );
			return order != 0 ? order : a.Index.CompareTo( b.Index );
		} );

		return ranked.Select( x => x.Item ).ToList();
	}

	/// <summary>
	/// Add a group of stacks that may merge to the order: as few stacks as the kind's size allows, whole ones first, so
	/// the total count and the quality are the group's own, exactly. A group of one is added as it is.
	/// </summary>
	static void AddMerged( List<ItemData> tidy, List<ItemData> group )
	{
		if ( group.Count == 1 )
		{
			tidy.Add( group[0] );
			return;
		}

		var cap = group[0].MaxStack;
		var left = group.Sum( x => x.Count );

		while ( left > 0 )
		{
			var stack = group[0].Clone();
			stack.Count = System.Math.Min( left, cap );
			left -= stack.Count;
			tidy.Add( stack );
		}
	}

	/// <summary>
	/// What two stacks must agree on to be put together: the same quality, as well as the same kind, order and
	/// addressee. Stricter than <see cref="StackRules.CanMerge(ItemData, ItemData)"/> for a kind whose quality does not
	/// matter, which is deliberate: a tidy keeps every quality it finds.
	/// </summary>
	readonly record struct MergeKey( string Kind, int Quality, string OrderId, string To );

	/// <summary>
	/// Where a kind sits in the order: its place in the catalogue, the catalogue's length for a kind it no longer has,
	/// and after everything for an ordinary item.
	/// </summary>
	static int Rank( ItemData item )
	{
		if ( !item.IsBatch )
			return int.MaxValue;

		var kinds = ItemCatalog.Items;

		for ( var i = 0; i < kinds.Count; i++ )
		{
			if ( kinds[i].Id == item.Kind )
				return i;
		}

		return kinds.Count;
	}

	/// <summary>
	/// The order two items sit in, which must be a total one: an item that compared equal to another and then moved on
	/// a second tidy would make the button do something every time it is pressed.
	/// </summary>
	static int Compare( ItemData a, ItemData b )
	{
		var rank = Rank( a ).CompareTo( Rank( b ) );

		if ( rank != 0 )
			return rank;

		var quality = b.Quality.CompareTo( a.Quality );

		if ( quality != 0 )
			return quality;

		var count = b.Count.CompareTo( a.Count );

		if ( count != 0 )
			return count;

		var name = string.Compare( a.Name, b.Name, System.StringComparison.OrdinalIgnoreCase );

		if ( name != 0 )
			return name;

		// The rest tells apart items of one name, and parcels of one kind: two of them may differ only in what they are
		// packed for or who packed them.
		var order = string.CompareOrdinal( a.OrderId, b.OrderId );

		if ( order != 0 )
			return order;

		var to = string.CompareOrdinal( a.To, b.To );

		if ( to != 0 )
			return to;

		var prefab = string.CompareOrdinal( a.PrefabPath, b.PrefabPath );

		if ( prefab != 0 )
			return prefab;

		var model = string.CompareOrdinal( a.ModelPath, b.ModelPath );

		return model != 0 ? model : string.CompareOrdinal( a.Description, b.Description );
	}
}
