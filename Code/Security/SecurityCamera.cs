namespace Sandbox;

/// <summary>
/// A camera that watches a place in the world and gives a live picture of what it sees to the Security System app on a
/// nearby computer. It draws only while an app asks for its picture, and stops a second after the last ask, so a camera
/// nobody watches costs nothing.
/// </summary>
/// <remarks>
/// The picture never crosses the network. Each client draws its own, from its own copy of the world and its own copy of
/// this camera, so nothing about a camera has to be synced to be watched, and every player near a screen sees the same
/// world in it. What the app needs is the camera's place: it connects to the cameras within
/// <see cref="SignalRange"/> of the computer.
///
/// A camera is out of action when it is <see cref="Destroyed"/> or when it has been unplugged
/// (<see cref="PluggedIn"/>). Either way it stops looking at the world and gives a television's static instead, made
/// here rather than in the world, and its LED goes out. Only a destroyed one throws sparks and crackles: one that is
/// merely unplugged is off, and the player can plug it back in. A destroyed one is fixed with the Repair option.
/// </remarks>
[Title( "Security Camera" ), Icon( "videocam" )]
public sealed class SecurityCamera : Component, Component.IDamageable, IInteractionHandler
{
	/// <summary>The id of the option that plugs the camera's power in and out.</summary>
	const string PowerOption = "power";

	/// <summary>The id of the option that fixes a destroyed camera.</summary>
	const string RepairOption = "repair";

	/// <summary>
	/// What the Security System calls this camera. Empty uses the object's name.
	/// </summary>
	[Property] public string Label { get; set; } = "";

	/// <summary>
	/// How far from this camera a computer can be and still connect to it, in units (1 is an inch): 1500 is about 38 m.
	/// </summary>
	[Property, Range( 0f, 5000f )]
	public float SignalRange { get; set; } = 1500f;

	/// <summary>
	/// How wide the view is, in degrees.
	/// </summary>
	[Property, Range( 20f, 120f )]
	public float FieldOfView { get; set; } = 70f;

	/// <summary>
	/// The camera has been shot to pieces: no picture, sparks and a crackle, and a Repair option to fix it.
	/// </summary>
	[Property, Sync] public bool Destroyed { get; set; }

	/// <summary>
	/// Whether the camera has power. Pulled (by the player, with the Unplug option) it shows the same static as a
	/// destroyed one, but nothing is broken: no sparks, no crackle, and plugging it back in is all it takes.
	/// </summary>
	[Property, Sync] public bool PluggedIn { get; set; } = true;

	/// <summary>
	/// The camera is out of action, whether it was destroyed or unplugged. What the app and the picture care about: it
	/// gives static instead of a view, and NO SIGNAL where LIVE was.
	/// </summary>
	public bool Offline => Destroyed || !PluggedIn;

	/// <summary>
	/// How much damage the camera takes before it is <see cref="Destroyed"/>. A couple of bullets is enough.
	/// </summary>
	[Property, Range( 1f, 500f )]
	public float Health { get; set; } = 20f;

	/// <summary>
	/// The size every picture is drawn at, in pixels: 16 by 9. Every screen scales it, the grid's cells and the enlarged
	/// one alike, so one camera never has to hold two pictures.
	/// </summary>
	const int FeedWidth = 640, FeedHeight = 360;

	/// <summary>
	/// How long the camera keeps drawing after the last time an app asked for its picture, in seconds.
	/// </summary>
	const float FeedHold = 1f;

	/// <summary>
	/// How many pictures the camera draws a second while it is watched. Enough to look live, and cheap enough for a
	/// screen of nine of them.
	/// </summary>
	const float FeedRate = 12f;

	/// <summary>The shortest gap between two live pictures, in seconds.</summary>
	const float FrameInterval = 1f / FeedRate;

	/// <summary>
	/// The size the static is made at, in pixels. Half the feed's, so a new pattern on every frame of a 60 Hz screen costs
	/// little; the screen scales it up, which is what a broken signal looks like anyway.
	/// </summary>
	const int StaticWidth = 320, StaticHeight = 180;

