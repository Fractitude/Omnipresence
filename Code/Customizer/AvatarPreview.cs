using Sandbox.Utility;
using Sandbox.UI;
using System.Threading;

namespace Sandbox;

/// <summary>
/// Where the preview camera zooms in to.
/// </summary>
public enum PreviewFocus
{
	Head,
	Chest,
	Pelvis,
	Legs,
	Feet
}

/// <summary>
/// A human in its own little scene, dressed in whatever outfit it's given.
/// Drag to turn it around, scroll to zoom in on the <see cref="ZoomFocus"/>.
/// </summary>
public sealed class AvatarPreview : Panel
{
	const float DefaultYaw = -20;
	const float VerticalFov = 40;

	// camera distance and look height for the whole body, and the distance when zoomed in
	static readonly Vector2 BodyView = new( 135, 37 );
	const float FocusDistance = 36;

	readonly ScenePanel _scene;
	SkinnedModelRenderer _body;
	CameraComponent _camera;
	CancellationTokenSource _dressing;
	ClothingContainer _outfit;
	Dictionary<string, Color> _colors = [];
	float _yaw = DefaultYaw;
	float _zoom;
	float _targetZoom;
	float _focusHeight = GetFocusHeight( PreviewFocus.Head );

	/// <summary>
	/// A thumbnail: no turning or zooming, and once dressed and posed it stops rendering every frame.
	/// </summary>
	public bool IsStatic { get; set; }

	// time for the pose to play out before the last frame is kept
	RealTimeUntil _freezeAt;
	bool _freezePending;

	/// <summary>
	/// The part of the body scrolling zooms in to.
	/// </summary>
	public PreviewFocus ZoomFocus { get; set; } = PreviewFocus.Head;

	static float GetFocusHeight( PreviewFocus focus ) => focus switch
	{
		PreviewFocus.Head => 68,
		PreviewFocus.Chest => 50,
		PreviewFocus.Pelvis => 38,
		PreviewFocus.Legs => 22,
		_ => 6
	};

	/// <summary>
	/// The scenes the previews draw. Every game system starts in a scene of its own, so they must not put a HUD in these
	/// (which would hold a customizer, which would make another preview scene, for ever): see
	/// <see cref="CharacterMenu.IsPreview"/>.
	/// </summary>
	static HashSet<Scene> previewScenes = new();

	public static bool IsPreviewScene( Scene scene ) => previewScenes.Contains( scene );

	readonly Scene _world;

	public AvatarPreview()
	{
		AddClass( "avatar-preview" );

		// Before the panel exists: a ScenePanel with no scene makes its own, and ticks the game scene.
		_world = new Scene();
		previewScenes.Add( _world );

		_scene = new ScenePanel { RenderScene = _world };
		_scene.AddClass( "scene" );
		AddChild( _scene );
		BuildScene();
	}

	void BuildScene()
	{
		using ( _scene.RenderScene.Push() )
		{
			var cameraObject = new GameObject( true, "Camera" );
			cameraObject.WorldRotation = Rotation.FromYaw( 180 );

			_camera = cameraObject.AddComponent<CameraComponent>();
			_camera.BackgroundColor = Color.Transparent;
			_camera.ZNear = 1;
			_camera.ZFar = 512;

			var sunObject = new GameObject( true, "Sun" );
			sunObject.WorldRotation = Rotation.From( 50, 150, 0 );

			var sun = sunObject.AddComponent<DirectionalLight>();
			sun.LightColor = new Color( 1.0f, 0.96f, 0.9f ) * 1.6f;
			sun.SkyColor = new Color( 0.35f, 0.38f, 0.45f );

			var bodyObject = new GameObject( true, "Body" );
			bodyObject.WorldRotation = Rotation.FromYaw( _yaw );

			_body = bodyObject.AddComponent<SkinnedModelRenderer>();
			_body.Model = Model.Load( "models/citizen_human/human.vmdl" );

			// hands on hips
			_body.Set( "special_idle_states", 1 );
		}
	}

