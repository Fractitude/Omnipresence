using System;

namespace OmnipresenceApps;

/// <summary>
/// Everything The Wired does: from home it connects the player in the seat to the Wired, and from the Wired it
/// disconnects them back home. The action runs in the host's copy only (the app is shared), since only the host can move
/// a player.
/// </summary>
/// <remarks>
/// Who travels is always the player in the seat, worked out from the computer. The action carries nothing. They are
/// stood up from the seat as they go. What the window says is each viewer's own, drawn from where they are
/// (<see cref="Describe"/>), so it is never empty and never waits for the host.
/// </remarks>
public sealed class TheWiredViewModel
{
	public const string CrossAction = "cross";

	readonly Func<Connection> getSeat;
	readonly Func<NaviComputer> getComputer;

	public TheWiredViewModel( Func<Connection> getSeat, Func<NaviComputer> getComputer )
	{
		this.getSeat = getSeat;
		this.getComputer = getComputer;
	}

	public void Act( string action, string[] args )
	{
		if ( action != CrossAction || getSeat() is not { } seat )
			return;

		// Connecting needs the network. Disconnecting never does, so nobody is stuck in the Wired.
		if ( WiredTravel.PlaceOf( seat ) != WiredPlace.Wired && !GameAppNetwork.IsOnline( getComputer() ) )
			return;

		// Connecting stands the player up from the seat at once, not when they have moved out of its reach.
		getComputer()?.StandUp();
		WiredTravel.Cross( seat );
	}

	/// <summary>
	/// What the window says for a player who is at <paramref name="place"/>, on <paramref name="computer"/>. At home it
	/// offers no way in while the computer is offline.
	/// </summary>
	public static TheWiredUiState Describe( WiredPlace place, NaviComputer computer ) => place switch
	{
		WiredPlace.Wired => new()
			{ Revision = 2, Status = "Present day. Present time.", ButtonText = "Disconnect" },
		_ when !GameAppNetwork.IsOnline( computer ) => new()
		{
			Revision = computer is { HasNetworkCard: true } ? 3 : 4,
			Status = $"Not connected. {GameAppNetwork.OfflineMessage( computer )}",
		},
		_ => new() { Revision = 1, Status = "Not connected.", ButtonText = "Connect" },
	};
}
