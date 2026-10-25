# UI: the HUD and menu windows

Everything the game draws on the screen as UI (not Navi's computer screens) lives on **one HUD GameObject** and, for
menus, in **one shared window**. A new piece should not copy the size rules or make an object of its own.

## The HUD object

- Each scene carries a GameObject named `HUD` with a `ScreenPanel` (ZIndex 100) and the pieces that need
  the editor (`ProgressionHud`, `Crosshair`, `CharacterMenu`). `Hud.Find` (`Code/Hud/Hud.cs`) takes the object that holds the
  `Crosshair`. A scene without one gets an object named HUD with a screen panel made at run time (never saved or networked).
- `HudSystem` (a `GameObjectSystem`) adds every other piece to that object with `hud.Attach<T>()`, once per playing
  client. **A new HUD piece is one line in `HudSystem.Add`.** `Attach` is idempotent (a hot reload finds the first one),
  turns the component on only after it set `NotSaved` and `NotNetworked`, and `Hud.Applies` skips the editor, a
  dedicated server and the Character menu's preview scene. Don't write another `GameObjectSystem` that makes a
  `new GameObject` with a `ScreenPanel`.
- The pieces share the HUD's screen panel, so a ScreenPanel's ZIndex does not order them. **The draw order is the CSS
  `z-index` on a piece's root**, from `Code/Hud/_layers.scss`: `$z-menu` (200, also the organization invitation window `OrgInviteMenu`), `$z-hotbar` (300), `$z-drag`
  (400), `$z-fade` (1000). Import it (`@import "../Hud/_layers.scss";`) and use the variable. A piece with no z-index
  (the readouts, the crosshair) draws below all of them. A menu covers the screen, so it is above the readouts and below
  the ability bar.
- A hot reload does not run `HudSystem` again for a scene that already has its HUD: add a piece, then press Play again.
  Removing a `GameObjectSystem` class makes the open editor log an error until it restarts.
- Leave the scene alone: the pieces are never put in the `.scene`.
- **The cursor is `HudCursor`'s** (`Code/Hud/HudCursor.cs`): hidden and locked every frame unless `GameMenus.IsAnyOpen` or a Navi app has the input, and re-locked when the window regains focus. The HUD's own panels take the mouse, so `MouseVisibility.Auto` would show the cursor over them. A menu sets `Visible` while open and `Hidden` on close, as before.

## A menu window

A window that takes the mouse (packing, planting, press, storage) is a `PanelComponent` whose markup is a
`<MenuWindow>`. Copy `Code/Crime/PressMenu.razor` and its `.scss`:

```razor
<root class="menu-root @( IsOpen ? "open" : "" )">
	@if ( IsOpen )
	{
		<MenuWindow Title="Press bench" Width=@( 560 ) OnClose=@Close Notice=@( ... ) Hint="...">
			<ChildContent>
				<div class="menu-heading">...</div>
				<div class="menu-list">
					<div class="menu-row roomy"> ... <div class="menu-button">Press</div></div>
				</div>
			</ChildContent>
		</MenuWindow>
	}
</root>
```

```scss
@import "../Hud/_hud.scss";
// Only what is the menu's own: the layout inside a row, a two-column body.
```

- `menu-root` (from `_hud.scss`) is the full-screen backdrop: no mouse until `open`, then a dim layer that takes it.
- `MenuWindow` (`Code/Hud/MenuWindow.razor` and `.scss`) is the window: header with `Title` and a Close button
  A menu can be made resizable (`Resizable`, with `Height` and `OnResize`, as `SentBrowser` does): a grip in the corner reports the size
  the player drags to, and the menu keeps it and passes it back as `Width` and `Height`.
  (`OnClose`), `Notice` (the host's last answer) and `Hint` (dim advice) under the content. `ShowClose=@( false )` hides the header Close button for a window with its own way out (the Welcome screen and its editor). `Width` is fixed pixels; leave it
  out for a window that fits its content (up to 96% of the screen, like the storage window). The content goes in
  `<ChildContent>`: s&box needs the tag.
- **The size rules live only in `MenuWindow.razor.scss`**: the window is at most 80% of the screen
  (`$max-height`) and keeps `$bar-clearance` (104px) at the bottom so it never covers the ability bar (56px slots, 24px
  from the edge). Change them there, never in a menu.
- **Lists scroll and rows do not shrink.** Put rows in a `menu-list` (it scrolls and may shrink) and give every row the
  class `menu-row` (it has `flex-shrink: 0`). Headings, notice, hint and header are `flex-shrink: 0` too. A row that is
  not a `menu-row` gets squashed instead of scrolling: do not write your own row without `flex-shrink: 0`.
- The shared classes: `menu-heading`, `menu-list`, `menu-row` (+ `roomy`, `selectable`, `selected`, `unavailable`),
  `menu-icon`, `menu-button` (+ `disabled`), `menu-empty`, `menu-notice`, `menu-hint`, and `warn` / `urgent` for deadlines
  (`CrimeCatalog.DeadlineClass`). They work on everything inside the window, whatever file made it.
- A window that has more than one list of its own (the storage grids) puts them in its own `body`; the window never grows
  past the screen, and `body` scrolls (`overflow: scroll`).

## The right-click menu

`ContextMenu` (`Code/Hud/ContextMenu.razor`, one line in `HudSystem.Add`) is a small menu at the cursor. A window opens it on `@onrightclick` with `ContextMenu.Show( "window name", heading, entries )`, where an entry is a `ContextEntry( Verb, Label, Icon, Enabled, Run )` and the window decides what is listed: `ItemMenus` (`Code/Inventory/ItemMenus.cs`) is the one place that says what a slot of the bag, a storage box or a cargo offers, and `ItemMenus.Bind` gives the verbs their actions. An empty list opens nothing.

- Its root is a full-screen layer at `$z-context` (350): above the menus and the ability hotbar, below the dragged icon (400) and the fade. While it is open the layer takes the mouse, so a click anywhere else only closes it (it does not also press what is under it). It is placed at the mouse and moved so the whole menu stays on the screen (`ContextMenu.Clamp`).
- It closes on choosing an entry, a click outside, Escape and `ContextMenu.HideFor( "window name" )`, which a window calls when it closes. A window that handles Escape must test `ContextMenu.IsOpen` first, hide the menu and consume the key, so the first Escape closes the menu and the second the window (`StorageMenu` and `CharacterMenu` do).
- A slot's mouse-down starts a drag only for the left button; a right press is a right click.
- It does not register with `GameMenus`: it only exists over a window that did.

## What a window must also do

1. **Register with `GameMenus`**: `GameMenus.SetOpen( "name", true )` on open and `false` on every way it closes (Close,
   Escape, walking away, `OnDisabled`). The crosshair and the interaction prompts stand down while any is open.
2. **Free the controls with `MenuControls.Free( false )`** every update while open (it also zeroes `WishVelocity`) and
   `Free( true )` on close, plus `Mouse.Visibility`.
3. **Open only when `!GameMenus.IsAnyOpen`** and no `NaviInputCapture` is active, and close on Escape
   (`Input.EscapePressed = false` after).
4. Use `@onclick` / `@onmousedown`, never `Input` mouse buttons over the panel. Keep the state the handlers share
   `static`. See the pitfalls in [AGENTS.md](../AGENTS.md).
5. Attach it with one line in `HudSystem.Add`. The hotbar (`$z-hotbar`) draws above the window, so keep the window
   clear of it (the bottom margin does that).
6. **A dialog opened from another window** (`DropCashMenu`, the amount dialog of the Character menu's wallet) is a `MenuWindow` piece like the rest, `menu-root` at `$z-menu`, so it draws over the Character menu, which has no z-index; the right-click menu (`$z-context`) draws above both. It registers as `dropcash` and takes the mouse and the keys while open: the Character menu does not close on Escape or its key while `DropCashMenu.IsOpen`, and the dialog gives the controls back on close only if the Character menu is not open under it. A text field is a `TextEntry` (digits only, the handler cleans the text and writes it back; `@onsubmit` is Enter); focus it once the panel exists. The Character menu's `WalletStrip` lists cash, crypto and credits; right-clicking the cash row opens the context menu with Drop.
7. The Character menu and the Ability menu predate `MenuWindow` and have their own layout; they still use
   `GameMenus` and `MenuControls`.
8. A window can also open **by itself** when something arrives (`OrgInviteMenu`, an organization invitation): it opens only
   when the player can take it (alive, not in a seat, `!GameMenus.IsAnyOpen`, no `NaviInputCapture`), otherwise it queues;
   Escape closes it and the item is not offered again that session.

## The welcome screen

`WelcomeScreen` (`Code/Welcome/`, one line in `HudSystem.Add`) opens by itself once per session, as soon as the player can be shown a window (same rule as `OrgInviteMenu`) and only if they have not seen the current version, and by the `welcome` console command at any time. It shows an intro, the rules and the changelog.

- **The text is data** (`WelcomeData` in `WelcomeContent.cs`): every heading, the intro, the rules, the changelog (releases, newest first) and the version. The host keeps it in `welcome.json` and `WelcomeSystem` sends it to each client once it is in the world and on every change (a client shows nothing until it arrives). `WelcomeContent.Save` is host only and nothing from a client is ever read.
- **Version**: a whole number that goes up by 1 on every Save in the editor. The screen saves the version the player closed it on in `welcome_seen.txt` in their own computer's data folder; next time it lists the changelog entries newer than that. Release numbers are automatic: a release added in the editor gets the highest number plus one (a number freed by removing the newest releases is reused, one freed from the middle is not), and nobody types them. The screen lists the releases newer than the newest one the player saw (kept beside the version in the same file); a new player, or one who has seen them all, sees the latest release only. It is not raised per task, and nobody sets it by hand. See AGENTS.md.
- **`WelcomeScreenEditor`** (host only: the Edit button on the screen, or `welcome_edit`) edits a copy of the data in text fields, with add and remove for rules, releases and their lines, and Save, Cancel and Reset to default. Save raises the version by 1 itself. Removing a line draws the lists again (a text field keeps its text by position).
