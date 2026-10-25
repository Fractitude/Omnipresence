namespace Sandbox.Npcs;

public static class NpcCommands
{
	const string GunmanPrefab = "npcs/gunman.prefab";
	const string ScientistPrefab = "npcs/scientist.prefab";
	const string RollerPrefab = "npcs/rollermine.prefab";

	/// <summary>
	/// Spawns a hostile gunman a few steps in front of you.
	/// </summary>
	[ConCmd( "spawn_gunman" )]
	public static void SpawnGunman() => SpawnOnHost( GunmanPrefab, "Gunman", 600f );

	/// <summary>
	/// Spawns a scientist a few steps in front of you. Press Use on it to have it follow you.
	/// </summary>
	[ConCmd( "spawn_scientist" )]
	public static void SpawnScientist() => SpawnOnHost( ScientistPrefab, "Scientist", 200f );

	/// <summary>
	/// Spawns a rollermine a few steps in front of you.
	/// </summary>
	[ConCmd( "spawn_rollermine" )]
	public static void SpawnRollermine() => SpawnOnHost( RollerPrefab, "Rollermine", 400f );

	[Rpc.Host]
	static void SpawnOnHost( string prefab, string name, float distance )
	{
		var caller = Rpc.Caller;
		var player = Game.ActiveScene?.GetAllComponents<PlayerController>()
			.FirstOrDefault( x => x.Network.Owner == caller );

		if ( !player.IsValid() )
		{
			Log.Warning( $"spawn {name}: no player" );
			return;
		}

		var forward = player.EyeAngles.WithPitch( 0 ).Forward;
		var at = player.WorldPosition + forward * distance;
		if ( player.Scene.NavMesh.GetClosestPoint( at ) is { } nav )
			at = nav;

		var npc = GameObject.Clone( prefab, new Transform( at + Vector3.Up * 8f, Rotation.LookAt( -forward ) ) );
		npc.Name = name;
		npc.NetworkSpawn();
	}
}
