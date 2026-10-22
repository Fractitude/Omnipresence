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
	/// What an app says while its computer is offline: why, and what to do about it. The player turned the network off, or
	/// the computer has no network card.
	/// </summary>
	public static string OfflineHint( NaviComputer computer ) => computer is { HasNetworkCard: true }
		? "The network is turned off. Right-click the network icon in the taskbar and choose Go online."
		: "This computer has no network card.";

	/// <summary>
	/// A line that tells the player the app is offline, with the way back.
	/// </summary>
	public static string OfflineMessage( NaviComputer computer ) => $"No network connection. {OfflineHint( computer )}";
}
