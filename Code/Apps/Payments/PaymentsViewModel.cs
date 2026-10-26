using System;

namespace OmnipresenceApps;

/// <summary>
/// Everything Payments does: shows the account of the player in the seat, walks them from choosing a player to the
/// money being sent (in dollars or in crypto), and changes dollars and crypto at the Government Exchange. Runs in the
/// host's copy only (the app is shared), because the money is the host's.
/// </summary>
/// <remarks>
/// Who is paying is always the player in the seat, worked out from the computer. Nothing an action carries says so, and
/// the recipient is looked up among the players in the game, so an action can't pay from or to anyone else's say-so. An
/// amount is digits or "all"; the exchange's terms are read from <see cref="ExchangeCatalog"/>, never from the panel.
/// </remarks>
public sealed class PaymentsViewModel
{
	public const string ChooseAction = "choose";
	public const string BackAction = "back";
	public const string AmountAction = "amount";
	public const string SendAction = "send";
	public const string CurrencyAction = "currency";
	public const string ExchangeAction = "exchange";
	public const string SellAction = "sell";
	public const string BuyAction = "buy";

	/// <summary>The amount that means everything the player has in the currency being spent.</summary>
	public const string AllAmount = "all";

	/// <summary>The most one payment can be, so a typo can't empty an account.</summary>
	public const long MaxPayment = 1_000_000_000;

	/// <summary>Seconds the success screen stays before it goes home by itself.</summary>
	const float SuccessSeconds = 3f;

	const int MaxDigits = 12;

	/// <summary>The exchange the app deals with: the Government's, which asks less than the darknet's.</summary>
	static ExchangeVenue Venue => ExchangeCatalog.GovernmentVenue;

	readonly Func<Connection> getSeat;
	readonly Func<NaviComputer> getComputer;

	PaymentsScreen screen;
	PaymentsCurrency currency;
	string targetId = "";
	string amount = "";
	string sentAmount = "";
	string sentTo = "";
	string notice = "";
	Guid lastSeatId;
	string signature;
	int revision;
	RealTimeSince sinceSuccess;

	public PaymentsViewModel( Func<Connection> getSeat, Func<NaviComputer> getComputer )
	{
		this.getSeat = getSeat;
		this.getComputer = getComputer;
		UiState = Build();
	}

	public PaymentsUiState UiState { get; private set; }

	/// <summary>
	/// Once a frame: go home when the success screen has been up long enough or the seat changed, and draw again if
	/// anything drawn changed.
	/// </summary>
	public void Tick()
	{
		var seat = getSeat();

		if ( (seat?.Id ?? default) != lastSeatId )
		{
			lastSeatId = seat?.Id ?? default;
			GoHome( "" );
		}

		if ( screen == PaymentsScreen.Success && sinceSuccess > SuccessSeconds )
			GoHome( "" );

		// The app needs the network: anything being chosen is dropped when it goes down.
		if ( screen != PaymentsScreen.Home && !GameAppNetwork.IsOnline( getComputer() ) )
			GoHome( "" );

		if ( Signature( seat, PlayerProgress.For( seat ), Payees( seat ) ) != signature )
			UiState = Build();
	}

	public void Act( string action, string[] args )
	{
		switch ( action )
		{
			case ChooseAction when args.Length > 0:
				Choose( args[0] );
				break;

			case BackAction:
				GoHome( "" );
				break;

			case AmountAction when args.Length > 0:
				SetAmount( args[0] );
				break;

			case SendAction:
				Send();
				break;

			case CurrencyAction when args.Length > 0
			                         && Enum.TryParse<PaymentsCurrency>( args[0], true, out var picked )
			                         && Enum.IsDefined( picked ):
				currency = picked;
				notice = "";
				break;

			case ExchangeAction:
				OpenExchange();
				break;

			case SellAction when args.Length > 0:
				Sell( args[0] );
				break;

			case BuyAction when args.Length > 0:
				Buy( args[0] );
				break;
		}

		Tick();
	}

	void GoHome( string message )
	{
		screen = PaymentsScreen.Home;
		targetId = "";
		amount = "";
		notice = message;
	}

	void Choose( string id )
	{
		if ( screen != PaymentsScreen.Home || !GameAppNetwork.IsOnline( getComputer() )
		                                 || Payees( getSeat() ).All( x => x.SteamId.ToString() != id ) )
			return;

		screen = PaymentsScreen.Send;
		targetId = id;
		amount = "";
		notice = "";
	}

