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
	public const string RetryAction = "retry";

	public const string ServersTab = "servers";
	public const string BlueprintsTab = "blueprints";

	readonly Func<Connection> getSeat;
	readonly Random random = new();

	IntrusionScreen screen;
	IntrusionServer server;
	IntrusionPuzzle puzzle;
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

				case RetryAction:
					Retry();
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
		rewards = [];
	}

	void ChooseTab( string tab )
	{
		if ( screen is not (IntrusionScreen.Servers or IntrusionScreen.Blueprints) )
			return;

		screen = tab == BlueprintsTab ? IntrusionScreen.Blueprints : IntrusionScreen.Servers;
	}

	void Open( string id )
	{
		if ( screen != IntrusionScreen.Servers || IntrusionCatalog.FindServer( id ) is not { } found )
			return;

		Start( found );
	}

	void Start( IntrusionServer found )
	{
		server = found;
		puzzle = new IntrusionPuzzle( found, random );
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
		if ( screen != IntrusionScreen.Terminal )
			return;

		puzzle.Guess( word );
		Finish();
	}

	void UseTrick( int trick )
	{
		if ( screen != IntrusionScreen.Terminal )
			return;

		puzzle.UseTrick( trick );
		Finish();
	}

	/// <summary>
	/// Move on when the puzzle is over: pay out a breach, or show the lock.
	/// </summary>
	void Finish()
	{
		if ( puzzle.Status == IntrusionStatus.Locked )
		{
			screen = IntrusionScreen.Locked;
			return;
		}

		if ( puzzle.Status != IntrusionStatus.Breached )
			return;

		screen = IntrusionScreen.Breached;
		rewards = Pay();
	}

	/// <summary>
	/// The reward of the server, paid to the player in the seat the first time. Returns the lines to show.
	/// </summary>
	List<string> Pay()
	{
		var progress = PlayerProgress.For( getSeat() );

		if ( progress is not { Loaded: true } )
			return ["Nothing could be saved."];

		if ( !progress.CompleteIntrusion( server.Id, server.Xp, server.Credits, server.Blueprint ) )
			return ["Already looted. Nothing new here."];

		var lines = new List<string> { $"+{server.Xp:N0} XP", $"+{server.Credits:N0} credits" };

		if ( IntrusionCatalog.FindBlueprint( server.Blueprint ) is { } blueprint )
			lines.Add( $"Blueprint: {blueprint.Name}" );

		return lines;
	}

	string Signature( Connection seat, PlayerProgress progress ) =>
		HashCode.Combine(
			seat?.Id,
			progress is { Loaded: true },
			progress?.IntrusionVersion,
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
			Blueprints = progress.Blueprints.Select( ToBlueprintRow ).Where( x => x is not null ).ToList(),
		};

		if ( puzzle is null )
			return state;

		return state with
		{
			ServerName = server.Name,
			ServerAddress = server.Address,
			Attempts = puzzle.Attempts,
			MaxAttempts = puzzle.MaxAttempts,
			Dump = puzzle.BuildLines(),
			Log = puzzle.LogLines.ToList(),
			Rewards = rewards,
		};
	}

	static ServerRow ToRow( IntrusionServer server, PlayerProgress progress )
	{
		var reward = $"{server.Xp:N0} XP, {server.Credits:N0} credits";

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
			progress.ClearedServers.Contains( server.Id ) );
	}

	static BlueprintRow ToBlueprintRow( string id )
	{
		if ( IntrusionCatalog.FindBlueprint( id ) is not { } blueprint )
			return null;

		var from = IntrusionCatalog.Servers.FirstOrDefault( x => x.Blueprint == id )?.Name ?? "";
		return new BlueprintRow( blueprint.Name, blueprint.Description, from );
	}
}
