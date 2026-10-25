namespace Sandbox;

/// <summary>One banned player, as kept in <c>bans.json</c>.</summary>
public sealed class BanEntry
{
	public ulong SteamId { get; set; }
	public string Name { get; set; } = "";
	public string Reason { get; set; } = "";
	public ulong BannedBy { get; set; }
	public DateTime Date { get; set; }

	/// <summary>When the ban ends (UTC), or null for a ban that does not.</summary>
	public DateTime? ExpiresAt { get; set; }

	public bool IsPermanent() => ExpiresAt is null;

	public bool IsExpired() => ExpiresAt is { } end && end <= DateTime.UtcNow;

	/// <summary>How long is left, or null for a permanent ban.</summary>
	public TimeSpan? Remaining() => ExpiresAt is { } end ? end - DateTime.UtcNow : null;

	/// <summary>The sentence a banned player is shown when the server refuses them.</summary>
	public string Message()
	{
		var until = Remaining() is { } left ? $"for {BanDuration.Describe( left )} more" : "permanently";
		return string.IsNullOrWhiteSpace( Reason ) ? $"Banned {until}" : $"Banned {until}: {Reason}";
	}
}

/// <summary>
/// Durations of bans: <c>30m</c>, <c>12h</c>, <c>7d</c>, <c>2w</c>, or <c>perm</c> for one that never ends.
/// </summary>
public static class BanDuration
{
	/// <summary>
	/// Reads a duration. Null in <paramref name="duration"/> means permanent. False if the text is not a duration.
	/// </summary>
	public static bool TryParse( string text, out TimeSpan? duration )
	{
		duration = null;
		text = text?.Trim().ToLowerInvariant() ?? "";

		if ( text is "" or "0" or "perm" or "permanent" or "forever" )
			return true;

		if ( text.Length < 2 || !double.TryParse( text[..^1], out var amount ) || amount <= 0 )
			return false;

		duration = text[^1] switch
		{
			'm' => TimeSpan.FromMinutes( amount ),
			'h' => TimeSpan.FromHours( amount ),
			'd' => TimeSpan.FromDays( amount ),
			'w' => TimeSpan.FromDays( amount * 7 ),
			_ => null,
		};

		return duration is not null;
	}

	/// <summary>A span as the largest unit that fits: "3 days", "45 minutes".</summary>
	public static string Describe( TimeSpan span )
	{
		if ( span.TotalDays >= 1 )
			return Plural( (int)Math.Ceiling( span.TotalDays ), "day" );

		if ( span.TotalHours >= 1 )
			return Plural( (int)Math.Ceiling( span.TotalHours ), "hour" );

		return Plural( Math.Max( 1, (int)Math.Ceiling( span.TotalMinutes ) ), "minute" );
	}

	static string Plural( int n, string unit ) => n == 1 ? $"1 {unit}" : $"{n} {unit}s";
}

/// <summary>
/// The players banned from this server, kept by the host in <c>bans.json</c>. <see cref="BanSystem"/> refuses their
/// connections. A ban with an end date lifts itself. Host only: the list reaches an admin's screen only through
/// <see cref="GameManager.RpcRequestBans"/>.
/// </summary>
public static class BanList
{
	const string FileName = "bans.json";

	static Dictionary<ulong, BanEntry> bans;

	static Dictionary<ulong, BanEntry> Bans => bans ??= Load();

	/// <summary>Every ban in force, newest first.</summary>
	public static IEnumerable<BanEntry> All
	{
		get
		{
			Purge();
			return Bans.Values.OrderByDescending( b => b.Date );
		}
	}

	/// <summary>The ban on a player, or null if they are not banned (or it has run out).</summary>
	public static BanEntry Find( ulong steamId )
	{
		Purge();
		return Bans.GetValueOrDefault( steamId );
	}

	public static bool IsBanned( ulong steamId ) => Find( steamId ) is not null;

	/// <summary>
	/// Adds a ban and saves it. A null <paramref name="duration"/> is permanent. False if the player is already banned.
	/// </summary>
	public static bool Add( ulong steamId, string name, string reason, ulong by, TimeSpan? duration = null )
	{
		if ( !Networking.IsHost || steamId == 0 || IsBanned( steamId ) )
			return false;

		Bans[steamId] = new BanEntry
		{
			SteamId = steamId, Name = name ?? "", Reason = reason ?? "", BannedBy = by, Date = DateTime.UtcNow,
			ExpiresAt = duration is { } d ? DateTime.UtcNow + d : null,
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

	/// <summary>Drops the bans whose time has run out.</summary>
	static void Purge()
	{
		var over = Bans.Values.Where( b => b.IsExpired() ).Select( b => b.SteamId ).ToList();

		if ( over.Count == 0 )
			return;

		foreach ( var id in over )
			Bans.Remove( id );

		Save();
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
/// Refuses a banned player's connection before they join, with the ban's reason as the message they are shown. A
/// <see cref="GameObjectSystem"/>, so the engine makes it in every scene.
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

		reason = ban.Message();
		return false;
	}
}
