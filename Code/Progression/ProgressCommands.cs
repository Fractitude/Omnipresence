namespace Sandbox;

/// <summary>
/// Console commands for testing progression and the world clock. They only ask: the host decides (see
/// <see cref="PlayerProgress.DebugGrant"/>, <see cref="PlayerWallet.DebugGrant"/>) and refuse outside the editor.
/// </summary>
public static class ProgressCommands
{
	[ConCmd( "progress_give_xp" )]
	public static void GiveXp( long amount ) => Grant( amount, 0 );

	[ConCmd( "progress_give_hack_xp" )]
	public static void GiveHackXp( long amount ) => Grant( 0, amount );

	[ConCmd( "progress_give_money" )]
	public static void GiveMoney( long amount ) => GrantWallet( amount, 0, 0 );

	[ConCmd( "progress_give_credits" )]
	public static void GiveCredits( long amount ) => GrantWallet( 0, amount, 0 );

	[ConCmd( "progress_give_crypto" )]
	public static void GiveCrypto( long amount ) => GrantWallet( 0, 0, amount );

	[ConCmd( "progress_show" )]
	public static void Show()
	{
		if ( PlayerProgress.Local is not { Loaded: true } progress )
		{
			Log.Info( "No player progress yet" );
			return;
		}

		var wallet = PlayerWallet.Local;

		Log.Info( $"Level {progress.Level}, {progress.Xp} XP, hacking level {progress.HackLevel}, {progress.HackXp} hacking XP, "
		          + $"${wallet?.Cash}, {wallet?.Credits} credits, {wallet?.Crypto} crypto" );
	}

	/// <summary>
	/// <c>progress_test_data on</c> keeps progress in <c>progress_test/</c>, the clock in <c>world_test.json</c> and
	/// the mailbox claims in <c>mailboxes_test.json</c>, the storage claims in <c>storage_test.json</c> and the dropped
	/// items in <c>world_items_test.json</c> and the organizations in <c>orgs_test.json</c>, so a test never
	/// touches the real save; <c>off</c> goes back.
	/// Editor only, and only before a player has spawned.
	/// </summary>
	[ConCmd( "progress_test_data" )]
	public static void TestData( string mode = "on" )
	{
		if ( !Application.IsEditor || !Networking.IsHost || Players.Local<PlayerProgress>() is not null )
		{
			Log.Info( "Only in the editor, before the player has spawned" );
			return;
		}

		var test = mode != "off";

		PlayerProgression.UseStore( new FileProgressStore( test ? "progress_test" : FileProgressStore.DefaultFolder ) );
		WorldClock.UseFile( test ? "world_test.json" : "world.json" );
		MailboxRegistry.UseFile( test ? "mailboxes_test.json" : "mailboxes.json" );
		StorageRegistry.UseFile( test ? "storage_test.json" : "storage.json" );
		WorldItemRegistry.UseFile( test ? "world_items_test.json" : "world_items.json" );
		OrgRegistry.UseFile( test ? "orgs_test.json" : "orgs.json" );
		StatsLog.UseFolder( test ? "stats_test" : StatsLog.DefaultFolder );
		TransactionLedger.UseFolder( test ? "transactions_test" : TransactionLedger.DefaultFolder );
		PlacedObjectRegistry.UseFile( test ? "placed_objects_test.json" : "placed_objects.json" );
		Log.Info( test ? "Test data: progress_test/ and world_test.json" : "Real data: progress/ and world.json" );
	}

	[ConCmd( "world_time_show" )]
	public static void ShowTime() => Log.Info( $"{WorldClock.TimeOfDay}, {WorldClock.Day}" );

	/// <summary>
	/// <c>world_time_set 09:30</c> keeps the day; <c>world_time_set 09:30 3</c> goes to day 3.
	/// </summary>
	[ConCmd( "world_time_set" )]
	public static void SetTime( string time, int day = 0 )
	{
		var parts = time.Split( ':' );
		if ( parts.Length != 2 || !int.TryParse( parts[0], out var hours ) || !int.TryParse( parts[1], out var minutes )
		     || hours is < 0 or > 23 || minutes is < 0 or > 59 )
		{
			Log.Info( "Use a 24 hour time, like world_time_set 09:30" );
			return;
		}

		var days = day > 0 ? day - 1 : WorldClock.Now / 86400;
		ShiftTime( days * 86400 + hours * 3600 + minutes * 60, false );
	}

	/// <summary>
	/// Skip forward by days, <c>world_time_skip_day</c> for one. Negative goes back.
	/// </summary>
	[ConCmd( "world_time_skip_day" )]
	public static void SkipDays( int days = 1 ) => ShiftTime( days * 86400L, true );

	[ConCmd( "world_time_skip_hours" )]
	public static void SkipHours( float hours ) => ShiftTime( (long)(hours * 3600), true );

	[ConCmd( "world_time_skip_minutes" )]
	public static void SkipMinutes( float minutes ) => ShiftTime( (long)(minutes * 60), true );

	static void ShiftTime( long seconds, bool relative )
	{
		// The host in the editor moves the clock itself, so it works without a player (a test of the offline
		// processing).
		if ( Application.IsEditor && Networking.IsHost )
		{
			WorldClock.Set( relative ? WorldClock.Now + seconds : seconds );
			return;
		}

		if ( PlayerProgress.Local is not { Loaded: true } progress )
		{
			Log.Info( "No player progress yet" );
			return;
		}

		progress.DebugSetTime( seconds, relative );
	}

	static void GrantWallet( long cash, long credits, long crypto )
	{
		if ( PlayerWallet.Local is not { } wallet )
		{
			Log.Info( "No wallet yet" );
			return;
		}

		wallet.DebugGrant( cash, credits, crypto );
	}

	static void Grant( long xp, long hackXp )
	{
		if ( PlayerProgress.Local is not { Loaded: true } progress )
		{
			Log.Info( "No player progress yet" );
			return;
		}

		progress.DebugGrant( xp, hackXp );
	}
}
