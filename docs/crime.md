# Crime: grow, press, ship, get paid

The first playable slice of the crime game, as built. Grow Static, press it into tabs, take an order in the **Hush** app, pack a parcel, post it from your mailbox, and get paid from escrow in **crypto** with a review. Cash out through the Exchange. Everything is rough placeholder gameplay: no minigames, no police, no disputes. The plan and the reasons are in [design-doc/crime-rp-first-build.md](design-doc/crime-rp-first-build.md); the three currencies are in [currency.md](currency.md).

```
Static culture ──plant──► Grow Tray ──wait (game clock)──► harvest ──► raw Static
raw Static ──press──► Press Bench ──► tabs (a batch with a quality)
Hush: accept an order ──► pack (takes tabs, gives a parcel) ──► Mailbox: send
Mailman collects on the clock ──► in transit ──► delivered ──► buyer reviews ──► escrow releases
escrow pays crypto (minus a fee by reputation) ──► Hush Exchange: crypto ──► dollars (fee, delay)
```

## What exists, and where

| Piece | Code | Notes |
| --- | --- | --- |
| **Wallet** | `Code/Wallet/PlayerWallet.cs` | Cash, credits and crypto. See [currency.md](currency.md). |
| **Items** | `Code/Inventory/ItemData.cs`, `ItemCatalog.cs`, `PlayerInventory.cs` | A batch has `Kind`, `Count`, `Quality`, `OrderId`. Kinds: `static_culture`, `static_raw`, `static_tab`, `parcel`. Host helpers: `AddItem`, `CountOf`, `TryTake`, `TakeBatch`. A parcel cannot be dropped. |
| **Crime state** | `Code/Crime/CrimeRecord.cs` | One section of `ProgressRecord.Crime`. Wipe it alone for a season. |
| **Rules** | `Code/Crime/PlayerCrime.cs` | A component on the player (made by `PlayerProgress` like the wallet). Every action runs on the host. |
| **Grow tray** | `Code/Crime/GrowTray.cs`, `Assets/crime/grow_tray.prefab` | One option; its label follows the looking player's own tray: Plant, Growing (countdown), Harvest. A world text above it (a local `TextRenderer` child made in code, never networked or saved) shows `Growing 62%  0:46` or `Ready to harvest` for the local player's own tray, only within 250 units. |
| **Press bench** | `Code/Crime/PressBench.cs`, `Assets/crime/press_bench.prefab` | Presses all the raw Static carried into one batch of tabs. |
| **Mailbox** | `Code/Objects/MailBox.cs` | Gets a "Send parcel" option in code. Claimed by the first player who sends from it. The flag goes up while a parcel waits. |
| **Post** | `Code/Crime/PostalSystem.cs` | The mailman collects at every multiple of 5 game minutes; transit takes 3. |
| **Market** | `Code/Crime/MarketSystem.cs` | A `GameObjectSystem`. NPC buyers' open orders, not saved. |
| **Exchange** | `Code/Crime/Exchange.cs` | The only crypto and dollars conversion. |
| **Hush app** | `Code/Apps/Hush/`, `Assets/navi/system/hush.napp` | Orders, Shipments, Wallet, Reviews, Exchange. A shared system app. Home only. |
| **Tuning numbers and text** | `Code/Crime/CrimeCatalog.cs` | All times, prices, fees, and the hand-written note and review text. |
| **Dev commands** | `Code/Crime/CrimeCommands.cs` | See [console-commands.md](console-commands.md). |

## The loop

1. **Start.** A new player gets 2 Static cultures (`CrimeCatalog.StartCultures`) the first time their crime state is made.
2. **Plant.** Use a grow tray with a culture in the bag. It is ready `GrowSeconds` later on the `WorldClock`, so time offline counts. The crop quality is picked at random when planting (placeholder).
3. **Harvest.** Gives `HarvestRaw` raw Static and a culture back, so the loop can go on.
4. **Press.** Use a press bench. All raw Static becomes one batch of tabs: `TabsPerRaw` tabs per raw, quality = the average of the raw nudged by up to +/-`PressQualitySpread` (placeholder for the Tuning minigame).
5. **Orders.** In Hush the board shows 3 to 5 NPC orders (handle, note, count of tabs, minimum quality, price). Buyers are honest or picky (hidden from the UI): picky ones ask for 10 more quality, pay a little more and review harder. **Accept** takes it off the board, reserves it for you and holds its payment in escrow. You have `DeliverWindowSeconds` to deliver; at most 5 orders at once.
6. **Pack.** The host checks the bag holds the count at the minimum quality or better, takes those tabs and gives one **parcel** item that remembers the order.
7. **Send.** Use the mailbox with a parcel in the bag. The first sender claims the box (remembered in the save). The flag goes up. At the next collection time the mailman takes it (Shipments: "Collected, in transit"), and `TransitSeconds` later it is delivered.
8. **Review and escrow.** On delivery the buyer reviews (1 to 5 stars from how far the quality beat the ask, a picky buyer is harder) and escrow releases `EscrowSeconds` later. You get the price minus a fee by reputation (10 percent Newcomer, 7 Known after 3 good trades, 4 Trusted after 10 good trades and a rating of 4 or more). A good trade counts.
9. **Late.** An order that is not delivered by its deadline is cancelled: a bad trade, a 1-star review, and a parcel still in your bag is taken.
10. **Exchange.** Sell crypto for dollars at 1 to 1 minus 8 percent; the dollars arrive `CashoutDelaySeconds` later. Buy crypto with dollars at 1 to 1 minus 4 percent, at once.

