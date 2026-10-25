# Player Customizer: where it came from

Taken from the workshop package **Player Customizer** by **Zontax**: https://sbox.game/zontax/player_customizer
(`zontax.player_customizer`, version 428253, taken 2026-10-25).

The source was read from the downloaded package (`sent_import`) and is now ours: edit it freely, the author's updates change
nothing here. What changed when it was brought in:

- Namespace `Zontax.Addons` is `Sandbox`. The SCP-294 vending machine and the example entity in the same package were not kept.
- It is no longer a spawnable entity: `PlayerCustomizer` (the logic) and `PlayerCustomizerPanel` (the window) are HUD pieces
  (`Hud.cs`), opened with the `Customizer` input (Tab) or the `customize` command.
- The outfit is sent by `Player.BroadcastOutfit` (`Code/Health/Player.Outfit.cs`) on the player's own object, not by the entity.
- The sandbox game's menu bar, spawn menu, notices and `Ownable` are gone, and so are the Settings tab, the view switch and closing on Apply; the last outfit is always restored.
- The widgets it draws with (`UIButton`, `UIDropdown`, `UIModal`, ...) and `Theme.scss` are the sandbox game's
  (`github.com/Facepunch/sandbox`, MIT), in `Code/UI/`. `UIModal` is a smaller version without the property sheet.
- The preview scene is made before its panel and kept out of the game systems (`AvatarPreview.IsPreviewScene`).
