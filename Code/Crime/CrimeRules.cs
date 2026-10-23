namespace Sandbox;

/// <summary>
/// What <see cref="CrimeRules.Advance"/> did to a record, for whoever called it to show and save.
/// </summary>
public sealed class CrimeAdvance
{
	/// <summary>Anything in the record changed (crime state, balances or hardware).</summary>
	public bool Changed { get; set; }

	/// <summary>Hardware packages arrived in the inventory.</summary>
	public bool HardwareChanged { get; set; }

	/// <summary>Orders that were cancelled. A parcel for one is still in the bag of a player who is playing.</summary>
	public List<string> CancelledOrders { get; } = new();

	/// <summary>Sentences for the player, if they are there to hear them.</summary>
	public List<string> Messages { get; } = new();
}

/// <summary>
/// The time rules of the crime game, on a <see cref="ProgressRecord"/> alone: the post, shop deliveries, the buyer's
/// review, escrow, deadlines, the Exchange's cash-outs and hardware shipments. No component, no player, no inventory.
/// The player's own <see cref="PlayerCrime"/> runs them once a second; the <see cref="OfflineCrimeSystem"/> runs the
/// very same function for a record whose player is away, and a player who comes back is caught up by the same call. So
/// the result does not depend on who runs it or how often: every event happens at its own time (a review is stamped
/// with the delivery, escrow releases at the delivery plus the delay) and the buyer's dice are seeded by the order.
/// Host only.
/// </summary>
public static class CrimeRules
{
	/// <summary>
	/// Seconds before an event that could not happen (a wallet that is full) is tried again.
	/// </summary>
	public const long RetrySeconds = 60;

	/// <summary>
	/// Bring a record up to <paramref name="now"/>. Safe to call as often as you like: an event that has happened is
	/// not applied again, because its state changes in the same call as its effect.
	/// </summary>
	public static CrimeAdvance Advance( ulong steamId, ProgressRecord record, long now )
	{
		var result = new CrimeAdvance();
		var crime = record.Crime;

		if ( record.DeliverShipments( now ) )
			result.Changed = result.HardwareChanged = true;

		result.Changed |= PostalSystem.Tick( crime, now, parcel => OnParcelDelivered( steamId, crime, parcel ) );
		result.Changed |= PostalSystem.TickDeliveries( crime, now );
		result.Changed |= CheckOrders( steamId, record, now, result );
		result.Changed |= Exchange.Settle( record, now );

		if ( result.Changed )
			TidyReviews( crime );

		return result;
	}

	/// <summary>
	/// The game time of the next thing that is due in this record, or null when nothing is waiting on the clock. A time
	/// that is already past means it is due now (or, after <see cref="Advance"/>, that it could not happen yet).
	/// </summary>
	public static long? NextDue( ProgressRecord record )
	{
		long? due = null;

		void Consider( long at ) => due = due is null ? at : System.Math.Min( due.Value, at );

		foreach ( var shipment in record.Shipments )
			Consider( shipment.ArriveAt );

		var crime = record.Crime;

		foreach ( var parcel in crime.Parcels )
		{
			if ( parcel.State == ParcelState.Waiting )
				Consider( parcel.CollectAt );
			else if ( parcel.State == ParcelState.InTransit )
				Consider( parcel.DeliverAt );
		}

		foreach ( var delivery in crime.Deliveries.Where( x => x.State == DeliveryState.InTransit ) )
			Consider( delivery.ArriveAt );

		foreach ( var cashout in crime.Cashouts )
			Consider( cashout.ReadyAt );

		foreach ( var order in crime.Orders )
		{
			switch ( order.State )
			{
				case OrderState.Accepted:
				case OrderState.Packed:
					Consider( order.Deadline + 1 );
					break;
				case OrderState.Delivered:
					Consider( order.ReleaseAt );
					break;
				case OrderState.Shipped:
					Consider( crime.Parcels.Any( x => x.OrderId == order.Id && x.State != ParcelState.Delivered )
						? order.Deadline + CrimeCatalog.ShippedMaxSeconds + 1
						: 0 );
					break;
			}
		}

		return due;
	}

	// -- Orders -------------------------------------------------------------------------------------------------------

	/// <summary>
	/// Complete sent orders, release escrow (in the order it falls due, since the fee depends on the good trades so
	/// far) and cancel the orders that ran out of time.
	/// </summary>
	static bool CheckOrders( ulong steamId, ProgressRecord record, long now, CrimeAdvance result )
	{
		var crime = record.Crime;
		var changed = false;

		foreach ( var order in crime.Orders.Where( x => x.State == OrderState.Shipped ).ToList() )
			changed |= CheckShipped( steamId, crime, order, now, result );

		foreach ( var order in crime.Orders
			         .Where( x => x.State == OrderState.Delivered && now >= x.ReleaseAt )
			         .OrderBy( x => x.ReleaseAt )
			         .ToList() )
		{
			changed |= Release( record, order );
		}

		foreach ( var order in crime.Orders
			         .Where( x => x.State is OrderState.Accepted or OrderState.Packed && now > x.Deadline )
			         .OrderBy( x => x.Deadline )
			         .ToList() )
		{
			Cancel( steamId, crime, order, result );
			changed = true;
		}

		return changed;
	}

