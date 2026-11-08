using System;
using System.Globalization;

namespace OmnipresenceApps;

/// <summary>
/// Everything the Car Dealership does: lists the cars, shows the one picked, and sells it. A bought car is made at a free
/// delivery point near the computer (<see cref="CarDelivery"/>), or the player is told there is no room and nothing is
/// charged. Only works on a computer tagged <see cref="CarDealer.ComputerTag"/>. Runs in the host's copy only (the app is
/// shared), because the money is the host's.
/// </summary>
/// <remarks>
/// Who buys is always the player in the seat, worked out from the computer. The actions carry only a car id: the price,
/// the cash and the room are all checked on the host. What is on show (the car, each car's colours, how the picture is
/// turned) is the computer's, in its <see cref="CarShowroom"/>, so it is the same for everyone and for whoever sits down next;
/// only the wallet is the seated player's. With nobody seated the screen still shows all of it.
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

	/// <summary>Turn the picture: three arguments, the yaw, the elevation and the zoom, as numbers.</summary>
	public const string ViewAction = "view";

	public const string BodyPart = "body";
	public const string TrimPart = "trim";
	public const string GlassPart = "glass";

	public const string StockArg = "stock";

	/// <summary>How often the free bays are counted again, in seconds.</summary>
	const float BayInterval = 0.5f;

	/// <summary>Seconds without a touch before the car turns by itself, and how fast (degrees a second).</summary>
	public const float IdleBeforeSpin = 3f;

	public const float SpinSpeed = 18f;

	/// <summary>
	/// How often the picture's turning is sent while the car turns by itself, in seconds. Screens carry on turning it at
	/// <see cref="SpinSpeed"/> between two, so this only keeps them together.
	/// </summary>
	const float SpinPublishInterval = 0.25f;

	/// <summary>How often it is sent while someone is turning it, at most.</summary>
	const float TouchPublishInterval = 0.06f;

	readonly Func<Connection> getSeat;
	readonly Func<NaviComputer> getComputer;

	string notice = "";
	bool noticeIsError;
	bool busy;
	Guid lastSeatId;
	string bays = "";
	RealTimeSince sinceBays = 1000;
	string signature;
	int revision;

	RealTimeSince sincePublished = 1000;
	bool viewDirty;
	bool publishedSpinning;

	public CarDealershipViewModel( Func<Connection> getSeat, Func<NaviComputer> getComputer )
	{
		this.getSeat = getSeat;
		this.getComputer = getComputer;
		UiState = Build();
	}

	public CarDealershipUiState UiState { get; private set; }

	CarShowroom Showroom => CarShowroom.For( getComputer() );

	string SelectedId => Showroom.SelectedId;

	/// <summary>
	/// How the picture is turned now, every frame. A viewer's copy gets <see cref="CarDealershipUiState.View"/>, which is
	/// sent less often; the host's copy draws this.
	/// </summary>
	public CarView LiveView
	{
		get
		{
			var showroom = Showroom;
			return new CarView( showroom.Yaw, showroom.Elevation, showroom.Zoom, showroom.SinceTouched > IdleBeforeSpin );
		}
	}

	/// <summary>
	/// Once a frame: start over when the seat changed, turn the car if nobody has touched it, send how it is turned, and
	/// draw again if anything drawn changed.
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

		TickView();

		if ( Signature( seat ) != signature )
			UiState = Build();
	}

	/// <summary>
	/// The car turns by itself on the host, whoever is watching, once nobody has touched it for a while. Screens are told how
	/// it is turned now and then (often while someone is turning it, rarely while it turns by itself).
	/// </summary>
	void TickView()
	{
		var showroom = Showroom;
		var spinning = showroom.SinceTouched > IdleBeforeSpin;

		if ( spinning )
			showroom.Yaw = (showroom.Yaw + SpinSpeed * Time.Delta) % 360f;

		var due = spinning != publishedSpinning
			? true
			: sincePublished > (spinning ? SpinPublishInterval : TouchPublishInterval) && (spinning || viewDirty);

		if ( !due )
			return;

		sincePublished = 0;
		viewDirty = false;
		publishedSpinning = spinning;

		// Not a new Revision: the panel is not drawn again for this, only the picture is turned.
		UiState = UiState with { View = LiveView };
	}

	public void Act( string action, string[] args )
	{
		if ( UiState.HasAccess && args.Length > 0 )
		{
			switch ( action )
			{
				case SelectAction when CarCatalog.Find( args[0] ) is { } car:
					// Each car keeps the colours picked for it, so nothing is reset here.
					Showroom.SelectedId = car.Id;
					notice = "";
					noticeIsError = false;
					break;

				// Sent by the seated player's click, so it is only ever parsed as a colour (and the body and trim are opaque).
				case PaintAction when args.Length > 1:
					Pick( args[0], args[1] );
					break;

				case ViewAction when args.Length > 2:
					TurnTo( args[0], args[1], args[2] );
					break;

				case BuyAction:
					_ = BuyAsync( args[0] );
					break;
			}
		}

		UiState = Build();
	}

	/// <summary>The picture was turned or zoomed on a screen. Clamped here, as it is a number from a client.</summary>
	void TurnTo( string yaw, string elevation, string zoom )
	{
		if ( !Number( yaw, out var y ) || !Number( elevation, out var e ) || !Number( zoom, out var z ) )
			return;

		var showroom = Showroom;

		showroom.Yaw = ((y % 360f) + 360f) % 360f;
		showroom.Elevation = Math.Clamp( e, CarPreviewScene.MinElevation, CarPreviewScene.MaxElevation );
		showroom.Zoom = Math.Clamp( z, CarPreviewScene.MinZoom, CarPreviewScene.MaxZoom );
		showroom.SinceTouched = 0;
		viewDirty = true;
	}

	static bool Number( string text, out float value ) =>
		float.TryParse( text, NumberStyles.Float, CultureInfo.InvariantCulture, out value ) && float.IsFinite( value );

	void Pick( string part, string value )
	{
		var picked = value == StockArg ? null : CarLook.Parse( value );

		if ( value != StockArg && picked is null )
			return;

		var picks = Showroom.PicksOf( SelectedId );

		switch ( part )
		{
			case BodyPart:
				picks.Paint = picked?.WithAlpha( 1 );
				break;

			case TrimPart when CarCatalog.HasTrim( CarCatalog.Find( SelectedId ) ):
				picks.Secondary = picked?.WithAlpha( 1 );
				break;

			case GlassPart:
				picks.Glass = picked;
				break;
		}
	}

	/// <summary>A car's colours: its own, and what has been picked over them. Null if it can't be painted.</summary>
	CarLook? LookOf( string id )
	{
		if ( CarCatalog.StockLook( CarCatalog.Find( id ) ) is not { } stock )
			return null;

		var picks = Showroom.PicksOf( id );

		return new CarLook( picks.Paint ?? stock.Paint, picks.Secondary ?? stock.Secondary, picks.Glass ?? stock.Glass );
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
		var chosen = SelectedId == id ? LookOf( id ) : null;

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

	string Signature( Connection seat )
	{
		var picks = Showroom.PicksOf( SelectedId );

		return HashCode.Combine(
			seat?.Id,
			PlayerWallet.For( seat )?.Version,
			PlayerProgress.For( seat )?.Version,
			CarDealer.IsDealership( getComputer() ),
			(SelectedId, picks.Paint, picks.Secondary, picks.Glass, CarCatalog.HasTrim( CarCatalog.Find( SelectedId ) )),
			notice,
			busy,
			bays ).ToString();
	}

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

		var state = new CarDealershipUiState
		{
			Revision = ++revision, SelectedId = SelectedId, Notice = notice, NoticeIsError = noticeIsError, Busy = busy,
			View = LiveView,
		};

		if ( !CarDealer.IsDealership( getComputer() ) )
			return state with { Message = "This terminal isn't connected to the dealership." };

		// The screen is the same with nobody seated; only the wallet is the seated player's.
		var seated = seat is not null;
		var loaded = progress is { Loaded: true };
		var cash = seated && loaded ? PlayerWallet.For( seat )?.Cash ?? 0 : 0;
		var stock = CarCatalog.StockLook( CarCatalog.Find( SelectedId ) );
		var look = LookOf( SelectedId );

		return state with
		{
			HasAccess = true,
			Seated = seated && loaded,
			CanTrim = CarCatalog.HasTrim( CarCatalog.Find( SelectedId ) ),
			Paint = look is { } now ? CarLook.Format( now.Paint ) : "",
			Secondary = look is { } now2 ? CarLook.Format( now2.Secondary ) : "",
			Glass = look is { } now3 ? CarLook.Format( now3.Glass ) : "",
			StockPaint = stock is { } own ? CarLook.Format( own.Paint ) : "",
			StockSecondary = stock is { } own2 ? CarLook.Format( own2.Secondary ) : "",
			StockGlass = stock is { } own3 ? CarLook.Format( own3.Glass ) : "",
			Balance = !seated ? "" : loaded ? $"${cash:N0}" : "Loading your account...",
			Bays = bays,
			Cars = CarCatalog.All
				.Select( x => new CarRow( x.Id, x.Name, x.Description, $"${x.Price:N0}", seated && loaded && cash >= x.Price ) )
				.ToList(),
		};
	}
}
