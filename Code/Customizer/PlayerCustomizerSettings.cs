using Sandbox.UI;
namespace Sandbox;

/// <summary>
/// Settings shown in the Settings tab of the player customizer.
/// </summary>
public sealed class PlayerCustomizerSettings
{
	public const string DefaultOpenKey = "O";

	/// <summary>
	/// Uses the "O" key when empty.
	/// </summary>
	[Property, Group( "Input" ), Title( "Open Menu" )]
	public ClientInput OpenMenuAction { get; set; }

	/// <summary>
	/// The key that opens the menu, as shown to the player.
	/// </summary>
	public string OpenKeyName => OpenMenuAction.IsEnabled
		? Input.GetButtonOrigin( OpenMenuAction.Action ) ?? OpenMenuAction.Action
		: DefaultOpenKey;

	/// <summary>
	/// Saved in the cookies, like the rest of the customizer's preferences.
	/// </summary>
	[Property, Group( "Outfit" ), Title( "Restore Last Outfit" )]
	[Description( "When the addon spawns, put the outfit applied last back on." )]
	public bool RestoreLastOutfit
	{
		get => PlayerCustomizer.RestoreLastOutfit;
		set => PlayerCustomizer.RestoreLastOutfit = value;
	}

	public bool OpenPressed()
	{
		return OpenMenuAction.IsEnabled
			? Input.Pressed( OpenMenuAction.Action )
			: Input.Keyboard.Pressed( DefaultOpenKey );
	}
}
