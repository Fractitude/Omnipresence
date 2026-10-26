namespace Sandbox;

/// <summary>
/// The items a player carries, in a grid of slots that each hold one item. Put it on the player's
/// <see cref="PlayerController"/> GameObject.
/// <para>
/// The host owns it. <see cref="Add"/> and <see cref="Remove"/> run on the host (e.g. from an <see cref="Interactable"/>
/// that runs on the host); the slots are synced from there. The player's own client may ask the host to
/// <see cref="Move"/> and <see cref="Drop"/> its own items, and the host checks who asked.
/// </para>
/// </summary>
public sealed class PlayerInventory : Component
{
	[Property, Range( 1, 12 )] public int Columns { get; set; } = 6;

	[Property, Range( 1, 12 )] public int Rows { get; set; } = 5;

	public int SlotCount => Columns * Rows;

	/// <summary>
	/// The slots as JSON, set by the host. Everyone else reads <see cref="slots"/> from it.
	/// </summary>
	[Sync( SyncFlags.FromHost ), Change( nameof( OnSavedChanged ) )]
	string Saved { get; set; } = "";

	ItemData[] slots = [];

	/// <summary>
	/// Bumps whenever a slot changes, so a panel knows to redraw. To react to a change instead, implement
	/// <see cref="Global.IInventoryEvents"/>.
	/// </summary>
	public int Version { get; private set; }

	/// <summary>
	/// The inventory of the player on this computer, or null.
	/// </summary>
	public static PlayerInventory Local => Players.Local<PlayerInventory>();

	/// <summary>
	/// This computer runs the game's rules: the host, or a game with no network.
	/// </summary>
	static bool IsAuthority => !Networking.IsActive || Networking.IsHost;

	/// <summary>
	/// The item in a slot, or null if it is empty.
	/// </summary>
	public ItemData At( int slot )
	{
		Resize();
		return slot >= 0 && slot < slots.Length ? slots[slot] : null;
	}

	/// <summary>
	/// Whether there is a free slot.
	/// </summary>
	public bool HasRoom
	{
		get
		{
			Resize();
			return slots.Any( x => x is null );
		}
	}

	/// <summary>
	/// How many slots are free.
	/// </summary>
	public int FreeSlots
	{
		get
		{
			Resize();
			return slots.Count( x => x is null );
		}
	}

	/// <summary>
	/// Put an item in the first free slot. The caller removes the item from the world when this returns true. Host only.
	/// </summary>
	public bool Add( IItem item ) => item is not null && Add( ItemData.From( item ) );

	/// <summary>
	/// Put an item in the first free slot. Returns false when there is none. A batch above its kind's stack size takes
	/// as many slots as it needs, all or nothing: <paramref name="data"/> itself becomes the first, full stack and the
	/// rest are copies. Host only.
	/// </summary>
	public bool Add( ItemData data )
	{
		if ( data is null || !CheckAuthority( nameof( Add ) ) )
			return false;

		Resize();
		var cap = data.IsBatch ? data.MaxStack : int.MaxValue;
		var needed = data.IsBatch ? ItemCatalog.SlotsFor( data.Kind, data.Count ) : 1;

		if ( needed < 1 || FreeSlots < needed )
			return false;

		var left = data.Count;
		data.Count = System.Math.Min( left, cap );
		left -= data.Count;
		slots[System.Array.FindIndex( slots, x => x is null )] = data;

		while ( left > 0 )
		{
			var part = data.Clone();
			part.Count = System.Math.Min( left, cap );
			left -= part.Count;
			slots[System.Array.FindIndex( slots, x => x is null )] = part;
		}

		Changed();
		return true;
	}

