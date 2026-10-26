using System;

namespace OmnipresenceApps;

/// <summary>
/// Everything the Depot does: a furniture and equipment store that sells the big objects a player places in the world
/// (<see cref="PlaceableCatalog"/>) for cash. An order is either mailed home, to arrive beside the buyer's mailbox after
/// a wait, or collected at the store the moment it is paid for: the store's bays are the objects tagged
/// <c>delivery_point</c> within <see cref="DeliverySpot.Range"/> of the computer the order is placed at. Runs in the
/// host's copy only (the app is shared), because the money and the deliveries are the host's. It only works at home, with
/// the network on.
/// </summary>
/// <remarks>
/// Who buys is always the player in the seat, worked out from the computer. An action carries an object id, and where to
/// deliver it; the price, the delivery time, the bay and the limit are all checked on the host
/// (<see cref="PlaceableShop"/>), never taken from what the panel says.
/// </remarks>
public sealed class DepotViewModel
{
	public const string BuyAction = "buy";

	/// <summary>Changes where the next order goes; its one argument is <see cref="Store"/> or <see cref="Home"/>.</summary>
	public const string DeliverAction = "deliver";

	/// <summary>The two places an order can go, as the panel sends them.</summary>
	public const string Store = PlacedObjectRules.DeliveryStore;

	/// <inheritdoc cref="Store"/>
	public const string Home = PlacedObjectRules.DeliveryHome;

	readonly Func<Connection> getSeat;
	readonly Func<NaviComputer> getComputer;

	string notice = "";
	string destination = Store;
	Guid lastSeatId;
	string signature;
	int revision;

	public DepotViewModel( Func<Connection> getSeat, Func<NaviComputer> getComputer )
	{
		this.getSeat = getSeat;
		this.getComputer = getComputer;
		UiState = Build();
	}

	public DepotUiState UiState { get; private set; }

	/// <summary>
	/// Once a frame: start over when the seat changed, and draw again if anything drawn changed.
	/// </summary>
	public void Tick()
	{
		var seat = getSeat();

		if ( (seat?.Id ?? default) != lastSeatId )
		{
			lastSeatId = seat?.Id ?? default;
			notice = "";
			destination = Store;
		}

		if ( Signature( seat ) != signature )
			UiState = Build();
	}

	public void Act( string action, string[] args )
	{
		var seat = getSeat();

		if ( action == DeliverAction && args.Length > 0 )
		{
			Choose( seat, args[0] );
		}
		else if ( action == BuyAction && args.Length > 0 && CanWork( seat ) && PlayerCrime.For( seat ) is { Ready: true } crime )
		{
			var terminal = Terminal();
			var result = PlaceableShop.Buy( seat, args[0], WorldClock.Now, WhereNow( StoreNear( seat ) ), terminal?.At,
				terminal?.Scene );

			notice = result.Message;
			crime.Tell( result );
		}

		UiState = Build();
	}

	/// <summary>
	/// Change where the next order goes. The store is only offered where this computer can see one; the shop checks the
	/// same thing again before it takes the money, so the panel cannot talk it into an impossible order.
	/// </summary>
	void Choose( Connection seat, string where )
	{
		if ( !CanWork( seat ) || !PlacedObjectRules.IsDestination( where ) )
			return;

		if ( where == Store && !StoreNear( seat ) )
			return;

		destination = where;
	}

	bool CanWork( Connection seat ) =>
		seat is not null && WiredTravel.PlaceOf( seat ) == WiredPlace.Home && GameAppNetwork.IsOnline( getComputer() );

	/// <summary>
	/// Where this computer is: the place in the scene that delivery points are looked for around. Null when there is no
	/// computer, or it is not in a scene (a viewer's copy; only the host's copy asks).
	/// </summary>
	(Vector3 At, Scene Scene)? Terminal()
	{
		if ( getComputer() is not { } computer || computer.Scene is not { } scene )
			return null;

		return (computer.WorldPosition, scene);
	}

