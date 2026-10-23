namespace Sandbox;

/// <summary>
/// Which full-screen windows (menus that take the mouse) are open on this computer. The crosshair, the interaction
/// prompts and the Interactable's wheel and press all stand down while any is open. A new window registers when it opens
/// (<see cref="SetOpen"/> with true) and unregisters every way it can close: its Close, OnDisabled, and the player
/// leaving.
/// </summary>
public static class GameMenus
{
	// Not readonly: a hot reload would reset a readonly set of strings to empty while a window is open.
	static HashSet<string> open = new();

	/// <summary>
	/// Whether any window is open, so the world must not draw prompts or react to Use and the wheel.
	/// </summary>
	public static bool IsAnyOpen => open.Count > 0;

	/// <summary>
	/// Register or unregister a window by name.
	/// </summary>
	public static void SetOpen( string name, bool isOpen )
	{
		if ( isOpen )
			open.Add( name );
		else
			open.Remove( name );
	}
}
