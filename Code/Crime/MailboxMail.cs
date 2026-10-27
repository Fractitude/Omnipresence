namespace Sandbox;

/// <summary>
/// Something waiting in a mailbox: a supply bought in the Hush shop that has arrived, with the player it was bought
/// for written on it.
/// </summary>
/// <remarks>
/// <see cref="For"/> says who it is for, never who may take it: a mailbox is open to everyone, so any player who opens
/// the box may take anything in it. The name is only there for the sentence the thief and the owner are told.
/// </remarks>
public sealed class MailLetter
{
	/// <summary>The Steam id of the player it was bought for.</summary>
	public ulong For { get; set; }

	/// <summary>The item kind, from <see cref="ItemCatalog"/>.</summary>
	public string Kind { get; set; } = "";

	public int Count { get; set; }
	public int Quality { get; set; }

	/// <summary>Game seconds when it reached the box.</summary>
	public long At { get; set; }
}

/// <summary>
/// What is waiting in each mailbox, kept in <c>mailboxes.json</c> in the data folder, under the box's id
/// (<see cref="MailBox.BoxId"/>).
/// </summary>
/// <remarks>
/// The mail is the box's and not the player's, because a box is open to everyone and anything in it may be taken by
/// whoever opens it. A shop delivery is moved here by <see cref="Receive"/> when it reaches the mailbox, so taking it
/// never touches another player's record, which may not even be loaded (its owner may be away).
/// <para>
/// Host only, written at once. Not <c>readonly</c> (see AGENTS.md, "Static state and hot reload"): loaded the first
/// time it is asked.
/// </para>
/// </remarks>
public static class MailboxMail
{
	/// <summary>How many letters one box keeps. The oldest are dropped.</summary>
	public const int MaxLetters = 40;

	const string DefaultFile = "mailboxes.json";

	static Dictionary<string, List<MailLetter>> boxes;
	static string fileName;

	/// <summary>
	/// The file in the data folder that keeps the mail. A test uses another one so it never touches the real one.
	/// </summary>
	public static string FileName => string.IsNullOrEmpty( fileName ) ? DefaultFile : fileName;

	/// <summary>
	/// Keep the mail in another file from now on, and read it again. For tests. Host only.
	/// </summary>
	public static void UseFile( string file )
	{
		if ( !Networking.IsHost )
			return;

		fileName = file;
		boxes = null;
	}

	static Dictionary<string, List<MailLetter>> Boxes => boxes ??= Load();

	/// <summary>How many letters wait in this box. Host only; zero everywhere else.</summary>
	public static int CountOf( string boxId )
	{
		if ( !Networking.IsHost || string.IsNullOrEmpty( boxId ) )
			return 0;

		return Boxes.TryGetValue( boxId, out var letters ) ? letters.Count : 0;
	}

	/// <summary>What waits in this box, oldest first. Host only; empty everywhere else.</summary>
	public static List<MailLetter> In( string boxId )
	{
		if ( !Networking.IsHost || string.IsNullOrEmpty( boxId )
		     || !Boxes.TryGetValue( boxId, out var letters ) )
			return [];

		return letters.ToList();
	}

	/// <summary>
	/// Put a letter in a box. Returns it, or null when nothing was put there. Host only.
	/// </summary>
	public static MailLetter Deliver( string boxId, ulong forSteamId, string kind, int count, int quality )
	{
		if ( !Networking.IsHost || string.IsNullOrEmpty( boxId ) || string.IsNullOrWhiteSpace( kind ) || count < 1 )
			return null;

		var letter = new MailLetter
		{
			For = forSteamId,
			Kind = kind,
			Count = count,
			Quality = System.Math.Clamp( quality, 0, 100 ),
			At = WorldClock.Now,
		};

		if ( !Boxes.TryGetValue( boxId, out var letters ) )
			letters = Boxes[boxId] = new();

		letters.Add( letter );

		if ( letters.Count > MaxLetters )
			letters.RemoveRange( 0, letters.Count - MaxLetters );

		Save();
		Live( boxId )?.ReadMail();
		return letter;
	}

