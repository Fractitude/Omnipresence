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

/// <summary>What a payment to another player carries.</summary>
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

	/// <summary>The exchange the Exchange screen deals with.</summary>
	public string ExchangeName { get; init; } = "";

	public string ExchangeTagline { get; init; } = "";

	/// <summary>What selling and buying at that exchange cost, in a sentence.</summary>
	public string SellRate { get; init; } = "";

	public string BuyRate { get; init; } = "";

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
/// One line of the activity list: who, how much, and what it was. The figure is drawn without its unit — the row puts
/// <see cref="PaymentsUiState.CryptoUnit"/> after an Ethereum amount — and the what and the when are separate so the
/// row can lay them out on their own line.
/// </summary>
public sealed record PaymentRow( string Peer, string Amount, string Kind, string When, bool Incoming, bool Crypto )
{
	public string Initial => PaymentsUiState.InitialOf( Peer );
}
