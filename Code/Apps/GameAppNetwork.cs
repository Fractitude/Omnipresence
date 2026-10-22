namespace OmnipresenceApps;

/// <summary>
/// The network, for the game's own apps that can't work without it. They read the computer they run on, so every viewer
/// of a shared app agrees on whether it is online.
/// </summary>
public static class GameAppNetwork
{
	/// <summary>
	/// Is this computer online? A missing computer isn't.
	/// </summary>
	public static bool IsOnline( NaviComputer computer ) => computer is { IsOnline: true };

	/// <summary>
	/// Why the computer is offline, in a short sentence: the player turned the network off, or it has no network card.
	/// </summary>
	public static string OfflineHint( NaviComputer computer ) => computer is { HasNetworkCard: true }
		? "The network is turned off."
		: "This computer has no network card.";}
