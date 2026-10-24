namespace Sandbox;

/// <summary>
/// A leg with the records it works on and the amount that will really move.
/// </summary>
public sealed class PlannedLeg
{
	public TxLeg Leg { get; init; }

	/// <summary>The source player's record, null for a system source.</summary>
	public ProgressRecord FromRecord { get; init; }

	/// <summary>The destination player's record, null for a system destination.</summary>
	public ProgressRecord ToRecord { get; init; }

	/// <summary>What moves: <see cref="TxLeg.Amount"/>, or less for an <see cref="TxLeg.UpTo"/> leg.</summary>
	public long Amount { get; set; }
}

/// <summary>
/// The checks and the arithmetic of a transaction, with nothing from the game in them: they work on
/// <see cref="ProgressRecord"/>s. <see cref="Plan"/> checks a whole batch against the records without changing them;
/// <see cref="Apply"/> then changes them. A plan that passes cannot fail in Apply, which is what makes a batch atomic.
/// </summary>
public static class TransactionRules
{
	/// <summary>The most one leg can move: what an account can hold at most.</summary>
	public const long MaxAmount = ProgressRecord.MaxMoney;

	public static long CapOf( TransactionCurrency currency ) =>
		currency == TransactionCurrency.Crypto ? ProgressRecord.MaxCrypto : ProgressRecord.MaxMoney;

	public static long BalanceOf( ProgressRecord record, TransactionCurrency currency ) =>
		currency == TransactionCurrency.Crypto ? record.Crypto : record.Money;

	/// <summary>
	/// Everything that can be checked about a leg without looking at a balance.
	/// </summary>
	public static TxFailure Validate( TxLeg leg )
	{
		if ( leg is null )
			return TxFailure.InvalidAmount;

		if ( leg.Amount <= 0 || leg.Amount > MaxAmount )
			return TxFailure.InvalidAmount;

		if ( !System.Enum.IsDefined( leg.Currency ) || !System.Enum.IsDefined( leg.Kind ) )
			return TxFailure.InvalidKind;

		if ( !PartyOk( leg.From ) || !PartyOk( leg.To ) )
			return TxFailure.UnknownParty;

		if ( leg.From == leg.To )
			return TxFailure.SelfTransfer;

		// Nobody pays themselves, and a system party does not pay a system party: no balance moves.
		if ( !leg.From.IsPlayer && !leg.To.IsPlayer )
			return TxFailure.UnknownParty;

		if ( leg.UpTo && leg.From.IsPlayer )
			return TxFailure.InvalidKind;

		return TxFailure.None;
	}

	static bool PartyOk( Party party ) =>
		party.IsPlayer ? string.IsNullOrEmpty( party.Id ) : Parties.IsKnown( party.Id );

	/// <summary>
	/// Check the whole batch against the records, in order, and set each leg's amount. Changes nothing. The balances
	/// are worked out over the batch together (a player who is paid by one leg can pay with another).
	/// </summary>
	public static TxFailure Plan( IReadOnlyList<PlannedLeg> legs )
	{
		if ( legs is null || legs.Count == 0 )
			return TxFailure.Empty;

		var net = new Dictionary<(ProgressRecord, TransactionCurrency), long>();

		foreach ( var planned in legs )
		{
			var failure = Validate( planned.Leg );

			if ( failure != TxFailure.None )
				return failure;

			if ( planned.Leg.From.IsPlayer && planned.FromRecord is null
			     || planned.Leg.To.IsPlayer && planned.ToRecord is null )
				return TxFailure.Unavailable;

			var amount = planned.Leg.Amount;
			var currency = planned.Leg.Currency;

			if ( planned.Leg.UpTo && planned.ToRecord is { } receiver )
			{
				var running = net.GetValueOrDefault( (receiver, currency) );
				var room = CapOf( currency ) - (BalanceOf( receiver, currency ) + running);
				amount = System.Math.Clamp( room, 0, amount );
			}

			planned.Amount = amount;

			if ( planned.FromRecord is { } source )
				net[(source, currency)] = checked(net.GetValueOrDefault( (source, currency) ) - amount);

			if ( planned.ToRecord is { } target )
				net[(target, currency)] = checked(net.GetValueOrDefault( (target, currency) ) + amount);
		}

		foreach ( var ((record, currency), delta) in net )
		{
			var after = checked(BalanceOf( record, currency ) + delta);

			if ( after < 0 )
				return TxFailure.Insufficient;

			if ( after > CapOf( currency ) )
				return TxFailure.CapExceeded;
		}

		return TxFailure.None;
	}

	/// <summary>
	/// Move the money of a plan that <see cref="Plan"/> passed. A leg of zero (an <see cref="TxLeg.UpTo"/> leg with no
	/// room) moves nothing.
	/// </summary>
	public static void Apply( IReadOnlyList<PlannedLeg> legs )
	{
		// The net change of each balance, applied once: a batch only has to balance as a whole, so a leg may spend what
		// another leg of the same batch pays in, and applying them one by one would dip below zero.
		var net = new Dictionary<(ProgressRecord, TransactionCurrency), long>();

		foreach ( var planned in legs.Where( x => x.Amount > 0 ) )
		{
			var currency = planned.Leg.Currency;

			if ( planned.FromRecord is { } source )
				net[(source, currency)] = net.GetValueOrDefault( (source, currency) ) - planned.Amount;

			if ( planned.ToRecord is { } target )
				net[(target, currency)] = net.GetValueOrDefault( (target, currency) ) + planned.Amount;
		}

		foreach ( var ((record, currency), delta) in net )
			Move( record, currency, delta );
	}

	static void Move( ProgressRecord record, TransactionCurrency currency, long delta ) =>
		record.Settle( currency, delta );

	/// <summary>
	/// Add one entry to a record's history, keeping the newest <see cref="TransactionEntry.MaxKept"/>.
	/// </summary>
	public static void Remember( ProgressRecord record, TransactionEntry entry )
	{
		record.Transactions ??= new();
		record.Transactions.Add( entry );

		if ( record.Transactions.Count > TransactionEntry.MaxKept )
			record.Transactions.RemoveRange( 0, record.Transactions.Count - TransactionEntry.MaxKept );
	}
}
