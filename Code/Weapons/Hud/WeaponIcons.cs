namespace Sandbox;

/// <summary>
/// The picture of a weapon, for the weapon switcher and the pickup feed. The prefabs embed their <c>DisplayIcon</c>,
/// which draws as the missing texture in an <c>Image</c> panel, so the picture is loaded from its file by the weapon's
/// prefab name instead.
/// </summary>
public static class WeaponIcons
{
	static Dictionary<string, string> files = new()
	{
		["camera"] = "camera_icon",
		["colt1911"] = "heritage__45_icon",
		["crowbar"] = "crowbar_icon",
		["glock"] = "up_45_icon",
		["grenade"] = "grenade_icon",
		["m4a1"] = "ar_4_icon",
		["mp5"] = "sg_5_icon",
		["physgun"] = "physgun_icon",
		["revolver"] = "ironwood__357_icon",
		["rpg"] = "ashford_r_7_icon",
		["shotgun"] = "spaghelli_m4_icon",
		["sniper"] = "m700_icon",
	};

	static Dictionary<string, Texture> cache = new();

	/// <summary>
	/// The icon of a weapon in the world or in a hand: its prefab's picture, else the weapon's own <c>DisplayIcon</c>.
	/// </summary>
	public static Texture Of( BaseSandboxWeapon weapon ) =>
		weapon.IsValid() ? Of( weapon.GameObject.PrefabInstanceSource, weapon.DisplayIcon ) : null;

	/// <summary>
	/// The icon of the weapon made from <paramref name="prefabPath"/>, or <paramref name="fallback"/> when it has none.
	/// </summary>
	public static Texture Of( string prefabPath, Texture fallback = null )
	{
		var name = System.IO.Path.GetFileNameWithoutExtension( prefabPath ?? "" ).ToLowerInvariant();
		if ( !files.TryGetValue( name, out var file ) )
			return fallback;

		if ( !cache.TryGetValue( file, out var texture ) )
			cache[file] = texture = Texture.Load( $"ui/weapons/{file}.png" );

		return texture ?? fallback;
	}
}
