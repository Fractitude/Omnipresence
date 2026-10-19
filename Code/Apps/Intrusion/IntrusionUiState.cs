namespace OmnipresenceApps;

public enum IntrusionScreen
{
	/// <summary>The dead servers that can be breached.</summary>
	Servers,

	/// <summary>The operations: four-stage break-ins graded on how clean they are.</summary>
	Operations,

	/// <summary>An operation being run, or its result.</summary>
	Operation,

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
	public IReadOnlyList<OperationRow> Operations { get; init; } = [];
	public IReadOnlyList<BlueprintRow> Blueprints { get; init; } = [];

	/// <summary>The hacking level of the player, and how far through it they are, from 0 to 1.</summary>
	public int HackLevel { get; init; } = 1;
	public float HackProgress { get; init; }

	/// <summary>The operation being run, on the Operation screen.</summary>
	public OperationView Operation { get; init; }

	/// <summary>The server being cracked, on the Terminal, Breached and Locked screens.</summary>
	public string ServerName { get; init; } = "";
	public string ServerAddress { get; init; } = "";

	public int Attempts { get; init; }
	public int MaxAttempts { get; init; }

	/// <summary>The memory dump and its log, on a Level 2 terminal.</summary>
	public IReadOnlyList<DumpLine> Dump { get; init; } = [];
	public IReadOnlyList<string> Log { get; init; } = [];

	/// <summary>The word board of a Level 1 terminal. Null on a Level 2 one.</summary>
	public GuessBoard Board { get; init; }

	/// <summary>What was won, on the Breached screen. One line each.</summary>
	public IReadOnlyList<string> Rewards { get; init; } = [];
}

public sealed record ServerRow( string Id, string Name, string Address, string Layer, string Difficulty, string Blurb,
	string Reward, bool Cleared, int Level );

/// <param name="Best">The best grade so far, or empty if never finished.</param>
/// <param name="Locked">The player's hacking level is below <paramref name="MinLevel"/>.</param>
public sealed record OperationRow( string Id, string Name, string Address, string Layer, string Difficulty, string Blurb,
	string Reward, int MinLevel, bool Locked, string Best );

public sealed record BlueprintRow( string Name, string Description, string From );
