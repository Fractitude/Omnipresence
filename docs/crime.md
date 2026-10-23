# Crime: grow, press, ship, get paid

The first playable slice of the crime game, as built. Grow Static, press it into tabs, buy supplies in the **Hush** shop, take an order in the **Hush** app, pack a parcel at the packing table, post it from your mailbox, and get paid from escrow in **crypto** with a review. Cash out through the Exchange. Everything is rough placeholder gameplay: no minigames, no police, no disputes. The plan and the reasons are in [design-doc/crime-rp-first-build.md](design-doc/crime-rp-first-build.md); the three currencies are in [currency.md](currency.md).

```
Hush Shop: buy cultures with crypto ──► in transit ──► waits in the Mailbox ──► collect
Static culture ──plant──► Grow Tray ──wait (game clock)──► harvest ──► raw Static
raw Static ──press──► Press Bench ──► tabs (a batch with a quality)
Hush: accept an order ──► Packing table: pick the order and the batch (any quality) ──► Mailbox: send all parcels
Mailman collects on the clock ──► in transit ──► delivered ──► buyer reviews ──► escrow releases
the buyer rates it by their personality, the quality against the ask, and how late it was
escrow pays crypto (minus a fee by reputation) ──► Hush Exchange: crypto ──► dollars (fee, delay)
```

## What exists, and where

| Piece | Code | Notes |
| --- | --- | --- |
| **Wallet** | `Code/Wallet/PlayerWallet.cs` | Cash, credits and crypto. See [currency.md](currency.md). |
| **Items** | `Code/Inventory/ItemData.cs`, `ItemCatalog.cs`, `PlayerInventory.cs` | A batch has `Kind`, `Count`, `Quality`, `OrderId`. Kinds: `static_culture`, `static_raw`, `static_tab`, `parcel`. Each kind has a `Category` ("Static") for the Hush filters. Host helpers: `AddItem`, `CountOf`, `TryTake`, `TakeFrom` (exact count from one slot), `TakeBatch`. A parcel cannot be dropped. A parcel's `Contents` list (kind, count, quality) says what is inside; the inventory details list it by name ("Static tabs x10, quality 66"), and the parcel's own count and quality are not shown. The tables in `ItemCatalog` and `CrimeCatalog` are properties that build on each call, so an edit shows after a hot reload (see AGENTS.md, "Static state and hot reload"). |
| **Crime state** | `Code/Crime/CrimeRecord.cs` | One section of `ProgressRecord.Crime`. Wipe it alone for a season. |
| **Rules** | `Code/Crime/PlayerCrime.cs`, `Code/Crime/CrimeRules.cs` | `PlayerCrime` is a component on the player (made by `PlayerProgress` like the wallet); every action runs on the host. The rules that run on the clock (post, shop deliveries, reviews, escrow, deadlines, cash-outs, hardware shipments) are `CrimeRules.Advance( steamId, record, now )`, which works on the `ProgressRecord` alone. `PlayerCrime` runs it once a second for a player who is there. |
| **Offline** | `Code/Crime/OfflineCrimeSystem.cs`, `OfflineCrimeProcessor.cs`, `Code/Progression/ProgressCache.cs`, `Code/Crime/MailboxRegistry.cs` | Runs the same `CrimeRules.Advance` for players who are not there. See "When a player is away". |
| **Grow tray** | `Code/Crime/GrowTray.cs`, `Assets/crime/grow_tray.prefab` | One option; its label follows the looking player's own tray: Plant, Growing (countdown), Harvest. A world text above it (a local `TextRenderer` child made in code, never networked or saved) shows `Growing 62%  0:46` or `Ready to harvest` for the local player's own tray, only within 250 units. |
| **Press bench** | `Code/Crime/PressBench.cs`, `Assets/crime/press_bench.prefab` | Presses all the raw Static carried into one batch of tabs. |
| **Packing table** | `Code/Crime/PackingTable.cs`, `PackingMenu.razor`, `PackingMenuSystem.cs`, `Assets/crime/packing_table.prefab` | One option, which runs on the pressing player's own client and opens the **packing panel** (`PackingMenu`, a screen panel made by `PackingMenuSystem`). Left: the accepted orders still to pack (handle, count, asked quality, time left to send). Right, for the picked order: the bag's batches of the same kind with enough count, each with a Pack button and a comparison ("Quality 55, asked 40+: above the ask" / "meets" / "below"). It shows the result message, closes with the Close button or Escape (or when the player walks away), frees no mouse for the game while open, and redraws as the bag and orders change. The buyer's personality is not shown. The prompt is "Pack orders" (disabled "No accepted orders"). The order list comes from `PlayerCrime.PackOrders`, which the host sends to the owning player alone (not synced to other clients). The Pack button calls `PlayerCrime.RequestPack( orderId, slot )`: the host checks the caller is the player, that a packing table is within `PackingTable.Reach`, and then `PlayerCrime.Pack` checks the order and the slot. |
| **Mailbox** | `Code/Objects/MailBox.cs` | Two options, set in code (anything else is removed): **Send all parcels (N)** and **Collect parcels (M)**. Whichever is used first claims an unclaimed box. The flag goes up while a parcel waits for the mailman and is for outgoing parcels only. M is `PlayerCrime.MailWaiting`, sent by the host to the owning player alone. |
| **Shop** | `Code/Crime/CrimeCatalog.cs` (`Shop`), `PlayerCrime.BuyFromShop` | Hush's Shop tab. Crypto buys supplies (first: Static culture, 40 each, 120 s). They go to `CrimeRecord.Deliveries`, travel on the `WorldClock` (`PostalSystem.TickDeliveries`) and wait in the mailbox until collected. |
| **Post** | `Code/Crime/PostalSystem.cs` | The mailman collects at every multiple of 5 game minutes; transit takes 3. |
| **Market** | `Code/Crime/MarketSystem.cs` | A `GameObjectSystem`. NPC buyers' open orders, not saved. |
| **Exchange** | `Code/Crime/Exchange.cs` | The only crypto and dollars conversion. |
| **Hush app** | `Code/Apps/Hush/`, `Assets/navi/system/hush.napp` | Orders (filter chips by product category with counts), Your orders (filter chips by status with counts, and a to-do count on the tab), Shop, Shipments (incoming supplies and sent parcels, separate), Wallet, Reviews, Exchange. A shared system app. Home only. The window can be resized and maximised (`hush.napp`: `CanResize`, `CanMaximize`, `MinSize` 360,300); the default is 600 wide and the cards wrap into columns. It cannot pack: Your orders says to pack at the packing table. The filters live in the view model (per seat, reset when the seat changes); their actions carry a category or status name only, which the host checks against the catalogue and the status list. |
| **Tuning numbers and text** | `Code/Crime/CrimeCatalog.cs` | All times, prices, fees, and the hand-written note and review text. |
| **Dev commands** | `Code/Crime/CrimeCommands.cs` | See [console-commands.md](console-commands.md). |

