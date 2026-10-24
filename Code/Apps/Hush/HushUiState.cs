namespace OmnipresenceApps;

public enum HushTab
{
	Orders,
	YourOrders,
	Shop,
	Shipments,
	Wallet,
	Reviews,
	Exchange,
}

/// <summary>
/// What the Hush panel draws. Replaced whole by <see cref="HushViewModel"/> on every change. It holds only what the
/// player may see: nothing about the buyers beyond their handle and note.
/// </summary>
public sealed record HushUiState
{
	/// <summary>What a viewer draws until the host's state arrives.</summary>
	public static HushUiState Empty { get; } = new();

	/// <summary>Goes up whenever anything else here changes. The panel's BuildHash is this.</summary>
	public int Revision { get; init; }

	public HushTab Tab { get; init; }

	/// <summary>False while the app can't work (nobody in the seat, in the Wired, offline, account not loaded).</summary>
	public bool HasAccount { get; init; }

	/// <summary>Shown instead of the app when there is no account: why.</summary>
	public string Message { get; init; } = "";

	/// <summary>True when the player is in the Wired: Hush only works at home.</summary>
	public bool NoSignal { get; init; }

	/// <summary>While the computer is offline: why. The window says so in place of everything else.</summary>
	public string OfflineHint { get; init; }

	/// <summary>What went wrong, or right, with the last action.</summary>
	public string Notice { get; init; } = "";

	public string Crypto { get; init; } = "";
	public string Cash { get; init; } = "";
	public string Level { get; init; } = "";
	public string Rating { get; init; } = "";
	public string Trades { get; init; } = "";
	public string FeeText { get; init; } = "";
	public string Escrow { get; init; } = "";

	/// <summary>The category chosen on the Orders tab: a category name, or empty for all.</summary>
	public string Category { get; init; } = "";

	/// <summary>The chips above the open orders: All, then one per category on the board, with how many.</summary>
	public IReadOnlyList<FilterChip> Categories { get; init; } = [];

	/// <summary>The status chosen on Your orders: a <see cref="MyOrderStatus"/> name, or empty for all.</summary>
	public string StatusFilter { get; init; } = "";

	/// <summary>The chips above Your orders: All, then one per status, with how many.</summary>
	public IReadOnlyList<FilterChip> StatusChips { get; init; } = [];

	/// <summary>Orders waiting to be packed or sent, whatever the filter, for the tab label.</summary>
	public int ToDo { get; init; }

	/// <summary>The orders on the board (of the chosen category), soonest to leave first.</summary>
	public IReadOnlyList<OpenOrderRow> Open { get; init; } = [];

	/// <summary>The player's accepted orders (of the chosen status), newest first.</summary>
	public IReadOnlyList<MyOrderRow> Mine { get; init; } = [];

	/// <summary>What the shop sells, with the price for the chosen quantity.</summary>
	public IReadOnlyList<HushShopRow> Shop { get; init; } = [];

	/// <summary>The quantity chosen in the shop, and the ones offered.</summary>
	public int ShopQuantity { get; init; } = 1;

	public IReadOnlyList<int> ShopQuantities { get; init; } = [];

	/// <summary>Supplies bought in the shop that are on their way or waiting in the mailbox, newest first.</summary>
	public IReadOnlyList<HushShipmentRow> Incoming { get; init; } = [];

	/// <summary>Packed and sent parcels, newest first.</summary>
	public IReadOnlyList<HushShipmentRow> Shipments { get; init; } = [];

	public IReadOnlyList<ReviewRow> Reviews { get; init; } = [];

	/// <summary>Dollars on their way from the Exchange.</summary>
	public IReadOnlyList<string> Cashouts { get; init; } = [];

	public string SellRate { get; init; } = "";
	public string BuyRate { get; init; } = "";
}

/// <summary>One filter chip. Key is what the action carries ("" for All).</summary>
public sealed record FilterChip( string Key, string Label, int Count );

public sealed record OpenOrderRow(
	string Id,
	string Handle,
	string Note,
	string Product,
	int Count,
	int MinQuality,
	string PriceEach,
	string Total,
	string Expires,
	bool CanAccept,
	string Urgency = "" );

public enum MyOrderStatus
{
	/// <summary>Waiting to be packed.</summary>
	ToPack,

	/// <summary>Packed. Take the parcel to the mailbox.</summary>
	ToSend,

	Shipped,

	/// <summary>Delivered, the buyer has reviewed it and escrow is about to release.</summary>
	Escrow,

	Paid,
	Cancelled,
}

public sealed record MyOrderRow(
	string Id,
	string Handle,
	string Product,
	int Count,
	int MinQuality,
	string Total,
	MyOrderStatus Status,
	string Detail,
	string Urgency = "" );

/// <summary>One thing for sale. Reason says why it can't be bought now, or is empty.</summary>
public sealed record HushShopRow( string Id, string Name, string Description, string PriceEach, string Total,
	string Delivery, bool CanBuy, string Reason );

public sealed record HushShipmentRow( string Title, string Detail, bool Delivered );

public sealed record ReviewRow( string Handle, int Stars, string Text, string When );
