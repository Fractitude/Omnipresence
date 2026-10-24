namespace Sandbox;

/// <summary>
/// Feeds <see cref="StatsLog"/> the joins and leaves and writes the log to disk every few seconds. A
/// <see cref="GameObjectSystem"/>, so the engine makes it in every scene and nothing needs to carry it.
/// </summary>
public sealed class StatsSystem : GameObjectSystem<StatsSystem>, Component.INetworkListener
{
	/// <summary>Seconds between writes.</summary>
	const float FlushInterval = 5f;

	TimeUntil nextFlush;

	public StatsSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.FinishUpdate, 0, Tick, "StatsSystem.Tick" );
	}

	void Tick()
	{
		if ( !Networking.IsHost || !nextFlush )
			return;

		nextFlush = FlushInterval;
		StatsLog.Flush();
	}

	void Component.INetworkListener.OnActive( Connection channel ) => StatsLog.PlayerJoined( channel );

	void Component.INetworkListener.OnDisconnected( Connection channel )
	{
		StatsLog.PlayerLeft( channel );
		StatsLog.Flush();
	}
}
