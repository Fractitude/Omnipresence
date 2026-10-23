namespace Sandbox;

/// <summary>
/// The post: the mailman collects parcels from mailboxes at fixed times on the game clock (every
/// <see cref="CrimeCatalog.CollectPeriod"/> seconds), and a collected parcel takes <see cref="CrimeCatalog.TransitSeconds"/>
/// to arrive. Sending just before a collection is faster. Host only; <see cref="PlayerCrime"/> ticks it once a second, the
/// way <see cref="PlayerProgress"/> delivers store packages.
/// </summary>
public static class PostalSystem
{
	/// <summary>
	/// Put a parcel in the mailbox: the times are worked out from the clock, never from the caller. Returns it.
	/// </summary>
	public static ParcelRecord Send( CrimeRecord crime, string orderId, string kind, int count, int quality )
	{
		var now = WorldClock.Now;
		var collect = CrimeCatalog.NextCollection( now );

		var parcel = new ParcelRecord
		{
			Id = $"p{crime.NextId++}",
			OrderId = orderId ?? "",
			Kind = kind,
			Count = count,
			Quality = quality,
			State = ParcelState.Waiting,
			SentAt = now,
			CollectAt = collect,
			DeliverAt = collect + CrimeCatalog.TransitSeconds,
		};

		crime.Parcels.Add( parcel );

		if ( crime.Parcels.Count > CrimeRecord.MaxEntries )
			crime.Parcels.RemoveAt( 0 );

		return parcel;
	}

	/// <summary>
	/// Whether the player's mailbox flag should be up: a parcel waits for the mailman.
	/// </summary>
	public static bool HasWaiting( CrimeRecord crime ) => crime.Parcels.Any( x => x.State == ParcelState.Waiting );

	/// <summary>
	/// Parcels that are not delivered yet.
	/// </summary>
	public static int InPost( CrimeRecord crime ) => crime.Parcels.Count( x => x.State != ParcelState.Delivered );

	/// <summary>
	/// Move the parcels along: collected when the mailman comes, delivered when their time is up. Calls
	/// <paramref name="delivered"/> for each parcel that arrives. Returns whether anything changed.
	/// </summary>
	public static bool Tick( CrimeRecord crime, System.Action<ParcelRecord> delivered )
	{
		var now = WorldClock.Now;
		var changed = false;

		foreach ( var parcel in crime.Parcels.ToList() )
		{
			if ( parcel.State == ParcelState.Waiting && now >= parcel.CollectAt )
			{
				parcel.State = ParcelState.InTransit;
				changed = true;
			}

			if ( parcel.State == ParcelState.InTransit && now >= parcel.DeliverAt )
			{
				parcel.State = ParcelState.Delivered;
				changed = true;
				delivered?.Invoke( parcel );
			}
		}

		return changed;
	}
}
