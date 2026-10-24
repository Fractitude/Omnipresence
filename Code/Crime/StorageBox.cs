namespace Sandbox;

/// <summary>
/// A storage box: a grid of slots a player can offload their bag into. Using it opens the storage panel
/// (<see cref="StorageMenu"/>) on the player's own screen; the press itself changes nothing (the option runs on the
/// player's client, <see cref="InteractionSide.Local"/>). Every move is a request to the host, which takes the player
/// from the network (<c>Rpc.Caller</c>), checks they stand within <see cref="Reach"/> of THIS box, checks the slots and
/// the items, and moves one item at a time (<see cref="StorageRules"/>). Only slot indices cross the network.
/// </summary>
/// <remarks>
/// Anyone may open and use any box. A box belongs to the first player who uses it, remembered by
/// <see cref="StorageRegistry"/> across restarts, and that owner's <see cref="CrimeRecord.Storage"/> is where what is
/// in it lives, under <see cref="BoxId"/>: the contents are one set for everyone who opens it, saved with the
/// owner, and reachable while the owner is away. The host sends the contents to the player who asks, and to nobody
/// else. Put it with an <see cref="Interactable"/> and a collider, and make the object a network object.
/// </remarks>
public sealed class StorageBox : Component, IInteractionHandler
{
	const string OptionId = "open_storage";

	/// <summary>How close, in world units, a player must be to this box for the host to move things for them.</summary>
	public const float Reach = 300f;

	/// <summary>How many slots wide the box is.</summary>
	[Property, Range( 1, StorageRules.MaxColumns )]
	public int Columns { get; set; } = 6;

	/// <summary>How many slots high the box is.</summary>
	[Property, Range( 1, StorageRules.MaxRows )]
	public int Rows { get; set; } = 4;

	/// <summary>The number of slots.</summary>
	public int Capacity =>
		System.Math.Clamp( Columns, 1, StorageRules.MaxColumns ) * System.Math.Clamp( Rows, 1, StorageRules.MaxRows );

	/// <summary>
	/// The Steam id of the player this box belongs to, or 0 while nobody has claimed it. Set by the host.
	/// </summary>
	[Sync( SyncFlags.FromHost )]
	public ulong OwnerId { get; set; }

	/// <summary>
	/// The id a player's save uses for this box: its object's id, which is stable in the scene.
	/// </summary>
	public string BoxId => GameObject.Id.ToString();

	// -- What this machine knows of the contents: only the player who asked is sent them ------------------------------

	ItemData[] view = [];

	/// <summary>
	/// Whether the host has answered since the panel opened.
	/// </summary>
	public bool ViewReady { get; private set; }

	/// <summary>
	/// Bumps whenever the host sends the contents, so the panel redraws.
	/// </summary>
	public int ViewVersion { get; private set; }

	/// <summary>
	/// The item in a slot as the host last told this player, or null. Empty before the host has answered.
	/// </summary>
	public ItemData ViewAt( int slot ) => slot >= 0 && slot < view.Length ? view[slot] : null;

	/// <summary>
	/// How many items the host last said are in the box.
	/// </summary>
	public int ViewCount => view.Count( x => x is not null );

	/// <summary>
	/// Forget the contents (the panel closed), so nothing stays in memory on this machine.
	/// </summary>
	public void ClearView()
	{
		view = [];
		ViewReady = false;
		ViewVersion++;
	}

	protected override void OnStart()
	{
		// A restart forgets who a box belongs to; the host reads it back, so a player who is away keeps theirs.
		if ( Networking.IsHost && Game.IsPlaying && OwnerId == 0 )
			OwnerId = StorageRegistry.OwnerOf( BoxId );

		var option = InteractionSetup.EnsureOption(
			this, OptionId, "Open storage", "inventory", "Keep things here when your bag is full." );

		// The press only opens the panel on the player's own screen.
		if ( option is not null )
			option.RunOn = InteractionSide.Local;
	}

	/// <summary>
	/// Whether a position is within <see cref="Reach"/> of this box.
	/// </summary>
	public bool IsNear( Vector3 position ) => WorldPosition.Distance( position ) <= Reach;

	void IInteractionHandler.OnInteract( InteractionEvent e )
	{
		if ( e.Option?.Id == OptionId && !Application.IsHeadless )
			StorageMenu.Open( this );
	}

	// -- Requests from the player's client ----------------------------------------------------------------------------