	/// <summary>
	/// Put as much of <paramref name="data"/> in the bag as fits and return how many of its count went in (0 when nothing
	/// did). A batch tops up the stacks it may merge with and fills free slots (<see cref="PickupRules"/>); an ordinary
	/// item takes one free slot or nothing. <paramref name="data"/> itself is not changed: the caller keeps the rest.
	/// Written in one step, so the bag gains exactly the returned count. Host only.
	/// </summary>
	public int AddPartial( ItemData data )
	{
		if ( data is null || !CheckAuthority( nameof( AddPartial ) ) )
			return 0;

		Resize();
		var taken = PickupRules.Plan( slots, data, planned );

		if ( taken < 1 )
			return 0;

		foreach ( var write in planned )
			slots[write.Slot] = write.Item;

		Changed();
		return taken;
	}

	/// <summary>
	/// How many of <paramref name="data"/>'s count <see cref="AddPartial"/> would take right now, without changing
	/// anything. Works on every machine, from the synced slots (a prompt asks it).
	/// </summary>
	public int RoomFor( ItemData data )
	{
		Resize();
		return data is null ? 0 : PickupRules.Plan( slots, data, new() );
	}

	readonly List<SlotWrite> planned = new();

	/// <summary>
	/// Put a new batch of a catalog kind in the free slots: as many full stacks (the kind's
	/// <see cref="ItemDef.MaxStack"/>) as the count needs, then the remainder, all of the same quality. All or nothing:
	/// false, and nothing added, when there is no room for all of it, no such kind or nothing to add. Host only.
	/// </summary>
	public bool AddItem( string kind, int count, int quality, string orderId = "" )
	{
		if ( ItemCatalog.Find( kind ) is null || count < 1 || ItemCatalog.SlotsFor( kind, count ) > FreeSlots )
			return false;

		// The catalogue's cap on a saved total is for records; a count asked for here is added in full.
		var first = ItemCatalog.Make( kind, ItemCatalog.MaxStackOf( kind ), quality, orderId );
		first.Count = count;
		return Add( first );
	}

	/// <summary>
	/// How many of a kind the player has in batches of at least this quality. Works on every machine.
	/// </summary>
	public int CountOf( string kind, int minQuality = 0 )
	{
		Resize();
		return slots.Where( x => x is not null && x.Kind == kind && x.Quality >= minQuality ).Sum( x => x.Count );
	}

	/// <summary>
	/// Take this many of a kind from the first matching batches (of at least this quality), emptying a batch before the
	/// next. All or nothing: false, and nothing taken, when there are fewer. <paramref name="taken"/> is what came out, one
	/// entry per batch touched, so the caller can work out the quality. Host only.
	/// </summary>
	public bool TryTake( string kind, int count, out List<ItemData> taken, int minQuality = 0 )
	{
		taken = [];

		if ( count < 1 || !CheckAuthority( nameof( TryTake ) ) || CountOf( kind, minQuality ) < count )
			return false;

		var left = count;

		for ( var i = 0; i < slots.Length && left > 0; i++ )
		{
			if ( slots[i] is not { } batch || batch.Kind != kind || batch.Quality < minQuality )
				continue;

			var part = System.Math.Min( batch.Count, left );
			var piece = batch.Clone();
			piece.Count = part;
			taken.Add( piece );
			left -= part;

			if ( part == batch.Count )
				slots[i] = null;
			else
				batch.Count -= part;
		}

		Changed();
		return true;
	}

	/// <summary>
	/// Take exactly this many from the batch in a slot and hand them back as a batch of their own (same kind and
	/// quality), or null when the slot is not a batch of at least that many. Host only.
	/// </summary>
	public ItemData TakeFrom( int slot, int count )
	{
		if ( count < 1 || !CheckAuthority( nameof( TakeFrom ) ) || At( slot ) is not { IsBatch: true } batch
		     || batch.Count < count )
			return null;

		var piece = batch.Clone();
		piece.Count = count;

		if ( count == batch.Count )
			slots[slot] = null;
		else
			batch.Count -= count;

		Changed();
		return piece;
	}

