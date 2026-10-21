namespace Sandbox;

/// <summary>
/// One thing the player can do with an <see cref="Interactable"/>: a row in the menu they scroll with the mouse wheel.
/// </summary>
public sealed class InteractionOption
{
	/// <summary>
	/// Names the option for handlers (<see cref="InteractionEvent.Option"/>), which check it to tell options apart.
	/// </summary>
	[Property] public string Id { get; set; } = "";

	/// <summary>
	/// The row the player sees, e.g. "Open". Empty hides the row.
	/// </summary>
	[Property] public string Label { get; set; } = "Use";

	/// <summary>
	/// Material icon shown beside the label.
	/// </summary>
	[Property] public string Icon { get; set; } = "";

	/// <summary>
	/// A second line under the label while the row is picked.
	/// </summary>
	[Property] public string Description { get; set; } = "";

	/// <summary>
	/// Turn off to stop it being used. The row stays, greyed out. Set it from code to lock and unlock.
	/// </summary>
	[Property] public bool Available { get; set; } = true;

	/// <summary>
	/// Where the handlers and events run. See <see cref="InteractionSide"/>.
	/// </summary>
	[Property] public InteractionSide RunOn { get; set; } = InteractionSide.Host;

	/// <summary>
	/// Seconds before it can be used again, for the player who used it (<see cref="InteractionSide.Local"/>) or for everyone
	/// (<see cref="InteractionSide.Host"/>).
	/// </summary>
	[Property, Range( 0f, 10f )] public float Cooldown { get; set; }

	/// <summary>
	/// Runs when the player uses it, with the player's GameObject (null if it can't be told). Connect any component's method
	/// to it in the editor; one that doesn't need the player leaves the argument out.
	/// </summary>
	[Property] public System.Action<GameObject> OnInteract { get; set; }

	internal float NextUse;
}
