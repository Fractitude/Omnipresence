namespace Sandbox;

/// <summary>
/// The rules of the storage boxes, on a player's <see cref="CrimeRecord"/> and bag alone: no box, no connection. Every
/// move takes the item out of one place and puts it in the other in one go, and fails before changing anything, so an
/// item is never lost or doubled. The box (<see cref="StorageBox"/>) checks who asks and how far they are, then calls
/// these. Host only.
/// </summary>
public static class StorageRules
{
	/// <summary>The most boxes one player keeps items in.</summary>
	public const int MaxBoxes = 20;

	/// <summary>The most slots a box has (the largest grid, 12 by 8). A slot index outside it is refused.</summary>
	public const int MaxSlots = 96;

	public const int MaxColumns = 12;
	public const int MaxRows = 8;

	const int MaxTextLength = 200;

	/// <summary>
	/// The record of a box, or null when the player keeps nothing in it.
	/// </summary>
	public static StorageBoxRecord Find( CrimeRecord crime, string boxId ) =>
		crime?.Storage?.FirstOrDefault( x => x.Id == boxId );

	/// <summary>
	/// The item in a slot of a box, or null when it is empty.
	/// </summary>
	public static ItemData At( StorageBoxRecord box, int slot ) =>
		box?.Slots.FirstOrDefault( x => x.Slot == slot )?.Item;

	/// <summary>
	/// The first empty slot of a box with this many slots, or -1 when it is full.
	/// </summary>
	public static int FirstFree( StorageBoxRecord box, int capacity )
	{
		for ( var i = 0; i < capacity && i < MaxSlots; i++ )
		{
			if ( At( box, i ) is null )
				return i;
		}

		return -1;
	}

	/// <summary>
	/// The box's slots, one entry for each of its <paramref name="capacity"/> slots (null where empty), for the panel.
	/// </summary>
	public static ItemData[] View( CrimeRecord crime, string boxId, int capacity )
	{
		var box = Find( crime, boxId );
		var view = new ItemData[System.Math.Clamp( capacity, 0, MaxSlots )];

		for ( var i = 0; i < view.Length; i++ )
			view[i] = At( box, i );

		return view;
	}

	/// <summary>
	/// Move the item in a bag slot to the first empty slot of the box. Nothing changes when the slot is empty or the
	/// box is full.
	/// </summary>
	public static CrimeResult Store( CrimeRecord crime, string boxId, int capacity, PlayerInventory bag, int bagSlot )
	{
		if ( crime is null || bag is null || bagSlot < 0 || bagSlot >= bag.SlotCount || bag.At( bagSlot ) is null )
			return CrimeResult.Fail( "There is nothing in that slot." );

		var box = Find( crime, boxId );

		if ( box is null && crime.Storage.Count >= MaxBoxes )
			return CrimeResult.Fail( "You cannot keep things in any more boxes." );

		var free = FirstFree( box, capacity );

		if ( free < 0 )
			return CrimeResult.Fail( "The storage box is full." );

		// Checked everything; only now take it out of the bag and put it in the box.
		if ( bag.Remove( bagSlot ) is not { } item )
			return CrimeResult.Fail( "There is nothing in that slot." );

		if ( box is null )
		{
			box = new StorageBoxRecord { Id = boxId };
			crime.Storage.Add( box );
		}

		Set( box, free, item );
		return CrimeResult.Success();
	}

	/// <summary>
	/// Move the item in a box slot to the first free bag slot. Nothing changes when the slot is empty or the bag is
	/// full. A slot beyond the box's size is still reachable, so a box made smaller does not lock items away.
	/// </summary>
	public static CrimeResult Take( CrimeRecord crime, string boxId, PlayerInventory bag, int boxSlot )
	{
		var box = Find( crime, boxId );

		if ( bag is null || boxSlot < 0 || boxSlot >= MaxSlots || At( box, boxSlot ) is not { } item )
			return CrimeResult.Fail( "There is nothing in that slot." );

		// The bag takes it first; the box lets go only when it did.
		if ( !bag.HasRoom || !bag.Add( item ) )
			return CrimeResult.Fail( "Your bag is full. Make room first." );

		Set( box, boxSlot, null );
		Tidy( crime, box );
		return CrimeResult.Success();
	}

