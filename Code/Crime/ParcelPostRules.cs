namespace Sandbox;

/// <summary>
/// What happens when a player posts a parcel, on records alone (no player, no inventory), so it can be tested. A parcel
/// is like a real one: the sender who packed it (<see cref="ItemData.SenderId"/>) and the buyer it is for are written on
/// it, and whoever posts it, it is delivered and the sender is paid.
/// </summary>
/// <remarks>
/// <para>
/// <b>Source of truth.</b> The sender's <see cref="OrderRecord"/> decides everything that matters: whether the parcel may
/// ship (the order must be <see cref="OrderState.Packed"/> and in time, so a duplicate can never pay twice), the kind,
/// count, quality, price, deadline and the buyer's personality, and so the review and the payment. The numbers written
/// on the parcel (<see cref="ItemData.OrderKind"/> and the rest) only describe it and are checked against the record; a
/// parcel that disagrees is refused. Nothing on a parcel is ever paid out.
/// </para>
/// <para>
/// The shipping record (<see cref="ParcelRecord"/>) is made in the <i>sender's</i> <see cref="CrimeRecord"/>, so the
/// post, the delivery, the buyer's review, the escrow and the payment are the very same
/// <see cref="CrimeRules.Advance"/> as for the sender's own parcels, online or away. The poster only carries it to the
/// mailbox and keeps a <see cref="PostedRecord"/> so their own post limit counts it. Host only.
/// </para>
/// </remarks>
public static class ParcelPostRules
{
	/// <summary>
	/// Whether a parcel may be posted against an order record, and if not, why.
	/// </summary>
	public enum Verdict
	{
		/// <summary>The order is packed and in time and the parcel matches it.</summary>
		Ship,

		/// <summary>The sender has no such order (cancelled and cleared, season wiped, never existed, unknown sender).</summary>
		NoOrder,

		/// <summary>The order is not waiting for a parcel: already shipped, delivered, paid, cancelled or not packed.</summary>
		NotPacked,

		/// <summary>The order ran out of time.</summary>
		TooLate,

		/// <summary>What the parcel says it holds does not match the order.</summary>
		Mismatch,
	}

	/// <summary>
	/// The player who is paid for a parcel: the sender written on it, or the poster for a parcel that has none (one packed
	/// before senders were kept).
	/// </summary>
	public static ulong SenderOf( ItemData parcel, ulong poster ) =>
		parcel is { SenderId: > 0 } ? parcel.SenderId : poster;

	/// <summary>
	/// Whether the parcel may ship against <paramref name="senderCrime"/> (the record of <see cref="SenderOf"/>; null when
	/// that record is unknown). Decides on the order record; the parcel's own numbers are only compared.
	/// </summary>
	public static Verdict Judge( ItemData parcel, CrimeRecord senderCrime, long now )
	{
		if ( parcel is not { Kind: ItemCatalog.Parcel } || senderCrime is null || string.IsNullOrEmpty( parcel.OrderId ) )
			return Verdict.NoOrder;

		if ( senderCrime.Orders.FirstOrDefault( x => x.Id == parcel.OrderId ) is not { } order )
			return Verdict.NoOrder;

		if ( order.State != OrderState.Packed )
			return Verdict.NotPacked;

		if ( now > order.Deadline )
			return Verdict.TooLate;

		if ( !string.IsNullOrEmpty( parcel.OrderKind ) && parcel.OrderKind != order.Kind )
			return Verdict.Mismatch;

		if ( parcel.OrderCount > 0 && parcel.OrderCount != order.Count )
			return Verdict.Mismatch;

		// What is inside, when the parcel says: the ordered kind and count (an old parcel has no contents).
		if ( parcel.Contents is { Count: > 0 } contents
		     && (contents.Any( x => x.Kind != order.Kind ) || contents.Sum( x => (long)x.Count ) != order.Count) )
			return Verdict.Mismatch;

		return Verdict.Ship;
	}

	/// <summary>
	/// The sentence told to the poster when the parcel is not posted. Says nothing about the sender's money.
	/// </summary>
	public static string TextFor( Verdict verdict ) => verdict switch
	{
		Verdict.NoOrder => "Nobody is waiting for this parcel any more.",
		Verdict.NotPacked => "This parcel was already posted, or its order is closed.",
		Verdict.TooLate => "It is too late: the buyer no longer wants this parcel.",
		Verdict.Mismatch => "This parcel doesn't match its order.",
		_ => "",
	};

	/// <summary>
	/// Put the parcel on its way in the sender's record: the order goes Packed -> Shipped and the post gets the parcel,
	/// with the order's own kind, count and packed quality. The caller has had <see cref="Judge"/> say Ship. Returns the
	/// shipping record, or null if the order is not shippable.
	/// </summary>
	public static ParcelRecord Ship( CrimeRecord senderCrime, string orderId )
	{
		if ( senderCrime.Orders.FirstOrDefault( x => x.Id == orderId ) is not { State: OrderState.Packed } order )
			return null;

		order.State = OrderState.Shipped;
		return PostalSystem.Send( senderCrime, order.Id, order.Kind, order.Count, order.PackedQuality );
	}

	/// <summary>
	/// Write the sender and the order's facts on a new parcel.
	/// </summary>
	public static void Stamp( ItemData parcel, ulong senderId, string senderName, OrderRecord order )
	{
		parcel.SenderId = senderId;
		parcel.SenderName = senderName ?? "";
		parcel.OrderKind = order.Kind ?? "";
		parcel.OrderCount = order.Count;
		parcel.OrderPrice = order.Total;
		parcel.OrderDeadline = order.Deadline;
	}

	/// <summary>
	/// How many parcels the poster posted for others that are still on their way. Counts toward the poster's limit.
	/// </summary>
	public static int ActivePosted( CrimeRecord poster, long now ) => poster.Posted.Count( x => x.DeliverAt > now );

	/// <summary>
	/// Remember a parcel the poster posted for another player, and forget the ones already delivered.
	/// </summary>
	public static void RecordPosted( CrimeRecord poster, ulong senderId, ParcelRecord parcel, long now )
	{
		poster.Posted.RemoveAll( x => x.DeliverAt <= now );
		poster.Posted.Add( new() { ParcelId = parcel.Id, SenderId = senderId, DeliverAt = parcel.DeliverAt } );

		if ( poster.Posted.Count > CrimeRecord.MaxEntries )
			poster.Posted.RemoveAt( 0 );
	}

	/// <summary>
	/// The poster's whole load on the post: their own parcels not yet delivered and the ones they posted for others.
	/// <see cref="CrimeCatalog.MaxParcels"/> limits this.
	/// </summary>
	public static int PostLoad( CrimeRecord poster, long now ) =>
		PostalSystem.InPost( poster ) + ActivePosted( poster, now );
}
