using System.IO;
using System.Text;

namespace Sandbox;

/// <summary>
/// One line of the world ledger: a transaction as the host did it.
/// </summary>
public sealed class TransactionLine
{
	public string Id { get; set; } = "";

	public string Group { get; set; } = "";

	/// <summary>Real-world UTC.</summary>
	public DateTimeOffset Utc { get; set; }

	/// <summary>Game seconds.</summary>
	public long At { get; set; }

	public string Currency { get; set; } = "";

	public long Amount { get; set; }

	/// <summary>The source, as <see cref="Party.Key"/>.</summary>
	public string From { get; set; } = "";

	public string FromName { get; set; } = "";

	public string To { get; set; } = "";

	public string ToName { get; set; } = "";

	public string Kind { get; set; } = "";

	public string Note { get; set; } = "";

	public string Ref { get; set; } = "";
}

/// <summary>
/// The append-only audit log of every transaction, for the host: <c>transactions/ledger_{year}-{month}_{n}.log</c> in
/// the game's data folder, one JSON object a line. Lines wait in memory and <see cref="TransactionSystem"/> writes
/// them every few seconds, so nothing is written per frame and a crash loses seconds, not the file. A file that passes
/// <see cref="MaxFileBytes"/> is left as it is and the next lines go to the next number. Host only.
/// </summary>
public static class TransactionLedger
{
	public const string DefaultFolder = "transactions";

	/// <summary>A file is closed once it holds this much.</summary>
	public const long MaxFileBytes = 4 * 1024 * 1024;

	/// <summary>The most lines kept waiting if the disk keeps refusing them; older ones are dropped.</summary>
	const int MaxPending = 20000;

	static string folder = DefaultFolder;
	static List<string> pending = new();
	static string segmentMonth = "";
	static int segment;

	/// <summary>
	/// Write to another folder from now on (the test data, see <c>progress_test_data</c>). What waits is written to the
	/// old folder first.
	/// </summary>
	public static void UseFolder( string name )
	{
		Flush();
		folder = string.IsNullOrWhiteSpace( name ) ? DefaultFolder : name;
		segmentMonth = "";
		segment = 0;
	}

	/// <summary>The folder the ledger is written to now.</summary>
	public static string Folder => folder;

	/// <summary>How many lines wait to be written.</summary>
	public static int Pending => pending.Count;

	public static void Add( TransactionLine line )
	{
		if ( !Networking.IsHost )
			return;

		pending.Add( Json.Serialize( line ).ReplaceLineEndings( " " ) );

		if ( pending.Count > MaxPending )
			pending.RemoveRange( 0, pending.Count - MaxPending );
	}

	/// <summary>
	/// Write what is waiting. Safe to call at any time; on a failure the lines stay for the next try.
	/// </summary>
	public static void Flush()
	{
		if ( !Networking.IsHost || pending.Count == 0 )
			return;

		try
		{
			var data = FileSystem.Data;
			data.CreateDirectory( folder );

			var month = DateTimeOffset.UtcNow.ToString( "yyyy-MM" );

			if ( month != segmentMonth )
			{
				segmentMonth = month;
				segment = 0;
			}

			string path;

			while ( true )
			{
				path = $"{folder}/ledger_{month}_{segment}.log";

				if ( !data.FileExists( path ) || data.FileSize( path ) < MaxFileBytes )
					break;

				segment++;
			}

			var bytes = Encoding.UTF8.GetBytes( string.Join( "\n", pending ) + "\n" );

			using ( var stream = data.OpenWrite( path, FileMode.Append ) )
				stream.Write( bytes, 0, bytes.Length );

			pending.Clear();
		}
		catch ( System.Exception e )
		{
			Log.Warning( $"Transactions: can't write the ledger: {e.Message}" );
		}
	}
}

/// <summary>
/// Writes the ledger to disk every few seconds. A <see cref="GameObjectSystem"/>, so the engine makes it in every
/// scene.
/// </summary>
public sealed class TransactionSystem : GameObjectSystem<TransactionSystem>
{
	const float FlushInterval = 5f;

	TimeUntil nextFlush;

	public TransactionSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.FinishUpdate, 0, Tick, "TransactionSystem.Tick" );
	}

	void Tick()
	{
		if ( !Networking.IsHost || !nextFlush )
			return;

		nextFlush = FlushInterval;
		TransactionLedger.Flush();
	}
}
