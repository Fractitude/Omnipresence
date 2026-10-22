namespace Sandbox;

/// <summary>
/// Runs the <see cref="WorldClock"/> on the host: loads the saved time once and saves it now and then. Clients follow
/// the time the host syncs on their own <see cref="PlayerProgress"/>.
/// </summary>
public sealed class WorldClockSystem : GameObjectSystem<WorldClockSystem>
{
	public WorldClockSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.StartUpdate, 0, Tick, "WorldClockSystem.Tick" );
	}

	void Tick()
	{
		if ( !Game.IsPlaying || Scene.IsEditor || !Networking.IsHost )
			return;

		WorldClock.TickHost();
	}
}
