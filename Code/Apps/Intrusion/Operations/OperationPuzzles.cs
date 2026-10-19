using System;
using System.Text;

namespace OmnipresenceApps;

/// <summary>
/// What a stage puzzle can do to the run it is part of. The run applies its pressure and the player's skill to the alert.
/// </summary>
public interface IOperationHost
{
	/// <summary>Raise the alert by this much before the pressure and the player's skill are applied.</summary>
	void Raise( double alert );

	/// <summary>Leave this many more traces behind.</summary>
	void AddTrace( int count );

	/// <summary>Set how many traces are left, after a clean-up.</summary>
	void SetTraces( int count );

	/// <summary>Set how much of the data got out, from 0 to 1.</summary>
	void SetData( double fraction );

	void Note( string text );
}

/// <summary>
/// What every stage puzzle has in common. No engine objects, so the rules run without a panel. A puzzle holds its own
/// secrets (the code, the watched relays, which log lines are the player's) and never hands them to the UI.
/// </summary>
public abstract class StagePuzzle
{
	protected StagePuzzle( IOperationHost host ) => Host = host;

	protected IOperationHost Host { get; }

	/// <summary>True once the stage is over, whether it went well or not.</summary>
	public bool Done { get; protected set; }

	/// <summary>One thing the player did, as the words of the action. Anything unknown is ignored.</summary>
	public abstract void Act( string[] args );
}

/// <summary>
/// Entry: a badge reader. Guess the code; the reader says how many digits are right and in place, and how many are right
/// but in the wrong place. Every wrong code adds alert, and running out of tries forces the door the loud way.
/// </summary>
public sealed class CodePuzzle : StagePuzzle
{
	readonly int[] secret;
	readonly int length;
	readonly int digits;
	readonly List<CodeGuessRow> history = [];

	string pending = "";

	public CodePuzzle( OperationDef def, Random random, IOperationHost host, int bonusTries ) : base( host )
	{
		length = def.CodeLength;
		digits = def.CodeDigits;
		MaxTries = TriesLeft = def.CodeTries + Math.Max( bonusTries, 0 );
		secret = Enumerable.Range( 0, length ).Select( _ => random.Next( 1, digits + 1 ) ).ToArray();

		Host.Note( $"Badge reader: {length} digits, each 1 to {digits}." );
	}

	public int MaxTries { get; }
	public int TriesLeft { get; private set; }

	public CodeView View => new( length, digits, TriesLeft, MaxTries, pending, history.ToList() );

	public override void Act( string[] args )
	{
		if ( Done || args.Length == 0 )
			return;

		switch ( args[0] )
		{
			case "digit" when args.Length > 1 && int.TryParse( args[1], out var digit ):
				if ( digit >= 1 && digit <= digits && pending.Length < length )
					pending += digit;
				break;

			case "back" when pending.Length > 0:
				pending = pending[..^1];
				break;

			case "enter" when pending.Length == length:
				Submit();
				break;
		}
	}

	void Submit()
	{
		var guess = pending.Select( x => x - '0' ).ToArray();
		var exact = 0;
		var common = 0;

		for ( var i = 0; i < length; i++ )
		{
			if ( guess[i] == secret[i] )
				exact++;
		}

		for ( var d = 1; d <= digits; d++ )
			common += Math.Min( guess.Count( x => x == d ), secret.Count( x => x == d ) );

		var near = common - exact;
		history.Add( new CodeGuessRow( pending, exact, near ) );
		pending = "";

		if ( exact == length )
		{
			Host.Note( "Badge accepted. The door opens like it was expecting you." );
			Done = true;
			return;
		}

		TriesLeft--;
		Host.Raise( 4 );
		Host.Note( $"Rejected. {exact} in place, {near} elsewhere." );

		if ( TriesLeft > 0 )
			return;

		Host.Raise( 18 );
		Host.AddTrace( 1 );
		Host.Note( "Reader locked out. You force the door and it screams." );
		Done = true;
	}
}

/// <summary>
/// Cross: guard daemons patrol the nodes between the door and the vault, and every move of yours is one tick for them
/// too. The next few ticks of their patrols are shown, so it is a path through time: advance when the way is clear, wait
/// or back off when it isn't. Waiting adds a little alert (an idle session stands out). Being caught throws you back to
/// the door.
/// </summary>
public sealed class SweepPuzzle : StagePuzzle
{
	public const int Horizon = 5;

	readonly int nodes;
	readonly int maxTicks;
	readonly List<(int Lo, int Hi, int Phase)> sweepers = [];

	int tick;

	public SweepPuzzle( OperationDef def, Random random, IOperationHost host ) : base( host )
	{
		nodes = Math.Max( def.SweepNodes, 5 );
		maxTicks = def.SweepTicks;

		// Daemons stay between the door (0) and the vault (last), so both ends are always safe.
		for ( var i = 0; i < def.Sweepers; i++ )
		{
			var lo = random.Next( 1, nodes - 2 );
			var hi = random.Next( lo + 1, nodes - 1 );
			sweepers.Add( (lo, hi, random.Next( 2 * (hi - lo) )) );
		}

		Host.Note( "A sweep daemon patrols the corridor. It moves when you do." );
	}

