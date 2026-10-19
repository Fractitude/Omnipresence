namespace Sandbox;

/// <summary>
/// The world's clock. Shipments, store lockouts and payments all run on it, and the HUD shows it, so what the player
/// sees is the time their packages arrive by. The host's clock is the world's: clients follow it, whatever their own
/// clock and time zone say.
/// </summary>
/// <remarks>
/// Not <c>readonly</c>: a hot reload would reset these (see AGENTS.md, "Static state and hot reload").
/// </remarks>
public static class WorldClock
{
	static long skew;
	static int utcOffsetMinutes = int.MinValue;

	static long LocalNow => System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();

	/// <summary>Unix seconds. The time every deadline in the game is kept in.</summary>
	public static long Now => LocalNow + skew;

	/// <summary>The host's time zone, in minutes from UTC.</summary>
	public static int HostUtcOffsetMinutes => (int)System.DateTimeOffset.Now.Offset.TotalMinutes;

	/// <summary>The current world time, in the host's time zone.</summary>
	static System.DateTimeOffset Time
	{
		get
		{
			var time = System.DateTimeOffset.FromUnixTimeSeconds( Now );

			return utcOffsetMinutes == int.MinValue
				? time.ToLocalTime()
				: time.ToOffset( System.TimeSpan.FromMinutes( utcOffsetMinutes ) );
		}
	}

	/// <summary>The current time of day, as the Payments app shows it.</summary>
	public static string TimeOfDay => Time.ToString( "HH:mm" );

	/// <summary>The current date, as the Payments app shows it.</summary>
	public static string Date => Time.ToString( "d MMM" );

	/// <summary>
	/// Follow the host's clock. Called on a client with what the host synced. The host is the clock and never follows.
	/// </summary>
	public static void Follow( long hostNow, int hostUtcOffsetMinutes )
	{
		if ( Networking.IsHost || hostNow <= 0 )
			return;

		skew = hostNow - LocalNow;
		utcOffsetMinutes = hostUtcOffsetMinutes;
	}
}
