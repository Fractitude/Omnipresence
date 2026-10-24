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

	public IReadOnlyList<DepotRow> Shop { get; init; } = [];

	public IReadOnlyList<DepotIncomingRow> Incoming { get; init; } = [];
}

public sealed record DepotRow(
	string Id, string Name, string Description, string Icon, string Price, string Delivery, bool CanBuy, string Reason );

public sealed record DepotIncomingRow( string Name, string Detail );
