namespace OmnipresenceApps;

/// <summary>
/// Everything The Wired does: from home it connects to the Wired, and from the Wired it disconnects back home. Runs
/// in the host's copy only (the app is shared), since only the host can change the scene.
/// </summary>
public sealed class TheWiredViewModel
{
	public const string CrossAction = "cross";

	public TheWiredUiState UiState { get; private set; } = Describe( WiredTravel.Current );

	public void Act( string action, string[] args )
	{
		if ( action == CrossAction )
			WiredTravel.Cross();
	}

	static TheWiredUiState Describe( WiredPlace place ) => place switch
	{
		WiredPlace.Home => new() { Revision = 1, Status = "Not connected.", ButtonText = "Connect" },
		WiredPlace.Wired => new() { Revision = 1, Status = "Present day. Present time.", ButtonText = "Disconnect" },
		_ => new() { Revision = 1, Status = "No connection here." },
	};
}
