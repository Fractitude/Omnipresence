using System;

namespace OmnipresenceApps;

/// <summary>
/// Everything Payments does: shows the account of the player in the seat, and walks them from choosing a player, to
/// choosing an amount, to the money being sent. Runs in the host's copy only (the app is shared), because the money is
/// the host's.
/// </summary>
/// <remarks>
/// Who is paying is always the player in the seat, worked out from the computer. Nothing an action carries says so, and
/// the recipient is looked up among the players in the game, so an action can't pay from or to anyone else's say-so.
/// </remarks>
public sealed class PaymentsViewModel
{
	public const string ChooseAction = "choose";
	public const string BackAction = "back";
	public const string AmountAction = "amount";
	public const string SendAction = "send";

	/// <summary>The most one payment can be, so a typo can't empty an account.</summary>
	public const long MaxPayment = 1_000_000_000;

	/// <summary>Seconds the success screen stays before it goes home by itself.</summary>
	const float SuccessSeconds = 3f;

	const int MaxDigits = 12;

	readonly Func<Connection> getSeat;
	readonly Func<NaviComputer> getComputer;

	PaymentsScreen screen;
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

		// Payments need the network: a payment being chosen is dropped when it goes down.
		if ( screen == PaymentsScreen.Send && !GameAppNetwork.IsOnline( getComputer() ) )
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
		if ( screen != PaymentsScreen.Home || !GameAppNetwork.IsOnline( getComputer() ) || Payees( getSeat() ).All( x => x.SteamId.ToString() != id ) )
			return;

		screen = PaymentsScreen.Send;
		targetId = id;
		amount = "";
		notice = "";
	}

	/// <summary>
	/// Keep the typed digits without drawing again: the panel already shows them, and a redraw would move the caret.
	/// </summary>
	void SetAmount( string text )
	{
		if ( screen != PaymentsScreen.Send )
			return;

		amount = new string( (text ?? "").Where( char.IsAsciiDigit ).Take( MaxDigits ).ToArray() ).TrimStart( '0' );
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
			notice = $"At most ${MaxPayment:N0} at a time.";
		else if ( PlayerWallet.For( seat ) is not { } wallet || value > wallet.Cash )
			notice = "Not enough money.";
		else if ( PlayerWallet.For( to ) is not { } target || !wallet.TryPayCash( target, value ) )
			notice = "That payment can't be made.";
		else
		{
			screen = PaymentsScreen.Success;
			sentAmount = $"${value:N0}";
			sentTo = to.DisplayName;
			sinceSuccess = 0;
			notice = "";
		}
	}

	string Signature( Connection seat, PlayerProgress progress, List<Connection> payees ) =>
		HashCode.Combine(
			seat?.Id,
			GameAppNetwork.IsOnline( getComputer() ),
			getComputer()?.HasNetworkCard,
			(progress?.Version, PlayerWallet.For( seat )?.Version),
			screen,
			targetId,
			notice,
			string.Join( ',', payees.Select( x => $"{x.Id}:{x.DisplayName}" ) ) ).ToString();

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

		var state = new PaymentsUiState { Revision = ++revision, Screen = screen, Notice = notice };

		if ( !GameAppNetwork.IsOnline( getComputer() ) )
			return state with { Screen = PaymentsScreen.Home, OfflineHint = GameAppNetwork.OfflineHint( getComputer() ) };

		if ( seat is null )
			return state with { Screen = PaymentsScreen.Home, Message = "Sit down to see your account." };

		if ( progress is not { Loaded: true } )
			return state with { Screen = PaymentsScreen.Home, Message = "Loading your account..." };

		var rows = payees.Select( x => new PayeeRow( x.SteamId.ToString(), x.DisplayName ) ).ToList();
		var target = screen == PaymentsScreen.Send
			? rows.FirstOrDefault( x => x.Id == targetId )
			: screen == PaymentsScreen.Success ? new PayeeRow( "", sentTo ) : null;

		return state with
		{
			HasAccount = true,
			Owner = seat.DisplayName,
			Balance = $"${PlayerWallet.For( seat )?.Cash:N0}",
			Payees = rows,
			History = progress.Payments.Reverse().Select( ToRow ).ToList(),
			Target = target,
			Amount = amount,
			SentAmount = sentAmount,
		};
	}

	static PaymentRow ToRow( PaymentRecord payment )
	{
		var incoming = payment.Amount > 0;
		var when = WorldClock.Format( payment.At );
		var amount = $"{(incoming ? "+" : "-")}${Math.Abs( payment.Amount ):N0}";

		return new PaymentRow( payment.PeerName, amount, when, incoming );
	}
}
