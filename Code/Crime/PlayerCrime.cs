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
/// the network object so clients get it. The views below (<see cref="TrayView"/>, <see cref="PackOrders"/>,
/// <see cref="MailWaiting"/>) are not synced to everyone: the host keeps them, and sends them to the owning player
/// alone (<see cref="RefreshViews"/>), so another player's client never holds a player's trays, orders or deliveries.
/// </remarks>
public sealed class PlayerCrime : Component, Global.IInventoryEvents
{
	/// <summary>
	/// This player's trays as "id,planted,readyAt,product;…", for the labels of the trays in the world. Held by the
	/// host and by this player's own client only.
	/// </summary>
	public string TrayView { get; private set; } = "";

	/// <summary>
	/// The orders this player can pack now (accepted, in time) as "id|handle|kind|count|asked|deadline;…", for the
	/// packing table and its panel. Only what the player already sees in Hush: never a buyer's personality. Held by the
	/// host and by this player's own client only.
	/// </summary>
	public string PackOrders { get; private set; } = "";

	/// <summary>
	/// How many deliveries from the shop wait in the player's mailbox, for the label of its Collect option. Held by the
	/// host and by this player's own client only.
	/// </summary>
	public int MailWaiting { get; private set; }

	/// <summary>
	/// The last result told to this player by <see cref="Tell"/>, with a counter that goes up each time, for the
	/// packing panel. Set on the player's own machine.
	/// </summary>
	public string LastTold { get; private set; } = "";

	public int ToldCount { get; private set; }

	/// <summary>
	/// Bumps whenever one of the views changes, on every machine that holds them.
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
	public IReadOnlyList<DeliveryRecord> Deliveries => Crime?.Deliveries ?? [];
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

	protected override void OnStart()
	{
		// A client that arrives (or respawns) asks the host for its views; the host sends them when they change.
		if ( !Networking.IsHost && !IsProxy )
			RequestViews();
	}

	/// <summary>
	/// Take new views here, and bump <see cref="Version"/> so panels redraw.
	/// </summary>
	void ApplyViews( string trays, string packOrders, int mailWaiting )
	{
		TrayView = trays ?? "";
		PackOrders = packOrders ?? "";
		MailWaiting = mailWaiting;
		Version++;
	}

	/// <summary>
	/// The host's views reach the owning player here, and nobody else.
	/// </summary>
	[Rpc.Owner]
	void SendViews( string trays, string packOrders, int mailWaiting )
	{
		if ( Rpc.Caller is { IsHost: false } )
			return;

		ApplyViews( trays, packOrders, mailWaiting );
	}

	/// <summary>
	/// The owning player's client asks for its views, once when it arrives. The host answers the caller alone, and only
	/// if the caller is the owner.
	/// </summary>
	[Rpc.Host]
	void RequestViews()
	{
		if ( Rpc.Caller is { } caller && caller == Network.Owner && caller != Connection.Local )
			SendViews( TrayView, PackOrders, MailWaiting );
	}

