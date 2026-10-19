namespace Sandbox;

/// <summary>
/// The game's own time, in seconds from midnight at the start of day 1. A new world starts at 12:00 and the clock runs
/// in real time. Shipments, store lockouts and payments run on it and the HUD shows it. The host keeps it and saves it
/// (<c>world.json</c>) so it carries on between sessions; clients follow what the host syncs, on join and when it jumps.
/// </summary>
/// <remarks>
/// Not <c>readonly</c>: a hot reload would reset these (see AGENTS.md, "Static state and hot reload").
/// </remarks>
public static class WorldClock
{
	/// <summary>Where a new world starts: 12:00 on day 1.</summary>
	public const long StartSeconds = 12 * 3600;

	/// <summary>Game seconds that pass each real second.</summary>
	public const double TimeScale = 1;

	/// <summary>
	/// Times from before the game had its own clock were Unix seconds, which are far above any game time.
	/// </summary>
	public const long LegacyLimit = 1_000_000_000;

	const long DaySeconds = 24 * 3600;
	const double SaveInterval = 10;
	const double FollowTolerance = 1.5;

	static double anchorGame = StartSeconds;
	static long anchorTicks = System.DateTime.UtcNow.Ticks;
	static bool loaded;
	static bool followed;
	static long lastSaveTicks;

	static double Elapsed => (System.DateTime.UtcNow.Ticks - anchorTicks) / (double)System.TimeSpan.TicksPerSecond;

	/// <summary>Game seconds since midnight at the start of day 1. The time every deadline in the game is kept in.</summary>
	public static long Now => (long)(anchorGame + Elapsed * TimeScale);

	/// <summary>The current time of day, like "12:00".</summary>
	public static string TimeOfDay => TimeOfDayOf( Now );

	/// <summary>The current day, like "Day 1".</summary>
	public static string Day => $"Day {Now / DaySeconds + 1}";

	public static string TimeOfDayOf( long seconds ) => $"{seconds % DaySeconds / 3600:00}:{seconds % 3600 / 60:00}";

	/// <summary>A time as a player reads it, like "Day 2 09:30". Anything saved before the game clock is "Earlier".</summary>
	public static string Format( long seconds ) =>
		seconds <= 0 ? "Earlier" : $"Day {seconds / DaySeconds + 1} {TimeOfDayOf( seconds )}";

	/// <summary>Set the world time. Host only. Clients follow on their next sync.</summary>
	public static void Set( long seconds )
	{
		if ( !Networking.IsHost )
			return;

		Anchor( System.Math.Max( seconds, 0 ) );
		Save();
	}

	/// <summary>
	/// Load the saved world time the first time, then save it now and then. Call every frame on the host.
	/// </summary>
	public static void TickHost()
	{
		if ( !loaded )
		{
			loaded = true;
			Load();
		}

		if ( (System.DateTime.UtcNow.Ticks - lastSaveTicks) / (double)System.TimeSpan.TicksPerSecond > SaveInterval )
			Save();
	}

	/// <summary>
	/// Follow the host's clock. Called on a client with what the host synced. Only a jump or the first sync moves the
	/// clock; the small steps between syncs would only make it stutter. The host is the clock and never follows.
	/// </summary>
	public static void Follow( long hostNow )
	{
		if ( Networking.IsHost || hostNow <= 0 )
			return;

		if ( followed && System.Math.Abs( hostNow - Now ) <= FollowTolerance )
			return;

		followed = true;
		Anchor( hostNow );
	}

	static void Anchor( double seconds )
	{
		anchorGame = seconds;
		anchorTicks = System.DateTime.UtcNow.Ticks;
	}

	static void Load()
	{
		try
		{
			if ( FileSystem.Data is { } data && data.FileExists( "world.json" ) )
			{
				var saved = Json.Deserialize<SavedClock>( data.ReadAllText( "world.json" ) );
				if ( saved is not null )
					Anchor( System.Math.Max( saved.Seconds, 0 ) );
			}
		}
		catch ( System.Exception e )
		{
			Log.Warning( $"Couldn't read world.json, starting at 12:00: {e.Message}" );
		}
	}

	/// <summary>Write the world time to disk. Host only.</summary>
	public static void Save()
	{
		lastSaveTicks = System.DateTime.UtcNow.Ticks;

		if ( !Networking.IsHost || !loaded || FileSystem.Data is not { } data )
			return;

		try
		{
			data.WriteAllText( "world.json", Json.Serialize( new SavedClock { Seconds = Now } ) );
		}
		catch ( System.Exception e )
		{
			Log.Warning( $"Couldn't save world.json: {e.Message}" );
		}
	}

	sealed class SavedClock
	{
		public long Seconds { get; set; }
	}
}
