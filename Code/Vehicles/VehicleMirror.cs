namespace Sandbox;

/// <summary>
/// A car's door mirror. While the local player is driving the car, a small camera at the glass looks along the line
/// their own sight takes when it bounces off the glass - the mirror's view, framed by the road behind rather than by
/// the car's own flank - and paints what it sees straight onto the glass, a few frames a second. Only that one client
/// ever asks for a picture: nobody else sees anything but the car's own modelled mirror.
/// </summary>
/// <remarks>
/// The glass is a quad of its own, sitting on the mirror's glass face and dressed in
/// <c>shaders/vehicle_mirror.shader</c>, rather than a <see cref="WorldPanel"/>: a panel is a screen hanging in the
/// world with its own idea of where its front is, and one placed at a mirror ends up inside the housing, where the
/// driver cannot see it. A quad is the glass.
/// </remarks>
[Title( "Vehicle Mirror" ), Category( "Vehicles" ), Icon( "directions_car" )]
public sealed class VehicleMirror : Component, Component.ExecuteInEditor
{
	/// <summary>
	/// Which way the glass faces, in this object's space: out of the mirror, towards the driver. The object itself sits
	/// at the middle of the glass.
	/// </summary>
	[Property, Group( "Glass" )] public Vector3 GlassNormal { get; set; } = new( -1f, 0f, 0f );

	/// <summary>How big the glass is, in inches. The quad is cut to a rounded rectangle of this size.</summary>
	[Property, Group( "Glass" )] public Vector2 GlassSize { get; set; } = new( 6f, 3.4f );

	/// <summary>How far in front of the modelled glass the quad sits, in inches: enough not to fight it for depth.</summary>
	[Property, Group( "Glass" ), Range( 0f, 3f )] public float GlassProud { get; set; } = 0.3f;

	/// <summary>Slide the quad within its plane, inches: X is up the glass, Y is across it.</summary>
	[Property, Group( "Glass" )] public Vector2 GlassShift { get; set; } = Vector2.Zero;

	/// <summary>Tilt the quad off the glass normal, degrees: for a glass face that is not quite square to it.</summary>
	[Property, Group( "Glass" )] public Angles GlassAngles { get; set; } = Angles.Zero;

	/// <summary>The feed's size, in pixels: low enough to be cheap and to read as a mirror, not a window.</summary>
	[Property, Group( "Lens" )] public Vector2Int TextureSize { get; set; } = new( 256, 128 );

	/// <summary>
	/// The widest the mirror's view may be, in degrees. A mirror's true view is the angle its glass covers from the
	/// driver's head, which is narrow; this caps it so a mirror never becomes a porthole.
	/// </summary>
	[Property, Group( "Lens" ), Range( 20f, 90f )] public float MaxFieldOfView { get; set; } = 55f;

	/// <summary>
	/// How far, in degrees, the mirror is turned away from the car's flank - the adjustment every driver makes when
	/// they set their mirrors, so the view shows the road behind rather than their own rear quarter.
	/// </summary>
	[Property, Group( "Lens" ), Range( 0f, 40f )] public float AimOutward { get; set; } = 12f;

	/// <summary>How many pictures the mirror draws a second while its driver can see it.</summary>
	[Property, Group( "Lens" ), Range( 5f, 60f )] public float RefreshRate { get; set; } = 20f;

	/// <summary>
	/// How far along the glass normal the camera stands, inches: positive is in front of the glass, on the driver's
	/// side of it. Pull it back to look through more of the glass, push it out to clear the housing.
	/// </summary>
	[Property, Group( "Lens" )] public float LensBack { get; set; } = 0.6f;

	/// <summary>Slide the camera within the glass's plane, inches: X is up the glass, Y is across it.</summary>
	[Property, Group( "Lens" )] public Vector2 LensShift { get; set; } = Vector2.Zero;

