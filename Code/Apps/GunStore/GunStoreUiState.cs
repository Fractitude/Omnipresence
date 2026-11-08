namespace OmnipresenceApps;

/// <summary>
/// What the Gun Store panel draws. Replaced whole by <see cref="GunStoreViewModel"/> on every change.
/// </summary>
public sealed record GunStoreUiState
{
	/// <summary>What a viewer draws until the host's state arrives.</summary>
	public static GunStoreUiState Empty { get; } = new();

	/// <summary>Goes up whenever anything else here changes. The panel's BuildHash is this.</summary>
	public int Revision { get; init; }

	/// <summary>False while this isn't the gun store's computer.</summary>
	public bool HasAccess { get; init; }

	/// <summary>Shown instead of the store when there is no access.</summary>
	public string Message { get; init; } = "";

	/// <summary>The seated player's cash. Empty with nobody seated: the rest of the screen is the same for everyone.</summary>
	public string Balance { get; init; } = "";

	/// <summary>A player is seated and their account is loaded, so something can be bought.</summary>
	public bool Seated { get; init; }

	/// <summary>How the picture is turned, shared. Sent without a new <see cref="Revision"/>: only the picture follows it.</summary>
	public CarView View { get; init; } = CarView.Default;

	/// <summary>The tab on show, shared with everyone watching the screen.</summary>
	public GunItemKind Tab { get; init; }

	public string SelectedId { get; init; } = "";

	public IReadOnlyList<GunRow> Items { get; init; } = [];

	/// <summary>The selected item, or null.</summary>
	public GunRow Selected => Items.FirstOrDefault( x => x.Id == SelectedId );

	/// <summary>What happened with the last click. Empty when nothing did.</summary>
	public string Notice { get; init; } = "";

	public bool NoticeIsError { get; init; }
}

/// <summary>
/// One line of the list. <see cref="Status"/> is the seated player's own state of it (they carry it, or how full their
/// reserve is), empty with nobody seated. <see cref="CanBuy"/> says the button works.
/// </summary>
public sealed record GunRow(
	string Id,
	string Name,
	string Description,
	string Stats,
	string Price,
	bool CanAfford,
	bool CanBuy,
	string Status );
