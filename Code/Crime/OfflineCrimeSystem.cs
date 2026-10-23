namespace Sandbox;

/// <summary>
/// Runs the <see cref="OfflineCrimeProcessor"/> on the host, so orders, parcels, deliveries and cash-outs of players
/// who are away (or who the server has not seen since it started) keep going on the game clock. Made in every scene by
/// the engine. Also saves everything it holds when the scene goes, so a clean stop never loses a change.
/// </summary>
/// <remarks>
/// Host only. Its state is in instance fields, which a hot reload keeps (see AGENTS.md, "Static state and hot reload").
/// A <c>new Scene()</c> starts every system, so a system that isn't in the active scene does nothing.
/// </remarks>
public sealed class OfflineCrimeSystem : GameObjectSystem<OfflineCrimeSystem>
{
	/// <summary>
	/// Real seconds between two tries to start after a failure to read the index.
	/// </summary>
	const float RetryStart = 30f;

	OfflineCrimeProcessor processor;
	bool started;
	bool starting;
	RealTimeSince sinceStart;
	RealTimeSince sinceTick;

	public OfflineCrimeSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.StartUpdate, 1, Tick, "OfflineCrimeSystem.Tick" );
	}

	/// <summary>
	/// The processor, once the system has started. For the dev commands.
	/// </summary>
	public OfflineCrimeProcessor Processor => processor;

	void Tick()
	{
		if ( Scene != Game.ActiveScene || !Game.IsPlaying || Scene.IsEditor || !Networking.IsHost
		     || !WorldClock.IsLoaded )
			return;

		if ( processor is null )
		{
			processor = new( PlayerProgression.Cache, () => WorldClock.Now );
			processor.Processed += OnProcessed;
			sinceStart = RetryStart;
		}

		if ( !started )
		{
			if ( !starting && sinceStart >= RetryStart )
				Start();

			return;
		}

		if ( sinceTick < 1 )
			return;

		sinceTick = 0;
		processor.Tick();
	}

	async void Start()
	{
		starting = true;
		sinceStart = 0;

		try
		{
			await processor.StartAsync();
			started = true;
		}
		catch ( System.Exception e )
		{
			Log.Warning( e, $"OfflineCrimeSystem: couldn't start: {e.Message}" );
		}
		finally
		{
			starting = false;
		}
	}

	/// <summary>
	/// A record of a player who is away was brought up to date: keep the flag of their mailbox right, since no player
	/// object does it for them.
	/// </summary>
	void OnProcessed( ulong steamId, ProgressRecord record )
	{
		if ( string.IsNullOrEmpty( record.Crime.MailboxId ) )
			return;

		var box = Scene.GetAll<MailBox>().FirstOrDefault( x => x.BoxId == record.Crime.MailboxId );

		if ( !box.IsValid() || box.OwnerId != steamId )
			return;

		box.HasMail = PostalSystem.HasWaiting( record.Crime );
	}

	public override void Dispose()
	{
		base.Dispose();

		// The scene is going: write what is in memory, so nothing waits for the next save. Only for a system that
		// worked (not a preview scene's).
		if ( processor is not null && Networking.IsHost )
			PlayerProgression.Cache.SaveAllNow();
	}
}
