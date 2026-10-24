# Transactions

Cash and crypto move between parties through one system, **`Transactions`** (`Code/Wallet/Transactions.cs`). Nothing else
changes a cash or crypto balance, apart from two named exceptions below. Credits are not part of it (they never leave the
player; see [currency.md](currency.md)).

## The model

| Piece | File | What it is |
| --- | --- | --- |
| `TxLeg` | `TransactionTypes.cs` | One movement: a `Party` to a `Party`, a `TransactionCurrency` (`Cash`, `Crypto`), an amount, a `TxKind`, a note and a ref (an order or item id). |
| `Party` | `TransactionTypes.cs` | A player (`Party.Player( steamId )`) or a system party (`Party.System( id )`). |
| `Parties` | `TransactionTypes.cs` | The system parties and their labels (below). |
| `TxKind` | `TransactionTypes.cs` | Why: `OrderPayout`, `OrgShare`, `ShopPurchase`, `DepotPurchase`, `ExchangeSell`, `ExchangeBuy`, `CashOut`, `PlayerPayment`, `Fee`, `RegistrationFee`, `Refund`, `Other`. |
| `TxResult` | `TransactionTypes.cs` | `Ok`, a `TxFailure` reason, a message for the player, the group id and what each leg moved. `ToCrimeResult()` turns it into a panel answer. |
| `TransactionRules` | `TransactionRules.cs` | The pure part: `Validate` a leg, `Plan` a batch against the records, `Apply` it. Works on `ProgressRecord`s only. |
| `Transactions` | `Transactions.cs` | The host part: finds the records, runs the rules, writes the history, the ledger and the stats log, tells the wallet. |
| `TransactionEntry` | `TransactionTypes.cs` | One line of a player's history, from that player's side. |
| `TransactionLedger` | `TransactionLedger.cs` | The world's append-only audit file. |

A system party has no balance. Money paid to one leaves the players' world, money it pays is made, and both are written down:
`Transactions.NetOf( "market", TransactionCurrency.Cash )` is what that party took in (positive) or paid out (negative). The
players' balances change by exactly the opposite of the sum over every system party, which the tests check with random batches.

### Parties

| Id | Label | Used for |
| --- | --- | --- |
| `world` | The world | Money made from nothing (an order payout that does not come from escrow). |
| `hush-escrow` | Hush escrow | Order payouts and the shares of them. |
| `hush-shop` | Hush shop | Crypto spent in the Hush shop. |
| `hardware-store` | Hardware Store | Dollars spent on hardware. |
| `depot` | Depot | Dollars spent in the Depot. |
| `exchange` | Exchange | Both exchanges: the crypto taken, the dollars paid, and the fee it keeps. |
| `government` | Government | Organization registration and its refund. |
| `market` | Market | Reserved for the market. |

**To add a system party:** add a constant and a label in `Parties` (`TransactionTypes.cs`). A party that is not registered is
refused (`UnknownParty`). **To add a kind:** add it at the end of `TxKind` (a kind is stored by name, never rename one), map it
in `Transactions.MechanicOf` for the stats log, and give it a label in `PaymentsViewModel.LabelOf`.

## The API (host only)

```csharp
Transactions.Transfer( from, to, currency, amount, kind, note, ref )   // any two parties
Transactions.Pay( playerId, "depot", currency, amount, kind, note )    // a player pays a system party
Transactions.Receive( playerId, "world", currency, amount, kind )      // a system party pays a player
Transactions.Batch( legs, group )                                      // several legs, all or none
await Transactions.BatchAsync( legs )                                  // loads a player who is not in memory first
```

- **Atomic.** `TransactionRules.Plan` checks every leg first: the amount is positive and at most `MaxAmount`, the currency and kind
  exist, both parties are real, source and destination differ, every player's record is available, and the balances worked out
  over the whole batch stay between zero and the cap (`MaxMoney`, `MaxCrypto`; arithmetic is `checked`). Only then does
  `Apply` change anything, so a failing leg changes nothing. Debit equals credit for two players; a system party is the explicit
  sink or mint.