	/// <summary>
	/// The panel opened or wants the contents again. The first player to ask claims an unclaimed box. The answer goes
	/// to the caller alone.
	/// </summary>
	[Rpc.Host]
	public void RequestRefresh() => _ = Ask( who => CrimeResult.Success(), moves: false );

	/// <summary>
	/// Move the item in a bag slot to the first free slot of the box.
	/// </summary>
	[Rpc.Host]
	public void RequestStore( int bagSlot ) =>
		_ = Ask( who => StorageRules.Store( who.Keeper.Crime, BoxId, Capacity, who.Bag, bagSlot ) );

	/// <summary>
	/// Move the item in a slot of the box to the first free bag slot.
	/// </summary>
	[Rpc.Host]
	public void RequestTake( int boxSlot ) =>
		_ = Ask( who => StorageRules.Take( who.Keeper.Crime, BoxId, who.Bag, boxSlot ) );

	/// <summary>
	/// Move the item in a slot of the box to another slot of the box, swapping with what is there.
	/// </summary>
	[Rpc.Host]
	public void RequestMoveInBox( int from, int to ) =>
		_ = Ask( who => StorageRules.MoveInBox( who.Keeper.Crime, BoxId, Capacity, from, to ) );

	/// <summary>
	/// Move the item in a bag slot to a chosen slot of the box (a drag). An item there goes to the freed bag slot.
	/// </summary>
	[Rpc.Host]
	public void RequestStoreTo( int bagSlot, int boxSlot ) =>
		_ = Ask( who => StorageRules.StoreTo( who.Keeper.Crime, BoxId, Capacity, who.Bag, bagSlot, boxSlot ) );

	/// <summary>
	/// Move the item in a slot of the box to a chosen bag slot (a drag). An item there goes to the freed box slot.
	/// </summary>
	[Rpc.Host]
	public void RequestTakeTo( int boxSlot, int bagSlot ) =>
		_ = Ask( who => StorageRules.TakeTo( who.Keeper.Crime, BoxId, who.Bag, boxSlot, bagSlot ) );

	/// <summary>
	/// Move the item in a bag slot to another bag slot (a drag inside the panel's bag), swapping with what is there.
	/// Asked here, not of the inventory, so the box's reach check applies while the panel is open.
	/// </summary>
	[Rpc.Host]
	public void RequestMoveInBag( int from, int to ) =>
		_ = Ask( who => StorageRules.MoveInBag( who.Bag, from, to ) );

	/// <summary>
	/// Combine or split with a drag (<see cref="StackRules"/>), between any two slots of the bag and the box. Sides are
	/// <see cref="StorageRules.SideBag"/> (0) and <see cref="StorageRules.SideBox"/> (1); the mode is a
	/// <see cref="StackMode"/> (1 combine, 2 split, 3 both). The host checks all of it, so only indices travel.
	/// </summary>
	[Rpc.Host]
	public void RequestStack( int fromSide, int from, int toSide, int to, int mode )
	{
		if ( !StackRules.IsMode( mode ) )
			return;

		_ = Ask( who => StorageRules.Stack(
			who.Keeper.Crime, BoxId, Capacity, who.Bag, fromSide, from, toSide, to, (StackMode)mode ) );
	}

	/// <summary>
	/// Who is asking, and the record the box's contents are in: the owner's, never the asker's.
	/// </summary>
	sealed record Asker( Connection Caller, PlayerCrime Player, Keeper Keeper )
	{
		public PlayerInventory Bag => Player.Components.Get<PlayerInventory>();
	}

	/// <summary>
	/// The record a box's contents live in, and how a change to it is kept: by the owner's own player object when they
	/// are here, else written here. Host only.
	/// </summary>
	sealed record Keeper( ulong SteamId, CrimeRecord Crime, PlayerProgress Live )
	{
		/// <summary>
		/// Say the record changed, so it is saved: the owner's player object does it when they are here, else it is
		/// written here and let go of when nothing else waits on it.
		/// </summary>
		public void Changed()
		{
			if ( Live is { Loaded: true } progress )
			{
				progress.NotifyChanged( ProgressChange.Crime );
				return;
			}

			_ = SaveAsync();
		}

