using Sandbox.UI;
namespace Sandbox;

/// <summary>
/// The player customizer: on the HUD object, one per client. Opens with the Customizer key (O) or the
/// <c>customize</c> command and changes the clothing and look of the local player, for everyone. Taken from the
/// workshop's Player Customizer by Zontax, see SOURCE.md.
/// </summary>
public sealed class PlayerCustomizer : Component
{
	public const string PackageIdent = "zontax.player_customizer";

	/// <summary>
	/// The menu's name in <see cref="GameMenus"/>.
	/// </summary>
	const string MenuName = "customizer";

	const string IconSizeCookie = "player_customizer.icon_size";
	const string PreviewBackgroundCookie = "player_customizer.preview_background";
	const string SortOrderCookie = "player_customizer.sort_order";
	const string VerifiedOnlyCookie = "player_customizer.verified_only";

	/// <summary>
	/// How the workshop list is sorted, remembered like the spawn menu's.
	/// </summary>
	public static PackageSortMode SortOrder
	{
		get => Game.Cookies.Get( SortOrderCookie, PackageSortMode.Popular );
		set => Game.Cookies.Set( SortOrderCookie, value );
	}

	/// <summary>
	/// Only show workshop clothing that is, or was, sold.
	/// </summary>
	public static bool VerifiedOnly
	{
		get => Game.Cookies.Get( VerifiedOnlyCookie, false );
		set => Game.Cookies.Set( VerifiedOnlyCookie, value );
	}

	/// <summary>
	/// What's behind the model in the preview, see-through by default so it sits on the panel.
	/// </summary>
	public static Color PreviewBackground
	{
		get => Game.Cookies.Get( PreviewBackgroundCookie, Color.Transparent );
		set => Game.Cookies.Set( PreviewBackgroundCookie, value );
	}

	/// <summary>
	/// How big the clothing tiles are, remembered between sessions like the spawn menu's.
	/// </summary>
	public static SpawnMenuIconSize IconSize
	{
		get => Game.Cookies.Get( IconSizeCookie, SpawnMenuIconSize.Medium );
		set => Game.Cookies.Set( IconSizeCookie, value );
	}

	public bool IsOpen { get; set; }

	/// <summary>
	/// Open the customizer for the local player.
	/// </summary>
	[ConCmd( "customize" )]
	public static void Open()
	{
		if ( Game.ActiveScene?.GetAllComponents<PlayerCustomizer>().FirstOrDefault() is { } customizer )
			customizer.TryOpen();
	}

	/// <summary>
	/// The key that opens the menu, as shown to the player.
	/// </summary>
	public static string OpenKeyName => Input.GetButtonOrigin( OpenAction ) ?? "TAB";

	const string OpenAction = "Customizer";

	/// <summary>
	/// The saved outfit kept in the cookies by older versions is sent to the server once, if the server has none.
	/// </summary>
	bool _migrated;

	protected override void OnDisabled()
	{
		SetMenuOpen( false );
	}

	/// <summary>
	/// Opens it unless another menu is open or the player is typing somewhere.
	/// </summary>
	void TryOpen()
	{
		if ( IsOpen || GameMenus.IsAnyOpen || NaviInputCapture.Current is not null || !Player.FindLocalPlayer().IsValid() )
			return;

		IsOpen = true;
	}

	// The window is open or closed by anything setting IsOpen; the game's menus, the mouse and the keys follow.
	void SetMenuOpen( bool open )
	{
		if ( open == _menuOpen )
			return;

		_menuOpen = open;
		GameMenus.SetOpen( MenuName, open );

		if ( open )
			return;

		MenuControls.Free( true );
		Mouse.Visibility = MouseVisibility.Hidden;
	}

	bool _menuOpen;

	protected override void OnUpdate()
	{
		// typing in a text entry shouldn't toggle the menu
		if ( InputFocus.Current is null && !Input.Suppressed && Input.Pressed( OpenAction ) )
		{
			if ( IsOpen )
				IsOpen = false;
			else
				TryOpen();
		}

		if ( IsOpen && Input.EscapePressed )
		{
			Input.EscapePressed = false;
			IsOpen = false;
		}

		SetMenuOpen( IsOpen );

		if ( IsOpen )
		{
			Mouse.Visibility = MouseVisibility.Visible;
			MenuControls.Free( false );
		}

		MigrateCookie();
	}

	// From when the outfit was kept on the player's computer: the server has it now (PlayerCustomization).
	void MigrateCookie()
	{
		if ( _migrated || PlayerCustomization.Local is not { Loaded: true } customization )
			return;

		_migrated = true;

		if ( customization.HasOutfit || OutfitPresets.LastOutfit is not { } last )
		{
			OutfitPresets.LastOutfit = null;
			return;
		}

		_ = Restore( last );
	}

	async Task Restore( OutfitPreset last )
	{
		var (outfit, colors) = await OutfitPresets.Resolve( last );

		// opened the panel and applied something else meanwhile
		if ( !IsValid || PlayerCustomization.Local is not { HasOutfit: false } ) return;

		ApplyOutfit( outfit, colors );
		OutfitPresets.LastOutfit = null;
	}

	/// <summary>
	/// Dress the local player in this outfit for everyone. The server keeps it (cleaned under its rules) and puts it
	/// back after a respawn or a new session.
	/// </summary>
	public void ApplyOutfit( ClothingContainer outfit, IReadOnlyDictionary<string, Color> colors )
	{
		PlayerCustomization.Local?.Request( outfit.Serialize(), OutfitColors.Serialize( colors ), WorkshopClothing.GetIdents( outfit ) );
	}

	/// <summary>
	/// Put the local player's own avatar back on.
	/// </summary>
	public void ApplyDefaultAvatar()
	{
		PlayerCustomization.Local?.Request( null, null, null );
	}

	/// <summary>
	/// The outfit the local player wears right now.
	/// </summary>
	public ClothingContainer GetCurrentOutfit()
	{
		var json = PlayerCustomization.Local is { HasOutfit: true } customization ? customization.OutfitJson : null;
		return json is null ? ClothingContainer.CreateFromJson( ClothingContainer.CreateFromLocalUser().Serialize() ) : ClothingContainer.CreateFromJson( json );
	}

	public Dictionary<string, Color> GetCurrentColors() => OutfitColors.Deserialize( PlayerCustomization.Local?.ColorsJson );

	/// <summary>
	/// Dress a player in a saved outfit, on this machine. Called by <see cref="PlayerCustomization"/> on every machine.
	/// </summary>
	public static async Task Dress( Dresser dresser, string json, Dictionary<string, Color> colors, string workshopIdents )
	{
		// workshop clothing is stored by path, which only resolves once its package is here
		await WorkshopClothing.Mount( workshopIdents );

		if ( !dresser.IsValid() ) return;

		// sandbox lets anyone wear sold clothing, owning it only matters to other game modes
		var outfit = ClothingContainer.CreateFromJson( json );

		await dresser.ApplyAsync( outfit );
		OutfitColors.Apply( dresser.BodyTarget, outfit, colors );
	}

	public static async Task DressDefault( Dresser dresser )
	{
		await dresser.Apply();
		OutfitColors.Apply( dresser.BodyTarget, null, null );
	}
}
