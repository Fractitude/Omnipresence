namespace Sandbox;

/// <summary>
/// Puts the health readout on the screen of every playing client, so no scene needs to carry it.
/// </summary>
public sealed class HealthHudSystem : GameObjectSystem<HealthHudSystem>
{
	GameObject hud;

	public HealthHudSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.StartUpdate, 0, EnsureHud, "HealthHudSystem.EnsureHud" );
	}

	void EnsureHud()
	{
		// Not in the Character menu's preview scene either: it is a scene too, and the systems start in it.
		if ( hud.IsValid() || CharacterMenu.IsPreview( Scene ) || !Game.IsPlaying || Scene.IsEditor
		     || Application.IsHeadless )
			return;

		hud = new GameObject( true, "Health HUD" ) { NetworkMode = NetworkMode.Never };
		hud.Components.Create<ScreenPanel>().ZIndex = 80;
		hud.Components.Create<HealthHud>();
	}
}
