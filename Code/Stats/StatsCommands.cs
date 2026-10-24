namespace Sandbox;

/// <summary>
/// Console commands for reading the stats log. Host only: the log lives on the host.
/// </summary>
public static class StatsCommands
{
	[ConCmd( "stats_show" )]
	public static void Show( ulong steamId = 0 )
	{
		if ( !Networking.IsHost )
		{
			Log.Info( "Stats are kept on the host" );
			return;
		}

		StatsLog.Flush();
		var id = steamId != 0 ? steamId : (ulong)Connection.Local.SteamId;

		if ( StatsLog.Find( id ) is not { } s )
		{
			Log.Info( $"No stats for {id}" );
			return;
		}

		var played = TimeSpan.FromSeconds( s.PlaytimeSeconds );
		var weapons = string.Join( ", ", s.KillsByWeapon.OrderByDescending( x => x.Value ).Select( x => $"{x.Key} {x.Value}" ) );

		Log.Info( $"{s.Name} ({s.SteamId}): first seen {s.FirstSeen:u}, last seen {s.LastSeen:u}, {s.Joins} joins, "
		          + $"{(int)played.TotalHours}h {played.Minutes}m played, {s.Kills} kills ({weapons}), {s.Deaths} deaths, "
		          + $"{s.Kicks} kicks, {s.Bans} bans, {s.Transactions} transactions, {s.Purchases} purchases, "
		          + $"${s.Cash}, {s.Credits} credits, {s.Crypto} crypto" );
	}
}
