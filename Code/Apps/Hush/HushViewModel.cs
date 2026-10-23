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
	public const string PackAction = "pack";
	public const string SellAction = "sell";
	public const string BuyAction = "buy";

	/// <summary>The Exchange amount that means everything the player has.</summary>
	public const string AllAmount = "all";

	readonly Func<Connection> getSeat;
	readonly Func<NaviComputer> getComputer;

	HushTab tab;
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

				case AcceptAction when args.Length > 0:
					notice = crime.Accept( args[0] ).Message;
					break;

				case PackAction when args.Length > 0:
					notice = crime.Pack( args[0] ).Message;
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
			tab,
			notice,
			// Countdowns redraw once a second.
			WorldClock.Now,
			MarketSystem.Current?.Open.Count ).ToString();

	HushUiState Build()
	{
		var seat = getSeat();
		var crime = PlayerCrime.For( seat );
		var wallet = PlayerWallet.For( seat );

		signature = Signature( seat );

		var state = new HushUiState { Revision = ++revision, Tab = tab, Notice = notice };

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
			Open = BuildOpen( crime ),
			Mine = BuildMine( crime ),
			Shipments = BuildShipments( crime ),
			Reviews = crime.Reviews.Reverse().Select( ToReviewRow ).ToList(),
			Cashouts = crime.Cashouts
				.Select( x => $"${x.Cash:N0} arrives in {InteractionSetup.Countdown( x.ReadyAt - WorldClock.Now )}" )
				.ToList(),
			SellRate = $"Crypto to dollars: 1 to 1, minus {CrimeCatalog.SellFeePercent}%, "
			           + $"paid after {CrimeCatalog.CashoutDelaySeconds / 60f:0.#} min.",
			BuyRate = $"Dollars to crypto: 1 to 1, minus {CrimeCatalog.BuyFeePercent}%, at once.",
		};
	}

	static bool IsHeld( OrderRecord order ) =>
		order.State is OrderState.Accepted or OrderState.Packed or OrderState.Shipped or OrderState.Delivered;

	static string ProductName( string kind ) => ItemCatalog.Find( kind )?.Name ?? kind;

	static IReadOnlyList<OpenOrderRow> BuildOpen( PlayerCrime crime )
	{
		var now = WorldClock.Now;
		var full = crime.Orders.Where( IsHeld ).Count() >= CrimeCatalog.MaxActiveOrders;

		var rows = new List<OpenOrderRow>();

		foreach ( var x in MarketSystem.Current?.Open ?? new List<MarketOrder>() )
		{
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
				!full ) );
		}

		return rows;
	}

	static IReadOnlyList<MyOrderRow> BuildMine( PlayerCrime crime )
	{
		var now = WorldClock.Now;
		var inventory = crime.Components.Get<PlayerInventory>();

		return crime.Orders
			.Reverse()
			.Select( x => ToMyRow( x, now, inventory ) )
			.ToList();
	}

	static MyOrderRow ToMyRow( OrderRecord order, long now, PlayerInventory inventory )
	{
		var left = InteractionSetup.Countdown( order.Deadline - now );

		var (status, detail) = order.State switch
		{
			OrderState.Accepted => (MyOrderStatus.ToPack, $"Deliver within {left}"),
			OrderState.Packed => (MyOrderStatus.ToSend, $"Take the parcel to your mailbox. {left} left"),
			OrderState.Shipped => (MyOrderStatus.Shipped, $"On its way. {left} left"),
			OrderState.Delivered => (MyOrderStatus.Escrow,
				$"Delivered. Escrow releases in {InteractionSetup.Countdown( order.ReleaseAt - now )}"),
			OrderState.Released => (MyOrderStatus.Paid, $"Paid {order.Paid:N0} after the fee"),
			_ => (MyOrderStatus.Cancelled, "Cancelled"),
		};

		var canPack = order.State == OrderState.Accepted && now <= order.Deadline
		              && (inventory?.CountOf( order.Kind, order.MinQuality ) ?? 0) >= order.Count;

		return new MyOrderRow(
			order.Id,
			order.Handle,
			ProductName( order.Kind ),
			order.Count,
			order.MinQuality,
			$"{order.Total:N0}",
			status,
			detail,
			canPack );
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
