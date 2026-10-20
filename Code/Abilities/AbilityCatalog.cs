namespace Sandbox;

/// <summary>
/// Every ability in the game. Add new ones here; the player knows all of them until abilities are unlocked in play.
/// </summary>
public static class AbilityCatalog
{
	/// <remarks>
	/// Not <c>readonly</c>: a hot reload would reset a static that holds only engine types (see AGENTS.md, "Static state
	/// and hot reload"), and it is how edits to this list get picked up.
	/// </remarks>
	static Ability[] all =
	[
		new()
		{
			Id = "pulse", Name = "Pulse", Kind = AbilityKind.Power, Icon = "bolt", Cooldown = 3f,
			Description = "A wave of signal that rolls out from you.",
			Use = _ => Log.Info( "Ability: Pulse" ),
		},
		new()
		{
			Id = "phase", Name = "Phase Step", Kind = AbilityKind.Power, Icon = "directions_run", Cooldown = 5f,
			Description = "Slip a short way through the Wired.",
			Use = _ => Log.Info( "Ability: Phase Step" ),
		},
		new()
		{
			Id = "ward", Name = "Ward", Kind = AbilityKind.Spell, Icon = "shield", Cooldown = 12f,
			Description = "A shell that turns away the next intrusion.",
			Use = _ => Log.Info( "Ability: Ward" ),
		},
		new()
		{
			Id = "echo", Name = "Echo", Kind = AbilityKind.Spell, Icon = "graphic_eq", Cooldown = 8f,
			Description = "Hear what the network last said nearby.",
			Use = _ => Log.Info( "Ability: Echo" ),
		},
		new()
		{
			Id = "shard", Name = "Data Shard", Kind = AbilityKind.Item, Icon = "diamond", Cooldown = 1f,
			Description = "A fragment of someone else's memory.",
			Use = _ => Log.Info( "Ability: Data Shard" ),
		},
	];

	public static IReadOnlyList<Ability> All => all;

	public static Ability Find( string id ) => string.IsNullOrEmpty( id ) ? null : all.FirstOrDefault( x => x.Id == id );
}
