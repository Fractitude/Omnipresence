namespace Sandbox;

/// <summary>
/// The scenes that draw item icons: the item's prefab (or just its model) on a spinning pivot, framed by its longest
/// side. One scene for each prefab or model, made when first asked for and kept, since a panel may still draw it (see
/// AGENTS.md, "UI pitfalls").
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
	/// The scene that draws an item: the whole prefab if there is one that loads, else the model. Null if neither does.
	/// </summary>
	public static Scene SceneFor( string prefabPath, string modelPath )
	{
		if ( !string.IsNullOrEmpty( prefabPath ) && SceneFor( prefabPath, true ) is { } fromPrefab )
			return fromPrefab;

		return string.IsNullOrEmpty( modelPath ) ? null : SceneFor( modelPath, false );
	}

	static Scene SceneFor( string path, bool prefab )
	{
		var key = (prefab ? "prefab:" : "model:") + path;

		if ( scenes.TryGetValue( key, out var existing ) )
			return existing;

		// Registered (as null) first, so a failure isn't tried again every frame.
		scenes[key] = null;
		return Create( key, path, prefab );
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

	static Scene Create( string key, string path, bool prefab )
	{
		var created = new Scene();
		// Registered before anything is built in it, so the game systems that start in it know it is an icon scene.
		scenes[key] = created;

		var failed = false;

		using ( created.Push() )
		{
			var pivot = new GameObject( true, "pivot" );
			pivots[key] = pivot;

			var content = prefab ? CreatePrefabContent( pivot, path ) : CreateModelContent( pivot, path );

			if ( content is not { } bounds )
			{
				failed = true;
				goto done;
			}

			// Frame all of it, whichever way it turns: back off until the sphere around its bounds fits the view.
			const float fieldOfView = 30f;
			var radius = System.MathF.Max( bounds.Size.Length * 0.5f, 0.1f );
			var distance = radius / System.MathF.Sin( (fieldOfView * 0.5f).DegreeToRadian() ) * 1.15f;

			var camera = new GameObject( true, "camera" ).AddComponent<CameraComponent>();
			camera.BackgroundColor = Color.Transparent;
			camera.FieldOfView = fieldOfView;

			// Looking down a little.
			var position = new Vector3( System.MathF.Cos( 20f.DegreeToRadian() ), 0, System.MathF.Sin( 20f.DegreeToRadian() ) ) * distance;
			camera.WorldPosition = position;
			camera.WorldRotation = Rotation.LookAt( -position );
			camera.ZNear = distance * 0.05f;
			camera.ZFar = distance * 4f;

			var sun = new GameObject( true, "sun" );
			sun.WorldRotation = Rotation.From( 35, 200, 0 );
			var light = sun.AddComponent<DirectionalLight>();
			light.LightColor = Color.White * 2.5f;
			light.SkyColor = Color.Parse( "#2a2840" ) ?? Color.Gray;

			new GameObject( true, "ambient" ).AddComponent<AmbientLight>().Color = Color.Parse( "#4a4638" ) ?? Color.Gray;
		}

		done:

		if ( !failed )
			return created;

		// Nothing to draw: forget the scene. No panel draws it yet.
		scenes[key] = null;
		pivots.Remove( key );
		created.Destroy();
		return null;
	}

	/// <summary>
	/// Put a model on the pivot, centred on it. Returns its bounds, or null if it doesn't load.
	/// </summary>
	static BBox? CreateModelContent( GameObject pivot, string modelPath )
	{
		var model = Model.Load( modelPath );

		if ( model is null || model.IsError )
			return null;

		var renderer = new GameObject( pivot, true, "model" ).AddComponent<ModelRenderer>();
		renderer.Model = model;
		var bounds = model.Bounds;
		renderer.LocalPosition = -bounds.Center;
		return bounds;
	}

	/// <summary>
	/// Put a copy of a prefab on the pivot, centred on it, with only what is drawn kept: its game components (pickup,
	/// colliders, physics, network) are removed, so it is a picture and does nothing. Returns the bounds of what is
	/// drawn, or null if the prefab doesn't load or draws nothing.
	/// </summary>
	static BBox? CreatePrefabContent( GameObject pivot, string prefabPath )
	{
		if ( !ResourceLibrary.TryGet<PrefabFile>( prefabPath, out var file ) )
			return null;

		// Disabled while it is stripped, so none of its components wake up.
		var clone = GameObject.Clone( file, new CloneConfig { Parent = pivot, StartEnabled = false, Transform = new Transform( Vector3.Zero ) } );
		clone.NetworkMode = NetworkMode.Never;

		foreach ( var go in clone.GetAllObjects( false ).Append( clone ) )
			go.NetworkMode = NetworkMode.Never;

		foreach ( var component in clone.Components.GetAll( FindMode.EverythingInSelfAndDescendants ).ToList() )
		{
			if ( component is not Renderer )
				component.Destroy();
		}

		clone.Enabled = true;

		var renderers = clone.Components.GetAll<Renderer>( FindMode.EnabledInSelfAndDescendants ).ToList();

		if ( renderers.Count == 0 )
		{
			clone.Destroy();
			return null;
		}

		var bounds = clone.GetBounds();

		// Centred on the pivot, so it turns on the spot. The pivot is at the origin, so bounds are in its space.
		clone.WorldPosition = -bounds.Center;
		return new BBox( bounds.Mins - bounds.Center, bounds.Maxs - bounds.Center );
	}
}
