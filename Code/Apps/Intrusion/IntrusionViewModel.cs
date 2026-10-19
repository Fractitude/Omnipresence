using System;

namespace OmnipresenceApps;

/// <summary>
/// Everything Intrusion does: lists the dead servers, runs the terminal puzzle on the one the player picks, and pays out
/// when it is cracked. Runs in the host's copy only (the app is shared), because the rewards are the host's.
/// </summary>
/// <remarks>
/// Who is rewarded is always the player in the seat, worked out from the computer. Nothing an action carries says so, and
/// the password never leaves the puzzle, so an action can't read it or reward anyone else.
/// </remarks>
public sealed class IntrusionViewModel
{
	public const string OpenAction = "open";
	public const string BackAction = "back";
	public const string TabAction = "tab";
	public const string GuessAction = "guess";
	public const string TrickAction = "trick";
	public const string WordAction = "word";
	public const string RetryAction = "retry";

	/// <summary>Start an operation. The argument is its id.</summary>
	public const string OperationOpenAction = "opopen";

	/// <summary>Do something in the operation being run. The arguments are those of <see cref="OperationRun.Act"/>.</summary>
	public const string OperationAction = "op";

	public const string ServersTab = "servers";
	public const string OperationsTab = "operations";
	public const string BlueprintsTab = "blueprints";

	readonly Func<Connection> getSeat;
	readonly Random random = new();

	IntrusionScreen screen;
	IntrusionServer server;
	IntrusionPuzzle puzzle;
	WordGuessPuzzle wordPuzzle;
	OperationRun operation;
	bool operationPaid;
	List<string> rewards = [];
	Guid lastSeatId;
	string signature;
	int revision;

	public IntrusionViewModel( Func<Connection> getSeat )
	{
		this.getSeat = getSeat;
		UiState = Build();
	}

	public IntrusionUiState UiState { get; private set; }

	/// <summary>
	/// The puzzle being played, whichever level the server is, or null.
	/// </summary>
	IIntrusionPuzzle Active => wordPuzzle ?? (IIntrusionPuzzle)puzzle;

	/// <summary>
	/// Once a frame: start over when the seat changed, and draw again if anything drawn changed.
	/// </summary>
	public void Tick()
	{
		var seat = getSeat();

		if ( (seat?.Id ?? default) != lastSeatId )
		{
			lastSeatId = seat?.Id ?? default;
			GoHome();
		}

		if ( Signature( seat, PlayerProgress.For( seat ) ) != signature )
			UiState = Build();
	}

	public void Act( string action, string[] args )
	{
		if ( UiState.HasAccess )
		{
			switch ( action )
			{
				case OpenAction when args.Length > 0:
					Open( args[0] );
					break;

				case BackAction:
					GoHome();
					break;

				case TabAction when args.Length > 0:
					ChooseTab( args[0] );
					break;

				case GuessAction when args.Length > 0 && int.TryParse( args[0], out var word ):
					Guess( word );
					break;

				case TrickAction when args.Length > 0 && int.TryParse( args[0], out var trick ):
					UseTrick( trick );
					break;

				case WordAction when args.Length > 0:
					GuessWord( args[0] );
					break;

				case RetryAction:
					Retry();
					break;

				case OperationOpenAction when args.Length > 0:
					OpenOperation( args[0] );
					break;

				case OperationAction when args.Length > 0:
					Operate( args );
					break;
			}
		}

		UiState = Build();
	}

	void GoHome()
	{
		screen = IntrusionScreen.Servers;
		server = null;
		puzzle = null;
		wordPuzzle = null;
		operation = null;
		rewards = [];
	}

	void ChooseTab( string tab )
	{
		if ( screen is not (IntrusionScreen.Servers or IntrusionScreen.Operations or IntrusionScreen.Blueprints) )
			return;

		screen = tab switch
		{
			BlueprintsTab => IntrusionScreen.Blueprints,
			OperationsTab => IntrusionScreen.Operations,
			_ => IntrusionScreen.Servers,
		};
	}

