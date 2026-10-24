namespace Sandbox;

/// <summary>
/// A grid of slots that the storage window (<see cref="StorageMenu"/>) shows next to the player's bag: the storage box
/// and a vehicle's cargo. It holds the part they share: the view this machine keeps of the contents, and the requests
/// from the player's client (move, swap, combine, split). Every request is a <c>[Rpc.Host]</c> call with slot indices
/// only; the host takes the player from <c>Rpc.Caller</c> (<see cref="PlayerCrime.For"/>), checks they stand within
/// reach of THIS container (<see cref="IsNear"/>) and may use it (<see cref="Admit"/>), checks the slots and the items,
/// then calls <see cref="StorageRules"/>, which changes nothing unless everything fits.
/// </summary>
/// <remarks>
/// What a container holds is a <see cref="CrimeRecord"/> section and an id (<see cref="Contents"/>): a storage box uses
/// its owner's record, a cargo uses a record of its own that nothing saves. The rules are the same either way, so a move
/// never loses or doubles an item. The host sends the contents to the player who asked, to nobody else.
/// </remarks>
public abstract class ItemContainer : Component
{
	// -- What the window needs ---------------------------------------------------------------------------------------

	/// <summary>The window's heading and the word in "the ... is full".</summary>
	public abstract string Title { get; }

	/// <summary>The word in the bag's menu entry "Move to ...": "storage", "cargo".</summary>
	public abstract string ActionWord { get; }

	/// <summary>How many slots wide the grid is.</summary>
	public abstract int ColumnCount { get; }

	/// <summary>How many slots high the grid is.</summary>
	public abstract int RowCount { get; }

	/// <summary>The number of slots.</summary>
	public int Capacity => ColumnCount * RowCount;

	/// <summary>How close, in world units, a player must be for the host to move things for them.</summary>
	public abstract float Reach { get; }

	/// <summary>
	/// Whether a position is within <see cref="Reach"/> of this container.
	/// </summary>
	public virtual bool IsNear( Vector3 position ) => WorldPosition.Distance( position ) <= Reach;

	/// <summary>
	/// The sentence a far-away player hears.
	/// </summary>
	protected abstract string TooFarText { get; }

	// -- What this machine knows of the contents: only the player who asked is sent them -----------------------------

	ItemData[] view = [];

	/// <summary>
	/// Whether the host has answered since the window opened.
	/// </summary>
	public bool ViewReady { get; private set; }

	/// <summary>
	/// Bumps whenever the host sends the contents, so the window redraws.
	/// </summary>
	public int ViewVersion { get; private set; }

	/// <summary>
	/// The item in a slot as the host last told this player, or null. Empty before the host has answered.
	/// </summary>
	public ItemData ViewAt( int slot ) => slot >= 0 && slot < view.Length ? view[slot] : null;

	/// <summary>
	/// How many items the host last said are in the container.
	/// </summary>
	public int ViewCount => view.Count( x => x is not null );

	/// <summary>
	/// Forget the contents (the window closed), so nothing stays in memory on this machine.
	/// </summary>
	public virtual void ClearView()
	{
		view = [];
		ViewReady = false;
		ViewVersion++;
	}

	/// <summary>
	/// Extra contents the grid cannot show, sent with the view as text (a cargo's big objects). Empty for none. Host.
	/// </summary>
	protected virtual string ExtraJson() => "";

	/// <summary>
	/// The extra contents arrived on this machine (the player who asked).
	/// </summary>
	protected virtual void OnExtra( string json )
	{
	}

	/// <summary>
	/// The host said this player may not use the container.
	/// </summary>
	protected virtual void OnRefused() => StorageMenu.Refused( this );

	// -- On the host --------------------------------------------------------------------------------------------------

	/// <summary>
	/// Whether this player may use the container now. Say why to the player when not. Called after the reach check.
	/// Host only.
	/// </summary>
	protected virtual bool Admit( Asker who ) => true;

	/// <summary>
	/// The record and the id the contents are kept under for this player. Host only.
	/// </summary>
	protected abstract (CrimeRecord Record, string Id) Contents( Asker who );

	// -- Requests from the player's client ----------------------------------------------------------------------------

	/// <summary>
	/// The window opened or wants the contents again. The answer goes to the caller alone.
	/// </summary>
	[Rpc.Host]
	public void RequestRefresh()
	{
		if ( Begin() is { } who )
			Reply( who, CrimeResult.Success() );
	}

	/// <summary>
	/// Move the item in a bag slot to the first free slot of the container.
	/// </summary>
	[Rpc.Host]
	public void RequestStore( int bagSlot )
	{
		if ( Begin() is not { } who )
			return;

		var (record, id) = Contents( who );
		Reply( who, StorageRules.Store( record, id, Capacity, who.Bag, bagSlot ) );
	}

