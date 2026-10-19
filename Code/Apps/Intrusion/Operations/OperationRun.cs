using System;

namespace OmnipresenceApps;

/// <summary>
/// One run at an operation: the alert, the traces left behind, the data taken and the stage the player is at. Each stage
/// can be done the quick way (easy, loud, leaves traces) or with a puzzle (quiet if done well). The alert decides how it
/// ends: at the top the player is burned and the run is over. How clean the finished run is decides the grade, and the
/// grade the reward.
/// </summary>
/// <remarks>
/// No engine objects, so the rules run without a panel. The puzzles keep their secrets; this builds a view that has none.
/// </remarks>
public sealed class OperationRun : IOperationHost
{
	public const int MaxAlert = 100;
	public const int MaxTraces = 10;

	/// <summary>From here on every alert gain is bigger: they are looking.</summary>
	public const int SuspiciousAt = 50;

	/// <summary>From here on the quick ways are closed and every alert gain is bigger still.</summary>
	public const int LockdownAt = 80;

	const int MaxLog = 6;

	readonly Random random;
	readonly double noiseScale;
	readonly int bonusTries;
	readonly List<string> log = [];

	StagePuzzle puzzle;

	/// <param name="noiseScale">What the alert is multiplied by, from the player's hacking level.</param>
	/// <param name="bonusTries">Extra badge codes allowed, from the player's hardware.</param>
	public OperationRun( OperationDef def, Random random, double noiseScale, int bonusTries )
	{
		Def = def;
		this.random = random;
		this.noiseScale = noiseScale;
		this.bonusTries = bonusTries;

		Note( $"Connected to {def.Name}." );
	}

	public OperationDef Def { get; }
	public OperationStage Stage { get; private set; }
	public OperationPhase Phase { get; private set; }
	public int Alert { get; private set; }

	/// <summary>The highest the alert got. The grade is worked out from it.</summary>
	public int Peak { get; private set; }

	public int Traces { get; private set; }

	/// <summary>How much of the data is out, from 0 to 1.</summary>
	public double Data { get; private set; }

	public OperationGrade Grade { get; private set; }

	public bool Lockdown => Alert >= LockdownAt;

	public long HackXpReward => (long)Math.Round( Def.HackXp * OperationGrades.XpScale( Grade ) );
	public long CreditReward => (long)Math.Round( Def.Credits * OperationGrades.CreditScale( Grade ) * Data );
	public long GhostBonus => Def.Credits;

	/// <summary>What a burned run still teaches.</summary>
	public long BurnedHackXp => Def.HackXp / 10;

	double Pressure => Alert >= LockdownAt ? 1.5 : Alert >= SuspiciousAt ? 1.25 : 1.0;

	string AlertLabel => Alert switch
	{
		>= LockdownAt => "LOCKDOWN",
		>= SuspiciousAt => "Suspicious",
		>= 25 => "Noticed",
		_ => "Quiet",
	};

	void IOperationHost.Raise( double alert ) => Raise( alert );

	void IOperationHost.AddTrace( int count ) => Traces = Math.Clamp( Traces + count, 0, MaxTraces );

	void IOperationHost.SetTraces( int count ) => Traces = Math.Clamp( count, 0, MaxTraces );

	void IOperationHost.SetData( double fraction ) => Data = Math.Clamp( fraction, 0, 1 );

	void IOperationHost.Note( string text ) => Note( text );

	/// <summary>
	/// One thing the player did: "choose" and a way, or an action of the puzzle that is being played.
	/// </summary>
	public void Act( string[] args )
	{
		if ( args.Length == 0 )
			return;

		if ( Phase == OperationPhase.Choose && args[0] == "choose" && args.Length > 1 )
		{
			Choose( args[1] );
		}
		else if ( Phase == OperationPhase.Puzzle && puzzle is not null )
		{
			puzzle.Act( args );

			if ( Phase == OperationPhase.Puzzle && puzzle.Done )
				Advance();
		}
	}

	void Choose( string id )
	{
		if ( !Choices().Any( x => x.Id == id && x.Enabled ) )
			return;

		switch ( id )
		{
			case "exploit":
				Raise( 20 );
				Traces += 2;
				Note( "An old exploit opens the door. The server logs it." );
				break;

			case "crash":
				Raise( 18 );
				Traces += 1;
				Note( "The daemon crashes. Its watchdog logs the fault." );
				break;

			case "dump":
				Raise( 22 );
				Traces += 2;
				Data = 0.6;
				Note( "You pull what you can in one burst. Most of it is out." );
				break;

			case "wipe":
				Raise( 10 );
				Traces = Math.Min( Traces, 1 );
				Note( "The log is wiped. The wipe is its own entry." );
				break;

			case "leave":
				Note( "You log off and leave the lines where they are." );
				break;

			case "badge":
				puzzle = new CodePuzzle( Def, random, this, bonusTries );
				break;

			case "slip":
				puzzle = new SweepPuzzle( Def, random, this );
				break;

			case "route":
				puzzle = new RoutePuzzle( Def, random, this );
				break;

			case "scrub":
				puzzle = new ScrubPuzzle( this, random, Traces );
				break;

			default:
				return;
		}

		Traces = Math.Min( Traces, MaxTraces );

		if ( Phase == OperationPhase.Burned )
			return;

		if ( puzzle is null )
			Advance();
		else
			Phase = OperationPhase.Puzzle;
	}

