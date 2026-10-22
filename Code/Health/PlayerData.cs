namespace Sandbox;

/// <summary>
/// Holds persistent player information like deaths and kills. One per connection, it outlives the player's body.
/// </summary>
public sealed class PlayerData : Component
{
	[Sync( SyncFlags.FromHost )] public int Kills { get; internal set; }
	[Sync( SyncFlags.FromHost )] public int Deaths { get; internal set; }
	[Sync( SyncFlags.FromHost )] public bool IsGodMode { get; internal set; }

	/// <summary>
	/// Is this player data me?
	/// </summary>
	public bool IsMe => Network.Owner == Connection.Local;

	/// <summary>
	/// Data for all players
	/// </summary>
	public static IEnumerable<PlayerData> All => Game.ActiveScene.GetAll<PlayerData>();

	/// <summary>
	/// Get player data for a player
	/// </summary>
	public static PlayerData For( Connection connection ) =>
		connection == null ? default : All.FirstOrDefault( x => x.Network.Owner == connection );
}
