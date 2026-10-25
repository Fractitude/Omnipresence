namespace Sandbox;

/// <summary>
/// One release in the changelog: the version it belongs to, a title and what changed.
/// </summary>
public sealed class ChangelogEntry
{
	public int Version { get; set; } = 1;
	public string Title { get; set; } = "";
	public List<string> Changes { get; set; } = new();
}

/// <summary>
/// Every piece of text on the welcome screen, plus its version. The defaults here are what a new world starts with;
/// the host changes them in <see cref="WelcomeScreenEditor"/>.
/// </summary>
public sealed class WelcomeData
{
	/// <summary>
	/// The version of these contents. Raised by 1 on every save in the editor: a player who has not seen it is shown the screen on
	/// joining, and the changelog entries newer than the version they saw are listed.
	/// </summary>
	public int Version { get; set; } = 1;

	public string Title { get; set; } = "Omnipresence in the Wired";
	public string WelcomeHeading { get; set; } = "Welcome";
	public string WelcomeBackHeading { get; set; } = "Welcome back";

	public string Intro { get; set; } =
		"A game inspired by Serial Experiments Lain. You live in the real world and in the Wired, the network under "
		+ "it. Earn, trade and hide your work, and reach the Wired from a computer.";

	public string RulesHeading { get; set; } = "Rules";

	public List<string> Rules { get; set; } = new()
	{
		"Be kind to other players. No harassment, hate or threats.",
		"No cheating, exploits or griefing. If you find a bug, report it instead of using it.",
		"Do not hurt people who are still starting out.",
		"The host's word is final.",
	};

	public string PlayButton { get; set; } = "Play";

	/// <summary>The releases, newest first.</summary>
	public List<ChangelogEntry> Changelog { get; set; } = new()
	{
		new()
		{
			Version = 1,
			Title = "Welcome screen",
			Changes = new()
			{
				"A welcome screen opens when you join. It tells you about the game, the rules and what changed.",
				"Type welcome in the console to open it again.",
			},
		},
	};

	/// <summary>The entries newer than a version, in the order they are listed.</summary>
	public IEnumerable<ChangelogEntry> Since( int version ) => Changelog.Where( x => x.Version > version );

	/// <summary>A copy to edit, so nothing changes until it is saved.</summary>
	public WelcomeData Copy() => Json.Deserialize<WelcomeData>( Json.Serialize( this ) ) ?? new();
}

/// <summary>
/// The welcome screen's text. The host keeps it (<c>welcome.json</c> in the data folder) and tells every client, on
/// join and whenever it changes (<see cref="WelcomeSystem"/>); only the host can change it, from
/// <see cref="WelcomeScreenEditor"/>, and nothing a client sends is ever read.
/// </summary>
/// <remarks>
/// Not <c>readonly</c>: a hot reload would reset these (see AGENTS.md, "Static state and hot reload").
/// </remarks>
public static class WelcomeContent
{
	const string FileName = "welcome.json";
	const int MaxText = 2000;
	const int MaxItems = 40;

	static WelcomeData data = new();
	static bool ready;

	/// <summary>The text to show.</summary>
	public static WelcomeData Data => data;

	/// <summary>
	/// Whether the text is the host's: loaded from its file on the host, or received from it on a client. Until then
	/// <see cref="Data"/> is only the default.
	/// </summary>
	public static bool IsReady => ready;

	/// <summary>Goes up whenever <see cref="Data"/> was replaced.</summary>
	public static int Revision { get; private set; }

	/// <summary>
	/// Read the saved text. Host only; called once by <see cref="WelcomeSystem"/>.
	/// </summary>
	internal static void LoadHost()
	{
		try
		{
			if ( FileSystem.Data is { } files && files.FileExists( FileName ) )
				data = Clean( Json.Deserialize<WelcomeData>( files.ReadAllText( FileName ) ) ?? new() );
		}
		catch ( System.Exception e )
		{
			Log.Warning( $"Couldn't read {FileName}, using the default text: {e.Message}" );
		}

		ready = true;
		Revision++;
	}

