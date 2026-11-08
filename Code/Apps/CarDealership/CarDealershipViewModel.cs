using System;

namespace OmnipresenceApps;

/// <summary>
/// Everything the Car Dealership does: lists the cars, shows the one picked, and sells it. A bought car is made at a free
/// delivery point near the computer (<see cref="CarDelivery"/>), or the player is told there is no room and nothing is
/// charged. Only works on a computer tagged <see cref="CarDealer.ComputerTag"/>. Runs in the host's copy only (the app is
/// shared), because the money is the host's.
/// </summary>
/// <remarks>
/// Who buys is always the player in the seat, worked out from the computer. The actions carry only a car id: the price,
/// the cash and the room are all checked on the host.
/// </remarks>
public sealed class CarDealershipViewModel
{
	public const string SelectAction = "select";
	public const string BuyAction = "buy";

	/// <summary>Pick a body colour: one argument, <c>#rrggbb</c>, or <see cref="StockArg"/> for the car's own.</summary>
	public const string PaintAction = "paint";

	public const string StockArg = "stock";

	/// <summary>How often the free bays are counted again, in seconds.</summary>
	const float BayInterval = 0.5f;

	readonly Func<Connection> getSeat;
	readonly Func<NaviComputer> getComputer;

	string selectedId = CarCatalog.All.FirstOrDefault()?.Id ?? "";

	// The colour picked for the selected car; null is the car's own. Back to the car's own when another is selected.
	Color? paint;
	string notice = "";
	bool noticeIsError;
	bool busy;
	Guid lastSeatId;
	string bays = "";
	RealTimeSince sinceBays = 1000;
	string signature;
	int revision;

	public CarDealershipViewModel( Func<Connection> getSeat, Func<NaviComputer> getComputer )
	{
		this.getSeat = getSeat;
		this.getComputer = getComputer;
		UiState = Build();
	}

	public CarDealershipUiState UiState { get; private set; }

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
			noticeIsError = false;
		}

		if ( sinceBays > BayInterval )
		{
			sinceBays = 0;
			bays = CountBays();
		}

		if ( Signature( seat ) != signature )
			UiState = Build();
	}

	public void Act( string action, string[] args )
	{
		if ( UiState.HasAccess && args.Length > 0 )
		{
			switch ( action )
			{
				case SelectAction when CarCatalog.Find( args[0] ) is { } car:
					if ( selectedId != car.Id )
						paint = null;

					selectedId = car.Id;
					notice = "";
					noticeIsError = false;
					break;

				// Typed by the seated player's click, so it is only ever parsed as a colour.
				case PaintAction:
					paint = args[0] == StockArg ? null : Color.Parse( args[0] ) is { } picked ? picked.WithAlpha( 1 ) : paint;
					break;

				case BuyAction:
					_ = BuyAsync( args[0] );
					break;
			}
		}

		UiState = Build();
	}

	/// <summary>
	/// Buy a car. The car's assets are mounted first (asynchronous), and everything is checked again after that: the
	/// world and the wallet may have changed while it waited. The money is taken and the car made in one step.
	/// </summary>
	async Task BuyAsync( string id )
	{
		var car = CarCatalog.Find( id );
		var seat = getSeat();
		var computer = getComputer();

		// The colour on the screen when the button was pressed, not whatever it is when the assets are done loading.
		var chosen = selectedId == id ? paint : null;

		if ( busy || car is null || seat is null || !CarDealer.IsDealership( computer ) )
			return;

		busy = true;
		UiState = Build();

		try
		{
			if ( car.Ident != "" )
				await SentLocal.Mount( car.Ident );

			// The player stood up, or another sat down, or the computer is gone.
			if ( getSeat()?.Id != seat.Id || !computer.IsValid() )
				return;

			var result = CarDealer.Buy( seat, car, computer.WorldPosition, paint: chosen );

			notice = result.Message;
			noticeIsError = !result.Ok;
			sinceBays = 1000;
		}
		catch ( Exception e )
		{
			Log.Warning( e, $"Car dealership: could not sell {car.Name}: {e.Message}" );
			notice = "The car could not be delivered.";
			noticeIsError = true;
		}
		finally
		{
			busy = false;
			bays = CountBays();
			UiState = Build();
		}
	}

	string Signature( Connection seat ) =>
		HashCode.Combine(
			seat?.Id,
			PlayerWallet.For( seat )?.Version,
			PlayerProgress.For( seat )?.Version,
			CarDealer.IsDealership( getComputer() ),
			(selectedId, paint),
			notice,
			busy,
			bays ).ToString();

	/// <summary>"2 of 3 bays free", or a word that there are none.</summary>
	string CountBays()
	{
		var computer = getComputer();

		if ( !computer.IsValid() )
			return "";

		CarDelivery.FindFree( computer.Scene, computer.WorldPosition, out var total, out var free );

		return total == 0 ? "No delivery bay near this computer" : $"{free} of {total} delivery bays free";
	}

	CarDealershipUiState Build()
	{
		var seat = getSeat();
		var progress = PlayerProgress.For( seat );

		signature = Signature( seat );

		var state = new CarDealershipUiState { Revision = ++revision, SelectedId = selectedId, Notice = notice, NoticeIsError = noticeIsError, Busy = busy };

		if ( seat is null )
			return state with { Message = "Sit down to use the terminal." };

		if ( !CarDealer.IsDealership( getComputer() ) )
			return state with { Message = "This terminal isn't connected to the dealership." };

		if ( progress is not { Loaded: true } )
			return state with { Message = "Loading your account..." };

		var cash = PlayerWallet.For( seat )?.Cash ?? 0;
		var stock = CarCatalog.StockPaint( CarCatalog.Find( selectedId ) );

		return state with
		{
			HasAccess = true,
			StockPaint = stock is { } own ? own.Hex : "",
			Paint = stock is null ? "" : (paint ?? stock.Value).Hex,
			Balance = $"${cash:N0}",
			Bays = bays,
			Cars = CarCatalog.All
				.Select( x => new CarRow( x.Id, x.Name, x.Description, $"${x.Price:N0}", cash >= x.Price ) )
				.ToList(),
		};
	}
}
