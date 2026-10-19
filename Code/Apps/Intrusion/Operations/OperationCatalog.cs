using System;

namespace OmnipresenceApps;

/// <summary>
/// A break-in with four stages, found in the Wired: get in, cross the network, get the data out, cover your tracks. Every
/// stage has a quick way and a careful puzzle. The quick way is easy and loud; the puzzle is quiet if you solve it well.
/// </summary>
/// <param name="Id">Saved in the player's progress, so never rename one.</param>
/// <param name="MinHackLevel">The hacking level that unlocks it.</param>
/// <param name="HackXp">Hacking XP for a normal run. The grade scales it.</param>
/// <param name="Credits">Credits for a normal run with all the data. The grade and the data taken scale it.</param>
/// <param name="CodeLength">Entry puzzle: digits in the badge code.</param>
/// <param name="CodeDigits">Entry puzzle: the code uses 1 to this digit.</param>
/// <param name="CodeTries">Entry puzzle: wrong codes before the reader locks.</param>
/// <param name="SweepNodes">Cross puzzle: nodes between the door and the vault, both ends included.</param>
/// <param name="Sweepers">Cross puzzle: how many daemons patrol.</param>
/// <param name="SweepTicks">Cross puzzle: moves before the player is caught for good.</param>
/// <param name="RouteColumns">Exfil puzzle: relays the data has to hop through.</param>
/// <param name="RouteDecoys">Exfil puzzle: the share of relays that hide what they run until they are pinged.</param>
public sealed record OperationDef(
	string Id,
	string Name,
	string Address,
	string Layer,
	string Difficulty,
	string Blurb,
	int MinHackLevel,
	long HackXp,
	long Credits,
	int CodeLength,
	int CodeDigits,
	int CodeTries,
	int SweepNodes,
	int Sweepers,
	int SweepTicks,
	int RouteColumns,
	double RouteDecoys );

public static class OperationCatalog
{
	// A property, not a readonly field: see IntrusionCatalog.
	public static IReadOnlyList<OperationDef> Operations =>
	[
		new( "bluebird-archive", "Bluebird Archive", "13.1.08", "Layer 03", "Easy",
			"A university's after-hours archive. One daemon sweeps the stacks and nobody reads the logs.",
			1, 120, 300, 3, 5, 6, 6, 1, 20, 3, 0.15 ),
		new( "tachibana-subnet", "Tachibana Labs Subnet", "13.6.40", "Layer 05", "Medium",
			"Research data behind a sensor net. Every relay in the building reports to somebody.",
			3, 320, 1100, 4, 6, 7, 7, 2, 24, 4, 0.25 ),
		new( "eastern-calculus", "Eastern Calculus Vault", "13.7.13", "Layer 07", "Hard",
			"A private society's vault. They notice every line that goes missing.",
			6, 800, 3200, 5, 6, 8, 8, 3, 28, 5, 0.35 ),
	];

	public static OperationDef Find( string id ) => Operations.FirstOrDefault( x => x.Id == id );
}

public enum OperationStage
{
	Entry,
	Cross,
	Exfil,
	Cover,
	Done,
}

public enum OperationPhase
{
	/// <summary>Picking how to do the stage.</summary>
	Choose,

	/// <summary>Playing the puzzle of the stage.</summary>
	Puzzle,

	/// <summary>All four stages done.</summary>
	Complete,

	/// <summary>The alert hit the top. The run is over.</summary>
	Burned,
}

/// <summary>
/// How clean a finished run was. The number is what is saved as the best grade.
/// </summary>
public enum OperationGrade
{
	None = 0,
	Sloppy = 1,
	Noisy = 2,
	Clean = 3,
	Ghost = 4,
}

public static class OperationGrades
{
	/// <summary>Hacking XP is multiplied by this.</summary>
	public static double XpScale( OperationGrade grade ) => grade switch
	{
		OperationGrade.Ghost => 2.0,
		OperationGrade.Clean => 1.4,
		OperationGrade.Noisy => 1.0,
		OperationGrade.Sloppy => 0.6,
		_ => 0,
	};

	/// <summary>Credits are multiplied by this.</summary>
	public static double CreditScale( OperationGrade grade ) => grade switch
	{
		OperationGrade.Ghost => 1.75,
		OperationGrade.Clean => 1.25,
		OperationGrade.Noisy => 1.0,
		OperationGrade.Sloppy => 0.6,
		_ => 0,
	};

	public static string Describe( OperationGrade grade ) => grade switch
	{
		OperationGrade.Ghost => "No trace, barely noticed, all the data. They will never know.",
		OperationGrade.Clean => "No trace left. Someone may have noticed something odd.",
		OperationGrade.Noisy => "A few traces. A careful admin will find them.",
		OperationGrade.Sloppy => "You got out, but you left a mess.",
		_ => "",
	};
}

public sealed record OperationChoice( string Id, string Title, string Detail, bool Enabled, bool Loud );

public sealed record CodeGuessRow( string Guess, int Exact, int Near );

/// <summary>Never holds the code: a shared app's state goes to every player.</summary>
public sealed record CodeView( int Length, int Digits, int TriesLeft, int MaxTries, string Pending,
	IReadOnlyList<CodeGuessRow> History );

/// <param name="Forecast">
/// One string per tick from now, one character per node: '#' is a sweeper, 'P' is you, '.' is empty.
/// </param>
public sealed record SweepView( int Nodes, int Player, int TicksLeft, IReadOnlyList<string> Forecast );

public sealed record RelayCell( int Column, int Row, string Banner, bool Known, bool Reachable, bool Picked );

/// <param name="Cells">Column by column, three rows to a column. Never says which relays are watched unless known.</param>
public sealed record RouteView( int Columns, int NextColumn, IReadOnlyList<RelayCell> Cells );

public sealed record ScrubLine( int Index, string Text, bool Selected );

public sealed record ScrubView( string Tag, int Mine, int Selected, IReadOnlyList<ScrubLine> Lines );

/// <summary>
/// What the panel draws of a run in progress. At most one of the four puzzle views is set.
/// </summary>
public sealed record OperationView
{
	public string Name { get; init; } = "";
	public string Address { get; init; } = "";

	public int Alert { get; init; }
	public string AlertLabel { get; init; } = "";
	public int Traces { get; init; }

	/// <summary>How much of the data is out, in percent.</summary>
	public int DataPercent { get; init; }

	public OperationStage Stage { get; init; }
	public OperationPhase Phase { get; init; }
	public string StageTitle { get; init; } = "";
	public string StageText { get; init; } = "";

	public IReadOnlyList<OperationChoice> Choices { get; init; } = [];
	public IReadOnlyList<string> Log { get; init; } = [];

	public CodeView Code { get; init; }
	public SweepView Sweep { get; init; }
	public RouteView Route { get; init; }
	public ScrubView Scrub { get; init; }

	/// <summary>On the Complete screen: the grade name, what it means and the highest the alert got.</summary>
	public string Grade { get; init; } = "";
	public string GradeNote { get; init; } = "";
	public int Peak { get; init; }
}