	/// <summary>
	/// Move the item in a slot of the container to the first free bag slot.
	/// </summary>
	[Rpc.Host]
	public void RequestTake( int boxSlot )
	{
		if ( Begin() is not { } who )
			return;

		var (record, id) = Contents( who );
		Reply( who, StorageRules.Take( record, id, who.Bag, boxSlot ) );
	}

	/// <summary>
	/// Move the item in a slot of the container to another slot of the container, swapping with what is there.
	/// </summary>
	[Rpc.Host]
	public void RequestMoveInBox( int from, int to )
	{
		if ( Begin() is not { } who )
			return;

		var (record, id) = Contents( who );
		Reply( who, StorageRules.MoveInBox( record, id, Capacity, from, to ) );
	}

	/// <summary>
	/// Move the item in a bag slot to a chosen slot of the container (a drag). An item there goes to the freed bag slot.
	/// </summary>
	[Rpc.Host]
	public void RequestStoreTo( int bagSlot, int boxSlot )
	{
		if ( Begin() is not { } who )
			return;

		var (record, id) = Contents( who );
		Reply( who, StorageRules.StoreTo( record, id, Capacity, who.Bag, bagSlot, boxSlot ) );
	}

	/// <summary>
	/// Move the item in a slot of the container to a chosen bag slot (a drag). An item there goes to the freed slot.
	/// </summary>
	[Rpc.Host]
	public void RequestTakeTo( int boxSlot, int bagSlot )
	{
		if ( Begin() is not { } who )
			return;

		var (record, id) = Contents( who );
		Reply( who, StorageRules.TakeTo( record, id, who.Bag, boxSlot, bagSlot ) );
	}

	/// <summary>
	/// Move the item in a bag slot to another bag slot (a drag inside the window's bag), swapping with what is there.
	/// Asked here, not of the inventory, so the reach and use checks apply while the window is open.
	/// </summary>
	[Rpc.Host]
	public void RequestMoveInBag( int from, int to )
	{
		if ( Begin() is not { } who )
			return;

		Reply( who, StorageRules.MoveInBag( who.Bag, from, to ) );
	}

	/// <summary>
	/// Combine or split with a drag (<see cref="StackRules"/>), between any two slots of the bag and the container.
	/// Sides are <see cref="StorageRules.SideBag"/> (0) and <see cref="StorageRules.SideBox"/> (1); the mode is a
	/// <see cref="StackMode"/> (1 combine, 2 split, 3 both). The host checks all of it, so only indices travel.
	/// </summary>
	[Rpc.Host]
	public void RequestStack( int fromSide, int from, int toSide, int to, int mode )
	{
		if ( Begin() is not { } who || !StackRules.IsMode( mode ) )
			return;

		var (record, id) = Contents( who );
		Reply( who, StorageRules.Stack( record, id, Capacity, who.Bag, fromSide, from, toSide, to, (StackMode)mode ) );
	}

	/// <summary>
	/// Who is asking, once the host has checked they may.
	/// </summary>
	protected sealed record Asker( Connection Caller, PlayerCrime Player, PlayerProgress Progress, CrimeRecord Crime )
	{
		public PlayerInventory Bag => Player.Components.Get<PlayerInventory>();
	}

	/// <summary>
	/// The player behind the running RPC, or null when they may not do this: their record is not loaded, they are not
	/// within reach, or <see cref="Admit"/> says no. Host only.
	/// </summary>
	protected Asker Begin()
	{
		var caller = Rpc.Caller ?? Connection.Local;

		if ( !Networking.IsHost || caller is null || PlayerCrime.For( caller ) is not { Ready: true } player
		     || PlayerProgress.For( caller ) is not { Loaded: true } progress
		     || progress.Record?.Crime is not { } crime )
			return null;

		var who = new Asker( caller, player, progress, crime );

		if ( !IsNear( player.WorldPosition ) )
		{
			player.Tell( CrimeResult.Fail( TooFarText ) );
			return null;
		}

		if ( Admit( who ) )
			return who;

		SendView( caller, false, "", "" );
		return null;
	}

	/// <summary>
	/// Save what a request did, say how it went, and send the contents back to the caller alone. Host only.
	/// </summary>
	protected void Reply( Asker who, CrimeResult result )
	{
		if ( result.Ok )
			who.Progress.NotifyChanged( ProgressChange.Crime );

		who.Player.Tell( result );
		var (record, id) = Contents( who );
		SendView( who.Caller, true, Json.Serialize( StorageRules.View( record, id, Capacity ) ), ExtraJson() );
	}

	void SendView( Connection caller, bool allowed, string json, string extra )
	{
		using ( Rpc.FilterInclude( caller ) )
			ReceiveView( allowed, json, extra );
	}

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	void ReceiveView( bool allowed, string json, string extra )
	{
		view = allowed && !string.IsNullOrEmpty( json ) ? Json.Deserialize<ItemData[]>( json ) ?? [] : [];
		ViewReady = allowed;
		ViewVersion++;
		OnExtra( allowed ? extra : "" );

		// Told it is not theirs: the window closes itself.
		if ( !allowed )
			OnRefused();
	}
}
