namespace Sandbox;

/// <summary>
/// Puts the storage panel on the screen of every playing client, closed until a storage box opens it.
/// </summary>
public sealed class StorageMenuSystem : GameObjectSystem<StorageMenuSystem>
{
	GameObject menu;

	public StorageMenuSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.StartUpdate, 0, EnsureMenu, "StorageMenuSystem.EnsureMenu" );
	}

	void EnsureMenu()
	{
		// Not in the Character menu's preview scene either: it is a scene too, and the systems start in it.
		if ( menu.IsValid() || CharacterMenu.IsPreview( Scene ) || !Game.IsPlaying || Scene.IsEditor
		     || Application.IsHeadless )
			return;

		menu = new GameObject( true, "Storage menu" ) { NetworkMode = NetworkMode.Never };
		menu.Components.Create<ScreenPanel>().ZIndex = 90;
		menu.Components.Create<StorageMenu>();
	}
}
