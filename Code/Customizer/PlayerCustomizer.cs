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
	/// The outfit applied last is always put back on when the game starts; players cannot turn it off.
	/// </summary>
	public static bool RestoreLastOutfit => true;

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
	/// The outfit picked with Apply, put back on after a respawn. Null means the player's own avatar.
	/// </summary>
	public ClothingContainer AppliedOutfit { get; private set; }

	/// <summary>
	/// Tints picked with Apply, they go with the outfit.
	/// </summary>
	public Dictionary<string, Color> AppliedColors { get; private set; } = [];

	Player _dressedPlayer;
	RealTimeSince _playerChanged;
	bool _redressPending;

	protected override void OnStart()
	{
		_dressedPlayer = Player.FindLocalPlayer();

		if ( RestoreLastOutfit && OutfitPresets.LastOutfit is { } last )
			_ = Restore( last );
	}

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

		RedressAfterRespawn();
	}

	void RedressAfterRespawn()
	{
		var player = Player.FindLocalPlayer();
		if ( player != _dressedPlayer )
		{
			_dressedPlayer = player;
			_playerChanged = 0;
			_redressPending = AppliedOutfit is not null;
		}

		// give the new body's own Dresser a moment to put the avatar on first
		if ( !_redressPending || _playerChanged < 0.5f || !player.IsValid() ) return;

		_redressPending = false;
		player.BroadcastOutfit( AppliedOutfit.Serialize(), OutfitColors.Serialize( AppliedColors ), WorkshopClothing.GetIdents( AppliedOutfit ) );
	}

	async Task Restore( OutfitPreset last )
	{
		var (outfit, colors) = await OutfitPresets.Resolve( last );

		// opened the panel and applied something else meanwhile
		if ( !IsValid || AppliedOutfit is not null ) return;

		ApplyOutfit( outfit, colors );
	}

	/// <summary>
	/// Dress the local player in this outfit for everyone, it stays on after respawns.
	/// </summary>
	public void ApplyOutfit( ClothingContainer outfit, IReadOnlyDictionary<string, Color> colors )
	{
		AppliedOutfit = ClothingContainer.CreateFromJson( outfit.Serialize() );
		AppliedColors = OutfitColors.Copy( colors );
		OutfitPresets.LastOutfit = OutfitPresets.Capture( null, AppliedOutfit, AppliedColors );

		var player = Player.FindLocalPlayer();
		if ( player.IsValid() )
			player.BroadcastOutfit( AppliedOutfit.Serialize(), OutfitColors.Serialize( AppliedColors ), WorkshopClothing.GetIdents( AppliedOutfit ) );
	}

	/// <summary>
	/// Put the local player's own avatar back on.
	/// </summary>
	public void ApplyDefaultAvatar()
	{
		AppliedOutfit = null;
		AppliedColors = [];
		OutfitPresets.LastOutfit = null;

		var player = Player.FindLocalPlayer();
		if ( player.IsValid() )
			player.BroadcastOutfit( null, null, null );
	}

	/// <summary>
	/// The outfit the local player wears right now.
	/// </summary>
	public ClothingContainer GetCurrentOutfit()
	{
		var outfit = AppliedOutfit ?? ClothingContainer.CreateFromLocalUser();
		return ClothingContainer.CreateFromJson( outfit.Serialize() );
	}

	public Dictionary<string, Color> GetCurrentColors() => OutfitColors.Copy( AppliedColors );

	/// <summary>
	/// Dress a player in a saved outfit, on this machine. Called for every machine by <see cref="Player.BroadcastOutfit"/>.
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
