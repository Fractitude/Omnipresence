using System;

namespace OmnipresenceApps;

/// <summary>
/// Everything The Wired does: from home it connects the player in the seat to the Wired, and from the Wired it
/// disconnects them back home. Runs in the host's copy only (the app is shared), since only the host can move a player.
/// </summary>
/// <remarks>
/// Who travels is always the player in the seat, worked out from the computer. The action carries nothing.
/// </remarks>
public sealed class TheWiredViewModel
{
	public const string CrossAction = "cross";

	readonly Func<Connection> getSeat;

	string signature;
	int revision;

	public TheWiredViewModel( Func<Connection> getSeat )
	{
		this.getSeat = getSeat;
		UiState = Describe( null, 1 );
	}

	public TheWiredUiState UiState { get; private set; }

	/// <summary>
	/// Once a frame: draw again when the player in the seat, or where they are, changed.
	/// </summary>
	public void Tick()
	{
		var seat = getSeat();
		var next = seat is null ? "none" : WiredTravel.PlaceOf( seat ).ToString();
		if ( next == signature )
			return;

		signature = next;
		UiState = Describe( seat is null ? null : WiredTravel.PlaceOf( seat ), ++revision );
	}

	public void Act( string action, string[] args )
	{
		if ( action == CrossAction && getSeat() is { } seat )
			WiredTravel.Cross( seat );
	}

	static TheWiredUiState Describe( WiredPlace? place, int revision ) => place switch
	{
		WiredPlace.Home => new() { Revision = revision, Status = "Not connected.", ButtonText = "Connect" },
		WiredPlace.Wired => new()
			{ Revision = revision, Status = "Present day. Present time.", ButtonText = "Disconnect" },
		_ => new() { Revision = revision, Status = "Sit down to connect." },
	};
}
