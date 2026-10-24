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
/// </remarks>
[Title( "Security Camera" ), Icon( "videocam" )]
public sealed class SecurityCamera : Component
{
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

	/// <summary>The shortest gap between two pictures, in seconds.</summary>
	const float FrameInterval = 1f / FeedRate;

	/// <summary>The camera the picture is drawn through, kept from the first frame it is needed.</summary>
	CameraComponent lens;

	Texture feed;

	RealTimeSince sinceAsked = float.MaxValue;
	RealTimeSince sinceFrame = float.MaxValue;

	/// <summary>What the app lists and what the picture's label says.</summary>
	public string DisplayName => string.IsNullOrWhiteSpace( Label ) ? GameObject.Name : Label;

	/// <summary>
	/// Can a computer at <paramref name="point"/> connect to this camera?
	/// </summary>
	public bool Reaches( Vector3 point ) => WorldPosition.Distance( point ) <= SignalRange;

	/// <summary>
	/// The live picture of this camera. The same texture comes back on every ask: the camera draws into it while it is
	/// asked for, so a panel that holds it sees the feed move without rebuilding.
	/// </summary>
	public Texture RequestFeed()
	{
		sinceAsked = 0;

		return feed ??= Texture.CreateRenderTarget().WithSize( FeedWidth, FeedHeight ).Create();
	}

	/// <summary>
	/// Draw a picture if an app asked for one recently enough and the last one is a frame old. Not every frame: a feed
	/// at twelve pictures a second reads as live and costs a twelfth as much as the screen's own camera.
	/// </summary>
	protected override void OnPreRender()
	{
		if ( !feed.IsValid() || sinceAsked > FeedHold || sinceFrame < FrameInterval )
			return;

		if ( EnsureLens() is not { } camera )
			return;

		sinceFrame = 0;
		camera.FieldOfView = FieldOfView;
		camera.RenderToTexture( feed );
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

	protected override void OnDestroy()
	{
		feed?.Dispose();
		feed = null;
	}

	/// <summary>
	/// Show where the camera looks, so it can be aimed while placing it: the edges of its view, forward along its x.
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

		Gizmo.Draw.Color = Color.Cyan;

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
