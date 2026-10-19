namespace OmnipresenceApps;

public enum StoreScreen
{
	/// <summary>What the store sells.</summary>
	Shop,

	/// <summary>The player's packages on their way.</summary>
	Orders,

	/// <summary>The staff panel, open after breaking in.</summary>
	Staff,

	/// <summary>The staff login, before breaking in.</summary>
	Login,

	/// <summary>The terminal being cracked.</summary>
	Terminal,

	/// <summary>The password was found.</summary>
	Breached,

	/// <summary>Too many wrong guesses: the store locked the player out.</summary>
	Locked,
}

public enum ShopStatus
{
	/// <summary>The player can order it.</summary>
	Buy,

	/// <summary>The player can't pay for it yet.</summary>
	TooExpensive,

	/// <summary>It needs a blueprint the player hasn't found.</summary>
	NeedsBlueprint,
}

/// <summary>
/// What the Hardware Store panel draws. Replaced whole by <see cref="HardwareStoreViewModel"/> on every change.
/// </summary>
public sealed record HardwareStoreUiState
{
	/// <summary>What a viewer draws until the host's state arrives.</summary>
	public static HardwareStoreUiState Empty { get; } = new();

	/// <summary>Goes up whenever anything else here changes. The panel's BuildHash is this.</summary>
	public int Revision { get; init; }

	public StoreScreen Screen { get; init; }

	/// <summary>False while nobody is in the seat, this isn't the store's computer, or the account isn't loaded.</summary>
	public bool HasAccess { get; init; }

	/// <summary>Shown instead of the store when there is no access.</summary>
	public string Message { get; init; } = "";

	public string Balance { get; init; } = "";

	/// <summary>True while the staff panel is open: the tab shows, and prices are cut.</summary>
	public bool StaffOpen { get; init; }

	/// <summary>How long the staff panel stays open, already formatted.</summary>
	public string StaffRemaining { get; init; } = "";

	public IReadOnlyList<ShopRow> Shop { get; init; } = [];
	public IReadOnlyList<ShipmentRow> Shipments { get; init; } = [];
	public IReadOnlyList<StaffOrderRow> StaffOrders { get; init; } = [];

	/// <summary>What happened with the last click.</summary>
	public string Notice { get; init; } = "";

	/// <summary>How long the player can't try the staff login again, already formatted. Empty when they can.</summary>
	public string Lockout { get; init; } = "";

	/// <summary>The terminal being cracked, on the Terminal, Breached and Locked screens.</summary>
	public int Attempts { get; init; }
	public int MaxAttempts { get; init; }
	public IReadOnlyList<DumpLine> Dump { get; init; } = [];
	public IReadOnlyList<string> Log { get; init; } = [];

	/// <summary>The word board of a Level 1 terminal. Null on a Level 2 one.</summary>
	public GuessBoard Board { get; init; }

	/// <summary>What the break-in gave, on the Breached screen. One line each.</summary>
	public IReadOnlyList<string> Rewards { get; init; } = [];
}

/// <param name="WasPrice">The full price while a discount applies, otherwise empty.</param>
/// <param name="Hint">What is needed when the status is <see cref="ShopStatus.NeedsBlueprint"/>.</param>
public sealed record ShopRow(
	string Id,
	string Name,
	string Slot,
	string Description,
	string Effect,
	string Price,
	string WasPrice,
	ShopStatus Status,
	string Hint );

public sealed record ShipmentRow( string Name, string Eta, bool Rerouted );

public sealed record StaffOrderRow( string Id, string Customer, string Name, string Effect, bool Taken );