	/// <summary>
	/// How many static pictures a second: one for every frame the screen draws, so it flickers instead of crawling.
	/// </summary>
	const float StaticRate = 60f;

	/// <summary>The shortest gap between two static pictures, in seconds.</summary>
	const float StaticInterval = 1f / StaticRate;

	/// <summary>How tall the brighter band rolling down the static is, in rows.</summary>
	const int BandHeight = 9;

	/// <summary>How many rows the band moves down with each picture.</summary>
	const float BandSpeed = 3f;

	/// <summary>The camera the picture is drawn through, kept from the first frame it is needed.</summary>
	CameraComponent lens;

	/// <summary>The LED on the front, so it can be put out when the camera is off.</summary>
	ModelRenderer led;

	/// <summary>The sparks that come off a destroyed camera.</summary>
	SparkEmitter sparks;

	Texture feed;
	Texture staticFeed;
	Color32[] staticPixels;

	/// <summary>How far down the static the brighter band has rolled.</summary>
	float bandRow;

	/// <summary>Whether the LED was last drawn lit, so it is only touched when that changes. Null until found.</summary>
	bool? ledLit;

	/// <summary>The damage the host has seen, which destroys the camera when it passes its health.</summary>
	float damageTaken;

	RealTimeSince sinceAsked = float.MaxValue;
	RealTimeSince sinceFrame = float.MaxValue;

	/// <summary>What the app lists and what the picture's label says.</summary>
	public string DisplayName => string.IsNullOrWhiteSpace( Label ) ? GameObject.Name : Label;

	/// <summary>
	/// Can a computer at <paramref name="point"/> connect to this camera?
	/// </summary>
	public bool Reaches( Vector3 point ) => WorldPosition.Distance( point ) <= SignalRange;

	/// <summary>
	/// Take the lens off the scene's own rendering the moment the component comes on. An enabled camera with no render
	/// target draws the player's screen, so a camera around the house took the view away from the main camera — in
	/// whatever order the scene happened to list them, which read as spectating one of them at random.
	/// </summary>
	protected override void OnEnabled()
	{
		EnsureLens();
	}

	/// <summary>
	/// Give the camera the two options a player uses on it: unplug it, and repair it once it is broken. Every machine does
	/// this the same way, so the host and the clients number the options alike.
	/// </summary>
	protected override void OnStart()
	{
		if ( !Game.IsPlaying )
			return;

		_ = Components.Get<Interactable>( FindMode.EnabledInSelfAndDescendants )
			?? GameObject.Components.Create<Interactable>();

		InteractionSetup.EnsureOption( this, PowerOption, "Unplug", "power", "Cut the picture." );
		InteractionSetup.EnsureOption( this, RepairOption, "Repair", "build", "Fix what the bullets broke." );
	}

	/// <summary>
	/// The picture of this camera for an app that shows it: its view while it works, static once it is offline. The same
	/// texture comes back on every ask, so a panel that holds it sees the picture move without rebuilding.
	/// </summary>
	public Texture RequestFeed()
	{
		sinceAsked = 0;

		return Offline ? StaticPicture() : LiveFeed();
	}

	/// <summary>
	/// Draw the next picture if an app asked for one recently enough and the last one is a frame old. A working camera
	/// draws its view twelve times a second, which reads as live and costs a twelfth of the screen's own camera; an
	/// offline one draws static at the screen's own rate.
	/// </summary>
	protected override void OnPreRender()
	{
		if ( sinceAsked > FeedHold || sinceFrame < (Offline ? StaticInterval : FrameInterval) )
			return;

		sinceFrame = 0;

		// A camera that is offline doesn't look at the world at all.
		if ( Offline )
		{
			DrawStatic();

			return;
		}

		if ( !feed.IsValid() || EnsureLens() is not { } camera )
			return;

		camera.FieldOfView = FieldOfView;
		camera.RenderToTexture( feed );
	}