	/// <summary>
	/// Move the item in a bag slot to a chosen slot of the box. An item already there goes to the bag slot that was
	/// just freed (a swap). Everything is checked first, and the one step that could fail (the displaced item needs the
	/// freed slot, which is always there) is undone if it does, so nothing is lost or doubled.
	/// </summary>
	public static CrimeResult StoreTo(
		CrimeRecord crime, string boxId, int capacity, PlayerInventory bag, int bagSlot, int boxSlot )
	{
		if ( crime is null || bag is null || bagSlot < 0 || bagSlot >= bag.SlotCount || bag.At( bagSlot ) is null )
			return CrimeResult.Fail( "There is nothing in that slot." );

		if ( boxSlot < 0 || boxSlot >= capacity || boxSlot >= MaxSlots )
			return CrimeResult.Fail( "Nothing happens." );

		var box = Find( crime, boxId );

		if ( box is null && crime.Storage.Count >= MaxBoxes )
			return CrimeResult.Fail( "You cannot keep things in any more boxes." );

		var displaced = At( box, boxSlot );

		if ( bag.Remove( bagSlot ) is not { } item )
			return CrimeResult.Fail( "There is nothing in that slot." );

		if ( displaced is not null && !PlaceInBag( bag, displaced, bagSlot ) )
		{
			bag.Add( item );
			return CrimeResult.Fail( "Nothing happens." );
		}

		if ( box is null )
		{
			box = new StorageBoxRecord { Id = boxId };
			crime.Storage.Add( box );
		}

		Set( box, boxSlot, item );
		return CrimeResult.Success();
	}

	/// <summary>
	/// Move the item in a box slot to a chosen bag slot. An item already there goes to the box slot that was just freed
	/// (a swap). Checked first and undone on a failed step, like <see cref="StoreTo"/>.
	/// </summary>
	public static CrimeResult TakeTo( CrimeRecord crime, string boxId, PlayerInventory bag, int boxSlot, int bagSlot )
	{
		var box = Find( crime, boxId );

		if ( bag is null || boxSlot < 0 || boxSlot >= MaxSlots || At( box, boxSlot ) is not { } item )
			return CrimeResult.Fail( "There is nothing in that slot." );

		if ( bagSlot < 0 || bagSlot >= bag.SlotCount )
			return CrimeResult.Fail( "Nothing happens." );

		var displaced = bag.At( bagSlot ) is null ? null : bag.Remove( bagSlot );

		if ( !PlaceInBag( bag, item, bagSlot ) )
		{
			if ( displaced is not null )
				bag.Add( displaced );

			return CrimeResult.Fail( "Your bag is full. Make room first." );
		}

		Set( box, boxSlot, displaced );
		Tidy( crime, box );
		return CrimeResult.Success();
	}

	/// <summary>
	/// Move the item in a bag slot to another bag slot, swapping with what is there.
	/// </summary>
	public static CrimeResult MoveInBag( PlayerInventory bag, int from, int to )
	{
		if ( bag is null || from == to || from < 0 || from >= bag.SlotCount || to < 0 || to >= bag.SlotCount
		     || bag.At( from ) is null )
			return CrimeResult.Fail( "Nothing happens." );

		bag.Move( from, to );
		return CrimeResult.Success();
	}

