using System;
using System.Text;

namespace OmnipresenceApps;

public enum DumpKind
{
	/// <summary>Noise between the words. Not clickable.</summary>
	Junk,

	/// <summary>A word that can be tried as the password.</summary>
	Word,

	/// <summary>A pair of brackets with junk inside that can be used once.</summary>
	Trick,

	/// <summary>A word or trick that is gone, drawn as dots.</summary>
	Removed,
}

public enum IntrusionStatus
{
	Running,
	Breached,
	Locked,
}

/// <param name="Index">The word or trick this is, for the click. -1 for junk and removed ones.</param>
public sealed record DumpSegment( DumpKind Kind, string Text, int Index = -1 );

public sealed record DumpLine( string Address, IReadOnlyList<DumpSegment> Segments );

/// <summary>
/// One run at a terminal: a memory dump with words in it, one of which is the password. Pick a word and the terminal says
/// how many letters are in the right place; wrong picks use up attempts. Brackets with junk inside are tricks that remove
/// a wrong word or give the attempts back. No engine objects, so the rules run without a panel.
/// </summary>
/// <remarks>
/// The password lives only here. Never copy it into the UI state: a shared app's state goes to every player.
/// </remarks>
public sealed class IntrusionPuzzle
{
	const int LineWidth = 16;
	const int MaxLog = 8;
	const string JunkChars = "!@#$%^&*-_+=;:,.?/|~";
	const string Dots = "....................";

	static readonly (char Open, char Close)[] Brackets = [('(', ')'), ('[', ']'), ('{', '}'), ('<', '>')];

	sealed class Trick
	{
		public string Text;
		public bool RestoresAttempts;
		public bool Used;
	}

	readonly Random random;
	readonly string[] words;
	readonly bool[] removed;
	readonly int secret;
	readonly List<Trick> tricks = new();
	readonly List<string> log = new();

	char[] cells;
	int[] owners;
	int lineCount;

	public IntrusionPuzzle( IntrusionServer server, Random random )
	{
		this.random = random;
		WordLength = server.WordLength;
		MaxAttempts = Attempts = server.Attempts;

		words = IntrusionCatalog.Words[WordLength].OrderBy( _ => random.Next() ).Take( server.WordCount ).ToArray();
		removed = new bool[words.Length];
		secret = random.Next( words.Length );

		for ( var i = 0; i < server.Tricks; i++ )
		{
			var (open, close) = Brackets[random.Next( Brackets.Length )];
			var inside = new StringBuilder();

			for ( var j = random.Next( 1, 5 ); j > 0; j-- )
				inside.Append( JunkChars[random.Next( JunkChars.Length )] );

			tricks.Add( new Trick { Text = $"{open}{inside}{close}", RestoresAttempts = random.Next( 2 ) == 0 } );
		}

		// Grow the dump until everything fits with room around it.
		lineCount = Math.Max( 10, (int)Math.Ceiling( (words.Length * WordLength + tricks.Count * 6) * 2.2 / LineWidth ) );

		while ( !TryLayout() )
			lineCount++;

		Log( $"Connected. {words.Length} entries in memory." );
	}

	public int WordLength { get; }
	public int MaxAttempts { get; }
	public int Attempts { get; private set; }
	public IntrusionStatus Status { get; private set; }

	public IReadOnlyList<string> LogLines => log;

	/// <summary>
	/// The dump as lines of segments, as it is now: removed words and used tricks show as dots.
	/// </summary>
	public List<DumpLine> BuildLines()
	{
		var lines = new List<DumpLine>();

		for ( var line = 0; line < lineCount; line++ )
		{
			var segments = new List<DumpSegment>();
			var start = line * LineWidth;
			var i = 0;

			while ( i < LineWidth )
			{
				var owner = owners[start + i];
				var end = i + 1;

				while ( end < LineWidth && owners[start + end] == owner )
					end++;

				var text = new string( cells, start + i, end - i );

				if ( owner == -1 )
					segments.Add( new DumpSegment( DumpKind.Junk, text ) );
				else if ( owner < words.Length )
				{
					segments.Add( removed[owner]
						? new DumpSegment( DumpKind.Removed, Dots[..text.Length] )
						: new DumpSegment( DumpKind.Word, text, owner ) );
				}
				else
				{
					var index = owner - words.Length;
					segments.Add( tricks[index].Used
						? new DumpSegment( DumpKind.Removed, Dots[..text.Length] )
						: new DumpSegment( DumpKind.Trick, text, index ) );
				}

				i = end;
			}

			lines.Add( new DumpLine( $"0x{0xF000 + start:X4}", Merge( segments ) ) );
		}

		return lines;
	}

