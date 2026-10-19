namespace OmnipresenceApps;

/// <summary>
/// A dead server in the Wired: an abandoned machine whose terminal can be cracked for a reward, once per player.
/// </summary>
/// <param name="Id">Saved in the player's progress, so never rename one.</param>
/// <param name="Layer">The layer of the Wired it sits in, for the list.</param>
/// <param name="WordLength">How long the password is (and each word in the memory dump).</param>
/// <param name="WordCount">Level 2: how many words are in the dump, one of which is the password.</param>
/// <param name="Attempts">How many wrong guesses lock the terminal.</param>
/// <param name="Tricks">Level 2: how many bracket tricks are hidden in the dump.</param>
/// <param name="Blueprint">The id of the blueprint it holds, or empty.</param>
/// <param name="Level">
/// The security level, which decides the puzzle: 1 is a Wordle-style guess from a clue, 2 is the memory dump.
/// </param>
public sealed record IntrusionServer(
	string Id,
	string Name,
	string Address,
	string Layer,
	string Difficulty,
	string Blurb,
	int WordLength,
	int WordCount,
	int Attempts,
	int Tricks,
	long Xp,
	long Credits,
	string Blueprint,
	int Level = 2 );

/// <summary>
/// Something found on a dead server that can be built at home.
/// </summary>
public sealed record BlueprintInfo( string Id, string Name, string Description );

public static class IntrusionCatalog
{
	// Properties, not readonly fields: a hot reload keeps a readonly field's old objects when the type changes shape, so
	// a new field on a record (like Cost) would read as 0 until the game restarted. These are built on every read.
	public static IReadOnlyList<IntrusionServer> Servers =>
	[
		new( "hollow-pines", "Hollow Pines Radio", "13.4.77", "Layer 03", "Easy",
			"A pirate radio site. Its last post asks whoever is still here not to let it copy the station.",
			5, 8, 4, 1, 80, 120, "signal-tuner" ),
		new( "cyberia-basement", "Cyberia Basement", "13.9.02", "Layer 03", "Medium",
			"The club's back-office machine. Nobody has logged in since the lights went out.",
			6, 10, 4, 2, 200, 400, "" ),
		new( "copy-shop", "The Copy Shop", "13.2.31", "Layer 04", "Hard",
			"Every file here exists twice. One of the copies is wrong.",
			7, 12, 4, 3, 500, 1000, "ram-stack" ),
	];

	public static IReadOnlyList<BlueprintInfo> Blueprints =>
	[
		new( "signal-tuner", "Signal Tuner",
			"A hand-built receiver for your desk. Pulls faint broadcasts out of the static." ),
		new( "ram-stack", "RAM Stack",
			"Four mismatched memory sticks wired in a row. More room for your rig." ),
	];

	/// <summary>
	/// The words a dump is built from, by length. Every word in a list has that many letters.
	/// </summary>
	public static readonly IReadOnlyDictionary<int, string[]> Words = new Dictionary<int, string[]>
	{
		[4] =
		[
			"WIRE", "DEAD", "LOST", "VOID", "FADE", "LINK", "DUST", "COLD", "DATA", "HUSH", "NODE", "LOOP",
		],
		[5] =
		[
			"WIRED", "ALONE", "NIGHT", "GHOST", "DREAM", "RADIO", "PINES", "TAPES", "NOISE", "LAYER", "CABLE", "ERASE",
			"VOICE", "FADED",
		],
		[6] =
		[
			"SIGNAL", "HOLLOW", "MEMORY", "SHADOW", "SILENT", "CIRCLE", "MIRROR", "STATIC", "BROKEN", "ECHOES", "PACKET",
			"ORPHAN", "WINTER",
		],
		[7] =
		[
			"PRESENT", "NETWORK", "CONNECT", "DISCARD", "MESSAGE", "ABANDON", "ARCHIVE", "COPYING", "DROPOUT", "PHANTOM",
			"SERVERS", "LAYERED", "SIGNALS",
		],
	};

	/// <summary>
	/// The clue a Level 1 terminal gives for each word. Never says the word itself.
	/// </summary>
	static readonly IReadOnlyDictionary<string, string> Hints = new Dictionary<string, string>
	{
		["WIRE"] = "A thin metal thread that carries a signal.",
		["DEAD"] = "No longer running.",
		["LOST"] = "Cannot be found.",
		["VOID"] = "Empty space with nothing in it.",
		["FADE"] = "To grow dim.",
		["LINK"] = "One piece of a chain, or a connection.",
		["DUST"] = "What settles on old machines.",
		["COLD"] = "The opposite of warm.",
		["DATA"] = "Facts a computer stores.",
		["HUSH"] = "Be quiet.",
		["NODE"] = "One point in a network.",
		["LOOP"] = "Something that repeats forever.",

		["WIRED"] = "Where everyone is connected.",
		["ALONE"] = "How you feel when nobody answers.",
		["NIGHT"] = "When the lights go out.",
		["GHOST"] = "Something that lingers after it is gone.",
		["DREAM"] = "What you see when you sleep.",
		["RADIO"] = "Pirate stations broadcast over it.",
		["PINES"] = "Trees around a quiet town.",
		["TAPES"] = "Old recordings on spools.",
		["NOISE"] = "Unwanted signal.",
		["LAYER"] = "One floor of the Wired.",
		["CABLE"] = "A wire that carries the signal.",
		["ERASE"] = "Delete for good.",
		["VOICE"] = "What a speaker lends to a message.",
		["FADED"] = "No longer bright.",

		["SIGNAL"] = "What a receiver tunes for.",
		["HOLLOW"] = "Empty on the inside.",
		["MEMORY"] = "What a dump holds, and a mind.",
		["SHADOW"] = "It follows you in the light.",
		["SILENT"] = "Without a sound.",
		["CIRCLE"] = "A ring with no end.",
		["MIRROR"] = "It shows you yourself.",
		["STATIC"] = "Hiss between stations.",
		["BROKEN"] = "No longer works.",
		["ECHOES"] = "Sounds that come back.",
		["PACKET"] = "A small parcel of data.",
		["ORPHAN"] = "A process whose parent is gone.",
		["WINTER"] = "The coldest season.",

		["PRESENT"] = "Right now. Also a gift.",
		["NETWORK"] = "Many machines linked together.",
		["CONNECT"] = "Join one thing to another.",
		["DISCARD"] = "Throw away.",
		["MESSAGE"] = "What you send to someone.",
		["ABANDON"] = "Leave behind for good.",
		["ARCHIVE"] = "Where old files are kept.",
		["COPYING"] = "Making a second one of everything.",
		["DROPOUT"] = "A signal that cuts off.",
		["PHANTOM"] = "Something seen that is not there.",
		["SERVERS"] = "Machines that answer requests.",
		["LAYERED"] = "Stacked one on another.",
		["SIGNALS"] = "More than one of what a receiver tunes for.",
	};

	public static string HintFor( string word ) => Hints.GetValueOrDefault( word, "No clue on file." );

	public static IntrusionServer FindServer( string id ) => Servers.FirstOrDefault( x => x.Id == id );

	public static BlueprintInfo FindBlueprint( string id ) => Blueprints.FirstOrDefault( x => x.Id == id );
}
