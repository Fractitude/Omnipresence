namespace Sandbox;

/// <summary>
/// Console commands for testing progression. They only ask: the host decides (see
/// <see cref="PlayerProgress.DebugGrant"/>) and refuses outside the editor.
/// </summary>
public static class ProgressCommands
{
	[ConCmd( "progress_give_xp" )]
	public static void GiveXp( long amount ) => Grant( amount, 0, 0 );

	[ConCmd( "progress_give_money" )]
	public static void GiveMoney( long amount ) => Grant( 0, amount, 0 );

	[ConCmd( "progress_give_credits" )]
	public static void GiveCredits( long amount ) => Grant( 0, 0, amount );

	[ConCmd( "progress_show" )]
	public static void Show()
	{
		if ( PlayerProgress.Local is not { Loaded: true } progress )
		{
			Log.Info( "No player progress yet" );
			return;
		}

		Log.Info( $"Level {progress.Level}, {progress.Xp} XP, ${progress.Money}, {progress.Credits} credits" );
	}

	static void Grant( long xp, long money, long credits )
	{
		if ( PlayerProgress.Local is not { Loaded: true } progress )
		{
			Log.Info( "No player progress yet" );
			return;
		}

		progress.DebugGrant( xp, money, credits );
	}
}