## Tuning

All in `Code/Crime/CrimeCatalog.cs`. Times are game seconds (1 real second each). They are short for testing.

| Constant | Now | Meaning |
| --- | --- | --- |
| `GrowSeconds` | 10 | Tray growth (short for testing). |
| `HarvestRaw` / `TabsPerRaw` | 12 / 4 | One harvest is 48 tabs. |
| `CollectPeriod` / `TransitSeconds` | 300 / 180 | Mailman rhythm and transit. |
| `EscrowSeconds` | 60 | Delay after delivery. |
| `OpenSeconds` / `DeliverWindowSeconds` | 1800 / 3600 | How long an order stays on the board; time to deliver after accepting. |
| `MinOpenOrders` / `MaxOpenOrders` / `RefillSeconds` | 3 / 5 / 30 | Board size and refill. |
| `OrderCountMin..Max`, `OrderQualityMin..Max` | 10..30, 30..70 | What buyers ask for. Tab base price is 10 crypto (`ItemCatalog`), spread 80 to 130 percent. |
| `FeeNewcomer` / `FeeKnown` / `FeeTrusted` | 10 / 7 / 4 | Percent kept from escrow. |
| `SellFeePercent` / `BuyFeePercent` / `CashoutDelaySeconds` | 8 / 4 / 120 | The Exchange. |

## What is saved

`ProgressRecord.Crime` (`CrimeRecord`), in the player's progress file, record version 3:

- `Items`: the batches in the inventory, copied from it whenever it changes and put back when the player's crime state starts. (The inventory itself is not saved; ordinary picked-up items are still lost on restart.)
- `Trays` (by the tray object's id), `Orders` (accepted, with state), `Parcels`, `Cashouts`, `Reviews` (last 20), reputation counters, `MailboxId`, `Started`.
- Not saved: the open orders on the board. They start fresh at every start.

Old saves load: the section defaults to empty, and `Sanitize` clamps everything.

## Security

Same rules as the apps in AGENTS.md.

- **The host decides everything.** `PlayerCrime` methods run on the host only. The tray, bench and mailbox are `Interactable` options that run on the host (the host re-checks the player's reach). Hush is a shared app: its logic runs in the host's copy, and the seat comes from the computer (`GameAppSeat`), never from an action.
- **Actions carry ids and amounts only.** `accept <orderId>`, `pack <orderId>`, `sell <amount>`, `buy <amount>`, `tab <name>`. The price, count, quality, times and fees come from the host's records. An exchange amount is the player's own to give and is checked against their balance and a maximum.
- **Buyers' secrets stay on the host.** The buyer type is not in the UI state. Nothing that sends a message as an NPC exists yet (buyer notes are plain text in the order).
- **Credits are never touched.** Crypto and dollars change only in `Exchange`.
- **Parcels cannot be dropped or faked.** `PlayerInventory.DoDrop` refuses a parcel. `crime_give` is editor only.
- **Hush needs home and the network.** In the Wired it says "No signal"; offline it says so. The host checks the same again before acting.
- A client's mailbox claim is by Steam id on the host, and a box whose object is gone is let go.

## Placing things in a scene

`Assets/scenes/home.scene` has one of each placed through the editor (the scene change is not committed): **Grow Tray** at (-80, -60, 0) and **Press Bench** at (-80, -20, 0) (both from the prefabs in `Assets/crime/`), next to the existing **MailBox**. To place more by hand:

- **Grow tray / press bench:** drag the prefab in. Or make a GameObject with a model, a collider, an `Interactable` and a `GrowTray` (or `PressBench`) component; **Network Mode must be Object**, or the press never reaches the host. The options are added in code, so the `Interactable` can keep its default option.
- **Mailbox:** the existing `Assets/MailBox.prefab` works as is (it is a network object). The "Send parcel" option is added in code. Its first option still toggles the flag by hand.
- Each tray is its own tray in the save, by its object's id. Do not duplicate a placed tray and expect two to share state.

## Known gaps

- **Not tested on a second client.** The wallet and crime components reach clients by a network refresh made on the host; this was checked on the host (singleplayer) only. Check that a client sees its own cash and the tray labels.
- **The Hush panel was drawn and its text read, but not clicked** by a real mouse. Its view model and all the rules were driven directly.
- **Ordinary inventory items are still not saved**, only crime batches.
- **Dropping a non-parcel item still only logs.** Batches (Static, tabs) can be dropped and are lost.
- No buyer messages in the Messages app yet (`NaviNpcs`), no Hot flag, no tutorial order, no sounds. Quality is a random placeholder. One home, one mailbox, the first claimant.
- Tabs are the only product; one order asks for one kind.
- A cash-out waits in the save until the wallet can hold it; the player is not told when it lands.
- Two players at one mailbox: the first sender owns it.

## Dev loop

`progress_give_money`, `crime_give static_tab 40 70`, `crime_tray_ready`, `crime_orders`, `crime_postal_now`, `world_time_skip_minutes`. See [console-commands.md](console-commands.md).
