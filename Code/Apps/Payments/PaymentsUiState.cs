namespace OmnipresenceApps;

public enum PaymentsScreen
{
	/// <summary>The account, the players to pay, the exchange and the history.</summary>
	Home,

	/// <summary>Choosing the amount, and the currency, for the player picked on Home.</summary>
	Send,

	/// <summary>Changing dollars and crypto at the Government Exchange.</summary>
	Exchange,

	/// <summary>The payment went through.</summary>
	Success,
}

/// <summary>
/// What the Exchange screen is doing: buying Ethereum with dollars, selling it, or converting either way, with the
/// direction chosen on the screen itself.
/// </summary>
public enum ExchangeTab
{
	/// <summary>Dollars in, Ethereum out.</summary>
	Buy,

	/// <summary>Ethereum in, dollars out.</summary>
	Sell,

	/// <summary>Either way round, picked on the screen.</summary>
	Convert,
}

/// <summary>
/// One of the two things the app moves: what a payment to another player carries, and what an exchange amount is typed
/// in.
/// </summary>
public enum PaymentsCurrency
{
	/// <summary>Dollars.</summary>
	Dollars,

	/// <summary>The dark web's crypto.</summary>
	Crypto,
}

/// <summary>
/// What the Payments panel draws. Replaced whole by <see cref="PaymentsViewModel"/> on every change.
/// </summary>
public sealed record PaymentsUiState
{
	/// <summary>What a viewer draws until the host's state arrives.</summary>
	public static PaymentsUiState Empty { get; } = new();

	/// <summary>Goes up whenever anything else here changes. The panel's BuildHash is this.</summary>
	public int Revision { get; init; }

	public PaymentsScreen Screen { get; init; }

	/// <summary>False while nobody is in the seat or their account isn't loaded.</summary>
	public bool HasAccount { get; init; }

	/// <summary>Shown instead of the account when there is none.</summary>
	public string Message { get; init; } = "";

	/// <summary>While the computer is offline: why. The window says so in place of everything else.</summary>
	public string OfflineHint { get; init; }

	public string Owner { get; init; } = "";

	/// <summary>Dollars, already formatted with the sign.</summary>
	public string Balance { get; init; } = "";

	/// <summary>Crypto, already formatted.</summary>
	public string Crypto { get; init; } = "";

	/// <summary>The players that can be paid.</summary>
	public IReadOnlyList<PayeeRow> Payees { get; init; } = [];

	/// <summary>The newest payment first.</summary>
	public IReadOnlyList<PaymentRow> History { get; init; } = [];

	/// <summary>The player being paid, on the Send and Success screens.</summary>
	public PayeeRow Target { get; init; }

	/// <summary>What the Send screen is about to send.</summary>
	public PaymentsCurrency Currency { get; init; }

	/// <summary>The digits typed so far. Changing it doesn't redraw, so typing keeps the caret.</summary>
	public string Amount { get; init; } = "";

	/// <summary>What was sent, already formatted, on the Success screen.</summary>
	public string SentAmount { get; init; } = "";

	/// <summary>What went wrong, or right, with the last attempt.</summary>
	public string Notice { get; init; } = "";

	/// <summary>Whether <see cref="Notice"/> is the exchange's good news rather than a refusal.</summary>
	public bool NoticeGood { get; init; }

	/// <summary>The exchange the Exchange screen deals with.</summary>
	public string ExchangeName { get; init; } = "";

	/// <summary>The Exchange screen; null on every other one.</summary>
	public ExchangeView Exchange { get; init; }

	/// <summary>The first letter of a name, for its avatar.</summary>
	public static string InitialOf( string name ) =>
		string.IsNullOrWhiteSpace( name ) ? "?" : name.Trim()[..1].ToUpperInvariant();

	/// <summary>
	/// What an Ethereum amount carries after its figure in place of the word: the ticker. One place for it, so it is the
	/// same on the card, in the activity list, in the amount field and on the success screen.
	/// </summary>
	public static string CryptoUnit => "ETH";

	/// <summary>The currency's name, for the places that want a word rather than the ticker.</summary>
	public static string CryptoName => "Ethereum";
}

public sealed record PayeeRow( string Id, string Name )
{
	public string Initial => PaymentsUiState.InitialOf( Name );
}

/// <summary>
/// One of the quick buttons under the amount: what it says, the key the view model reads it as, and the digits it fills
/// the field with.
/// </summary>
public sealed record QuickChip( string Label, string Key, string Digits );

/// <summary>
/// The Exchange screen, worked out whole by <see cref="PaymentsViewModel"/>: the amount being typed, what it deals at
/// the Government Exchange and the buttons for it. A record like the state around it, replaced whole on every change.
/// </summary>
public sealed record ExchangeView
{
	public ExchangeTab Tab { get; init; }

	/// <summary>The currency the amount is typed in.</summary>
	public PaymentsCurrency Unit { get; init; }

	/// <summary>The currency that comes back: the other one.</summary>
	public PaymentsCurrency To => Unit == PaymentsCurrency.Dollars ? PaymentsCurrency.Crypto : PaymentsCurrency.Dollars;

	/// <summary>The digits typed so far.</summary>
	public string Amount { get; init; } = "";

	/// <summary>What the typed number is, such as "You pay".</summary>
	public string EntryLabel { get; init; } = "";

	/// <summary>How much of the typed currency can be dealt with, with its sign or ticker.</summary>
	public string Available { get; init; } = "";

	/// <summary>The rate before the exchange's cut.</summary>
	public string Rate { get; init; } = "";

	/// <summary>The cut, as a percentage and as money: "2% · -$2".</summary>
	public string FeeText { get; init; } = "";

	/// <summary>What comes back: as a figure for the big result row, and as an amount with its currency.</summary>
	public string ResultFigure { get; init; } = "0";

	public string ResultText { get; init; } = "";

	/// <summary>A line about the amount: that there is nothing to deal with, or that it is more than there is.</summary>
	public string Hint { get; init; } = "";

	/// <summary>What the button at the bottom does.</summary>
	public string ActionLabel { get; init; } = "";

	/// <summary>The four quick buttons.</summary>
	public IReadOnlyList<QuickChip> Quick { get; init; } = [];

	/// <summary>
	/// How many trades have gone through while the screen was open. The panel empties its amount field when this moves:
	/// the field keeps its own text, so the host clearing the amount doesn't reach it on its own.
	/// </summary>
	public int Settled { get; init; }
}

/// <summary>
/// One line of the activity list: who, how much, and what it was. The figure is drawn without its unit — the row puts
/// <see cref="PaymentsUiState.CryptoUnit"/> after an Ethereum amount — and the what and the when are separate so the
/// row can lay them out on their own line.
/// </summary>
public sealed record PaymentRow( string Peer, string Amount, string Kind, string When, bool Incoming, bool Crypto )
{
	public string Initial => PaymentsUiState.InitialOf( Peer );
}
