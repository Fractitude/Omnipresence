namespace Sandbox;

/// <summary>
/// The 3D picture of a weapon or a pack of ammo in the gun store app: a small scene of its own (a pivot, a light, a camera)
/// that a <c>ScenePanel</c> draws, built like <see cref="CarPreviewScene"/>. The object is a copy of the prefab with only
/// what is drawn kept (<see cref="ItemIcons"/>), or a bare model, so it is a picture and does nothing.
/// </summary>
/// <remarks>
/// Built before the panel that draws it and destroyed with it (AGENTS.md, "UI pitfalls"). Every game system starts in a
/// scene of its own, so the scene is registered first and <see cref="CharacterMenu.IsPreview"/> keeps the HUD out of it.
/// </remarks>
public sealed class GunPreviewScene
{
	/// <summary>Distance, in object sizes, at which the camera starts.</summary>
	const float FitDistance = 1.5f;

	/// <summary>Zoom limits, as a factor of the fitting distance. The closest view is what used to be the farthest.</summary>
	public const float MinZoom = 1.6f;

	public const float MaxZoom = 2.6f;
	public const float WheelZoom = 1.1f;

	// Not readonly: a hot reload would reset this (AGENTS.md, "Static state and hot reload").
	static HashSet<Scene> scenes = new();

	/// <summary>Whether the scene only draws an item. Game systems must not put a HUD in it.</summary>
	public static bool IsPreviewScene( Scene scene ) => scene is not null && scenes.Contains( scene );

	readonly Scene scene;
	readonly GameObject pivot;
	readonly CameraComponent camera;

	string shownKey;
	float fitDistance = 1;

	/// <summary>Degrees the item has turned.</summary>
	public float Yaw { get; set; } = 210f;

	/// <summary>How high the camera looks from, in degrees above the ground.</summary>
	public float Elevation { get; set; } = 18f;

	/// <summary>Distance as a factor of the fitting distance: below 1 is closer.</summary>
	public float Zoom { get; set; } = MinZoom;

	public GunPreviewScene()
	{
		scene = new Scene();
		// Registered before anything is built in it, so the game systems that start in it know what it is.
		scenes.Add( scene );

		using ( scene.Push() )
		{
			pivot = new GameObject( true, "pivot" );

			camera = new GameObject( true, "camera" ).AddComponent<CameraComponent>();
			camera.BackgroundColor = Color.Parse( "#10151c" ) ?? Color.Black;
			camera.FieldOfView = 30;

			var sun = new GameObject( true, "sun" );
			sun.WorldRotation = Rotation.From( 40, 150, 0 );
			var light = sun.AddComponent<DirectionalLight>();
			light.LightColor = Color.White * 2.5f;
			light.SkyColor = Color.Parse( "#2a3048" ) ?? Color.Gray;

			new GameObject( true, "ambient" ).AddComponent<AmbientLight>().Color = Color.Parse( "#4a4a58" ) ?? Color.Gray;
		}
	}

	/// <summary>The scene for <c>ScenePanel.RenderScene</c>.</summary>
	public Scene Scene => scene;

	/// <summary>Show this item (null shows nothing). Cheap when it is already shown.</summary>
	public void Show( GunItem item )
	{
		if ( item is null )
		{
			Clear();
			return;
		}

		var key = $"{item.Kind}/{item.Id}";

		if ( shownKey == key )
			return;

		Clear();
		shownKey = key;

		using ( scene.Push() )
		{
			// Centred in world space, so the pivot must not be turned while it is made; Update turns it.
			pivot.WorldRotation = Rotation.Identity;

			var bounds = item.Preview.EndsWith( ".vmdl" )
				? CreateModel( item.Preview )
				: ItemIcons.CreatePrefabContent( pivot, item.Preview );

			if ( bounds is not { } size )
				return;

			fitDistance = System.MathF.Max( size.Size.Length * FitDistance, 8f );
			camera.ZNear = fitDistance * 0.02f;
			camera.ZFar = fitDistance * 6f;
		}
	}

	/// <summary>A bare model on the pivot, centred on it. Null if it does not load.</summary>
	BBox? CreateModel( string path )
	{
		var model = Model.Load( path );

		if ( model is null || model.IsError )
			return null;

		var renderer = new GameObject( pivot, true, "model" ).AddComponent<ModelRenderer>();
		renderer.Model = model;
		renderer.LocalPosition = -model.Bounds.Center;
		return model.Bounds;
	}

	/// <summary>Turn the item and put the camera where the view says. Call each frame.</summary>
	public void Update()
	{
		Elevation = System.Math.Clamp( Elevation, CarPreviewScene.MinElevation, CarPreviewScene.MaxElevation );

		Zoom = System.Math.Clamp( Zoom, MinZoom, MaxZoom );

		pivot.WorldRotation = Rotation.FromYaw( Yaw );

		var angle = Elevation.DegreeToRadian();
		var position = new Vector3( -System.MathF.Cos( angle ), 0, System.MathF.Sin( angle ) ) * fitDistance * Zoom;

		camera.WorldPosition = position;
		camera.WorldRotation = Rotation.LookAt( -position );
	}

	/// <summary>Destroy the scene. The panel must not draw it any more.</summary>
	public void Destroy()
	{
		scenes.Remove( scene );
		scene.Destroy();
	}

	void Clear()
	{
		shownKey = null;

		// Right away: nothing ticks this scene (a ScenePanel only draws it), so a plain Destroy would leave the old item.
		foreach ( var child in pivot.Children.ToList() )
			child.DestroyImmediate();
	}
}
