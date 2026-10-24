using System.Threading.Tasks;

namespace Sandbox;

/// <summary>
/// The one system every movement of cash and crypto goes through. A movement is a <see cref="TxLeg"/>: an amount of a
/// currency from one <see cref="Party"/> to another (a player, or a system party such as the market), with a
/// <see cref="TxKind"/> and a note. One call runs one leg or a batch of legs, <b>all or none</b>: everything is checked
/// first (<see cref="TransactionRules.Plan"/>) and nothing changes if any leg fails. A player's balance never changes
/// except through here, apart from the two named exceptions on <see cref="PlayerWallet.GrantUntracked"/>. See
/// <c>docs/transactions.md</c>.
/// <para>
/// Each transaction gets a host-made id, is added to the history of each player in it
/// (<see cref="ProgressRecord.Transactions"/>), is written to the world ledger (<see cref="TransactionLedger"/>) and
/// to the stats log, and the player's wallet and progress are told. It works the same for a player who is playing, one
/// whose record is only in memory, and one whose record the caller holds (the offline processor): the caller says
/// nothing different. Host only; a client gets <see cref="TxFailure.NotHost"/>.
/// </para>
/// </summary>
public static class Transactions
{
	/// <summary>The most player-to-player payments one player makes a minute.</summary>
	public static int PaymentsPerMinute = 30;

	static Dictionary<ulong, List<float>> recentPayments = new();
	static Dictionary<string, long> systemNet = new();
	static Dictionary<ulong, string> knownNames = new();

	// -- Entry points -------------------------------------------------------------------------------------------------

	/// <summary>
	/// Move money from one party to another.
	/// </summary>
	public static TxResult Transfer(
		Party from, Party to, TransactionCurrency currency, long amount, TxKind kind, string note = null,
		string reference = null ) =>
		Run( [new( from, to, currency, amount, kind, note, reference )], null );

	/// <summary>
	/// A player pays a system party (a purchase, a fee). <paramref name="record"/> is the player's record when the
	/// caller holds it.
	/// </summary>
	public static TxResult Pay(
		ulong player, string to, TransactionCurrency currency, long amount, TxKind kind, string note = null,
		string reference = null, ProgressRecord record = null ) =>
		Run( [new( Party.Player( player ), Party.System( to ), currency, amount, kind, note, reference )
			{ FromRecord = record }], null );

	/// <summary>
	/// A system party pays a player (a payout, a refund). <paramref name="record"/> is the player's record when the
	/// caller holds it.
	/// </summary>
	public static TxResult Receive(
		ulong player, string from, TransactionCurrency currency, long amount, TxKind kind, string note = null,
		string reference = null, ProgressRecord record = null ) =>
		Run( [new( Party.System( from ), Party.Player( player ), currency, amount, kind, note, reference )
			{ ToRecord = record }], null );

	/// <summary>
	/// Several legs, all or none, as one group (they share a group id in the history and the ledger).
	/// </summary>
	public static TxResult Batch( IReadOnlyList<TxLeg> legs, string group = null ) => Run( legs, group );

	/// <summary>
	/// Like <see cref="Batch"/>, for legs that may name a player whose record is not in memory: it is loaded first, and
	/// saved and let go afterwards. The player's own record is used when they are connected.
	/// </summary>
	public static async Task<TxResult> BatchAsync( IReadOnlyList<TxLeg> legs, string group = null )
	{
		if ( !Networking.IsHost )
			return TxResult.Fail( TxFailure.NotHost, "Only the host moves money." );

		var loaded = new List<ulong>();

		try
		{
			foreach ( var id in PlayersOf( legs ).Distinct() )
			{
				if ( Live( id ) is not null || PlayerProgression.Cache.Peek( id ) is not null )
					continue;

				await PlayerProgression.LoadAsync( id );
				loaded.Add( id );
			}

			var result = Run( legs, group );

			foreach ( var id in loaded )
			{
				if ( result.Ok )
					await PlayerProgression.SaveAsync( id );
			}

			return result;
		}
		finally
		{
			foreach ( var id in loaded )
				PlayerProgression.Release( id );
		}
	}

	// -- Reading ------------------------------------------------------------------------------------------------------

	/// <summary>
	/// How much a system party took in (positive) and put out (negative) of this currency since the game started. The
	/// players' balances change by exactly the opposite of the sum over every system party.
	/// </summary>
	public static long NetOf( string systemParty, TransactionCurrency currency ) =>
		systemNet.GetValueOrDefault( $"{systemParty}/{currency}" );

	/// <summary>
	/// Forget the system parties' totals and the payment rate limits (tests).
	/// </summary>
	public static void ResetTotals()
	{
		systemNet = new();
		recentPayments = new();
	}

	// -- The work -----------------------------------------------------------------------------------------------------

	sealed class Handle
	{
		public ulong SteamId;
		public ProgressRecord Record;
		public PlayerProgress Live;
		public bool FromCache;
	}

