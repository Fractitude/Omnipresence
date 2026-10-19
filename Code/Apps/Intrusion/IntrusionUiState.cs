namespace OmnipresenceApps;

public enum IntrusionScreen
{
	/// <summary>The dead servers that can be breached.</summary>
	Servers,

	/// <summary>The blueprints found so far.</summary>
	Blueprints,

	/// <summary>A terminal being cracked.</summary>
	Terminal,

	/// <summary>The password was found.</summary>
	Breached,

	/// <summary>Too many wrong guesses.</summary>
	Locked,
}

/// <summary>
/// What the Intrusion panel draws. Replaced whole by <see cref="IntrusionViewModel"/> on every change. Never holds the
/// password: a shared app's state goes to every player.
/// </summary>
public sealed record IntrusionUiState
{
	/// <summary>What a viewer draws until the host's state arrives.</summary>
	public static IntrusionUiState Empty { get; } = new();

	/// <summary>Goes up whenever anything else here changes. The panel's BuildHash is this.</summary>
	public int Revision { get; init; }

	public IntrusionScreen Screen { get; init; }

	/// <summary>False while there is nobody in the seat, no signal, or the account isn't loaded.</summary>
	public bool HasAccess { get; init; }

	/// <summary>Shown instead of the app when there is no access.</summary>
	public string Message { get; init; } = "";

	public IReadOnlyList<ServerRow> Servers { get; init; } = [];
	public IReadOnlyList<BlueprintRow> Blueprints { get; init; } = [];

	/// <summary>The server being cracked, on the Terminal, Breached and Locked screens.</summary>
	public string ServerName { get; init; } = "";
	public string ServerAddress { get; init; } = "";

	public int Attempts { get; init; }
	public int MaxAttempts { get; init; }

	public IReadOnlyList<DumpLine> Dump { get; init; } = [];
	public IReadOnlyList<string> Log { get; init; } = [];

	/// <summary>What was won, on the Breached screen. One line each.</summary>
	public IReadOnlyList<string> Rewards { get; init; } = [];
}

public sealed record ServerRow( string Id, string Name, string Address, string Layer, string Difficulty, string Blurb,
	string Reward, bool Cleared );

public sealed record BlueprintRow( string Name, string Description, string From );
