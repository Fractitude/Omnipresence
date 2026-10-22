namespace Sandbox;

/// <summary>
/// Runs the <see cref="WorldClock"/> on the host: loads the saved time once and saves it now and then. Clients follow
/// the time the host syncs on their own <see cref="PlayerProgress"/>. On every client it is also the time Navi computers'
/// taskbar clocks show (<see cref="NaviClock"/>).
/// </summary>
public sealed class WorldClockSystem : GameObjectSystem<WorldClockSystem>
{
	public WorldClockSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.StartUpdate, 0, Tick, "WorldClockSystem.Tick" );
	}

	// What Navi's taskbar clock reads. Every client runs the world clock itself, so it needs nothing sent.
	readonly System.Func<System.TimeSpan> naviTime = () => System.TimeSpan.FromSeconds( WorldClock.Now );
	readonly System.Func<string> naviDate = () => WorldClock.Day;

	void Tick()
	{
		// Every frame, on every client: a hot reload of either side would leave the clock on a stale source.
		NaviClock.TimeOfDay = naviTime;
		NaviClock.Date = naviDate;

		if ( !Game.IsPlaying || Scene.IsEditor || !Networking.IsHost )
			return;

		WorldClock.TickHost();
	}
}
