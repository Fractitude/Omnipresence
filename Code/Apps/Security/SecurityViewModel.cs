using System;

namespace OmnipresenceApps;

/// <summary>
/// Everything the Security System does: it connects to the cameras within range of the computer, keeps that list
/// current, and remembers which one the player enlarged. The panel (<c>Security.razor</c>) draws <see cref="UiState"/>.
/// </summary>
/// <remarks>
/// The app is shared, so this runs in the host's copy only and everyone near the screen sees the same camera enlarged.
/// The pictures are each client's own and are asked for by the panel, not here (see <see cref="SecurityFeeds"/>).
/// </remarks>
public sealed class SecurityViewModel
{
	/// <summary>The player clicked a camera: it fills the window and the others are listed beside it.</summary>
	public const string SelectAction = "select";

	/// <summary>The player asked for the grid of every camera again.</summary>
	public const string GridAction = "all";

	/// <summary>How many cameras one computer shows. With more than this in range, it keeps the nearest.</summary>
	const int MaxCameras = 9;

	/// <summary>How often the cameras in range are looked for, in seconds.</summary>
	const float ScanInterval = 0.5f;

	/// <summary>Units are inches, so this is how many metres one of them is.</summary>
	const float MetresPerUnit = 0.0254f;

	readonly Func<NaviComputer> getComputer;

	RealTimeSince sinceScan = float.MaxValue;

	public SecurityViewModel( Func<NaviComputer> getComputer )
	{
		this.getComputer = getComputer;
	}

	public SecurityUiState UiState { get; private set; } = new();

	public int Revision => UiState.Revision;

	void Update( Func<SecurityUiState, SecurityUiState> change ) =>
		UiState = change( UiState ) with { Revision = UiState.Revision + 1 };

	/// <summary>
	/// Called by the panel each frame: connect to any camera that came into range, and drop the ones that left. The
	/// state only changes when the list does, so a screen left open doesn't redraw twice a second for nothing.
	/// </summary>
	public void Tick()
	{
		if ( sinceScan < ScanInterval )
			return;

		sinceScan = 0;

		var cameras = InRange();

		if ( Same( cameras, UiState.Cameras ) )
			return;

		Update( uiState => uiState with
		{
			Cameras = cameras,

			// An enlarged camera that left range closes back to the grid rather than showing nothing.
			SelectedId = cameras.Any( x => x.Id == uiState.SelectedId ) ? uiState.SelectedId : "",
		} );
	}

	/// <summary>
	/// The player clicked a camera or asked for the grid. Anything else, and a camera that isn't connected any more, is
	/// ignored, as if the click had been on nothing.
	/// </summary>
	public void Act( string action, string[] args )
	{
		switch ( action )
		{
			case SelectAction when args is [var id, ..] && UiState.Cameras.Any( x => x.Id == id ):
				Update( uiState => uiState with { SelectedId = id } );
				break;

			case GridAction:
				Update( uiState => uiState with { SelectedId = "" } );
				break;
		}
	}

	/// <summary>
	/// The cameras the computer can connect to right now, nearest first, as the rows the panel prints.
	/// </summary>
	IReadOnlyList<SecurityCameraRow> InRange()
	{
		if ( getComputer() is not { } computer || computer.Scene is not { } scene )
			return [];

		var at = computer.WorldPosition;

		return SecurityCamera.Near( scene, at )
			.Take( MaxCameras )
			.Select( x => new SecurityCameraRow(
				x.GameObject.Id.ToString(),
				x.DisplayName,
				$"{MathF.Round( x.WorldPosition.Distance( at ) * MetresPerUnit )} m away",
				x.Broken ) )
			.ToList();
	}

	/// <summary>
	/// Are these the same cameras, in the same order and the same state, as the state already holds? A camera breaking
	/// changes the list the panel draws, so it counts.
	/// </summary>
	static bool Same( IReadOnlyList<SecurityCameraRow> cameras, IReadOnlyList<SecurityCameraRow> known ) =>
		cameras.Count == known.Count
		&& !cameras.Where( ( row, i ) => row.Id != known[i].Id || row.Broken != known[i].Broken ).Any();
}
