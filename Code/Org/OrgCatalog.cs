namespace Sandbox;

/// <summary>An emblem a player can choose for their organization: a Material icon.</summary>
public sealed record OrgIcon( string Id, string Label, string Glyph );

/// <summary>
/// The numbers and the preset lists of organizations: the sixteen emblems, the colours, the limits and the fee.
/// </summary>
public static class OrgCatalog
{
	/// <summary>The most characters in a name, and the fewest.</summary>
	public const int MinNameLength = 3, MaxNameLength = 24;

	/// <summary>The most members an organization has.</summary>
	public const int MaxMembers = 16;

	/// <summary>The most invitations one organization has waiting at once.</summary>
	public const int MaxPendingInvites = 5;

	/// <summary>
	/// After a player rejects an organization's invitation it can't invite them again for this long, doubling with each
	/// further rejection up to the cap. Real seconds.
	/// </summary>
	public const long RejectBaseSeconds = 300, RejectMaxSeconds = 1800;

	/// <summary>A rejection streak is forgotten once this long has passed since the last one (24 hours).</summary>
	public const long RejectResetSeconds = 86400;

	/// <summary>The newest payouts an organization keeps for the Payouts tab.</summary>
	public const int MaxPayouts = 50;

	/// <summary>Dollars it costs to register an organization. Zero for now; tune it here.</summary>
	public const long RegistrationFee = 0;

	/// <summary>The colour a new organization starts with.</summary>
	public const string DefaultColor = "#b48cff";

	/// <summary>
	/// The sixteen emblems. Every glyph was looked up in the ligature table of the engine's icon font
	/// (<c>core/fonts/MaterialIcons-Regular.ttf</c>); that font has no "skull", so the set has none.
	/// </summary>
	public static IReadOnlyList<OrgIcon> Icons { get; } =
	[
		new( "shield", "Shield", "shield" ),
		new( "bolt", "Bolt", "bolt" ),
		new( "fire", "Flame", "local_fire_department" ),
		new( "diamond", "Diamond", "diamond" ),
		new( "crown", "Crown", "workspace_premium" ),
		new( "paw", "Paw", "pets" ),
		new( "rocket", "Rocket", "rocket_launch" ),
		new( "pentagon", "Pentagon", "pentagon" ),
		new( "hexagon", "Hexagon", "hexagon" ),
		new( "token", "Token", "token" ),
		new( "radar", "Radar", "radar" ),
		new( "key", "Key", "vpn_key" ),
		new( "anchor", "Anchor", "anchor" ),
		new( "castle", "Castle", "castle" ),
		new( "leaf", "Leaf", "eco" ),
		new( "storm", "Storm", "cyclone" ),
	];

	/// <summary>The colour palette offered besides a typed hex colour.</summary>
	public static IReadOnlyList<string> Palette { get; } =
	[
		"#b48cff", "#7c5cff", "#4f8cff", "#2ec4d6", "#3ccf91", "#9bd45a",
		"#f2d04b", "#f29b3c", "#ef5b5b", "#ff6fae", "#c9a0e6", "#aab0c0",
	];

	public static OrgIcon IconOf( string id ) => Icons.FirstOrDefault( x => x.Id == id );

	/// <summary>The words shown for a role.</summary>
	public static string RoleLabel( OrgRole role ) => role.ToString();
}