	/// <summary>
	/// A parcel arrived. If it was for an order that was shipped, the buyer reviews it now (at the time it arrived) and
	/// the payment is released after the escrow delay. The deadline only governs sending: a parcel that comes after it
	/// is still paid for, and rated lower.
	/// </summary>
	static void OnParcelDelivered( ulong steamId, CrimeRecord crime, ParcelRecord parcel )
	{
		var order = crime.Orders.FirstOrDefault( x => x.Id == parcel.OrderId );

		if ( order is not { State: OrderState.Shipped } )
			return;

		order.State = OrderState.Delivered;
		order.ReleaseAt = parcel.DeliverAt + CrimeCatalog.EscrowSeconds;

		// The part of the delivery window the parcel took, from accepting to arriving.
		var window = (double)CrimeCatalog.DeliverWindowSeconds;
		var timeUsed = System.Math.Clamp( (parcel.DeliverAt - (order.Deadline - window)) / window, 0, 1 );
		var secondsLate = parcel.DeliverAt - order.Deadline;
		var random = SeededRandom( steamId, order.Id, "review" );

		var stars = CrimeCatalog.Stars(
			order.Personality, order.MinQuality, order.PackedQuality, secondsLate, timeUsed, random );

		AddReview(
			crime, parcel.DeliverAt, order, stars,
			CrimeCatalog.PickReview( order.Personality, stars, secondsLate > 0, random ) );
	}

	/// <summary>
	/// A sent order is never cancelled by the clock for being late, but one whose parcel is gone, or never completes,
	/// would stay sent forever and block a slot. A missing parcel, or one that waited too long past the deadline, is
	/// lost in the post (no penalty: it is not the player's fault); a parcel that is delivered while its order was
	/// missed is completed through the normal path, once.
	/// </summary>
	static bool CheckShipped( ulong steamId, CrimeRecord crime, OrderRecord order, long now, CrimeAdvance result )
	{
		var parcel = crime.Parcels.FirstOrDefault( x => x.OrderId == order.Id );

		if ( parcel is { State: ParcelState.Delivered } )
		{
			OnParcelDelivered( steamId, crime, parcel );
			return true;
		}

		if ( parcel is not null && now <= order.Deadline + CrimeCatalog.ShippedMaxSeconds )
			return false;

		order.State = OrderState.Cancelled;
		Log.Info( $"CrimeRules: order {order.Id} lost in the post" );
		result.Messages.Add( "Your parcel was lost in the post. The order is cancelled." );
		return true;
	}

	/// <summary>
	/// Pay what escrow held, less the reputation fee. Waits (returns false) while the record cannot hold the payment.
	/// </summary>
	static bool Release( ProgressRecord record, OrderRecord order )
	{
		var crime = record.Crime;
		var gross = order.Total;
		var fee = gross * CrimeCatalog.FeeFor( crime ) / 100;
		var paid = gross - fee;

		if ( !record.CanHoldCrypto( paid ) )
			return false;

		record.AddCrypto( paid );
		order.Paid = paid;
		order.State = OrderState.Released;
		crime.GoodTrades++;
		return true;
	}

	/// <summary>
	/// The order is dead at its deadline: it costs reputation (a bad trade and a one star review). The caller takes a
	/// parcel for it out of the bag, since nobody wants it.
	/// </summary>
	static void Cancel( ulong steamId, CrimeRecord crime, OrderRecord order, CrimeAdvance result )
	{
		order.State = OrderState.Cancelled;
		crime.BadTrades++;

		AddReview(
			crime, order.Deadline, order, CrimeCatalog.CancelStars,
			CrimeCatalog.PickCancelReview( SeededRandom( steamId, order.Id, "cancel" ) ) );

		Log.Info( $"CrimeRules: order {order.Id} cancelled: the deadline passed" );
		result.CancelledOrders.Add( order.Id );
	}

	static void AddReview( CrimeRecord crime, long at, OrderRecord order, int stars, string text )
	{
		crime.Reviews.Add( new() { At = at, Handle = order.Handle, Stars = stars, Text = text } );
		crime.RatingSum += stars;
		crime.RatingCount++;
	}

	/// <summary>
	/// Keep the reviews in the order they happened (a catch-up can add an older one after a newer one) and only the
	/// newest few.
	/// </summary>
	static void TidyReviews( CrimeRecord crime )
	{
		crime.Reviews = crime.Reviews.OrderBy( x => x.At ).TakeLast( CrimeRecord.MaxReviews ).ToList();
	}

	/// <summary>
	/// The dice of one event of one order of one player: the same every time they are thrown, so a catch-up after a
	/// crash gives the same stars as the first time. A stable hash, since <c>string.GetHashCode</c> differs per
	/// process.
	/// </summary>
	static System.Random SeededRandom( ulong steamId, string orderId, string salt )
	{
		var hash = 2166136261u;

		foreach ( var c in $"{steamId}/{orderId}/{salt}" )
			hash = unchecked((hash ^ c) * 16777619u);

		return new System.Random( unchecked((int)hash) );
	}
}
