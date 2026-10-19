namespace OmnipresenceApps;

public enum PaymentsScreen
{
	/// <summary>The account, the players to pay and the history.</summary>
	Home,

	/// <summary>Choosing the amount for the player picked on Home.</summary>
	Send,

	/// <summary>The payment went through.</summary>
	Success,
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

	public string Owner { get; init; } = "";
	public string Balance { get; init; } = "";

	/// <summary>The players that can be paid.</summary>
	public IReadOnlyList<PayeeRow> Payees { get; init; } = [];

	/// <summary>The newest payment first.</summary>
	public IReadOnlyList<PaymentRow> History { get; init; } = [];

	/// <summary>The player being paid, on the Send and Success screens.</summary>
	public PayeeRow Target { get; init; }

	/// <summary>The digits typed so far. Changing it doesn't redraw, so typing keeps the caret.</summary>
	public string Amount { get; init; } = "";

	/// <summary>What was sent, already formatted, on the Success screen.</summary>
	public string SentAmount { get; init; } = "";

	/// <summary>What went wrong with the last attempt.</summary>
	public string Notice { get; init; } = "";

	/// <summary>The first letter of a name, for its avatar.</summary>
	public static string InitialOf( string name ) =>
		string.IsNullOrWhiteSpace( name ) ? "?" : name.Trim()[..1].ToUpperInvariant();
}

public sealed record PayeeRow( string Id, string Name )
{
	public string Initial => PaymentsUiState.InitialOf( Name );
}

public sealed record PaymentRow( string Peer, string Amount, string When, bool Incoming )
{
	public string Initial => PaymentsUiState.InitialOf( Peer );
}
