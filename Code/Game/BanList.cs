namespace Sandbox;

/// <summary>One banned player, as kept in <c>bans.json</c>.</summary>
public sealed class BanEntry
{
	public ulong SteamId { get; set; }
	public string Name { get; set; } = "";
	public string Reason { get; set; } = "";
	public ulong BannedBy { get; set; }
	public DateTime Date { get; set; }
}

/// <summary>
/// The players banned from this server, kept by the host in <c>bans.json</c>. <see cref="BanSystem"/> refuses their
/// connections. Host only: the list never goes to clients.
/// </summary>
public static class BanList
{
	const string FileName = "bans.json";

	static Dictionary<ulong, BanEntry> bans;

	static Dictionary<ulong, BanEntry> Bans => bans ??= Load();

	public static IEnumerable<BanEntry> All => Bans.Values.OrderByDescending( b => b.Date );

	public static BanEntry Find( ulong steamId ) => Bans.GetValueOrDefault( steamId );

	public static bool IsBanned( ulong steamId ) => Bans.ContainsKey( steamId );

	/// <summary>Adds a ban and saves it. False if the player is already banned.</summary>
	public static bool Add( ulong steamId, string name, string reason, ulong by )
	{
		if ( !Networking.IsHost || steamId == 0 || Bans.ContainsKey( steamId ) )
			return false;

		Bans[steamId] = new BanEntry
		{
			SteamId = steamId, Name = name ?? "", Reason = reason ?? "", BannedBy = by, Date = DateTime.UtcNow,
		};
		Save();
		return true;
	}

	/// <summary>Removes a ban and saves. False if the player was not banned.</summary>
	public static bool Remove( ulong steamId )
	{
		if ( !Networking.IsHost || !Bans.Remove( steamId ) )
			return false;

		Save();
		return true;
	}

	static Dictionary<ulong, BanEntry> Load()
	{
		var loaded = new Dictionary<ulong, BanEntry>();

		try
		{
			if ( FileSystem.Data is { } data && data.FileExists( FileName ) )
			{
				var saved = Json.Deserialize<List<BanEntry>>( data.ReadAllText( FileName ) );

				if ( saved is not null )
					foreach ( var b in saved )
						loaded[b.SteamId] = b;
			}
		}
		catch ( Exception e )
		{
			Log.Warning( $"Could not read {FileName}: {e.Message}" );
		}

		return loaded;
	}

	static void Save()
	{
		try
		{
			FileSystem.Data?.WriteAllText( FileName, Json.Serialize( Bans.Values.ToList() ) );
		}
		catch ( Exception e )
		{
			Log.Warning( $"Could not write {FileName}: {e.Message}" );
		}
	}
}

/// <summary>
/// Refuses a banned player's connection. A <see cref="GameObjectSystem"/>, so the engine makes it in every scene.
/// </summary>
public sealed class BanSystem : GameObjectSystem<BanSystem>, Component.INetworkListener
{
	public BanSystem( Scene scene ) : base( scene )
	{
	}

	bool Component.INetworkListener.AcceptConnection( Connection channel, ref string reason )
	{
		if ( !Networking.IsHost || BanList.Find( channel.SteamId ) is not { } ban )
			return true;

		reason = string.IsNullOrWhiteSpace( ban.Reason ) ? "You are banned from this server" : $"Banned: {ban.Reason}";
		return false;
	}
}
