namespace Sandbox;

/// <summary>
/// Aims the player's world panel clicks from their first person eye, whatever the camera does. A
/// <see cref="WorldInput"/> on the camera casts from the camera, so in third person the camera's distance to a screen
/// decided whether it could be used. This moves the input onto a stand-in at the eye, so range is measured from where a first person camera would
/// be. With the mouse free (seated at a computer) the input still uses the cursor ray from the camera.
/// </summary>
public sealed class FirstPersonWorldInput : GameObjectSystem<FirstPersonWorldInput>
{
	GameObject eye;

	public FirstPersonWorldInput( Scene scene ) : base( scene )
	{
		Listen( Stage.FinishUpdate, 0, Update, "FirstPersonWorldInput" );
	}

	void Update()
	{
		var controller = Scene.GetAll<PlayerController>().FirstOrDefault( x => !x.IsProxy );
		if ( !controller.IsValid() )
			return;

		if ( !eye.IsValid() )
			Create();

		if ( !eye.IsValid() )
			return;

		eye.WorldPosition = controller.EyePosition;
		eye.WorldRotation = controller.EyeAngles.ToRotation();
	}

	/// <summary>
	/// Make the stand-in with the camera's input settings, and turn the camera's own input off so only one routes
	/// clicks.
	/// </summary>
	void Create()
	{
		if ( Scene.Camera?.GameObject.GetComponent<WorldInput>() is not { } cameraInput )
			return;

		eye = new GameObject( true, "First Person World Input" );
		eye.Flags |= GameObjectFlags.NotSaved;
		eye.NetworkMode = NetworkMode.Never;

		var input = eye.Components.Create<WorldInput>();
		input.LeftMouseAction = cameraInput.LeftMouseAction;
		input.RightMouseAction = cameraInput.RightMouseAction;
		input.VRHandSource = cameraInput.VRHandSource;

		cameraInput.Enabled = false;
	}
}
