using System.Threading.Tasks;

namespace Sandbox;

/// <summary>
/// One JSON file per player in the game's data folder, <c>progress/{steamId}.json</c>. On a dedicated server that is the
/// server's own disk. Tests use another folder (<c>progress_test</c>) so they never touch a real save.
/// </summary>
/// <remarks>
/// A write that is cut short (a crash, a power cut) must not cost a record. Before a file is overwritten, the copy that
/// is there (if it can be read) is kept as <c>.bak</c>; a load that finds the file damaged takes the backup instead of
/// failing. The engine's own atomic write is not open to game code.
/// </remarks>
public sealed class FileProgressStore : IProgressStore
{
	/// <summary>The folder of the real saves.</summary>
	public const string DefaultFolder = "progress";

	const string BackupSuffix = ".bak";
	const string PendingFile = "_pending.json";

	/// <summary>
	/// The folder in the game's data folder that holds the files. Never empty, even on a store made by an older build
	/// that a hot reload kept: it would write into the data folder itself.
	/// </summary>
	public string Folder => string.IsNullOrEmpty( folder ) ? DefaultFolder : folder;

	readonly string folder;

	public FileProgressStore( string folder = DefaultFolder )
	{
		this.folder = folder;
	}

	string PathFor( ulong steamId ) => $"{Folder}/{steamId}.json";

	static BaseFileSystem Data => FileSystem.Data
		?? throw new System.InvalidOperationException( "The data folder isn't available" );

	public Task<ProgressRecord> LoadAsync( ulong steamId )
	{
		var data = Data;
		var path = PathFor( steamId );

		if ( !data.FileExists( path ) && !data.FileExists( path + BackupSuffix ) )
			return Task.FromResult<ProgressRecord>( null );

		if ( TryRead<ProgressRecord>( data, path ) is { } record )
			return Task.FromResult( record );

		if ( TryRead<ProgressRecord>( data, path + BackupSuffix ) is { } backup )
		{
			Log.Warning( $"{path} is damaged: using its backup" );
			return Task.FromResult( backup );
		}

		throw new System.InvalidOperationException( $"{path} is empty or damaged" );
	}

	public Task SaveAsync( ulong steamId, ProgressRecord record )
	{
		WriteSafely( Data, Folder, PathFor( steamId ), Json.Serialize( record ) );
		return Task.CompletedTask;
	}

	public Task<IReadOnlyList<ulong>> ListAsync()
	{
		var data = Data;
		var ids = new List<ulong>();

		if ( data.DirectoryExists( Folder ) )
		{
			foreach ( var file in data.FindFile( Folder, "*.json" ) )
			{
				if ( ulong.TryParse( System.IO.Path.GetFileNameWithoutExtension( file ), out var id ) )
					ids.Add( id );
			}
		}

		return Task.FromResult<IReadOnlyList<ulong>>( ids );
	}

	public Task<IReadOnlyList<ulong>> LoadPendingAsync()
	{
		var data = Data;
		var path = $"{Folder}/{PendingFile}";

		if ( !data.FileExists( path ) && !data.FileExists( path + BackupSuffix ) )
			return Task.FromResult<IReadOnlyList<ulong>>( null );

		var index = TryRead<PendingIndex>( data, path ) ?? TryRead<PendingIndex>( data, path + BackupSuffix );

		// A damaged index is no index: the caller looks at every record again.
		return Task.FromResult<IReadOnlyList<ulong>>( index?.Ids );
	}

	public Task SavePendingAsync( IReadOnlyCollection<ulong> steamIds )
	{
		var index = new PendingIndex { Ids = steamIds.OrderBy( x => x ).ToList() };
		WriteSafely( Data, Folder, $"{Folder}/{PendingFile}", Json.Serialize( index ) );
		return Task.CompletedTask;
	}

	static void WriteSafely( BaseFileSystem data, string folder, string path, string text )
	{
		data.CreateDirectory( folder );

		if ( data.FileExists( path ) )
		{
			// Only a copy that can be read replaces the backup: never overwrite a good backup with a damaged file.
			var old = data.ReadAllText( path );

			if ( !string.IsNullOrWhiteSpace( old ) && IsJson( old ) )
				data.WriteAllText( path + BackupSuffix, old );
		}

		data.WriteAllText( path, text );
	}

	static bool IsJson( string text )
	{
		try
		{
			return Json.Deserialize<object>( text ) is not null;
		}
		catch ( System.Exception )
		{
			return false;
		}
	}

	static T TryRead<T>( BaseFileSystem data, string path ) where T : class
	{
		if ( !data.FileExists( path ) )
			return null;

		try
		{
			return Json.Deserialize<T>( data.ReadAllText( path ) );
		}
		catch ( System.Exception )
		{
			return null;
		}
	}

	sealed class PendingIndex
	{
		public List<ulong> Ids { get; set; } = new();
	}
}
