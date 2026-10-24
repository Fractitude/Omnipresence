using System.IO;
using System.Text;

namespace Sandbox;

/// <summary>
/// The server's record of what players do over time: joins and leaves, kills and deaths, every money movement, purchases,
/// kicks and bans. Host only; on a client every method does nothing. See <c>docs/stats.md</c>.
/// <para>
/// The engine has no database (no SQLite, no <c>System.Data</c>) in a game's sandbox, so this is two kinds of file in
/// the game's data folder, like the saves: an append-only log of <see cref="StatEvent"/> lines, one JSON object each,
/// <c>stats/events_{year}-{month}.jsonl</c>, and a <see cref="PlayerStatsSummary"/> per player, <c>stats/players/{steamId}.json</c>.
/// The log is the truth; the summaries are totals kept beside it. Lines wait in memory and are written every few seconds
/// by <see cref="StatsSystem"/>, so a crash loses seconds of events, not the file.
/// </para>
/// </summary>
public static class StatsLog
{
	/// <summary>The folder of the real log in the game's data folder.</summary>
	public const string DefaultFolder = "stats";

	/// <summary>The most lines kept waiting if the disk keeps refusing them; older ones are dropped.</summary>
	const int MaxPending = 20000;

	static string folder = DefaultFolder;
	static List<string> pending = new();
	static Dictionary<ulong, PlayerStatsSummary> summaries = new();
	static HashSet<ulong> dirty = new();

	// Per connected player: when this visit began and up to when their playtime is already counted.
	static Dictionary<ulong, DateTimeOffset> visitStart = new();
	static Dictionary<ulong, DateTimeOffset> countedTo = new();

	static BaseFileSystem Data => FileSystem.Data;

	static bool Active => Networking.IsHost;

	/// <summary>
	/// Keep the log in another folder (the test data, see <c>progress_test_data</c>). What is waiting is written to the
	/// old folder first.
	/// </summary>
	public static void UseFolder( string name )
	{
		Flush();
		folder = string.IsNullOrWhiteSpace( name ) ? DefaultFolder : name;
		summaries = new();
		dirty = new();
	}

	// -- Players coming and going -------------------------------------------------------------------------------------

	/// <summary>
	/// A player connected. Starts counting their playtime.
	/// </summary>
	public static void PlayerJoined( Connection connection )
	{
		if ( !Active || connection is null )
			return;

		var now = DateTimeOffset.UtcNow;
		var id = connection.SteamId;
		var summary = SummaryOf( id, connection.DisplayName );

		// A second join without a leave (a hot reload, a reconnect) closes the first visit.
		if ( visitStart.ContainsKey( id ) )
			PlayerLeft( connection );

		visitStart[id] = now;
		countedTo[id] = now;
		summary.Joins++;
		summary.LastSeen = now;
		Dirty( id );

		Write( new() { Kind = StatKind.Join, SteamId = id, Name = connection.DisplayName } );
	}

	/// <summary>
	/// A player disconnected. Writes how long the visit lasted.
	/// </summary>
	public static void PlayerLeft( Connection connection )
	{
		if ( !Active || connection is null )
			return;

		var now = DateTimeOffset.UtcNow;
		var id = connection.SteamId;

		if ( !visitStart.TryGetValue( id, out var started ) )
			return;

		Accrue( id, now );
		var summary = SummaryOf( id, connection.DisplayName );
		summary.Leaves++;
		summary.LastSeen = now;
		Dirty( id );

		visitStart.Remove( id );
		countedTo.Remove( id );

		Write( new()
		{
			Kind = StatKind.Leave, SteamId = id, Name = connection.DisplayName,
			SessionSeconds = System.Math.Round( (now - started).TotalSeconds, 1 ),
		} );
	}

	// -- Fights -------------------------------------------------------------------------------------------------------

	/// <summary>
	/// A player killed another player.
	/// </summary>
	public static void Kill( Connection killer, Connection victim, string weapon )
	{
		if ( !Active || killer is null )
			return;

		weapon = Clean( weapon );
		var summary = SummaryOf( killer.SteamId, killer.DisplayName );
		summary.Kills++;
		var key = weapon ?? "unarmed";
		summary.KillsByWeapon[key] = summary.KillsByWeapon.GetValueOrDefault( key ) + 1;
		Dirty( killer.SteamId );

		Write( new()
		{
			Kind = StatKind.Kill, SteamId = killer.SteamId, Name = killer.DisplayName,
			OtherId = victim?.SteamId, OtherName = victim?.DisplayName, Weapon = weapon,
		} );
	}

