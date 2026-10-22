namespace Sandbox;

/// <summary>
/// Implement on any component that can appear as an attacker, so a kill can be credited to it.
/// Examples: the player, a turret, an explosive barrel.
/// </summary>
public interface IKillSource
{
	/// <summary>
	/// Display name
	/// </summary>
	string DisplayName { get; }

	/// <summary>
	/// Steam ID of the player, or 0 when it isn't one.
	/// </summary>
	long SteamId => default;

	/// <summary>
	/// Entity-type tag, empty for plain player kills.
	/// </summary>
	string Tags => "";

	/// <summary>
	/// Called on the host when this source kills something. Credit kills, update stats, etc.
	/// </summary>
	void OnKill( GameObject victim ) { }
}
