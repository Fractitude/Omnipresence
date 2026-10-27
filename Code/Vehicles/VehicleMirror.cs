namespace Sandbox;

/// <summary>
/// A car's door mirror. While the local player is driving the car and the mirror itself is somewhere on that
/// player's own screen, a small low-resolution camera mounted at the mirror and pointed back along the car renders
/// what's behind onto a little screen there, a few times a second. The render only runs for that one client: nobody
/// else - a passenger, another player's own screen, anyone standing outside the car - ever asks the camera for a
/// picture, so the screen stays switched off for them and they see the car's own modelled mirror underneath it,
/// untouched.
/// </summary>
/// <remarks>
/// The screen is a <see cref="WorldPanel"/> showing a <see cref="VehicleMirrorPanel"/>, the same
/// <c>&lt;Image Texture=@...&gt;</c> pattern <see cref="SecurityCamera"/>'s feed uses, rather than a textured mesh: a
/// car body's own paint materials don't take a plain texture override reliably, where a UI image always does.
/// </remarks>
[Title( "Vehicle Mirror" ), Category( "Vehicles" ), Icon( "directions_car" )]
public sealed class VehicleMirror : Component
{
	public enum MirrorSide { Left, Right }

	/// <summary>Which side of the car this is on - only changes which way the screen leans to face the driver.</summary>
	[Property] public MirrorSide Side { get; set; } = MirrorSide.Left;

	/// <summary>The feed's size, in pixels: low enough to be cheap and to read as a mirror, not a window.</summary>
	[Property] public Vector2Int TextureSize { get; set; } = new( 128, 64 );

	/// <summary>How wide the mirror's own view is, in degrees.</summary>
	[Property, Range( 20f, 90f )] public float FieldOfView { get; set; } = 50f;

	/// <summary>How many pictures the mirror draws a second while its driver can see it.</summary>
	[Property, Range( 5f, 60f )] public float RefreshRate { get; set; } = 20f;

	/// <summary>How wide the little screen is, inches.</summary>
	[Property] public float ScreenWidth { get; set; } = 6f;

	/// <summary>How tall the little screen is, inches.</summary>
	[Property] public float ScreenHeight { get; set; } = 3.5f;

	/// <summary>The camera the feed is drawn through, kept from the first frame it's needed.</summary>
	CameraComponent lens;

	/// <summary>The little in-world screen the driver sees the feed on.</summary>
	WorldPanel screen;

	/// <summary>The panel the screen draws - holds the one texture the picture is painted into.</summary>
	VehicleMirrorPanel panel;

	/// <summary>The seat this mirror belongs to - found once, on whatever vehicle this mirror is a child of.</summary>
	BaseChair seat;

	Texture feed;
	RealTimeSince sinceFrame = float.MaxValue;

	protected override void OnEnabled()
	{
		EnsureLens();
		EnsureScreen();
	}

	protected override void OnStart()
	{
		seat ??= GetComponentInParent<BaseChair>();
	}

	/// <summary>
	/// Show the feed only to the player actually driving, and only spend the render while they could see the mirror.
	/// </summary>
	protected override void OnPreRender()
	{
		seat ??= GetComponentInParent<BaseChair>();

		if ( !IsLocalDriver() )
		{
			SetVisible( false );
			return;
		}

		SetVisible( true );

		if ( !InLocalView() || sinceFrame < 1f / RefreshRate )
			return;

		sinceFrame = 0;

		var camera = EnsureLens();
		camera.FieldOfView = FieldOfView;
		camera.RenderToTexture( EnsureFeed() );
	}

	/// <summary>The local player is the one sitting in this mirror's car.</summary>
	bool IsLocalDriver()
	{
		var driver = seat.IsValid() ? seat.GetOccupant() : null;
		return driver.IsValid() && driver.Network.IsOwner;
	}