	void OpenOperation( string id )
	{
		var progress = PlayerProgress.For( getSeat() );

		if ( screen is not (IntrusionScreen.Operations or IntrusionScreen.Operation) || progress is not { Loaded: true }
		     || OperationCatalog.Find( id ) is not { } found || progress.HackLevel < found.MinHackLevel )
			return;

		var hardware = HardwareCatalog.BonusOf( progress.InstalledHardware );

		operation = new OperationRun( found, random, HackingCurve.NoiseScale( progress.HackLevel ), hardware.Attempts );
		operationPaid = false;
		rewards = [];
		screen = IntrusionScreen.Operation;
	}

	void Operate( string[] args )
	{
		if ( screen != IntrusionScreen.Operation || operation is null )
			return;

		// Start the same operation over, once it has ended.
		if ( args[0] == "again" )
		{
			if ( operationPaid )
				OpenOperation( operation.Def.Id );

			return;
		}

		operation.Act( args );

		if ( !operationPaid && operation.Phase is OperationPhase.Complete or OperationPhase.Burned )
		{
			operationPaid = true;
			rewards = PayOperation();
		}
	}

	/// <summary>
	/// Pay a run that ended, to the player in the seat. A burned run only teaches a little; a finished one pays by grade.
	/// </summary>
	List<string> PayOperation()
	{
		var progress = PlayerProgress.For( getSeat() );

		if ( progress is not { Loaded: true } )
			return ["Nothing could be saved."];

		var before = progress.HackLevel;
		var burned = operation.Phase == OperationPhase.Burned;
		var grade = burned ? OperationGrade.None : operation.Grade;
		var xp = burned ? operation.BurnedHackXp : operation.HackXpReward;

		if ( progress.FinishOperation( operation.Def.Id, (int)grade, xp, operation.CreditReward, operation.GhostBonus )
		     is not { } payout )
			return ["Nothing could be saved."];

		var lines = new List<string> { $"+{payout.HackXp:N0} hacking XP" };

		if ( payout.Credits > 0 )
			lines.Add( $"+{payout.Credits:N0} credits{(payout.First ? "" : " (repeat run: a quarter)")}" );

		if ( payout.GhostBonus > 0 )
			lines.Add( $"+{payout.GhostBonus:N0} credits for your first ghost run" );

		if ( progress.HackLevel > before )
			lines.Add( $"Hacking level {progress.HackLevel}. You are harder to notice." );

		return lines;
	}

	void Open( string id )
	{
		if ( screen != IntrusionScreen.Servers || IntrusionCatalog.FindServer( id ) is not { } found )
			return;

		Start( found );
	}

	void Start( IntrusionServer found )
	{
		var hardware = HardwareCatalog.BonusOf( PlayerProgress.For( getSeat() )?.InstalledHardware ?? [] );

		server = found;
		puzzle = null;
		wordPuzzle = null;

		if ( found.Level <= 1 )
			wordPuzzle = new WordGuessPuzzle( found, random, hardware.Attempts, hardware.Duds );
		else
			puzzle = new IntrusionPuzzle( found, random, hardware.Attempts, hardware.Duds );

		rewards = [];
		screen = IntrusionScreen.Terminal;
	}

	void Retry()
	{
		if ( screen == IntrusionScreen.Locked && server is not null )
			Start( server );
	}

	void Guess( int word )
	{
		if ( screen != IntrusionScreen.Terminal || puzzle is null )
			return;

		puzzle.Guess( word );
		Finish();
	}

	void UseTrick( int trick )
	{
		if ( screen != IntrusionScreen.Terminal || puzzle is null )
			return;

		puzzle.UseTrick( trick );
		Finish();
	}

	void GuessWord( string word )
	{
		if ( screen != IntrusionScreen.Terminal || wordPuzzle is null )
			return;

		wordPuzzle.Guess( word );
		Finish();
	}

	/// <summary>
	/// Move on when the puzzle is over: pay out a breach, or show the lock.
	/// </summary>
	void Finish()
	{
		if ( Active.Status == IntrusionStatus.Locked )
		{
			screen = IntrusionScreen.Locked;
			return;
		}

		if ( Active.Status != IntrusionStatus.Breached )
			return;

		screen = IntrusionScreen.Breached;
		rewards = Pay();
	}

