namespace Sandbox;

/// <summary>
/// Console commands for reading the stats log. Host only: the log lives on the host.
/// </summary>
public static class StatsCommands
{
	/// <summary>
	/// With no player, lists every saved player. With a Steam id or a name, logs that player's totals:
	/// <c>stats_show "Yodeling Cat"</c>. A name matches exactly (any case) first, then part of a name, when only one
	/// saved player fits.
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
		var matches = players.Where( x => string.Equals( x.Name, player, StringComparison.OrdinalIgnoreCase ) ).ToList();

		if ( matches.Count == 0 )
			matches = players.Where( x => x.Name?.Contains( player, StringComparison.OrdinalIgnoreCase ) == true ).ToList();

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