	/// <summary>
	/// Keep the LED on the front saying whether the camera works, and the sparks coming off a destroyed one, on every
	/// machine: the host decides, and both are part of what a client sees.
	/// </summary>
	protected override void OnUpdate()
	{
		var electric = EnsureSparks();

		// Only damage throws sparks; a camera that is merely unplugged is off, not shorting.
		if ( electric.IsValid() && electric.Emitting != Destroyed )
			electric.Emitting = Destroyed;

		if ( !led.IsValid() )
			led = FindLed();

		if ( !led.IsValid() || ledLit == !Offline )
			return;

		// Red while it works, black when it is off. The LED stays where it is: a dead light, not a missing one.
		ledLit = !Offline;
		led.Tint = Offline ? Color.Black : new Color( 1f, 0f, 0f );
	}

	void Component.IDamageable.OnDamage( in DamageInfo damage )
	{
		if ( IsProxy || !Networking.IsHost || Destroyed || damage.Damage <= 0f )
			return;

		damageTaken += damage.Damage;

		if ( damageTaken >= Health )
			Destroyed = true;
	}

	/// <summary>
	/// What the player sees in the menu while looking at the camera: plug it in or pull it out, and repair it once it is
	/// broken (nothing to repair before that, so the row is hidden).
	/// </summary>
	InteractionInfo? IInteractionHandler.Describe( InteractionEvent e )
	{
		switch ( e.Option?.Id )
		{
			case PowerOption:
				return PluggedIn
					? new InteractionInfo( "Unplug", "power", "Cut the picture." )
					: new InteractionInfo( "Plug in", "power", "Give it power." );

			case RepairOption:
				return Destroyed
					? new InteractionInfo( "Repair", "build", "Fix what the bullets broke." )
					: new InteractionInfo( "" );

			default:
				return null;
		}
	}

	/// <summary>There is nothing to repair on a camera that still works.</summary>
	bool IInteractionHandler.CanInteract( InteractionEvent e ) =>
		e.Option?.Id != RepairOption || Destroyed;

	/// <summary>
	/// The player plugged the camera in or pulled it out, or repaired it. Runs on the host, which owns both flags.
	/// </summary>
	void IInteractionHandler.OnInteract( InteractionEvent e )
	{
		if ( IsProxy || !Networking.IsHost )
			return;

		switch ( e.Option?.Id )
		{
			case PowerOption:
				PluggedIn = !PluggedIn;
				break;

			case RepairOption:
				if ( !Destroyed )
					return;

				Destroyed = false;
				damageTaken = 0f;
				break;
		}
	}

	Texture LiveFeed() => feed ??= Texture.CreateRenderTarget().WithSize( FeedWidth, FeedHeight ).Create();

	/// <summary>
	/// The static an offline camera shows. Made the first time it is asked for, and drawn on before it is handed back, so
	/// the first picture is already snow rather than an empty texture.
	/// </summary>
	Texture StaticPicture()
	{
		if ( staticFeed.IsValid() )
			return staticFeed;

		staticFeed = Texture.CreateRenderTarget().WithSize( StaticWidth, StaticHeight ).Create();
		staticPixels = new Color32[StaticWidth * StaticHeight];

		DrawStatic();

		return staticFeed;
	}

	/// <summary>
	/// Make the next picture of static: grey snow, and a brighter band rolling down it, which is what reads as a lost
	/// signal rather than as plain noise.
	/// </summary>
	void DrawStatic()
	{
		if ( staticPixels is null || !staticFeed.IsValid() )
			return;

		var pixels = staticPixels.AsSpan();

		// Snow: every pixel its own grey, with nothing of the world left in it.
		for ( var i = 0; i < pixels.Length; i++ )
		{
			var value = (byte)System.Random.Shared.Next( 0, 256 );
			pixels[i] = new Color32( value, value, value, 255 );
		}

		var band = (int)bandRow % StaticHeight;

		for ( var y = band; y < band + BandHeight && y < StaticHeight; y++ )
		{
			for ( var x = 0; x < StaticWidth; x++ )
			{
				var i = y * StaticWidth + x;
				var value = (byte)Math.Min( 255, pixels[i].r + 90 );

				pixels[i] = new Color32( value, value, value, 255 );
			}
		}

		bandRow = (bandRow + BandSpeed) % StaticHeight;

		staticFeed.Update( pixels, 0, 0, StaticWidth, StaticHeight );
	}

