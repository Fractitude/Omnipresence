namespace Sandbox;

/// <summary>
/// One crop, as the world file keeps it: which drug grows in a tray and when it is ready.
/// </summary>
public sealed class TrayCrop
{
	/// <summary>The drug planted (<see cref="ProductDef.Id"/>).</summary>
	public string Kind { get; set; } = "";

	/// <summary>Game seconds (<see cref="WorldClock.Now"/>) when the crop is ready.</summary>
	public long ReadyAt { get; set; }

	/// <summary>The quality of the crop, fixed when it was planted.</summary>
	public int Quality { get; set; }
}

/// <summary>
/// What grows in each grow tray, kept in <c>trays.json</c> in the data folder (<c>trays_test.json</c> under
/// <c>progress_test_data on</c>) so a restart does not empty every tray. A crop is the tray's, not a player's (see
/// <see cref="GrowTray"/>), so it cannot live in a <see cref="CrimeRecord"/>: the tray reads its own crop from here
/// when it starts, by its id, and writes it back whenever a crop is planted, ripens or is harvested. There is an entry
/// only while something grows, so harvesting forgets it.
/// </summary>
/// <remarks>
/// Host only. Not <c>readonly</c> (see AGENTS.md, "Static state and hot reload"): the file is read the first time it is
/// asked, and a hot reload keeps it.
/// </remarks>
public static class TrayRegistry
{
	const string DefaultFile = "trays.json";

	/// <summary>The most crops kept. A file holding more has the extras dropped.</summary>
	public const int MaxCrops = 500;

	static Dictionary<string, TrayCrop> crops;
	static string fileName;

	/// <summary>
	/// The file in the data folder that keeps the crops. A test uses another one so it never touches the real one.
	/// </summary>
	public static string FileName => string.IsNullOrEmpty( fileName ) ? DefaultFile : fileName;

	/// <summary>
	/// Keep the crops in another file from now on, and read them again. For tests. Host only.
	/// </summary>
	public static void UseFile( string file )
	{
		if ( Networking.IsHost )
		{
			fileName = file;
			crops = null;
		}
	}

	static Dictionary<string, TrayCrop> Crops => crops ??= Load();

	/// <summary>
	/// The crop in this tray, or null when it is empty. Host only.
	/// </summary>
	public static TrayCrop Crop( string trayId )
	{
		if ( !Networking.IsHost || string.IsNullOrEmpty( trayId ) )
			return null;

		return Crops.GetValueOrDefault( trayId );
	}

	/// <summary>
	/// Write down what grows in a tray. An empty kind forgets the tray instead. Host only.
	/// </summary>
	public static void Set( string trayId, string kind, long readyAt, int quality )
	{
		if ( !Networking.IsHost || string.IsNullOrEmpty( trayId ) )
			return;

		if ( string.IsNullOrWhiteSpace( kind ) )
		{
			Forget( trayId );
			return;
		}

		Crops[trayId] = new TrayCrop
		{
			Kind = kind,
			// Fixed on the way in: the clock is the host's, and nothing grows for ever.
			ReadyAt = System.Math.Clamp( readyAt, 0, WorldClock.Now + 30 * 24 * 60 * 60 ),
			Quality = System.Math.Clamp( quality, 0, 100 ),
		};

		Save();
	}

	/// <summary>
	/// Forget a tray, so it comes back empty. Host only.
	/// </summary>
	public static void Forget( string trayId )
	{
		if ( !Networking.IsHost || string.IsNullOrEmpty( trayId ) || !Crops.Remove( trayId ) )
			return;

		Save();
	}

	static Dictionary<string, TrayCrop> Load()
	{
		Dictionary<string, TrayCrop> loaded = null;

		try
		{
			if ( FileSystem.Data is { } data
			     && FileProgressStore.TryReadSafely<Dictionary<string, TrayCrop>>( data, FileName ) is { } saved )
				loaded = saved;
		}
		catch ( System.Exception e )
		{
			Log.Warning( e, $"Couldn't read {FileName}: {e.Message}" );
		}

		// A file is outside input: a tray with no id, a drug that no longer exists or a time that makes no sense goes.
		var clean = new Dictionary<string, TrayCrop>();
		var dropped = 0;

		foreach ( var (id, crop) in loaded ?? [] )
		{
			if ( crop is null || string.IsNullOrWhiteSpace( id ) || id.Length > CrimeRecord.MaxIdLength
			     || ProductCatalog.Find( crop.Kind ) is null || crop.ReadyAt < 0 || clean.Count >= MaxCrops )
			{
				dropped++;
				continue;
			}

			clean[id] = crop;
			crop.Quality = System.Math.Clamp( crop.Quality, 0, 100 );
		}

		if ( dropped > 0 )
			Log.Warning( $"Trays: {dropped} entries of {FileName} were invalid and are dropped" );

		return clean;
	}

	static void Save()
	{
		try
		{
			if ( FileSystem.Data is { } data )
				FileProgressStore.WriteSafely( data, "", FileName, Json.Serialize( Crops ) );
		}
		catch ( System.Exception e )
		{
			Log.Warning( e, $"Couldn't save {FileName}: {e.Message}" );
		}
	}
}
