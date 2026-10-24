# Currency

The game has three currencies. Each belongs to a place, and credits never mix with the others. All three live in one component on the player, **`PlayerWallet`** (`Code/Wallet/PlayerWallet.cs`), and nothing else reads a balance. Cash and crypto change only through `Transactions` ([transactions.md](transactions.md)).

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
5. **Crypto and cash convert only through an exchange** (`Exchange`, `Code/Crime/Exchange.cs`), and which exchange you use is what it costs: the **Government Exchange**, in the Payments app, asks 2 percent to buy crypto and 5 percent to sell it, and pays at once; the **darknet Exchange**, in Hush, asks 4 and 8 and makes the dollars wait 2 minutes. The rate is one to one everywhere, and getting there and back always loses money. The terms are per venue in **`ExchangeCatalog`** (`Code/Wallet/ExchangeVenue.cs`); adding an exchange is one line there.
6. **The Payments app moves dollars and crypto** between players, and changes either into the other at the Government Exchange. Hush changes money only at the darknet's.
7. **XP is separate.** XP and level come from either world and are not a currency. Hacking XP and hacking level are a second skill that only hacking puzzles feed.

## In code

- Read a balance with `PlayerWallet.Local` (the player on this computer) or `PlayerWallet.For( connection )`. The values are `[Sync]` from the host, so clients can draw them. `Version` goes up on every change on every machine; a panel hashes it.
- **Move cash and crypto only through `Transactions`** ([transactions.md](transactions.md)), on the host: a transfer between players, a payment to a system party (a shop, a fee) or a payment from one (a payout, a refund), all or none, capped (`MaxMoney`, `MaxCrypto`), written to both players' history (`ProgressRecord.Transactions`, the Payments app's Activity list) and to the ledger file. The old `AddCash` / `TrySpendCash` / `AddCrypto` / `TrySpendCrypto` / `TryPayCash` / `TryPayCrypto` are gone. The only direct additions are `PlayerWallet.GrantUntracked` (a pickup from the ground, a console command). `tools/check_money_writes.py` fails on anything else.
- Credits change only on the host: `AddCredits` / `TrySpendCredits`, capped by `MaxCredits`.
- A change bumps the wallet and `PlayerProgress` versions, saves soon, and posts `Global.IProgressEvents.OnProgressChanged` with `ProgressChange.Wallet`.
- A reward from the Wired calls `AddCredits` (or `PlayerProgress.CompleteIntrusion` / `FinishOperation`, which pay credits through the wallet). A reward from the real world is a `Transactions.Receive` of cash; Hush orders pay crypto the same way (`OrgPayouts.Distribute`).
- The caps live on the record (`ProgressRecord.MaxMoney`, `MaxCrypto`, `AddCredits`, `CanHoldCash`, `CanHoldCrypto`) and the check in `TransactionRules`. A transaction works on a player who is away (escrow pays crypto and a cash-out pays dollars with no player object) the same way as on one who is playing; the caller passes the record it holds.
- The wallet keeps nothing of its own: it reads and writes the player's `ProgressRecord` through `PlayerProgress` (one file per Steam id). The JSON names `Money` and `Credits` are unchanged, so old saves load. `Crypto` came with record version 3; older saves have 0 (`ProgressRecord.Sanitize`).
- `PlayerProgress` creates the `PlayerWallet` on the host if the player prefab doesn't have one, then refreshes the network object so clients receive it. No prefab edit is needed.
- Dev commands: `progress_give_money`, `progress_give_credits`, `progress_give_crypto` (editor only, host checks); money and crypto go through `PlayerWallet.GrantUntracked`.

## Cash on the ground

Only **cash** can be dropped: crypto and credits are digital. A dropped amount is a world item of the kind `cash` (`ItemCatalog.Cash`) that carries its dollars in `ItemData.Amount` (a long, because a balance reaches a trillion; `Count` stays 1). Cash is never in the bag: the balance stays in `PlayerWallet`, and there is no cash slot. Code: `CashDrops` and `CashRules` (`Code/Wallet`).

- **Drop** (the Character menu: right-click the Cash row, Drop, type an amount). The client sends only the amount (`CashDrops.DropRpc`); the host takes the player from the connection and checks the amount is a whole number from 1 to the balance, the player is alive and in the real world (not the Wired), the scene has room (`WorldItem.MaxInScene`) and the player drops at most `CashRules.DropsPerMinute` (20) a minute. The dollars leave the wallet as a `Transactions.Pay` to the `world` party (kind `DropCash`, so it is in the history and the ledger), the pile is made in front of the player (`WorldItem.SpawnFrom`), and if that fails the dollars are given back (`Transactions.Receive`, kind `Refund`). Nothing is lost or made. The player is told in chat: "You dropped $250."
- **Pick up** is the **second exempt path** besides console commands: `CashDrops.Pickup` calls `PlayerWallet.GrantUntracked` with `UntrackedReason.GroundPickup`, because whoever picks a pile up is not who dropped it, so it is no transfer between two parties. It has no history entry and no ledger line (the stats log hears of it). So `Transactions.NetOf( "world", Cash )` only counts what was dropped, never what was picked up again. The prompt reads "Pick up $1,250". Under the wallet cap only the part that fits is taken and the rest stays on the ground ("Picked up $100 of $250. $150 left on the ground."); with a full wallet the pickup is refused. The bag is never asked.
- **Persistence.** The pile is a normal world item (`world_items.json`, see [world-items.md](world-items.md)). Because the wallet and the file change together, a drop and a pickup write the world file at once (`WorldItemRegistry.Flush`) instead of three seconds later, so a crash cannot lose the dollars.

## Reward items

Blueprints and hardware are not currency. They are found in one world and used in the other (a blueprint from a dead server lets you buy a special part at the Hardware Store with cash; the part, installed at home, helps in the Wired), which is how the two worlds connect without a conversion. See [hardware.md](hardware.md).

Design of the crypto side: [design-doc/crime-rp.md](design-doc/crime-rp.md), "Three currencies". Crime loop as built: [crime.md](crime.md).
