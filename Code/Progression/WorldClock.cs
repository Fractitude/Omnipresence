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

	/// <summary>Day 1 is a Monday.</summary>
	static string[] weekdays = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"];
	const double SaveInterval = 10;
	const double FollowTolerance = 1.5;

	/// <summary>
	/// The file in the data folder that keeps the time. A test uses another one (<see cref="UseFile"/>) so it never
	/// moves the real clock.
	/// </summary>
	public static string FileName => string.IsNullOrEmpty( fileName ) ? "world.json" : fileName;

	/// <summary>
	/// Whether the saved time has been read, so <see cref="Now"/> is the world's and not the default 12:00. Host only.
	/// </summary>
	public static bool IsLoaded => loaded;

	static string fileName;
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

	/// <summary>The current day, like "Monday, Day 1".</summary>
	public static string Day => DayOf( Now );

	/// <summary>The current week, counted from 1.</summary>
	public static long Week => Now / DaySeconds / 7 + 1;

	static string DayOf( long seconds, bool shortName = false )
	{
		var day = seconds / DaySeconds;
		var name = weekdays[day % 7];

		return $"{(shortName ? name[..3] : name)}, Day {day + 1}";
	}

	public static string TimeOfDayOf( long seconds ) => $"{seconds % DaySeconds / 3600:00}:{seconds % 3600 / 60:00}";

	/// <summary>A time as a player reads it, like "Tue, Day 2 09:30". Anything saved before the game clock is "Earlier".</summary>
	public static string Format( long seconds ) =>
		seconds <= 0 ? "Earlier" : $"{DayOf( seconds, shortName: true )} {TimeOfDayOf( seconds )}";

	/// <summary>Set the world time. Host only. Clients follow on their next sync.</summary>
	public static void Set( long seconds )
	{
		if ( !Networking.IsHost )
			return;

		Anchor( System.Math.Max( seconds, 0 ) );
		Save();
	}

	/// <summary>
	/// Keep the time in another file from now on, and read it at the next <see cref="TickHost"/> (the time stays as it
	/// is when that file does not exist yet). For tests. Host only.
	/// </summary>
	public static void UseFile( string fileName )
	{
		if ( !Networking.IsHost )
			return;

		WorldClock.fileName = fileName;
		loaded = false;
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
			if ( FileSystem.Data is { } data && data.FileExists( FileName ) )
			{
				var saved = Json.Deserialize<SavedClock>( data.ReadAllText( FileName ) );
				if ( saved is not null )
					Anchor( System.Math.Max( saved.Seconds, 0 ) );
			}
		}
		catch ( System.Exception e )
		{
			Log.Warning( $"Couldn't read {FileName}, starting at 12:00: {e.Message}" );
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
			data.WriteAllText( FileName, Json.Serialize( new SavedClock { Seconds = Now } ) );
		}
		catch ( System.Exception e )
		{
			Log.Warning( $"Couldn't save {FileName}: {e.Message}" );
		}
	}

	sealed class SavedClock
	{
		public long Seconds { get; set; }
	}
}
