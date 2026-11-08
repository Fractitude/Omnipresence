namespace Sandbox;

/// <summary>What a purchase at the dealership came to. <see cref="Message"/> is what the player reads either way.</summary>
public sealed record CarPurchase( bool Ok, string Message, GameObject Car = null )
{
	public static CarPurchase Fail( string message ) => new( false, message );
}

/// <summary>
/// The dealership's sale, on the host: a car costs cash, is made at a free delivery point near the computer
/// (<see cref="CarDelivery"/>) and belongs to its buyer (<see cref="OwnershipComponent"/>). The room is checked before
/// the money is taken, and the money is given back if the car cannot be made, so cash is never spent for nothing.
/// </summary>
public static class CarDealer
{
	public const string NoRoom = "There is no free space to deliver a car to.";
	public const string NoPoint = "There is no delivery point near this computer.";

	/// <summary>The tag the dealership's computer has, and the app's manifest lists as its <c>ComputerTags</c>.</summary>
	public const string ComputerTag = "store";

	/// <summary>Does this computer carry the tag? The app only works on one that does.</summary>
	public static bool IsDealership( NaviComputer computer ) =>
		computer?.ComputerTags?.Any( x => string.Equals( x?.Trim(), ComputerTag, System.StringComparison.OrdinalIgnoreCase ) )
		== true;

	/// <summary>
	/// Buy a car. <paramref name="around"/> is where the computer stands. Host only. The assets of an integrated sent
	/// have to be mounted already (<see cref="SentLocal.Mount"/>), which is asynchronous, so the caller does that first
	/// and calls this in the same step it checks the world again.
	/// </summary>
	public static CarPurchase Buy( Connection buyer, CarModel car, Vector3 around, Scene scene = null, Color? paint = null )
	{
		if ( !Networking.IsHost || buyer is null || car is null )
			return CarPurchase.Fail( "Nothing happens." );

		scene ??= Game.ActiveScene;

		if ( PlayerWallet.For( buyer ) is not { } wallet || wallet.Cash < car.Price )
			return CarPurchase.Fail( "Not enough cash." );

		var point = CarDelivery.FindFree( scene, around, out var total, out _ );

		if ( point is null )
			return CarPurchase.Fail( total == 0 ? NoPoint : NoRoom );

		if ( !ResourceLibrary.TryGet<PrefabFile>( car.Prefab, out var prefab ) )
		{
			Log.Warning( $"Car dealership: no prefab '{car.Prefab}' for {car.Name}" );
			return CarPurchase.Fail( "That car isn't available." );
		}

		var steamId = (ulong)buyer.SteamId;

		if ( !Transactions.Pay( steamId, Parties.CarDealership, TransactionCurrency.Cash, car.Price,
			    TxKind.CarPurchase, $"Car dealership: {car.Name}", car.Id ).Ok )
			return CarPurchase.Fail( "Not enough cash." );

		var made = Make( car, prefab, point, steamId, scene, paint );

		if ( made is null )
		{
			Transactions.Receive( steamId, Parties.CarDealership, TransactionCurrency.Cash, car.Price,
				TxKind.Refund, $"Car dealership: {car.Name} could not be delivered", car.Id );

			return CarPurchase.Fail( "The car could not be delivered. You were not charged." );
		}

		StatsLog.Purchase( buyer, "car", car.Id, car.Name, car.Price );

		return new CarPurchase( true, $"Bought a {car.Name} for ${car.Price:N0}. It is waiting at the delivery point.", made );
	}

	/// <summary>
	/// The car itself, at the point, with the buyer's title on it and the network told. Null if it could not be made.
	/// </summary>
	static GameObject Make( CarModel car, PrefabFile prefab, GameObject point, ulong buyer, Scene scene, Color? paint )
	{
		try
		{
			// The lowest part of the car, in its own space, so it ends up resting on the ground the bay stands on.
			var lowest = SceneUtility.GetPrefabScene( prefab ).GetLocalBounds().Mins.z;
			var transform = CarDelivery.Placement( scene, point, lowest );

			GameObject go;

			using ( scene.Push() )
				go = GameObject.Clone( prefab, new CloneConfig { Transform = transform, StartEnabled = false } );

			if ( !go.IsValid() )
				return null;

			go.Name = car.Name;

			// The buyer's colour, set before the car is spawned so it reaches everyone with the car. A car without a
			// CarPaint keeps its own look; none of this is trusted from a client (the host's app picked it).
			if ( paint is { } color && go.Components.Get<CarPaint>( FindMode.EverythingInSelfAndDescendants ) is { } carPaint )
				carPaint.Paint = color;

			// The buyer holds title from the moment the car exists (see docs/ownership.md): this only seeds the record.
			go.Components.GetOrCreate<OwnershipComponent>().DefaultOwner = Ownership.Player( buyer );

			go.NetworkMode = NetworkMode.Object;
			go.NetworkSpawn( true, null );
			go.Network.SetOrphanedMode( NetworkOrphaned.Host );
			return go;
		}
		catch ( System.Exception e )
		{
			Log.Warning( e, $"Car dealership: could not make {car.Name}: {e.Message}" );
			return null;
		}
	}
}
