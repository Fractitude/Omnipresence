namespace Sandbox;

/// <summary>
/// Buying from the Depot, on the host. The caller names the buyer's connection (worked out from the computer's seat) and
/// an object id; the price, the delivery time and the limit all come from <see cref="PlaceableCatalog"/> and the
/// registry, never from the caller.
/// </summary>
public static class PlaceableShop
{
	/// <summary>
	/// Why this player cannot buy this object now, or null if they can.
	/// </summary>
	public static string WhyNot( Connection buyer, Placeable item )
	{
		if ( item is null || buyer is null )
			return "Nothing happens.";

		if ( !PlacedObjectRegistry.CanOrder( (ulong)buyer.SteamId ) )
			return "Too many deliveries are on their way.";

		return PlayerWallet.For( buyer ) is { } wallet && wallet.Cash >= item.Price ? null : "Not enough cash.";
	}

	/// <summary>
	/// Buy an object with cash and put it on its way. The order is made first and the money taken second, and the order
	/// is taken back if the money cannot be taken, so cash is never spent for nothing. Host only.
	/// </summary>
	public static CrimeResult Buy( Connection buyer, string placeableId, long now )
	{
		if ( !Networking.IsHost || PlaceableCatalog.Find( placeableId ) is not { } item )
			return CrimeResult.Fail( "Nothing happens." );

		if ( WhyNot( buyer, item ) is { } reason )
			return CrimeResult.Fail( reason );

		var delivery = PlacedObjectRegistry.Order( (ulong)buyer.SteamId, item, now );

		if ( delivery is null )
			return CrimeResult.Fail( "Too many deliveries are on their way." );

		if ( !PlayerWallet.For( buyer ).TrySpendCash( item.Price ) )
		{
			PlacedObjectRegistry.Cancel( delivery );
			return CrimeResult.Fail( "Not enough cash." );
		}

		return CrimeResult.Success(
			$"Bought a {item.Name} for ${item.Price:N0}. It will be delivered at {WorldClock.TimeOfDayOf( delivery.ArriveAt )}." );
	}
}