- **`TxLeg.UpTo`** moves as much as the destination can hold instead of refusing (for the queue of an absent member's share, and
  only from a system party). `TxResult.Amount` is what fitted.
- **Who is the player.** A connected player with a loaded `PlayerProgress` is changed through it (the wallet and the HUD update,
  the save is scheduled). Otherwise the record is the one the caller passes (`TxLeg.FromRecord` / `ToRecord`, the offline
  processor, which saves it itself), otherwise one in `PlayerProgression.Cache` (saved by the system). A player in none of those
  gets `Unavailable`; `BatchAsync` loads them, saves them and lets them go.
- **Rate limit.** A player makes at most `Transactions.PaymentsPerMinute` (30) `PlayerPayment`s a minute.
- **Trust.** Only host code calls it. The Payments app sends nothing but the amount and which listed player; the seat is the payer.
  Ids are made by the host (`Guid`), never taken from a client.

## The exempt paths

Two things add money without a transaction, and they are named for it: `PlayerWallet.GrantUntracked( currency, amount, reason )`
with `UntrackedReason.GroundPickup` (picking money up from the ground; no such item exists yet, so the future feature calls this)
and `UntrackedReason.ConsoleCommand` (`progress_give_money`, `progress_give_crypto`, `PlayerWallet.DebugGrant`). They write no
history entry and no ledger line; the stats log still records them. **They are the only direct mutations.**

`tools/check_money_writes.py` is the guard: it fails when any file but `TransactionRules.cs`, `Transactions.cs`, `PlayerWallet.cs`
and `ProgressRecord.cs` calls an old mutator (`AddCash`, `TrySpendCrypto`, `TryPayCash`, ...), `ProgressRecord.Settle`, or writes
`.Money` / `.Crypto`. Run it before committing a change that touches money.

## History

Each `ProgressRecord` keeps its last 200 transactions (`ProgressRecord.Transactions`, oldest first, `TransactionEntry`): id, group,
game time, currency, signed amount, kind, the other party (a Steam id, or `s:id`) and its name then, note, ref. Record version 4
moved the old `Payments` list into it (a payment became a `PlayerPayment` entry) and `Payments` is now always empty.
`PlayerProgress.TransactionHistory` reads it; the Payments app's Activity list shows it (kind, note, `+`/`-` amount).

## The ledger

`transactions/ledger_{year}-{month}_{n}.log` in the game's data folder (`transactions_test/` under `progress_test_data on`): one JSON
object a line (`TransactionLine`: id, group, UTC and game time, currency, amount, `From`/`To` as `p:{steamId}` or `s:{id}`, their
names, kind, note, ref). Lines wait in memory and `TransactionSystem` writes them every 5 s. A file that reaches 4 MB is left and
the next lines go to the next `n`. Host only. The stats log ([stats.md](stats.md)) also gets a money line for each player leg.

## Callers

| Flow | Call |
| --- | --- |
| Hush order released from escrow | `OrgPayouts.Distribute` (a group, below) |
| Waiting org share arriving | `OrgPayouts.CreditRecord` (`UpTo`) |
| Exchange sell / buy | a two-leg batch, crypto and dollars |
| Delayed cash-out | `Exchange.Settle`: `Receive` from `exchange` into the record |
| Hush shop | `Pay` to `hush-shop` |
| Hardware store | `Pay` to `hardware-store` |
| Depot | `Pay` to `depot` |
| Organization registration fee and refund | `Pay` / `Receive` with `government` |
| Payments app | `Transfer` player to player |

**Org payouts.** One logical payout is one group. The payer (`hush-escrow` for an order, `world` otherwise) pays the receiver's
own part (`OrderPayout`) and each live teammate's share (`OrgShare`) directly, so the receiver never holds the whole amount and a
full wallet can't block a share. The arithmetic (`OrgRules.Split`), the cap checks, the queue for absent members (their
transaction happens when the entry is delivered) and the idempotent ledger (`OrgRecord.Payouts`) are unchanged.
