namespace Sandbox;

/// <summary>
/// Restores the dropped items of the scene on the host when it starts (<see cref="WorldItemRegistry.Restore"/>), writes
/// the world file a few seconds after the last change, and writes it once more when the scene goes. Made in every scene
/// by the engine.
/// </summary>
/// <remarks>
/// Host only. A <c>new Scene()</c> starts every system, so a system that isn't in the active scene does nothing. Its state
/// is in instance fields, which a hot reload keeps; a restore is by id, so it could not duplicate anything anyway.
/// </remarks>
public sealed class WorldItemSystem : GameObjectSystem<WorldItemSystem>
{
	/// <summary>Seconds after the game starts before the items are made, so the map is up.</summary>
	const float RestoreDelay = 1f;

	bool restored;
	RealTimeSince sinceStart;
	RealTimeSince sinceTick;

	public WorldItemSystem( Scene scene ) : base( scene )
	{
		sinceStart = 0;
		Listen( Stage.StartUpdate, 1, Tick, "WorldItemSystem.Tick" );
	}

	void Tick()
	{
		if ( Scene != Game.ActiveScene || !Game.IsPlaying || Scene.IsEditor || Networking.IsActive && !Networking.IsHost )
			return;

		if ( !restored )
		{
			if ( sinceStart < RestoreDelay )
				return;

			restored = true;
			var made = WorldItemRegistry.Restore( Scene );

			if ( made > 0 )
				Log.Info( $"World items: restored {made}" );

			return;
		}

		if ( sinceTick < 0.5f )
			return;

		sinceTick = 0;
		WorldItemRegistry.Tick();
	}

	public override void Dispose()
	{
		base.Dispose();

		// The scene is going: write what is waiting. Only for a system that worked (not a preview scene's).
		if ( restored )
			WorldItemRegistry.Flush();
	}
}
