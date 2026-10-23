# Second product: Haze (a test of the multi-product systems)

Part of [crime-rp.md](crime-rp.md), after [crime-rp-first-product.md](crime-rp-first-product.md). Haze exists mainly to **test how every system copes with more than one product**: the tray, the press, the shop, the market, the packing panel, Hush and the inventory. Numbers are starting guesses.

## The drug

**Haze** (fictional): a pale, slow-growing **bloom** that is pressed into small **vials** of a clear syrup. It is quieter and slower than Static, and pays more per unit. Users say it "numbs the edges". (Its use effect, Numb, is in the consumables design; no consumables exist yet.)

| | Static (existing) | Haze (new) |
| --- | --- | --- |
| Seed item | Static culture | **Haze seedling** |
| Grow time | 10 s (test value) | **25 s** (test value) |
| Harvest gives | 12 raw Static | **8 Haze (raw)** (bloom) |
| Raw per product | 1 raw: 1 tab | **2 raw: 1 vial** |
| Product | Static tabs | **Haze vials** |
| One harvest makes | 12 tabs | **4 vials** |
| Base price (crypto per unit) | 10 | **30** |
| Seed price in the shop | 40 | **90** |
| Delivery of seeds | 120 s | **180 s** |
| Order size on the board | 10 to 30 tabs (steps of 5) | **3 to 10 vials** (steps of 1) |
| Category (Hush filter) | Static | **Haze** |
| Icons (Material) | biotech, grain, medication | **spa**, **eco**, **opacity** (a drop; `water_drop` is not in the game's icon font, so it was swapped) |
| Colours | green, teal, cyan | **lavender, pale violet, lilac** |

Different numbers on every axis is on purpose: it shows whether any rule still hides an assumption about Static (for example "one raw makes four tabs" or "orders are for tabs").

## What has to change (everything that assumed one product)

The right fix is **data-driven**: one `ProductCatalog` entry per drug, so a third drug is only data.

A product definition holds: id and name, the seed item, the raw item, the product item, grow seconds, raw yielded per harvest, raw needed per product, crop quality range, press quality spread, base price, order count range and step, ask range, shop seed price and delivery time, category, and icon and colour for its three items.

### 1. Items
- New items in `ItemCatalog`: `haze_seed`, `haze_raw`, `haze_vial` (name, description, icon, colour, category "Haze", base price for the vial).
- A new player does not start with Haze seedlings. They are bought in the Hush Shop.

### 2. Grow Tray: choose what to plant
- A tray holds **one product at a time**. Its state records which product is planted (`TrayRecord.Kind`), the ready time, and the quality.
- Using an empty tray opens a **small planting panel** (like the packing panel, registered with `GameMenus`): one row per product the player can grow, showing its name, the seeds in the bag, the grow time and what a harvest yields, with a Plant button (disabled with a reason when there are no seeds).
- A planted tray's prompt names the product ("Growing Haze, 0:25 left" / "Harvest Haze"). The world text above the tray names it too.
- Harvest yields the planted product's raw, plus its seed back.
- The panel sends the tray id and a product id (the host cannot guess which tray the panel was opened on when two stand side by side); the host checks the player is within reach of that tray, the product exists, the tray is empty and the seed is in the bag. An empty tray shows one row, Plant (local, opens the panel); a planted tray shows the growing/harvest row (host). A tray with an unknown or missing product loads as Static.

### 3. Press Bench: choose what to press
- The bench no longer presses "all raw Static". Using it opens a **small pressing panel** listing each raw item the player carries (name, count, quality, how many products it makes, and its ratio), with a Press button per row.
- Pressing a row takes all of that raw batch (or only a multiple of the ratio: 8 raw for Haze makes 4 vials; an odd leftover raw stays in the bag) and gives one batch of the product with the placeholder quality rule (the batch's quality nudged by up to 10). A row is one inventory slot, so raw of different quality is pressed batch by batch (before, all raw Static was pressed into one batch at the average quality).
- The panel sends the inventory slot only; the host checks it holds a raw item of a known product.
- Later, the Tuning minigame goes here; the panel is the natural place for it.

### 4. Market and orders (Hush)
- The board generates orders for **any product**, by weights in the catalogue (start with about 60 percent Static and 40 percent Haze), using that product's count range, ask range and base price. **Haze orders therefore appear in the Hush Orders tab**, next to Static orders.
- Order cards show the product name and unit ("5 x Haze vials").
- The category chips on Orders show All, Haze and Static (alphabetical, one per drug in the catalogue, even with no order on the board) with counts; the filter by category works for both.
- Buyer notes are generic; a product can add its own flavour lines later.
- Accept, Pack, Send, review, escrow and fees are unchanged and work from the order's kind.

### 5. Packing panel, parcels, mailbox
- The packing panel already works from the order's kind; check eligible batches are of that kind and that parcel contents show "Haze vials x5, quality 55".
- Parcels, the post and delivery work from the order record, not from a hard-coded product.

### 6. Shop
- The Shop tab lists **both** seeds (Static culture and Haze seedling), each with its own price, delivery time and quantity chips.
- Deliveries and collecting work from the item kind.

### 7. Inventory and details
- New icons and colours; a batch's detail line reads right ("x8, quality 60").
- The inventory descriptions name the product.

### 8. Hard-coded "Static" and "tabs" to remove
An audit of the crime code and UI for any text or logic that names Static or tabs directly. Known places: the packing table prompt, result messages ("raw Static", "tabs"), `PlayerCrime` defaults (`order?.Kind ?? StaticTab` for a parcel with no order, the plant and press logic, the harvest message and the starter culture), `MarketSystem.Generate` (fixed kind), Hush wording, the dev commands, and the docs. Each becomes product-driven.

### 9. Offline processing and saves
- The offline processor and the saved record work from kinds. Check that a Haze parcel, order and delivery are processed for an absent player exactly like Static, and that old saves (a tray with no `Kind`) load as Static.

## How to test the systems

| Test | Pass if |
| --- | --- |
| Buy a Haze seedling in the Shop, collect it at the mailbox | It appears in the bag with its own icon |
| Use two trays: plant Static in one, Haze in the other | The panel offers both, each tray shows its own text, harvests give the right raw |
| Use the press with both raws in the bag | The panel lists both, 8 Haze raw makes 4 vials, 12 Static raw makes 12 tabs |
| Hush Orders | Both products appear, the category chips filter, accepting a Haze order works |
| Pack a Haze order, send, deliver | Review, escrow and crypto work as for Static |
| Mixed bag, several orders | Packing lists only eligible batches per order |
| Away player | A Haze parcel is delivered and paid while the player is away |
| Old save | A save from before Haze loads, trays and parcels intact |

## Open questions

1. Should a tray plant be allowed while the player has seeds of both products (yes: the panel chooses), and can one tray ever hold two products (no)?
2. Should Haze need a different press bench later (a still), or share the bench for now (share)?
3. Should order mix weights follow a trend (demand shifts) later? Not now; the weights are fixed.
4. Names and numbers are placeholders; Haze may be renamed.
