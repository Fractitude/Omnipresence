namespace Sandbox;

/// <summary>
/// Puts the fade-to-black layer on the screen of every playing client, above the rest of the HUD, so no scene needs to
/// carry it. <see cref="ScreenFade.IsBlack"/> turns it on and off.
/// </summary>
public sealed class ScreenFadeSystem : GameObjectSystem<ScreenFadeSystem>
{
	GameObject hud;

	public ScreenFadeSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.StartUpdate, 0, EnsureHud, "ScreenFadeSystem.EnsureHud" );
	}

	void EnsureHud()
	{
		// Not in the Character menu's preview scene either: it is a scene too, and the systems start in it.
		if ( hud.IsValid() || CharacterMenu.IsPreview( Scene ) || !Game.IsPlaying || Scene.IsEditor
		     || Application.IsHeadless )
			return;

		// It starts clear: a hot reload or a new scene mustn't leave the screen black.
		ScreenFade.IsBlack = false;

		hud = new GameObject( true, "Screen Fade" ) { NetworkMode = NetworkMode.Never };
		hud.Components.Create<ScreenPanel>().ZIndex = 1000;
		hud.Components.Create<ScreenFade>();
	}
}
