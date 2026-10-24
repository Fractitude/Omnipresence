namespace Sandbox;

/// <summary>
/// One action of the right-click menu of an item slot, before a window gives it something to run. The verbs are the
/// same in every window, so what a slot offers is decided here in one place and tested without a window.
/// </summary>
public readonly record struct ItemVerb( string Id, string Label, string Icon, bool Enabled = true );

/// <summary>
/// What the right-click menu of an item slot lists: the bag (the Character menu, or the bag half of a container window),
/// a storage box and a vehicle's cargo. A verb that does not apply is not listed. Pure: no window, no connection. The
/// host still checks every action it is asked for (slot indices only).
/// </summary>
public static class ItemMenus
{
	/// <summary>Drop the stack on the ground in front of the player: what dragging it out of the window does.</summary>
	public const string Drop = "drop";

	/// <summary>Move the stack from the bag to the open container.</summary>
	public const string ToContainer = "to_container";

	/// <summary>Move the stack from the container to the bag.</summary>
	public const string ToBag = "to_bag";

	/// <summary>Put the stack on the ground behind the vehicle.</summary>
	public const string Unload = "unload";

	/// <summary>
	/// The verbs for a slot of the bag. <paramref name="containerWord"/> names the container window that is open
	/// ("storage", "cargo"), or is null when only the bag is shown.
	/// </summary>
	public static List<ItemVerb> ForBag( ItemData item, string containerWord )
	{
		var verbs = new List<ItemVerb>();

		if ( item is null )
			return verbs;

		if ( containerWord is not null )
			verbs.Add( new ItemVerb( ToContainer, $"Move to {containerWord}", "move_to_inbox" ) );

		if ( CanDrop( item ) )
			verbs.Add( new ItemVerb( Drop, "Drop", "arrow_downward" ) );

		return verbs;
	}

	/// <summary>
	/// The verbs for a slot of a container: always move to the bag, and for a vehicle's cargo also unload.
	/// </summary>
	public static List<ItemVerb> ForContainer( ItemData item, bool isCargo )
	{
		var verbs = new List<ItemVerb>();

		if ( item is null )
			return verbs;

		verbs.Add( new ItemVerb( ToBag, "Move to bag", "outbox" ) );

		if ( isCargo )
			verbs.Add( new ItemVerb( Unload, "Unload cargo", "local_shipping" ) );

		return verbs;
	}

	/// <summary>
	/// Whether the kind may be dropped (<see cref="ItemDef.CanDrop"/>); an ordinary item may.
	/// </summary>
	public static bool CanDrop( ItemData item ) =>
		item is not null && (!item.IsBatch || ItemCatalog.Find( item.Kind ) is not { CanDrop: false });

	/// <summary>
	/// Give the verbs something to run, for the window that shows them.
	/// </summary>
	public static List<ContextEntry> Bind( IEnumerable<ItemVerb> verbs, System.Func<string, System.Action> run ) =>
		verbs.Select( x => new ContextEntry( x.Id, x.Label, x.Icon, x.Enabled, run( x.Id ) ) ).ToList();
}

/// <summary>
/// A row of the <see cref="ContextMenu"/>: what it says and what it does.
/// </summary>
public sealed record ContextEntry( string Verb, string Label, string Icon, bool Enabled, System.Action Run );
