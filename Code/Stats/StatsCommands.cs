using System.Globalization;
using System.Text;

namespace Sandbox;

/// <summary>
/// Console commands for reading the stats log. Host only: the log lives on the host.
/// </summary>
public static class StatsCommands
{
	/// <summary>
	/// With no player, lists every saved player. With a Steam id or a name, logs that player's totals:
	/// <c>stats_show "Yodeling Cat"</c>. A name matches exactly first, then part of a name, when only one saved player
	/// fits. Case, accents and punctuation don't count: <c>stats_show tomas</c> finds Tomáš.
	/// </summary>
	[ConCmd( "stats_show" )]
	public static void Show( string player = "" )
	{
		if ( !Networking.IsHost )
		{
			Log.Info( "Stats are kept on the host" );
			return;
		}

		player = player?.Trim() ?? "";

		if ( player.Length == 0 )
		{
			ListSaved();
			return;
		}

		if ( Find( player ) is not { } s )
			return;

		var played = TimeSpan.FromSeconds( s.PlaytimeSeconds );
		var weapons = string.Join( ", ", s.KillsByWeapon.OrderByDescending( x => x.Value ).Select( x => $"{x.Key} {x.Value}" ) );

		Log.Info( $"{s.Name} ({s.SteamId}): first seen {s.FirstSeen:u}, last seen {s.LastSeen:u}, {s.Joins} joins, "
		          + $"{(int)played.TotalHours}h {played.Minutes}m played, {s.Kills} kills ({weapons}), {s.Deaths} deaths, "
		          + $"{s.Kicks} kicks, {s.Bans} bans, {s.Transactions} transactions, {s.Purchases} purchases, "
		          + $"${s.Cash}, {s.Credits} credits, {s.Crypto} crypto" );
	}

	/// <summary>
	/// The saved player a console argument names: a Steam id, or a name. Logs why there is none, or lists the candidates
	/// when a name fits more than one player.
	/// </summary>
	static PlayerStatsSummary Find( string player )
	{
		if ( ulong.TryParse( player, out var steamId ) )
		{
			if ( StatsLog.Find( steamId ) is { } known )
				return known;

			Log.Info( $"No stats for {steamId}. Run stats_show with no argument to list the saved players." );
			return null;
		}

		var players = StatsLog.SavedPlayers();
		var key = Plain( player );
		var matches = key.Length == 0 ? new List<PlayerStatsSummary>() : players.Where( x => Plain( x.Name ) == key ).ToList();

		if ( matches.Count == 0 && key.Length > 0 )
			matches = players.Where( x => Plain( x.Name ).Contains( key ) ).ToList();

		if ( matches.Count == 1 )
			return matches[0];

		if ( matches.Count == 0 )
		{
			Log.Info( $"No saved player named \"{player}\". Run stats_show with no argument to list them." );
			return null;
		}

		Log.Info( $"{matches.Count} saved players match \"{player}\". Pass the Steam id of the one you want:" );

		foreach ( var m in matches )
			Log.Info( $"  {m.SteamId}  {m.Name}" );

		return null;
	}

	/// <summary>
	/// A name as plain lowercase letters and digits: accents are taken off their letters, and spaces and punctuation are
	/// dropped. So "Tomáš" and "tomas" both become "tomas", and "Yodeling Cat" and "yodelingcat" match.
	/// </summary>
	static string Plain( string text )
	{
		if ( string.IsNullOrEmpty( text ) )
			return "";

		var letters = text.Normalize( NormalizationForm.FormD )
			.Where( c => CharUnicodeInfo.GetUnicodeCategory( c ) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit( c ) )
			.Select( c => char.ToLowerInvariant( c ) );

		return new string( letters.ToArray() );
	}

	/// <summary>
	/// Lists everyone connected right now, one per line: the Steam id, then the name. Copy the id to kick or ban them.
	/// </summary>
	[ConCmd( "players" )]
	public static void Players()
	{
		if ( !Networking.IsHost )
		{
			Log.Info( "Players are listed by the host" );
			return;
		}

		var connected = Connection.All.ToList();
		Log.Info( $"{connected.Count} players connected:" );

		foreach ( var c in connected )
			Log.Info( $"  {c.SteamId}  {c.DisplayName}" );
	}

	/// <summary>
	/// Bans a Steam id (see <c>players</c> or <c>stats_show</c>) and kicks them if connected: <c>ban 7656119… "cheating"</c>.
	/// </summary>
	[ConCmd( "ban" )]
	public static void Ban( ulong steamId, string reason = "" )
	{
		if ( !Networking.IsHost )
		{
			Log.Info( "Bans are kept by the host" );
			return;
		}

		var name = StatsLog.Find( steamId )?.Name ?? steamId.ToString();

		Log.Info( GameManager.Current?.Ban( steamId, name, reason ) == true
			? $"Banned {name} ({steamId})"
			: $"{steamId} is already banned" );
	}

	/// <summary>
	/// Lifts the ban on a Steam id: <c>unban 7656119…</c>. With no id, lists everyone banned.
	/// </summary>
	[ConCmd( "unban" )]
	public static void Unban( ulong steamId = 0 )
	{
		if ( !Networking.IsHost )
		{
			Log.Info( "Bans are kept by the host" );
			return;
		}

		if ( steamId == 0 )
		{
			var all = BanList.All.ToList();
			Log.Info( $"{all.Count} banned. Run unban <steamId> to lift one:" );

			foreach ( var b in all )
				Log.Info( $"  {b.SteamId}  {b.Name}  {b.Date:u}  {b.Reason}" );

			return;
		}

		Log.Info( BanList.Remove( steamId ) ? $"Unbanned {steamId}" : $"{steamId} is not banned" );
	}

	/// <summary>
	/// One line per saved player: the id to pass to <c>stats_show</c>, the name and the playtime.
	/// </summary>
	static void ListSaved()
	{
		var players = StatsLog.SavedPlayers();

		if ( players.Count == 0 )
		{
			Log.Info( "No saved players yet" );
			return;
		}

		Log.Info( $"{players.Count} saved players, most recent first. Run stats_show <name or steamId> to read one:" );

		foreach ( var s in players )
		{
			var played = TimeSpan.FromSeconds( s.PlaytimeSeconds );
			Log.Info( $"  {s.SteamId}  {s.Name}  last seen {s.LastSeen:u}, {(int)played.TotalHours}h {played.Minutes}m played" );
		}
	}
}