	/// <summary>
	/// A player died. <paramref name="killerName"/> is whoever or whatever did it when it was not a player.
	/// </summary>
	public static void Death( Connection victim, Connection killer, string killerName, string weapon, string cause )
	{
		if ( !Active || victim is null )
			return;

		SummaryOf( victim.SteamId, victim.DisplayName ).Deaths++;
		Dirty( victim.SteamId );

		Write( new()
		{
			Kind = StatKind.Death, SteamId = victim.SteamId, Name = victim.DisplayName,
			OtherId = killer?.SteamId, OtherName = killer?.DisplayName ?? Clean( killerName ),
			Weapon = Clean( weapon ), Cause = Clean( cause ),
		} );
	}

	// -- Money --------------------------------------------------------------------------------------------------------

	/// <summary>
	/// A balance changed. <paramref name="amount"/> is signed; <paramref name="balance"/> is the balance after it.
	/// <paramref name="other"/> is the giver (money in) or receiver (money out) when there is one; without one,
	/// <paramref name="mechanic"/> and <paramref name="detail"/> say where it came from.
	/// </summary>
	public static void Money(
		Connection player, string currency, long amount, long balance, string mechanic, string detail = null,
		Connection other = null )
	{
		if ( player is not null )
			Money( player.SteamId, player.DisplayName, currency, amount, balance, mechanic, detail, other?.SteamId,
				other?.DisplayName );
	}

	/// <summary>
	/// <see cref="Money(Connection, string, long, long, string, string, Connection)"/> for a player who may be offline.
	/// </summary>
	public static void Money(
		ulong steamId, string name, string currency, long amount, long balance, string mechanic, string detail = null,
		ulong? otherId = null, string otherName = null )
	{
		if ( !Active || amount == 0 )
			return;

		var summary = SummaryOf( steamId, name );
		summary.Transactions++;

		switch ( currency )
		{
			case MoneyCash:
				summary.Cash = balance;
				break;
			case MoneyCredits:
				summary.Credits = balance;
				break;
			case MoneyCrypto:
				summary.Crypto = balance;
				break;
		}

		Dirty( steamId );

		Write( new()
		{
			Kind = StatKind.Money, SteamId = steamId, Name = name, Amount = amount, Currency = currency,
			Balance = balance, Mechanic = mechanic, Detail = Clean( detail ), OtherId = otherId,
			OtherName = Clean( otherName ),
		} );
	}

	public const string MoneyCash = "cash";
	public const string MoneyCredits = "credits";
	public const string MoneyCrypto = "crypto";

	// -- Purchases ----------------------------------------------------------------------------------------------------

	/// <summary>
	/// A player bought something: a placeable, a piece of hardware, and later a house, a car or a weapon. The money has
	/// its own line (<see cref="Money(Connection, string, long, long, string, string, Connection)"/>); this one says
	/// what it bought.
	/// </summary>
	public static void Purchase( Connection player, string itemKind, string itemId, string itemName, long price )
	{
		if ( !Active || player is null )
			return;

		SummaryOf( player.SteamId, player.DisplayName ).Purchases++;
		Dirty( player.SteamId );

		Write( new()
		{
			Kind = StatKind.Purchase, SteamId = player.SteamId, Name = player.DisplayName, ItemKind = itemKind,
			ItemId = itemId, Detail = Clean( itemName ), Amount = price,
		} );
	}

	// -- Discipline ---------------------------------------------------------------------------------------------------

	/// <summary>
	/// A player was kicked. <paramref name="by"/> is the admin, or null for the server itself.
	/// </summary>
	public static void Kicked( Connection target, string reason, Connection by = null )
	{
		if ( !Active || target is null )
			return;

		SummaryOf( target.SteamId, target.DisplayName ).Kicks++;
		Dirty( target.SteamId );

		Write( new()
		{
			Kind = StatKind.Kick, SteamId = target.SteamId, Name = target.DisplayName, Reason = Clean( reason ),
			OtherId = by?.SteamId, OtherName = by?.DisplayName,
		} );
	}

