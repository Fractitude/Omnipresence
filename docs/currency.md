# Currency

The game has three currencies. Each belongs to a place, and credits never mix with the others. All three live in one component on the player, **`PlayerWallet`** (`Code/Wallet/PlayerWallet.cs`), and nothing else reads or changes a balance.

| Currency | World | Symbol | Wallet value | Saved as |
| --- | --- | --- | --- | --- |
| Cash (dollars) | The real world | `$` | `PlayerWallet.Cash` | `ProgressRecord.Money` |
| Credits | The Wired (under the map, in the same scene) | credits | `PlayerWallet.Credits` | `ProgressRecord.Credits` |
| Crypto | The dark web (Hush, in the real world) | crypto | `PlayerWallet.Crypto` | `ProgressRecord.Crypto` |

## Rules

1. **Activity in the Wired only earns credits.** Dead servers, minigames and anything else done in the Wired pay in credits, never cash.
2. **Activity in the real world earns cash**, and **dark web trades (Hush orders) pay crypto**.
3. **Each currency is spent in its own world.** Cash buys real-world things (hardware, mail, posters). Credits buy Wired things. Crypto buys dark web things.
4. **Credits never convert to anything.** No code path turns credits into cash or crypto, or the other way round.
5. **Crypto and cash convert only through the Exchange** (the Hush app's Exchange tab, `Code/Crime/Exchange.cs`): crypto to cash with an 8 percent fee and a delay, cash to crypto with a 4 percent fee. The Payments app moves cash only.
6. **XP is separate.** XP and level come from either world and are not a currency. Hacking XP and hacking level are a second skill that only hacking puzzles feed.

## In code

- Read a balance with `PlayerWallet.Local` (the player on this computer) or `PlayerWallet.For( connection )`. The values are `[Sync]` from the host, so clients can draw them. `Version` goes up on every change on every machine; a panel hashes it.
- Change a balance only on the host: `AddCash` / `TrySpendCash`, `AddCredits` / `TrySpendCredits`, `AddCrypto` / `TrySpendCrypto`, each capped by `ProgressRecord` (`MaxMoney`, `MaxCredits`, `MaxCrypto`). `TryPayCash` moves cash between two players and writes both payment histories.
- A change bumps the wallet and `PlayerProgress` versions, saves soon, and posts `Global.IProgressEvents.OnProgressChanged` with `ProgressChange.Wallet`.
- A reward from the Wired calls `AddCredits` (or `PlayerProgress.CompleteIntrusion` / `FinishOperation`, which pay credits through the wallet). A reward from the real world calls `AddCash`; Hush calls `AddCrypto`.
- The caps and the arithmetic of a balance live on the record (`ProgressRecord.AddCash`, `AddCrypto`, `AddCredits`, `CanHoldCash`, `CanHoldCrypto`); `PlayerWallet` delegates to them and then shows and saves the change. The crime rules (`CrimeRules`, `Exchange.Settle`) change the record directly for a player who is away: escrow pays crypto and a cash-out pays dollars with no player object, under the same caps.
- The wallet keeps nothing of its own: it reads and writes the player's `ProgressRecord` through `PlayerProgress` (one file per Steam id). The JSON names `Money` and `Credits` are unchanged, so old saves load. `Crypto` came with record version 3; older saves have 0 (`ProgressRecord.Sanitize`).
- `PlayerProgress` creates the `PlayerWallet` on the host if the player prefab doesn't have one, then refreshes the network object so clients receive it. No prefab edit is needed.
- Dev commands: `progress_give_money`, `progress_give_credits`, `progress_give_crypto` (editor only, host checks).

## Reward items

Blueprints and hardware are not currency. They are found in one world and used in the other (a blueprint from a dead server lets you buy a special part at the Hardware Store with cash; the part, installed at home, helps in the Wired), which is how the two worlds connect without a conversion. See [hardware.md](hardware.md).

Design of the crypto side: [design-doc/crime-rp.md](design-doc/crime-rp.md), "Three currencies". Crime loop as built: [crime.md](crime.md).
