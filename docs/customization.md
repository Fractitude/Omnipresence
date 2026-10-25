# Player customization: the outfit the server keeps

What a player's character wears (clothing, model, colors) belongs to the **host**, not to the player's computer. Code:
`Code/Customization/`; the window that edits it is the player customizer (`Code/Customizer/`, see its `SOURCE.md`).

- `PlayerCustomization` (a component on every player, made on the host by `Player` and refreshed to the clients, like the
  wallet) holds the outfit as `[Sync( FromHost )]` strings: `OutfitJson`, `ColorsJson`, `Workshop` (package idents for
  clients to download first) and `Loaded`. Every machine dresses the body from them in `OnUpdate`, so a player who joins
  later, and a body that respawns, wear the outfit from the first frame. It is **not** in `PlayerProgress` /
  `ProgressRecord`: it is not progress and has its own file.
- **Saved** by the host in the data folder, `customization/{steamId}.json` (with the `.bak` copy `FileProgressStore`
  keeps), read when the player's object starts and written on every change. A player keeps the outfit on any computer.
  Presets (the Presets tab) still live in the player's own cookies.
- **Changing it**: the owner calls `PlayerCustomization.Request` (the customizer's Apply and Default Avatar do). The owner's
  screen puts the outfit on at once, and `Submit` (`[Rpc.Host]`) asks the host: caller must be the owner (no limit on how often), then `CustomizationRules` cleans it and the result is saved and synced. What the host cleans, it writes
  the same way the owner's screen did, so the echo is not dressed twice.
- **Validation and rules** (`CustomizationRules`): the outfit must parse and be under 32 KB; at most 40 items; every other
  field must be a number or a flag; colors at most 64; at most 16 well-formed `org.package` idents. Then the item rules in
  `customization/rules.json`: mode `off` (anything), `allow` (only the listed) or `block` (all but the listed). An item is a
  clothing's resource path, or a folder with `*` at the end (`hl2_metrocop/*` is a whole workshop package). Items are named
  by the paths in the outfit, which the host reads itself: the idents a client sends are never used to decide. Players are
  told when something was left out. Commands (host only): `customization_mode`, `customization_add`,
  `customization_remove`, `customization_list`; changing a rule cleans the outfits of the players that are in the game.
- Not checked: that a path is a real clothing (an unknown path just fails to dress), and the appearance numbers' range.
- An older outfit kept in the player's cookies is sent to the server once, if the server has none, and the cookie is
  cleared (`PlayerCustomizer.MigrateCookie`).
