namespace Sandbox;

/// <summary>
/// Puts the packing panel on the screen of every playing client, closed until a packing table opens it.
/// </summary>
public sealed class PackingMenuSystem : GameObjectSystem<PackingMenuSystem>
{
	GameObject menu;

	public PackingMenuSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.StartUpdate, 0, EnsureMenu, "PackingMenuSystem.EnsureMenu" );
	}

	void EnsureMenu()
	{
		// Not in the Character menu's preview scene either: it is a scene too, and the systems start in it.
		if ( menu.IsValid() || CharacterMenu.IsPreview( Scene ) || !Game.IsPlaying || Scene.IsEditor
		     || Application.IsHeadless )
			return;

		menu = new GameObject( true, "Packing menu" ) { NetworkMode = NetworkMode.Never };
		menu.Components.Create<ScreenPanel>().ZIndex = 90;
		menu.Components.Create<PackingMenu>();
	}
}
