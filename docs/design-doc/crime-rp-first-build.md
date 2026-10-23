# First build: grow, ship, get paid

The smallest thing worth building in code. It proves the crime loop end to end on one machine, for one player, with placeholder minigames. Part of [crime-rp.md](crime-rp.md). Written 2026-10-23 after reading the code as it stands.

## Goal

A player grows Static, presses it into tabs, accepts an order in the **Hush** app, packs a parcel, **walks it to their mailbox**, waits, and **gets paid from escrow** with a review. No police, no stings, no disputes, no real minigames yet.

**Done when this 10-minute script works with no console commands:**

1. Sit at the home computer, open **Hush**, see 3 orders.
2. Walk to the **grow tray**, plant a culture, wait (the game clock can be skipped in testing).
3. Harvest. Walk to the **press bench**, press into tabs.
4. Back at Hush, **accept** an order, then **pack** it (the parcel appears in the inventory).
5. Walk to the **mailbox** and send the parcel.
6. Watch it move in the **Shipments** tab: Collected, In transit, Delivered.
7. Escrow releases. **Crypto** goes up. A review line appears. Reputation moves. Open the **Exchange** tab and cash out some crypto into dollars (with the fee).
8. Restart the game. The state is still there.

## What already exists (verified in the code)

| Piece | Where | Use in this build |
| --- | --- | --- |
| **Game clock** | `WorldClock` (seconds, saved, `TimeScale` 1) | All timers (growth, transit, escrow). `world_time_skip_*` commands already exist for testing. |
| **Per-player save** | `ProgressRecord` through `PlayerProgress` (host only, saved one file per Steam id, `Sanitize`) | Rep, parcels, escrow, trays. Money through `AddMoney` and `TrySpend`. |
| **Deliveries to home** | `ShipmentRecord` (`ArriveAt`), checked once a second in `PlayerProgress.Deliver()` | The model for parcels in transit. |
| **Inventory** | `PlayerInventory`, grid of `ItemData` (name, description, icon), host-owned, synced as JSON | Holds products and parcels. No quantity or quality yet. |
| **Interaction** | `Interactable` + `IInteractionHandler`, options run on the host; `PickupItem`, `MailBox` | Tray, bench and mailbox. |
| **Mailbox** | `Code/Objects/MailBox.cs` | Only a flag today. No contents. |
| **App pattern** | `ViewModel` + `UiState` + stateless `.razor`, `GameAppSeat` for the seat; `.napp` manifests in `Assets/navi/` | The Hush app, copied from the Hardware Store and Payments. |
| **NPC messages** | `NaviNpcs` / `NaviNpc` (`WiredStranger` is the sample) | Buyer texts (optional, last). |
| **Where the player is** | `WiredTravel.PlaceOf( seat )` | Hush refuses to work in the Wired. |
| **Events** | `Global.IProgressEvents`, `Global.IInventoryEvents` | Redraw without polling. |
| **Console commands** | `ProgressCommands.cs`, listed in `docs/console-commands.md` | Dev commands, and the doc must be updated for each. |

### Gaps this build has to fill

- Items have no **count**, **quality** or **kind**.
- The mailbox cannot hold or send anything.
- There is no market, order, escrow, review or reputation.
- There are no grow or press objects.
- The save has no crime state, and it must be **wipeable on its own** later (seasons).

## Decisions made for the build

| Decision | Why |
| --- | --- |
| **Crime state goes in its own `CrimeRecord`** inside `ProgressRecord` (bump `CurrentVersion` to 3). | Seasons will wipe it without touching XP, hack level, credits or cosmetics. |
| **Items become batches.** One slot holds "40 Static tabs, purity 72". `ItemData` gets `Kind`, `Count`, `Quality`. | No stacking system needed. A batch is the unit the design already uses. |
| **Trays and the press are saved per player** (a list in `CrimeRecord`) for now. | A solo test needs no world ownership. Move them to bases later. |
| **Parcels are items.** Packing in Hush creates a `parcel` item in the inventory. The mailbox sends it. | Keeps the walk, needs no new UI for choosing items, and the parcel can be stolen or lost later. |
| **Orders are global on the host**, not saved. | They regenerate at restart. Accepted orders and escrow are saved with the player. |
| **Hush is a system app** like Payments, and checks `WiredTravel.PlaceOf` itself. | It pays crypto, earned in the real world's dark web (see `currency.md` and "Three currencies" in `crime-rp.md`). |
| **No hand edits of scenes while the editor is open.** Place the tray, bench and mailbox with the editor tools. | AGENTS.md. |
| **Stage only the files each step touches.** `home.scene`, `wired.scene` and `Assets/House.prefab` have uncommitted changes of yours that must not be swept in. | AGENTS.md. |

