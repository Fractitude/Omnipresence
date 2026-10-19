using System.Threading.Tasks;

namespace Sandbox;

/// <summary>
/// One JSON file per player in the game's data folder, <c>progress/{steamId}.json</c>. On a dedicated server that is the
/// server's own disk.
/// </summary>
public sealed class FileProgressStore : IProgressStore
{
	static string PathFor( ulong steamId ) => $"progress/{steamId}.json";

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

		data.CreateDirectory( "progress" );
		data.WriteAllText( PathFor( steamId ), Json.Serialize( record ) );
		return Task.CompletedTask;
	}
}