	/// <summary>
	/// The camera the feed is drawn through: the one the prefab has, aimed out of the lens, or one made here for a camera
	/// a player put together without it. It is never the scene's main camera and never draws on its own.
	/// </summary>
	CameraComponent EnsureLens()
	{
		if ( lens.IsValid() )
			return lens;

		lens = GetComponentInChildren<CameraComponent>( true );

		if ( !lens.IsValid() )
		{
			var go = new GameObject( GameObject, true, "Lens Camera" );
			go.Flags |= GameObjectFlags.NotSaved;
			go.LocalTransform = new Transform( Vector3.Forward * 14f, Rotation.Identity );

			lens = go.Components.Create<CameraComponent>();
		}

		lens.IsMainCamera = false;
		lens.Enabled = false;

		// The held weapon and the arms in front of the player's own camera have no business in a security feed.
		if ( !lens.RenderExcludeTags.Has( "viewmodel" ) )
			lens.RenderExcludeTags.Add( "viewmodel" );

		return lens;
	}

	/// <summary>The object's LED, if the prefab has one.</summary>
	ModelRenderer FindLed() => GameObject.GetAllObjects( true )
		.Where( x => x.Name == "Led" )
		.Select( x => x.Components.Get<ModelRenderer>() )
		.FirstOrDefault( x => x.IsValid() );

	/// <summary>
	/// The sparks a destroyed camera gives off: the emitter the prefab has, or one made here for a camera a player put
	/// together by hand. It starts off, and <see cref="Destroyed"/> turns it on.
	/// </summary>
	SparkEmitter EnsureSparks()
	{
		if ( sparks.IsValid() )
			return sparks;

		sparks = GetComponentInChildren<SparkEmitter>( true );

		if ( !sparks.IsValid() )
		{
			var shorting = new GameObject( GameObject, true, "Sparks" );
			shorting.Flags |= GameObjectFlags.NotSaved;
			shorting.LocalPosition = Vector3.Up * 6f;

			sparks = shorting.Components.Create<SparkEmitter>();
			sparks.Emitting = false;
		}

		return sparks;
	}

	protected override void OnDestroy()
	{
		feed?.Dispose();
		feed = null;

		staticFeed?.Dispose();
		staticFeed = null;
	}

	/// <summary>
	/// Show where the camera looks, so it can be aimed while placing it: the edges of its view, forward along its x. Red
	/// for one that is offline, so a dead camera in the scene is obvious.
	/// </summary>
	protected override void DrawGizmos()
	{
		const float reach = 160f;

		var half = reach * MathF.Tan( FieldOfView * 0.5f * MathF.PI / 180f );
		var corners = new[]
		{
			new Vector3( reach, half, half ),
			new Vector3( reach, -half, half ),
			new Vector3( reach, -half, -half ),
			new Vector3( reach, half, -half ),
		};

		Gizmo.Draw.Color = Offline ? Color.Red : Color.Cyan;

		for ( var i = 0; i < corners.Length; i++ )
		{
			Gizmo.Draw.Line( Vector3.Zero, corners[i] );
			Gizmo.Draw.Line( corners[i], corners[(i + 1) % corners.Length] );
		}
	}

	/// <summary>
	/// Every camera a computer at <paramref name="point"/> can connect to, nearest first.
	/// </summary>
	public static IEnumerable<SecurityCamera> Near( Scene scene, Vector3 point )
	{
		if ( scene is null )
			yield break;

		foreach ( var camera in scene.GetAllComponents<SecurityCamera>()
			.Where( x => x.IsValid() && x.Enabled && x.GameObject.Enabled && x.Reaches( point ) )
			.OrderBy( x => x.WorldPosition.Distance( point ) ) )
		{
			yield return camera;
		}
	}
}
