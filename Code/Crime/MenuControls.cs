namespace Sandbox;

/// <summary>
/// What a full-screen panel that takes the mouse does to the player's controls: it takes the look and move controls
/// while it is open and gives them back when it closes.
/// </summary>
public static class MenuControls
{
	/// <summary>
	/// Take the look and move controls from the player, or give them back.
	/// </summary>
	public static void Free( bool allowed )
	{
		var controller = Players.LocalController;

		if ( controller is null )
			return;

		controller.UseLookControls = allowed;
		controller.UseInputControls = allowed;

		// The controller keeps the last move it was given, so a held key would walk on: zero it.
		if ( !allowed )
			controller.WishVelocity = 0;
	}
}
