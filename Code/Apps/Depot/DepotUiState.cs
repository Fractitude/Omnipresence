namespace OmnipresenceApps;

/// <summary>
/// What the Depot panel draws. Replaced whole by <see cref="DepotViewModel"/> on every change.
/// </summary>
public sealed record DepotUiState
{
	/// <summary>What a viewer draws until the host's state arrives.</summary>
	public static DepotUiState Empty { get; } = new();

	/// <summary>Goes up whenever anything else here changes. The panel's BuildHash is this.</summary>
	public int Revision { get; init; }

	/// <summary>False while the app cannot work (nobody in the seat, in the Wired, offline, account not loaded).</summary>
	public bool HasAccount { get; init; }

	/// <summary>Shown instead of the app when there is no account: why.</summary>
	public string Message { get; init; } = "";

	/// <summary>True when the player is in the Wired: the Depot only works from home.</summary>
	public bool NoSignal { get; init; }

	/// <summary>While the computer is offline: why. The window says so in place of everything else.</summary>
	public string OfflineHint { get; init; }

	/// <summary>What happened with the last click.</summary>
	public string Notice { get; init; } = "";

	public string Cash { get; init; } = "";

	/// <summary>True when a delivery point is near this computer: the Depot can hand an order over at the store.</summary>
	public bool StoreAvailable { get; init; }

	/// <summary>True when the next order is collected at the store instead of mailed home.</summary>
	public bool StoreChosen { get; init; }

	/// <summary>How store pickup works here, worded for the panel, or why it is not offered.</summary>
	public string StoreDetail { get; init; } = "";

	/// <summary>How a home delivery works, worded for the panel.</summary>
	public string HomeDetail { get; init; } = "";

	public IReadOnlyList<DepotRow> Shop { get; init; } = [];

	public IReadOnlyList<DepotIncomingRow> Incoming { get; init; } = [];
}

public sealed record DepotRow(
	string Id, string Name, string Description, string Icon, string Price, string Delivery, bool CanBuy, string Reason );

public sealed record DepotIncomingRow( string Name, string Detail );
