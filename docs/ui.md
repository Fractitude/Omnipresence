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
  `z-index` on a piece's root**, from `Code/Hud/_layers.scss`: `$z-menu` (200), `$z-hotbar` (300), `$z-drag`
  (400), `$z-fade` (1000). Import it (`@import "../Hud/_layers.scss";`) and use the variable. A piece with no z-index
  (the readouts, the crosshair) draws below all of them. A menu covers the screen, so it is above the readouts and below
  the ability bar.
- A hot reload does not run `HudSystem` again for a scene that already has its HUD: add a piece, then press Play again.
  Removing a `GameObjectSystem` class makes the open editor log an error until it restarts.
- Leave the scene alone: the pieces are never put in the `.scene`.

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
  (`OnClose`), `Notice` (the host's last answer) and `Hint` (dim advice) under the content. `Width` is fixed pixels; leave it
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
6. The Character menu and the Ability menu predate `MenuWindow` and have their own layout; they still use
   `GameMenus` and `MenuControls`.
