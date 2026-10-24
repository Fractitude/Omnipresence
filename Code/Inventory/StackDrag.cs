using Sandbox.UI;

namespace Sandbox;

/// <summary>
/// What the inventory panels need to know about combining and splitting while an item is dragged: which modifier keys
/// are held, whether a drop would work, and the label to show beside the dragged icon. The rules themselves are
/// <see cref="StackRules"/>; the host decides again when the drop is sent.
/// </summary>
public static class StackDrag
{
	/// <summary>The hint under the inventory panels.</summary>
	public const string Hint = "Shift: combine stacks. Ctrl: split half. Both: add half to a stack.";

	/// <summary>
	/// The modifier keys held now: Shift gives <see cref="StackMode.Combine"/>, Ctrl <see cref="StackMode.Split"/>. The
	/// panel event's own flags count too, and so do the keys themselves, since an event from a world panel comes
	/// without the flags.
	/// </summary>
	public static StackMode Held( PanelEvent e = null )
	{
		var mode = StackMode.None;

		var shift = e is MousePanelEvent { HasShift: true };
		var ctrl = e is MousePanelEvent { HasCtrl: true };

		if ( shift || Input.Keyboard.Down( "SHIFT" ) || Input.Keyboard.Down( "RSHIFT" ) )
			mode |= StackMode.Combine;

		if ( ctrl || Input.Keyboard.Down( "CTRL" ) || Input.Keyboard.Down( "RCTRL" ) )
			mode |= StackMode.Split;

		return mode;
	}

	/// <summary>
	/// Whether a drop with this mode would be tried at all. Without a key it is a plain move. Shift on an empty slot
	/// and Ctrl alone on an occupied one do nothing, and the player is not told off for them.
	/// </summary>
	public static bool Worth( ItemData over, StackMode mode ) => mode switch
	{
		StackMode.None => true,
		StackMode.Combine => over is not null,
		StackMode.Split => over is null,
		_ => true,
	};

	/// <summary>
	/// Whether a drop of <paramref name="dragged"/> on <paramref name="over"/> (null for an empty slot) with this mode
	/// would work. A plain move always does.
	/// </summary>
	public static bool Works( ItemData dragged, ItemData over, StackMode mode ) =>
		mode == StackMode.None
		|| (Worth( over, mode ) && StackRules.Plan( dragged, over, mode, out _, out _, out _ ));

	/// <summary>
	/// The label beside the dragged icon, or null with no key held. It names the action while it can work, and says
	/// what is wrong when it cannot. <paramref name="overSlot"/> is false while the mouse is not on another slot.
	/// </summary>
	public static string Label( ItemData dragged, ItemData over, StackMode mode, bool overSlot )
	{
		if ( mode == StackMode.None )
			return null;

		var action = mode switch
		{
			StackMode.Combine => "Combine",
			StackMode.Split => "Split half",
			_ => "Split half and combine",
		};

		if ( !overSlot )
			return action;

		if ( !Worth( over, mode ) )
			return mode == StackMode.Combine ? "Drop on a stack" : "Drop on an empty slot";

		return StackRules.Plan( dragged, over, mode, out _, out _, out var error ) ? action : error;
	}
}
