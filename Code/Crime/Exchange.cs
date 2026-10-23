namespace Sandbox;

/// <summary>
/// The only place crypto and dollars change into each other (the Hush app's Exchange tab). The base rate is one to one;
/// the Exchange keeps a fee, and cashing out takes time (the crypto is gone at once, the dollars arrive later). Credits
/// are never touched. Every method is host only and checks everything itself: the caller passes only the amount.
/// </summary>
public static class Exchange
{
	/// <summary>
	/// Sell crypto for dollars: take the crypto now, and queue the dollars (after the fee) to arrive after the delay.
	/// </summary>
	public static CrimeResult TrySell( PlayerWallet wallet, CrimeRecord crime, long crypto )
	{
		if ( !Networking.IsHost || wallet is null || crime is null )
			return CrimeResult.Fail( "The Exchange is closed." );

		if ( crypto < 1 || crypto > CrimeCatalog.MaxExchange )
			return CrimeResult.Fail( "Pick an amount." );

		var cash = CrimeCatalog.CashFor( crypto );

		if ( cash < 1 )
			return CrimeResult.Fail( "That is too little to cash out." );

		if ( crime.Cashouts.Count >= CrimeRecord.MaxEntries )
			return CrimeResult.Fail( "Too many cash-outs waiting." );

		if ( !wallet.TrySpendCrypto( crypto ) )
			return CrimeResult.Fail( "Not enough crypto." );

		crime.Cashouts.Add( new() { Cash = cash, ReadyAt = WorldClock.Now + CrimeCatalog.CashoutDelaySeconds } );
		var minutes = CrimeCatalog.CashoutDelaySeconds / 60f;
		return CrimeResult.Success( $"Cashing out {crypto:N0} crypto. ${cash:N0} arrives in {minutes:0.#} min." );
	}

	/// <summary>
	/// Buy crypto with dollars, at once, after the fee.
	/// </summary>
	public static CrimeResult TryBuy( PlayerWallet wallet, long cash )
	{
		if ( !Networking.IsHost || wallet is null )
			return CrimeResult.Fail( "The Exchange is closed." );

		if ( cash < 1 || cash > CrimeCatalog.MaxExchange )
			return CrimeResult.Fail( "Pick an amount." );

		var crypto = CrimeCatalog.CryptoFor( cash );

		if ( crypto < 1 )
			return CrimeResult.Fail( "That is too little to change." );

		if ( !wallet.CanHoldCrypto( crypto ) )
			return CrimeResult.Fail( "Your wallet can't hold that much crypto." );

		if ( !wallet.TrySpendCash( cash ) )
			return CrimeResult.Fail( "Not enough dollars." );

		wallet.AddCrypto( crypto );
		return CrimeResult.Success( $"Bought {crypto:N0} crypto for ${cash:N0}." );
	}

	/// <summary>
	/// Pay the cash-outs whose delay is over by <paramref name="now"/>, into the record. Needs no player: the offline
	/// processor calls it too. A payment the record cannot hold yet waits. Returns whether any was paid. Host only.
	/// </summary>
	public static bool Settle( ProgressRecord record, long now )
	{
		if ( !Networking.IsHost || record?.Crime is null )
			return false;

		var due = record.Crime.Cashouts.Where( x => x.ReadyAt <= now && record.CanHoldCash( x.Cash ) ).ToList();

		foreach ( var cashout in due )
		{
			record.AddCash( cashout.Cash );
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
