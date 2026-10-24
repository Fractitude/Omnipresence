namespace Sandbox;

/// <summary>
/// The two currencies that move between parties through <see cref="Transactions"/>. Credits are not here: they never
/// leave the player (see <c>docs/currency.md</c>).
/// </summary>
public enum TransactionCurrency
{
	/// <summary>Dollars.</summary>
	Cash,

	/// <summary>The dark web's crypto.</summary>
	Crypto,
}

/// <summary>
/// Why money moved. Stored by name in a player's history and in the ledger, so add new kinds at the end and never
/// rename one.
/// </summary>
public enum TxKind
{
	Other,

	/// <summary>Payment for a finished Hush order, or the receiver's own part of it.</summary>
	OrderPayout,

	/// <summary>A teammate's share of an order payout.</summary>
	OrgShare,

	/// <summary>A purchase from a shop that is not the Depot: the Hush shop, the Hardware Store.</summary>
	ShopPurchase,

	/// <summary>A purchase from the Depot.</summary>
	DepotPurchase,

	/// <summary>Crypto sold at an exchange, and the dollars paid for it when they come at once.</summary>
	ExchangeSell,

	/// <summary>Dollars spent on crypto at an exchange, and the crypto that came back.</summary>
	ExchangeBuy,

	/// <summary>Dollars from a delayed crypto sale arriving.</summary>
	CashOut,

	/// <summary>One player paying another, in the Payments app.</summary>
	PlayerPayment,

	/// <summary>A fee.</summary>
	Fee,

	/// <summary>The fee for registering an organization.</summary>
	RegistrationFee,

	/// <summary>Money given back.</summary>
	Refund,

	/// <summary>Dollars a player put on the ground (<see cref="CashDrops"/>). They are picked up without a transaction.</summary>
	DropCash,
}

/// <summary>
/// Why a transaction was refused. Nothing changed when it is not <see cref="None"/>.
/// </summary>
public enum TxFailure
{
	None,

	/// <summary>Transactions run on the host only.</summary>
	NotHost,

	/// <summary>The amount is zero, negative or more than any account can hold.</summary>
	InvalidAmount,

	/// <summary>A currency or kind that does not exist.</summary>
	InvalidKind,

	/// <summary>The source and the destination are the same party.</summary>
	SelfTransfer,

	/// <summary>A party that is no player and no registered system party.</summary>
	UnknownParty,

	/// <summary>A player's record is not in memory (not loaded yet, or no such player).</summary>
	Unavailable,

	/// <summary>The source holds less than the amount.</summary>
	Insufficient,

	/// <summary>The destination could not hold the amount under the cap.</summary>
	CapExceeded,

	/// <summary>The sender made too many payments in the last minute.</summary>
	RateLimited,

	/// <summary>A batch with no leg.</summary>
	Empty,
}

/// <summary>
/// One side of a transaction: a player (by Steam id) or a named system party such as the market. A system party has no
/// balance. Money paid to it is gone from the players' world and money it pays is made, and both are written down, so
/// the books say how much each system party took in or put out (<see cref="Transactions.NetOf"/>).
/// </summary>
public readonly record struct Party( ulong SteamId, string Id )
{
	/// <summary>A player.</summary>
	public static Party Player( ulong steamId ) => new( steamId, "" );

	/// <summary>A system party. It must be registered in <see cref="Parties"/>.</summary>
	public static Party System( string id ) => new( 0, id ?? "" );

	public bool IsPlayer => SteamId != 0;

	/// <summary>A stable text for the ledger: <c>p:76561198...</c> or <c>s:market</c>.</summary>
	public string Key => IsPlayer ? $"p:{SteamId}" : $"s:{Id}";
}

/// <summary>
/// The system parties money can come from and go to. Adding one is a constant and a label here.
/// </summary>
public static class Parties
{
	/// <summary>Money made from nothing: payouts from NPC buyers and the like.</summary>
	public const string World = "world";

	/// <summary>Where a Hush order's payment is held, and paid from.</summary>
	public const string HushEscrow = "hush-escrow";

	/// <summary>The Hush shop (crypto).</summary>
	public const string HushShop = "hush-shop";

	/// <summary>The Hardware Store.</summary>
	public const string HardwareStore = "hardware-store";

