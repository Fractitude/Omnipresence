using System.Threading.Tasks;

namespace Sandbox;

/// <summary>
/// The records of the players the host is working with, and the index of the ones that have work waiting on the clock.
/// There is one in-memory <see cref="ProgressRecord"/> per player, however many things ask for it: the player's own
/// components and the <see cref="OfflineCrimeProcessor"/> share it, and a record that something is attached to is never
/// processed by the processor. Everything runs on the main thread and each change is made in one synchronous step, so
/// no lock is needed: it is enough to check <see cref="IsAttached"/> at the moment of the change.
/// </summary>
/// <remarks>
/// Host only. <see cref="PlayerProgression"/> holds the one the game uses; a test makes its own, on a store of its own.
/// </remarks>
public sealed class ProgressCache
{
	readonly System.Func<IProgressStore> getStore;
	readonly System.Func<ulong, bool> isConnected;
	readonly Dictionary<ulong, ProgressRecord> records;
	readonly Dictionary<ulong, Task<ProgressRecord>> loading;
	readonly Dictionary<ulong, int> attached = new();
	readonly HashSet<ulong> pending = new();
	Task indexTask;

	/// <param name="getStore">The store to use, asked for every time so it can be swapped.</param>
	/// <param name="isConnected">Whether a player is in the game: a record of one is kept in memory.</param>
	/// <param name="records">An existing cache to share (the game's own, kept across hot reloads).</param>
	/// <param name="loading">The loads in flight that go with <paramref name="records"/>.</param>
	public ProgressCache(
		System.Func<IProgressStore> getStore, System.Func<ulong, bool> isConnected = null,
		Dictionary<ulong, ProgressRecord> records = null, Dictionary<ulong, Task<ProgressRecord>> loading = null )
	{
		this.getStore = getStore;
		this.isConnected = isConnected ?? (_ => false);
		this.records = records ?? new();
		this.loading = loading ?? new();
	}

	IProgressStore Store => getStore();

	/// <summary>
	/// The player's record, loaded from the store the first time. Two callers at once share one load.
	/// </summary>
	public Task<ProgressRecord> LoadAsync( ulong steamId )
	{
		if ( records.TryGetValue( steamId, out var record ) )
			return Task.FromResult( record );

		if ( loading.TryGetValue( steamId, out var inFlight ) )
			return inFlight;

		var load = Fetch( steamId );

		// A store that answers at once is done before this line, and Fetch has already taken its entry out: adding it now
		// would leave a finished load behind that hands the same record out again after Release, detached from the cache.
		if ( !load.IsCompleted )
			loading[steamId] = load;

		return load;
	}

	async Task<ProgressRecord> Fetch( ulong steamId )
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

	/// <summary>
	/// The record, if it is in memory. Never loads.
	/// </summary>
	public ProgressRecord Peek( ulong steamId ) => records.GetValueOrDefault( steamId );

	/// <summary>
	/// Write the record, and keep the index of players with work waiting right: an id with work is in the index
	/// before its record is written (so a crash cannot leave work that nobody looks for), and an id with none leaves it
	/// after.
	/// </summary>
	public async Task SaveAsync( ulong steamId )
	{
		if ( !records.TryGetValue( steamId, out var record ) )
			return;

		await EnsureIndexAsync();

		var work = CrimeRules.NextDue( record ) is not null;

		if ( work && pending.Add( steamId ) )
			await Store.SavePendingAsync( pending );

		// The clock first: a clock that is a little ahead of a record is harmless, one that is behind it is not.
		WorldClock.Save();

		record.Revision++;
		await Store.SaveAsync( steamId, record );

		if ( !work && pending.Remove( steamId ) )
			await Store.SavePendingAsync( pending );
	}

	/// <summary>
	/// Write every record in memory, now. For the moment the game stops. Does not wait, so it only completes for a
	/// store that writes at once (the file store does).
	/// </summary>
	public void SaveAllNow()
	{
		foreach ( var steamId in records.Keys.ToList() )
		{
			try
			{
				_ = SaveAsync( steamId );
			}
			catch ( System.Exception e )
			{
				Log.Warning( e, $"ProgressCache: couldn't save progress for {steamId}: {e.Message}" );
			}
		}
	}

	/// <summary>
	/// Drop a player's record from memory once they have left and nothing is attached to it. Save it first.
	/// </summary>
	public void Release( ulong steamId )
	{
		if ( IsAttached( steamId ) || isConnected( steamId ) )
			return;

		records.Remove( steamId );
	}

	// -- Who is using a record ----------------------------------------------------------------------------------------

	/// <summary>
	/// A live player object took the record: it changes it from now on, and the processor leaves it alone. Counted,
	/// since a respawn can have the old and the new object alive for a moment.
	/// </summary>
	public void Attach( ulong steamId ) => attached[steamId] = attached.GetValueOrDefault( steamId ) + 1;

	public void Detach( ulong steamId )
	{
		var count = attached.GetValueOrDefault( steamId ) - 1;

		if ( count > 0 )
			attached[steamId] = count;
		else
			attached.Remove( steamId );
	}

	public bool IsAttached( ulong steamId ) => attached.ContainsKey( steamId );

	// -- The index of players with work waiting -----------------------------------------------------------------------

	/// <summary>
	/// The ids that have work waiting on the clock. A copy.
	/// </summary>
	public IReadOnlyList<ulong> PendingIds => pending.ToList();

	/// <summary>
	/// Take an id out of the index without writing its record, for a record that was found to have nothing waiting.
	/// </summary>
	public async Task ClearPendingAsync( ulong steamId )
	{
		await EnsureIndexAsync();

		if ( pending.Remove( steamId ) )
			await Store.SavePendingAsync( pending );
	}

	/// <summary>
	/// Read the index once (or, when there is none, look at every record to make it). Every caller shares the one run.
	/// </summary>
	public Task EnsureIndexAsync() => indexTask ??= ReadIndexAsync();

	async Task ReadIndexAsync()
	{
		try
		{
			var saved = await Store.LoadPendingAsync();

			if ( saved is null )
			{
				await RescanCoreAsync();
				return;
			}

			foreach ( var id in saved )
				pending.Add( id );
		}
		catch ( System.Exception e )
		{
			// Start again at the next call rather than save an index that is missing players.
			indexTask = null;
			Log.Warning( e, $"ProgressCache: couldn't read the index of waiting work: {e.Message}" );
			throw;
		}
	}

	/// <summary>
	/// Look at every record in the store and rebuild the index from what has work waiting. For a store with no index
	/// yet, a damaged one, or a check by hand. A record that can't be read is logged and left out.
	/// </summary>
	public Task RescanAsync() => indexTask = RescanCoreAsync();

	async Task RescanCoreAsync()
	{
		var found = new HashSet<ulong>();
		var ids = await Store.ListAsync();
		var count = 0;

		foreach ( var id in ids )
		{
			try
			{
				// A record in memory is the newest; the store may be behind it.
				var record = records.GetValueOrDefault( id );

				if ( record is null )
				{
					record = await Store.LoadAsync( id );
					record?.Sanitize();
				}

				if ( record is not null && CrimeRules.NextDue( record ) is not null )
					found.Add( id );
			}
			catch ( System.Exception e )
			{
				Log.Warning( e, $"ProgressCache: couldn't read the record of {id} while looking for waiting work" );
			}

			if ( ++count % 25 == 0 )
				await Task.Delay( 1 );
		}

		pending.Clear();

		foreach ( var id in found )
			pending.Add( id );

		await Store.SavePendingAsync( pending );
	}
}
