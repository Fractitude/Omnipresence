using System.Threading.Tasks;

namespace Sandbox;

/// <summary>
/// The host's records of every connected player's progress, and the <see cref="IProgressStore"/> they come from and go
/// back to. Static, because progress outlives the player object: a scene change destroys and respawns it. The work is
/// done by a <see cref="ProgressCache"/>.
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

	/// <summary>
	/// Use another store from now on, and forget every record read from the old one. For tests; call it before any
	/// player has loaded.
	/// </summary>
	public static void UseStore( IProgressStore store )
	{
		Store = store;
		records.Clear();
		loading.Clear();
		cache = null;
	}

	static Dictionary<ulong, ProgressRecord> records = new();
	static Dictionary<ulong, Task<ProgressRecord>> loading = new();
	static ProgressCache cache;

	/// <summary>
	/// The cache the game works with. It shares the dictionaries above, so a hot reload that keeps them keeps it
	/// working too. Made again when missing, for the same reason.
	/// </summary>
	public static ProgressCache Cache =>
		cache ??= new( () => Store, id => Connection.All.Any( x => x.SteamId == id ), records, loading );

	/// <summary>
	/// The player's record, loaded from the store the first time. Two callers at once share one load.
	/// </summary>
	public static Task<ProgressRecord> LoadAsync( ulong steamId ) => Cache.LoadAsync( steamId );

	public static Task SaveAsync( ulong steamId ) => Cache.SaveAsync( steamId );

	/// <summary>
	/// Drop a player's record from memory once they have left and nothing is attached to it. Save it first.
	/// </summary>
	public static void Release( ulong steamId ) => Cache.Release( steamId );

	/// <summary>
	/// A player object took the record, or let it go. While one is attached the offline processor leaves the record
	/// alone.
	/// </summary>
	public static void Attach( ulong steamId ) => Cache.Attach( steamId );

	public static void Detach( ulong steamId ) => Cache.Detach( steamId );
}