	/// <summary>
	/// Put an item in a given, empty bag slot. The bag only adds to its first free slot, so it goes there and is then
	/// moved. False, with the bag as it was, when there is no free slot.
	/// </summary>
	static bool PlaceInBag( PlayerInventory bag, ItemData item, int slot )
	{
		if ( bag.At( slot ) is not null || !bag.Add( item ) )
			return false;

		for ( var i = 0; i < bag.SlotCount; i++ )
		{
			if ( ReferenceEquals( bag.At( i ), item ) )
			{
				if ( i != slot )
					bag.Move( i, slot );

				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Move the item in a box slot to another slot of the same box, swapping with what is there.
	/// </summary>
	public static CrimeResult MoveInBox( CrimeRecord crime, string boxId, int capacity, int from, int to )
	{
		var box = Find( crime, boxId );

		if ( from == to || from < 0 || from >= MaxSlots || to < 0 || to >= capacity || to >= MaxSlots
		     || At( box, from ) is not { } item )
			return CrimeResult.Fail( "Nothing happens." );

		var other = At( box, to );
		Set( box, to, item );
		Set( box, from, other );
		return CrimeResult.Success();
	}

	/// <summary>The side of a move that is the player's bag.</summary>
	public const int SideBag = 0;

	/// <summary>The side of a move that is the storage box.</summary>
	public const int SideBox = 1;

	/// <summary>
	/// Combine or split with a drag (<see cref="StackRules"/>) between any two slots of the bag and the box: bag to
	/// box, box to bag, inside the box and inside the bag. The result is planned and checked in full first (indices,
	/// kinds, counts, the cap, a free box record); only then are the two slots written, and neither write can fail, so
	/// the total is kept exactly and a refusal changes nothing. Sides are <see cref="SideBag"/> and
	/// <see cref="SideBox"/>.
	/// </summary>
	public static CrimeResult Stack(
		CrimeRecord crime, string boxId, int capacity, PlayerInventory bag,
		int fromSide, int from, int toSide, int to, StackMode mode )
	{
		var nothing = CrimeResult.Fail( "Nothing happens." );

		if ( crime is null || bag is null || !StackRules.IsMode( (int)mode ) || fromSide is not (SideBag or SideBox)
		     || toSide is not (SideBag or SideBox) || (fromSide == toSide && from == to) )
			return nothing;

		var box = Find( crime, boxId );

		// A slot of the bag must exist. In the box the source may be any slot (a box made smaller keeps its items
		// reachable), the target must be inside the box's size.
		if ( !SlotOk( bag, fromSide, from, MaxSlots ) || !SlotOk( bag, toSide, to, capacity ) )
			return nothing;

		var source = fromSide == SideBag ? bag.At( from ) : At( box, from );
		var target = toSide == SideBag ? bag.At( to ) : At( box, to );

		if ( source is null )
			return CrimeResult.Fail( "There is nothing in that slot." );

		if ( !StackRules.Plan( source, target, mode, out var newFrom, out var newTo, out var error ) )
			return CrimeResult.Fail( error );

		if ( toSide == SideBox && box is null )
		{
			if ( crime.Storage.Count >= MaxBoxes )
				return CrimeResult.Fail( "You cannot keep things in any more boxes." );

			box = new StorageBoxRecord { Id = boxId };
			crime.Storage.Add( box );
		}

		// Everything is checked: write both slots. The bag's writes only fail on a bad index, checked above.
		if ( !Write( bag, box, fromSide, from, newFrom ) || !Write( bag, box, toSide, to, newTo ) )
			return nothing;

		Tidy( crime, box );
		return CrimeResult.Success();
	}

	static bool SlotOk( PlayerInventory bag, int side, int slot, int boxLimit ) =>
		slot >= 0 && slot < (side == SideBag ? bag.SlotCount : System.Math.Min( boxLimit, MaxSlots ));

	static bool Write( PlayerInventory bag, StorageBoxRecord box, int side, int slot, ItemData item )
	{
		if ( side == SideBag )
			return bag.SetSlot( slot, item );

		Set( box, slot, item );
		return true;
	}

	/// <summary>
	/// Take the parcels of an order out of every box, since nobody wants them. Returns how many went. For a cancelled
	/// order, whether its player is there or away.
	/// </summary>
	public static int RemoveParcelsOf( CrimeRecord crime, string orderId )
	{
		var removed = 0;

		foreach ( var box in (crime?.Storage ?? []).ToList() )
		{
			removed += box.Slots.RemoveAll(
				x => x.Item is { Kind: ItemCatalog.Parcel } parcel && parcel.OrderId == orderId );

			Tidy( crime, box );
		}

		return removed;
	}

	/// <summary>
	/// Put an item in a slot (or empty it, with null), keeping the list ordered by slot.
	/// </summary>
	static void Set( StorageBoxRecord box, int slot, ItemData item )
	{
		box.Slots.RemoveAll( x => x.Slot == slot );

		if ( item is null )
			return;

		box.Slots.Add( new StorageSlotRecord { Slot = slot, Item = item } );
		box.Slots.Sort( ( a, b ) => a.Slot.CompareTo( b.Slot ) );
	}

	/// <summary>
	/// Forget a box that holds nothing, so the record does not fill up with empty ones.
	/// </summary>
	static void Tidy( CrimeRecord crime, StorageBoxRecord box )
	{
		if ( box is not null && box.Slots.Count == 0 )
			crime.Storage.Remove( box );
	}

	// -- Loading ------------------------------------------------------------------------------------------------------

	/// <summary>
	/// Make loaded boxes safe: a file is outside input. Ids are checked, sizes capped, slots made unique and in range,
	/// every item cleaned like a bag item (<see cref="CleanItem"/>), and boxes left empty are dropped.
	/// </summary>
	public static List<StorageBoxRecord> Clean( List<StorageBoxRecord> boxes )
	{
		var cleaned = new List<StorageBoxRecord>();

		var unique = (boxes ?? [])
			.Where( x => x is not null && IsId( x.Id ) )
			.GroupBy( x => x.Id )
			.Select( x => x.First() );

		foreach ( var box in unique )
		{
			var clean = new StorageBoxRecord { Id = box.Id };
			var extra = new List<ItemData>();

			foreach ( var entry in (box.Slots ?? []).Where( x => x is not null && x.Slot is >= 0 and < MaxSlots ) )
			{
				if ( clean.Slots.Any( x => x.Slot == entry.Slot ) )
					continue;

				var stacks = CleanStacks( entry.Item );

				if ( stacks.Count == 0 )
					continue;

				clean.Slots.Add( new StorageSlotRecord { Slot = entry.Slot, Item = stacks[0] } );
				extra.AddRange( stacks.Skip( 1 ) );
			}

			// The rest of a stack above its kind's size goes into free slots of the same box, beyond its visible size
			// if need be (they stay reachable). Only a box with no slot left at all (96) loses it, and says so.
			foreach ( var item in extra )
			{
				var free = Enumerable.Range( 0, MaxSlots ).FirstOrDefault(
					i => clean.Slots.All( x => x.Slot != i ), -1 );

				if ( free < 0 )
					Log.Warning( $"StorageRules: no free slot in box {box.Id} for {item.Count} x {item.Kind}." );
				else
					clean.Slots.Add( new StorageSlotRecord { Slot = free, Item = item } );
			}

			clean.Slots.Sort( ( a, b ) => a.Slot.CompareTo( b.Slot ) );

			if ( clean.Slots.Count > 0 )
				cleaned.Add( clean );

			if ( cleaned.Count >= MaxBoxes )
				break;
		}

		return cleaned;
	}

	/// <summary>
	/// An item from a file, made safe, as the stacks it becomes: a batch of a known kind is rebuilt from the catalogue
	/// (quality clamped, a parcel's contents capped) as full stacks of its kind's size and a remainder, so a stack
	/// saved above the size loses nothing (the total is cut to <see cref="ItemCatalog.MaxCount"/>); an ordinary item
	/// keeps its text, cut short, and a count of at least one. An unknown kind, or nothing, gives none.
	/// </summary>
	public static List<ItemData> CleanStacks( ItemData item )
	{
		if ( item is null )
			return [];

		if ( string.IsNullOrEmpty( item.Kind ) )
			return CleanItem( item ) is { } plain ? [plain] : [];

		var order = Cut( item.OrderId, CrimeRecord.MaxIdLength );
		var stacks = ItemCatalog.MakeStacks( item.Kind, item.Count, item.Quality, order );

		foreach ( var stack in stacks )
		{
			var parcel = stack.Kind == ItemCatalog.Parcel;
			stack.Contents = parcel ? CrimeRecord.CleanContents( item.Contents ) : new();
			stack.To = parcel ? Cut( item.To, 64 ) : "";

			if ( parcel )
				stack.CopySenderFrom( item );
		}

		return stacks;
	}

	/// <summary>
	/// An item from a file, made safe, as one item: the first stack of <see cref="CleanStacks"/>, or null.
	/// </summary>
	public static ItemData CleanItem( ItemData item )
	{
		if ( item is null )
			return null;

		if ( !string.IsNullOrEmpty( item.Kind ) )
			return CleanStacks( item ).FirstOrDefault();

		return new ItemData
		{
			Name = Cut( item.Name, 64 ),
			Description = Cut( item.Description, MaxTextLength ),
			PrefabPath = Cut( item.PrefabPath, MaxTextLength ),
			ModelPath = Cut( item.ModelPath, MaxTextLength ),
			Count = System.Math.Clamp( item.Count, 1, ItemCatalog.MaxCount ),
		};
	}

	static string Cut( string text, int max ) =>
		string.IsNullOrEmpty( text ) ? "" : text.Length > max ? text[..max] : text;

	static bool IsId( string id ) => !string.IsNullOrWhiteSpace( id ) && id.Length <= CrimeRecord.MaxIdLength
		&& id.All( x => char.IsAsciiLetterOrDigit( x ) || x is '-' or '_' );
}
