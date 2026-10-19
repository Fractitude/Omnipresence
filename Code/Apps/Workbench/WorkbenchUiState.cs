namespace OmnipresenceApps;

public enum BenchStatus
{
	/// <summary>Found, not built, and the player has the dollars.</summary>
	Buildable,

	/// <summary>Found and not built, but the player can't pay yet.</summary>
	TooExpensive,

	/// <summary>Already built.</summary>
	Built,
}

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

	public string Balance { get; init; } = "";

	public IReadOnlyList<BenchRow> Rows { get; init; } = [];

	/// <summary>What went wrong with the last attempt, or what was just built.</summary>
	public string Notice { get; init; } = "";
}

public sealed record BenchRow( string Id, string Name, string Description, string Effect, string Cost, BenchStatus Status );
