namespace Sandbox;

/// <summary>
/// A player sat down in a seat and is about to put their weapon away. Set <see cref="Cancelled"/> to let them keep it
/// out (a seat with a gun port, say).
/// </summary>
public sealed class SeatHolsterEvent
{
	public PlayerController Player { get; init; }
	public GameObject Seat { get; init; }
	public BaseSandboxWeapon Weapon { get; init; }
	public bool Cancelled { get; set; }
}

/// <summary>
/// Posted to the player's GameObject and to the whole scene when a seated player's weapon is about to be holstered.
/// </summary>
public interface ISeatHolsterEvents : ISceneEvent<ISeatHolsterEvents>
{
	void OnSeatHolster( SeatHolsterEvent e ) { }
}
