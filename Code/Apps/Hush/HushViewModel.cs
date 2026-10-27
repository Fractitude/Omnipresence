using System;

namespace OmnipresenceApps;

/// <summary>
/// Everything Hush does: the dark web market. It shows the open orders of the NPC buyers, the player's own orders and
/// parcels, their wallet and reviews, and the Exchange between crypto and dollars. Runs in the host's copy only (the app
/// is shared), because the money and the orders are the host's. It only works at home: in the Wired it has no signal.
/// </summary>
/// <remarks>
/// Who is acting is always the player in the seat, worked out from the computer. The actions carry only an order id, a tab
/// name or an Exchange amount: the terms, the stock, the quality and the prices are all checked on the host
/// (<see cref="PlayerCrime"/>). The state holds no buyer type or any other hidden flag.
/// </remarks>
public sealed class HushViewModel
{
	public const string TabAction = "tab";
	public const string AcceptAction = "accept";
	public const string CategoryAction = "category";
	public const string StatusAction = "status";
	public const string ShopAction = "shop";
	public const string QuantityAction = "quantity";

	/// <summary>The chip that means no filter.</summary>
	public const string AllKey = "all";
	public const string SellAction = "sell";
	public const string BuyAction = "buy";

	/// <summary>The Exchange amount that means everything the player has.</summary>
	public const string AllAmount = "all";

	readonly Func<Connection> getSeat;
	readonly Func<NaviComputer> getComputer;

	HushTab tab;
	string category = "";
	string statusFilter = "";
	int shopQuantity = 1;
	string notice = "";
	Guid lastSeatId;
	string signature;
	int revision;

	public HushViewModel( Func<Connection> getSeat, Func<NaviComputer> getComputer )
	{
		this.getSeat = getSeat;
		this.getComputer = getComputer;
		UiState = Build();
	}

	public HushUiState UiState { get; private set; }

	/// <summary>
	/// Once a frame: start over when the seat changed, and draw again if anything drawn changed.
	/// </summary>
	public void Tick()
	{
		var seat = getSeat();

		if ( (seat?.Id ?? default) != lastSeatId )
		{
			lastSeatId = seat?.Id ?? default;
			tab = HushTab.Orders;
			category = "";
			statusFilter = "";
			shopQuantity = 1;
			notice = "";
		}

		if ( Signature( seat ) != signature )
			UiState = Build();
	}

	public void Act( string action, string[] args )
	{
		var seat = getSeat();
		var crime = PlayerCrime.For( seat );

		if ( crime is { Ready: true } && CanWork( seat ) )
		{
			switch ( action )
			{
				case TabAction when args.Length > 0 && Enum.TryParse<HushTab>( args[0], true, out var next ):
					tab = next;
					notice = "";
					break;

				// Only a category the catalog knows is taken; anything else changes nothing.
				case CategoryAction when args.Length > 0 && CategoryOf( args[0] ) is { } chosen:
					category = chosen;
					break;

				case StatusAction when args.Length > 0 && StatusOf( args[0] ) is { } chosenStatus:
					statusFilter = chosenStatus;
					break;

				// Only a quantity the shop offers as a chip is taken.
				case QuantityAction when args.Length > 0 && int.TryParse( args[0], out var quantity )
				                                         && CrimeCatalog.ShopQuantities.Contains( quantity ):
					shopQuantity = quantity;
					break;

				// An item id and a quantity: the host checks both against the catalogue, and takes the price from it.
				case ShopAction when args.Length > 1 && int.TryParse( args[1], out var wanted ):
					notice = crime.BuyFromShop( args[0], wanted ).Message;
					break;

				// The player stays on the Orders tab after accepting; the accepted order shows under Your orders.
				case AcceptAction when args.Length > 0:
					notice = crime.Accept( args[0] ).Message;
					break;

				case SellAction when args.Length > 0:
					notice = crime.SellCrypto( AmountOf( args[0], PlayerWallet.For( seat )?.Crypto ?? 0 ) ).Message;
					break;

				case BuyAction when args.Length > 0:
					notice = crime.BuyCrypto( AmountOf( args[0], PlayerWallet.For( seat )?.Cash ?? 0 ) ).Message;
					break;
			}
		}

		UiState = Build();
	}

