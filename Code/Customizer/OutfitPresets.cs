using Sandbox.UI;
namespace Sandbox;

/// <summary>
/// A saved outfit: the model, the clothing, the colors, and the workshop packages it needs.
/// </summary>
public sealed class OutfitPreset
{
	public string Name { get; set; }

	/// <summary>
	/// The outfit as <see cref="ClothingContainer.Serialize"/> writes it.
	/// </summary>
	public string Outfit { get; set; }

	public string Colors { get; set; }

	public string Workshop { get; set; }
}

/// <summary>
/// Presets and the last applied outfit, kept in the cookies so they outlive the addon and the session.
/// </summary>
public static class OutfitPresets
{
	const string PresetsCookie = "player_customizer.presets";
	const string LastOutfitCookie = "player_customizer.last_outfit";

	public static List<OutfitPreset> Load() => Game.Cookies.Get<List<OutfitPreset>>( PresetsCookie, null ) ?? [];

	public static void Save( List<OutfitPreset> presets ) => Game.Cookies.Set( PresetsCookie, presets );

	/// <summary>
	/// The outfit applied last, put back on when the addon spawns.
	/// </summary>
	public static OutfitPreset LastOutfit
	{
		get => Game.Cookies.Get<OutfitPreset>( LastOutfitCookie, null );
		set
		{
			if ( value is null )
				Game.Cookies.Remove( LastOutfitCookie );
			else
				Game.Cookies.Set( LastOutfitCookie, value );
		}
	}

	public static OutfitPreset Capture( string name, ClothingContainer outfit, IReadOnlyDictionary<string, Color> colors )
	{
		return new OutfitPreset
		{
			Name = name,
			Outfit = outfit.Serialize(),
			Colors = OutfitColors.Serialize( colors ),
			Workshop = WorkshopClothing.GetIdents( outfit )
		};
	}

	/// <summary>
	/// Read a preset back. Its workshop packages download first, the outfit only knows them by path.
	/// </summary>
	public static async Task<(ClothingContainer Outfit, Dictionary<string, Color> Colors)> Resolve( OutfitPreset preset )
	{
		await WorkshopClothing.Mount( preset.Workshop );
		return (ClothingContainer.CreateFromJson( preset.Outfit ), OutfitColors.Deserialize( preset.Colors ));
	}
}
