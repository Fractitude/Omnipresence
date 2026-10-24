namespace Sandbox;

/// <summary>
/// What a drag with a modifier key does to a stack. Flags, so the two keys together are <c>Combine | Split</c>. This is
/// the value the player's client sends to the host with two slot indices; the host checks it
/// (<see cref="StackRules.IsMode"/>).
/// </summary>
[System.Flags]
public enum StackMode
{
	/// <summary>No modifier: a plain move (a swap onto an occupied slot).</summary>
	None = 0,

	/// <summary>Shift: add the dragged stack to the stack it is dropped on.</summary>
	Combine = 1,

	/// <summary>Ctrl: take half of the dragged stack, and move only that half.</summary>
	Split = 2,
}

/// <summary>
/// The arithmetic of combining and splitting stacks, in one place, on plain <see cref="ItemData"/>:
/// no inventory, no box.
/// The bag (<see cref="PlayerInventory.Stack"/>) and the storage boxes (<see cref="StorageRules.Stack"/>) both plan
/// a move here, check it fully, and only then write the result, so the total is kept exactly and nothing is doubled.
/// Only the kinds whose <see cref="ItemDef.MaxStack"/> is above one stack (<see cref="ItemCatalog.IsCombinable"/>), and
/// a stack never grows past its kind's maximum.
/// <list type="bullet">
/// <item>Combine: both stacks are the same kind. The dragged one is added to the one under it, up to
/// kind's maximum stack; what does not fit stays in the dragged slot. The quality is the average of the
/// two weighted by how many each one adds, rounded to the nearest (halves up).</item>
/// <item>Split: the dragged stack gives <c>Count / 2</c> (rounded down) to an empty slot, with the same kind and
/// quality, and keeps the rest. A stack of one cannot be split.</item>
/// <item>Both: half of the dragged stack is added to the stack under it (the half has the dragged quality), or becomes
/// a new stack if the slot is empty.</item>
/// </list>
/// </summary>
public static class StackRules
{
	/// <summary>Whether a value is one a client may send: Combine, Split, or both.</summary>
	public static bool IsMode( int mode ) => mode is >= 1 and <= 3;

	/// <summary>Whether an item is a batch of a kind that stacks, so it can be combined and split.</summary>
	public static bool IsStackable( ItemData item ) =>
		item is { IsBatch: true } && ItemCatalog.IsCombinable( item.Kind );

	/// <summary>
	/// Whether two stacks may be merged by the game on its own (a pickup, or any code that puts a stack into the bag
	/// without the player choosing): the same kind, both stackable, the same order and addressee, and, for a kind whose
	/// <see cref="ItemDef.QualityMatters"/>, the same quality. The result of a merge never averages a quality. The
	/// player's own Shift-combine (<see cref="Plan"/>) does average, because they chose it.
	/// </summary>
	public static bool CanMerge( ItemData a, ItemData b ) =>
		IsStackable( a ) && IsStackable( b ) && Compatible( a, b, ItemCatalog.QualityMattersOf( a.Kind ) );

	/// <summary>
	/// The part of <see cref="CanMerge(ItemData, ItemData)"/> that does not read the catalogue: the same kind, order and
	/// addressee, and the same quality when <paramref name="qualityMatters"/>.
	/// </summary>
	public static bool Compatible( ItemData a, ItemData b, bool qualityMatters ) =>
		a is not null && b is not null && a.Kind == b.Kind && a.OrderId == b.OrderId && a.To == b.To
		&& (!qualityMatters || a.Quality == b.Quality);

	/// <summary>
	/// The quality of two stacks put together: each quality weighted by its count, rounded to the nearest.
	/// </summary>
	public static int AverageQuality( int countA, int qualityA, int countB, int qualityB )
	{
		var total = (long)countA + countB;

		if ( total < 1 )
			return 0;

		var sum = (long)countA * qualityA + (long)countB * qualityB;
		return (int)System.Math.Clamp( (sum * 2 + total) / (total * 2), 0, 100 );
	}

	/// <summary>How many a split moves out of a stack: half, rounded down.</summary>
	public static int HalfOf( int count ) => count / 2;

	/// <summary>
	/// Work out what a combine or split does, without touching anything. <paramref name="from"/> is the dragged stack,
	/// <paramref name="to"/> the item under it (null for an empty slot). On success the results are new objects:
	/// <paramref name="newFrom"/> is what the dragged slot holds afterwards (null when it is emptied) and
	/// <paramref name="newTo"/> what the target slot holds. On failure <paramref name="error"/> says why and nothing
	/// else is meaningful. The count in the two results is always the count that went in.
	/// </summary>
	public static bool Plan(
		ItemData from, ItemData to, StackMode mode, out ItemData newFrom, out ItemData newTo, out string error )
	{
		newFrom = null;
		newTo = null;
		error = "Nothing happens.";

		if ( !IsMode( (int)mode ) || from is null )
			return false;

		if ( !IsStackable( from ) )
		{
			error = "That does not stack.";
			return false;
		}

		var split = mode.HasFlag( StackMode.Split );

		if ( split && from.Count < 2 )
		{
			error = "You cannot split a stack of one.";
			return false;
		}

		if ( to is null )
		{
			// Onto an empty slot: a split makes a new stack, a plain combine has nothing to combine with.
			if ( !split )
				return false;

			var half = HalfOf( from.Count );
			newTo = from.Clone();
			newTo.Count = half;
			newFrom = from.Clone();
			newFrom.Count = from.Count - half;
			error = "";
			return true;
		}

		if ( !mode.HasFlag( StackMode.Combine ) )
			return false;

		if ( to.Kind != from.Kind || !IsStackable( to ) )
		{
			error = "Those are not the same kind.";
			return false;
		}

		var offered = split ? HalfOf( from.Count ) : from.Count;
		var cap = ItemCatalog.MaxStackOf( from.Kind );
		var moved = System.Math.Min( offered, cap - to.Count );

		if ( moved < 1 )
		{
			error = $"That stack is full. A stack holds at most {cap}.";
			return false;
		}

		newTo = to.Clone();
		newTo.Count = to.Count + moved;
		newTo.Quality = AverageQuality( to.Count, to.Quality, moved, from.Quality );

		if ( moved < from.Count )
		{
			newFrom = from.Clone();
			newFrom.Count = from.Count - moved;
		}

		error = "";
		return true;
	}
}