	/// <summary>
	/// Hush works at home, with the network on.
	/// </summary>
	bool CanWork( Connection seat ) =>
		seat is not null && WiredTravel.PlaceOf( seat ) == WiredPlace.Home && GameAppNetwork.IsOnline( getComputer() );

	/// <summary>
	/// A category from an action: empty for All, the catalog's own spelling of a known one, or null for anything else.
	/// </summary>
	static string CategoryOf( string text ) =>
		text == AllKey ? "" : ProductCatalog.OfCategory( text )?.Name;

	/// <summary>
	/// A status from an action: empty for All, a <see cref="MyOrderStatus"/> name, or null for anything else.
	/// </summary>
	static string StatusOf( string text ) =>
		text == AllKey ? ""
		: Enum.TryParse<MyOrderStatus>( text, true, out var status ) && Enum.IsDefined( status )
			? status.ToString()
			: null;

	/// <summary>
	/// An amount from an action: digits, or "all". Anything else is nothing.
	/// </summary>
	static long AmountOf( string text, long all ) =>
		text == AllAmount ? all : long.TryParse( text, out var value ) && value > 0 ? value : 0;

	string Signature( Connection seat ) =>
		HashCode.Combine(
			seat?.Id,
			(PlayerProgress.For( seat )?.Version, PlayerWallet.For( seat )?.Version),
			GameAppNetwork.IsOnline( getComputer() ),
			seat is null ? default : WiredTravel.PlaceOf( seat ),
			(tab, category, statusFilter, notice, shopQuantity),
			// Countdowns redraw once a second.
			WorldClock.Now,
			MarketSystem.Current?.Open.Count ).ToString();

	HushUiState Build()
	{
		var seat = getSeat();
		var crime = PlayerCrime.For( seat );
		var wallet = PlayerWallet.For( seat );
		var venue = ExchangeCatalog.DarknetVenue;

		signature = Signature( seat );

		var state = new HushUiState
		{
			Revision = ++revision, Tab = tab, Notice = notice, Category = category, StatusFilter = statusFilter,
		};

		if ( !GameAppNetwork.IsOnline( getComputer() ) )
			return state with { OfflineHint = GameAppNetwork.OfflineHint( getComputer() ) };

		if ( seat is null )
			return state with { Message = "Sit down to open Hush." };

		if ( WiredTravel.PlaceOf( seat ) != WiredPlace.Home )
			return state with { NoSignal = true, Message = "No signal. Hush only works from home." };

		if ( crime is not { Ready: true } || wallet is null )
			return state with { Message = "Connecting..." };

		return state with
		{
			HasAccount = true,
			Crypto = $"{wallet.Crypto:N0}",
			Cash = $"${wallet.Cash:N0}",
			Level = crime.Level.ToString(),
			Rating = crime.Rating > 0 ? $"{crime.Rating:0.0} / 5" : "No reviews yet",
			Trades = $"{crime.GoodTrades} good, {crime.BadTrades} bad",
			FeeText = $"{crime.FeePercent}% fee on payouts",
			Escrow = $"{crime.Orders.Where( IsHeld ).Sum( x => x.Total ):N0} held in escrow",
			Open = BuildOpen( crime, category ),
			Categories = BuildCategories(),
			Mine = BuildMine( crime, statusFilter ),
			Shop = BuildShop( crime, wallet, shopQuantity ),
			ShopQuantity = shopQuantity,
			ShopQuantities = CrimeCatalog.ShopQuantities,
			Incoming = BuildIncoming( crime ),
			StatusChips = BuildStatusChips( crime ),
			ToDo = crime.Orders.Count( x => x.State is OrderState.Accepted or OrderState.Packed ),
			Shipments = BuildShipments( crime ),
			Reviews = crime.Reviews.Reverse().Select( ToReviewRow ).ToList(),
			Cashouts = crime.Cashouts
				.Select( x => $"${x.Cash:N0} arrives in {InteractionSetup.Countdown( x.ReadyAt - WorldClock.Now )}" )
				.ToList(),
			SellRate = $"Crypto to dollars: 1 to 1, minus {venue.SellFeePercent}%, "
			           + (venue.PaysAtOnce
				           ? "paid at once."
				           : $"paid after {venue.CashoutDelaySeconds / 60f:0.#} min."),
			BuyRate = $"Dollars to crypto: 1 to 1, minus {venue.BuyFeePercent}%, at once.",
		};
	}