	public int Player { get; private set; }

	public SweepView View => new( nodes, Player, maxTicks - tick, Forecast() );

	public override void Act( string[] args )
	{
		if ( Done || args.Length == 0 )
			return;

		var step = args[0] switch
		{
			"advance" => 1,
			"wait" => 0,
			"back" => -1,
			_ => 2,
		};

		if ( step == 2 )
			return;

		var from = Player;
		var to = Math.Clamp( from + step, 0, nodes - 1 );
		var next = tick + 1;

		// Caught when a daemon is on your node after the tick, or you and a daemon swapped places.
		var caught = sweepers.Any( x => PositionAt( x, next ) == to
		                                || (PositionAt( x, tick ) == to && PositionAt( x, next ) == from) );

		Player = to;
		tick = next;

		if ( step <= 0 )
			Host.Raise( 2 );

		if ( caught )
		{
			Player = 0;
			Host.Raise( 12 );
			Host.AddTrace( 1 );
			Host.Note( "The daemon found you. You jump back to the door." );
			return;
		}

		if ( Player == nodes - 1 )
		{
			Host.Note( "You reach the vault unseen." );
			Done = true;
			return;
		}

		if ( tick < maxTicks )
			return;

		Host.Raise( 18 );
		Host.AddTrace( 1 );
		Host.Note( "Too slow. You kill the daemon the loud way." );
		Done = true;
	}

	static int PositionAt( (int Lo, int Hi, int Phase) sweeper, int time )
	{
		var span = sweeper.Hi - sweeper.Lo;
		var phase = (time + sweeper.Phase) % (2 * span);
		return sweeper.Lo + (phase <= span ? phase : 2 * span - phase);
	}

	List<string> Forecast()
	{
		var rows = new List<string>();

		for ( var k = 0; k < Horizon; k++ )
		{
			var row = new StringBuilder();

			for ( var node = 0; node < nodes; node++ )
			{
				if ( k == 0 && node == Player )
					row.Append( 'P' );
				else
					row.Append( sweepers.Any( x => PositionAt( x, tick + k ) == node ) ? '#' : '.' );
			}

			rows.Add( row.ToString() );
		}

		return rows;
	}
}

/// <summary>
/// Exfil: the data hops through relays, one per column, to a row next to the one before. Some relays are watched, and the
/// banner shows what they run: audit and sensor software is watched. Some hide their banner until pinged, and a ping adds
/// alert. Using a watched relay logs you and adds alert and a trace.
/// </summary>
public sealed class RoutePuzzle : StagePuzzle
{
	public const int Rows = 3;

	static readonly string[] WatchedBanners = ["auditd 2.8", "netflow tap", "SIEM agent", "ids-sensor 4", "packet logger"];
	static readonly string[] QuietBanners = ["dropbear ssh", "busybox httpd", "lighttpd 1.4", "ntp daemon", "print spooler"];

	readonly int columns;
	readonly bool[,] watched;
	readonly bool[,] known;
	readonly string[,] banners;
	readonly int[] picked;

	int next;

	public RoutePuzzle( OperationDef def, Random random, IOperationHost host ) : base( host )
	{
		columns = def.RouteColumns;
		watched = new bool[columns, Rows];
		known = new bool[columns, Rows];
		banners = new string[columns, Rows];
		picked = Enumerable.Repeat( -1, columns ).ToArray();

		// A quiet path always exists: it never steps more than one row at a time.
		var quietRow = random.Next( Rows );

		for ( var c = 0; c < columns; c++ )
		{
			if ( c > 0 )
				quietRow = Math.Clamp( quietRow + random.Next( -1, 2 ), 0, Rows - 1 );

			for ( var r = 0; r < Rows; r++ )
			{
				watched[c, r] = r != quietRow && random.NextDouble() < 0.6;
				known[c, r] = random.NextDouble() >= def.RouteDecoys;

				var pool = watched[c, r] ? WatchedBanners : QuietBanners;
				banners[c, r] = pool[random.Next( pool.Length )];
			}
		}

		Host.Note( "Pick a relay in each column, next to the last one. Relays running audit or sensor software are watched." );
	}

	public RouteView View
	{
		get
		{
			var cells = new List<RelayCell>();

			for ( var c = 0; c < columns; c++ )
			{
				for ( var r = 0; r < Rows; r++ )
					cells.Add( new RelayCell( c, r, known[c, r] ? banners[c, r] : "???", known[c, r], IsReachable( c, r ), picked[c] == r ) );
			}

			return new RouteView( columns, next, cells );
		}
	}

