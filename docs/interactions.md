# Interactions

Anything the player can look at and press Use on is an `Interactable` (`Code/Interaction/`). It is built on the engine's
`Component.IPressable`, so `PlayerController` finds and presses it. `Crosshair.razor` draws the hint for whatever
`PlayerController.Tooltips` holds, so any pressable shows one.

## Set one up

1. Give the GameObject (or a child) a collider the player's look can hit.
2. Add `Interactable`. Set `Label`, and optionally `Icon` (a Material icon name) and `Description`.
3. React to it, any of these:
   - Wire a component's method to `OnInteract` in the editor (it passes the player's GameObject; a method that doesn't need it leaves it out).
   - Implement `IInteractionHandler` on a component on the same GameObject or a child, or, if there is none, on the nearest
     parent that has one. The Interactable can sit on a child with the collider while the handler is on the root (`MailBox`).
   - Subscribe to `Interactable.Interacted` in code.

## From code

- `Available = false` greys the hint out and stops the press. Set `Label`, `Icon` and `Description` any time.
- `IInteractionHandler.Describe` returns a label for the moment (null keeps the static one). `CanInteract` blocks the press.
- `Interactable.Execute` uses it as if pressed, without checks.

## Multiplayer

`RunOn = Local` runs on the pressing client. `RunOn = Host` sends the press to the host, which finds the caller's player from
the connection, checks it is within `HostRange` and that the object is usable, then runs it. The GameObject must be a network
object. Never trust a client's word about who pressed.

`NaviSpectatorSeat` (in the Navi library, which can't use game code) is a plain `IPressable`, so it shows its hint the same way.
