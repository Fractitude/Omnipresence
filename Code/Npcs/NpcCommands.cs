namespace Sandbox.Npcs;

public static class NpcCommands
{
	const string CombatNpcPrefab = "npcs/combat_npc.prefab";

	/// <summary>
	/// Spawns a hostile gunman a few steps in front of you.
	/// </summary>
	[ConCmd( "spawn_combat_npc" )]
	public static void SpawnCombatNpc()
	{
		SpawnCombatNpcOnHost();
	}

	[Rpc.Host]
	static void SpawnCombatNpcOnHost()
	{
		var caller = Rpc.Caller;
		var player = Game.ActiveScene?.GetAllComponents<PlayerController>()
			.FirstOrDefault( x => x.Network.Owner == caller );

		if ( !player.IsValid() )
		{
			Log.Warning( "spawn_combat_npc: no player" );
			return;
		}

		var at = player.WorldPosition + player.EyeAngles.WithPitch( 0 ).Forward * 600f;
		if ( player.Scene.NavMesh.GetClosestPoint( at ) is { } nav )
			at = nav;

		var npc = GameObject.Clone( CombatNpcPrefab, new Transform( at, Rotation.LookAt( -player.EyeAngles.WithPitch( 0 ).Forward ) ) );
		npc.Name = "Gunman";
		npc.NetworkSpawn();
	}
}
