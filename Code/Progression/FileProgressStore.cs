using System.Threading.Tasks;

namespace Sandbox;

/// <summary>
/// One JSON file per player in the game's data folder, <c>progress/{steamId}.json</c>. On a dedicated server that is the
/// server's own disk. Tests use another folder (<c>progress_test</c>) so they never touch a real save.
/// </summary>
public sealed class FileProgressStore : IProgressStore
{
	/// <summary>The folder of the real saves.</summary>
	public const string DefaultFolder = "progress";

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

	public Task<ProgressRecord> LoadAsync( ulong steamId )
	{
		if ( FileSystem.Data is not { } data )
			throw new System.InvalidOperationException( "The data folder isn't available" );

		var path = PathFor( steamId );
		if ( !data.FileExists( path ) )
			return Task.FromResult<ProgressRecord>( null );

		var record = Json.Deserialize<ProgressRecord>( data.ReadAllText( path ) );
		return record is null
			? throw new System.InvalidOperationException( $"{path} is empty or damaged" )
			: Task.FromResult( record );
	}

	public Task SaveAsync( ulong steamId, ProgressRecord record )
	{
		if ( FileSystem.Data is not { } data )
			throw new System.InvalidOperationException( "The data folder isn't available" );

		data.CreateDirectory( Folder );
		data.WriteAllText( PathFor( steamId ), Json.Serialize( record ) );
		return Task.CompletedTask;
	}
}