		async Task SaveAsync()
		{
			try
			{
				await PlayerProgression.SaveAsync( SteamId );

				// Nothing of theirs waits on the clock: let the record go. The processor keeps it when something does.
				if ( PlayerProgression.Cache.Peek( SteamId ) is { } record && CrimeRules.NextDue( record ) is null )
					PlayerProgression.Release( SteamId );
			}
			catch ( System.Exception e )
			{
				Log.Warning( e, $"StorageBox: couldn't save the record of {SteamId}: {e.Message}" );
			}
		}
	}

	/// <summary>
	/// Run one request: check who asks, run the rule for them, and answer them alone.
	/// </summary>
	/// <param name="rule">What the request does, once the host has said who is asking.</param>
	/// <param name="moves">Whether it can change the box. A refresh only reads it, so nothing is saved.</param>
	async Task Ask( System.Func<Asker, CrimeResult> rule, bool moves = true )
	{
		try
		{
			if ( await Begin() is { } who )
				Reply( who, rule( who ), moves );
		}
		catch ( System.Exception e )
		{
			Log.Warning( e, $"StorageBox: a request failed: {e.Message}" );
		}
	}

	/// <summary>
	/// The player behind the running RPC and the record the box's contents are in, or null when they may not do this:
	/// their record is not loaded or they are not within reach of this box. Anyone may use any box; the first player
	/// to open one claims it, so that its contents have a record to live in. Host only.
	/// </summary>
	async Task<Asker> Begin()
	{
		var caller = Rpc.Caller ?? Connection.Local;

		if ( !Networking.IsHost || caller is null || PlayerCrime.For( caller ) is not { Ready: true } player )
			return null;

		if ( !IsNear( player.WorldPosition ) )
		{
			player.Tell( CrimeResult.Fail( "Go to the storage box." ) );
			return null;
		}

		if ( OwnerId == 0 )
		{
			var steamId = (ulong)caller.SteamId;

			OwnerId = StorageRegistry.Claim( BoxId, steamId ) ? steamId : StorageRegistry.OwnerOf( BoxId );
		}

		if ( OwnerId == 0 || await RecordOf( OwnerId ) is not { } crime )
		{
			player.Tell( CrimeResult.Fail( "The storage box cannot be opened right now." ) );
			SendView( caller, false, "" );
			return null;
		}

		return new Asker( caller, player, new Keeper( OwnerId, crime, Here( OwnerId ) ) );
	}

	/// <summary>
	/// The record of the player a box belongs to, where its contents live: theirs when they are here, else the copy the
	/// host has in memory, else read from the store (they are away). Null when it cannot be read. Host only.
	/// </summary>
	async Task<CrimeRecord> RecordOf( ulong steamId )
	{
		if ( Here( steamId ) is { Loaded: true } progress && progress.Record is { } live )
			return live.Crime;

		if ( PlayerProgression.Cache.Peek( steamId ) is { } held )
			return held.Crime;

		try
		{
			var record = await PlayerProgression.LoadAsync( steamId );

			if ( !this.IsValid() || !Networking.IsHost )
				return null;

			return record?.Crime;
		}
		catch ( System.Exception e )
		{
			Log.Warning( e, $"StorageBox: couldn't read the record of {steamId}: {e.Message}" );
			return null;
		}
	}

	/// <summary>
	/// The player object of a player who is in the game, or null when they are away. Host only.
	/// </summary>
	static PlayerProgress Here( ulong steamId ) =>
		Connection.All.FirstOrDefault( x => x.SteamId == steamId ) is { } connection
			? PlayerProgress.For( connection )
			: null;

	/// <summary>
	/// Save what a request did (when it could change the box), say how it went, and send the contents back to the
	/// caller alone.
	/// </summary>
	void Reply( Asker who, CrimeResult result, bool moves )
	{
		if ( result.Ok && moves )
			who.Keeper.Changed();

		who.Player.Tell( result );
		SendView( who.Caller, true, Json.Serialize( StorageRules.View( who.Keeper.Crime, BoxId, Capacity ) ) );
	}

	void SendView( Connection caller, bool allowed, string json )
	{
		using ( Rpc.FilterInclude( caller ) )
			ReceiveView( allowed, json );
	}

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	void ReceiveView( bool allowed, string json )
	{
		view = allowed && !string.IsNullOrEmpty( json ) ? Json.Deserialize<ItemData[]>( json ) ?? [] : [];
		ViewReady = allowed;
		ViewVersion++;

		// Told it cannot be opened: the panel closes itself.
		if ( !allowed )
			StorageMenu.Refused( this );
	}
}
