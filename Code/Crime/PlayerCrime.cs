namespace Sandbox;

/// <summary>
/// A player's part in the crime game, on the player object: grow, press, pack, send, get paid. Every rule runs on the
/// host, on the state in the player's <see cref="CrimeRecord"/> (saved with their <see cref="ProgressRecord"/> through
/// <see cref="PlayerProgress"/>). The things in the world (<see cref="GrowTray"/>, <see cref="PressBench"/>,
/// <see cref="MailBox"/>) and the Hush app only ask: they pass ids, never a price, count or quality, and the host checks
/// everything.
/// </summary>
/// <remarks>
/// <see cref="PlayerProgress"/> creates this component on the host if the player prefab doesn't have it, and refreshes
/// the network object so clients get it. Clients only read <see cref="TrayView"/>, to label the grow trays.
/// </remarks>
public sealed class PlayerCrime : Component, Global.IInventoryEvents
{
	/// <summary>
	/// This player's trays as "id,planted,readyAt;…", set by the host, for the labels of the trays in the world.
	/// </summary>
	[Sync( SyncFlags.FromHost ), Change( nameof( OnTrayViewChanged ) )]
	public string TrayView { get; private set; } = "";

	/// <summary>
	/// Bumps whenever <see cref="TrayView"/> changes, on every machine.
	/// </summary>
	public int Version { get; private set; }

	/// <summary>
	/// The crime state of the player on this computer, or null before it has arrived.
	/// </summary>
	public static PlayerCrime Local => Players.Local<PlayerCrime>();

	/// <summary>
	/// The crime state of the player on this connection, or null.
	/// </summary>
	public static PlayerCrime For( Connection connection ) => Players.For<PlayerCrime>( connection );

	PlayerProgress Progress => field ??= Components.Get<PlayerProgress>();

	PlayerWallet Wallet => field ??= Components.Get<PlayerWallet>();

	PlayerInventory Inventory => field ??= Components.Get<PlayerInventory>();

	/// <summary>
	/// The player's crime record, or null off the host and before the progress has loaded.
	/// </summary>
	CrimeRecord Crime => Networking.IsHost && Progress is { Loaded: true } ? Progress.Record?.Crime : null;

	/// <summary>
	/// Whether the player's state is ready for the rules to run on the host.
	/// </summary>
	public bool Ready => Crime is not null && Wallet is not null && Inventory is not null;

	// Host-only views of the record, for the Hush app.
	public IReadOnlyList<OrderRecord> Orders => Crime?.Orders ?? [];
	public IReadOnlyList<ParcelRecord> Parcels => Crime?.Parcels ?? [];
	public IReadOnlyList<CashoutRecord> Cashouts => Crime?.Cashouts ?? [];
	public IReadOnlyList<ReviewRecord> Reviews => Crime?.Reviews ?? [];
	public int GoodTrades => Crime?.GoodTrades ?? 0;
	public int BadTrades => Crime?.BadTrades ?? 0;
	public ReputationLevel Level => Crime is { } crime ? CrimeCatalog.LevelOf( crime ) : ReputationLevel.Newcomer;
	public int FeePercent => Crime is { } crime ? CrimeCatalog.FeeFor( crime ) : CrimeCatalog.FeeNewcomer;

	/// <summary>
	/// The average of all the stars the player has been given, or 0 for none yet.
	/// </summary>
	public double Rating => Crime is { RatingCount: > 0 } crime ? crime.RatingSum / (double)crime.RatingCount : 0;

	readonly System.Random random = new();

	RealTimeSince sinceTick;
	bool restored;
	bool restoring;

