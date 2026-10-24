namespace Sandbox;

/// <summary>
/// Delivers the shares that wait for members whose record was not in memory when a payout was made
/// (<see cref="OrgRegistry.FlushPendingAsync"/>), every few seconds, so an absent member is paid soon after. Made in
/// every scene by the engine; host only.
/// </summary>
public sealed class OrgSystem : GameObjectSystem<OrgSystem>
{
	RealTimeSince sinceFlush;

	public OrgSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.StartUpdate, 1, Tick, "OrgSystem.Tick" );
	}

	void Tick()
	{
		if ( Scene != Game.ActiveScene || !Game.IsPlaying || Scene.IsEditor || !Networking.IsHost || sinceFlush < 5 )
			return;

		sinceFlush = 0;

		if ( OrgRegistry.Pending.Count > 0 )
			_ = OrgRegistry.FlushPendingAsync();
	}
}