	public override void Act( string[] args )
	{
		if ( Done || args.Length < 3 || !int.TryParse( args[1], out var column ) || !int.TryParse( args[2], out var row ) )
			return;

		if ( column < 0 || column >= columns || row < 0 || row >= Rows )
			return;

		if ( args[0] == "ping" && !known[column, row] )
		{
			known[column, row] = true;
			Host.Raise( 3 );
			Host.Note( $"Ping: {banners[column, row]}." );
		}
		else if ( args[0] == "relay" && IsReachable( column, row ) )
		{
			Pick( column, row );
		}
	}

	bool IsReachable( int column, int row ) =>
		column == next && (column == 0 || Math.Abs( picked[column - 1] - row ) <= 1);

	void Pick( int column, int row )
	{
		picked[column] = row;
		next++;

		if ( watched[column, row] )
		{
			Host.Raise( 16 );
			Host.AddTrace( 1 );
			Host.Note( $"Hop {column + 1}: {banners[column, row]} logged the transfer." );
		}
		else
		{
			Host.Note( $"Hop {column + 1}: clean." );
		}

		if ( next < columns )
			return;

		Host.SetData( 1 );
		Host.Note( "The whole archive is out." );
		Done = true;
	}
}

/// <summary>
/// Cover: the log is full of lines. The ones with your session tag are yours: one for each trace you left. Pick exactly
/// those and commit. Lines with a tag that is almost yours are somebody else's, and removing them leaves a hole that
/// raises the alert and is a trace of its own. Lines you miss stay as traces.
/// </summary>
public sealed class ScrubPuzzle : StagePuzzle
{
	const string Hex = "0123456789ABCDEF";

	static readonly string[] Messages =
	[
		"login accepted", "file read /vault/index", "outbound transfer 4.2MB", "scanner fault 0x1F", "key accepted",
		"session opened", "cache miss", "door badge read", "config reload", "port 22 probe", "sync complete",
	];

	sealed record Line( int Time, string Text, bool Mine );

	readonly List<Line> lines = [];
	readonly bool[] selected;

	public ScrubPuzzle( IOperationHost host, Random random, int traces ) : base( host )
	{
		Tag = RandomTag( random );
		Mine = traces;

		var decoys = 6 + random.Next( 4 );
		var all = new List<Line>();

		for ( var i = 0; i < traces + decoys; i++ )
		{
			var mine = i < traces;
			var tag = mine ? Tag : random.Next( 4 ) == 0 ? "----" : NearTag( Tag, random );
			var time = 2 * 3600 + 10 * 60 + random.Next( 5400 );
			var text = $"{Clock( time )}  10.4.{random.Next( 1, 40 )}.{random.Next( 2, 250 )}  sess={tag}  "
			           + Messages[random.Next( Messages.Length )];

			all.Add( new Line( time, text, mine ) );
		}

		lines.AddRange( all.OrderBy( x => x.Time ) );
		selected = new bool[lines.Count];

		Host.Note( $"Your session is {Tag}. Find the {traces} line{(traces == 1 ? "" : "s")} that are yours." );
	}

	public string Tag { get; }
	public int Mine { get; }

	public ScrubView View => new( Tag, Mine, selected.Count( x => x ),
		lines.Select( ( x, i ) => new ScrubLine( i, x.Text, selected[i] ) ).ToList() );

	public override void Act( string[] args )
	{
		if ( Done || args.Length == 0 )
			return;

		if ( args[0] == "toggle" && args.Length > 1 && int.TryParse( args[1], out var index ) && index >= 0 && index < lines.Count )
		{
			selected[index] = !selected[index];
		}
		else if ( args[0] == "commit" )
		{
			Commit();
		}
	}

	void Commit()
	{
		var wrong = lines.Where( ( x, i ) => selected[i] && !x.Mine ).Count();
		var missed = lines.Where( ( x, i ) => !selected[i] && x.Mine ).Count();

		if ( wrong > 0 )
		{
			Host.Raise( 8 * wrong );
			Host.Note( $"{wrong} line{(wrong == 1 ? "" : "s")} that weren't yours are gone. That gap will be noticed." );
		}

		if ( missed > 0 )
			Host.Note( $"{missed} of your lines are still in the log." );
		else if ( wrong == 0 )
			Host.Note( "Not a line out of place. You were never here." );

		Host.SetTraces( missed + wrong );
		Done = true;
	}

	static string RandomTag( Random random )
	{
		var tag = new StringBuilder();

		for ( var i = 0; i < 4; i++ )
			tag.Append( Hex[random.Next( Hex.Length )] );

		return tag.ToString();
	}

	/// <summary>
	/// The tag with one character changed, so it has to be read to be told apart.
	/// </summary>
	static string NearTag( string tag, Random random )
	{
		var chars = tag.ToCharArray();
		var at = random.Next( chars.Length );

		while ( chars[at] == tag[at] )
			chars[at] = Hex[random.Next( Hex.Length )];

		return new string( chars );
	}

	static string Clock( int seconds ) => $"{seconds / 3600:00}:{seconds / 60 % 60:00}:{seconds % 60:00}";
}
