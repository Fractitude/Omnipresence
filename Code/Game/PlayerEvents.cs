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
/// Fired from player's client when they try to pick up an object, and again on the instance that simulates it before it's moved for them. Set <see cref="Cancelled"/> to true to refuse, or change <see cref="MaxMass"/> to let them lift more or less. See <see cref="Player"/>'s carry.
/// </summary>
public class PlayerCarryEvent
{
	public Player Player { get; init; }
	public GameObject Target { get; init; }
	public Rigidbody Body { get; init; }
	public float Mass { get; init; }
	public float MaxMass { get; set; }
	public bool Cancelled { get; set; }
}

/// <summary>
/// Fired on the player's own machine when they press the flashlight key, before the flashlight toggles. Set
/// <see cref="Cancelled"/> to true to keep the key for yourself - a vehicle can use it for its headlights.
/// </summary>
public class PlayerFlashlightEvent
{
	public Player Player { get; init; }
	public bool Cancelled { get; set; }
}

/// <summary>
/// Pre-damage event. Fired before damage is applied. Listeners can modify <see cref="Damage"/>
/// or set <see cref="Cancelled"/> to block damage entirely.
/// </summary>
public class PlayerDamageEvent
{
	public Player Player { get; init; }
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
	public Player Player { get; init; }
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
		void OnFlashlight( PlayerFlashlightEvent e ) { }
		void OnCarry( PlayerCarryEvent e ) { }
	}
}

/// <summary>
/// Events broadcast to the entire scene for any player action.
/// </summary>
public static partial class Global
{
	public interface IPlayerEvents : ISceneEvent<IPlayerEvents>
	{
		void OnPlayerSpawned( Player player ) { }
		void OnPlayerDied( Player player, PlayerDiedParams args ) { }
		void OnPlayerDamage( Player player, PlayerDamageParams args ) { }
		void OnPlayerSuicide( Player player ) { }
		void OnPlayerDamaging( PlayerDamageEvent e ) { }
		void OnPlayerRespawning( PlayerRespawnEvent e ) { }
		void OnPlayerKill( PlayerKillEvent e ) { }
		void OnPlayerFlashlight( PlayerFlashlightEvent e ) { }
		void OnPlayerCarry( PlayerCarryEvent e ) { }
	}
}
