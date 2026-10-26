namespace Sandbox;

/// <summary>
/// One change of hands: who had it, who has it now, who did it and why. Kept so a title can be traced back, which is
/// what makes it worth calling legal: a sale, a repossession and a gift look different here.
/// </summary>
public sealed class OwnershipChange
{
	/// <summary>The owner before, as <see cref="Ownership.Key"/>. Empty for nobody.</summary>
	public string From { get; set; } = "";

	/// <summary>The owner after, as <see cref="Ownership.Key"/>. Empty for nobody.</summary>
	public string To { get; set; } = "";

	/// <summary>The Steam id of the player who made the change, or 0 for the game itself.</summary>
	public ulong By { get; set; }

	/// <summary>Real unix seconds.</summary>
	public long At { get; set; }

	/// <summary>Why, in words: "bought at the Depot", "sold to Mule", "repossessed".</summary>
	public string Reason { get; set; } = "";
}

/// <summary>
/// The title of one thing, as <c>ownership.json</c> keeps it. The id is the thing's key
/// (<see cref="OwnershipRegistry.KeyOf"/> for a thing in a scene, or any key of the caller's own for a thing that is
/// not a GameObject, such as a business).
/// </summary>
public sealed class OwnershipRecord
{
	/// <summary>
	/// The key of the thing. It is what every other system saves about it under, and it does not change.
	/// </summary>
	public string Id { get; set; } = "";

	/// <summary>The owner, as <see cref="Ownership.Key"/>. Empty for nobody.</summary>
	public string Owner { get; set; } = "";

	/// <summary>Real unix seconds when the current owner got it.</summary>
	public long Since { get; set; }

	/// <summary>The last changes of hands, oldest first.</summary>
	public List<OwnershipChange> History { get; set; } = new();
}

/// <summary>The file (<c>ownership.json</c>).</summary>
public sealed class OwnershipFile
{
	public const int CurrentVersion = 1;

	public int Version { get; set; } = CurrentVersion;

	public List<OwnershipRecord> Records { get; set; } = new();
}