	/// <summary>The Depot.</summary>
	public const string Depot = "depot";

	/// <summary>Both exchanges: what they keep as the fee stays with them.</summary>
	public const string Exchange = "exchange";

	/// <summary>Registrations and fees.</summary>
	public const string Government = "government";

	/// <summary>The market.</summary>
	public const string Market = "market";

	// Lookup table nobody changes at run time, so readonly is fine (a hot reload only picks up edits).
	static readonly Dictionary<string, string> labels = new()
	{
		[World] = "The world",
		[HushEscrow] = "Hush escrow",
		[HushShop] = "Hush shop",
		[HardwareStore] = "Hardware Store",
		[Depot] = "Depot",
		[Exchange] = "Exchange",
		[Government] = "Government",
		[Market] = "Market",
	};

	public static bool IsKnown( string id ) => !string.IsNullOrEmpty( id ) && labels.ContainsKey( id );

	/// <summary>What a player sees for this party.</summary>
	public static string LabelOf( string id ) =>
		id is not null && labels.TryGetValue( id, out var label ) ? label : id ?? "";

	public static Party Of( string id ) => Party.System( id );
}

/// <summary>
/// One movement of money from <see cref="From"/> to <see cref="To"/>. The unit of work of <see cref="Transactions"/>:
/// several legs run together (all or none) as a group.
/// </summary>
/// <param name="Note">A short text for the history, such as the order id.</param>
/// <param name="Ref">An id of the thing it was about (an order, an item), for finding it again.</param>
public sealed record TxLeg(
	Party From,
	Party To,
	TransactionCurrency Currency,
	long Amount,
	TxKind Kind,
	string Note = null,
	string Ref = null )
{
	/// <summary>
	/// The record of the source player when the caller already holds it (a player who is away, in the offline
	/// processor). Without it the record is found by Steam id. The caller saves a record it passed in.
	/// </summary>
	public ProgressRecord FromRecord { get; init; }

	/// <summary>The same for the destination.</summary>
	public ProgressRecord ToRecord { get; init; }

	/// <summary>
	/// Move as much as the destination can hold, up to <see cref="Amount"/>, instead of refusing when it does not all
	/// fit. For a share that waits for room (the queue of an absent member). Only for a system source.
	/// </summary>
	public bool UpTo { get; init; }
}

/// <summary>
/// What a transaction did, or why it did not.
/// </summary>
public sealed record TxResult( TxFailure Failure, string Message, string Group, IReadOnlyList<long> Moved )
{
	public bool Ok => Failure == TxFailure.None;

	/// <summary>What the first leg moved (the amount, or for <see cref="TxLeg.UpTo"/> what fitted).</summary>
	public long Amount => Moved.Count > 0 ? Moved[0] : 0;

	public static TxResult Fail( TxFailure failure, string message ) => new( failure, message, null, [] );

	/// <summary>As the crime game's answer to a panel, with <paramref name="success"/> as the text when it worked.</summary>
	public CrimeResult ToCrimeResult( string success = "" ) =>
		Ok ? CrimeResult.Success( success ) : CrimeResult.Fail( Message );
}

/// <summary>
/// One transaction as a player's history keeps it: from that player's side. Plain data, saved in the
/// <see cref="ProgressRecord"/>.
/// </summary>
public sealed class TransactionEntry
{
	/// <summary>The most a player keeps. Older ones make room.</summary>
	public const int MaxKept = 200;

	/// <summary>Host-made, unique.</summary>
	public string Id { get; set; } = "";

	/// <summary>Shared by the legs of one logical movement (an order payout and its shares).</summary>
	public string Group { get; set; } = "";

	/// <summary>Game seconds (<see cref="WorldClock.Now"/>).</summary>
	public long At { get; set; }

	public TransactionCurrency Currency { get; set; }

	/// <summary>Positive when the player received it, negative when they paid it.</summary>
	public long Amount { get; set; }

	/// <summary>A <see cref="TxKind"/> name.</summary>
	public string Kind { get; set; } = "";

	/// <summary>The other party: a Steam id, or <c>s:{system id}</c>.</summary>
	public string Peer { get; set; } = "";

	/// <summary>The other party's name when it happened.</summary>
	public string PeerName { get; set; } = "";

	public string Note { get; set; } = "";

	public string Ref { get; set; } = "";
}
