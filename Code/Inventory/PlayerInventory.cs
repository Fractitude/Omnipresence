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
	/// Bumps whenever a slot changes, so a panel knows to redraw.
	/// </summary>
	public int Version { get; private set; }

	/// <summary>
	/// Raised on the host when an item is dropped. The item is out of the inventory by then. Nothing puts it back in the
	/// world yet.
	/// </summary>
	public event System.Action<PlayerInventory, ItemData> ItemDropped;

	static PlayerInventory local;

	/// <summary>
	/// The inventory of the player on this computer, or null.
	/// </summary>
	public static PlayerInventory Local
	{
		get
		{
			if ( !local.IsValid() )
				local = Game.ActiveScene?.GetAllComponents<PlayerInventory>().FirstOrDefault( x => !x.IsProxy );

			return local;
		}
	}

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
		var item = Remove( slot );

		if ( item is null )
			return;

		Log.Warning( $"PlayerInventory: dropped '{item.Name}', but nothing puts it back in the world yet." );
		ItemDropped?.Invoke( this, item );
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
		Version++;
		Saved = Json.Serialize( slots );
	}

	void OnSavedChanged( string oldValue, string newValue )
	{
		slots = string.IsNullOrEmpty( newValue ) ? [] : Json.Deserialize<ItemData[]>( newValue ) ?? [];
		Version++;
	}
}