	static TxResult Run( IReadOnlyList<TxLeg> legs, string group )
	{
		if ( !Networking.IsHost )
			return TxResult.Fail( TxFailure.NotHost, "Only the host moves money." );

		if ( legs is null || legs.Count == 0 )
			return TxResult.Fail( TxFailure.Empty, "Nothing to move." );

		foreach ( var leg in legs )
		{
			var invalid = TransactionRules.Validate( leg );

			if ( invalid != TxFailure.None )
				return Refused( invalid, leg );
		}

		if ( RateLimited( legs ) )
			return TxResult.Fail( TxFailure.RateLimited, "Too many payments. Wait a moment." );

		var handles = new Dictionary<ulong, Handle>();
		var planned = new List<PlannedLeg>();

		foreach ( var leg in legs )
		{
			planned.Add( new()
			{
				Leg = leg,
				FromRecord = leg.From.IsPlayer ? HandleOf( handles, leg.From.SteamId, leg.FromRecord )?.Record : null,
				ToRecord = leg.To.IsPlayer ? HandleOf( handles, leg.To.SteamId, leg.ToRecord )?.Record : null,
			} );
		}

		var failure = TransactionRules.Plan( planned );

		if ( failure != TxFailure.None )
			return Refused( failure, legs[0] );

		TransactionRules.Apply( planned );

		var groupId = group ?? (legs.Count > 1 ? NewId() : "");
		var at = WorldClock.Now;
		var utc = DateTimeOffset.UtcNow;
		var moved = new List<long>();

		foreach ( var one in planned )
		{
			moved.Add( one.Amount );

			if ( one.Amount > 0 )
				Record( one, handles, groupId, at, utc );
		}

		foreach ( var handle in handles.Values )
			Changed( handle );

		return new( TxFailure.None, "", groupId, moved );
	}

	static void Record(
		PlannedLeg planned, Dictionary<ulong, Handle> handles, string group, long at, DateTimeOffset utc )
	{
		var leg = planned.Leg;
		var id = NewId();
		var fromName = NameOf( leg.From, planned.FromRecord );
		var toName = NameOf( leg.To, planned.ToRecord );
		var kind = leg.Kind.ToString();

		if ( !leg.From.IsPlayer )
			AddNet( leg.From.Id, leg.Currency, -planned.Amount );

		if ( !leg.To.IsPlayer )
			AddNet( leg.To.Id, leg.Currency, planned.Amount );

		if ( planned.FromRecord is { } source )
			Remember( source, id, group, at, leg, -planned.Amount, leg.To, toName );

		if ( planned.ToRecord is { } target )
			Remember( target, id, group, at, leg, planned.Amount, leg.From, fromName );

		TransactionLedger.Add( new()
		{
			Id = id, Group = group ?? "", Utc = utc, At = at, Currency = leg.Currency.ToString().ToLowerInvariant(),
			Amount = planned.Amount, From = leg.From.Key, FromName = fromName, To = leg.To.Key, ToName = toName,
			Kind = kind, Note = leg.Note ?? "", Ref = leg.Ref ?? "",
		} );

		var currency = leg.Currency == TransactionCurrency.Crypto ? StatsLog.MoneyCrypto : StatsLog.MoneyCash;
		var mechanic = MechanicOf( leg.Kind );

		if ( leg.From.IsPlayer )
		{
			StatsLog.Money( leg.From.SteamId, fromName, currency, -planned.Amount,
				TransactionRules.BalanceOf( planned.FromRecord, leg.Currency ), mechanic, leg.Note,
				leg.To.IsPlayer ? leg.To.SteamId : null, toName );
		}

		if ( leg.To.IsPlayer )
		{
			StatsLog.Money( leg.To.SteamId, toName, currency, planned.Amount,
				TransactionRules.BalanceOf( planned.ToRecord, leg.Currency ), mechanic, leg.Note,
				leg.From.IsPlayer ? leg.From.SteamId : null, fromName );
		}
	}

	static void Remember(
		ProgressRecord record, string id, string group, long at, TxLeg leg, long signed, Party peer, string peerName )
	{
		TransactionRules.Remember( record, new()
		{
			Id = id, Group = group ?? "", At = at, Currency = leg.Currency, Amount = signed, Kind = leg.Kind.ToString(),
			Peer = peer.IsPlayer ? peer.SteamId.ToString() : peer.Key, PeerName = peerName ?? "", Note = leg.Note ?? "",
			Ref = leg.Ref ?? "",
		} );
	}

	/// <summary>The transaction finished for this player: show it, or save it.</summary>
	static void Changed( Handle handle )
	{
		if ( handle.Live is { } live )
			live.NotifyChanged( ProgressChange.Wallet | ProgressChange.Payments );
		else if ( handle.FromCache )
			_ = PlayerProgression.SaveAsync( handle.SteamId );
	}

