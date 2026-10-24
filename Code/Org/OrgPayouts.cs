namespace Sandbox;

/// <summary>
/// Shares a payment between the members of the organization of the player who earned it. This is the one function
/// every source of revenue calls where it credits a player (today the escrow release of a Hush order, in
/// <see cref="CrimeRules"/>); a new source calls <see cref="Distribute"/> too and gets the split for free.
/// </summary>
/// <remarks>
/// The rule: the receiver is the player who did the work (for an order, whoever packed the parcel). If they are in an
/// organization, every other member with a share gets floor(amount x share / 100) at once. <b>The remainder stays with
/// the receiver</b>: the rounding, the share nobody holds, the receiver's own share, and any share a member could not
/// hold under the wallet cap. So the money paid out is always exactly the amount, and an organization with no shares
/// changes nothing. Only the organization the receiver is in at the moment of payment counts. A member whose record is
/// in memory (a player who is playing, or a record someone loaded) is paid at once, through <see cref="PlayerWallet"/>
/// for a player who is there, so there is one source of truth; any other member's share is written down in
/// <see cref="OrgRegistry"/> and put into their saved record as soon as it is loaded (a minute at most). Host only.
/// </remarks>
public static class OrgPayouts
{
	/// <summary>
	/// Pay <paramref name="amount"/> to <paramref name="receiver"/> (whose record is <paramref name="record"/>), sharing
	/// it with their organization. Returns false and changes nothing when the receiver can't hold what they would keep
	/// (the caller tries again later, as it did before organizations). Paying the same <paramref name="orderId"/> to the
	/// same receiver again only gives the receiver what was kept the first time, so a replay after a crash never pays
	/// the members twice.
	/// </summary>
	public static bool Distribute(
		ulong receiver, ProgressRecord record, long amount, OrgCurrency currency, string reason, string orderId )
	{
		if ( amount <= 0 )
			return true;

		var org = OrgRegistry.OrgOf( receiver );

		if ( org is null )
		{
			if ( amount > RoomOf( record, currency ) )
				return false;

			CreditRecord( receiver, record, currency, amount, reason );
			return true;
		}

		var done = string.IsNullOrEmpty( orderId )
			? null
			: org.Payouts.FirstOrDefault( x => x.OrderId == orderId && x.ReceiverId == receiver
			                                   && x.Currency == currency && x.Reason == reason );

		if ( done is not null )
		{
			if ( done.Kept > RoomOf( record, currency ) )
				return false;

			CreditRecord( receiver, record, currency, done.Kept, $"{org.Name}: {reason}" );
			return true;
		}

		var split = OrgRules.Split(
			amount, receiver, org.Members.Select( x => (x.SteamId, x.SharePercent) ), id => MemberRoom( id, currency ) );

		if ( split.Kept > RoomOf( record, currency ) )
			return false;

		var payout = new OrgPayout
		{
			At = OrgRegistry.Now,
			OrderId = orderId ?? "",
			Reason = reason ?? "",
			Currency = currency,
			Total = amount,
			ReceiverId = receiver,
			ReceiverName = org.MemberOf( receiver ) is { } who ? OrgRegistry.NameOf( who ) : "",
			Kept = split.Kept,
		};

		foreach ( var (memberId, share) in split.Credits )
		{
			var member = org.MemberOf( memberId );
			CreditMember( memberId, currency, share, org, orderId, reason );
			payout.Splits.Add( new() { MemberId = memberId, Name = member is null ? "" : OrgRegistry.NameOf( member ), Amount = share } );
		}

		CreditRecord( receiver, record, currency, split.Kept, $"{org.Name}: {reason}" );

		if ( org.MemberOf( receiver ) is { } receiverMember )
			receiverMember.Name = payout.ReceiverName;

		// A payout nobody shared in changes nothing for the others, so it is not written down.
		if ( split.Credits.Count > 0 )
		{
			org.Payouts.Add( payout );

			if ( org.Payouts.Count > OrgCatalog.MaxPayouts )
				org.Payouts.RemoveRange( 0, org.Payouts.Count - OrgCatalog.MaxPayouts );

			OrgRegistry.Save();
			_ = OrgRegistry.FlushPendingAsync();
		}

		return true;
	}

	/// <summary>How much more of this currency the record can hold.</summary>
	static long RoomOf( ProgressRecord record, OrgCurrency currency ) => currency == OrgCurrency.Crypto
		? ProgressRecord.MaxCrypto - record.Crypto
		: ProgressRecord.MaxMoney - record.Money;

	/// <summary>
	/// Add as much of <paramref name="amount"/> as fits under the cap to a record, and write it in the stats log as
	/// <paramref name="steamId"/>'s. Returns what was added.
	/// </summary>
	internal static long CreditRecord(
		ulong steamId, ProgressRecord record, OrgCurrency currency, long amount, string detail )
	{
		var given = System.Math.Clamp( amount, 0, System.Math.Max( RoomOf( record, currency ), 0 ) );

		if ( currency == OrgCurrency.Crypto )
			record.AddCrypto( given );
		else
			record.AddCash( given );

		var crypto = currency == OrgCurrency.Crypto;
		StatsLog.Money( steamId, Exchange.SteamName( steamId ), crypto ? StatsLog.MoneyCrypto : StatsLog.MoneyCash, given,
			crypto ? record.Crypto : record.Money, StatMechanic.OrgPayout, detail );

		return given;
	}

	/// <summary>The record of a member who can be paid right now, or null when it is not in memory.</summary>
	static ProgressRecord LiveRecord( ulong steamId )
	{
		// Not PlayerProgress.For( null ): a missing connection would read as the local player.
		if ( ConnectionOf( steamId ) is { } connection && PlayerProgress.For( connection ) is { Loaded: true } progress )
			return progress.Record;

		return PlayerProgression.Cache.Peek( steamId );
	}

	static Connection ConnectionOf( ulong steamId ) => Connection.All.FirstOrDefault( x => x.SteamId == steamId );

	/// <summary>What a member can still hold; a member whose record is not in memory can hold anything for now.</summary>
	static long MemberRoom( ulong steamId, OrgCurrency currency ) =>
		LiveRecord( steamId ) is { } record ? RoomOf( record, currency ) : long.MaxValue;

	static void CreditMember(
		ulong steamId, OrgCurrency currency, long amount, OrgRecord org, string orderId, string reason )
	{
		var connection = ConnectionOf( steamId );

		if ( connection is not null && PlayerProgress.For( connection ) is { Loaded: true }
	     && PlayerWallet.For( connection ) is { } wallet )
		{
			if ( currency == OrgCurrency.Crypto )
				wallet.AddCrypto( amount, StatMechanic.OrgPayout, $"{org.Name}: {reason}" );
			else
				wallet.AddCash( amount, StatMechanic.OrgPayout, $"{org.Name}: {reason}" );
		}
		else if ( PlayerProgression.Cache.Peek( steamId ) is { } record )
		{
			CreditRecord( steamId, record, currency, amount, $"{org.Name}: {reason}" );
			_ = PlayerProgression.SaveAsync( steamId );
		}
		else
		{
			OrgRegistry.AddPending( steamId, currency, amount, org.Name, orderId );
		}

		if ( connection is not null )
		{
			var unit = currency == OrgCurrency.Crypto ? "crypto" : "dollars";
			var order = string.IsNullOrEmpty( orderId ) ? "" : $" (order #{(orderId.Length > 8 ? orderId[..8] : orderId)})";
			GameManager.Current?.Notify( connection, $"You got {amount:N0} {unit} from {org.Name}{order}" );
		}
	}
}