	/// <summary>The mirror itself is somewhere on the local player's own screen right now.</summary>
	bool InLocalView()
	{
		if ( Scene.Camera is not { } camera )
			return false;

		var screenPos = camera.PointToScreenNormal( WorldPosition, out var isBehind );
		const float margin = 0.08f;

		return !isBehind && screenPos.x is >= -margin and <= 1f + margin && screenPos.y is >= -margin and <= 1f + margin;
	}

	void SetVisible( bool visible )
	{
		var worldPanel = EnsureScreen();

		if ( worldPanel.Enabled != visible )
			worldPanel.Enabled = visible;

		if ( panel.IsValid() )
			panel.Texture = visible ? EnsureFeed() : null;
	}

	Texture EnsureFeed() => feed ??= Texture.CreateRenderTarget().WithSize( TextureSize.x, TextureSize.y ).Create();

	/// <summary>
	/// The camera the feed is drawn through, pointed straight back along the car. Made once, as a child that doesn't
	/// get saved into the prefab.
	/// </summary>
	CameraComponent EnsureLens()
	{
		if ( lens.IsValid() )
			return lens;

		lens = GetComponentInChildren<CameraComponent>( true );

		if ( !lens.IsValid() )
		{
			var go = new GameObject( GameObject, true, "Mirror Lens" );
			go.Flags |= GameObjectFlags.NotSaved;
			go.LocalRotation = Rotation.LookAt( Vector3.Backward, Vector3.Up );

			lens = go.Components.Create<CameraComponent>();
		}

		lens.IsMainCamera = false;
		lens.Enabled = false;

		// The held weapon and the arms in front of the player's own camera have no business in a mirror.
		if ( !lens.RenderExcludeTags.Has( "viewmodel" ) )
			lens.RenderExcludeTags.Add( "viewmodel" );

		return lens;
	}

	/// <summary>
	/// The little screen, leant to face the driver: mostly inward, a little back. Made once, not saved into the
	/// prefab.
	/// </summary>
	WorldPanel EnsureScreen()
	{
		if ( screen.IsValid() )
			return screen;

		screen = GetComponentInChildren<WorldPanel>( true );

		if ( !screen.IsValid() )
		{
			var go = new GameObject( GameObject, true, "Mirror Screen" );
			go.Flags |= GameObjectFlags.NotSaved;

			var inward = Side == MirrorSide.Left ? Vector3.Right : Vector3.Left;
			var facing = (Vector3.Backward * 0.4f + inward + Vector3.Up * 0.05f).Normal;
			go.LocalRotation = Rotation.LookAt( facing, Vector3.Up );

			screen = go.Components.Create<WorldPanel>();
			panel = go.Components.Create<VehicleMirrorPanel>();
		}

		panel ??= screen.GetComponent<VehicleMirrorPanel>() ?? screen.Components.Create<VehicleMirrorPanel>();

		screen.PanelSize = new Vector2( ScreenWidth, ScreenHeight ) / Sandbox.UI.WorldPanel.ScreenToWorldScale;
		screen.LookAtCamera = false;
		screen.Lighting = false;

		return screen;
	}

	protected override void OnDisabled() => SetVisible( false );

	protected override void OnDestroy()
	{
		feed?.Dispose();
		feed = null;
	}

	/// <summary>Show where the mirror's camera looks, for placing it: a cone pointed straight back along the car.</summary>
	protected override void DrawGizmos()
	{
		const float reach = 60f;
		var half = reach * MathF.Tan( FieldOfView * 0.5f * MathF.PI / 180f );

		var corners = new[]
		{
			new Vector3( -reach, half, half ),
			new Vector3( -reach, -half, half ),
			new Vector3( -reach, -half, -half ),
			new Vector3( -reach, half, -half ),
		};

		Gizmo.Draw.Color = Color.Cyan;

		for ( var i = 0; i < corners.Length; i++ )
		{
			Gizmo.Draw.Line( Vector3.Zero, corners[i] );
			Gizmo.Draw.Line( corners[i], corners[(i + 1) % corners.Length] );
		}
	}
}