## The loop

1. **Start.** A new player gets 2 Static cultures (`CrimeCatalog.StartCultures`) the first time their crime state is made. More come from the Shop: buy them in Hush with crypto, then **collect** them from the mailbox once they have arrived.
2. **Plant.** Use a grow tray with a culture in the bag. It is ready `GrowSeconds` later on the `WorldClock`, so time offline counts. The crop quality is picked at random when planting (placeholder).
3. **Harvest.** Gives `HarvestRaw` raw Static and a culture back, so the loop can go on.
4. **Press.** Use a press bench. All raw Static becomes one batch of tabs: `TabsPerRaw` tabs per raw, quality = the average of the raw nudged by up to +/-`PressQualitySpread` (placeholder for the Tuning minigame).
5. **Orders.** In Hush the board shows 3 to 5 NPC orders (handle, note, count of tabs, **asked quality**, price). Each buyer has a hidden **personality** (see Review): a Connoisseur asks for 10 more quality and pays 15 points more of price spread; the others ask as listed. The ask is a guide for the rating, not a requirement. **Accept** takes it off the board, reserves it for you and holds its payment in escrow. You have `DeliverWindowSeconds` to deliver; at most 5 orders at once.
6. **Pack.** Use the packing table at home (not Hush) and pick an accepted order and one of your batches in the panel. Any quality can be packed; the batch must be of the order's kind and hold at least the ordered count. The host takes exactly the ordered count from that batch and gives one **parcel** item that remembers the order, the batch's quality and what is inside.
7. **Send.** Use **Send all parcels** at the mailbox: every parcel in the bag goes in one use, up to `MaxParcels` in the post at once (the rest stay in the bag, and the message says so). The first sender or collector claims the box (remembered in the save). The flag goes up. At the next collection time the mailman takes them (Shipments: "Collected, in transit"), and `TransitSeconds` later they are delivered.
8. **Review and escrow.** On delivery the buyer rates the batch (1 to 5 stars, see "The rating rule") and escrow releases `EscrowSeconds` after the parcel arrived (also when you were away at the time). The agreed price is always paid in full; only the rating (and so reputation and the fee level) changes with quality and lateness. You get the price minus a fee by reputation (10 percent Newcomer, 7 Known after 3 good trades, 4 Trusted after 10 good trades and a rating of 4 or more). A good trade counts.
9. **Late.** The deadline only governs **sending**. An order still Accepted or Packed (not yet in the mailbox) at its deadline is cancelled: a bad trade, a 1-star review, and a parcel still in your bag is taken. Once sent it is never cancelled by the clock; a parcel that arrives after the deadline is still paid for, but the buyer rates it lower and says in the review that it took too long.
10. **Exchange.** Sell crypto for dollars at 1 to 1 minus 8 percent; the dollars arrive `CashoutDelaySeconds` later. Buy crypto with dollars at 1 to 1 minus 4 percent, at once.

