namespace OmnipresenceApps;

/// <summary>
/// What the Workbench panel draws. Replaced whole by <see cref="WorkbenchViewModel"/> on every change.
/// </summary>
public sealed record WorkbenchUiState
{
	/// <summary>What a viewer draws until the host's state arrives.</summary>
	public static WorkbenchUiState Empty { get; } = new();

	/// <summary>Goes up whenever anything else here changes. The panel's BuildHash is this.</summary>
	public int Revision { get; init; }

	/// <summary>False while there is nobody in the seat, the player is in the Wired, or the account isn't loaded.</summary>
	public bool HasAccess { get; init; }

	/// <summary>Shown instead of the bench when there is no access.</summary>
	public string Message { get; init; } = "";

	/// <summary>Every slot of the rig, filled or empty.</summary>
	public IReadOnlyList<RigSlotRow> Rig { get; init; } = [];

	/// <summary>What the installed hardware adds to a terminal, already formatted.</summary>
	public string Summary { get; init; } = "";

	/// <summary>The hardware that arrived and isn't installed, one row per kind.</summary>
	public IReadOnlyList<InventoryRow> Inventory { get; init; } = [];

	/// <summary>How many packages are still on their way, already formatted. Empty when none.</summary>
	public string Incoming { get; init; } = "";

	/// <summary>What happened with the last click.</summary>
	public string Notice { get; init; } = "";
}

/// <param name="Id">The installed piece, or empty for an empty slot.</param>
public sealed record RigSlotRow( string Slot, string Id, string Name, string Effect );

/// <param name="CanInstall">False when the rig has no room left for its kind.</param>
public sealed record InventoryRow( string Id, string Name, string Slot, string Effect, int Count, bool CanInstall );
