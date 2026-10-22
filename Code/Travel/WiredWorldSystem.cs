namespace Sandbox;

/// <summary>
/// Makes the Wired look like a place of its own although it is in the same scene as the real world. On this client,
/// while the local player is in the Wired (<see cref="PlayerData.InWired"/>): the Wired's sky (a skybox on an object
/// tagged <see cref="WiredTag"/>) is on and the real world's is off, 2D or 3D, and the main camera leaves out everything
/// tagged <see cref="MapTag"/>, so the map under which the Wired lies is not drawn. At home it leaves out
/// <see cref="WiredTag"/> instead, so the Wired is not drawn. Nothing is networked: each client dresses its own view
/// for its own player.
/// </summary>
public sealed class WiredWorldSystem : GameObjectSystem<WiredWorldSystem>
{
	/// <summary>
	/// On the object of the Wired's skybox, which makes it the Wired's sky; any other sky is the real world's. The
	/// camera does not draw what has this tag while the player is at home.
	/// </summary>
	public const string WiredTag = "wired";

	/// <summary>
	/// The camera does not draw what has this tag while the player is in the Wired.
	/// </summary>
	public const string MapTag = "map";

	bool? applied;

	public WiredWorldSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.FinishUpdate, 0, Update, "WiredWorldSystem.Update" );
	}

	void Update()
	{
		// Not in the Character menu's preview scene either: it is a scene too, and the systems start in it.
		if ( !Game.IsPlaying || Scene.IsEditor || Application.IsHeadless || CharacterMenu.IsPreview( Scene ) )
			return;

		var inWired = WiredTravel.PlaceOf( Connection.Local ) == WiredPlace.Wired;

		if ( applied != inWired )
		{
			applied = inWired;
			ApplySkyboxes( inWired );
		}

		// Kept every frame: the camera can be replaced, and it costs a lookup.
		ApplyCamera( inWired );
	}

	void ApplySkyboxes( bool inWired )
	{
		// Found by component, not by a tag: the map makes its own objects (the 3D skybox, `skybox_reference`) each time it
		// loads, and a tag put on one in the editor is not saved, so it is gone at Play.
		foreach ( var sky in Scene.GetAllObjects( true ).SelectMany( x => x.Components.GetAll().Where( IsSky ) ) )
			sky.Enabled = sky.GameObject.Tags.Has( WiredTag ) == inWired;
	}

	static bool IsSky( Component component ) =>
		component.GetType().Name.Contains( "Skybox", StringComparison.OrdinalIgnoreCase );

	void ApplyCamera( bool inWired )
	{
		var camera = Scene.Camera;
		if ( !camera.IsValid() )
			return;

		SetExcluded( camera.RenderExcludeTags, MapTag, inWired );
		SetExcluded( camera.RenderExcludeTags, WiredTag, !inWired );
	}

	static void SetExcluded( TagSet tags, string tag, bool excluded )
	{
		if ( tags.Has( tag ) == excluded )
			return;

		if ( excluded )
			tags.Add( tag );
		else
			tags.Remove( tag );
	}
}
