namespace Sandbox;

/// <summary>
/// Owns the mouse cursor while the player is playing: hidden and locked to the window, unless a menu is open
/// (<see cref="GameMenus"/>) or a Navi app has the player's input (<see cref="NaviInputCapture"/>), which show it
/// themselves. A HUD piece, put on the HUD object by <see cref="HudSystem"/>. The HUD's own panels (the ability bar, the
/// wallet) take the mouse, so with <see cref="MouseVisibility.Auto"/> the engine would show the cursor over them.
/// </summary>
public sealed class HudCursor : Component
{
	bool wasFocused = true;

	protected override void OnUpdate()
	{
		// Setting Hidden when it already is does nothing, so a lock the OS took (alt-tab, the engine menu, a click on
		// another monitor) was never taken back. On regaining focus, drop to Auto for one frame so Hidden is a real
		// change.
		var focused = Application.IsFocused;
		var regained = focused && !wasFocused;
		wasFocused = focused;

		if ( NaviInputCapture.Current is not null || GameMenus.IsAnyOpen )
			return;

		Mouse.Visibility = regained ? MouseVisibility.Auto : MouseVisibility.Hidden;
	}

	protected override void OnDisabled()
	{
		Mouse.Visibility = MouseVisibility.Auto;
	}
}
