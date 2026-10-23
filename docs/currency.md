# Currency

The game has two currencies built, and a **third planned: Crypto**, earned on the dark web (see "Crypto (planned)" below). Each belongs to a place, and dollars and credits never mix.

| Currency | World | Symbol | Field |
| --- | --- | --- | --- |
| Dollars | The real world | `$` | `PlayerProgress.Money` |
| Credits | The Wired (under the map, in the same scene) | credits | `PlayerProgress.Credits` |

## Rules

1. **Activity in the Wired only earns credits.** Dead servers, minigames and anything else done in the Wired pay in credits, never dollars.
2. **Activity in the real world only earns dollars.** Jobs, selling things and anything else done at home pay in dollars, never credits.
3. **Each currency is spent in its own world.** Dollars buy real-world things (hardware, mail, posters). Credits buy Wired things.
4. **No conversion between dollars and credits.** There is no exchange between the two. Code must not add a way to turn one into the other. (Crypto, when it exists, converts to and from dollars through an exchange, never to or from credits. See below.)
5. **XP is separate.** XP and level come from either world and are not a currency. Hacking XP and hacking level are a second skill that only hacking puzzles feed.

## Crypto (planned, not built)

A third currency, **Crypto** (placeholder name Cipher), is earned on the dark web (Hush orders, selling data) and spent there (ingredients, equipment, market fees). It converts to and from dollars through an exchange with fees, delays and evidence, never to credits. Design: [design-doc/crime-rp.md](design-doc/crime-rp.md), "Three currencies". Nothing in this document's code section covers it yet. When built, it needs its own field in `PlayerProgress` and `ProgressRecord` (`Crypto`, with its own maximum), host-only methods like the others, and no path that turns credits into it.

## In code

- `PlayerProgress.AddMoney` / `TrySpend` are for dollars. `AddCredits` / `TrySpendCredits` are for credits. All are host only.
- A reward from the Wired calls `AddCredits` (or `CompleteIntrusion`, which pays credits). A reward from the real world calls `AddMoney`.
- Player to player payments (the Payments app) move dollars only.
- Both are saved in `ProgressRecord` (`Money`, `Credits`), each capped at its own maximum.

## Reward items

Blueprints and hardware are not currency. They are found in one world and used in the other (a blueprint from a dead server lets you buy a special part at the Hardware Store with dollars; the part, installed at home, helps in the Wired), which is how the two worlds connect without a conversion. See [hardware.md](hardware.md).