	/// <summary>
	/// Put back what <see cref="TakeFrom"/> took from a slot: onto the same batch if it is still there, else into the
	/// empty slot, else the first free one. Host only. Returns false if there is no room at all.
	/// </summary>
	public bool Return( int slot, ItemData piece )
	{
		if ( piece is null || !CheckAuthority( nameof( Return ) ) )
			return false;

		if ( At( slot ) is { } there && there.Kind == piece.Kind && there.Quality == piece.Quality
		     && there.Count + piece.Count <= there.MaxStack )
		{
			there.Count += piece.Count;
			Changed();
			return true;
		}

		if ( At( slot ) is null )
		{
			slots[slot] = piece;
			Changed();
			return true;
		}

		return Add( piece );
	}

	/// <summary>
	/// Take the whole batch out of a slot, or null if it is empty or not a batch. Host only.
	/// </summary>
	public ItemData TakeBatch( int slot ) => At( slot ) is { IsBatch: true } ? Remove( slot ) : null;

	/// <summary>
	/// Take the item out of a slot and hand it back, or null if it is empty. Host only.
	/// </summary>
	public ItemData Remove( int slot )
	{
		if ( !CheckAuthority( nameof( Remove ) ) )
			return null;

		var item = At( slot );

		if ( item is null )
			return null;

		slots[slot] = null;
		Changed();
		return item;
	}

	/// <summary>
	/// Move the item in a slot to another, swapping with what is there. On the player's client this asks the host.
	/// </summary>
	public void Move( int from, int to )
	{
		if ( IsAuthority )
			DoMove( from, to );
		else
			HostMove( from, to );
	}

	/// <summary>
	/// Combine or split with a drag (<see cref="StackRules"/>): the stack in <paramref name="from"/> is added to the
	/// stack in <paramref name="to"/> (Combine), or half of it is moved to an empty slot or added to the stack there
	/// (Split, or both). Checked in full before anything changes, written in one step. On the player's client this asks
	/// the host.
	/// </summary>
	public void Stack( int from, int to, StackMode mode )
	{
		if ( IsAuthority )
			DoStack( from, to, mode );
		else
			HostStack( from, to, (int)mode );
	}

	/// <summary>
	/// Combine or split between two bag slots. Returns null when it worked, else the reason it did not, and then
	/// nothing has changed. Host only.
	/// </summary>
	public string TryStack( int from, int to, StackMode mode )
	{
		if ( !CheckAuthority( nameof( TryStack ) ) )
			return "Nothing happens.";

		Resize();

		if ( from == to || from < 0 || to < 0 || from >= slots.Length || to >= slots.Length )
			return "Nothing happens.";

		if ( !StackRules.Plan( slots[from], slots[to], mode, out var newFrom, out var newTo, out var error ) )
			return error;

		slots[from] = newFrom;
		slots[to] = newTo;
		Changed();
		return null;
	}

	/// <summary>
	/// Put an item in a slot, or empty it with null, replacing what is there. For code that has already checked a move
	/// as a whole (<see cref="StorageRules.Stack"/>). Host only.
	/// </summary>
	public bool SetSlot( int slot, ItemData item )
	{
		if ( !CheckAuthority( nameof( SetSlot ) ) )
			return false;

		Resize();

		if ( slot < 0 || slot >= slots.Length )
			return false;

		slots[slot] = item;
		Changed();
		return true;
	}

	/// <summary>
	/// Take the item out of a slot and drop it. On the player's client this asks the host.
	/// </summary>
	public void Drop( int slot )
	{
		if ( IsAuthority )
			DoDrop( slot );
		else
			HostDrop( slot );
	}

	/// <summary>
	/// Tidy the bag: the same kinds together, stacks put together where the game may merge them, and everything packed
	/// from the first slot with no gaps (<see cref="InventorySort"/>). No count and no quality changes. On the player's
	/// client this asks the host.
	/// </summary>
	public void Sort()
	{
		if ( IsAuthority )
			DoSort();
		else
			HostSort();
	}

	[Rpc.Host]
	void HostMove( int from, int to )
	{
		if ( IsOwnerCalling() )
			DoMove( from, to );
	}