	/// <summary>
	/// Turn the camera off the aim the mirror's geometry gives it, degrees, in world space: the last word when the
	/// framing is wrong.
	/// </summary>
	[Property, Group( "Lens" )] public Angles LensAngles { get; set; } = Angles.Zero;

	/// <summary>The tag the glass carries, so the mirror's own lens never photographs it.</summary>
	const string GlassTag = "mirrorfeed";

	/// <summary>The camera the feed is drawn through, kept from the first frame it's needed.</summary>
	CameraComponent lens;

	/// <summary>The quad standing in for the glass.</summary>
	ModelRenderer glass;

	/// <summary>The one material this mirror paints its feed with - every mirror needs its own.</summary>
	Material material;

	/// <summary>The seat this mirror belongs to, found on whatever vehicle this mirror is a child of.</summary>
	BaseChair seat;

	Texture feed;
	RealTimeSince sinceFrame = float.MaxValue;

	/// <summary>A copy that bowed out to a twin, before the end of the frame its destruction takes: it builds nothing.</summary>
	bool retired;

	/// <summary>
	/// A prefab instance can arrive with more than one copy of this component: the engine matches an instance's
	/// components to its prefab by id, and one it cannot match is added rather than taken over - so a scene whose
	/// instance predates the mirror objects ends up with the prefab's copy and its own on the same object. One mirror
	/// is enough: the copy with the lowest id keeps the job and the rest go before they build anything.
	/// </summary>
	protected override void OnEnabled()
	{
		var twins = GameObject.Components.GetAll<VehicleMirror>().ToList();

		if ( twins.Count > 1 )
		{
			var keeper = twins.OrderBy( x => x.Id.ToString(), StringComparer.Ordinal ).First();

			if ( keeper != this )
			{
				retired = true;
				Destroy();
				return;
			}

			foreach ( var extra in twins.Where( x => x != this ).ToList() )
			{
				Log.Warning( $"{GameObject.Name}: {twins.Count} VehicleMirror components on one object, keeping one" );
				extra.retired = true;
				extra.Destroy();
			}
		}
	}

	protected override void OnStart() => seat ??= GetComponentInParent<BaseChair>();

