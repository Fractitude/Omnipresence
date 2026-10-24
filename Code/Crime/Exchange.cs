namespace Sandbox;

/// <summary>
/// The only place crypto and dollars change into each other. The rate is one to one and the venue keeps a fee
/// (<see cref="ExchangeVenue"/>); selling may make the player wait for the dollars (the crypto is gone at once, the
/// dollars arrive later). Credits are never touched. Every method is host only and checks everything itself: the
/// caller passes only the amount, and the venue is one from <see cref="ExchangeCatalog"/>, never one a panel made up.
/// </summary>
public static class Exchange
{
	/// <summary>
	/// Sell crypto for dollars at <paramref name="venue"/>: take the crypto now, and hand over the dollars after the fee,
	/// at once or after the venue's delay.
	/// </summary>
	public static CrimeResult TrySell( PlayerWallet wallet, CrimeRecord crime, ExchangeVenue venue, long crypto )
	{
		if ( !Networking.IsHost || wallet is null || venue is null )
			return CrimeResult.Fail( "The exchange is closed." );

		if ( crypto < 1 )
			return CrimeResult.Fail( "Pick an amount." );

		if ( crypto > venue.MaxPerRequest )
			return CrimeResult.Fail( $"At most {venue.MaxPerRequest:N0} crypto at a time." );

		var cash = venue.CashFor( crypto );

		if ( cash < 1 )
			return CrimeResult.Fail( "That is too little to cash out." );

		// A venue that pays at once needs no queue: the dollars go straight in.
		if ( venue.PaysAtOnce )
		{
			if ( !wallet.CanHoldCash( cash ) )
				return CrimeResult.Fail( "Your account can't hold that much." );

			if ( !wallet.TrySpendCrypto( crypto, StatMechanic.Exchange, $"Sold at {venue.Name}" ) )
				return CrimeResult.Fail( "Not enough crypto." );

			wallet.AddCash( cash, StatMechanic.Exchange, $"Sold crypto at {venue.Name}" );
			return CrimeResult.Success( $"Sold {crypto:N0} crypto for ${cash:N0}." );
		}

		// A venue that pays later needs somewhere to keep the cash-out until it is due.
		if ( crime is null )
			return CrimeResult.Fail( "The exchange can't hold a cash-out for you." );

		if ( crime.Cashouts.Count >= CrimeRecord.MaxEntries )
			return CrimeResult.Fail( "Too many cash-outs waiting." );

		if ( !wallet.TrySpendCrypto( crypto, StatMechanic.Exchange, $"Sold at {venue.Name}, cash-out waiting" ) )
			return CrimeResult.Fail( "Not enough crypto." );

		crime.Cashouts.Add( new() { Cash = cash, ReadyAt = WorldClock.Now + venue.CashoutDelaySeconds } );
		var minutes = venue.CashoutDelaySeconds / 60f;
		return CrimeResult.Success( $"Cashing out {crypto:N0} crypto. ${cash:N0} arrives in {minutes:0.#} min." );
	}

	/// <summary>
	/// Buy crypto with dollars at <paramref name="venue"/>, at once, after the fee.
	/// </summary>
	public static CrimeResult TryBuy( PlayerWallet wallet, ExchangeVenue venue, long cash )
	{
		if ( !Networking.IsHost || wallet is null || venue is null )
			return CrimeResult.Fail( "The exchange is closed." );

		if ( cash < 1 )
			return CrimeResult.Fail( "Pick an amount." );

		if ( cash > venue.MaxPerRequest )
			return CrimeResult.Fail( $"At most ${venue.MaxPerRequest:N0} at a time." );

		var crypto = venue.CryptoFor( cash );

		if ( crypto < 1 )
			return CrimeResult.Fail( "That is too little to change." );

		if ( !wallet.CanHoldCrypto( crypto ) )
			return CrimeResult.Fail( "Your wallet can't hold that much crypto." );

		if ( !wallet.TrySpendCash( cash, StatMechanic.Exchange, $"Bought crypto at {venue.Name}" ) )
			return CrimeResult.Fail( "Not enough dollars." );

		wallet.AddCrypto( crypto, StatMechanic.Exchange, $"Bought at {venue.Name}" );
		return CrimeResult.Success( $"Bought {crypto:N0} crypto for ${cash:N0}." );
	}

	/// <summary>
	/// The name of a connected player for the stats log, or empty.
	/// </summary>
	internal static string SteamName( ulong steamId ) =>
		Connection.All.FirstOrDefault( x => x.SteamId == steamId )?.DisplayName ?? "";

	/// <summary>
	/// Pay the cash-outs whose delay is over by <paramref name="now"/>, into the record. Needs no player: the offline
	/// processor calls it too. A payment the record cannot hold yet waits. Returns whether any was paid. Host only.
	/// </summary>
	public static bool Settle( ulong steamId, ProgressRecord record, long now )
	{
		if ( !Networking.IsHost || record?.Crime is null )
			return false;

		var due = record.Crime.Cashouts.Where( x => x.ReadyAt <= now && record.CanHoldCash( x.Cash ) ).ToList();

		foreach ( var cashout in due )
		{
			var before = record.Money;
			record.AddCash( cashout.Cash );
			StatsLog.Money( steamId, SteamName( steamId ), StatsLog.MoneyCash, record.Money - before, record.Money,
				StatMechanic.Exchange, "Cash-out arrived" );
			record.Crime.Cashouts.Remove( cashout );
		}

		return due.Count > 0;
	}
}

/// <summary>
/// What a crime action says back: whether it worked, and a short sentence for the player.
/// </summary>
public readonly record struct CrimeResult( bool Ok, string Message )
{
	public static CrimeResult Success( string message = "" ) => new( true, message );

	public static CrimeResult Fail( string message ) => new( false, message );
}
