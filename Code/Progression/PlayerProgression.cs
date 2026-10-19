using System.Threading.Tasks;

namespace Sandbox;

/// <summary>
/// The host's records of every connected player's progress, and the <see cref="IProgressStore"/> they come from and go
/// back to. Static, because progress outlives the player object: a scene change destroys and respawns it.
/// </summary>
/// <remarks>
/// Host only. Never read or write this on a client. Not <c>readonly</c>: a hot reload would reset the cache
/// (see AGENTS.md, "Static state and hot reload").
/// </remarks>
public static class PlayerProgression
{
	/// <summary>
	/// Where records live. Set to an <c>HttpProgressStore</c> when the shared backend exists.
	/// </summary>
	public static IProgressStore Store { get; set; } = new FileProgressStore();

	static Dictionary<ulong, ProgressRecord> records = new();
	static Dictionary<ulong, Task<ProgressRecord>> loading = new();

	/// <summary>
	/// The player's record, loaded from the store the first time. Two callers at once share one load.
	/// </summary>
	public static Task<ProgressRecord> LoadAsync( ulong steamId )
	{
		if ( records.TryGetValue( steamId, out var record ) )
			return Task.FromResult( record );

		if ( loading.TryGetValue( steamId, out var pending ) )
			return pending;

		return loading[steamId] = Fetch( steamId );
	}

	static async Task<ProgressRecord> Fetch( ulong steamId )
	{
		try
		{
			var record = await Store.LoadAsync( steamId ) ?? new ProgressRecord();
			record.Sanitize();
			records[steamId] = record;
			return record;
		}
		finally
		{
			loading.Remove( steamId );
		}
	}

	public static async Task SaveAsync( ulong steamId )
	{
		if ( !records.TryGetValue( steamId, out var record ) )
			return;

		record.Revision++;
		await Store.SaveAsync( steamId, record );
	}

	/// <summary>
	/// Drop a player's record from memory once they have left. Save it first.
	/// </summary>
	public static void Release( ulong steamId )
	{
		if ( Connection.All.Any( x => x.SteamId == steamId ) )
			return;

		records.Remove( steamId );
	}
}
