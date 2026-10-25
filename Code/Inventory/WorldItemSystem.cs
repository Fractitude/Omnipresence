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
	/// <summary>How many saved things are made in one tick, so a long list does not hitch a frame. The first batch is made
	/// on the first tick the scene is ready.</summary>
	const int Batch = 50;

	bool restored;
	int total;
	int batches;
	float firstAt = -1f;
	readonly HashSet<string> skip = new();
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
			// Not a fixed wait: the first tick the scene is ready (its maps loaded, something to stand on).
			if ( !SceneReadiness.IsReady( Scene, sinceStart ) )
				return;

			var made = WorldItemRegistry.Restore( Scene, Batch, skip, out var attempted );
			total += made;
			batches++;

			if ( made > 0 && firstAt < 0f )
				firstAt = sinceStart;

			// Fewer tried than a batch holds: nothing is left.
			if ( attempted < Batch )
			{
				restored = true;

				if ( total > 0 )
					Log.Info(
						$"World items: restored {total} in {batches} tick(s)"
						+ $", the first {firstAt * 1000f:0} ms and the last {sinceStart * 1000f:0} ms after the scene started" );
			}

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