	/// <summary>
	/// Work out the views from the record. If they changed, keep them here (the host reads them too) and send them to
	/// the owning player when that is another machine. Host only.
	/// </summary>
	void RefreshViews()
	{
		if ( !Ready )
			return;

		var crime = Crime;
		var now = WorldClock.Now;

		var trays = string.Join( ';', crime.Trays.Select(
			x => $"{x.Id},{(x.Planted ? 1 : 0)},{x.ReadyAt},{ProductCatalog.OfTray( x.Kind ).Id}" ) );

		var packOrders = string.Join( ';', crime.Orders
			.Where( x => x.State == OrderState.Accepted && now <= x.Deadline )
			.OrderBy( x => x.Deadline )
			.Select( x => $"{x.Id}|{x.Handle}|{x.Kind}|{x.Count}|{x.MinQuality}|{x.Deadline}" ) );

		var mailWaiting = crime.Deliveries.Count( x => x.State == DeliveryState.InMailbox );

		if ( trays == TrayView && packOrders == PackOrders && mailWaiting == MailWaiting )
			return;

		ApplyViews( trays, packOrders, mailWaiting );

		if ( Network.Owner is { } owner && owner != Connection.Local )
			SendViews( trays, packOrders, mailWaiting );
	}

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
	void Changed( ProgressChange also = ProgressChange.None )
	{
		var crime = Crime;

		if ( crime is null )
			return;

		RefreshViews();
		Progress.NotifyChanged( ProgressChange.Crime | also );
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
			// A stack above its kind's size (an old save) goes into as many slots as it takes, one stack at a time so
			// that what fits is kept when the bag is too small for all of it.
			foreach ( var item in crime.Items.ToList() )
			{
				var left = item.Count;

				while ( left > 0 )
				{
					var piece = item.Clone();
					piece.Count = System.Math.Min( left, item.MaxStack );

					if ( !Inventory.Add( piece ) )
						break;

					left -= piece.Count;
				}

				if ( left > 0 )
					Log.Warning( $"PlayerCrime: no room in the bag to restore {left} x {item.Kind}." );
			}

			if ( !crime.Started )
			{
				crime.Started = true;

				foreach ( var product in ProductCatalog.Products.Where( x => x.StartSeeds > 0 ) )
					Inventory.AddItem( product.Seed.Id, product.StartSeeds, 50 );
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
		if ( !Networking.IsHost || !restored || restoring || inventory != Inventory )
			return;

		SaveItems();
		RefreshViews();
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
	void Say( string text )
	{
		Sandbox.Platform.Chat.AddText( text );
		LastTold = text;
		ToldCount++;
	}

	// -- Growing and pressing ----------------------------------------------------------------------------------------

	static bool IsId( string id ) => !string.IsNullOrWhiteSpace( id ) && id.Length <= CrimeRecord.MaxIdLength
		&& id.All( x => char.IsAsciiLetterOrDigit( x ) || x is '-' or '_' );

	TrayRecord TrayAt( string id ) => Crime?.Trays.FirstOrDefault( x => x.Id == id );

	/// <summary>
	/// What a tray holds for this player, from the view the host sent, so it works on a client too: whether it is
	/// planted, the game time it is ready, and the drug planted (<see cref="ProductDef.Id"/>; Static for a tray with
	/// none).
	/// </summary>
	public (bool Planted, long ReadyAt, string Kind) TrayState( string id )
	{
		foreach ( var part in (TrayView ?? "").Split( ';', System.StringSplitOptions.RemoveEmptyEntries ) )
		{
			var fields = part.Split( ',' );

			if ( fields.Length is 3 or 4 && fields[0] == id && long.TryParse( fields[2], out var readyAt ) )
				return (fields[1] == "1", readyAt, ProductCatalog.OfTray( fields.Length == 4 ? fields[3] : "" ).Id);
		}

		return (false, 0, "");
	}

	/// <summary>
	/// The player asks to plant a drug in a tray. The host takes the player from the network (<c>Rpc.Caller</c>),
	/// checks the player is within reach of that tray, and runs <see cref="Plant"/>, which checks the rest. Only the
	/// ids of the tray and the drug are sent.
	/// </summary>
	[Rpc.Host]
	public void RequestPlant( string trayId, string productId )
	{
		var caller = Rpc.Caller;

		if ( caller is not null && caller != Network.Owner )
			return;

		if ( !GrowTray.IsNear( trayId, WorldPosition ) )
		{
			Tell( CrimeResult.Fail( "Go to the grow tray." ) );
			return;
		}

		Tell( Plant( trayId, productId ) );
	}

	/// <summary>
	/// Plant a drug in a tray: takes one of its seeds from the inventory. The tray is ready after the drug's grow time.
	/// The host checks the drug exists, the tray is empty and the seed is in the bag. Host only.
	/// </summary>
	public CrimeResult Plant( string trayId, string productId )
	{
		if ( !Ready || !IsId( trayId ) || ProductCatalog.Find( productId ) is not { } product )
			return CrimeResult.Fail( "Nothing happens." );

		var crime = Crime;
		var tray = TrayAt( trayId );

		if ( tray is { Planted: true } )
			return CrimeResult.Fail( "Something is already growing here." );

		if ( tray is null && crime.Trays.Count >= CrimeRecord.MaxTrays )
			return CrimeResult.Fail( "You have too many trays." );

		if ( !Inventory.TryTake( product.Seed.Id, 1, out _ ) )
			return CrimeResult.Fail( $"You need a {product.Seed.Name}." );

		if ( tray is null )
			crime.Trays.Add( tray = new() { Id = trayId } );

		tray.Kind = product.Id;
		tray.Planted = true;
		tray.ReadyAt = WorldClock.Now + product.GrowSeconds;
		tray.Quality = random.Next( product.CropQualityMin, product.CropQualityMax + 1 );

		Changed();
		return CrimeResult.Success( $"Planted {product.Name}." );
	}

	/// <summary>
	/// Harvest a ready tray: gives the planted drug's raw (and its seed back). Host only.
	/// </summary>
	public CrimeResult Harvest( string trayId )
	{
		if ( !Ready || !IsId( trayId ) || TrayAt( trayId ) is not { Planted: true } tray )
			return CrimeResult.Fail( "Nothing to harvest." );

		if ( WorldClock.Now < tray.ReadyAt )
			return CrimeResult.Fail( "It isn't ready yet." );

		var product = ProductCatalog.OfTray( tray.Kind );

		if ( Inventory.FreeSlots < ItemCatalog.SlotsFor( product.Raw.Id, product.HarvestRaw )
		     + ItemCatalog.SlotsFor( product.Seed.Id, product.HarvestSeeds ) )
			return CrimeResult.Fail( "Your inventory is full." );

		Inventory.AddItem( product.Raw.Id, product.HarvestRaw, tray.Quality );
		Inventory.AddItem( product.Seed.Id, product.HarvestSeeds, 50 );
		tray.Planted = false;

		Changed();
		return CrimeResult.Success( $"Harvested {product.HarvestRaw} {product.Raw.Name}." );
	}

	/// <summary>
	/// The player asks to press the raw in an inventory slot. The host takes the player from the network
	/// (<c>Rpc.Caller</c>), checks the player is at a press bench, and runs <see cref="Press"/>, which checks the rest.
	/// Only the slot index is sent.
	/// </summary>
	[Rpc.Host]
	public void RequestPress( int slot )
	{
		var caller = Rpc.Caller;

		if ( caller is not null && caller != Network.Owner )
			return;

		if ( !PressBench.IsNear( WorldPosition ) )
		{
			Tell( CrimeResult.Fail( "Go to a press bench." ) );
			return;
		}

		Tell( Press( slot ) );
	}

	/// <summary>
	/// Press the raw in an inventory slot into its drug's product: as many products as the batch holds whole multiples
	/// of the drug's raw per product (an odd leftover raw stays in the bag). The quality is a placeholder: the batch's,
	/// nudged at random (the Tuning minigame replaces it later). The host checks the slot holds raw of a known drug.
	/// Host only.
	/// </summary>
	public CrimeResult Press( int slot )
	{
		if ( !Ready )
			return CrimeResult.Fail( "Nothing happens." );

		if ( Inventory.At( slot ) is not { IsBatch: true } batch
		     || ProductCatalog.OfRaw( batch.Kind ) is not { } product )
			return CrimeResult.Fail( "That can't be pressed." );

		var made = batch.Count / product.RawPerProduct;

		if ( made < 1 )
		{
			return CrimeResult.Fail(
				$"You need {product.RawPerProduct} {product.Raw.Name} for one {product.Product.Name}." );
		}

		var used = made * product.RawPerProduct;

		// The product needs its slots, one more of them free if the raw is used up and frees its own.
		if ( Inventory.FreeSlots + (used == batch.Count ? 1 : 0) < ItemCatalog.SlotsFor( product.Product.Id, made ) )
			return CrimeResult.Fail( "Your inventory is full." );

		if ( Inventory.TakeFrom( slot, used ) is not { } taken )
			return CrimeResult.Fail( "Nothing happens." );

		var spread = product.PressQualitySpread;
		var quality = taken.Quality + random.Next( -spread, spread + 1 );

		Inventory.AddItem( product.Product.Id, made, quality );
		Changed();
		return CrimeResult.Success( $"Pressed {made} {product.Product.Name}." );
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
	/// Why this player may not use this mailbox, or null if they may: it is another player's, or they already use
	/// another one. A claim on a mailbox that is no longer in the scene is let go. Host only.
	/// </summary>
	string CheckMailbox( MailBox box, out ulong steamId )
	{
		steamId = Progress.Owner.SteamId;

		if ( box.OwnerId != 0 && box.OwnerId != steamId )
			return "This isn't your mailbox.";

		if ( Crime.MailboxId != "" && Crime.MailboxId != box.BoxId && ClaimedMailBox() is not null )
			return "You already use another mailbox.";

		return null;
	}

	/// <summary>
	/// Take what the shop delivered out of the mailbox, as much as the bag holds; the rest stays. The first of send or
	/// collect to be used claims an unclaimed mailbox. Host only.
	/// </summary>
	public CrimeResult CollectParcels( MailBox box )
	{
		if ( !Ready || !box.IsValid() )
			return CrimeResult.Fail( "Nothing happens." );

		if ( CheckMailbox( box, out var steamId ) is { } error )
			return CrimeResult.Fail( error );

		var crime = Crime;
		var waiting = crime.Deliveries.Where( x => x.State == DeliveryState.InMailbox ).ToList();

		if ( waiting.Count == 0 )
			return CrimeResult.Fail( "Nothing waiting for you." );

		box.OwnerId = steamId;
		crime.MailboxId = box.BoxId;
		MailboxRegistry.Claim( box.BoxId, steamId );

		var got = new List<string>();

		foreach ( var delivery in waiting )
		{
			if ( !Inventory.AddItem( delivery.Kind, delivery.Count, delivery.Quality ) )
				continue;

			crime.Deliveries.Remove( delivery );
			got.Add( $"{delivery.Count} {ItemCatalog.Find( delivery.Kind )?.Name ?? delivery.Kind}" );
		}

		Changed();
		var left = waiting.Count - got.Count;

		if ( got.Count == 0 )
			return CrimeResult.Fail( "Your bag is full. Make room first." );

		var text = $"Collected {string.Join( ", ", got )}.";
		return CrimeResult.Success( left > 0 ? $"{text} Your bag is full: {Plural( left )} still waiting." : text );
	}

	/// <summary>
	/// Buy from the Hush shop with crypto: an item id from the catalogue and a quantity. The price and the delivery
	/// time come from <see cref="CrimeCatalog"/>, never from the caller. The goods go on their way to the mailbox. Host
	/// only.
	/// </summary>
	public CrimeResult BuyFromShop( string itemId, int quantity )
	{
		if ( !Ready || CrimeCatalog.ShopEntryOf( itemId ) is not { } entry
		     || quantity < 1 || quantity > CrimeCatalog.ShopMaxQuantity )
			return CrimeResult.Fail( "Nothing happens." );

		var crime = Crime;

		if ( crime.Deliveries.Count( x => x.State == DeliveryState.InTransit ) >= CrimeCatalog.ShopMaxInTransit )
			return CrimeResult.Fail( "Too many deliveries are on their way." );

		if ( crime.Deliveries.Count >= CrimeRecord.MaxEntries )
			return CrimeResult.Fail( "Your mailbox is full of deliveries. Collect them first." );

		var total = entry.Price * quantity;

		if ( !Wallet.TrySpendCrypto( total ) )
			return CrimeResult.Fail( "Not enough crypto." );

		var delivery = new DeliveryRecord
		{
			Id = $"d{crime.NextId++}",
			Kind = entry.Kind,
			Count = quantity,
			Quality = entry.Quality,
			State = DeliveryState.InTransit,
			ArriveAt = WorldClock.Now + entry.DeliverSeconds,
		};

		crime.Deliveries.Add( delivery );
		Changed();

		var name = ItemCatalog.Find( entry.Kind )?.Name ?? entry.Kind;
		return CrimeResult.Success(
			$"Bought {quantity} {name} for {total:N0} crypto. "
			+ $"It reaches your mailbox at {WorldClock.TimeOfDayOf( delivery.ArriveAt )}." );
	}

	/// <summary>
	/// Send every parcel the player carries from this mailbox, until the post is full (<see
	/// cref="CrimeCatalog.MaxParcels"/>); the rest stay in the bag. A mailbox is claimed by the first player who sends
	/// from it, and stays theirs; nobody else can use it. Host only.
	/// </summary>
	public CrimeResult SendAllParcels( MailBox box )
	{
		if ( !Ready || !box.IsValid() )
			return CrimeResult.Fail( "Nothing happens." );

		var crime = Crime;

		if ( CheckMailbox( box, out var steamId ) is { } error )
			return CrimeResult.Fail( error );

		var slots = new List<int>();

		for ( var i = 0; i < Inventory.SlotCount; i++ )
		{
			if ( Inventory.At( i ) is { Kind: ItemCatalog.Parcel } )
				slots.Add( i );
		}

		if ( slots.Count == 0 )
			return CrimeResult.Fail( "You have no parcel to send." );

		if ( PostalSystem.InPost( crime ) >= CrimeCatalog.MaxParcels )
			return CrimeResult.Fail( "The post has too many of your parcels." );

		var sent = 0;
		var firstCollect = long.MaxValue;

		foreach ( var slot in slots )
		{
			if ( PostalSystem.InPost( crime ) >= CrimeCatalog.MaxParcels )
				break;

			var item = Inventory.TakeBatch( slot );
			var order = crime.Orders.FirstOrDefault( x => x.Id == item.OrderId );

			// A parcel for an order that is gone (cancelled) is only a box. One for an order in progress ships it.
			if ( order is { State: OrderState.Packed } )
				order.State = OrderState.Shipped;

			// A parcel with no order shows what is inside; only a parcel with no contents either falls back to Static.
			var inside = (item.Contents ?? []).FirstOrDefault();

			var parcel = PostalSystem.Send(
				crime,
				order?.Id ?? "",
				order?.Kind ?? inside?.Kind ?? ProductCatalog.Default.Product.Id,
				order?.Count ?? inside?.Count ?? item.Count,
				item.Quality );

			firstCollect = System.Math.Min( firstCollect, parcel.CollectAt );
			sent++;
		}

		box.OwnerId = steamId;
		crime.MailboxId = box.BoxId;
		MailboxRegistry.Claim( box.BoxId, steamId );
		UpdateFlag();
		Changed();

		var time = WorldClock.TimeOfDayOf( firstCollect );
		var left = slots.Count - sent;

		if ( left > 0 )
		{
			var verb = left == 1 ? "is" : "are";
			return CrimeResult.Success(
				$"Sent {Plural( sent )}. The post is full: {Plural( left )} {verb} still in your bag." );
		}

		return CrimeResult.Success( sent == 1
			? $"The mailman comes at {time}."
			: $"Sent {sent} parcels. The mailman comes at {time}." );
	}

	static string Plural( int parcels ) => parcels == 1 ? "1 parcel" : $"{parcels} parcels";

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
			Personality = offer.Personality,
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
	/// One order the player can pack, as the packing panel reads it from <see cref="PackOrders"/>.
	/// </summary>
	public readonly record struct PackOrder(
		string Id, string Handle, string Kind, int Count, int Asked, long Deadline );

	/// <summary>
	/// The orders the player can pack, from the view the host sent, so it works on a client too.
	/// </summary>
	public IReadOnlyList<PackOrder> PackableOrders()
	{
		// The prompt asks several times a frame: parse again only when the text changed.
		if ( parsed is not null && parsedFrom == PackOrders )
			return parsed;

		var list = new List<PackOrder>();

		foreach ( var part in (PackOrders ?? "").Split( ';', System.StringSplitOptions.RemoveEmptyEntries ) )
		{
			var f = part.Split( '|' );

			if ( f.Length == 6 && int.TryParse( f[3], out var count ) && int.TryParse( f[4], out var asked )
			     && long.TryParse( f[5], out var deadline ) )
				list.Add( new( f[0], f[1], f[2], count, asked, deadline ) );
		}

		parsedFrom = PackOrders;
		return parsed = list;
	}

	string parsedFrom;
	IReadOnlyList<PackOrder> parsed;

	/// <summary>
	/// The player asks to pack an order with the batch in an inventory slot. The host takes the player from the network
	/// (<c>Rpc.Caller</c>), never from the message, checks the player is at a packing table, and runs <see
	/// cref="Pack"/>, which checks the rest. Only ids and a slot index are sent.
	/// </summary>
	[Rpc.Host]
	public void RequestPack( string orderId, int slot )
	{
		var caller = Rpc.Caller;

		if ( caller is not null && caller != Network.Owner )
			return;

		if ( !PackingTable.IsNear( WorldPosition ) )
		{
			Tell( CrimeResult.Fail( "Go to a packing table." ) );
			return;
		}

		Tell( Pack( orderId, slot ) );
	}

	/// <summary>
	/// Pack an accepted order with the batch in an inventory slot. The host checks the player owns the order, that it
	/// is accepted and in time, and that the slot holds a batch of the order's kind with at least the ordered count. It
	/// takes exactly the ordered count from that batch and gives a parcel that remembers the order and the batch's
	/// quality. The quality may be below what the buyer asked: the buyer rates it. Host only.
	/// </summary>
	public CrimeResult Pack( string orderId, int slot )
	{
		if ( !Ready || !IsId( orderId ) || Crime.Orders.FirstOrDefault( x => x.Id == orderId ) is not { } order )
			return CrimeResult.Fail( "Nothing happens." );

		if ( order.State != OrderState.Accepted )
			return CrimeResult.Fail(
				order.State == OrderState.Packed ? "That order is already packed." : "That order can't be packed." );

		if ( WorldClock.Now > order.Deadline )
			return CrimeResult.Fail( "That order has run out of time." );

		if ( Inventory.At( slot ) is not { IsBatch: true } batch || batch.Kind != order.Kind )
			return CrimeResult.Fail( "That is not the right product for this order." );

		if ( batch.Count < order.Count )
			return CrimeResult.Fail( $"That batch holds {batch.Count}. The order needs {order.Count}." );

		// A parcel needs a slot, unless the batch is used up and frees its own.
		if ( batch.Count != order.Count && Inventory.FreeSlots < 1 )
			return CrimeResult.Fail( "Your inventory is full." );

		// All or nothing: build the parcel first, and if it cannot be added the batch goes back and the order is
		// untouched.
		var parcel = ItemCatalog.Make( ItemCatalog.Parcel, 1, batch.Quality, order.Id );

		if ( parcel is null || Inventory.TakeFrom( slot, order.Count ) is not { } taken )
			return CrimeResult.Fail( "Nothing happens." );

		parcel.Contents.Add( new( taken.Kind, taken.Count, taken.Quality ) );

		if ( !Inventory.Add( parcel ) )
		{
			Inventory.Return( slot, taken );
			return CrimeResult.Fail( "Your inventory is full." );
		}

		order.State = OrderState.Packed;
		order.PackedQuality = taken.Quality;
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
	/// Once a second on the host: the post, deadlines, escrow and the Exchange, by the same rules that run for a player
	/// who is away (<see cref="CrimeRules"/>). What they did to the record is shown here: the bag, the messages, the
	/// views.
	/// </summary>
	void Tick()
	{
		RefreshViews();

		var result = CrimeRules.Advance( Progress.Owner.SteamId, Progress.Record, WorldClock.Now );

		foreach ( var orderId in result.CancelledOrders )
			TakeParcelsOf( orderId );

		foreach ( var message in result.Messages )
			Tell( CrimeResult.Fail( message ) );

		UpdateFlag();

		if ( result.Changed )
			Changed( result.HardwareChanged ? ProgressChange.Hardware : ProgressChange.None );
	}

	/// <summary>
	/// A parcel for a cancelled order that is still in the player's bag is taken, since nobody wants it.
	/// </summary>
	void TakeParcelsOf( string orderId )
	{
		for ( var i = 0; i < Inventory.SlotCount; i++ )
		{
			if ( Inventory.At( i ) is { Kind: ItemCatalog.Parcel } parcel && parcel.OrderId == orderId )
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
		if ( !IsDebugCaller() || !Ready )
			return;

		if ( !Inventory.AddItem( kind, count, quality ) )
			Tell( CrimeResult.Fail( "Not enough room in the bag for all of it." ) );
	}

	/// <summary>
	/// Testing only: every delivery from the shop has reached the mailbox now.
	/// </summary>
	[Rpc.Host]
	public void DebugDeliverNow()
	{
		if ( !IsDebugCaller() || !Ready )
			return;

		foreach ( var delivery in Crime.Deliveries )
			delivery.ArriveAt = System.Math.Min( delivery.ArriveAt, WorldClock.Now );

		PostalSystem.TickDeliveries( Crime, WorldClock.Now );
		Changed();
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
