using System;

namespace OmnipresenceApps;

/// <summary>
/// Everything the Depot does: a furniture and equipment store that sells the big objects a player places in the world
/// (<see cref="PlaceableCatalog"/>) for cash and delivers them after a wait. Runs in the host's copy only (the app is
/// shared), because the money and the deliveries are the host's. It only works at home, with the network on.
/// </summary>
/// <remarks>
/// Who buys is always the player in the seat, worked out from the computer. The only action carries an object id: the
/// price, the delivery time and the limit are checked on the host (<see cref="PlaceableShop"/>).
/// </remarks>
public sealed class DepotViewModel
{
	public const string BuyAction = "buy";

	readonly Func<Connection> getSeat;
	readonly Func<NaviComputer> getComputer;

	string notice = "";
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
		}

		if ( Signature( seat ) != signature )
			UiState = Build();
	}

	public void Act( string action, string[] args )
	{
		var seat = getSeat();

		if ( action == BuyAction && args.Length > 0 && CanWork( seat ) && PlayerCrime.For( seat ) is { Ready: true } crime )
		{
			var result = PlaceableShop.Buy( seat, args[0], WorldClock.Now );
			notice = result.Message;
			crime.Tell( result );
		}

		UiState = Build();
	}

	bool CanWork( Connection seat ) =>
		seat is not null && WiredTravel.PlaceOf( seat ) == WiredPlace.Home && GameAppNetwork.IsOnline( getComputer() );

	string Signature( Connection seat ) =>
		HashCode.Combine(
			seat?.Id,
			PlayerWallet.For( seat )?.Version,
			GameAppNetwork.IsOnline( getComputer() ),
			seat is null ? default : WiredTravel.PlaceOf( seat ),
			notice,
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

		return state with
		{
			HasAccount = true,
			Cash = $"${wallet.Cash:N0}",
			Shop = PlaceableCatalog.All.Select( x => BuildRow( seat, x ) ).ToList(),
			Incoming = PlacedObjectRegistry.OnTheWay( (ulong)seat.SteamId )
				.Select( x => new DepotIncomingRow(
					PlaceableCatalog.Find( x.Placeable )?.Name ?? "Order",
					$"On its way, arrives {WorldClock.TimeOfDayOf( x.ArriveAt )} "
					+ $"({InteractionSetup.Countdown( x.ArriveAt - now )} left)" ) )
				.ToList(),
		};
	}

	static DepotRow BuildRow( Connection seat, Placeable item )
	{
		var reason = PlaceableShop.WhyNot( seat, item ) ?? "";

		return new DepotRow( item.Id, item.Name, item.Description, item.Icon, $"${item.Price:N0}",
			$"Delivered in {InteractionSetup.Countdown( item.DeliverSeconds )}", reason == "", reason );
	}
}