## Steps

Sizes are rough: **S** about 30 minutes, **M** about an hour or two, **L** a few hours. Each step ends in something testable, and gets its own commit.

### Step 1: Items with count, quality and kind (S)

- `ItemData` gets `Kind` (string id), `Count`, `Quality` (0 to 100). Old saves load with defaults.
- New `ItemCatalog`: id, name, description, base price, for the first items: `static_culture`, `static_raw`, `static_tab`, `parcel`.
- `PlayerInventory` gets helpers on the host: `AddItem( kind, count, quality )`, `CountOf( kind )`, `TryTake( kind, count )` (takes from the first matching batches), `TakeBatch( slot )`.
- The inventory grid shows `x40` and the quality on hover.
- Dev command `crime_give <kind> <count> <quality>` and an entry in `docs/console-commands.md`.
- **Test:** give yourself items, see them in the grid, restart, still there.

### Step 2: Grow tray and press bench, with placeholder production (M)

- `GrowTray` (an `Interactable` handler, options run on the host): **Plant** takes one `static_culture`; it is **ready** after N game seconds (a time on `WorldClock`, so time offline counts); **Harvest** gives `static_raw`. State is saved in `CrimeRecord.Trays` by tray id.
- `PressBench`: **Press** takes `static_raw` and gives `static_tab` batches with a **placeholder quality** (a random range for now). Later this is replaced by the Tuning minigame.
- The option label shows state ("Plant", "Growing, 4:10 left", "Harvest").
- Dev command `crime_tray_ready` to skip waiting (or use `world_time_skip_minutes`).
- **Test:** plant, skip time, harvest, press, see tabs in the inventory.

### Step 3: Mailbox that sends parcels, and the postal clock (M)

- `MailBox` gets an owner (the player whose home it is, by their Steam id) and a **Send parcel** option. The existing flag stays.
- `CrimeRecord.Parcels`: id, order id, contents, `SentAt`, `CollectAt`, `DeliverAt`, state.
- The **mailman** collects at fixed times (say every 10 game minutes); sending just before is faster. `PostalSystem` is a host-only piece that `PlayerProgress` ticks once a second, like `Deliver()`.
- **Send** takes the `parcel` item out of the inventory and starts the clock.
- The flag goes up when a parcel is waiting and down when collected.
- **Test:** with a given parcel item, send it, and watch the state change over time with the clock skipped.

### Step 4: Market, escrow and the Hush app (L, the big one)

**Host side**

- `MarketSystem` (a host `GameObjectSystem`): keeps a list of open **orders** from NPC buyers: handle, product, count, minimum quality, price each, deadline, buyer type (honest, picky). It refills a few at a time. It is not saved.
- `CrimeRecord`: `Reputation` (good trades, rating sum), `Orders` accepted (id, buyer, terms, escrow state), `Reviews` (last few).
- Host rules, all inside `PlayerProgress`-style calls (`TryAcceptOrder`, `TryPackOrder`, release on delivery):
  - **Accept:** the order is reserved for this player, escrow marked held.
  - **Pack:** the host checks the inventory has enough of the right kind and quality, takes them, and gives a `parcel` item that remembers the order.
  - **Delivery:** when a parcel reaches `DeliverAt`, the order is marked delivered; the **buyer reviews** (rating from quality against what was asked, amount, speed) and escrow **releases** after a short timer. The player gets **crypto** (`AddCrypto`, new, host only) minus the fee. Cashing out to dollars is the Exchange tab.
  - **Deadline:** an order not delivered in time is cancelled and costs reputation.