	static bool IsHeld( OrderRecord order ) =>
		order.State is OrderState.Accepted or OrderState.Packed or OrderState.Shipped or OrderState.Delivered;

	static string ProductName( string kind ) => ItemCatalog.Find( kind )?.Name ?? kind;

	static string CategoryOfKind( string kind ) => ItemCatalog.Find( kind )?.Category ?? "";

	/// <summary>
	/// All, then one chip per drug (by name), each with how many of its orders are on the board, even none, so the
	/// filter can be undone from where it is.
	/// </summary>
	static IReadOnlyList<FilterChip> BuildCategories()
	{
		var open = MarketSystem.Current?.Open ?? new List<MarketOrder>();
		var names = ProductCatalog.Products.Select( x => x.Name ).OrderBy( x => x );

		var chips = new List<FilterChip> { new( "", "All", open.Count ) };
		chips.AddRange( names.Select( x => new FilterChip( x, x, open.Count( y => CategoryOfKind( y.Kind ) == x ) ) ) );
		return chips;
	}

	static IReadOnlyList<HushShopRow> BuildShop( PlayerCrime crime, PlayerWallet wallet, int quantity )
	{
		var inTransit = crime.Deliveries.Count( x => x.State == DeliveryState.InTransit );

		return CrimeCatalog.Shop.Select( x =>
		{
			var total = x.Price * quantity;
			var item = ItemCatalog.Find( x.Kind );

			var reason = inTransit >= CrimeCatalog.ShopMaxInTransit ? "Too many deliveries are on their way."
				: wallet.Crypto < total ? "Not enough crypto."
				: "";

			return new HushShopRow(
				x.Id,
				item?.Name ?? x.Kind,
				item?.Description ?? "",
				$"{x.Price:N0}",
				$"{total:N0}",
				$"Arrives in your mailbox in {InteractionSetup.Countdown( x.DeliverSeconds )}",
				reason == "",
				reason );
		} ).ToList();
	}

	static IReadOnlyList<HushShipmentRow> BuildIncoming( PlayerCrime crime )
	{
		var rows = crime.Deliveries
			.Reverse()
			.Select( x => new HushShipmentRow(
				$"{ProductName( x.Kind )} x{x.Count}",
				x.State == DeliveryState.InTransit
					? $"On its way, arrives {Clock( x.ArriveAt )}"
					: "Waiting in your mailbox",
				x.State == DeliveryState.InMailbox ) )
			.ToList();

		// Mail that has reached a mailbox is in the box and not in the record (anyone may take it out of the box), so
		// it is listed from the box itself.
		foreach ( var letter in crime.MailInBox )
			rows.Insert( 0, new HushShipmentRow(
				$"{ProductName( letter.Kind )} x{letter.Count}", "Waiting in your mailbox", true ) );

		return rows;
	}

	static IReadOnlyList<FilterChip> BuildStatusChips( PlayerCrime crime )
	{
		var chips = new List<FilterChip> { new( "", "All", crime.Orders.Count ) };

		foreach ( var status in Enum.GetValues<MyOrderStatus>() )
		{
			var count = crime.Orders.Count( x => StatusOfOrder( x ) == status );
			chips.Add( new( status.ToString(), StatusLabel( status ), count ) );
		}

		return chips;
	}

	static string StatusLabel( MyOrderStatus status ) => status switch
	{
		MyOrderStatus.ToPack => "To pack",
		MyOrderStatus.ToSend => "To send",
		_ => status.ToString(),
	};

	static MyOrderStatus StatusOfOrder( OrderRecord order ) => order.State switch
	{
		OrderState.Accepted => MyOrderStatus.ToPack,
		OrderState.Packed => MyOrderStatus.ToSend,
		OrderState.Shipped => MyOrderStatus.Shipped,
		OrderState.Delivered => MyOrderStatus.Escrow,
		OrderState.Released => MyOrderStatus.Paid,
		_ => MyOrderStatus.Cancelled,
	};

