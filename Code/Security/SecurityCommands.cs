namespace Sandbox;

public static class SecurityCommands
{
	const string CameraPrefab = "security/camera.prefab";

	/// <summary>
	/// Puts a security camera where you stand, at eye height, looking where you look.
	/// </summary>
	[ConCmd( "spawn_security_camera" )]
	public static void SpawnCamera()
	{
		SpawnCameraOnHost();
	}

	[Rpc.Host]
	static void SpawnCameraOnHost()
	{
		var caller = Rpc.Caller;
		var player = Game.ActiveScene?.GetAllComponents<PlayerController>()
			.FirstOrDefault( x => x.Network.Owner == caller );

		if ( !player.IsValid() )
		{
			Log.Warning( "spawn_security_camera: no player" );
			return;
		}

		var at = player.WorldPosition + Vector3.Up * 60f;
		var camera = GameObject.Clone( CameraPrefab, new Transform( at, player.EyeAngles.ToRotation() ) );
		camera.Name = "Security Camera";
		camera.NetworkSpawn();
	}
}
