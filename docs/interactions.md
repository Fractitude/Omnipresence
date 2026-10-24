# Interactions

Anything the player can look at and press Use on is an `Interactable` (`Code/Interaction/`). It is built on the engine's
`Component.IPressable`, so `PlayerController` finds and presses it. While the player looks at one, `Crosshair.razor` lists its
options under the crosshair. The mouse wheel picks one (it stops at the first and last), and Use does it. Other pressables (like
`NaviSpectatorSeat`) show a single hint from `PlayerController.Tooltips`.

## Set one up

1. Give the GameObject (or a child) a collider the player's look can hit.
2. Add `Interactable`. Fill in `Options`: one entry per thing the player can do, in the order they are listed. Each has
   an `Id`, a `Label` (empty hides the row), `Icon` (a Material icon name), `Description` (shown while picked), `Available`,
   `RunOn`, `Cooldown` and `OnInteract`.
3. React to it, any of these:
   - Wire a component's method to an option's `OnInteract` in the editor (it passes the player's GameObject; a method that
     doesn't need it leaves it out).
   - Implement `IInteractionHandler` on a component on the same GameObject or a child, or, if there is none, on the nearest
     parent that has one. The Interactable can sit on a child with the collider while the handler is on the root (`MailBox`).
     To pick the object yourself, set `HandlerObject`: its handlers (and its children's) are used instead.
     A handler gets every option's events: check `e.Option.Id` (or `e.Index`) to tell them apart.
   - Subscribe to `Interactable.Interacted` in code.

## From code

- `Options[i].Available = false` greys a row out and stops the press. Set `Label`, `Icon` and `Description` any time.
  Change the list the same way on every machine: the host takes an option as its place in `Options`.
- `IInteractionHandler.Describe` returns a label for the moment, per option (null keeps the static one). `CanInteract` blocks
  the press.
- `Interactable.Execute` uses an option as if pressed, without checks.

## Multiplayer

An option with `RunOn = Local` runs on the pressing client. `RunOn = Host` sends the press to the host, which finds the
caller's player from the connection, checks the option is a host one, the player is in `Range` and can see the object, and the option is
usable, then runs it. The GameObject must be a network object. Never trust a client's word about who pressed.

`NaviSpectatorSeat` (in the Navi library, which can't use game code) is a plain `IPressable`, so it shows a single hint.

`Range` (default 50) is how far the player's eye can be from the object's colliders (its origin if it has none). Beyond it the prompt is hidden and the press does nothing. A press also has to pass a line-of-sight check: a ray from the eye to one of the object's colliders must hit that collider first. The host runs the same check on every press from a client, from the player's own eye. The engine only finds what the player looks at within `PlayerController.ReachLength`, so that is the most `Range` can reach. 0 turns the checks off.

## Windows hide the prompts

While any full-screen window (a menu that takes the mouse) is open, no interaction prompt or crosshair is drawn, the mouse wheel does not move the picked row, and Use does nothing (`Interactable.Press`). `GameMenus` (`Code/Hud/GameMenus.cs`) is the one switch. **A new window must register with it**: `GameMenus.SetOpen( "name", true )` when it opens and `SetOpen( "name", false )` on every way it closes (its Close, `OnDisabled`, the player leaving). The Character menu, the Ability menu and every `MenuWindow` menu do (see [ui.md](ui.md)). `Crosshair.razor` and `Interactable` read `GameMenus.IsAnyOpen`; do not add another hard-coded menu check.
