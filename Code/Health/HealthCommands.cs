namespace Sandbox;

public static class HealthCommands
{
	/// <summary>
	/// Kill yourself
	/// </summary>
	[ConCmd( "kill" )]
	public static void Kill( Connection source )
	{
		Player.FindForConnection( source )?.KillSelf();
	}

	[ConCmd( "god", ConVarFlags.Server | ConVarFlags.Cheat, Help = "Toggle invulnerability" )]
	public static void God( Connection source )
	{
		var data = PlayerData.For( source );
		if ( !data.IsValid() )
			return;

		data.IsGodMode = !data.IsGodMode;
		source.SendLog( LogLevel.Info, data.IsGodMode ? "Godmode enabled" : "Godmode disabled" );
	}
}
