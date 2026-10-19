namespace OmnipresenceApps;

/// <summary>
/// Where a piece of hardware goes in the player's rig. <see cref="HardwareCatalog.SlotCount"/> says how many fit.
/// </summary>
public enum HardwareSlot
{
	Ram,
	Cpu,
	Card,
}

/// <summary>
/// A piece of hardware. Bought at the Hardware Store, or taken from another customer's order, and installed at the
/// workbench. Once installed it changes how terminals in the Wired play.
/// </summary>
/// <param name="Id">Saved in the player's progress, so never rename one.</param>
/// <param name="Price">Dollars. Ignored for <paramref name="Restricted"/> items, which the store doesn't sell.</param>
/// <param name="Blueprint">The blueprint, found on a dead server, that unlocks buying it. Empty when it is in stock.</param>
/// <param name="Restricted">Not for sale to anyone. It only comes from rerouting a customer's order.</param>
/// <param name="Attempts">Extra wrong guesses a terminal allows.</param>
/// <param name="Duds">Wrong words already removed when a terminal opens.</param>
public sealed record HardwareItem(
	string Id,
	string Name,
	HardwareSlot Slot,
	string Description,
	long Price,
	string Blueprint,
	bool Restricted,
	int Attempts,
	int Duds )
{
	/// <summary>What it does, as a line for the player.</summary>
	public string Effect
	{
		get
		{
			var parts = new List<string>();

			if ( Attempts > 0 )
				parts.Add( Attempts == 1 ? "+1 attempt" : $"+{Attempts} attempts" );

			if ( Duds > 0 )
				parts.Add( Duds == 1 ? "1 wrong word removed" : $"{Duds} wrong words removed" );

			return string.Join( ", ", parts ) + " on every terminal.";
		}
	}
}

/// <summary>
/// What the installed hardware adds to a terminal.
/// </summary>
public readonly record struct HardwareBonus( int Attempts, int Duds );

/// <summary>
/// An order from another customer that is waiting at the store to be sent. The staff panel can reroute it.
/// </summary>
public sealed record StoreOrder( string Id, string Customer, string ItemId );

public static class HardwareCatalog
{
	/// <summary>Seconds until a bought package arrives. Short, so it can be played with; real mail is slower.</summary>
	public const long OrderDeliverySeconds = 60;

	/// <summary>Seconds until a rerouted package arrives.</summary>
	public const long RerouteDeliverySeconds = 120;

	// Properties, not readonly fields: see IntrusionCatalog.
	public static IReadOnlyList<HardwareItem> Items =>
	[
		new( "ram-basic", "Mismatched RAM Stick", HardwareSlot.Ram,
			"A spare stick from somebody's old tower. It still works.",
			120, "", false, 1, 0 ),
		new( "cpu-basic", "Used Pentium CPU", HardwareSlot.Cpu,
			"Pulled from a dead office machine. Warm to the touch.",
			250, "", false, 0, 1 ),
		new( "signal-tuner", "Signal Tuner", HardwareSlot.Card,
			"A hand-built receiver that pulls faint broadcasts out of the static. Special order: needs its blueprint.",
			200, "signal-tuner", false, 0, 1 ),
		new( "ram-stack", "RAM Stack", HardwareSlot.Ram,
			"Four mismatched sticks wired in a row. Special order: needs its blueprint.",
			500, "ram-stack", false, 2, 0 ),

		new( "cpu-mainframe", "Mainframe CPU Module", HardwareSlot.Cpu,
			"Not on any shelf. It was made for a lab, and nobody on this street could pay for it.",
			0, "", true, 0, 3 ),
		new( "ram-ecc", "Server ECC RAM", HardwareSlot.Ram,
			"Error-correcting memory from a data centre. Not sold to the public.",
			0, "", true, 3, 0 ),
		new( "card-resonance", "Resonance Card", HardwareSlot.Card,
			"A prototype board with no label. Whoever ordered it wants it quietly.",
			0, "", true, 2, 1 ),
	];

	/// <summary>
	/// The orders of other customers that the store has not sent yet. Their items are restricted ones.
	/// </summary>
	public static IReadOnlyList<StoreOrder> Orders =>
	[
		new( "ord-4471", "Tachibana Labs", "cpu-mainframe" ),
		new( "ord-4478", "Cyberia Records", "ram-ecc" ),
		new( "ord-4490", "K. Iwakura", "card-resonance" ),
	];

	public static HardwareItem Find( string id ) => Items.FirstOrDefault( x => x.Id == id );

	public static StoreOrder FindOrder( string id ) => Orders.FirstOrDefault( x => x.Id == id );

	/// <summary>
	/// How many pieces of a kind fit in the rig.
	/// </summary>
	public static int SlotCount( HardwareSlot slot ) => slot switch
	{
		HardwareSlot.Ram => 2,
		HardwareSlot.Cpu => 1,
		HardwareSlot.Card => 1,
		_ => 0,
	};

	/// <summary>
	/// Whether the rig has room for this piece, given what is installed.
	/// </summary>
	public static bool CanInstall( IReadOnlyList<string> installed, string itemId )
	{
		if ( Find( itemId ) is not { } item )
			return false;

		return installed.Count( x => Find( x )?.Slot == item.Slot ) < SlotCount( item.Slot );
	}

	/// <summary>
	/// What the installed pieces add up to.
	/// </summary>
	public static HardwareBonus BonusOf( IEnumerable<string> installed )
	{
		var items = installed.Select( Find ).Where( x => x is not null ).ToList();
		return new HardwareBonus( items.Sum( x => x.Attempts ), items.Sum( x => x.Duds ) );
	}
}