	static IReadOnlyList<OpenOrderRow> BuildOpen( PlayerCrime crime, string category )
	{
		var now = WorldClock.Now;
		var full = crime.Orders.Where( IsHeld ).Count() >= CrimeCatalog.MaxActiveOrders;

		var rows = new List<OpenOrderRow>();

		foreach ( var x in MarketSystem.Current?.Open ?? new List<MarketOrder>() )
		{
			if ( category != "" && CategoryOfKind( x.Kind ) != category )
				continue;

			rows.Add( new OpenOrderRow(
				x.Id,
				x.Handle,
				x.Text,
				ProductName( x.Kind ),
				x.Count,
				x.MinQuality,
				$"{x.PriceEach:N0}",
				$"{x.Total:N0}",
				InteractionSetup.Countdown( x.ExpiresAt - now ),
				!full,
				CrimeCatalog.DeadlineClass( x.ExpiresAt - now ) ) );
		}

		return rows;
	}

	static IReadOnlyList<MyOrderRow> BuildMine( PlayerCrime crime, string status )
	{
		var now = WorldClock.Now;

		return crime.Orders
			.Reverse()
			.Where( x => status == "" || StatusOfOrder( x ).ToString() == status )
			.Select( x => ToMyRow( x, now ) )
			.ToList();
	}

	static MyOrderRow ToMyRow( OrderRecord order, long now )
	{
		var left = InteractionSetup.Countdown( order.Deadline - now );

		var status = StatusOfOrder( order );

		var detail = status switch
		{
			MyOrderStatus.ToPack => $"Pack it at the packing table at home. Send within {left}",
			MyOrderStatus.ToSend =>
				$"Parcel quality {order.PackedQuality}. Take it to your mailbox. {left} left to send",
			MyOrderStatus.Shipped => $"On its way. Due {Clock( order.Deadline )}",
			MyOrderStatus.Escrow =>
				$"Delivered. Escrow releases in {InteractionSetup.Countdown( order.ReleaseAt - now )}",
			MyOrderStatus.Paid => $"Paid {order.Paid:N0} after the fee",
			_ => "Cancelled",
		};

		return new MyOrderRow(
			order.Id,
			order.Handle,
			ProductName( order.Kind ),
			order.Count,
			order.MinQuality,
			$"{order.Total:N0}",
			status,
			detail,
			status is MyOrderStatus.ToPack or MyOrderStatus.ToSend
				? CrimeCatalog.DeadlineClass( order.Deadline - now )
				: "" );
	}

	static IReadOnlyList<HushShipmentRow> BuildShipments( PlayerCrime crime )
	{
		var now = WorldClock.Now;
		var rows = new List<HushShipmentRow>();

		foreach ( var order in crime.Orders.Where( x => x.State == OrderState.Packed ).Reverse() )
			rows.Add( new( $"{order.Handle}: parcel packed", "In your bag. Send it from your mailbox.", false ) );

		foreach ( var parcel in crime.Parcels.Reverse() )
		{
			var order = crime.Orders.FirstOrDefault( x => x.Id == parcel.OrderId );
			var title = order is null ? "Parcel" : $"{order.Handle}: {order.Count} x {ProductName( order.Kind )}";

			var detail = parcel.State switch
			{
				ParcelState.Waiting => $"In the mailbox. The mailman comes at {Clock( parcel.CollectAt )}",
				ParcelState.InTransit => $"Collected. In transit, arrives in {Left( parcel.DeliverAt, now )}",
				_ => $"Delivered at {Clock( parcel.DeliverAt )}",
			};

			rows.Add( new( title, detail, parcel.State == ParcelState.Delivered ) );
		}

		return rows;
	}

	static string Clock( long seconds ) => WorldClock.TimeOfDayOf( seconds );

	static string Left( long until, long now ) => InteractionSetup.Countdown( until - now );

	static ReviewRow ToReviewRow( ReviewRecord review ) =>
		new( review.Handle, review.Stars, review.Text, WorldClock.Format( review.At ) );
}
