namespace Sandbox;

public enum GunItemKind
{
	Weapon,
	Ammo,
}

/// <summary>
/// One thing the gun store sells. <see cref="Preview"/> is what the 3D picture draws: a prefab path (<c>.prefab</c>) or a
/// model path (<c>.vmdl</c>). A weapon is the short name of <c>weapons/&lt;name&gt;/&lt;name&gt;.prefab</c>
/// (<see cref="WeaponInventory.PrefabPath"/>); ammo is a pack of <see cref="Amount"/> rounds of the ammo resource at
/// <see cref="AmmoPath"/>.
/// </summary>
public sealed record GunItem(
	string Id,
	GunItemKind Kind,
	string Name,
	string Description,
	long Price,
	string Preview,
	string Stats = "",
	string AmmoPath = "",
	int Amount = 0 );

/// <summary>
/// What the gun store sells. Data only: a new weapon or ammo pack is one more line. Prices and descriptions are first
/// guesses. The physgun and the camera are tools and are not sold.
/// </summary>
public static class GunCatalog
{
	const string Box9mm = "models/weapons/sbox_ammo/9mm_ammobox/ammobox_9mm.vmdl";

	// A table nobody changes at run time, so readonly is fine (a hot reload only picks up edits).
	static readonly GunItem[] weapons =
	[
		Weapon( "crowbar", "Crowbar", "A heavy steel bar. Quiet, and it never runs dry.", 60, "Melee" ),
		Weapon( "glock", "UP-45", "A light service pistol.", 450, "Pistol · 17 rounds · 9mm" ),
		Weapon( "colt1911", "1911", "An old, heavy-hitting pistol.", 600, "Pistol · 7 rounds · 9mm" ),
		Weapon( "revolver", "Ironwood .357", "A six-shot revolver that hits hard.", 900, "Revolver · 6 rounds · 9mm" ),
		Weapon( "mp5", "SMG", "A fast submachine gun with an underbarrel grenade launcher.", 1_400, "SMG · 30 rounds · 9mm" ),
		Weapon( "shotgun", "Spaghelli M4", "A semi-automatic combat shotgun.", 1_600, "Shotgun · 6 shells" ),
		Weapon( "m4a1", "M4A1", "An assault rifle for any range.", 2_800, "Rifle · 30 rounds · Rifle ammo" ),
		Weapon( "sniper", "M700", "A bolt-action sniper rifle with a scope.", 3_500, "Sniper · 5 rounds" ),
		Weapon( "grenade", "Hand Grenade", "One thrown explosive. Buy more under Ammunition.", 150, "Explosive" ),
		Weapon( "rpg", "Rocket Launcher", "A recoilless rifle. Not for the faint of heart.", 6_000, "Launcher · 1 rocket" ),
	];

	static readonly GunItem[] ammo =
	[
		Ammo( "9mm", "9mm Rounds", "For the pistols, the revolver and the SMG.", 40, "ammo/9mm.ammo", 30, Box9mm ),
		Ammo( "rifle", "Rifle Ammo", "For the M4A1.", 90, "ammo/rifle.ammo", 60, Box9mm ),
		Ammo( "shotgun", "Shotgun Shells", "For the Spaghelli M4.", 50, "ammo/shotgun.ammo", 18,
			"models/weapons/sbox_ammo/12g_ammobox/ammobox_12g.vmdl" ),
		Ammo( "sniper", "Sniper Rounds", "For the M700.", 80, "ammo/sniper.ammo", 10, Box9mm ),
		Ammo( "rocket", "Rockets", "For the rocket launcher.", 400, "ammo/rocket.ammo", 2,
			"models/weapons/sbox_ammo/84mm_ammobox/missile_carrier.vmdl" ),
		Ammo( "grenade", "Grenades", "For the hand grenade and the SMG's launcher.", 600, "ammo/grenade.ammo", 5,
			"weapons/grenade/grenade_wm.prefab" ),
	];

	public static IReadOnlyList<GunItem> Of( GunItemKind kind ) => kind == GunItemKind.Weapon ? weapons : ammo;

	public static GunItem Find( GunItemKind kind, string id ) => Of( kind ).FirstOrDefault( x => x.Id == id );

	static GunItem Weapon( string id, string name, string description, long price, string stats ) =>
		new( id, GunItemKind.Weapon, name, description, price, $"weapons/{id}/{id}_wm.prefab", stats );

	static GunItem Ammo( string id, string name, string description, long price, string path, int amount, string preview ) =>
		new( id, GunItemKind.Ammo, name, description, price, preview, $"{amount} rounds", path, amount );
}
