namespace Sandbox;

/// <summary>
/// The one GameObject that carries the game's HUD: the scene's own object named HUD (the one with the
/// <see cref="Crosshair"/> and its <see cref="ScreenPanel"/>), or, in a scene that has none, one made here. Every HUD
/// piece the game adds at run time goes on it with <see cref="Attach{T}"/>, so a piece costs one line in
/// <see cref="HudSystem"/> and no object of its own. The pieces share the HUD's screen panel, so what draws above what
/// is the CSS <c>z-index</c> on each piece's root (the <c>$z-</c> numbers in <c>Code/Hud/_hud.scss</c>), not a
/// panel's ZIndex.
/// </summary>
public static class Hud
{
	/// <summary>
	/// The HUD's screen panel ZIndex when this makes the object (the scene's own HUD sets its own).
	/// </summary>
	const int ScreenZIndex = 100;

	/// <summary>
	/// Whether this scene is one that draws the HUD: a playing game's own scene. Not the editor's, a dedicated
	/// server's, or one the game makes to draw something else (the Character menu's preview, the item icons): those
	/// are scenes too, and the systems start in them.
	/// </summary>
	public static bool Applies( Scene scene ) =>
		Game.IsPlaying && !scene.IsEditor && !Application.IsHeadless && !CharacterMenu.IsPreview( scene );

	/// <summary>
	/// The HUD object of a scene: the one that holds the <see cref="Crosshair"/>, else a new object named HUD with a
	/// screen panel. Never saved or networked: it is every client's own.
	/// </summary>
	public static GameObject Find( Scene scene )
	{
		var hud = scene.GetAllComponents<Crosshair>().FirstOrDefault()?.GameObject;

		if ( !hud.IsValid() )
			hud = new GameObject( true, "HUD" ) { NetworkMode = NetworkMode.Never };

		if ( hud.Components.Get<ScreenPanel>() is null )
			hud.Components.Create<ScreenPanel>().ZIndex = ScreenZIndex;

		return hud;
	}

	/// <summary>
	/// Put a HUD piece on the HUD object, once: a second call, or a hot reload, finds the first and does nothing.
	/// </summary>
	public static T Attach<T>( this GameObject hud ) where T : Component, new()
	{
		if ( hud.Components.Get<T>() is { } existing )
			return existing;

		// Off while it is set up, so it starts with its flags already on: runtime only, never saved into the scene or
		// sent to a client (every client makes its own).
		var piece = hud.Components.Create<T>( false );
		piece.Flags |= ComponentFlags.NotSaved | ComponentFlags.NotNetworked;
		piece.Enabled = true;
		return piece;
	}
}

/// <summary>
/// Puts the HUD pieces the scene does not carry on the HUD object of every playing client. A new piece is one line in
/// <see cref="Add"/>. Its draw order against the rest is the <c>z-index</c> in its stylesheet (see <see cref="Hud"/>).
/// </summary>
public sealed class HudSystem : GameObjectSystem<HudSystem>
{
	GameObject hud;

	public HudSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.StartUpdate, 0, EnsureHud, "HudSystem.EnsureHud" );
	}

	void EnsureHud()
	{
		if ( hud.IsValid() || !Hud.Applies( Scene ) )
			return;

		hud = Hud.Find( Scene );
		Add( hud );
	}

	/// <summary>
	/// Every HUD piece the game adds at run time, one per line. Menus cover the screen and take the mouse: they
	/// register with <see cref="GameMenus"/> and free the controls with <see cref="MenuControls"/>.
	/// </summary>
	static void Add( GameObject hud )
	{
		// The fade is above everything; it starts clear, so a new scene never begins black.
		hud.Attach<ScreenFade>();

		hud.Attach<HealthHud>();
		hud.Attach<AmmoHud>();
		hud.Attach<NpcSubtitles>();

		hud.Attach<PackingMenu>();
		hud.Attach<PlantingMenu>();
		hud.Attach<PressMenu>();
		hud.Attach<StorageMenu>();
		hud.Attach<DropCashMenu>();
		hud.Attach<ContextMenu>();
		hud.Attach<OrgInviteMenu>();
		hud.Attach<WelcomeScreen>();
		hud.Attach<WelcomeScreenEditor>();

		// The hotbar draws above the menus, and the dragged icon above both.
		hud.Attach<AbilityMenu>();
		hud.Attach<AbilityHotbar>();
		hud.Attach<AbilityDragGhost>();
	}
}