	/// <summary>
	/// Dress the preview, it gets its own copy so the outfit can keep changing.
	/// </summary>
	public void SetOutfit( ClothingContainer outfit, IReadOnlyDictionary<string, Color> colors )
	{
		_dressing?.Cancel();
		_dressing = new CancellationTokenSource();

		_outfit = ClothingContainer.CreateFromJson( outfit.Serialize() );
		_colors = OutfitColors.Copy( colors );

		_ = Dress( _outfit, _dressing.Token );
	}

	/// <summary>
	/// Retint without dressing again.
	/// </summary>
	public void SetColors( IReadOnlyDictionary<string, Color> colors )
	{
		_colors = OutfitColors.Copy( colors );
		OutfitColors.Apply( _body, _outfit, _colors );
	}

	async Task Dress( ClothingContainer outfit, CancellationToken token )
	{
		if ( !_body.IsValid() ) return;

		try
		{
			using ( _scene.RenderScene.Push() )
			{
				var dresser = Dresser.GetOrCreate( _body );
				dresser.UpdateAppearance( outfit );
				await dresser.ApplyAsync( outfit, token );
				OutfitColors.Apply( _body, outfit, _colors );

				if ( IsStatic )
				{
					_scene.RenderOnce = false;
					_freezeAt = 0.6f;
					_freezePending = true;
				}
			}
		}
		catch ( OperationCanceledException )
		{
			// a newer outfit replaced this one
		}
	}

	/// <summary>
	/// Back to the whole body, facing the camera.
	/// </summary>
	public void ResetView()
	{
		_yaw = DefaultYaw;
		_targetZoom = 0;
	}

	public override void OnMouseWheel( Vector2 value )
	{
		if ( IsStatic )
		{
			base.OnMouseWheel( value );
			return;
		}

		_targetZoom = (_targetZoom - value.y * 0.1f).Clamp( 0, 1 );
	}

	public override void Tick()
	{
		base.Tick();

		if ( HasActive && !IsStatic )
			_yaw += Mouse.Delta.x * 0.5f;

		if ( _freezePending && _freezeAt )
		{
			_freezePending = false;
			_scene.RenderOnce = true;
			_scene.RenderNextFrame();
		}

		if ( _body.IsValid() )
			_body.WorldRotation = Rotation.FromYaw( _yaw );

		UpdateCamera();
	}

	/// <summary>
	/// The player's body height (the Player Controller's Body Height) against the one the camera numbers were made for.
	/// </summary>
	const float DefaultBodyHeight = 72;

	static float HeightScale =>
		Player.FindLocalPlayer()?.Controller is { IsValid: true } controller && controller.BodyHeight > 0
			? controller.BodyHeight / DefaultBodyHeight
			: 1;

	void UpdateCamera()
	{
		if ( !_camera.IsValid() ) return;

		_zoom = _zoom.LerpTo( _targetZoom, RealTime.Delta * 8 );

		_focusHeight = _focusHeight.LerpTo( GetFocusHeight( ZoomFocus ), RealTime.Delta * 8 );

		var focusView = new Vector2( FocusDistance, _focusHeight );
		// thumbnails are small, so they frame the body a little tighter
		var bodyView = IsStatic ? BodyView with { x = BodyView.x * 0.85f } : BodyView;
		var view = Vector2.Lerp( bodyView, focusView, Easing.QuadraticInOut( _zoom ) );
		// the heights above are for a body of the default height: scale them to the player's
		view *= HeightScale;
		_camera.WorldPosition = new Vector3( view.x, 0, view.y );

		// the camera's fov is horizontal, so work it out from the vertical one to frame the same at any panel shape
		var size = Box.Rect.Size;
		if ( size.y <= 0 ) return;

		float halfHeight = MathF.Tan( VerticalFov.DegreeToRadian() * 0.5f );
		_camera.FieldOfView = (2 * MathF.Atan( halfHeight * size.x / size.y )).RadianToDegree();
	}

	public override void OnDeleted()
	{
		_dressing?.Cancel();
		_scene.Delete( true );
		base.OnDeleted();

		// after the panel that draws it
		previewScenes.Remove( _world );
		_world.Destroy();
	}
}