	[Rpc.Host]
	void HostStack( int from, int to, int mode )
	{
		if ( IsOwnerCalling() )
			DoStack( from, to, (StackMode)mode );
	}

	[Rpc.Host]
	void HostDrop( int slot )
	{
		if ( IsOwnerCalling() )
			DoDrop( slot );
	}

	[Rpc.Host]
	void HostSort()
	{
		if ( IsOwnerCalling() )
			DoSort();
	}

	/// <summary>
	/// The connection that called the running RPC is the player who owns this inventory (or it is the host itself).
	/// </summary>
	bool IsOwnerCalling()
	{
		var caller = Rpc.Caller;
		return caller is null || caller == Network.Owner;
	}

	void DoMove( int from, int to )
	{
		Resize();

		if ( from == to || from < 0 || to < 0 || from >= slots.Length || to >= slots.Length || slots[from] is null )
			return;

		(slots[from], slots[to]) = (slots[to], slots[from]);
		Changed();
	}

	void DoSort()
	{
		Resize();
		var ordered = InventorySort.Order( slots );

		// Nothing to do when the slots already hold the tidy order: the items are handed back as they are, so this is a
		// reference check, and a bag that is already tidy is not written and synced again.
		var moved = false;

		for ( var i = 0; i < slots.Length && !moved; i++ )
			moved = !ReferenceEquals( slots[i], i < ordered.Count ? ordered[i] : null );

		if ( !moved )
			return;

		for ( var i = 0; i < slots.Length; i++ )
			slots[i] = i < ordered.Count ? ordered[i] : null;

		Changed();
	}

	void DoStack( int from, int to, StackMode mode )
	{
		if ( !StackRules.IsMode( (int)mode ) )
			return;

		// Say why it did not work, to the player who asked, in chat.
		if ( TryStack( from, to, mode ) is { } error && Network.Owner is { } owner
		     && PlayerCrime.For( owner ) is { } player )
			player.Tell( CrimeResult.Fail( error ) );
	}

	void DoDrop( int slot )
	{
		// A kind can opt out of being dropped (ItemDef.CanDrop); none does today, parcels drop like anything else.
		if ( At( slot ) is { IsBatch: true } batch && ItemCatalog.Find( batch.Kind ) is { CanDrop: false } )
			return;

		if ( At( slot ) is not { } held )
			return;

		if ( WorldItem.CountInScene( Scene ) >= WorldItem.MaxInScene )
		{
			Tell( "There is too much on the ground already." );
			return;
		}

		// The world item is made first, so a drop that cannot be placed never loses the item.
		if ( WorldItem.SpawnFrom( GameObject, held.Clone() ) is null )
		{
			Tell( "You cannot drop that here." );
			return;
		}

		var item = Remove( slot );

		if ( item is null )
			return;

		Global.IInventoryEvents.Post( x => x.OnItemDropped( this, item ) );
	}

	/// <summary>
	/// Say something to this player alone, in their chat. Host only.
	/// </summary>
	void Tell( string text )
	{
		if ( Components.Get<PlayerCrime>() is { } crime )
			crime.Tell( CrimeResult.Fail( text ) );
	}

	bool CheckAuthority( string method )
	{
		if ( IsAuthority )
			return true;

		Log.Warning( $"PlayerInventory.{method} runs on the host only." );
		return false;
	}

	void Resize()
	{
		if ( slots.Length != SlotCount )
			System.Array.Resize( ref slots, SlotCount );
	}

	void Changed()
	{
		Saved = Json.Serialize( slots );
		Updated();
	}

	void OnSavedChanged( string oldValue, string newValue )
	{
		// The host made this change itself and has already told the scene.
		if ( IsAuthority )
			return;

		slots = string.IsNullOrEmpty( newValue ) ? [] : Json.Deserialize<ItemData[]>( newValue ) ?? [];
		Updated();
	}

	void Updated()
	{
		Version++;
		Global.IInventoryEvents.Post( x => x.OnInventoryChanged( this ) );
	}
}