	/// <summary>
	/// Replace the text and save it; clients are told by <see cref="WelcomeSystem"/>. Host only.
	/// </summary>
	public static bool Save( WelcomeData edited )
	{
		if ( !Networking.IsHost || edited is null )
			return false;

		data = Clean( edited );
		ready = true;
		Revision++;

		try
		{
			FileProgressStore.WriteSafely( FileSystem.Data, "", FileName, Json.Serialize( data ) );
		}
		catch ( System.Exception e )
		{
			Log.Warning( $"Couldn't save {FileName}: {e.Message}" );
			return false;
		}

		return true;
	}

	/// <summary>
	/// Take the text the host sent. Client side; the host's own copy is <see cref="Data"/> already.
	/// </summary>
	internal static void Receive( string json )
	{
		try
		{
			data = Clean( Json.Deserialize<WelcomeData>( json ) ?? new() );
			ready = true;
			Revision++;
		}
		catch ( System.Exception e )
		{
			Log.Warning( e, "Couldn't read the welcome text" );
		}
	}

	/// <summary>
	/// Make text safe to keep and draw: trimmed, not too long, no empty list lines, a version of at least 1.
	/// </summary>
	static WelcomeData Clean( WelcomeData d )
	{
		d.Version = System.Math.Max( d.Version, 1 );
		d.Title = Text( d.Title );
		d.WelcomeHeading = Text( d.WelcomeHeading );
		d.WelcomeBackHeading = Text( d.WelcomeBackHeading );
		d.Intro = Text( d.Intro, keepLines: true );
		d.RulesHeading = Text( d.RulesHeading );
		d.PlayButton = Text( d.PlayButton );
		d.Rules = Lines( d.Rules );
		d.Changelog = (d.Changelog ?? new()).Where( x => x is not null ).Take( MaxItems ).ToList();

		foreach ( var entry in d.Changelog )
		{
			entry.Version = System.Math.Max( entry.Version, 1 );
			entry.Title = Text( entry.Title );
			entry.Changes = Lines( entry.Changes );
		}

		return d;
	}

	static string Text( string text, bool keepLines = false )
	{
		text = (text ?? "").Trim();

		// The editor's fields wrap, and Enter there starts a new line: a one-line text takes it as a space.
		if ( !keepLines )
			text = text.Replace( "\r\n", " " ).Replace( '\n', ' ' ).Replace( '\r', ' ' );

		return text.Length > MaxText ? text[..MaxText] : text;
	}

	static List<string> Lines( List<string> lines ) =>
		(lines ?? new()).Select( x => Text( x ) ).Where( x => x.Length > 0 ).Take( MaxItems ).ToList();
}

/// <summary>
/// Tells every client the welcome text. The host loads it, then sends it to each connection once it is in the world
/// and again whenever it changed (and now and then, in case a message went before the client could take it). Made in
/// every scene by the engine.
/// </summary>
public sealed class WelcomeSystem : GameObjectSystem<WelcomeSystem>
{
	const float Resend = 30;

	// Host side: the revision each connection was last told, and when.
	readonly Dictionary<System.Guid, (int Revision, RealTimeSince At)> told = new();
	RealTimeSince sinceCheck;

	public WelcomeSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.StartUpdate, 2, Tick, "WelcomeSystem.Tick" );
	}

	void Tick()
	{
		if ( Scene != Game.ActiveScene || !Game.IsPlaying || Scene.IsEditor || !Networking.IsHost || sinceCheck < 0.5f )
			return;

		sinceCheck = 0;

		if ( !WelcomeContent.IsReady )
			WelcomeContent.LoadHost();

		foreach ( var id in told.Keys.Where( x => Connection.All.All( y => y.Id != x ) ).ToList() )
			told.Remove( id );

		string json = null;

		foreach ( var connection in Connection.All )
		{
			// The host already has it; a client needs its scene to receive the message in.
			if ( connection == Connection.Local || Players.For<PlayerController>( connection ) is null )
				continue;

			if ( told.TryGetValue( connection.Id, out var last ) && last.Revision == WelcomeContent.Revision
			     && last.At < Resend )
				continue;

			told[connection.Id] = (WelcomeContent.Revision, 0);
			json ??= Json.Serialize( WelcomeContent.Data );

			using ( Rpc.FilterInclude( connection ) )
				ReceiveRpc( json );
		}
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ReceiveRpc( string json ) => WelcomeContent.Receive( json );
}
