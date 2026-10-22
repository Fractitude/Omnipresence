namespace Sandbox;

public struct PlayerDiedParams
{
	public GameObject Attacker { get; set; }
}

public struct PlayerDamageParams
{
	public float Damage { get; set; }
	public GameObject Attacker { get; set; }
	public GameObject Weapon { get; set; }
	public TagSet Tags { get; set; }
	public Vector3 Position { get; set; }
	public Vector3 Origin { get; set; }
}

/// <summary>
/// Pre-damage event. Fired before damage is applied. Listeners can modify <see cref="Damage"/>
/// or set <see cref="Cancelled"/> to block damage entirely.
/// </summary>
public class PlayerDamageEvent
{
	public PlayerHealth Player { get; init; }
	public DamageInfo DamageInfo { get; init; }
	public float Damage { get; set; }
	public bool Cancelled { get; set; }
}

/// <summary>
/// Pre-respawn event. Fired on the host before the player is spawned. Listeners can modify
/// <see cref="SpawnLocation"/> to control where the player appears, or set <see cref="Cancelled"/>
/// to stop the spawn entirely. A player cancelled after death stays in the observer state; spawn them
/// later with <see cref="GameManager.Respawn"/>, which skips this event.
/// </summary>
public class PlayerRespawnEvent
{
	public PlayerData PlayerData { get; init; }
	public Transform SpawnLocation { get; set; }
	public bool Cancelled { get; set; }
}

/// <summary>
/// Fired when a player kills another player.
/// </summary>
public class PlayerKillEvent
{
	public PlayerHealth Player { get; init; }
	public GameObject Victim { get; init; }
	public DamageInfo DamageInfo { get; init; }
}

/// <summary>
/// Events fired only to the player's own GameObject hierarchy.
/// </summary>
public static partial class Local
{
	public interface IPlayerEvents : ISceneEvent<IPlayerEvents>
	{
		void OnSpawned() { }
		void OnDied( PlayerDiedParams args ) { }
		void OnDamage( PlayerDamageParams args ) { }
		void OnSuicide() { }
		void OnDamaging( PlayerDamageEvent e ) { }
		void OnKill( PlayerKillEvent e ) { }
	}
}

/// <summary>
/// Events broadcast to the entire scene for any player action.
/// </summary>
public static partial class Global
{
	public interface IPlayerEvents : ISceneEvent<IPlayerEvents>
	{
		void OnPlayerSpawned( PlayerHealth player ) { }
		void OnPlayerDied( PlayerHealth player, PlayerDiedParams args ) { }
		void OnPlayerDamage( PlayerHealth player, PlayerDamageParams args ) { }
		void OnPlayerSuicide( PlayerHealth player ) { }
		void OnPlayerDamaging( PlayerDamageEvent e ) { }
		void OnPlayerRespawning( PlayerRespawnEvent e ) { }
		void OnPlayerKill( PlayerKillEvent e ) { }
	}
}
