namespace Sandbox;

public enum AbilityKind
{
	Power,
	Spell,
	Item,
}

/// <summary>
/// Something the player can put on the hotbar and do: a power, a spell or an item. <see cref="Use"/> runs on the
/// player's own client, with the player's object.
/// </summary>
public sealed class Ability
{
	public string Id { get; init; }

	public string Name { get; init; }

	public string Description { get; init; }

	public AbilityKind Kind { get; init; }

	/// <summary>
	/// A Material icon name, like <c>bolt</c>.
	/// </summary>
	public string Icon { get; init; } = "star";

	/// <summary>
	/// Seconds before it can be used again.
	/// </summary>
	public float Cooldown { get; init; }

	/// <summary>
	/// What it does. Gets the player's object, which can be null before the player has spawned.
	/// </summary>
	public System.Action<GameObject> Use { get; init; }
}