	/// <summary>
	/// Take what <paramref name="take"/> accepts out of the box, oldest first, and leave the rest there (a bag that is
	/// full must not cost a letter). Returns how many letters were taken. Host only.
	/// </summary>
	public static int Take( string boxId, System.Func<MailLetter, bool> take )
	{
		if ( !Networking.IsHost || take is null || string.IsNullOrEmpty( boxId )
		     || !Boxes.TryGetValue( boxId, out var letters ) )
			return 0;

		var before = letters.Count;
		letters.RemoveAll( x => take( x ) );
		var taken = before - letters.Count;

		if ( taken == 0 )
			return 0;

		if ( letters.Count == 0 )
			Boxes.Remove( boxId );

		Save();
		Live( boxId )?.ReadMail();
		return taken;
	}

	/// <summary>
	/// Move the shop deliveries that have just reached their mailbox out of the player's record and into the box they
	/// are addressed to (<see cref="CrimeRecord.MailboxId"/>), which anyone may take them out of. A delivery whose
	/// player has no box in this scene stays in the record, and is collected from any box, as it always was. Host only.
	/// </summary>
	public static void Receive( ulong steamId, CrimeRecord crime, IReadOnlyList<DeliveryRecord> arrived )
	{
		if ( !Networking.IsHost || crime is null || arrived is null || arrived.Count == 0 )
			return;

		if ( Live( crime.MailboxId ) is not { } box )
			return;

		foreach ( var delivery in arrived )
		{
			if ( !crime.Deliveries.Remove( delivery ) )
				continue;

			Deliver( box.BoxId, steamId, delivery.Kind, delivery.Count, delivery.Quality );
		}
	}

	/// <summary>The box with this id in the scene, or null when it is not here now.</summary>
	static MailBox Live( string boxId ) =>
		Game.ActiveScene.IsValid() && !string.IsNullOrEmpty( boxId )
			? Game.ActiveScene.GetAll<MailBox>().FirstOrDefault( x => x.BoxId == boxId )
			: null;

	// -- The file -----------------------------------------------------------------------------------------------------

	static Dictionary<string, List<MailLetter>> Load()
	{
		var loaded = new Dictionary<string, List<MailLetter>>();

		try
		{
			if ( FileSystem.Data is { } data
			     && FileProgressStore.TryReadSafely<Dictionary<string, List<MailLetter>>>( data, FileName ) is { } saved )
				loaded = saved;
		}
		catch ( System.Exception e )
		{
			Log.Warning( e, $"Couldn't read {FileName}: {e.Message}" );
		}

		Sanitise( loaded );
		return loaded;
	}

	/// <summary>
	/// A file is outside input: drop a letter with no kind or no count, clamp what is left, and keep the newest
	/// <see cref="MaxLetters"/> of each box. A box left empty goes. Returns how many letters were dropped.
	/// </summary>
	static int Sanitise( Dictionary<string, List<MailLetter>> loaded )
	{
		var dropped = 0;

		foreach ( var boxId in loaded.Keys.ToList() )
		{
			var letters = loaded[boxId] ?? new();
			var before = letters.Count;

			letters.RemoveAll( x => x is null || string.IsNullOrWhiteSpace( x.Kind ) || x.Count < 1 );

			foreach ( var letter in letters )
			{
				letter.Count = System.Math.Clamp( letter.Count, 1, ItemCatalog.MaxCount );
				letter.Quality = System.Math.Clamp( letter.Quality, 0, 100 );
			}

			if ( letters.Count > MaxLetters )
				letters.RemoveRange( 0, letters.Count - MaxLetters );

			dropped += before - letters.Count;

			if ( letters.Count == 0 )
				loaded.Remove( boxId );
			else
				loaded[boxId] = letters;
		}

		if ( dropped > 0 )
			Log.Warning( $"Mailbox: {dropped} letters of {FileName} were invalid and are dropped" );

		return dropped;
	}

	static void Save()
	{
		try
		{
			if ( FileSystem.Data is { } data )
				FileProgressStore.WriteSafely( data, "", FileName, Json.Serialize( Boxes ) );
		}
		catch ( System.Exception e )
		{
			Log.Warning( e, $"Couldn't save {FileName}: {e.Message}" );
		}
	}
}