	void OnTrayViewChanged( string oldValue, string newValue ) => Version++;

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost || !Ready )
			return;

		if ( !restored )
			Restore();

		if ( sinceTick > 1 )
		{
			sinceTick = 0;
			Tick();
		}
	}

	/// <summary>
	/// A copy of the crime state changed: show it, save it soon and tell the scene.
	/// </summary>
	void Changed()
	{
		var crime = Crime;

		if ( crime is null )
			return;

		TrayView = string.Join( ';', crime.Trays.Select( x => $"{x.Id},{(x.Planted ? 1 : 0)},{x.ReadyAt}" ) );
		Progress.NotifyChanged( ProgressChange.Crime );
	}

	// -- Inventory: the batches survive a restart by being copied into the record ------------------------------------

	/// <summary>
	/// Put the saved crime items back in the inventory, once, and give a new player their starting stock.
	/// </summary>
	void Restore()
	{
		var crime = Crime;
		restored = true;
		restoring = true;

		try
		{
			foreach ( var item in crime.Items.ToList() )
				Inventory.Add( item.Clone() );

			if ( !crime.Started )
			{
				crime.Started = true;
				Inventory.AddItem( ItemCatalog.StaticCulture, CrimeCatalog.StartCultures, 50 );
			}
		}
		finally
		{
			restoring = false;
		}

		SaveItems();
		Changed();
	}

	void Global.IInventoryEvents.OnInventoryChanged( PlayerInventory inventory )
	{
		if ( Networking.IsHost && restored && !restoring && inventory == Inventory )
			SaveItems();
	}

	/// <summary>
	/// Copy the batches in the inventory into the record. Other items are not saved.
	/// </summary>
	void SaveItems()
	{
		var crime = Crime;

		if ( crime is null )
			return;

		var items = new List<ItemData>();

		for ( var i = 0; i < Inventory.SlotCount; i++ )
		{
			if ( Inventory.At( i ) is { IsBatch: true } item )
				items.Add( item.Clone() );
		}

		crime.Items = items;
		Progress.NotifyChanged( ProgressChange.Crime );
	}

	/// <summary>
	/// Say the result of an action to this player alone, in their chat. Host only.
	/// </summary>
	public void Tell( CrimeResult result )
	{
		if ( Networking.IsHost && !string.IsNullOrEmpty( result.Message ) )
			Say( result.Message );
	}

	[Rpc.Owner]
	void Say( string text ) => Sandbox.Platform.Chat.AddText( text );

	// -- Growing and pressing ----------------------------------------------------------------------------------------

	static bool IsId( string id ) => !string.IsNullOrWhiteSpace( id ) && id.Length <= CrimeRecord.MaxIdLength
		&& id.All( x => char.IsAsciiLetterOrDigit( x ) || x is '-' or '_' );

	TrayRecord TrayAt( string id ) => Crime?.Trays.FirstOrDefault( x => x.Id == id );

	/// <summary>
	/// What a tray holds for this player, from the synced view, so it works on a client too: whether it is planted, and
	/// the game time it is ready.
	/// </summary>
	public (bool Planted, long ReadyAt) TrayState( string id )
	{
		foreach ( var part in (TrayView ?? "").Split( ';', System.StringSplitOptions.RemoveEmptyEntries ) )
		{
			var fields = part.Split( ',' );

			if ( fields.Length == 3 && fields[0] == id && long.TryParse( fields[2], out var readyAt ) )
				return (fields[1] == "1", readyAt);
		}

		return (false, 0);
	}

	/// <summary>
	/// Plant a culture in a tray: takes one <c>static_culture</c> from the inventory. The tray is ready after
	/// <see cref="CrimeCatalog.GrowSeconds"/>. Host only.
	/// </summary>
	public CrimeResult Plant( string trayId )
	{
		if ( !Ready || !IsId( trayId ) )
			return CrimeResult.Fail( "Nothing happens." );

		var crime = Crime;
		var tray = TrayAt( trayId );

		if ( tray is { Planted: true } )
			return CrimeResult.Fail( "Something is already growing here." );

		if ( tray is null && crime.Trays.Count >= CrimeRecord.MaxTrays )
			return CrimeResult.Fail( "You have too many trays." );

		if ( !Inventory.TryTake( ItemCatalog.StaticCulture, 1, out _ ) )
			return CrimeResult.Fail( "You need a Static culture." );

		if ( tray is null )
			crime.Trays.Add( tray = new() { Id = trayId } );

		tray.Planted = true;
		tray.ReadyAt = WorldClock.Now + CrimeCatalog.GrowSeconds;
		tray.Quality = random.Next( CrimeCatalog.GrowQualityMin, CrimeCatalog.GrowQualityMax + 1 );

		Changed();
		return CrimeResult.Success( "Planted." );
	}

	/// <summary>
	/// Harvest a ready tray: gives <c>static_raw</c> (and a culture back). Host only.
	/// </summary>
	public CrimeResult Harvest( string trayId )
	{
		if ( !Ready || !IsId( trayId ) || TrayAt( trayId ) is not { Planted: true } tray )
			return CrimeResult.Fail( "Nothing to harvest." );

		if ( WorldClock.Now < tray.ReadyAt )
			return CrimeResult.Fail( "It isn't ready yet." );

		if ( Inventory.FreeSlots < 2 )
			return CrimeResult.Fail( "Your inventory is full." );

		Inventory.AddItem( ItemCatalog.StaticRaw, CrimeCatalog.HarvestRaw, tray.Quality );
		Inventory.AddItem( ItemCatalog.StaticCulture, CrimeCatalog.HarvestCultures, 50 );
		tray.Planted = false;

		Changed();
		return CrimeResult.Success( $"Harvested {CrimeCatalog.HarvestRaw} raw Static." );
	}

	/// <summary>
	/// Press all the raw Static the player carries into tabs. The quality is a placeholder: the average of the raw, nudged
	/// at random (the Tuning minigame replaces it later). Host only.
	/// </summary>
	public CrimeResult Press()
	{
		if ( !Ready )
			return CrimeResult.Fail( "Nothing happens." );

		var raw = Inventory.CountOf( ItemCatalog.StaticRaw );

		if ( raw < 1 )
			return CrimeResult.Fail( "You have no raw Static to press." );

		if ( Inventory.FreeSlots < 1 )
			return CrimeResult.Fail( "Your inventory is full." );

		Inventory.TryTake( ItemCatalog.StaticRaw, raw, out var taken );

		var quality = AverageQuality( taken ) + random.Next( -CrimeCatalog.PressQualitySpread, CrimeCatalog.PressQualitySpread + 1 );
		var tabs = raw * CrimeCatalog.TabsPerRaw;

		Inventory.AddItem( ItemCatalog.StaticTab, tabs, quality );
		Changed();
		return CrimeResult.Success( $"Pressed {tabs} tabs." );
	}

	static int AverageQuality( List<ItemData> batches )
	{
		var total = batches.Sum( x => x.Count );
		return total == 0 ? 0 : (int)System.Math.Round( batches.Sum( x => (double)x.Quality * x.Count ) / total );
	}

	// -- Mailbox ------------------------------------------------------------------------------------------------------

	/// <summary>
	/// The mailbox this player has claimed, or null.
	/// </summary>
	public MailBox ClaimedMailBox()
	{
		var id = Crime?.MailboxId;

		return string.IsNullOrEmpty( id )
			? null
			: Scene.GetAll<MailBox>().FirstOrDefault( x => x.BoxId == id );
	}

	/// <summary>
	/// Send the first parcel the player carries from this mailbox. A mailbox is claimed by the first player who sends from
	/// it, and stays theirs; nobody else can use it. Host only.
	/// </summary>
	public CrimeResult SendParcel( MailBox box )
	{
		if ( !Ready || !box.IsValid() )
			return CrimeResult.Fail( "Nothing happens." );

		var crime = Crime;
		var steamId = Progress.Owner.SteamId;

		if ( box.OwnerId != 0 && box.OwnerId != steamId )
			return CrimeResult.Fail( "This isn't your mailbox." );

		if ( crime.MailboxId != "" && crime.MailboxId != box.BoxId )
			return CrimeResult.Fail( "You already use another mailbox." );

		var slot = -1;

		for ( var i = 0; i < Inventory.SlotCount && slot < 0; i++ )
		{
			if ( Inventory.At( i ) is { Kind: ItemCatalog.Parcel } )
				slot = i;
		}

		if ( slot < 0 )
			return CrimeResult.Fail( "You have no parcel to send." );

		if ( PostalSystem.InPost( crime ) >= CrimeCatalog.MaxParcels )
			return CrimeResult.Fail( "The post has too many of your parcels." );

		var item = Inventory.TakeBatch( slot );
		var order = crime.Orders.FirstOrDefault( x => x.Id == item.OrderId );

		// A parcel for an order that is gone (cancelled) is only a box. One for an order in progress ships it.
		if ( order is { State: OrderState.Packed } )
			order.State = OrderState.Shipped;

		box.OwnerId = steamId;
		crime.MailboxId = box.BoxId;

		var parcel = PostalSystem.Send( crime, order?.Id ?? "", order?.Kind ?? ItemCatalog.StaticTab, order?.Count ?? item.Count, item.Quality );
		UpdateFlag();
		Changed();

		return CrimeResult.Success( $"The mailman comes at {WorldClock.TimeOfDayOf( parcel.CollectAt )}." );
	}

	/// <summary>
	/// Put the flag of the player's mailbox up while a parcel waits for the mailman.
	/// </summary>
	void UpdateFlag()
	{
		if ( ClaimedMailBox() is { } box )
		{
			box.OwnerId = Progress.Owner.SteamId;
			box.HasMail = PostalSystem.HasWaiting( Crime );
		}
	}

	// -- Orders -------------------------------------------------------------------------------------------------------

	/// <summary>
	/// Accept an open order: it leaves the board and is reserved for this player, with its payment held in escrow. Host
	/// only.
	/// </summary>
	public CrimeResult Accept( string orderId )
	{
		if ( !Ready || !IsId( orderId ) || MarketSystem.Current is not { } market )
			return CrimeResult.Fail( "Nothing happens." );

		var crime = Crime;

		if ( crime.Orders.Any( x => x.Id == orderId ) )
			return CrimeResult.Fail( "You already took that order." );

		if ( crime.Orders.Count( IsActive ) >= CrimeCatalog.MaxActiveOrders )
			return CrimeResult.Fail( "You have too many open orders." );

		if ( market.TryTake( orderId ) is not { } offer )
			return CrimeResult.Fail( "That order is gone." );

		crime.Orders.Add( new OrderRecord
		{
			Id = offer.Id,
			Handle = offer.Handle,
			Buyer = offer.Buyer,
			Text = offer.Text,
			Kind = offer.Kind,
			Count = offer.Count,
			MinQuality = offer.MinQuality,
			PriceEach = offer.PriceEach,
			Deadline = WorldClock.Now + CrimeCatalog.DeliverWindowSeconds,
			State = OrderState.Accepted,
		} );

		if ( crime.Orders.Count > CrimeRecord.MaxEntries )
			crime.Orders.RemoveAt( 0 );

		Changed();
		return CrimeResult.Success( $"Accepted. {offer.Total:N0} crypto is held in escrow." );
	}

	/// <summary>
	/// Pack an accepted order: the host checks the inventory has enough of the right kind and quality, takes it, and gives a
	/// parcel that remembers the order. Host only.
	/// </summary>
	public CrimeResult Pack( string orderId )
	{
		if ( !Ready || !IsId( orderId ) || Crime.Orders.FirstOrDefault( x => x.Id == orderId ) is not { } order )
			return CrimeResult.Fail( "Nothing happens." );

		if ( order.State != OrderState.Accepted )
			return CrimeResult.Fail( "That order is already packed." );

		if ( WorldClock.Now > order.Deadline )
			return CrimeResult.Fail( "That order has run out of time." );

		if ( Inventory.CountOf( order.Kind, order.MinQuality ) < order.Count )
			return CrimeResult.Fail( $"You need {order.Count} of quality {order.MinQuality} or better." );

		if ( Inventory.FreeSlots < 1 )
			return CrimeResult.Fail( "Your inventory is full." );

		if ( !Inventory.TryTake( order.Kind, order.Count, out var taken, order.MinQuality ) )
			return CrimeResult.Fail( "You don't have enough." );

		var quality = AverageQuality( taken );

		Inventory.AddItem( ItemCatalog.Parcel, 1, quality, order.Id );

		order.State = OrderState.Packed;
		order.PackedQuality = quality;
		Changed();
		return CrimeResult.Success( "Packed. Take the parcel to your mailbox." );
	}

	static bool IsActive( OrderRecord order ) => order.State is OrderState.Accepted or OrderState.Packed
		or OrderState.Shipped or OrderState.Delivered;

	// -- The Exchange -------------------------------------------------------------------------------------------------

	/// <summary>
	/// Sell crypto for dollars through the <see cref="Exchange"/>. Host only.
	/// </summary>
	public CrimeResult SellCrypto( long crypto )
	{
		if ( !Ready )
			return CrimeResult.Fail( "Nothing happens." );

		var result = Exchange.TrySell( Wallet, Crime, crypto );

		if ( result.Ok )
			Changed();

		return result;
	}

	/// <summary>
	/// Buy crypto with dollars through the <see cref="Exchange"/>. Host only.
	/// </summary>
	public CrimeResult BuyCrypto( long cash )
	{
		if ( !Ready )
			return CrimeResult.Fail( "Nothing happens." );

		return Exchange.TryBuy( Wallet, cash );
	}

	// -- Time ---------------------------------------------------------------------------------------------------------

	/// <summary>
	/// Once a second on the host: the post, deadlines, escrow and the Exchange.
	/// </summary>
	void Tick()
	{
		var crime = Crime;
		var changed = PostalSystem.Tick( crime, OnParcelDelivered );

		changed |= CheckOrders( crime );
		changed |= Exchange.Settle( Wallet, crime );

		UpdateFlag();

		if ( changed )
			Changed();
	}

	/// <summary>
	/// A parcel arrived. If it was for an order in time, the buyer reviews it now and the payment is released after the
	/// escrow delay; if it came too late the order is dead.
	/// </summary>
	void OnParcelDelivered( ParcelRecord parcel )
	{
		var crime = Crime;
		var order = crime.Orders.FirstOrDefault( x => x.Id == parcel.OrderId );

		if ( order is not { State: OrderState.Shipped } )
			return;

		if ( parcel.DeliverAt > order.Deadline )
		{
			Cancel( order, "Your parcel came too late. The buyer left." );
			return;
		}

		order.State = OrderState.Delivered;
		order.ReleaseAt = WorldClock.Now + CrimeCatalog.EscrowSeconds;

		var stars = Stars( order );
		AddReview( order, stars, CrimeCatalog.PickReview( stars, random ) );
	}

	/// <summary>
	/// What the buyer thinks of the batch: how far above the quality they asked for it was. A picky buyer is harder.
	/// </summary>
	static int Stars( OrderRecord order )
	{
		var surplus = order.PackedQuality - order.MinQuality;

		return order.Buyer == BuyerType.Picky
			? surplus >= 20 ? 5 : surplus >= 10 ? 4 : surplus >= 5 ? 3 : 2
			: surplus >= 15 ? 5 : surplus >= 5 ? 4 : 3;
	}

	void AddReview( OrderRecord order, int stars, string text )
	{
		var crime = Crime;

		crime.Reviews.Add( new() { At = WorldClock.Now, Handle = order.Handle, Stars = stars, Text = text } );

		if ( crime.Reviews.Count > CrimeRecord.MaxReviews )
			crime.Reviews.RemoveAt( 0 );

		crime.RatingSum += stars;
		crime.RatingCount++;
	}

	/// <summary>
	/// Release escrow for delivered orders, and cancel the ones that ran out of time.
	/// </summary>
	bool CheckOrders( CrimeRecord crime )
	{
		var now = WorldClock.Now;
		var changed = false;

		foreach ( var order in crime.Orders.ToList() )
		{
			if ( order.State == OrderState.Delivered && now >= order.ReleaseAt )
				changed |= Release( crime, order );
			else if ( order.State is OrderState.Accepted or OrderState.Packed or OrderState.Shipped && now > order.Deadline )
			{
				Cancel( order, "The deadline passed. The buyer cancelled the order." );
				changed = true;
			}
		}

		return changed;
	}

	/// <summary>
	/// Pay the player what escrow held, less the reputation fee.
	/// </summary>
	bool Release( CrimeRecord crime, OrderRecord order )
	{
		var gross = order.Total;
		var fee = gross * CrimeCatalog.FeeFor( crime ) / 100;
		var paid = gross - fee;

		if ( !Wallet.CanHoldCrypto( paid ) )
			return false;

		Wallet.AddCrypto( paid );
		order.Paid = paid;
		order.State = OrderState.Released;
		crime.GoodTrades++;
		return true;
	}

	/// <summary>
	/// The order is dead: it costs reputation (a bad trade and a one star review), and a parcel still in the player's bag is
	/// taken, since nobody wants it.
	/// </summary>
	void Cancel( OrderRecord order, string reason )
	{
		var crime = Crime;

		order.State = OrderState.Cancelled;
		crime.BadTrades++;
		AddReview( order, CrimeCatalog.CancelStars, CrimeCatalog.PickReview( CrimeCatalog.CancelStars, random ) );
		Log.Info( $"PlayerCrime: order {order.Id} cancelled: {reason}" );

		for ( var i = 0; i < Inventory.SlotCount; i++ )
		{
			if ( Inventory.At( i ) is { Kind: ItemCatalog.Parcel } parcel && parcel.OrderId == order.Id )
				Inventory.TakeBatch( i );
		}
	}

	// -- Testing ------------------------------------------------------------------------------------------------------

	/// <summary>
	/// Testing only: the player asks the host for items. The host refuses unless it is the editor, and unless the caller is
	/// this player.
	/// </summary>
	[Rpc.Host]
	public void DebugGive( string kind, int count, int quality )
	{
		if ( IsDebugCaller() && Ready )
			Inventory.AddItem( kind, count, quality );
	}

	/// <summary>
	/// Testing only: every planted tray is ready now.
	/// </summary>
	[Rpc.Host]
	public void DebugTraysReady()
	{
		if ( !IsDebugCaller() || !Ready )
			return;

		foreach ( var tray in Crime.Trays.Where( x => x.Planted ) )
			tray.ReadyAt = WorldClock.Now;

		Changed();
	}

	/// <summary>
	/// Testing only: the mailman, the buyers' escrow and the Exchange are all due now.
	/// </summary>
	[Rpc.Host]
	public void DebugPostalNow()
	{
		if ( !IsDebugCaller() || !Ready )
			return;

		var now = WorldClock.Now;
		var crime = Crime;

		foreach ( var parcel in crime.Parcels.Where( x => x.State != ParcelState.Delivered ) )
		{
			parcel.CollectAt = System.Math.Min( parcel.CollectAt, now );
			parcel.DeliverAt = System.Math.Min( parcel.DeliverAt, now );
		}

		foreach ( var order in crime.Orders.Where( x => x.State == OrderState.Delivered ) )
			order.ReleaseAt = now;

		foreach ( var cashout in crime.Cashouts )
			cashout.ReadyAt = now;

		Changed();
	}

	/// <summary>
	/// Testing only: put orders on the board.
	/// </summary>
	[Rpc.Host]
	public void DebugOrders( int count )
	{
		if ( IsDebugCaller() )
			MarketSystem.Current?.Fill( System.Math.Clamp( count, 0, 10 ) );
	}

	bool IsDebugCaller() => Application.IsEditor && Rpc.Caller == (Network.Owner ?? Connection.Local);
}
