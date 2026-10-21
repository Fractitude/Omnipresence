namespace Sandbox;

/// <summary>
/// The scenes that draw item icons: the item's model on a spinning pivot, framed by its longest side. One scene for each
/// model, made when first asked for and kept, since a panel may still draw it (see AGENTS.md, "UI pitfalls").
/// </summary>
public static class ItemIcons
{
	/// <summary>
	/// Degrees per second.
	/// </summary>
	const float SpinSpeed = 45f;

	// Not readonly: a hot reload would reset these (see AGENTS.md, "Static state and hot reload").
	static Dictionary<string, Scene> scenes = new();
	static Dictionary<string, GameObject> pivots = new();

	/// <summary>
	/// Whether the scene is one of the icon scenes. Game systems must not put a HUD in those.
	/// </summary>
	public static bool IsIconScene( Scene scene ) => scene is not null && scenes.ContainsValue( scene );

	/// <summary>
	/// The scene that draws a model, or null if there is no such model.
	/// </summary>
	public static Scene SceneFor( string modelPath )
	{
		if ( string.IsNullOrEmpty( modelPath ) )
			return null;

		if ( scenes.TryGetValue( modelPath, out var existing ) )
			return existing;

		var model = Model.Load( modelPath );
		var scene = model is null || model.IsError ? null : Create( modelPath, model );
		scenes[modelPath] = scene;
		return scene;
	}

	/// <summary>
	/// Turn every icon. Call it each frame while icons are on the screen.
	/// </summary>
	public static void Spin()
	{
		var rotation = Rotation.FromYaw( RealTime.Now * SpinSpeed );

		foreach ( var pivot in pivots.Values )
		{
			if ( pivot.IsValid() )
				pivot.WorldRotation = rotation;
		}
	}

	static Scene Create( string key, Model model )
	{
		var created = new Scene();
		// Registered before anything is built in it, so the game systems that start in it know it is an icon scene.
		scenes[key] = created;

		using ( created.Push() )
		{
			var pivot = new GameObject( true, "pivot" );
			pivots[key] = pivot;

			var renderer = new GameObject( pivot, true, "model" ).AddComponent<ModelRenderer>();
			renderer.Model = model;

			// Centred on the pivot, so it turns on the spot.
			var bounds = model.Bounds;
			renderer.LocalPosition = -bounds.Center;

			// Frame the longest side it shows while it turns: its height, or its widest turn (the diagonal of its base).
			var size = bounds.Size;
			var extent = System.MathF.Max( size.z, System.MathF.Sqrt( size.x * size.x + size.y * size.y ) );
			extent = System.MathF.Max( extent, 0.1f );

			var camera = new GameObject( true, "camera" ).AddComponent<CameraComponent>();
			camera.BackgroundColor = Color.Transparent;
			camera.Orthographic = true;
			camera.OrthographicHeight = extent * 1.1f;

			// Looking down a little. The distance doesn't change the size, but must clear the model.
			var distance = System.MathF.Max( size.Length * 2f, 50f );
			var position = new Vector3( System.MathF.Cos( 20f.DegreeToRadian() ), 0, System.MathF.Sin( 20f.DegreeToRadian() ) ) * distance;
			camera.WorldPosition = position;
			camera.WorldRotation = Rotation.LookAt( -position );
			camera.ZNear = 1f;
			camera.ZFar = distance * 4f;

			var sun = new GameObject( true, "sun" );
			sun.WorldRotation = Rotation.From( 35, 200, 0 );
			var light = sun.AddComponent<DirectionalLight>();
			light.LightColor = Color.White * 2.5f;
			light.SkyColor = Color.Parse( "#2a2840" ) ?? Color.Gray;

			new GameObject( true, "ambient" ).AddComponent<AmbientLight>().Color = Color.Parse( "#4a4638" ) ?? Color.Gray;
		}

		return created;
	}
}
