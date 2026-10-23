namespace Sandbox;

/// <summary>
/// Puts the pressing panel on the screen of every playing client, closed until a press bench opens it.
/// </summary>
public sealed class PressMenuSystem : GameObjectSystem<PressMenuSystem>
{
	GameObject menu;

	public PressMenuSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.StartUpdate, 0, EnsureMenu, "PressMenuSystem.EnsureMenu" );
	}

	void EnsureMenu()
	{
		// Not in the Character menu's preview scene either: it is a scene too, and the systems start in it.
		if ( menu.IsValid() || CharacterMenu.IsPreview( Scene ) || !Game.IsPlaying || Scene.IsEditor
		     || Application.IsHeadless )
			return;

		menu = new GameObject( true, "Press menu" ) { NetworkMode = NetworkMode.Never };
		menu.Components.Create<ScreenPanel>().ZIndex = 90;
		menu.Components.Create<PressMenu>();
	}
}
