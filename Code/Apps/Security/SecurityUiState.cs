namespace OmnipresenceApps;

/// <summary>
/// One camera the computer can connect to, as the app lists it: the id its object has on every machine, its name, how
/// far away it is already written out, and whether it is out of action.
/// </summary>
public sealed record SecurityCameraRow( string Id, string Name, string Detail, bool Offline );

/// <summary>
/// What the Security System panel draws: the cameras in range, nearest first, and which one the player enlarged.
/// Replaced whole by <see cref="SecurityViewModel"/> on every change. The pictures are not here: they are each client's
/// own, and never leave it (see <see cref="SecurityFeeds"/>).
/// </summary>
public sealed record SecurityUiState
{
	/// <summary>What a viewer draws until the host's state arrives.</summary>
	public static SecurityUiState Empty { get; } = new();

	/// <summary>Goes up whenever anything else here changes. The panel's BuildHash is this.</summary>
	public int Revision { get; init; }

	/// <summary>The cameras within range of the computer, nearest first.</summary>
	public IReadOnlyList<SecurityCameraRow> Cameras { get; init; } = [];

	/// <summary>The camera the player enlarged, or empty for the grid of all of them.</summary>
	public string SelectedId { get; init; } = "";

	/// <summary>The enlarged camera, or null in the grid.</summary>
	public SecurityCameraRow Selected => Cameras.FirstOrDefault( x => x.Id == SelectedId );

	/// <summary>True while one camera fills the window and the rest are listed beside it.</summary>
	public bool IsEnlarged => Selected is not null;

	/// <summary>The cameras beside the enlarged one, for the list on the left.</summary>
	public IReadOnlyList<SecurityCameraRow> Others => Cameras.Where( x => x.Id != SelectedId ).ToList();

	/// <summary>How many are connected, beside the title: "3 cameras", or "no cameras".</summary>
	public string CountText => Cameras.Count switch
	{
		0 => "no cameras",
		1 => "1 camera",
		_ => $"{Cameras.Count} cameras",
	};

	/// <summary>The name in the title bar: the enlarged camera's, or the app's.</summary>
	public string Heading => Selected?.Name ?? "Security";

	/// <summary>There is nothing to show: no camera is close enough to the computer.</summary>
	public bool HasCameras => Cameras.Count > 0;
}