## The rating rule

`CrimeCatalog.Stars`. A buyer's score starts at `BaseStars` (3) plus the personality's bias. Let `delta` = quality delivered minus quality asked.

- Shortfall (`delta` < 0): lose one star per `ShortfallPerStar` points beyond the personality's `Tolerance`.
- Excess (`delta` > 0): gain `delta * ExcessWeight / PointsPerStar` stars (`PointsPerStar` = 7.5).
- Lateness: no penalty if the parcel arrives by the deadline. Late loses 1 star, plus 1 more for every further `LateStepSeconds` (300) late, at most `LateMaxStarsLost` (3), times the personality's `LateWeight` (rounded down). An on-time parcel can earn an Impatient buyer `EarlyBonus` if it arrives within `EarlyFraction` (25 percent) of the delivery window.
- The score is rounded, may move one star at random if the personality has noise, is capped by its `MaxStars` (a Skeptic gives 5 only at a raw score of 5.5 or more), and kept between its `MinStars` and 5. The review line is hand-written, by personality and stars; a late parcel gets a line that says it took too long.

| Personality | Weight | Ask / price | Tolerance | Points per star short | Excess weight | Bias | Noise | Late weight | Early bonus | Min / Max stars |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Layman | 35 | 0 / 0 | 20 | 15 | 2.0 | 0 | 20% of 1 star | 1 | 0 | 1 / 5 |
| Connoisseur | 20 | +10 / +15 | 0 | 8 | 0.5 | 0 | none | 1 | 0 | 1 / 5 |
| Impatient | 15 | 0 / 0 | 0 | 7.5 | 1.0 | 0 | none | 2 | +1 | 1 / 5 |
| Easygoing | 15 | 0 / 0 | 10 | 7.5 | 1.0 | +1 | none | 0.5 | 0 | 3 / 5 |
| Skeptic | 15 | 0 / 0 | 0 | 7.5 | 1.0 | -1 | none | 1 | 0 | 1 / 4 |

The personality is host-side data on the market order and the saved `OrderRecord`, never synced and never in the Hush state or the packing panel. It shows only in the sound of the buyer's note. An old save with only the old buyer type is moved on load (`Sanitize`): Honest becomes Easygoing, Picky becomes Connoisseur.

## Tuning

All in `Code/Crime/CrimeCatalog.cs`. Times are game seconds (1 real second each). They are short for testing.

| Constant | Now | Meaning |
| --- | --- | --- |
| `GrowSeconds` | 10 | Tray growth (short for testing). |
| `HarvestRaw` / `TabsPerRaw` | 12 / 1 | One harvest is 12 tabs: pressing gives as many tabs as the raw Static put in. |
| `CollectPeriod` / `TransitSeconds` | 300 / 180 | Mailman rhythm and transit. |
| `EscrowSeconds` | 60 | Delay after delivery. |
| `OpenSeconds` / `DeliverWindowSeconds` | 1800 / 3600 | How long an order stays on the board; time to **send** after accepting (the deadline). |
| `LateStepSeconds` / `LateMaxStarsLost` | 300 / 3 | Late arrival costs 1 star plus 1 per step, up to the cap. |
| `BaseStars` / `PointsPerStar` / `EarlyFraction` | 3 / 7.5 / 0.25 | The rating rule above. |
| `ShopMaxQuantity` / `ShopMaxInTransit` / `ShopQuantities` | 20 / 10 / 1, 5, 10 | The Shop. Static culture: 40 crypto, 120 s. |
| `MaxParcels` | 10 | Parcels in the post at once. |
| `MinOpenOrders` / `MaxOpenOrders` / `RefillSeconds` | 3 / 5 / 30 | Board size and refill. |
| `OrderCountMin..Max`, `OrderQualityMin..Max` | 10..30, 30..70 | What buyers ask for. Tab base price is 10 crypto (`ItemCatalog`), spread 80 to 130 percent. |
| `FeeNewcomer` / `FeeKnown` / `FeeTrusted` | 10 / 7 / 4 | Percent kept from escrow. |
| `SellFeePercent` / `BuyFeePercent` / `CashoutDelaySeconds` | 8 / 4 / 120 | The Exchange. |