	/// <summary>
	/// Try a word as the password.
	/// </summary>
	public void Guess( int index )
	{
		if ( Status != IntrusionStatus.Running || index < 0 || index >= words.Length || removed[index] )
			return;

		var word = words[index];
		Log( $"> {word}" );

		if ( index == secret )
		{
			Status = IntrusionStatus.Breached;
			Log( "Access granted." );
			return;
		}

		Attempts--;
		Log( $"Denied. Likeness {Likeness( word )}/{WordLength}." );

		if ( Attempts <= 0 )
		{
			Status = IntrusionStatus.Locked;
			Log( "Terminal locked." );
		}
	}

	/// <summary>
	/// Use a bracket trick: it removes a wrong word or gives the attempts back, whichever it holds and still can.
	/// </summary>
	public void UseTrick( int index )
	{
		if ( Status != IntrusionStatus.Running || index < 0 || index >= tricks.Count || tricks[index].Used )
			return;

		var trick = tricks[index];
		trick.Used = true;
		Log( $"> {trick.Text}" );

		var done = trick.RestoresAttempts ? RestoreAttempts() || RemoveDud() : RemoveDud() || RestoreAttempts();

		if ( !done )
			Log( "Nothing happened." );
	}

	bool RestoreAttempts()
	{
		if ( Attempts >= MaxAttempts )
			return false;

		Attempts = MaxAttempts;
		Log( "Attempts reset." );
		return true;
	}

	bool RemoveDud()
	{
		var duds = Enumerable.Range( 0, words.Length ).Where( x => x != secret && !removed[x] ).ToList();

		if ( duds.Count == 0 )
			return false;

		var dud = duds[random.Next( duds.Count )];
		removed[dud] = true;
		Log( $"Dud removed: {words[dud]}." );
		return true;
	}

	int Likeness( string word )
	{
		var answer = words[secret];
		var same = 0;

		for ( var i = 0; i < word.Length; i++ )
		{
			if ( word[i] == answer[i] )
				same++;
		}

		return same;
	}

	void Log( string text )
	{
		log.Add( text );

		if ( log.Count > MaxLog )
			log.RemoveAt( 0 );
	}

	/// <summary>
	/// Fill the dump with junk and place every word and trick on a line, with a gap around each. Returns false when
	/// something doesn't fit, so the dump can grow and try again.
	/// </summary>
	bool TryLayout()
	{
		cells = new char[lineCount * LineWidth];
		owners = new int[cells.Length];

		for ( var i = 0; i < cells.Length; i++ )
		{
			cells[i] = JunkChars[random.Next( JunkChars.Length )];
			owners[i] = -1;
		}

		for ( var w = 0; w < words.Length; w++ )
		{
			if ( !Place( words[w], w ) )
				return false;
		}

		for ( var t = 0; t < tricks.Count; t++ )
		{
			if ( !Place( tricks[t].Text, words.Length + t ) )
				return false;
		}

		return true;
	}

	bool Place( string text, int owner )
	{
		for ( var attempt = 0; attempt < 300; attempt++ )
		{
			var line = random.Next( lineCount );
			var offset = random.Next( LineWidth - text.Length + 1 );

			if ( !IsFree( line, offset - 1, offset + text.Length ) )
				continue;

			for ( var i = 0; i < text.Length; i++ )
			{
				cells[line * LineWidth + offset + i] = text[i];
				owners[line * LineWidth + offset + i] = owner;
			}

			return true;
		}

		return false;
	}

	bool IsFree( int line, int from, int to )
	{
		for ( var i = Math.Max( from, 0 ); i <= Math.Min( to, LineWidth - 1 ); i++ )
		{
			if ( owners[line * LineWidth + i] != -1 )
				return false;
		}

		return true;
	}

	/// <summary>
	/// Join neighbouring junk or removed segments, so a gap of dots reads as one run.
	/// </summary>
	static List<DumpSegment> Merge( List<DumpSegment> segments )
	{
		var merged = new List<DumpSegment>();

		foreach ( var segment in segments )
		{
			var joinable = segment.Kind is DumpKind.Junk or DumpKind.Removed;

			if ( joinable && merged.Count > 0 && merged[^1].Kind == segment.Kind )
				merged[^1] = merged[^1] with { Text = merged[^1].Text + segment.Text };
			else
				merged.Add( segment );
		}

		return merged;
	}
}
