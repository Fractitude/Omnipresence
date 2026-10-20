namespace Sandbox;

/// <summary>
/// Tags the body of the player this client controls with <see cref="Tag"/>, so a camera can leave it out. Navi's seat
/// puts the camera inside the player's head and excludes this tag, so the player doesn't see their own body, while
/// other players' cameras still draw it, and its shadow stays. It goes on the body (the controller's renderer), not the
/// controller, and is added in code so the player prefab stays as it is.
/// </summary>
public sealed class LocalPlayerTag : GameObjectSystem<LocalPlayerTag>
{
	public const string Tag = "local_player";

	public LocalPlayerTag( Scene scene ) : base( scene )
	{
		Listen( Stage.FinishUpdate, 0, Update, "LocalPlayerTag" );
	}

	void Update()
	{
		foreach ( var controller in Scene.GetAll<PlayerController>() )
		{
			if ( controller.Renderer?.GameObject is not { } body )
				continue;

			// Taken off the others, in case the tag came along with a networked copy of the object.
			if ( controller.IsProxy )
				body.Tags.Remove( Tag );
			else
				body.Tags.Add( Tag );
		}
	}
}
