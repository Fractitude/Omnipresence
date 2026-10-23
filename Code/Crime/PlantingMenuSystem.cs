namespace Sandbox;

/// <summary>
/// Puts the planting panel on the screen of every playing client, closed until a grow tray opens it.
/// </summary>
public sealed class PlantingMenuSystem : GameObjectSystem<PlantingMenuSystem>
{
	GameObject menu;

	public PlantingMenuSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.StartUpdate, 0, EnsureMenu, "PlantingMenuSystem.EnsureMenu" );
	}

	void EnsureMenu()
	{
		// Not in the Character menu's preview scene either: it is a scene too, and the systems start in it.
		if ( menu.IsValid() || CharacterMenu.IsPreview( Scene ) || !Game.IsPlaying || Scene.IsEditor
		     || Application.IsHeadless )
			return;

		menu = new GameObject( true, "Planting menu" ) { NetworkMode = NetworkMode.Never };
		menu.Components.Create<ScreenPanel>().ZIndex = 90;
		menu.Components.Create<PlantingMenu>();
	}
}
