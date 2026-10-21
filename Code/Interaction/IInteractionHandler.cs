namespace Sandbox;

/// <summary>
/// What an <see cref="Interactable"/> shows the player: its label, icon and whether it can be used right now.
/// </summary>
public readonly record struct InteractionInfo( string Label, string Icon = "", string Description = "", bool Enabled = true );

/// <summary>
/// One use of an <see cref="Interactable"/>.
/// </summary>
/// <param name="Interactable">The thing being used.</param>
/// <param name="Interactor">The player's GameObject, or null if it can't be told.</param>
/// <param name="Option">Which of the Interactable's options is meant.</param>
/// <param name="Index">Its place in <see cref="Interactable.Options"/>.</param>
public readonly record struct InteractionEvent( Interactable Interactable, GameObject Interactor, InteractionOption Option, int Index );

/// <summary>
/// Where an <see cref="Interactable"/> runs its handlers and events when it is used.
/// </summary>
public enum InteractionSide
{
	/// <summary>
	/// On the client of the player who pressed Use. For things only that player sees or feels.
	/// </summary>
	Local,

	/// <summary>
	/// On the host, which checks the caller first. For anything that changes the shared game. The Interactable's GameObject
	/// must be a network object, or the press never reaches the host.
	/// </summary>
	Host,
}

/// <summary>
/// A component that answers to the <see cref="Interactable"/> on its GameObject or a parent of it. Implement it to be told
/// when the player uses the object, and to describe the object to the player as the game changes.
/// </summary>
public interface IInteractionHandler
{
	/// <summary>
	/// The player used one of the object's options (<see cref="InteractionEvent.Option"/>). Runs where its
	/// <see cref="InteractionOption.RunOn"/> says.
	/// </summary>
	void OnInteract( InteractionEvent e );

	/// <summary>
	/// What the player sees for one option, in place of the option's own label. Null leaves it as is.
	/// Runs on the looking player's client every frame, so keep it cheap.
	/// </summary>
	InteractionInfo? Describe( InteractionEvent e ) => null;

	/// <summary>
	/// May the player use this option now? False greys it out and stops the press. Checked on the host again for
	/// <see cref="InteractionSide.Host"/>, so don't trust client-only state.
	/// </summary>
	bool CanInteract( InteractionEvent e ) => true;
}
