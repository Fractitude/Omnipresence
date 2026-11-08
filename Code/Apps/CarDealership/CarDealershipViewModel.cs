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

	/// <summary>
	/// Pick a colour: two arguments, which part (<see cref="BodyPart"/>, <see cref="TrimPart"/> or <see cref="GlassPart"/>)
	/// and the colour (<see cref="CarLook.Format"/>, or <see cref="StockArg"/> for the car's own).
	/// </summary>
	public const string PaintAction = "paint";

	public const string BodyPart = "body";
	public const string TrimPart = "trim";
	public const string GlassPart = "glass";

	public const string StockArg = "stock";

	/// <summary>How often the free bays are counted again, in seconds.</summary>
	const float BayInterval = 0.5f;

	readonly Func<Connection> getSeat;
	readonly Func<NaviComputer> getComputer;

	string selectedId = CarCatalog.All.FirstOrDefault()?.Id ?? "";

	// The colours picked for the selected car; null is the car's own. Back to the car's own when another is selected.
	Color? paint;
	Color? secondary;
	Color? glass;
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
						paint = secondary = glass = null;

					selectedId = car.Id;
					notice = "";
					noticeIsError = false;
					break;

				// Sent by the seated player's click, so it is only ever parsed as a colour (and the body and trim are opaque).
				case PaintAction when args.Length > 1:
					Pick( args[0], args[1] );
					break;

				case BuyAction:
					_ = BuyAsync( args[0] );
					break;
			}
		}

		UiState = Build();
	}

	void Pick( string part, string value )
	{
		var picked = value == StockArg ? null : CarLook.Parse( value );

		if ( value != StockArg && picked is null )
			return;

		switch ( part )
		{
			case BodyPart:
				paint = picked?.WithAlpha( 1 );
				break;

			case TrimPart when CarCatalog.HasTrim( CarCatalog.Find( selectedId ) ):
				secondary = picked?.WithAlpha( 1 );
				break;

			case GlassPart:
				glass = picked;
				break;
		}
	}

	/// <summary>The selected car's colours: its own, and what has been picked over them. Null if it can't be painted.</summary>
	CarLook? LookOf( string id )
	{
		if ( CarCatalog.StockLook( CarCatalog.Find( id ) ) is not { } stock )
			return null;

		return new CarLook( paint ?? stock.Paint, secondary ?? stock.Secondary, glass ?? stock.Glass );
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
		var chosen = selectedId == id ? LookOf( id ) : null;

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

			var result = CarDealer.Buy( seat, car, computer.WorldPosition, look: chosen );

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
			(selectedId, paint, secondary, glass, CarCatalog.HasTrim( CarCatalog.Find( selectedId ) )),
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
		var stock = CarCatalog.StockLook( CarCatalog.Find( selectedId ) );
		var look = LookOf( selectedId );

		return state with
		{
			HasAccess = true,
			CanTrim = CarCatalog.HasTrim( CarCatalog.Find( selectedId ) ),
			Paint = look is { } now ? CarLook.Format( now.Paint ) : "",
			Secondary = look is { } now2 ? CarLook.Format( now2.Secondary ) : "",
			Glass = look is { } now3 ? CarLook.Format( now3.Glass ) : "",
			StockPaint = stock is { } own ? CarLook.Format( own.Paint ) : "",
			StockSecondary = stock is { } own2 ? CarLook.Format( own2.Secondary ) : "",
			StockGlass = stock is { } own3 ? CarLook.Format( own3.Glass ) : "",
			Balance = $"${cash:N0}",
			Bays = bays,
			Cars = CarCatalog.All
				.Select( x => new CarRow( x.Id, x.Name, x.Description, $"${x.Price:N0}", cash >= x.Price ) )
				.ToList(),
		};
	}
}
