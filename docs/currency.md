# Currency

The game has two currencies. Each belongs to one world, and the two never mix.

| Currency | World | Symbol | Field |
| --- | --- | --- | --- |
| Dollars | The real world (`home.scene`) | `$` | `PlayerProgress.Money` |
| Credits | The Wired (`wired.scene`) | credits | `PlayerProgress.Credits` |

## Rules

1. **Activity in the Wired only earns credits.** Dead servers, minigames and anything else done in the Wired pay in credits, never dollars.
2. **Activity in the real world only earns dollars.** Jobs, selling things and anything else done at home pay in dollars, never credits.
3. **Each currency is spent in its own world.** Dollars buy real-world things (hardware, mail, posters). Credits buy Wired things.
4. **No conversion.** There is no exchange between the two. Code must not add a way to turn one into the other.
5. **XP is separate.** XP and level come from either world and are not a currency.

## In code

- `PlayerProgress.AddMoney` / `TrySpend` are for dollars. `AddCredits` / `TrySpendCredits` are for credits. All are host only.
- A reward from the Wired calls `AddCredits` (or `CompleteIntrusion`, which pays credits). A reward from the real world calls `AddMoney`.
- Player to player payments (the Payments app) move dollars only.
- Both are saved in `ProgressRecord` (`Money`, `Credits`), each capped at its own maximum.

## Reward items

Blueprints and other items are not currency. They are found in one world and used in the other (for example a blueprint from a dead server is built at home with dollars), which is how the two worlds connect without a conversion.
