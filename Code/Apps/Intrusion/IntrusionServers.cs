namespace OmnipresenceApps;

/// <summary>
/// A dead server in the Wired: an abandoned machine whose terminal can be cracked for a reward, once per player.
/// </summary>
/// <param name="Id">Saved in the player's progress, so never rename one.</param>
/// <param name="Layer">The layer of the Wired it sits in, for the list.</param>
/// <param name="WordLength">How long each word in the memory dump is.</param>
/// <param name="WordCount">How many words are in the dump, one of which is the password.</param>
/// <param name="Attempts">How many wrong guesses lock the terminal.</param>
/// <param name="Tricks">How many bracket tricks are hidden in the dump.</param>
/// <param name="Blueprint">The id of the blueprint it holds, or empty.</param>
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
	string Blueprint );

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

	public static IntrusionServer FindServer( string id ) => Servers.FirstOrDefault( x => x.Id == id );

	public static BlueprintInfo FindBlueprint( string id ) => Blueprints.FirstOrDefault( x => x.Id == id );
}
