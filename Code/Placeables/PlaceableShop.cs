namespace Sandbox;

/// <summary>
/// Buying from the Depot, on the host. The caller names the buyer's connection (worked out from the computer's seat), an
/// object id and where to deliver it; the price, the delivery time, the delivery points and the limit all come from
/// <see cref="PlaceableCatalog"/>, the scene and the registry, never from the caller.
/// </summary>
public static class PlaceableShop
{
	/// <summary>
	/// Why this player cannot buy this object now, or null if they can. Whether store pickup is possible is checked
	/// separately (<see cref="Buy"/>) because it depends on the computer the order is placed at.
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
	/// Buy an object with cash. A home order is mailed to the buyer's mailbox and arrives after the object's delivery
	/// time; a store pickup is put down at a delivery point within <see cref="DeliverySpot.Range"/> of
	/// <paramref name="around"/> (the terminal the order is placed at) the moment it is paid for. The order is made first
	/// and the money taken second, and the order is taken back if the money cannot be taken, so cash is never spent for
	/// nothing. Host only.
	/// </summary>
	public static CrimeResult Buy(
		Connection buyer, string placeableId, long now, string destination = PlacedObjectRules.DeliveryHome,
		Vector3? around = null, Scene scene = null )
	{
		if ( !Networking.IsHost || PlaceableCatalog.Find( placeableId ) is not { } item )
			return CrimeResult.Fail( "Nothing happens." );

		if ( !PlacedObjectRules.IsDestination( destination ) )
			return CrimeResult.Fail( "Nothing happens." );

		scene ??= Game.ActiveScene;

		var at = around ?? default;
		var store = destination == PlacedObjectRules.DeliveryStore;

		if ( store && (around is null || !DeliverySpot.HasPointNear( scene, at )) )
			return CrimeResult.Fail( "There is no delivery point near this computer." );

		if ( WhyNot( buyer, item ) is { } reason )
			return CrimeResult.Fail( reason );

		// The room is checked before the money is taken, so a pickup the store cannot take is refused while the player
		// is still at the window and can order it home instead.
		if ( store && DeliverySpot.FindStore( scene, at ) is null )
			return CrimeResult.Fail( "There is no room at the delivery point." );

		var delivery = PlacedObjectRegistry.Order( (ulong)buyer.SteamId, item, now, destination, at );

		if ( delivery is null )
			return CrimeResult.Fail( "Too many deliveries are on their way." );

		if ( !Transactions.Pay( (ulong)buyer.SteamId, Parties.Depot, TransactionCurrency.Cash, item.Price,
			    TxKind.DepotPurchase, $"Depot: {item.Name}", item.Id ).Ok )
		{
			PlacedObjectRegistry.Cancel( delivery );
			return CrimeResult.Fail( "Not enough cash." );
		}

		StatsLog.Purchase( buyer, "placeable", item.Id, item.Name, item.Price );

		if ( store )
		{
			// Now, in this call, so the object stands there by the time the window redraws. If another order took the
			// room in between, it waits like any other order and arrives on the next tick.
			PlacedObjectSystem.Deliver( scene, delivery, new List<Vector3>() );

			return CrimeResult.Success( delivery.Arrived
				? $"Bought a {item.Name} for ${item.Price:N0}. It is {delivery.Where}."
				: $"Bought a {item.Name} for ${item.Price:N0}. It is waiting for a place at the delivery point." );
		}

		return CrimeResult.Success(
			$"Bought a {item.Name} for ${item.Price:N0}. It will be delivered at {WorldClock.TimeOfDayOf( delivery.ArriveAt )}." );
	}
}
