namespace Sandbox;

/// <summary>
/// Which player each storage box in the world belongs to, kept in <c>storage.json</c> so a restart does not forget. The
/// contents are in the owner's record, but the box in the scene only learns its owner from here when it starts: a
/// player who is away would otherwise leave their box looking unclaimed, and someone else could take it. Like
/// <see cref="MailboxRegistry"/>, except that a player may own many boxes.
/// </summary>
/// <remarks>
/// Host only. Not <c>readonly</c> (see AGENTS.md, "Static state and hot reload"): loaded the first time it is asked.
/// </remarks>
public static class StorageRegistry
{
	const string DefaultFile = "storage.json";

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
	/// The Steam id that claimed this box, or 0 when nobody did. Host only.
	/// </summary>
	public static ulong OwnerOf( string boxId )
	{
		if ( !Networking.IsHost || string.IsNullOrEmpty( boxId ) )
			return 0;

		return Owners.GetValueOrDefault( boxId );
	}

	/// <summary>
	/// Write down that a player claimed a box. Returns false when the box already belongs to someone else. Host only.
	/// </summary>
	public static bool Claim( string boxId, ulong steamId )
	{
		if ( !Networking.IsHost || string.IsNullOrEmpty( boxId ) || steamId == 0 )
			return false;

		var claims = Owners;

		if ( claims.TryGetValue( boxId, out var owner ) )
			return owner == steamId;

		claims[boxId] = steamId;
		Save();
		return true;
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
