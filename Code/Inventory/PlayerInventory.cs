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
	/// Put an item in the first free slot. Returns false when there is none. Host only.
	/// </summary>
	public bool Add( ItemData data )
	{
		if ( data is null || !CheckAuthority( nameof( Add ) ) )
			return false;

		Resize();
		var free = System.Array.FindIndex( slots, x => x is null );

		if ( free < 0 )
			return false;

		slots[free] = data;
		Changed();
		return true;
	}

	/// <summary>
	/// Put a new batch of a catalog kind in the first free slot. Returns false when there is no room, no such kind or
	/// nothing to add. Host only.
	/// </summary>
	public bool AddItem( string kind, int count, int quality, string orderId = "" ) =>
		ItemCatalog.Make( kind, count, quality, orderId ) is { } batch && Add( batch );

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
	/// Take exactly this many from the batch in a slot and hand them back as a batch of their own (same kind and quality),
	/// or null when the slot is not a batch of at least that many. Host only.
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

		if ( At( slot ) is { } there && there.Kind == piece.Kind && there.Quality == piece.Quality )
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
	/// Take the item out of a slot and drop it. On the player's client this asks the host.
	/// </summary>
	public void Drop( int slot )
	{
		if ( IsAuthority )
			DoDrop( slot );
		else
			HostDrop( slot );
	}

	[Rpc.Host]
	void HostMove( int from, int to )
	{
		if ( IsOwnerCalling() )
			DoMove( from, to );
	}

	[Rpc.Host]
	void HostDrop( int slot )
	{
		if ( IsOwnerCalling() )
			DoDrop( slot );
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

	void DoDrop( int slot )
	{
		// A parcel stays with its owner until it is sent: nothing puts a dropped item back in the world yet.
		if ( At( slot ) is { IsBatch: true } batch && ItemCatalog.Find( batch.Kind ) is { CanDrop: false } )
			return;

		var item = Remove( slot );

		if ( item is null )
			return;

		Log.Warning( $"PlayerInventory: dropped '{item.Name}', but nothing puts it back in the world yet." );
		Global.IInventoryEvents.Post( x => x.OnItemDropped( this, item ) );
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
