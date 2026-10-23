namespace Sandbox;

/// <summary>
/// Console commands for testing the offline processing (<see cref="OfflineCrimeSystem"/>). Editor only, and only on the
/// host: they destroy and respawn the local player's object or read the processor, and send nothing to a client.
/// </summary>
public static class OfflineCommands
{
	static bool Allowed()
	{
		if ( Application.IsEditor && Networking.IsHost )
			return true;

		Log.Info( "Only in the editor, on the host" );
		return false;
	}

	/// <summary>
	/// <c>offline_away</c> takes your player object away without disconnecting, like a player who left or one who is
	/// dead: the processor carries on with your record. <c>offline_back</c> respawns you.
	/// </summary>
	[ConCmd( "offline_away" )]
	public static void Away()
	{
		if ( !Allowed() )
			return;

		if ( Players.Local<PlayerProgress>() is { } progress )
			progress.GameObject.Destroy();
		else
			Log.Info( "You are away already" );
	}

	[ConCmd( "offline_back" )]
	public static void Back()
	{
		if ( !Allowed() )
			return;

		if ( GameManager.Current?.Respawn( Connection.Local ) is null )
			Log.Info( "Couldn't respawn you" );
	}

	/// <summary>
	/// <c>offline_status</c> logs what the processor looks after and when it looks next.
	/// </summary>
	[ConCmd( "offline_status" )]
	public static void Status()
	{
		if ( !Allowed() )
			return;

		var cache = PlayerProgression.Cache;
		var processor = OfflineCrimeSystem.Current?.Processor;
		var now = WorldClock.Now;

		Log.Info( $"Offline: clock {now}, index {cache.PendingIds.Count}, tracked {processor?.Tracked ?? 0}" );

		foreach ( var id in cache.PendingIds )
		{
			var due = processor?.DueOf( id );
			var next = cache.Peek( id ) is { } record ? CrimeRules.NextDue( record ) : null;

			Log.Info( $"  {id}: attached {cache.IsAttached( id )}, in memory {cache.Peek( id ) is not null}, "
			          + $"looked at again {(due is null ? "never" : $"{due - now:+0;-0} s")}, "
			          + $"next event {(next is null ? "unknown" : $"{next - now:+0;-0} s")}" );
		}
	}

	/// <summary>
	/// <c>offline_rescan</c> looks at every saved record and rebuilds the index of players with work waiting.
	/// </summary>
	[ConCmd( "offline_rescan" )]
	public static async void Rescan()
	{
		if ( !Allowed() )
			return;

		try
		{
			await PlayerProgression.Cache.RescanAsync();
			Log.Info( $"Rescanned: {PlayerProgression.Cache.PendingIds.Count} with work waiting" );
		}
		catch ( System.Exception e )
		{
			Log.Warning( e, $"Couldn't rescan: {e.Message}" );
		}
	}
}
