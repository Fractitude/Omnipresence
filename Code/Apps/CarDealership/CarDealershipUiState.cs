namespace OmnipresenceApps;

/// <summary>
/// What the Car Dealership panel draws. Replaced whole by <see cref="CarDealershipViewModel"/> on every change.
/// </summary>
public sealed record CarDealershipUiState
{
	/// <summary>What a viewer draws until the host's state arrives.</summary>
	public static CarDealershipUiState Empty { get; } = new();

	/// <summary>Goes up whenever anything else here changes. The panel's BuildHash is this.</summary>
	public int Revision { get; init; }

	/// <summary>False while nobody is in the seat, this isn't the dealership's computer, or the account isn't loaded.</summary>
	public bool HasAccess { get; init; }

	/// <summary>Shown instead of the dealership when there is no access.</summary>
	public string Message { get; init; } = "";

	public string Balance { get; init; } = "";

	/// <summary>The car in the showroom, shared with everyone watching the screen. Empty before the first is picked.</summary>
	public string SelectedId { get; init; } = "";

	/// <summary>
	/// The colours picked for the selected car (<see cref="CarLook.Format"/>), shared like the selection so every viewer's
	/// picture is painted the same: the body, the trim and the glass (its alpha is the tint's strength). Empty when the car
	/// can't be painted.
	/// </summary>
	public string Paint { get; init; } = "";

	public string Secondary { get; init; } = "";

	public string Glass { get; init; } = "";

	/// <summary>The selected car's own colours (<see cref="CarCatalog.StockLook"/>). Empty when it can't be painted.</summary>
	public string StockPaint { get; init; } = "";

	public string StockSecondary { get; init; } = "";

	public string StockGlass { get; init; } = "";

	public bool CanPaint => Paint != "";

	public IReadOnlyList<CarRow> Cars { get; init; } = [];

	/// <summary>The selected car, or null.</summary>
	public CarRow Selected => Cars.FirstOrDefault( x => x.Id == SelectedId );

	/// <summary>What happened with the last click. Empty when nothing did.</summary>
	public string Notice { get; init; } = "";

	/// <summary>True when <see cref="Notice"/> says a purchase failed.</summary>
	public bool NoticeIsError { get; init; }

	/// <summary>A purchase is being made: the car's assets are loading. Buying again waits.</summary>
	public bool Busy { get; init; }

	/// <summary>The delivery points near the computer, and how many of them have room: "2 of 3 bays free".</summary>
	public string Bays { get; init; } = "";
}

public sealed record CarRow( string Id, string Name, string Description, string Price, bool CanAfford );
