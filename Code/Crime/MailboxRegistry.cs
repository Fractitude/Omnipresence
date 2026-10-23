namespace Sandbox;

/// <summary>
/// Which player each mailbox in the world belongs to, kept in <c>mailboxes.json</c> so a restart does not forget. A
/// player's record names the mailbox they claimed (<see cref="CrimeRecord.MailboxId"/>), but the box in the scene only
/// learns its owner while that player's own <see cref="PlayerCrime"/> runs; a player who is away would leave their box
/// looking unclaimed after a restart, and someone else could take it. The box reads its owner from here when it starts.
/// </summary>
/// <remarks>
/// Host only. A player has one mailbox, and a box has one owner. Not <c>readonly</c> (see AGENTS.md, "Static state and
/// hot reload"): loaded the first time it is asked.
/// </remarks>
public static class MailboxRegistry
{
	const string DefaultFile = "mailboxes.json";

	static Dictionary<string, ulong> owners;
	static string fileName;

	/// <summary>
	/// The file in the data folder that keeps the claims. A test uses another one so it never touches the real one.
	/// </summary>
	public static string FileName => string.IsNullOrEmpty( fileName ) ? DefaultFile : fileName;

	/// <summary>
	/// Keep the claims in another file from now on, and read them again. For tests. Host only.
	/// </summary>
	public static void UseFile( string file )
	{
		if ( !Networking.IsHost )
			return;

		fileName = file;
		owners = null;
	}

	static Dictionary<string, ulong> Owners => owners ??= Load();

	/// <summary>
	/// The Steam id that claimed this mailbox, or 0 when nobody did. Host only.
	/// </summary>
	public static ulong OwnerOf( string boxId )
	{
		if ( !Networking.IsHost || string.IsNullOrEmpty( boxId ) )
			return 0;

		return Owners.GetValueOrDefault( boxId );
	}

	/// <summary>
	/// Write down that a player claimed a mailbox, and let go of the one they had before. Host only.
	/// </summary>
	public static void Claim( string boxId, ulong steamId )
	{
		if ( !Networking.IsHost || string.IsNullOrEmpty( boxId ) || steamId == 0 )
			return;

		var claims = Owners;

		if ( claims.GetValueOrDefault( boxId ) == steamId && claims.Count( x => x.Value == steamId ) == 1 )
			return;

		foreach ( var other in claims.Where( x => x.Value == steamId ).Select( x => x.Key ).ToList() )
			claims.Remove( other );

		claims[boxId] = steamId;
		Save();
	}

	static Dictionary<string, ulong> Load()
	{
		try
		{
			if ( FileSystem.Data is { } data
			     && FileProgressStore.TryReadSafely<Dictionary<string, ulong>>( data, FileName ) is { } saved )
				return saved;
		}
		catch ( System.Exception e )
		{
			Log.Warning( e, $"Couldn't read {FileName}: {e.Message}" );
		}

		return new();
	}

	static void Save()
	{
		try
		{
			if ( FileSystem.Data is { } data )
				FileProgressStore.WriteSafely( data, "", FileName, Json.Serialize( Owners ) );
		}
		catch ( System.Exception e )
		{
			Log.Warning( e, $"Couldn't save {FileName}: {e.Message}" );
		}
	}
}
