# Player stats log

The host keeps a record of what players do over time: who joined and left and when, playtime, kills (with the weapon),
deaths, every money movement, purchases, kicks and bans. Code: `Code/Stats/`. Read it with `stats_show`
([console-commands.md](console-commands.md)) or open the files.

## Why files, not SQLite

The engine's game sandbox has no database: no SQLite, no `System.Data`. What a game can write is the data folder
(`FileSystem.Data`, on a dedicated server the server's own disk), so the log is two kinds of file there, like the saves:

| File | What it is |
| --- | --- |
| `stats/events_{yyyy}-{MM}.jsonl` | Append-only log, one JSON object (`StatEvent`) per line, one file per month. The truth. |
| `stats/players/{steamId}.json` | `PlayerStatsSummary`: the totals, kept beside the log so nobody has to add the log up. Written with a `.bak` backup like the saves. |

`progress_test_data on` moves both to `stats_test/`. Times are real-world UTC (`DateTimeOffset`), not the game clock
(`WorldClock`). Any tool can read a `.jsonl` file (`jq`, Python, a spreadsheet import), so the data can go into a real
database outside the game if it is ever wanted.

Lines wait in memory and are written every 5 seconds by `StatsSystem` (and when a player leaves or is kicked), so a
crash loses seconds of events. Playtime is added to the totals at the same moments, so a crash loses seconds, not a visit.

## What each line is

`StatEvent.Kind`, with the fields it sets (the rest are null):

| Kind | Fields |
| --- | --- |
| `join`, `leave` | `SteamId`, `Name`; `leave` has `SessionSeconds`. |
| `kill` | the killer as `SteamId`/`Name`, the victim as `OtherId`/`OtherName`, `Weapon`. |
| `death` | the victim as `SteamId`/`Name`, the killer as `OtherId`/`OtherName` (a name only when it was not a player), `Weapon`, `Cause` (damage tags). |
| `money` | `Amount` (signed), `Currency` (`cash`, `credits`, `crypto`), `Balance` after, `Mechanic`, `Detail`, and `OtherId`/`OtherName` when another player gave or received it. |
| `purchase` | `ItemKind`, `ItemId`, `Detail` (name), `Amount` (price). The money has its own `money` line. |
| `kick`, `ban` | `Reason` when given, `OtherId`/`OtherName` for the admin. |

`Mechanic` is one of `StatMechanic`: `payment_app`, `console_command`, `ground_pickup`, `org_payout`, `exchange`, `shop`,
`fee`, `refund`, `hacking`.

## Adding a source of money

`PlayerWallet.AddCash`, `TrySpendCash` and the credits and crypto twins take `mechanic` and `detail`; they write the line
themselves, with the real change (clamped at the cap) and the balance after. Pass a `StatMechanic` and say what it was:

```csharp
wallet.AddCash( reward, StatMechanic.Hacking, $"Intrusion {serverId}" );
```

A path that changes a `ProgressRecord` without a wallet (the player is offline) calls `StatsLog.Money( steamId, ... )` itself
(`Exchange.Settle`, `OrgPayouts.CreditRecord`). Never change a balance without one of these, or the log has a hole.

Logging a purchase: `StatsLog.Purchase( connection, kind, id, name, price )` next to the spend.

## Known gaps

- The engine's own `kick` console command does not go through `GameManager.Kick`, so it is not logged. Use `GameManager.Kick`.
- A share waiting for an offline member (the `OrgRegistry` pending list) is logged when it is paid into their record, not
  when it is put on the list.
- Bans, and buying houses, cars and weapons, do not exist yet: see [../plans/stats.md](../plans/stats.md).