	/// <summary>
	/// In the editor a mirror draws for whoever is looking at the scene, at every frame it can, so its glass and its
	/// camera can be lined up by hand. It runs from here as well as <see cref="OnPreRender"/> because a viewport that
	/// is not playing does not always reach the render stage.
	/// </summary>
	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying )
			UpdateFeed();
	}

	/// <summary>
	/// Show the feed only to the player actually driving, and only spend the render while they could see the mirror.
	/// </summary>
	protected override void OnPreRender() => UpdateFeed();

	void UpdateFeed()
	{
		if ( retired )
			return;

		seat ??= GetComponentInParent<BaseChair>();

		// Playing: the driver's own eye, and only while their own screen shows the mirror. Editing: no driver, so the
		// editor's camera stands in for one and the feed is always drawn.
		var eye = Game.IsPlaying ? LocalDriver()?.EyeTransform.Position : AdjustingEye();

		if ( eye is null )
		{
			SetVisible( false );
			return;
		}

		SetVisible( true );

		if ( ( Game.IsPlaying && !InLocalView() ) || sinceFrame < 1f / RefreshRate )
			return;

		sinceFrame = 0;

		DrawFrom( eye.Value );
	}

	/// <summary>
	/// The eye an editor's mirror draws for: whatever camera is looking at the scene, or - a prefab tab has none, and
	/// its camera comes and goes - a head forty inches in front of the glass, where a driver's would be.
	/// </summary>
	Vector3? AdjustingEye()
	{
		if ( Scene.Camera is { } camera )
			return camera.WorldPosition;

		var pane = EnsureGlass();

		return pane.IsValid() ? pane.WorldPosition + pane.WorldRotation.Up.Normal * 40f : WorldPosition;
	}

	/// <summary>The local player, when they are the one sitting in this mirror's car.</summary>
	PlayerController LocalDriver()
	{
		var occupant = seat.IsValid() ? seat.GetOccupant() : null;

		return occupant.IsValid() && occupant == Players.LocalController ? occupant : null;
	}

	/// <summary>The mirror itself is somewhere on the local player's own screen right now.</summary>
	bool InLocalView()
	{
		if ( Scene.Camera is not { } camera )
			return false;

		var screenPos = camera.PointToScreenNormal( WorldPosition, out var isBehind );
		const float margin = 0.12f;

		return !isBehind && screenPos.x is >= -margin and <= 1f + margin && screenPos.y is >= -margin and <= 1f + margin;
	}

	void SetVisible( bool visible )
	{
		var pane = EnsureGlass();

		if ( !pane.IsValid() )
			return;

		if ( pane.Enabled != visible )
			pane.Enabled = visible;

		// The feed goes on the pane's own scene object, not on its material: a material made in code carries no
		// texture slot of its own, so an attribute set on it never reaches the shader.
		if ( visible )
		{
			pane.SceneObject.Attributes.Set( "Feed", EnsureFeed() );

			// Being lined up by hand, so the glass is tinted and its edges are easy to see.
			pane.SceneObject.Attributes.Set( "Adjusting", Game.IsPlaying ? 0f : 1f );
		}
	}

	Texture EnsureFeed() => feed ??= Texture.CreateRenderTarget()
		.WithSize( TextureSize.x, TextureSize.y )
		.WithFormat( ImageFormat.RGBA16161616F )
		.WithInitialColor( Color.Black )
		.Create();

	/// <summary>
	/// Draw one picture: what the driver's own sight would find if the glass were a perfect mirror, seen from the
	/// glass itself so that neither the housing nor the modelled glass stands in the way.
	/// </summary>
	void DrawFrom( Vector3 eye )
	{
		var pane = EnsureGlass();
		var camera = EnsureLens();

		if ( !pane.IsValid() || !camera.IsValid() )
			return;

		var centre = pane.WorldPosition;
		var normal = pane.WorldRotation.Up.Normal;

		// Stand just in front of the glass, on the driver's side of it, looking along the line the driver's own
		// sight takes when it bounces off the glass. That ray leaves the mirror at the same angle it arrived, so
		// the picture is the mirror's, and nothing of the housing or the modelled glass is between camera and view.
		var sight = ( centre - eye ).Normal;
		var bounced = ( sight - 2f * Vector3.Dot( sight, normal ) * normal ).Normal;

		// Turned away from the car's flank: a mirror set from the driver's seat frames the road behind, not the
		// rear quarter panel.
		var outward = -MathF.Sign( normal.y ) * AimOutward;
		bounced = Rotation.FromYaw( outward ) * bounced;

		// The two placements are the inspector's to move, separately: the pane by GlassProud, GlassShift and
		// GlassAngles, the camera by LensBack, LensShift and LensAngles, each on top of the geometry above.
		var paneRotation = pane.WorldRotation;
		camera.WorldPosition = centre + paneRotation * new Vector3( LensShift.y, LensShift.x, LensBack );
		camera.WorldRotation = Rotation.From( LensAngles ) * Rotation.LookAt( bounced, Vector3.Up );

		// A mirror shows exactly as much of that space as its glass covers from the driver's eye.
		var distance = MathF.Max( ( centre - eye ).Length, 1f );
		var covered = MathF.Atan2( GlassSize.x * 0.5f, distance ) * 2f * ( 180f / MathF.PI );

		camera.FovAxis = CameraComponent.Axis.Horizontal;
		camera.FieldOfView = Math.Clamp( covered, 2f, MaxFieldOfView );

		camera.RenderToTexture( EnsureFeed() );
	}

	/// <summary>
	/// The quad standing in for the glass: this object's position, a face towards <see cref="GlassNormal"/>, and the
	/// feed's material. Made once, as a child that isn't saved into the prefab and is never networked.
	/// </summary>
	ModelRenderer EnsureGlass()
	{
		if ( retired )
			return null;

		if ( glass.IsValid() )
			return glass;

		var plane = Model.Load( "models/dev/plane.vmdl" );

		if ( plane is null || plane.IsError )
			return null;

		var go = new GameObject( GameObject, true, "Mirror Glass" );
		go.Flags |= GameObjectFlags.NotSaved;
		go.NetworkMode = NetworkMode.Never;
		go.Tags.Add( GlassTag );
		go.LocalPosition = GlassNormal.Normal * GlassProud;
		go.LocalRotation = GlassRotation();
		go.LocalScale = new Vector3( GlassSize.x, GlassSize.y, 1f ) / plane.Bounds.Size.x;

		glass = go.Components.Create<ModelRenderer>();
		glass.Model = plane;
		glass.MaterialOverride = FeedMaterial();

		// Drawn with the scene, depth and all: the housing's rim has to be able to cut the picture off, the way a
		// real mirror's bezel does.
		glass.RenderOptions.Game = true;
		glass.RenderOptions.AfterUI = false;

		return glass;
	}

	/// <summary>Where the quad sits on the glass: proud of it, then slid within its plane.</summary>
	Vector3 GlassLocalPosition()
	{
		var normal = GlassNormal.Normal;
		var across = Vector3.Up.Cross( normal ).Normal;

		return normal * GlassProud + Vector3.Up * GlassShift.x + across * GlassShift.y;
	}

	/// <summary>Turn this object's face so its own +Z - the plane's front - points along the glass normal.</summary>
	Rotation GlassRotation()
	{
		var normal = GlassNormal.Normal;

		return Rotation.LookAt( Vector3.Up.Cross( normal ).Normal, normal );
	}

	Material FeedMaterial()
	{
		if ( material.IsValid() )
			return material;

		material = Material.FromShader( "shaders/vehicle_mirror.shader" )?.CreateCopy();

		return material;
	}

	/// <summary>
	/// The camera the feed is drawn through. Made once, as a child that doesn't get saved into the prefab.
	/// </summary>
	CameraComponent EnsureLens()
	{
		if ( lens.IsValid() )
			return lens;

		var go = new GameObject( GameObject, true, "Mirror Lens" );
		go.Flags |= GameObjectFlags.NotSaved;
		go.NetworkMode = NetworkMode.Never;

		lens = go.Components.Create<CameraComponent>();
		lens.IsMainCamera = false;
		lens.Enabled = false;
		lens.ClearFlags = ClearFlags.All;
		lens.BackgroundColor = Color.Black;
		lens.ZNear = 1f;
		lens.ZFar = 20000f;

		// The held weapon and the arms in front of the player's own camera have no business in a mirror, and neither
		// does the mirror's own glass.
		lens.RenderExcludeTags.Add( "viewmodel" );
		lens.RenderExcludeTags.Add( GlassTag );

		return lens;
	}

	protected override void OnDisabled() => SetVisible( false );

	protected override void OnDestroy()
	{
		material = null;

		feed?.Dispose();
		feed = null;
	}

	/// <summary>Show where the glass is and which way it faces, for placing it.</summary>
	protected override void DrawGizmos()
	{
		var rotation = WorldRotation * GlassRotation();
		var across = rotation.Right * GlassSize.x * 0.5f;
		var up = rotation.Up * GlassSize.y * 0.5f;

		var corners = new[]
		{
			WorldPosition - across - up,
			WorldPosition + across - up,
			WorldPosition + across + up,
			WorldPosition - across + up,
		};

		Gizmo.Draw.Color = Color.Cyan;

		for ( var i = 0; i < corners.Length; i++ )
		{
			Gizmo.Draw.Line( corners[i], corners[( i + 1 ) % corners.Length] );
			Gizmo.Draw.Line( WorldPosition, WorldPosition + rotation.Up * 6f );
		}
	}
}
