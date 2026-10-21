namespace Sandbox;

/// <summary>
/// Puts the ammo readout on the screen of every playing client, so no scene needs to carry it.
/// </summary>
public sealed class WeaponHudSystem : GameObjectSystem<WeaponHudSystem>
{
	GameObject hud;

	public WeaponHudSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.StartUpdate, 0, EnsureHud, "WeaponHudSystem.EnsureHud" );
	}

	void EnsureHud()
	{
		// Not in the Character menu's preview scene either: it is a scene too, and the systems start in it.
		if ( hud.IsValid() || CharacterMenu.IsPreview( Scene ) || !Game.IsPlaying || Scene.IsEditor
		     || Application.IsHeadless )
			return;

		hud = new GameObject( true, "Weapon HUD" ) { NetworkMode = NetworkMode.Never };
		hud.Components.Create<ScreenPanel>().ZIndex = 80;
		hud.Components.Create<AmmoHud>();
	}
}
