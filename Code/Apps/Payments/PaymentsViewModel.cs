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

	/// <summary>Buy, Sell or Convert on the Exchange screen.</summary>
	public const string ExchangeTabAction = "xtab";

	/// <summary>Which currency the exchange amount is typed in, or which one Convert starts from.</summary>
	public const string ExchangeUnitAction = "xunit";

	/// <summary>The digits typed into the exchange amount.</summary>
	public const string ExchangeAmountAction = "xamount";

	/// <summary>One of the four quick buttons under the exchange amount.</summary>
	public const string QuickAction = "xquick";

	/// <summary>Deal what the exchange screen asks for.</summary>
	public const string TradeAction = "xtrade";

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
	ExchangeTab tab = ExchangeTab.Buy;
	PaymentsCurrency unit = PaymentsCurrency.Dollars;
	string targetId = "";
	string amount = "";
	string exchangeAmount = "";
	string sentAmount = "";
	string sentTo = "";
	string notice = "";
	bool noticeGood;
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

			case ExchangeTabAction when args.Length > 0
			                            && Enum.TryParse<ExchangeTab>( args[0], true, out var pickedTab )
			                            && Enum.IsDefined( pickedTab ):
				PickTab( pickedTab );
				break;

			case ExchangeUnitAction when args.Length > 0
			                             && Enum.TryParse<PaymentsCurrency>( args[0], true, out var pickedUnit )
			                             && Enum.IsDefined( pickedUnit ):
				PickUnit( pickedUnit );
				break;

			case ExchangeAmountAction when args.Length > 0:
				SetExchangeAmount( args[0] );
				break;

			case QuickAction when args.Length > 0:
				PickQuick( args[0] );
				break;

			case TradeAction:
				Trade();
				break;
		}

		Tick();
	}

	void GoHome( string message )
	{
		screen = PaymentsScreen.Home;
		targetId = "";
		amount = "";
		exchangeAmount = "";
		Say( message );
	}

	void Choose( string id )
	{
		if ( screen != PaymentsScreen.Home || !GameAppNetwork.IsOnline( getComputer() )
		                                 || Payees( getSeat() ).All( x => x.SteamId.ToString() != id ) )
			return;

		screen = PaymentsScreen.Send;
		targetId = id;
		amount = "";
		Say( "" );
	}

	void OpenExchange()
	{
		if ( screen != PaymentsScreen.Home || !GameAppNetwork.IsOnline( getComputer() )
		                                 || PlayerProgress.For( getSeat() ) is not { Loaded: true } )
			return;

		screen = PaymentsScreen.Exchange;
		tab = ExchangeTab.Buy;
		unit = DefaultUnit( tab );
		exchangeAmount = "";
		Say( "" );
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
			Say( "No account." );
		else if ( to is null )
			GoHome( "That player left." );
		else if ( !long.TryParse( amount, out var value ) || value <= 0 )
			Say( "Enter an amount." );
		else if ( value > MaxPayment )
			Say( $"At most {MostText()} at a time." );
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
		var money = crypto ? TransactionCurrency.Crypto : TransactionCurrency.Cash;

		var result = Transactions.Transfer( Party.Player( seat.SteamId ), Party.Player( to.SteamId ), money, value,
			TxKind.PlayerPayment );

		if ( !result.Ok )
			Say( result.Failure == TxFailure.Insufficient && !crypto ? "Not enough money." : result.Message );
		else
			Succeeded( crypto ? $"{value:N0}" : $"${value:N0}", to.DisplayName );
	}

	void Succeeded( string amountText, string name )
	{
		screen = PaymentsScreen.Success;
		sentAmount = amountText;
		sentTo = name;
		sinceSuccess = 0;
		Say( "" );
	}

	/// <summary>
	/// Switch tabs. Each starts from an empty amount typed in its own currency: Buy in dollars, Sell in Ethereum, and
	/// Convert in dollars until the player picks the other way round.
	/// </summary>
	void PickTab( ExchangeTab picked )
	{
		if ( screen != PaymentsScreen.Exchange || tab == picked )
			return;

		tab = picked;
		unit = DefaultUnit( picked );
		exchangeAmount = "";
		Say( "" );
	}

	/// <summary>
	/// Type the amount in the other currency. An empty amount, not a converted one: a hundred dollars is not a hundred
	/// Ethereum, and the player is about to type a different number anyway. On Convert this picks the way round.
	/// </summary>
	void PickUnit( PaymentsCurrency picked )
	{
		if ( screen != PaymentsScreen.Exchange || unit == picked )
			return;

		unit = picked;
		exchangeAmount = "";
		Say( "" );
	}

	/// <summary>
	/// The digits typed at the exchange. Draws again at once, because what the amount deals is drawn under it.
	/// </summary>
	void SetExchangeAmount( string text )
	{
		if ( screen != PaymentsScreen.Exchange )
			return;

		exchangeAmount = Digits( text );
		UiState = Build();
	}

	/// <summary>
	/// A quick button: a fixed amount in the currency being typed, or a share of everything there is ("p25", "p50",
	/// "p75") or all of it.
	/// </summary>
	void PickQuick( string key )
	{
		if ( screen != PaymentsScreen.Exchange )
			return;

		var all = Affordable( PlayerWallet.For( getSeat() ) );

		var typed = key switch
		{
			AllAmount => all,
			_ when key.Length > 1 && key[0] == 'p' && int.TryParse( key[1..], out var percent ) => all * percent / 100,
			_ when long.TryParse( key, out var value ) => value,
			_ => 0,
		};

		exchangeAmount = Digits( typed.ToString() );
		Say( "" );
		UiState = Build();
	}

	/// <summary>
	/// Deal what the screen asks for: the amount is worked out into the currency being spent here, and the exchange checks
	/// everything again. The amount goes once it works, so a second press can't repeat a trade by accident.
	/// </summary>
	void Trade()
	{
		if ( screen != PaymentsScreen.Exchange || !GameAppNetwork.IsOnline( getComputer() ) )
			return;

		var seat = getSeat();
		var progress = PlayerProgress.For( seat );
		var wallet = PlayerWallet.For( seat );

		if ( progress is not { Loaded: true } || wallet is null )
		{
			Say( "No account." );
			return;
		}

		var typed = TypedAmount();

		if ( typed < 1 )
		{
			Say( "Enter an amount." );
			return;
		}

		var spendsCrypto = SpendsCrypto();
		var (spend, _, _) = Deal( typed, spendsCrypto, unit );

		var result = spendsCrypto
			? Exchange.TrySell( wallet, progress.Record?.Crime, Venue, spend )
			: Exchange.TryBuy( wallet, Venue, spend );

		Say( result.Message, result.Ok );

		if ( result.Ok )
			exchangeAmount = "";
	}

	/// <summary>Whether the deal spends Ethereum (selling) rather than spending dollars (buying).</summary>
	bool SpendsCrypto() => tab == ExchangeTab.Sell || (tab == ExchangeTab.Convert && unit == PaymentsCurrency.Crypto);

	/// <summary>The digits typed, in the currency being typed.</summary>
	long TypedAmount() => long.TryParse( exchangeAmount, out var value ) ? value : 0;

	/// <summary>
	/// What an amount typed in a currency deals: the currency it spends, what the venue keeps of it, and what comes
	/// back. The preview and the trade both go through here, so what is drawn is what is done.
	/// </summary>
	(long Spend, long Fee, long Get) Deal( long typed, bool spendsCrypto, PaymentsCurrency typedIn )
	{
		if ( spendsCrypto )
		{
			var crypto = typedIn == PaymentsCurrency.Crypto ? typed : CryptoFor( typed, Venue.SellFeePercent );
			var cash = Venue.CashFor( crypto );

			return (crypto, crypto - cash, cash);
		}

		var spend = typedIn == PaymentsCurrency.Dollars ? typed : CashFor( typed, Venue.BuyFeePercent );
		var bought = Venue.CryptoFor( spend );

		return (spend, spend - bought, bought);
	}

	/// <summary>
	/// The crypto to hand over so that <paramref name="cash"/> dollars arrive after the fee: rounded up, so the player
	/// never gets less than the amount they typed.
	/// </summary>
	static long CryptoFor( long cash, int feePercent ) => CeilDiv( cash * 100, 100 - feePercent );

	/// <summary>
	/// The dollars to hand over so that <paramref name="crypto"/> arrives after the fee.
	/// </summary>
	static long CashFor( long crypto, int feePercent ) => CeilDiv( crypto * 100, 100 - feePercent );

	static long CeilDiv( long value, long by ) => (value + by - 1) / by;

	/// <summary>
	/// The most the player can deal in the currency being typed: the balance of the one being spent, or what spending all
	/// of it would get in the one coming back.
	/// </summary>
	long Affordable( PlayerWallet wallet )
	{
		var spendsCrypto = SpendsCrypto();
		var purse = spendsCrypto ? wallet?.Crypto ?? 0 : wallet?.Cash ?? 0;

		if ( unit == PaymentsCurrency.Crypto )
			return spendsCrypto ? purse : Venue.CryptoFor( purse );

		return spendsCrypto ? Venue.CashFor( purse ) : purse;
	}

	/// <summary>The currency a tab starts from.</summary>
	static PaymentsCurrency DefaultUnit( ExchangeTab of ) =>
		of == ExchangeTab.Sell ? PaymentsCurrency.Crypto : PaymentsCurrency.Dollars;

	/// <summary>Note for the player, and whether it is good news.</summary>
	void Say( string message, bool good = false )
	{
		notice = message;
		noticeGood = good;
	}

	/// <summary>An amount with its currency: a sign for dollars, a ticker for Ethereum.</summary>
	static string AmountText( long value, PaymentsCurrency of ) =>
		of == PaymentsCurrency.Dollars ? $"${value:N0}" : $"{value:N0} {PaymentsUiState.CryptoUnit}";

	/// <summary>The most this screen takes at once, in the currency it is set to.</summary>
	string MostText() =>
		currency == PaymentsCurrency.Crypto ? $"{MaxPayment:N0} {PaymentsUiState.CryptoUnit}" : $"${MaxPayment:N0}";

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
			me, targetId, notice, noticeGood, tab, unit, exchangeAmount,
			string.Join( ',', payees.Select( x => $"{x.Id}:{x.DisplayName}" ) ) ).ToString();
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

		var state = new PaymentsUiState
		{
			Revision = ++revision,
			Screen = screen,
			Notice = notice,
			NoticeGood = noticeGood,
			Currency = currency,
		};

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
			Exchange = screen == PaymentsScreen.Exchange ? BuildExchange( wallet ) : null,
		};
	}

	/// <summary>
	/// The Exchange screen: the amount typed, what it deals at <see cref="Venue"/>, and the four quick buttons for the
	/// tab. Buying spends dollars and selling spends Ethereum; Convert is either, as picked with its currency pills.
	/// </summary>
	ExchangeView BuildExchange( PlayerWallet wallet )
	{
		var convert = tab == ExchangeTab.Convert;
		var spendsCrypto = SpendsCrypto();
		var spent = spendsCrypto ? PaymentsCurrency.Crypto : PaymentsCurrency.Dollars;
		var received = spendsCrypto ? PaymentsCurrency.Dollars : PaymentsCurrency.Crypto;
		var typedIn = unit;

		// The number typed means the currency being spent or the one coming back, and the label says which.
		var typingSpent = typedIn == spent;
		var typed = TypedAmount();
		var (spend, fee, gets) = Deal( typed, spendsCrypto, typedIn );

		var purse = spendsCrypto ? wallet?.Crypto ?? 0 : wallet?.Cash ?? 0;
		var affordable = Affordable( wallet );
		var feePercent = spendsCrypto ? Venue.SellFeePercent : Venue.BuyFeePercent;

		var hint = "";
		if ( purse < 1 )
			hint = NothingText( convert, spendsCrypto );
		else if ( typed > affordable )
			hint = $"You only have {AmountText( purse, spent )}.";
		else if ( typed > 0 && spend > Venue.MaxPerRequest )
			hint = $"At most {AmountText( Venue.MaxPerRequest, spent )} at a time.";

		return new ExchangeView
		{
			Tab = tab,
			Unit = typedIn,
			Amount = exchangeAmount,
			EntryLabel = convert ? "You convert" : typingSpent ? "You pay" : "You get",
			Available = typingSpent
				? $"Available {AmountText( affordable, typedIn )}"
				: $"Up to {AmountText( affordable, typedIn )}",
			Rate = $"1 {PaymentsUiState.CryptoUnit} = $1",
			FeeText = $"{feePercent}% · -{AmountText( fee, spent )}",
			ResultFigure = received == PaymentsCurrency.Dollars ? $"${gets:N0}" : $"{gets:N0}",
			ResultText = AmountText( gets, received ),
			Hint = hint,
			ActionLabel = convert
				? $"Convert to {UnitName( received )}"
				: spendsCrypto ? $"Sell {PaymentsUiState.CryptoUnit}" : $"Buy {PaymentsUiState.CryptoUnit}",
			Quick = QuickChips( tab, typedIn ),
		};
	}

	/// <summary>
	/// The four buttons under the amount: a share of everything there is for Convert, where the point is to change all of
	/// a holding, and a round amount in the currency being typed for Buy and Sell.
	/// </summary>
	static IReadOnlyList<QuickChip> QuickChips( ExchangeTab tab, PaymentsCurrency typedIn )
	{
		if ( tab == ExchangeTab.Convert )
			return [new( "25%", "p25" ), new( "50%", "p50" ), new( "75%", "p75" ), new( "Max", AllAmount )];

		return
		[
			new( AmountText( 10, typedIn ), "10" ),
			new( AmountText( 50, typedIn ), "50" ),
			new( AmountText( 100, typedIn ), "100" ),
			new( "Max", AllAmount ),
		];
	}

	/// <summary>The currency in a sentence, where the ticker would be too terse.</summary>
	static string UnitName( PaymentsCurrency of ) =>
		of == PaymentsCurrency.Dollars ? "dollars" : PaymentsUiState.CryptoUnit;

	/// <summary>The currency being spent, in a sentence.</summary>
	static string SpentName( bool spendsCrypto ) =>
		spendsCrypto ? PaymentsUiState.CryptoUnit : "dollars";

	/// <summary>What an empty purse says: the tab says whether the player meant to buy, to sell or to convert.</summary>
	static string NothingText( bool convert, bool spendsCrypto ) => convert
		? $"You don't have any {SpentName( spendsCrypto )} to convert."
		: spendsCrypto ? $"You don't have any {PaymentsUiState.CryptoUnit} to sell."
		: "You don't have any dollars to buy with.";

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
