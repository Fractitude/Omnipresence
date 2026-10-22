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
	/// Is this player in the Wired rather than the real world? Set by <see cref="WiredTravel"/>; a new body starts in
	/// the real world.
	/// </summary>
	[Sync( SyncFlags.FromHost )] public bool InWired { get; internal set; }

	/// <summary>
	/// Where the player was when they connected to the Wired, which is where they come back to. Host only.
	/// </summary>
	public Transform? HomeTransform { get; internal set; }

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
