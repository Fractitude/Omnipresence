using Sandbox.UI;
namespace Sandbox;

/// <summary>
/// Tint colors of an outfit: one for the body and one per clothing item, keyed by its resource path.
/// Items without a color keep whatever the Dresser gave them.
/// </summary>
public static class OutfitColors
{
	public const string BodyKey = "body";

	/// <summary>
	/// Every underwear piece, including the pants and bra the Dresser puts on by itself.
	/// </summary>
	public const string UnderwearKey = "underwear";

	/// <summary>
	/// The underwear the Dresser adds when the outfit has none, it isn't in the outfit so it has no row of its own.
	/// </summary>
	public const string DefaultUnderwearPath = "models/citizen_clothes/underwear/y_front_pants/y_front_pants_white.clothing";

	public static string GetKey( Clothing clothing ) => clothing?.ResourcePath;

	/// <summary>
	/// Tint only takes the color, a see-through body or shirt isn't something we want.
	/// </summary>
	public static Color Opaque( Color color ) => color.WithAlpha( 1 );

	// tinted copies of skin materials, so retinting doesn't make a new material every time
	static readonly Dictionary<(Material Material, Color Color), Material> _tintedSkins = [];

	public static Dictionary<string, Color> Copy( IReadOnlyDictionary<string, Color> colors ) => colors is null ? [] : new( colors );

	public static string Serialize( IReadOnlyDictionary<string, Color> colors ) => colors is null || colors.Count == 0 ? null : Json.Serialize( colors );

	public static Dictionary<string, Color> Deserialize( string json )
	{
		if ( string.IsNullOrWhiteSpace( json ) ) return [];
		return Json.Deserialize<Dictionary<string, Color>>( json ) ?? [];
	}

	/// <summary>
	/// Tint a dressed body. Call it after the Dresser is done, it makes new clothing renderers every time.
	/// </summary>
	public static void Apply( SkinnedModelRenderer body, ClothingContainer outfit, IReadOnlyDictionary<string, Color> colors )
	{
		if ( !body.IsValid() ) return;

		// the body renderer outlives outfits, so it always gets set
		var bodyColor = Opaque( colors?.GetValueOrDefault( BodyKey, Color.White ) ?? Color.White );
		body.Tint = bodyColor;

		if ( colors is null || colors.Count == 0 ) return;

		// the Dresser names them after the resource, the old outfit's objects linger until the end of the frame
		var renderers = body.GameObject.Children
			.Where( x => !x.IsDestroyed && x.Tags.Has( "clothing" ) )
			.Select( x => (Name: x.Name["Clothing - ".Length..], Renderer: x.GetComponent<SkinnedModelRenderer>()) )
			.Where( x => x.Renderer.IsValid() )
			.ToList();

		// underwear first, an underwear item with a color of its own wins
		if ( colors.TryGetValue( UnderwearKey, out var underwearColor ) )
		{
			var underwear = GetUnderwearNames();
			foreach ( var (name, renderer) in renderers.Where( x => underwear.Contains( x.Name ) ) )
				renderer.Tint = Opaque( underwearColor );
		}

		if ( outfit is not null )
		{
			foreach ( var entry in outfit.Clothing )
			{
				var key = GetKey( entry.Clothing );
				if ( key is null || !colors.TryGetValue( key, out var color ) ) continue;

				foreach ( var (name, renderer) in renderers.Where( x => x.Name == entry.Clothing.ResourceName ) )
					renderer.Tint = Opaque( color );
			}
		}

		TintSkinOnClothing( renderers.Select( x => x.Renderer ), outfit, bodyColor, colors.ContainsKey( BodyKey ) );
	}

	static HashSet<string> GetUnderwearNames()
	{
		return ResourceLibrary.GetAll<Clothing>()
			.Where( x => x.GetGroup() == ClothingGroup.Underwear )
			.Select( x => x.ResourceName )
			.ToHashSet();
	}

	/// <summary>
	/// Clothing like the default underwear carries a piece of the body (the legs) as a material marked "skin".
	/// The Dresser swaps it for the skin's own material through the "skin" target, this does the same with a tinted copy,
	/// so the body color reaches the legs without tinting the cloth.
	/// </summary>
	static void TintSkinOnClothing( IEnumerable<SkinnedModelRenderer> renderers, ClothingContainer outfit, Color color, bool bodyColored )
	{
		var skin = outfit?.Clothing.Select( x => x.Clothing ).FirstOrDefault( x => x?.HasHumanSkin == true );
		var skinMaterial = string.IsNullOrWhiteSpace( skin?.HumanSkinMaterial ) ? null : Material.Load( skin.HumanSkinMaterial );

		foreach ( var renderer in renderers )
		{
			if ( !renderer.Model.IsValid() ) continue;

			// a tinted piece of clothing would tint its bit of body too, so that bit takes the body color instead
			if ( !bodyColored && renderer.Tint == Color.White ) continue;

			// without a skin item the Dresser leaves the model's own skin material alone
			var source = skinMaterial ?? renderer.Model.Materials.FirstOrDefault( x => x.Attributes.GetInt( "skin" ) == 1 );
			if ( source is null || !renderer.Model.Materials.Any( x => x.Attributes.GetInt( "skin" ) == 1 ) ) continue;

			renderer.SetMaterialOverride( GetTintedSkin( source, color ), "skin" );
		}
	}

	static Material GetTintedSkin( Material source, Color color )
	{
		if ( _tintedSkins.TryGetValue( (source, color), out var tinted ) && tinted.IsValid() )
			return tinted;

		// dragging the color picker goes through lots of colors, old ones aren't needed
		if ( _tintedSkins.Count > 64 )
			_tintedSkins.Clear();

		tinted = source.CreateCopy();
		tinted.Set( "g_vColorTint", color );

		// ignore the renderer's tint, that one is for the cloth
		tinted.Set( "g_flModelTintAmount", 0f );
		_tintedSkins[(source, color)] = tinted;
		return tinted;
	}
}

/// <summary>
/// One color of an outfit as a property, so the sandbox color control can edit it.
/// </summary>
public sealed class ColorSlot
{
	readonly Func<Color> _get;
	readonly Action<Color> _set;

	readonly bool _opaque;

	public ColorSlot( Func<Color> get, Action<Color> set, bool opaque = true )
	{
		_get = get;
		_set = set;
		_opaque = opaque;
	}

	[Property]
	public Color Color
	{
		get => _get();
		set => _set( _opaque ? OutfitColors.Opaque( value ) : value );
	}

	public SerializedProperty GetProperty()
	{
		return Game.TypeLibrary.GetSerializedObject( this ).GetProperty( nameof( Color ) );
	}
}