	void OpenExchange()
	{
		if ( screen != PaymentsScreen.Home || !GameAppNetwork.IsOnline( getComputer() )
		                                 || PlayerProgress.For( getSeat() ) is not { Loaded: true } )
			return;

		screen = PaymentsScreen.Exchange;
		notice = "";
	}

	/// <summary>
	/// Keep the typed digits without drawing again: the panel already shows them, and a redraw would move the caret.
	/// </summary>
	void SetAmount( string text )
	{
		if ( screen != PaymentsScreen.Send )
			return;

		amount = Digits( text );
		UiState = UiState with { Amount = amount };
	}

	void Send()
	{
		if ( screen != PaymentsScreen.Send || !GameAppNetwork.IsOnline( getComputer() ) )
			return;

		var seat = getSeat();
		var from = PlayerProgress.For( seat );
		var to = Payees( seat ).FirstOrDefault( x => x.SteamId.ToString() == targetId );

		if ( from is not { Loaded: true } )
			notice = "No account.";
		else if ( to is null )
			GoHome( "That player left." );
		else if ( !long.TryParse( amount, out var value ) || value <= 0 )
			notice = "Enter an amount.";
		else if ( value > MaxPayment )
			notice = $"At most {MostText()} at a time.";
		else
			Pay( seat, to, value );
	}

	/// <summary>
	/// The payment itself is a <see cref="Transactions"/> transfer, made for the player in the seat to a player the
	/// panel listed; nothing about it is taken from the panel but the amount and the choice of payee.
	/// </summary>
	void Pay( Connection seat, Connection to, long value )
	{
		var crypto = currency == PaymentsCurrency.Crypto;
		var unit = crypto ? TransactionCurrency.Crypto : TransactionCurrency.Cash;

		var result = Transactions.Transfer( Party.Player( seat.SteamId ), Party.Player( to.SteamId ), unit, value,
			TxKind.PlayerPayment );

		if ( !result.Ok )
			notice = result.Failure == TxFailure.Insufficient && !crypto ? "Not enough money." : result.Message;
		else
			Succeeded( crypto ? $"{value:N0}" : $"${value:N0}", to.DisplayName );
	}

	void Succeeded( string amountText, string name )
	{
		screen = PaymentsScreen.Success;
		sentAmount = amountText;
		sentTo = name;
		sinceSuccess = 0;
		notice = "";
	}

	/// <summary>
	/// Sell crypto for dollars at the Government Exchange: everything for "all", otherwise the digits.
	/// </summary>
	void Sell( string text )
	{
		if ( screen != PaymentsScreen.Exchange || !GameAppNetwork.IsOnline( getComputer() ) )
			return;

		var seat = getSeat();
		var progress = PlayerProgress.For( seat );
		var wallet = PlayerWallet.For( seat );

		if ( progress is not { Loaded: true } || wallet is null )
		{
			notice = "No account.";
			return;
		}

		var crypto = AmountOf( text, wallet.Crypto );
		notice = Exchange.TrySell( wallet, progress.Record?.Crime, Venue, crypto ).Message;
	}

	/// <summary>
	/// Buy crypto with dollars at the Government Exchange.
	/// </summary>
	void Buy( string text )
	{
		if ( screen != PaymentsScreen.Exchange || !GameAppNetwork.IsOnline( getComputer() ) )
			return;

		var seat = getSeat();
		var wallet = PlayerWallet.For( seat );

		if ( PlayerProgress.For( seat ) is not { Loaded: true } || wallet is null )
		{
			notice = "No account.";
			return;
		}

		var cash = AmountOf( text, wallet.Cash );
		notice = Exchange.TryBuy( wallet, Venue, cash ).Message;
	}

	/// <summary>The most this screen takes at once, in the currency it is set to.</summary>
	string MostText() => currency == PaymentsCurrency.Crypto ? $"{MaxPayment:N0} crypto" : $"${MaxPayment:N0}";

	/// <summary>When selling at the Exchange delivers the dollars.</summary>
	static string PayoutText() => Venue.PaysAtOnce
		? "the dollars arrive at once."
		: $"the dollars arrive after {Venue.CashoutDelaySeconds / 60f:0.#} min.";

	/// <summary>
	/// An amount from an action: digits, or "all" for everything the player has. Anything else is nothing, which the
	/// exchange refuses.
	/// </summary>
	static long AmountOf( string text, long all ) =>
		text == AllAmount ? all : long.TryParse( Digits( text ), out var value ) ? value : 0;

	static string Digits( string text ) =>
		new string( (text ?? "").Where( char.IsAsciiDigit ).Take( MaxDigits ).ToArray() ).TrimStart( '0' );

