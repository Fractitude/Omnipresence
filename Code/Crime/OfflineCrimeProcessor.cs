using System.Threading.Tasks;

namespace Sandbox;

/// <summary>
/// Carries on the crime game for players who are not playing: a parcel is collected and delivered, the buyer reviews
/// it, escrow pays, a cash-out lands, a shop delivery reaches the mailbox, an unsent order is cancelled at its
/// deadline. It runs the same <see cref="CrimeRules.Advance"/> that <see cref="PlayerCrime"/> runs for a player who is
/// there, on the player's own saved record, with no player object. It handles a player who left, one who is dead and
/// has not respawned, and one the server has not seen since it started. Plain class (the
/// <see cref="OfflineCrimeSystem"/> drives it) so a test can run it on a cache and a clock of its own. Host only.
/// </summary>
/// <remarks>
/// It never touches a record that something is attached to (<see cref="ProgressCache.IsAttached"/>): that player's own
/// components change it. Both run on the main thread and a record is changed in one synchronous step, so the two can't
/// overlap; a player who connects while the record is being loaded takes the very same instance, after or before the
/// step, and sees the whole result either way. What a record needs is found from the persisted index of ids with work
/// waiting (<see cref="ProgressCache.PendingIds"/>), never from anything a client says.
/// </remarks>
public sealed class OfflineCrimeProcessor
{
	/// <summary>
	/// The most records worked on in one tick, so a restart with many players away does not stall a frame.
	/// </summary>
	public const int MaxPerTick = 8;

	/// <summary>
	/// Game seconds a record that could not be handled (a file that won't load) waits before the next try.
	/// </summary>
	public const long FailureBackoffSeconds = 60;

	readonly ProgressCache cache;
	readonly System.Func<long> clock;

	// The game time each id with waiting work is looked at next: 0 until it has been looked at once.
	readonly Dictionary<ulong, long> due = new();
	readonly HashSet<ulong> busy = new();

	public OfflineCrimeProcessor( ProgressCache cache, System.Func<long> clock )
	{
		this.cache = cache;
		this.clock = clock;
	}

	/// <summary>
	/// Called after a record was brought up to date, whether or not anything changed. The record may be gone from the
	/// cache already.
	/// </summary>
	public event System.Action<ulong, ProgressRecord> Processed;

	/// <summary>
	/// How many players have work waiting that this is looking after.
	/// </summary>
	public int Tracked => due.Count;

	/// <summary>
	/// The game time this id is looked at next, or null when it is not tracked.
	/// </summary>
	public long? DueOf( ulong steamId ) => due.TryGetValue( steamId, out var at ) ? at : null;

	/// <summary>
	/// Look at this record on the next tick: something outside the processor gave it work that falls due sooner than
	/// the time it was set to wait for (another player posted a parcel the player had packed). No effect on an id that
	/// is not tracked yet: its save puts it in the index, which <see cref="Track"/> reads.
	/// </summary>
	public void Wake( ulong steamId )
	{
		if ( due.ContainsKey( steamId ) )
			due[steamId] = 0;
	}

	/// <summary>
	/// Read the index of players with work waiting (making it first if the store has none) and start on them.
	/// </summary>
	public async Task StartAsync()
	{
		await cache.EnsureIndexAsync();
		Track();
	}

	/// <summary>
	/// Once a second or so: look at every tracked record that is due, up to <see cref="MaxPerTick"/> at a time.
	/// </summary>
	public void Tick()
	{
		Track();

		var now = clock();
		var started = 0;

		foreach ( var steamId in due.Where( x => x.Value <= now ).Select( x => x.Key ).ToList() )
		{
			if ( busy.Contains( steamId ) || cache.IsAttached( steamId ) )
				continue;

			if ( ++started > MaxPerTick )
				break;

			_ = ProcessAsync( steamId );
		}
	}

	/// <summary>
	/// Take on every id the index lists, including ones a player's own save added since (a player who left with work
	/// still waiting).
	/// </summary>
	void Track()
	{
		foreach ( var steamId in cache.PendingIds )
			due.TryAdd( steamId, 0 );
	}

	/// <summary>
	/// Bring one player's record up to the clock, save it if it changed, and work out when to look again.
	/// </summary>
	public async Task ProcessAsync( ulong steamId )
	{
		if ( !busy.Add( steamId ) )
			return;

		try
		{
			var record = await cache.LoadAsync( steamId );

			// A player took the record while it loaded: their own components carry on from here.
			if ( cache.IsAttached( steamId ) )
				return;

			var now = clock();
			var result = CrimeRules.Advance( steamId, record, now );

			// A parcel for a cancelled order is in the bag of a player who is not there: it is in the saved items.
			foreach ( var orderId in result.CancelledOrders )
				record.Crime.Items.RemoveAll( x => x.Kind == ItemCatalog.Parcel && x.OrderId == orderId );

			var next = CrimeRules.NextDue( record );

			if ( result.Changed )
				await cache.SaveAsync( steamId );
			else if ( next is null )
				await cache.ClearPendingAsync( steamId );

			Processed?.Invoke( steamId, record );

			if ( next is null )
			{
				due.Remove( steamId );
				cache.Release( steamId );
			}
			else
			{
				// Anything still overdue could not happen (a wallet that is full): try again in a while.
				due[steamId] = next.Value <= now ? now + CrimeRules.RetrySeconds : next.Value;
			}
		}
		catch ( System.Exception e )
		{
			Log.Warning( e, $"OfflineCrimeProcessor: couldn't carry on for {steamId}: {e.Message}" );
			due[steamId] = clock() + FailureBackoffSeconds;
		}
		finally
		{
			busy.Remove( steamId );
		}
	}
}