	/// <summary>Whether a delivery point is near enough to this computer for the Depot to hand an order over here.</summary>
	bool StoreNear( Connection seat ) =>
		CanWork( seat ) && Terminal() is { } place && DeliverySpot.HasPointNear( place.Scene, place.At );

	/// <summary>
	/// Where the next order goes: the store when that is what was chosen and a bay is in reach, home otherwise.
	/// Collecting at the store is the default, so a computer with no bay near it mails orders without the player having
	/// to choose home first, and the store's choice comes back by itself when a bay is in reach again.
	/// </summary>
	string WhereNow( bool bayInReach ) => destination == Store && bayInReach ? Store : Home;

	string Signature( Connection seat ) =>
		HashCode.Combine(
			seat?.Id,
			PlayerWallet.For( seat )?.Version,
			GameAppNetwork.IsOnline( getComputer() ),
			seat is null ? default : WiredTravel.PlaceOf( seat ),
			notice,
			destination,
			// Countdowns redraw once a second, while something is on its way.
			seat is not null && PlacedObjectRegistry.OnTheWay( (ulong)seat.SteamId ).Count > 0 ? WorldClock.Now : 0,
			PlacedObjectRegistry.Data.Deliveries.Count ).ToString();

	DepotUiState Build()
	{
		var seat = getSeat();
		var wallet = PlayerWallet.For( seat );

		signature = Signature( seat );

		var state = new DepotUiState { Revision = ++revision, Notice = notice };

		if ( !GameAppNetwork.IsOnline( getComputer() ) )
			return state with { OfflineHint = GameAppNetwork.OfflineHint( getComputer() ) };

		if ( seat is null )
			return state with { Message = "Sit down to open the Depot." };

		if ( WiredTravel.PlaceOf( seat ) != WiredPlace.Home )
			return state with { NoSignal = true, Message = "No signal. The Depot only works from home." };

		if ( PlayerCrime.For( seat ) is not { Ready: true } || wallet is null )
			return state with { Message = "Connecting..." };

		var now = WorldClock.Now;
		var terminal = Terminal();

		// The store's bays: the delivery points near this computer, nearest first. Collecting at the store is the
		// default, so only a computer with no bay in reach mails its orders home.
		var bays = terminal is { } place
			? DeliverySpot.StorePlaces( place.Scene, place.At )
			: new List<DeliverySpot.DeliveryPlace>();

		var store = WhereNow( bays.Count > 0 ) == Store;

		return state with
		{
			HasAccount = true,
			Cash = $"${wallet.Cash:N0}",
			StoreAvailable = bays.Count > 0,
			StoreChosen = store,
			StoreDetail = bays.Count == 0
				? "There is no delivery point near this computer."
				: "Handed over now.",
			HomeDetail = $"Mailed to your mailbox in "
			             + $"{Minutes( PlaceableCatalog.All.Min( x => x.DeliverSeconds ))}-"
			             + $"{Minutes( PlaceableCatalog.All.Max( x => x.DeliverSeconds ))} mins.",
			Shop = PlaceableCatalog.All.Select( x => BuildRow( seat, x, store ) ).ToList(),
			Incoming = PlacedObjectRegistry.OnTheWay( (ulong)seat.SteamId )
				.Select( x => new DepotIncomingRow(
					PlaceableCatalog.Find( x.Placeable )?.Name ?? "Order",
					$"On its way, arrives {WorldClock.TimeOfDayOf( x.ArriveAt )} "
					+ $"({InteractionSetup.Countdown( x.ArriveAt - now )} left)" ) )
				.ToList(),
		};
	}

	static DepotRow BuildRow( Connection seat, Placeable item, bool store )
	{
		var reason = PlaceableShop.WhyNot( seat, item ) ?? "";
		var delivery = store
			? "At the delivery point, now"
			: $"Mailed, in {InteractionSetup.Countdown( item.DeliverSeconds )}";

		return new DepotRow( item.Id, item.Name, item.Description, item.Icon, $"${item.Price:N0}", delivery, reason == "",
			reason );
	}

	/// <summary>A delivery time as the panel shows it: whole minutes.</summary>
	static long Minutes( long seconds ) => (long)MathF.Round( seconds / 60f );
}
