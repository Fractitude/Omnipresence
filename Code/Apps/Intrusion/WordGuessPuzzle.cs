using System;

namespace OmnipresenceApps;

public enum LetterMark
{
	/// <summary>The letter isn't in the password, or all its copies are accounted for.</summary>
	Absent,

	/// <summary>The letter is in the password, in another place.</summary>
	Misplaced,

	/// <summary>The letter is in the right place.</summary>
	Correct,
}

public sealed record GuessCell( char Letter, LetterMark Mark );

public sealed record GuessRow( IReadOnlyList<GuessCell> Cells );

/// <summary>
/// What a Level 1 terminal shows: the clue, the letters given away, the guesses so far. Never the password itself.
/// </summary>
/// <param name="Known">The word with "_" where a letter is not known, spaced out, like "S _ _ N _ _".</param>
/// <param name="Notice">What the last entry did when it didn't count as a guess, like "Enter 6 letters."</param>
/// <param name="Slots">How many rows the board has: the most guesses there can be.</param>
public sealed record GuessBoard( string Hint, int WordLength, string Known, string Notice, int Slots,
	IReadOnlyList<GuessRow> Rows );

/// <summary>
/// One run at a Level 1 terminal: a Wordle-style password. The terminal gives a clue to the word; the player types a
/// word of the right length and sees which letters are right, which are in the word but in another place, and which are
/// not in it. Wrong words use up attempts. No engine objects, so the rules run without a panel.
/// </summary>
/// <remarks>
/// The password lives only here. Never copy it into the UI state: a shared app's state goes to every player.
/// </remarks>
public sealed class WordGuessPuzzle : IIntrusionPuzzle
{
	/// <summary>The word lengths a Level 1 terminal can have, set by <see cref="IntrusionServer.WordLength"/>.</summary>
	public const int MinLength = 4;

	public const int MaxLength = 6;

	readonly string secret;
	readonly bool[] revealed;
	readonly List<GuessRow> rows = new();
	readonly List<string> tried = new();

	string notice = "";

	/// <param name="bonusAttempts">Extra wrong guesses allowed, from installed hardware.</param>
	/// <param name="startingDuds">Letters given away before the first guess, from installed hardware.</param>
	public WordGuessPuzzle( IntrusionServer server, Random random, int bonusAttempts = 0, int startingDuds = 0 )
	{
		WordLength = Math.Clamp( server.WordLength, MinLength, MaxLength );
		MaxAttempts = Attempts = server.Attempts + Math.Max( bonusAttempts, 0 );

		var words = IntrusionCatalog.Words[WordLength];
		secret = words[random.Next( words.Length )];

		// One letter always stays hidden.
		revealed = new bool[WordLength];

		foreach ( var place in Enumerable.Range( 0, WordLength )
			         .OrderBy( _ => random.Next() )
			         .Take( Math.Clamp( startingDuds, 0, WordLength - 1 ) ) )
			revealed[place] = true;
	}

	public int WordLength { get; }
	public int MaxAttempts { get; }
	public int Attempts { get; private set; }
	public IntrusionStatus Status { get; private set; }

	/// <summary>
	/// The board as it is now.
	/// </summary>
	public GuessBoard BuildBoard() => new(
		IntrusionCatalog.HintFor( secret ),
		WordLength,
		string.Join( ' ', secret.Select( ( c, i ) => revealed[i] ? c : '_' ) ),
		notice,
		MaxAttempts,
		rows.ToList() );

	/// <summary>
	/// Try a word as the password. Something that isn't a word of the right length, or was tried already, costs nothing.
	/// </summary>
	public void Guess( string text )
	{
		if ( Status != IntrusionStatus.Running )
			return;

		var word = (text ?? "").Trim().ToUpperInvariant();

		if ( word.Length != WordLength || !word.All( c => c is >= 'A' and <= 'Z' ) )
		{
			notice = $"Enter {WordLength} letters.";
			return;
		}

		if ( tried.Contains( word ) )
		{
			notice = "Already tried that.";
			return;
		}

		tried.Add( word );
		rows.Add( new GuessRow( Mark( word ) ) );

		if ( word == secret )
		{
			Status = IntrusionStatus.Breached;
			notice = "Access granted.";
			return;
		}

		Attempts--;
		notice = "Denied.";

		if ( Attempts <= 0 )
		{
			Status = IntrusionStatus.Locked;
			notice = "Terminal locked.";
		}
	}

	/// <summary>
	/// Wordle's marking: right places first, then each remaining letter is Misplaced while the password still has an
	/// unmatched copy of it, so a letter is never marked more times than it appears.
	/// </summary>
	List<GuessCell> Mark( string word )
	{
		var marks = new LetterMark[WordLength];
		var unmatched = new Dictionary<char, int>();

		for ( var i = 0; i < WordLength; i++ )
		{
			if ( word[i] == secret[i] )
				marks[i] = LetterMark.Correct;
			else
				unmatched[secret[i]] = unmatched.GetValueOrDefault( secret[i] ) + 1;
		}

		for ( var i = 0; i < WordLength; i++ )
		{
			if ( marks[i] == LetterMark.Correct || !unmatched.TryGetValue( word[i], out var left ) || left <= 0 )
				continue;

			marks[i] = LetterMark.Misplaced;
			unmatched[word[i]] = left - 1;
		}

		return word.Select( ( c, i ) => new GuessCell( c, marks[i] ) ).ToList();
	}
}