## What is saved

`ProgressRecord.Crime` (`CrimeRecord`), in the player's progress file, record version 3:

- `Items`: the batches in the inventory, copied from it whenever it changes and put back when the player's crime state starts. (The inventory itself is not saved; ordinary picked-up items are still lost on restart.)
- A parcel item keeps its `Contents` (capped at 20 entries, counts and qualities clamped on load; an unknown kind shows by its stored name; an old parcel has none and shows no lines).
- `Deliveries`: what was bought in the Shop (kind, count, quality, `ArriveAt`, in transit or in the mailbox). Old saves have none.
- Each accepted order keeps its `Personality` and the `PackedQuality`; `MinQuality` is the asked quality.
- `Trays` (by the tray object's id), `Orders` (accepted, with state), `Parcels`, `Cashouts`, `Reviews` (last 20), reputation counters, `MailboxId`, `Started`.
- Not saved: the open orders on the board. They start fresh at every start (what a player accepted is in their record).
- Next to the records (`progress/`): `_pending.json`, the ids of players with work waiting on the clock; each file also has a `.bak` copy of its previous write. In the data folder: `mailboxes.json` (which player owns which mailbox) and `world.json` (the game clock).

Old saves load: the section defaults to empty, and `Sanitize` clamps everything.

## When a player is away

Orders in the post, shop deliveries, cash-outs and unsent orders keep going while a player is not playing, and the server remembers them across a restart. Everything is applied to the player's own saved record.

- **Who runs it.** `OfflineCrimeSystem` (a `GameObjectSystem`, host only) drives an `OfflineCrimeProcessor` once a second. For every player with work waiting it loads the record through `PlayerProgression` and runs `CrimeRules.Advance` at the clock's time: parcels are collected and delivered, the buyer's review is written, escrow pays crypto, a cash-out lands, a shop delivery reaches the mailbox, an unsent order is cancelled at its deadline (a bad trade and a 1-star review, as always), hardware shipments arrive. It saves the record at once. It serves a player who left, a player who is dead and has not respawned, and a player the server has not seen since it started.
- **The same rules everywhere.** A player who comes back is caught up by the same call, so the result does not depend on who ran it or how often. Every event happens at its own time: the review is stamped with the delivery time, escrow releases at the delivery plus `EscrowSeconds`, releases run in the order they fall due (the fee depends on the good trades so far), a cancel is stamped with the deadline. The buyer's dice are seeded by player and order, so a replay gives the same stars. A payment the wallet cannot hold waits and is tried again a minute (game time) later.
- **One record, no race.** `ProgressCache` holds one in-memory record per player. A player's `PlayerProgress` attaches to it from the moment it is loaded and detaches when the object goes; the processor never changes an attached record, and checks that at the moment it changes one. Both run on the main thread and each step is synchronous, so a player who connects while the processor works sees the whole result.
- **Which players.** `progress/_pending.json` lists the ids with work waiting. An id is written there before its record is saved with work in it, and leaves after a save with none, so a crash leaves at most one extra look. At start, the processor reads it; if it is missing or damaged it looks at every record in `progress/` once and rebuilds it (`offline_rescan` does that by hand). A record that can't be read is logged and tried again after a minute.
- **The bag.** A parcel in the bag of a player who is away is in `CrimeRecord.Items`; when its order is cancelled the processor removes it there, so `PlayerCrime.Restore` finds it gone.
- **Mailboxes.** A mailbox learns its owner from `mailboxes.json` when it starts, so a player who is away keeps their box after a restart. The processor keeps the flag of the owner's box right (up while a parcel waits for the mailman).
- **Time.** The game clock stops while the server is off (`world.json` holds the time of the last save), so downtime costs nobody a deadline. It is saved at least every 10 seconds and with every record save.
- **Saving.** A change to an attached record is saved 2 seconds after the last change, and at the latest 10 seconds after the first (`PlayerProgress`). The processor saves at once. A file is written as `.bak` first and the damaged file falls back to it, so a write cut short costs at most the last write. The `OfflineCrimeSystem` writes every record in memory when the scene goes (a clean stop, a scene change); a crash loses at most the changes of the last 2 seconds (10 under a constant stream of changes), and a record replays from the older state with the same dice. Stopping the editor was seen to write the record at once.
- **Testing without touching a real save.** `progress_test_data on` (before the player has spawned) keeps progress in `progress_test/`, the clock in `world_test.json` and the mailbox claims in `mailboxes_test.json`. `offline_away` takes your player object away without disconnecting, `offline_back` respawns you, `offline_status` shows what the processor looks after, and `world_time_skip_minutes` moves the clock without a player. See [console-commands.md](console-commands.md).
- **Not done.** No "while you were away" summary for the player (the reviews and the money are simply there when they return). The open orders on the board are not saved.

## Security

Same rules as the apps in AGENTS.md.

- **The host decides everything.** `PlayerCrime` methods run on the host only. The tray, bench and mailbox are `Interactable` options that run on the host (the host re-checks the player's reach). The packing table's option is local (it only opens the panel); packing itself is the `RequestPack` RPC. Hush is a shared app: its logic runs in the host's copy, and the seat comes from the computer (`GameAppSeat`), never from an action.
- **Actions carry ids and amounts only.** `accept <orderId>`, `sell <amount>`, `buy <amount>`, `tab <name>`, `category <name>`, `status <name>`, `quantity <n>` and `shop <itemId> <quantity>`. Hush has no pack action. The packing panel sends an order id and an inventory slot index (`RequestPack`); the host takes the player from `Rpc.Caller`, checks a packing table is near, that the player owns the order, that it is Accepted and in time, and that the slot holds a batch of the right kind and count, and never reads a quality, count or price from the client. The shop takes the price and delivery time from `CrimeCatalog` and refuses an unknown item, a quantity outside 1 to `ShopMaxQuantity`, too many deliveries in transit, or not enough crypto. Filter names are checked against the catalogue and the status list. The price, count, quality, times and fees come from the host's records. An exchange amount is the player's own to give and is checked against their balance and a maximum.
- **Buyers' secrets stay on the host.** The buyer personality is not in the UI state, the packing panel or any synced data. Nothing that sends a message as an NPC exists yet (buyer notes are plain text in the order).
- **Credits are never touched.** Crypto and dollars change only in `Exchange`.
- **Parcels cannot be dropped or faked.** `PlayerInventory.DoDrop` refuses a parcel. `crime_give` is editor only.
- **The offline processor has no input from a client.** It works on ids from the host's own index and store, one record at a time, never on a record a live player is attached to, and no RPC or app call reaches it. The editor-only `offline_*` commands run on the host and only in the editor.
- **Hush needs home and the network.** In the Wired it says "No signal"; offline it says so. The host checks the same again before acting.
- A client's mailbox claim is by Steam id on the host, and a box whose object is gone is let go.

## Placing things in a scene

`Assets/scenes/home.scene` has one of each placed through the editor (the scene change is not committed): **Grow Tray**, **Press Bench** and **Packing Table** at (-70, -50, 0) (from the prefabs in `Assets/crime/`), next to the existing **MailBox**. To place more by hand:

- **Grow tray / press bench / packing table:** drag the prefab in. Or make a GameObject with a model, a collider, an `Interactable` and a `GrowTray` (or `PressBench`, `PackingTable`) component; **Network Mode must be Object**, or the press never reaches the host. The options are added in code, so the `Interactable` can keep its default option.
- **Mailbox:** the existing `Assets/MailBox.prefab` works as is (it is a network object). The "Send all parcels" and "Collect parcels" options are added in code, and they are the only options: any other the object has is removed at run time, so the flag cannot be moved by hand.
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

`progress_give_money`, `crime_give static_tab 40 70`, `crime_tray_ready`, `crime_orders`, `crime_postal_now`, `crime_deliver_now`, `world_time_skip_minutes`. To test being away: `progress_test_data on`, play, post a parcel, `offline_away`, `world_time_skip_minutes 30`, `offline_status`, `offline_back`. See [console-commands.md](console-commands.md).