- Reputation levels (Newcomer, Known, Trusted) change the fee. Fees and prices come from one catalogue.

**App side** (three files, copied from the Hardware Store pattern)

- `Code/Apps/Hush/`: `HushUiState.cs`, `HushViewModel.cs`, `Hush.razor` (+ `.scss`). Registered in `Assets/navi/system/hush.napp`.
- Tabs for v0: **Orders**, **Shipments**, **Wallet**, **Reviews**, **Exchange** (crypto to dollars at a fixed rate with an 8 percent fee and a short delay, and dollars to crypto at 4 percent). Trends, Shop and Security come later.
- **Crypto is the third currency.** Add `Crypto` to `ProgressRecord` and `PlayerProgress` (synced, host-only changes, own cap, `AddCrypto`/`TrySpendCrypto`), and update `docs/currency.md` from "planned" to as-built. No path may turn credits into crypto.
- Actions carry only ids (`accept <orderId>`, `pack <orderId>`). The seat comes from `GameAppSeat`. The host re-checks everything.
- The app says "No signal" or "Home only" in the Wired.
- **Test:** the 10-minute script above.

### Step 5: Make it feel like a game (M)

- Order flow polish: expiry timers, the "Hot" flag, and buyer notes via Messages (`NaviNpc` with templates).
- The review text lines from a small template pool.
- A short **tutorial order** with a gentle buyer.
- Sounds and a small HUD hint for the parcel.

### Step 6: Replace the placeholder with Tuning (L)

- A client UI panel for the radio-tuning minigame, opened from the press bench. The host runs the drifting peak and scores the lock from the dial inputs, so the score cannot be faked.
- Quality becomes the purity from the score.
- **Test:** five runs. Do scores improve and do players ask for another?

### Step 7: Climate (L) and sabotage hooks (later)

- The Climate minigame for the tray with the three gauges, checks and contamination.
- The Grow Controller as a Navi app.

## Not in this build

Street sales (cash, riskier), consumable drugs and the status-effects system, a sleep need, volatile crypto prices, mixers and crypto ATMs, police, case file, forensics, stings, disputes and scams, trends, the pharmacy, crafting ingredients, player-run markets, seasons, the street box and post office, VPN and trace, packaging stealth, bases and raiding, cars, the Wired leaks. Each has its own design doc.

## Risks and checks

| Risk | What to check or do |
| --- | --- |
| **`ItemData` change breaks old saves or the grid** | Defaults for new fields, and run the existing inventory flow before and after. |
| **Dynamic interaction labels** | `Interactable.Options` are fixed per object and read by index on the host. The tray and bench use a few fixed options with a state-driven label from `Describe`. |
| **A parcel item can be dropped or lost** | `PlayerInventory.DoDrop` only logs today. Block dropping parcels, or have `Drop` keep them, until a world drop exists. |
| **Hot reload resets statics** | Keep `MarketSystem` state in instance fields or non-readonly statics (AGENTS.md). |
| **Editor freezes when poking private methods** | Test by pressing the real keys and using real interactions. |
| **Time scale 1** | Real minutes pass in real minutes, so the testing commands to skip time matter. Tune the numbers for play afterwards. |
| **Security** | Every state change on the host. Actions carry ids only. No client sends a price, a count, a quality or a recipient. The sting and scammer flags (later) never reach the UI state. |
| **Currency** | Hush pays **crypto** (new third currency), and only the Exchange tab moves crypto to dollars (`AddMoney`). Credits are never touched. `currency.md` must be updated with the new field. |

## Open questions for this build

1. **Where do the tray, bench and mailbox go** in the home scene? The editor placement needs a spot and a prop for each (a shelf, a table, the existing mailbox).
2. **Mailbox ownership.** Which mailbox is yours, and what if two players share a home? (For now: one home, one mailbox, the first claimant.)
3. **Starting stock.** How does a new player get their first culture and first money? (Suggest: one free culture and $300 at the start, or a tutorial order that pays up front.)
4. **Order pacing.** How many orders at once, how often they refill, and how long they last.
5. **Do you want `docs/crime.md`** (a short as-built doc like `hardware.md`) written alongside Step 4, so the code and the doc stay together?
