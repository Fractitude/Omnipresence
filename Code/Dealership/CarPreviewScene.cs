namespace Sandbox;

/// <summary>
/// The 3D picture of a car in the dealership app: a small scene of its own (a pivot, a light, a camera) that a
/// <c>ScenePanel</c> draws. The car is a copy of the prefab with only what is drawn kept, as in an item icon
/// (<see cref="ItemIcons"/>), so it is a picture and does nothing. The view can be turned and zoomed; none of it is shared,
/// it is the view of the player who does it.
/// </summary>
/// <remarks>
/// Built before the panel that draws it and destroyed with it (AGENTS.md, "UI pitfalls"). Every game system starts in a
/// scene of its own, so the scene is registered first and <see cref="CharacterMenu.IsPreview"/> keeps the HUD out of it.
/// </remarks>
public sealed class CarPreviewScene
{
	/// <summary>Distance, in car sizes, at which the camera starts.</summary>
	const float FitDistance = 1.6f;

	const float MinZoom = 0.4f;
	const float MaxZoom = 1.6f;
	const float WheelZoom = 1.1f;

	const float MinElevation = 2f;
	const float MaxElevation = 60f;

	// Not readonly: a hot reload would reset these (AGENTS.md, "Static state and hot reload").
	static HashSet<Scene> scenes = new();
	static HashSet<string> mounting = new();
	static HashSet<string> mounted = new();

	/// <summary>Whether the scene only draws a car. Game systems must not put a HUD in it.</summary>
	public static bool IsPreviewScene( Scene scene ) => scene is not null && scenes.Contains( scene );

	readonly Scene scene;
	readonly GameObject pivot;
	readonly CameraComponent camera;

	string shownId;
	CarPaint shownPaint;
	float fitDistance = 1;

	/// <summary>Degrees the car has turned.</summary>
	public float Yaw { get; set; } = 210f;

	/// <summary>How high the camera looks from, in degrees above the ground.</summary>
	public float Elevation { get; set; } = 18f;

	/// <summary>Distance as a factor of the fitting distance: below 1 is closer.</summary>
	public float Zoom { get; set; } = 1f;

	public CarPreviewScene()
	{
		scene = new Scene();
		// Registered before anything is built in it, so the game systems that start in it know what it is.
		scenes.Add( scene );

		using ( scene.Push() )
		{
			pivot = new GameObject( true, "pivot" );

			camera = new GameObject( true, "camera" ).AddComponent<CameraComponent>();
			camera.BackgroundColor = Color.Parse( "#10151c" ) ?? Color.Black;
			camera.FieldOfView = 36;

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

	/// <summary>
	/// Show this car (null shows nothing). Cheap when it is already shown. A sent's assets are mounted first; the car
	/// appears when they are there.
	/// </summary>
	public void Show( CarModel car, CarLook? look = null )
	{
		if ( car is null )
		{
			Clear();
			return;
		}

		if ( car.Ident != "" && !mounted.Contains( car.Ident ) )
		{
			Mount( car.Ident );
			return;
		}

		if ( shownId == car.Id )
		{
			Paint( look );
			return;
		}

		Clear();
		shownId = car.Id;

		using ( scene.Push() )
		{
			// The car is centred in world space, so the pivot must not be turned while it is made; Update turns it.
			pivot.WorldRotation = Rotation.Identity;

			if ( ItemIcons.CreatePrefabContent( pivot, car.Prefab, keepPaint: true ) is not { } bounds )
				return;

			fitDistance = bounds.Size.Length * FitDistance;
			camera.ZNear = fitDistance * 0.02f;
			camera.ZFar = fitDistance * 6f;
		}

		shownPaint = pivot.GetComponentInChildren<CarPaint>( true );
		Paint( look );
	}

	/// <summary>Paint the car shown. Null leaves the colours its prefab gives it. Only sets them when they changed.</summary>
	void Paint( CarLook? look )
	{
		if ( look is { } colours && shownPaint.IsValid() && shownPaint.Look != colours )
			shownPaint.SetLook( colours );
	}

	/// <summary>Zoom by wheel notches (positive is away).</summary>
	public void ZoomBy( float notches ) =>
		Zoom = System.Math.Clamp( Zoom * System.MathF.Pow( WheelZoom, notches ), MinZoom, MaxZoom );

	/// <summary>Turn the car and put the camera where the view says. Call each frame.</summary>
	public void Update()
	{
		Elevation = System.Math.Clamp( Elevation, MinElevation, MaxElevation );

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
		shownId = null;
		shownPaint = null;

		foreach ( var child in pivot.Children.ToList() )
			child.Destroy();
	}

	/// <summary>Mount a sent's assets once; the next <see cref="Show"/> after it is done builds the car.</summary>
	static async void Mount( string ident )
	{
		if ( !mounting.Add( ident ) )
			return;

		await SentLocal.Mount( ident );

		mounting.Remove( ident );
		mounted.Add( ident );
	}
}
