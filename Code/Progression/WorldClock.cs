namespace Sandbox;

/// <summary>
/// The world's clock. Shipments, store lockouts and payments all run on it, and the HUD shows it, so what the player
/// sees is the time their packages arrive by.
/// </summary>
public static class WorldClock
{
	/// <summary>Unix seconds. The time every deadline in the game is kept in.</summary>
	public static long Now => System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();

	/// <summary>The current time of day, as the Payments app shows it.</summary>
	public static string TimeOfDay => System.DateTimeOffset.Now.ToString( "HH:mm" );

	/// <summary>The current date, as the Payments app shows it.</summary>
	public static string Date => System.DateTimeOffset.Now.ToString( "d MMM" );
}