	/// <summary>
	/// A player was banned. There is no ban system yet (see plans/stats.md); the one that comes calls this.
	/// </summary>
	public static void Banned( ulong steamId, string name, string reason, Connection by = null )
	{
		if ( !Active )
			return;

		SummaryOf( steamId, name ).Bans++;
		Dirty( steamId );

		Write( new()
		{
			Kind = StatKind.Ban, SteamId = steamId, Name = name, Reason = Clean( reason ), OtherId = by?.SteamId,
			OtherName = by?.DisplayName,
		} );
	}

	// -- Reading ------------------------------------------------------------------------------------------------------

	/// <summary>
	/// The totals of a player, from memory or disk, or null if the log has never seen them. Host only.
	/// </summary>
	public static PlayerStatsSummary Find( ulong steamId )
	{
		if ( !Active )
			return null;

		if ( summaries.TryGetValue( steamId, out var known ) )
			return known;

		return TryLoad( steamId );
	}

	// -- Writing ------------------------------------------------------------------------------------------------------

	static void Write( StatEvent e )
	{
		e.At = DateTimeOffset.UtcNow;
		pending.Add( Json.Serialize( e ).ReplaceLineEndings( " " ) );

		if ( pending.Count > MaxPending )
			pending.RemoveRange( 0, pending.Count - MaxPending );
	}

	static void Dirty( ulong id ) => dirty.Add( id );

	static string Clean( string text ) => string.IsNullOrWhiteSpace( text ) ? null : text.Trim();

	/// <summary>
	/// Add the time since the last count to a connected player's playtime.
	/// </summary>
	static void Accrue( ulong id, DateTimeOffset now )
	{
		if ( !countedTo.TryGetValue( id, out var from ) || !summaries.TryGetValue( id, out var summary ) )
			return;

		summary.PlaytimeSeconds += System.Math.Max( 0, (now - from).TotalSeconds );
		summary.LastSeen = now;
		countedTo[id] = now;
		Dirty( id );
	}

	/// <summary>
	/// Count everyone's playtime up to now and write what is waiting: the event lines, then the changed summaries. Called
	/// every few seconds by <see cref="StatsSystem"/>; safe to call at any time.
	/// </summary>
	public static void Flush()
	{
		if ( !Active )
			return;

		var now = DateTimeOffset.UtcNow;

		foreach ( var id in countedTo.Keys.ToList() )
			Accrue( id, now );

		if ( pending.Count == 0 && dirty.Count == 0 )
			return;

		try
		{
			var data = Data;
			data.CreateDirectory( folder );

			if ( pending.Count > 0 )
			{
				var text = string.Join( "\n", pending ) + "\n";
				using ( var stream = data.OpenWrite( $"{folder}/events_{now:yyyy-MM}.jsonl", FileMode.Append ) )
				{
					var bytes = Encoding.UTF8.GetBytes( text );
					stream.Write( bytes, 0, bytes.Length );
				}

				pending.Clear();
			}

			foreach ( var id in dirty.ToList() )
			{
				if ( summaries.TryGetValue( id, out var summary ) )
				{
					FileProgressStore.WriteSafely( data, $"{folder}/players", $"{folder}/players/{id}.json",
						Json.Serialize( summary ) );
				}

				dirty.Remove( id );
			}
		}
		catch ( System.Exception e )
		{
			// Keep what is waiting; the next flush tries again.
			Log.Warning( $"Stats: can't write the log: {e.Message}" );
		}
	}

	static PlayerStatsSummary SummaryOf( ulong id, string name )
	{
		if ( !summaries.TryGetValue( id, out var summary ) )
		{
			summary = TryLoad( id ) ?? new() { SteamId = id, FirstSeen = DateTimeOffset.UtcNow };
			summaries[id] = summary;
		}

		if ( !string.IsNullOrWhiteSpace( name ) )
			summary.Name = name;

		return summary;
	}

	static PlayerStatsSummary TryLoad( ulong id )
	{
		try
		{
			var summary = FileProgressStore.TryReadSafely<PlayerStatsSummary>( Data, $"{folder}/players/{id}.json" );

			if ( summary is not null )
				summaries[id] = summary;

			return summary;
		}
		catch ( System.Exception e )
		{
			Log.Warning( $"Stats: can't read the totals of {id}: {e.Message}" );
			return null;
		}
	}
}
