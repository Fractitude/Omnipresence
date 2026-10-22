using Sandbox.UI;

namespace OmnipresenceApps;

/// <summary>
/// Where one of the game's own apps runs: the computer its window is on, and the player in that computer's seat. The
/// seat is worked out from the computer, never from anything the app or an action says.
/// </summary>
public static class GameAppSeat
{
	/// <summary>
	/// The computer whose window holds <paramref name="panel"/>, or null.
	/// </summary>
	public static NaviComputer ComputerOf( Panel panel ) =>
		panel?.AncestorsAndSelf.OfType<NaviAppHost>().FirstOrDefault()?.Computer;

	/// <summary>
	/// The connection in the seat of the computer that holds <paramref name="panel"/>, or null when nobody sits there.
	/// Without a network it is the local connection.
	/// </summary>
	public static Connection UserOf( Panel panel )
	{
		if ( ComputerOf( panel ) is not { HasUser: true } computer )
			return null;

		return Networking.IsActive ? computer.User : Connection.Local;
	}
}