	string Signature( Connection seat, PlayerProgress progress, List<Connection> payees )
	{
		var me = HashCode.Combine(
			seat?.Id,
			GameAppNetwork.IsOnline( getComputer() ),
			getComputer()?.HasNetworkCard,
			(progress?.Version, PlayerWallet.For( seat )?.Version),
			screen,
			currency );

		return HashCode.Combine(
			me, targetId, notice, string.Join( ',', payees.Select( x => $"{x.Id}:{x.DisplayName}" ) ) ).ToString();
	}

	/// <summary>
	/// The other players in the game: the ones with money that can be paid.
	/// </summary>
	static List<Connection> Payees( Connection seat ) =>
		Connection.All
			.Where( x => x != seat && PlayerProgress.For( x ) is { Loaded: true } )
			.OrderBy( x => x.DisplayName )
			.ToList();

	PaymentsUiState Build()
	{
		var seat = getSeat();
		var progress = PlayerProgress.For( seat );
		var payees = Payees( seat );

		// The player being paid went away while the amount was being chosen.
		if ( screen == PaymentsScreen.Send && payees.All( x => x.SteamId.ToString() != targetId ) )
			GoHome( "That player left." );

		signature = Signature( seat, progress, payees );

		var state = new PaymentsUiState { Revision = ++revision, Screen = screen, Notice = notice, Currency = currency };

		if ( !GameAppNetwork.IsOnline( getComputer() ) )
			return state with { Screen = PaymentsScreen.Home, OfflineHint = GameAppNetwork.OfflineHint( getComputer() ) };

		if ( seat is null )
			return state with { Screen = PaymentsScreen.Home, Message = "Sit down to see your account." };

		if ( progress is not { Loaded: true } )
			return state with { Screen = PaymentsScreen.Home, Message = "Loading your account..." };

		var wallet = PlayerWallet.For( seat );
		var rows = payees.Select( x => new PayeeRow( x.SteamId.ToString(), x.DisplayName ) ).ToList();
		var target = screen == PaymentsScreen.Send
			? rows.FirstOrDefault( x => x.Id == targetId )
			: screen == PaymentsScreen.Success ? new PayeeRow( "", sentTo ) : null;

		return state with
		{
			HasAccount = true,
			Owner = seat.DisplayName,
			Balance = $"${wallet?.Cash:N0}",
			Crypto = $"{wallet?.Crypto:N0}",
			Payees = rows,
			History = progress.TransactionHistory.Reverse().Select( ToRow ).ToList(),
			Target = target,
			Amount = amount,
			SentAmount = sentAmount,
			ExchangeName = Venue.Name,
			ExchangeTagline = Venue.Tagline,
			SellRate = $"1 to 1, minus {Venue.SellFeePercent}%; {PayoutText()}",
			BuyRate = $"1 to 1, minus {Venue.BuyFeePercent}%; the crypto arrives at once.",
		};
	}

	static PaymentRow ToRow( TransactionEntry entry )
	{
		var incoming = entry.Amount > 0;
		var crypto = entry.Currency == TransactionCurrency.Crypto;

		var amount = $"{(incoming ? "+" : "-")}{(crypto ? "" : "$")}{Math.Abs( entry.Amount ):N0}";

		return new PaymentRow( string.IsNullOrEmpty( entry.PeerName ) ? "Unknown" : entry.PeerName, amount,
			LabelOf( entry ), WorldClock.Format( entry.At ), incoming, crypto );
	}

	/// <summary>What the activity list calls a transaction: its kind, and the note when there is one.</summary>
	static string LabelOf( TransactionEntry entry )
	{
		var kind = entry.Kind switch
		{
			nameof(TxKind.PlayerPayment) => "Payment",
			nameof(TxKind.OrderPayout) => "Order payout",
			nameof(TxKind.OrgShare) => "Organization share",
			nameof(TxKind.ShopPurchase) or nameof(TxKind.DepotPurchase) => "Purchase",
			nameof(TxKind.ExchangeSell) or nameof(TxKind.ExchangeBuy) => "Exchange",
			nameof(TxKind.CashOut) => "Cash-out",
			nameof(TxKind.RegistrationFee) => "Registration",
			var other when other.Length > 0 => other,
			_ => "Transaction",
		};

		return string.IsNullOrEmpty( entry.Note ) || entry.Kind == nameof(TxKind.PlayerPayment)
			? kind
			: $"{kind}: {entry.Note}";
	}
}
