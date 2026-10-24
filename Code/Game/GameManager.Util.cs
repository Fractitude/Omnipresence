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
	/// Kicks a connected player with an optional reason.
	/// </summary>
	public void Kick( Connection connection, string reason = "Kicked" )
	{
		Assert.True( Networking.IsHost, "Only the host may kick players." );

		Notify( reason == "Kicked"
			? $"{connection.DisplayName} was kicked"
			: $"{connection.DisplayName} was kicked: {reason}" );

		connection.Kick( reason );
	}

	/// <summary>
	/// RPC to kick a player. Caller must be host or have admin permission.
	/// </summary>
	[Rpc.Host]
	public static void RpcKickPlayer( Connection target, string reason = "Kicked" )
	{
		if ( !Rpc.Caller.HasPermission( "admin" ) ) return;

		Current?.Kick( target, reason );
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
