namespace Sandbox;

public static class VehicleCommands
{
	private const string BuggyPrefab = "vehicles/buggy/buggy.prefab";

	/// <summary>
	/// Spawns a buggy in front of you.
	/// </summary>
	[ConCmd( "spawn_buggy" )]
	public static void SpawnBuggy()
	{
		SpawnBuggyOnHost();
	}

	[Rpc.Host]
	private static void SpawnBuggyOnHost()
	{
		var caller = Rpc.Caller;
		var player = Game.ActiveScene?.GetAllComponents<PlayerController>()
			.FirstOrDefault( x => x.Network.Owner == caller );
		if ( !player.IsValid() )
		{
			Log.Warning( "spawn_buggy: no player" );
			return;
		}

		var forward = player.EyeAngles.WithPitch( 0 ).ToRotation();
		var position = player.WorldPosition + forward.Forward * 200f + Vector3.Up * 40f;

		var buggy = GameObject.Clone( BuggyPrefab, new Transform( position, forward ) );
		buggy.NetworkSpawn();
	}
}
