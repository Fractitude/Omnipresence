using Sandbox.Diagnostics;

namespace Sandbox;

public sealed partial class GameManager
{
	/// <summary>
	/// Puts a line in everyone's chat. Host only.
	/// </summary>
	public void Notify( string text )
	{
		Assert.True( Networking.IsHost, "Only the host can send notifications" );
		NotifyRpc( text );
	}

	/// <summary>
	/// Puts a line in one player's chat. Host only.
	/// </summary>
	public void Notify( Connection connection, string text )
	{
		Assert.True( Networking.IsHost, "Only the host can send notifications" );

		using ( Rpc.FilterInclude( connection ) )
			NotifyRpc( text );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void NotifyRpc( string text )
	{
		Sandbox.Platform.Chat.AddText( text );
	}

	public static Connection FindPlayerWithName( string name, bool partial = true )
	{
		return Connection.All.FirstOrDefault( c =>
			partial
				? c.DisplayName.Contains( name, StringComparison.OrdinalIgnoreCase )
				: c.DisplayName.Equals( name, StringComparison.OrdinalIgnoreCase )
		);
	}

	/// <summary>
	/// Kicks a connected player with an optional reason, and writes it in the stats log. <paramref name="by"/> is the
	/// admin who asked, or null for the server itself. The engine's own <c>kick</c> command skips this.
	/// </summary>
	public void Kick( Connection connection, string reason = "Kicked", Connection by = null )
	{
		Assert.True( Networking.IsHost, "Only the host may kick players." );

		Notify( reason == "Kicked"
			? $"{connection.DisplayName} was kicked"
			: $"{connection.DisplayName} was kicked: {reason}" );

		StatsLog.Kicked( connection, reason == "Kicked" ? null : reason, by );
		StatsLog.Flush();
		connection.Kick( reason );
	}

	/// <summary>
	/// Bans a Steam id, kicks them if connected, and writes it in the stats log. <paramref name="by"/> is the admin who
	/// asked, or null for the server itself. False if they are already banned.
	/// </summary>
	public bool Ban( ulong steamId, string name, string reason = "", Connection by = null, TimeSpan? duration = null )
	{
		Assert.True( Networking.IsHost, "Only the host may ban players." );

		var connection = Connection.All.FirstOrDefault( c => c.SteamId == steamId );
		name = connection?.DisplayName ?? name;

		if ( !BanList.Add( steamId, name, reason, by?.SteamId ?? 0, duration ) )
			return false;

		StatsLog.Banned( steamId, name, reason, by );
		StatsLog.Flush();

		var length = duration is { } d ? $" for {BanDuration.Describe( d )}" : "";
		Notify( string.IsNullOrWhiteSpace( reason ) ? $"{name} was banned{length}" : $"{name} was banned{length}: {reason}" );
		connection?.Kick( BanList.Find( steamId )?.Message() ?? "Banned" );
		return true;
	}

	/// <summary>
	/// Whether a player may use the admin tools: the host, or anyone with the engine's <c>admin</c> permission.
	/// </summary>
	public static bool IsAdmin( Connection connection ) =>
		connection is not null && (connection.IsHost || connection.HasPermission( "admin" ));

	/// <summary>
	/// RPC to ban a player. Caller must be an admin. <paramref name="seconds"/> is how long, 0 for ever. The host and the
	/// caller cannot be banned.
	/// </summary>
	[Rpc.Host]
	public static void RpcBanPlayer( Connection target, string reason = "", long seconds = 0 )
	{
		if ( !IsAdmin( Rpc.Caller ) || target is null || target.IsHost || target == Rpc.Caller ) return;

		TimeSpan? duration = seconds > 0 ? TimeSpan.FromSeconds( Math.Min( seconds, 3650L * 24 * 3600 ) ) : null;
		Current?.Ban( target.SteamId, target.DisplayName, Clean( reason ), Rpc.Caller, duration );
		SendBans( Rpc.Caller );
	}

	/// <summary>
	/// RPC to lift a ban. Caller must be an admin.
	/// </summary>
	[Rpc.Host]
	public static void RpcUnban( ulong steamId )
	{
		if ( !IsAdmin( Rpc.Caller ) ) return;

		if ( BanList.Remove( steamId ) )
			Current?.Notify( Rpc.Caller, $"Unbanned {steamId}" );

		SendBans( Rpc.Caller );
	}

	/// <summary>
	/// RPC asking for the list of bans, which comes back to the caller alone. Caller must be an admin.
	/// </summary>
	[Rpc.Host]
	public static void RpcRequestBans()
	{
		if ( IsAdmin( Rpc.Caller ) )
			SendBans( Rpc.Caller );
	}

	static void SendBans( Connection to )
	{
		var json = Json.Serialize( BanList.All.ToList() );

		using ( Rpc.FilterInclude( to ) )
			ReceiveBansRpc( json );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static void ReceiveBansRpc( string json ) => AdminPanel.ReceiveBans( json );

	/// <summary>A reason as one short line of text.</summary>
	static string Clean( string text )
	{
		text = (text ?? "").Replace( '\r', ' ' ).Replace( '\n', ' ' ).Trim();
		return text.Length > 200 ? text[..200] : text;
	}

	/// <summary>
	/// RPC to kick a player. Caller must be host or have admin permission.
	/// </summary>
	[Rpc.Host]
	public static void RpcKickPlayer( Connection target, string reason = "Kicked" )
	{
		if ( !IsAdmin( Rpc.Caller ) || target is null || target.IsHost || target == Rpc.Caller ) return;

		Current?.Kick( target, string.IsNullOrWhiteSpace( reason ) ? "Kicked" : Clean( reason ), Rpc.Caller );
	}

	/// <summary>
	/// Sets a boolean convar and tells all players in chat. Only callable by the host.
	/// </summary>
	public static void SetConVar( string name, bool value )
	{
		if ( !Networking.IsHost ) return;

		ConsoleSystem.Run( name, value ? "true" : "false" );

		Current?.Notify( $"{name} is now {(value ? "on" : "off")}" );
	}
}
