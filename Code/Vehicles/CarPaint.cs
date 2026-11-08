namespace Sandbox;

/// <summary>
/// A car's body paint. Put on a car prefab, it tints the body: the car's own <c>primary</c> materials get
/// <see cref="Paint"/> and its <c>secondary</c> ones <see cref="Secondary"/>. The default colour is set on the prefab in
/// the inspector, and the dealership sets it per car on purchase (<see cref="CarDealer"/>) and in its preview
/// (<see cref="CarPreviewScene"/>), which keeps this component when it strips a car down to what is drawn, so the preview,
/// the prefab and the car in the world are painted by the same code.
/// </summary>
/// <remarks>
/// The tint is set once when the car starts and again when the colour changes (<see cref="SetPaint"/>, or a synced change from the host), never every frame. It works on
/// copies of the materials, put on the renderers as overrides on this machine only, so the models' own materials are
/// untouched. It runs in the editor too, so the prefab shows its colour and the inspector edits it live. The overrides it makes there are saved with the prefab as entries naming the stock materials (the same look), so a prefab saved from the editor shows those as a diff; they are harmless.
/// </remarks>
[Title( "Car Paint" ), Category( "Vehicles" ), Icon( "format_color_fill" )]
public sealed class CarPaint : Component, Component.ExecuteInEditor
{
	/// <summary>How long a car whose model is not loaded yet is looked at again, in seconds.</summary>
	const float RetryTime = 3f;

	/// <summary>The body colour. The prefab's value is the car's stock colour.</summary>
	[Property, Sync( SyncFlags.FromHost ), Change( nameof(OnPaintChanged) )]
	public Color Paint { get; set; } = Color.Red;

	/// <summary>The colour of the car's secondary parts (trim, mirrors, spoiler).</summary>
	[Property]
	public Color Secondary { get; set; } = new( 0.098f, 0.11f, 0.125f );

	readonly List<(Material Material, bool Secondary)> materials = [];
	TimeSince sinceStart;
	bool found;
	Color appliedPaint;
	Color appliedSecondary;

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

		// In the editor the colours are edited in the inspector, which does not come through a change handler, so the
		// two are compared here. The game never does this.
		if ( Scene.IsEditor && found && (appliedPaint != Paint || appliedSecondary != Secondary) )
			Apply();
	}

	/// <summary>Another machine's change arrives here. A change made on this machine does not, so use <see cref="SetPaint"/>.</summary>
	void OnPaintChanged( Color oldValue, Color newValue ) => Apply();

	/// <summary>Paint the car now. Sets <see cref="Paint"/> (synced if this is the host) and tints this machine's copy.</summary>
	public void SetPaint( Color color )
	{
		Paint = color;
		Apply();
	}

	/// <summary>Make copies of the car's paint materials and tint them.</summary>
	void Collect()
	{
		if ( Application.IsDedicatedServer )
		{
			found = true;
			return;
		}

		materials.Clear();

		foreach ( var renderer in GetComponentsInChildren<ModelRenderer>() )
		{
			if ( renderer.Model is null || renderer.Model.IsError )
				continue;

			for ( var i = 0; i < renderer.Model.Materials.Length; i++ )
			{
				var original = renderer.Model.Materials[i];
				var name = original.Name.ToLowerInvariant();
				var secondary = name.Contains( "secondary" );

				if ( !secondary && !name.Contains( "primary" ) )
					continue;

				var copy = original.CreateCopy();
				renderer.Materials.SetOverride( i, copy );
				materials.Add( (copy, secondary) );
			}
		}

		found = materials.Count > 0;
		Apply();
	}

	void Apply()
	{
		appliedPaint = Paint;
		appliedSecondary = Secondary;

		foreach ( var (material, secondary) in materials )
			material.Set( "g_vColorTint", secondary ? Secondary : Paint );
	}
}
