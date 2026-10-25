namespace Sandbox;

/// <summary>
/// The one place that finds a player's components: the local player's, or the player of a connection. Every part of the
/// player (<see cref="PlayerController"/>, <see cref="Player"/>, <see cref="PlayerProgress"/>,
/// <see cref="PlayerInventory"/>, …) is on the player prefab, so any of them can be asked for.
/// </summary>
public static class Players
{
	/// <summary>
	/// The local player's controller, or null before they have spawned or while they are dead.
	/// </summary>
	public static PlayerController LocalController => Local<PlayerController>();

	/// <summary>
	/// The local player's GameObject, or null.
	/// </summary>
	public static GameObject LocalObject => LocalController?.GameObject;

	/// <summary>
	/// A component of the player this computer controls, or null.
	/// </summary>
	public static T Local<T>() where T : Component =>
		Game.ActiveScene?.GetAll<T>().FirstOrDefault( x => x.IsValid() && !x.IsProxy );

	/// <summary>
	/// A component of the player that <paramref name="connection"/> owns, or null.
	/// </summary>
	public static T For<T>( Connection connection ) where T : Component =>
		connection is null
			? null
			: Game.ActiveScene?.GetAll<T>().FirstOrDefault( x => x.IsValid() && OwnerOf( x ) == connection );

	/// <summary>
	/// The connection that controls a player's component. Without a network it is the local connection; with one it is
	/// null until the host has handed the object to its owner.
	/// </summary>
	public static Connection OwnerOf( Component component ) =>
		component?.Network.Owner ?? (Networking.IsActive ? null : Connection.Local);
}
