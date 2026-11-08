namespace Sandbox;

/// <summary>
/// A car's colours: the body, the trim and the glass, as the dealership picks them and the prefab gives them by default.
/// <see cref="Glass"/> is a tint with an opacity: the colour is the tint and the alpha is how much of the glass is the
/// tint, so clear is a pale colour at a very low alpha and a blacked-out window is black at alpha 1.
/// </summary>
public readonly record struct CarLook( Color Paint, Color Secondary, Color Glass )
{
	/// <summary>A colour as the app sends it: <c>r,g,b,a</c>, each 0 to 1.</summary>
	public static string Format( Color color ) => string.Create( System.Globalization.CultureInfo.InvariantCulture,
		$"{color.r:0.####},{color.g:0.####},{color.b:0.####},{color.a:0.####}" );

	/// <summary>Read <see cref="Format"/>. Null if it isn't a colour; each channel is kept in 0 to 1.</summary>
	public static Color? Parse( string text )
	{
		var parts = (text ?? "").Split( ',' );

		if ( parts.Length != 4 )
			return null;

		var values = new float[4];

		for ( var i = 0; i < 4; i++ )
		{
			if ( !float.TryParse( parts[i], System.Globalization.NumberStyles.Float,
				    System.Globalization.CultureInfo.InvariantCulture, out values[i] ) || !float.IsFinite( values[i] ) )
				return null;

			values[i] = System.Math.Clamp( values[i], 0f, 1f );
		}

		return new Color( values[0], values[1], values[2], values[3] );
	}
}

/// <summary>
/// A car's paint. Put on a car prefab, it tints the body: the car's own <c>primary</c> materials get
/// <see cref="Paint"/> and its <c>secondary</c> ones <see cref="Secondary"/>, and its windows are tinted
/// <see cref="GlassTint"/>. The defaults are set on the prefab in the inspector, and the dealership sets them per car on
/// purchase (<see cref="CarDealer"/>) and in its preview (<see cref="CarPreviewScene"/>), which keeps this component when
/// it strips a car down to what is drawn, so the preview, the prefab and the car in the world are painted by the same code.
/// </summary>
/// <remarks>
/// The tint is set once when the car starts and again when the colours change (<see cref="SetLook"/>, or a synced change
/// from the host), never every frame. It works on copies of the materials, put on the renderers as overrides on this
/// machine only, so the models' own materials are untouched. It runs in the editor too, so the prefab shows its colours
/// and the inspector edits them live. The overrides it makes there are saved with the prefab as entries naming the stock
/// materials (the same look), so a prefab saved from the editor shows those as a diff; they are harmless.
/// </remarks>
[Title( "Car Paint" ), Category( "Vehicles" ), Icon( "format_color_fill" )]
public sealed class CarPaint : Component, Component.ExecuteInEditor
{
	/// <summary>How long a car whose model is not loaded yet is looked at again, in seconds.</summary>
	const float RetryTime = 3f;

	/// <summary>The glass material every window slot is put on a copy of: the lens shader, near transparent.</summary>
	const string GlassMaterial = "vehicles/shared/clear_glass.vmat";

	/// <summary>The body colour. The prefab's value is the car's stock colour.</summary>
	[Property, Sync( SyncFlags.FromHost ), Change( nameof(OnColorsChanged) )]
	public Color Paint { get; set; } = Color.Red;

	/// <summary>The colour of the car's secondary parts (trim, mirrors, spoiler).</summary>
	[Property, Sync( SyncFlags.FromHost ), Change( nameof(OnColorsChanged) )]
	public Color Secondary { get; set; } = new( 0.098f, 0.11f, 0.125f );

	/// <summary>
	/// The window tint: the colour is the tint and the alpha is the opacity (the default is clear). A black at alpha 1 is
	/// a blacked-out window.
	/// </summary>
	[Property, Sync( SyncFlags.FromHost ), Change( nameof(OnColorsChanged) )]
	public Color GlassTint { get; set; } = new( 0.9f, 0.9f, 0.9f, 0.05f );

	/// <summary>The colours the car has now.</summary>
	public CarLook Look => new( Paint, Secondary, GlassTint );

	readonly List<(Material Material, bool Secondary)> materials = [];
	Material glass;
	TimeSince sinceStart;
	bool found;
	CarLook applied;

	/// <summary>Whether the car has any painted material. False until its model is loaded.</summary>
	public bool HasPaintedMaterials => materials.Count > 0;

	protected override void OnStart()
	{
		sinceStart = 0;
		Collect();
	}

	protected override void OnUpdate()
	{
		// A car placed in the scene starts before its model has loaded, so it finds nothing then. Look again until it does.
		if ( !found && (sinceStart < RetryTime || Scene.IsEditor) )
			Collect();

		// In the editor the colours are edited in the inspector, which does not come through a change handler, so they
		// are compared here. The game never does this.
		if ( Scene.IsEditor && found && applied != Look )
			Apply();
	}

	/// <summary>Another machine's change arrives here. A change made on this machine does not, so use <see cref="SetLook"/>.</summary>
	void OnColorsChanged( Color oldValue, Color newValue ) => Apply();

	/// <summary>Paint the car now. Sets the colours (synced if this is the host) and tints this machine's copy.</summary>
	public void SetLook( CarLook look )
	{
		Paint = look.Paint;
		Secondary = look.Secondary;
		GlassTint = look.Glass;
		Apply();
	}

	/// <summary>Make copies of the car's paint and window materials and tint them.</summary>
	void Collect()
	{
		if ( Application.IsDedicatedServer )
		{
			found = true;
			return;
		}

		materials.Clear();
		glass = null;

		foreach ( var renderer in GetComponentsInChildren<ModelRenderer>() )
		{
			if ( renderer.Model is null || renderer.Model.IsError )
				continue;

			for ( var i = 0; i < renderer.Model.Materials.Length; i++ )
			{
				var original = renderer.Model.Materials[i];

				if ( IsWindow( original ) )
				{
					glass ??= Material.Load( GlassMaterial )?.CreateCopy();

					if ( glass is not null )
						renderer.Materials.SetOverride( i, glass );

					continue;
				}

				var name = original.Name.ToLowerInvariant();
				var secondary = name.Contains( "secondary" );

				if ( !secondary && !name.Contains( "primary" ) )
					continue;

				var copy = original.CreateCopy();
				renderer.Materials.SetOverride( i, copy );
				materials.Add( (copy, secondary) );
			}
		}

		found = materials.Count > 0 || glass is not null;
		Apply();
	}

	/// <summary>
	/// Whether a material is a window: crosschainer's window glass uses the <c>drift_glass</c> shader, and three of the
	/// cars also draw a rear window with a plain opaque material, <c>dark_cabin.vmat</c>.
	/// </summary>
	static bool IsWindow( Material material ) =>
		(material.ShaderName ?? "").Contains( "drift_glass" )
		|| (material.Name ?? "").ToLowerInvariant().EndsWith( "/dark_cabin.vmat" );

	void Apply()
	{
		applied = Look;

		foreach ( var (material, secondary) in materials )
			material.Set( "g_vColorTint", secondary ? Secondary : Paint );

		if ( glass is null )
			return;

		glass.Set( "LensTint", new Color( GlassTint.r, GlassTint.g, GlassTint.b, 1 ) );
		glass.Set( "LensOpacity", GlassTint.a );
	}
}
