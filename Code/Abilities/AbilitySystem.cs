namespace Sandbox;

/// <summary>
/// Puts the hotbar and the abilities menu on the screen of every playing client, so no scene needs to carry them.
/// </summary>
public sealed class AbilitySystem : GameObjectSystem<AbilitySystem>
{
	GameObject hud;

	public AbilitySystem( Scene scene ) : base( scene )
	{
		Listen( Stage.StartUpdate, 0, EnsureHud, "AbilitySystem.EnsureHud" );
	}

	void EnsureHud()
	{
		if ( hud.IsValid() || !Game.IsPlaying || Scene.IsEditor || Application.IsHeadless )
			return;

		hud = new GameObject( true, "Abilities" ) { NetworkMode = NetworkMode.Never };
		hud.Components.Create<ScreenPanel>().ZIndex = 90;
		hud.Components.Create<AbilityHotbar>();
		hud.Components.Create<AbilityMenu>();
	}
}