	/// <summary>
	/// The reward of the target, paid to the player in the seat. XP, credits and the blueprint are paid the first time
	/// only. Returns the lines to show.
	/// </summary>
	List<string> Pay()
	{
		var progress = PlayerProgress.For( getSeat() );

		if ( progress is not { Loaded: true } )
			return ["Nothing could be saved."];

		if ( !progress.CompleteIntrusion( server.Id, server.Xp, server.Credits, server.Blueprint ) )
			return ["Already looted. Nothing new here."];

		var lines = new List<string> { $"+{server.Xp:N0} hacking XP", $"+{server.Credits:N0} credits" };

		if ( IntrusionCatalog.FindBlueprint( server.Blueprint ) is { } blueprint )
			lines.Add( $"Blueprint: {blueprint.Name}" );

		return lines;
	}

	string Signature( Connection seat, PlayerProgress progress ) =>
		HashCode.Combine(
			seat?.Id,
			progress is { Loaded: true },
			progress?.IntrusionVersion,
			progress?.OperationVersion,
			progress?.HackXp,
			WiredTravel.Current,
			screen ).ToString();

	IntrusionUiState Build()
	{
		var seat = getSeat();
		var progress = PlayerProgress.For( seat );

		signature = Signature( seat, progress );

		var state = new IntrusionUiState { Revision = ++revision, Screen = screen };

		if ( seat is null )
			return state with { Message = "Sit down to use the terminal." };

		if ( WiredTravel.Current == WiredPlace.Home )
			return state with { Message = "No signal. Connect to the Wired first." };

		if ( progress is not { Loaded: true } )
			return state with { Message = "Loading your account..." };

		state = state with
		{
			HasAccess = true,
			Servers = IntrusionCatalog.Servers.Select( x => ToRow( x, progress ) ).ToList(),
			Operations = OperationCatalog.Operations.Select( x => ToOperationRow( x, progress ) ).ToList(),
			Blueprints = progress.Blueprints.Select( ToBlueprintRow ).Where( x => x is not null ).ToList(),
			HackLevel = progress.HackLevel,
			HackProgress = HackingCurve.Fraction( progress.HackXp ),
		};

		if ( operation is not null )
			return state with { ServerName = operation.Def.Name, Operation = operation.BuildView(), Rewards = rewards };

		if ( Active is not { } active )
			return state;

		return state with
		{
			ServerName = server.Name,
			ServerAddress = server.Address,
			Attempts = active.Attempts,
			MaxAttempts = active.MaxAttempts,
			Dump = puzzle?.BuildLines() ?? [],
			Log = puzzle?.LogLines.ToList() ?? [],
			Board = wordPuzzle?.BuildBoard(),
			Rewards = rewards,
		};
	}

	static ServerRow ToRow( IntrusionServer server, PlayerProgress progress )
	{
		var reward = $"{server.Xp:N0} hacking XP, {server.Credits:N0} credits";

		if ( IntrusionCatalog.FindBlueprint( server.Blueprint ) is { } blueprint )
			reward += $", {blueprint.Name}";

		return new ServerRow(
			server.Id,
			server.Name,
			server.Address,
			server.Layer,
			server.Difficulty,
			server.Blurb,
			reward,
			progress.ClearedServers.Contains( server.Id ),
			server.Level );
	}

	static OperationRow ToOperationRow( OperationDef def, PlayerProgress progress )
	{
		var best = (OperationGrade)(progress.Operations.FirstOrDefault( x => x.Id == def.Id )?.Best ?? 0);

		return new OperationRow(
			def.Id,
			def.Name,
			def.Address,
			def.Layer,
			def.Difficulty,
			def.Blurb,
			$"Up to {(long)(def.HackXp * OperationGrades.XpScale( OperationGrade.Ghost )):N0} hacking XP, "
			+ $"{(long)(def.Credits * OperationGrades.CreditScale( OperationGrade.Ghost )):N0} credits",
			def.MinHackLevel,
			progress.HackLevel < def.MinHackLevel,
			best == OperationGrade.None ? "" : best.ToString() );
	}

	static BlueprintRow ToBlueprintRow( string id )
	{
		if ( IntrusionCatalog.FindBlueprint( id ) is not { } blueprint )
			return null;

		var from = IntrusionCatalog.Servers.FirstOrDefault( x => x.Blueprint == id )?.Name ?? "";
		return new BlueprintRow( blueprint.Name, blueprint.Description, from );
	}
}