	static string MechanicOf( TxKind kind ) => kind switch
	{
		TxKind.PlayerPayment => StatMechanic.Payment,
		TxKind.OrderPayout or TxKind.OrgShare => StatMechanic.OrgPayout,
		TxKind.ShopPurchase or TxKind.DepotPurchase => StatMechanic.Shop,
		TxKind.ExchangeSell or TxKind.ExchangeBuy or TxKind.CashOut => StatMechanic.Exchange,
		TxKind.Fee or TxKind.RegistrationFee => StatMechanic.Fee,
		TxKind.Refund => StatMechanic.Refund,
		_ => "other",
	};

	static void AddNet( string party, TransactionCurrency currency, long delta )
	{
		var key = $"{party}/{currency}";
		systemNet[key] = systemNet.GetValueOrDefault( key ) + delta;
	}

	static TxResult Refused( TxFailure failure, TxLeg leg )
	{
		var unit = leg?.Currency == TransactionCurrency.Crypto ? "crypto" : "dollars";

		var message = failure switch
		{
			TxFailure.InvalidAmount => "Pick an amount.",
			TxFailure.SelfTransfer => "You can't pay yourself.",
			TxFailure.UnknownParty => "That payment can't be made.",
			TxFailure.Unavailable => "That account isn't available right now.",
			TxFailure.Insufficient => $"Not enough {unit}.",
			TxFailure.CapExceeded => "That account can't hold that much.",
			TxFailure.RateLimited => "Too many payments. Wait a moment.",
			_ => "That payment can't be made.",
		};

		return TxResult.Fail( failure, message );
	}

	// -- Players ------------------------------------------------------------------------------------------------------

	static IEnumerable<ulong> PlayersOf( IReadOnlyList<TxLeg> legs ) =>
		(legs ?? []).Where( x => x is not null )
			.SelectMany( x => new[] { x.From, x.To } )
			.Where( x => x.IsPlayer )
			.Select( x => x.SteamId );

	/// <summary>The progress of a connected player whose record has loaded, or null.</summary>
	static PlayerProgress Live( ulong steamId )
	{
		// Not PlayerProgress.For( null ): a missing connection would read as the local player.
		var connection = Connection.All.FirstOrDefault( x => x.SteamId == steamId );
		return connection is not null && PlayerProgress.For( connection ) is { Loaded: true } progress
			? progress
			: null;
	}

	static Handle HandleOf( Dictionary<ulong, Handle> handles, ulong steamId, ProgressRecord hint )
	{
		if ( handles.TryGetValue( steamId, out var known ) )
			return known;

		var live = Live( steamId );
		var handle = new Handle { SteamId = steamId, Live = live };

		if ( live?.Record is { } record )
		{
			handle.Record = record;
		}
		else if ( hint is not null )
		{
			handle.Record = hint;
		}
		else if ( PlayerProgression.Cache.Peek( steamId ) is { } cached )
		{
			handle.Record = cached;
			handle.FromCache = true;
		}

		handles[steamId] = handle;
		return handle;
	}

	/// <summary>What the history calls this party: a player's name, or a system party's label.</summary>
	static string NameOf( Party party, ProgressRecord record )
	{
		if ( !party.IsPlayer )
			return Parties.LabelOf( party.Id );

		var connection = Connection.All.FirstOrDefault( x => x.SteamId == party.SteamId );

		if ( !string.IsNullOrWhiteSpace( connection?.DisplayName ) )
		{
			knownNames[party.SteamId] = connection.DisplayName;
			return connection.DisplayName;
		}

		return knownNames.TryGetValue( party.SteamId, out var name ) ? name : party.SteamId.ToString();
	}

	// -- Limits and ids -----------------------------------------------------------------------------------------------

	/// <summary>
	/// A player sending too many payments a minute is refused. Counted per sender; other kinds are not limited (they are
	/// the host's own events).
	/// </summary>
	static bool RateLimited( IReadOnlyList<TxLeg> legs )
	{
		var now = RealTime.Now;

		foreach ( var leg in legs.Where( x => x.Kind == TxKind.PlayerPayment && x.From.IsPlayer ) )
		{
			if ( !recentPayments.TryGetValue( leg.From.SteamId, out var times ) )
				recentPayments[leg.From.SteamId] = times = new();

			times.RemoveAll( x => now - x > 60f );

			if ( times.Count >= PaymentsPerMinute )
				return true;
		}

		// Counted once the whole batch is known to pass this check.
		foreach ( var leg in legs.Where( x => x.Kind == TxKind.PlayerPayment && x.From.IsPlayer ) )
			recentPayments[leg.From.SteamId].Add( now );

		return false;
	}

	/// <summary>A new unique id. Made here on the host, never taken from a client.</summary>
	static string NewId() => System.Guid.NewGuid().ToString( "N" )[..16];
}