	void Raise( double alert )
	{
		if ( Phase is OperationPhase.Complete or OperationPhase.Burned || alert <= 0 )
			return;

		Alert = Math.Min( Alert + Preview( alert ), MaxAlert );
		Peak = Math.Max( Peak, Alert );

		if ( Alert < MaxAlert )
			return;

		Phase = OperationPhase.Burned;
		puzzle = null;
		Note( "ALERT 100. They have your address. Connection cut." );
	}

	/// <summary>
	/// What <paramref name="alert"/> adds right now, with the pressure and the player's skill.
	/// </summary>
	int Preview( double alert ) => (int)Math.Ceiling( alert * Pressure * noiseScale );

	/// <summary>
	/// Move to the next stage, or finish. The clean-up is skipped when there is nothing to clean.
	/// </summary>
	void Advance()
	{
		puzzle = null;
		Stage++;

		if ( Stage == OperationStage.Cover && Traces == 0 )
		{
			Note( "Nothing to scrub. You left no trace." );
			Stage++;
		}

		if ( Stage == OperationStage.Done )
		{
			Phase = OperationPhase.Complete;
			Grade = Score();
			return;
		}

		Phase = OperationPhase.Choose;
	}

	/// <summary>
	/// Ghost takes no trace, an alert that never passed 30 and all the data. Clean takes no trace and an alert that never
	/// became suspicious. Noisy takes at most two traces and an alert under 70. Anything else is sloppy.
	/// </summary>
	OperationGrade Score()
	{
		if ( Traces == 0 && Peak <= 30 && Data >= 1 )
			return OperationGrade.Ghost;

		if ( Traces == 0 && Peak < SuspiciousAt )
			return OperationGrade.Clean;

		return Traces <= 2 && Peak < 70 ? OperationGrade.Noisy : OperationGrade.Sloppy;
	}

	void Note( string text )
	{
		log.Add( text );

		if ( log.Count > MaxLog )
			log.RemoveAt( 0 );
	}

	IReadOnlyList<OperationChoice> Choices()
	{
		if ( Phase != OperationPhase.Choose )
			return [];

		var open = !Lockdown;
		var closed = "Closed: the lockdown has patched this shortcut.";

		return Stage switch
		{
			OperationStage.Entry =>
			[
				new( "exploit", "Exploit an old bug",
					open ? $"Instant. Loud: about +{Preview( 20 )} alert, leaves 2 traces." : closed, open, true ),
				new( "badge", "Clone a badge",
					$"Puzzle: crack the {Def.CodeLength}-digit code. Wrong codes add alert.", true, false ),
			],
			OperationStage.Cross =>
			[
				new( "crash", "Crash the sweep daemon",
					open ? $"Instant. Loud: about +{Preview( 18 )} alert, leaves a trace." : closed, open, true ),
				new( "slip", "Slip past the sweep",
					"Puzzle: time your moves between the patrols. Waiting adds a little alert.", true, false ),
			],
			OperationStage.Exfil =>
			[
				new( "dump", "Dump it straight out",
					open
						? $"Instant. Loud: about +{Preview( 22 )} alert, 2 traces, and only 60% of the data."
						: closed,
					open, true ),
				new( "route", "Ghost route",
					"Puzzle: bounce the data through quiet relays. Watched ones log you.", true, false ),
			],
			OperationStage.Cover =>
			[
				new( "wipe", "Wipe the logs",
					open
						? $"Instant. Loud: about +{Preview( 10 )} alert, and the wipe itself stays as 1 trace."
						: closed,
					open, true ),
				new( "scrub", "Scrub your lines",
					$"Puzzle: remove your {Traces} line{(Traces == 1 ? "" : "s")} and nobody else's.", true, false ),
				new( "leave", "Walk away", $"No alert. All {Traces} trace{(Traces == 1 ? "" : "s")} stay.", true, false ),
			],
			_ => [],
		};
	}

	/// <summary>
	/// What the panel draws now.
	/// </summary>
	public OperationView BuildView()
	{
		var (title, text) = Stage switch
		{
			OperationStage.Entry => ("1. ENTRY", "The way in is a badge reader on a forgotten maintenance port."),
			OperationStage.Cross => ("2. CROSS", "A sweep daemon patrols the corridor between you and the vault."),
			OperationStage.Exfil => ("3. EXFIL", "The vault is open. Getting the data out unseen is the hard part."),
			OperationStage.Cover => ("4. COVER YOUR TRACKS", "Everything you touched is in a log somewhere."),
			_ => ("DONE", ""),
		};

		return new OperationView
		{
			Name = Def.Name,
			Address = Def.Address,
			Alert = Alert,
			AlertLabel = AlertLabel,
			Traces = Traces,
			DataPercent = (int)Math.Round( Data * 100 ),
			Stage = Stage,
			Phase = Phase,
			StageTitle = title,
			StageText = text,
			Choices = Choices(),
			Log = log.ToList(),
			Code = (puzzle as CodePuzzle)?.View,
			Sweep = (puzzle as SweepPuzzle)?.View,
			Route = (puzzle as RoutePuzzle)?.View,
			Scrub = (puzzle as ScrubPuzzle)?.View,
			Grade = Phase == OperationPhase.Complete ? Grade.ToString().ToUpperInvariant() : "",
			GradeNote = Phase == OperationPhase.Complete ? OperationGrades.Describe( Grade ) : "",
			Peak = Peak,
		};
	}
}
