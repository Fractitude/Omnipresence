namespace Sandbox;

/// <summary>
/// Console commands for reading the stats log. Host only: the log lives on the host.
/// </summary>
public static class StatsCommands
{
	/// <summary>
	/// With no id, lists every saved player with the id to pass back in. With an id, logs that player's totals.
	/// </summary>
	[ConCmd( "stats_show" )]
	public static void Show( ulong steamId = 0 )
	{
		if ( !Networking.IsHost )
		{
			Log.Info( "Stats are kept on the host" );
			return;
		}

		if ( steamId == 0 )
		{
			ListSaved();
			return;
		}

		if ( StatsLog.Find( steamId ) is not { } s )
		{
			Log.Info( $"No stats for {steamId}. Run stats_show with no id to list the saved players." );
			return;
		}

		var played = TimeSpan.FromSeconds( s.PlaytimeSeconds );
		var weapons = string.Join( ", ", s.KillsByWeapon.OrderByDescending( x => x.Value ).Select( x => $"{x.Key} {x.Value}" ) );

		Log.Info( $"{s.Name} ({s.SteamId}): first seen {s.FirstSeen:u}, last seen {s.LastSeen:u}, {s.Joins} joins, "
		          + $"{(int)played.TotalHours}h {played.Minutes}m played, {s.Kills} kills ({weapons}), {s.Deaths} deaths, "
		          + $"{s.Kicks} kicks, {s.Bans} bans, {s.Transactions} transactions, {s.Purchases} purchases, "
		          + $"${s.Cash}, {s.Credits} credits, {s.Crypto} crypto" );
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

		Log.Info( $"{players.Count} saved players, most recent first. Run stats_show <steamId> to read one:" );

		foreach ( var s in players )
		{
			var played = TimeSpan.FromSeconds( s.PlaytimeSeconds );
			Log.Info( $"  {s.SteamId}  {s.Name}  last seen {s.LastSeen:u}, {(int)played.TotalHours}h {played.Minutes}m played" );
		}
	}
}
