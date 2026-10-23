# The dark-web market app, version 1

Part of [crime-rp.md](crime-rp.md). Pairs with [crime-rp-first-product.md](crime-rp-first-product.md). This is what the player sees and does the first time they sell something. Numbers are starting guesses.

**Revised 2026-10-23:** goods are **shipped by mail** (post office, mailbox or street box), not handed over in person. Buyers leave **reviews** based on quality, can **scam** (claim non-delivery), and **demand shifts** over time so one product is not enough. Ingredients come from the pharmacy, the dark web, or crafting.

**Currency (updated 2026-10-23):** Hush trades in **crypto**, not cash. Orders, escrow, the market fee, the Shop and the Wallet are all in crypto. To spend earnings on real-world things the player **cashes out through an exchange** (fees, a delay, and financial evidence), and to buy dark-web supplies they may convert cash into crypto. See "Three currencies" in [crime-rp.md](crime-rp.md). In this document, "$" amounts mean crypto unless stated; the exchange is an extra tab in the app.

## What version 1 is

**One market, run by the game** (placeholder name **Hush**), with NPC buyers and NPC sellers. It is the only market at first, so the economy works on day one. Player-run markets come later and plug into the same app (see "Later").

## Why it is an app

The player sits at a computer in their base, opens Navi, and uses **Hush**. That is deliberate:

- The computer is the crime tool. Everything in this app is a real action on a real machine.
- It leaves traces on that machine, which police can find.
- It uses the existing Navi pieces: the app pattern, Messages, Payments, the file system and the Intrusion puzzles.

## The player's first sale (the flow)

1. **Sit at your computer.** Open **Hush**. Turn the **VPN** on.
2. **Orders.** Read the board. Pick a buyer. *Dex_44 wants 10 Street tabs at $38 each, 12 minutes to deliver.*
3. **Read the buyer.** Account age, past reviews, tone. A good buyer, a scammer, or a sting?
4. **Accept.** The buyer's money goes into **escrow**. You have until the deadline to ship.
5. **Make or pull** the goods from your stock (see the product doc).
6. **Package.** Walk to the **packing table** in the house (not in the app). In the first build one press packs the next order you can fill; later: choose a box, add stealth, seal, take a **photo** of the sealed package (evidence), and print the **label** from the order.
7. **Ship.** Put it in your **mailbox**, take it to the **post office**, or drop it in a **street box**. Each has different risk and cost.
8. **Track.** The app shows the package moving: *Collected, In transit, Delivered.*
9. **Release or dispute.** The buyer confirms, and escrow pays you. Or they claim a problem, and you defend yourself.
10. **Review.** The buyer leaves a rating and a short note. It moves your reputation and your future prices.
11. **Clean up.** Wipe your history, or take the risk.

The fun lives in step 3 (reading a buyer), step 6 and 7 (packaging and picking a risk), step 9 (defending against scams), and step 5 (stock and demand).

## Screens

### Layout

```
+---------------------------------------------------------------+
| HUSH        [ VPN: ON ]   Trace 12   Rep: Trusted   $2,140     |
+----------+----------------------------------------------------+
| Orders   |                                                    |
| Shipments|    (the selected tab fills this area)              |
| Trends   |                                                    |
| Shop     |                                                    |
| Wallet   |                                                    |
| Reviews  |                                                    |
| Security |                                                    |
+----------+----------------------------------------------------+
```

### 1. Orders (the main screen)

A list of open buyer orders. Each row shows:

| Field | Meaning |
| --- | --- |
| **Buyer name** | A handle (for example `Dex_44`) |
| **Wants** | Product, amount and an asked quality (a guide: any quality may be sent, the rating reflects it) |
| **Pays** | Price each, and the total |
| **Deadline** | Time left to ship |
| **Buyer rating** | Stars and number of trades |
| **Account age** | New accounts are riskier |
| **Risk** | A hint: *Looks normal*, *New account*, *Wants signature*, *Odd wording* |
| **Hot** | A flame if the product is trending |

- **Open** a row to see the buyer's profile, past orders and a few messages.
- **Accept** reserves the order and moves the buyer's money to escrow. You cannot take it back for free (a cancel hurts reputation).
- **Decline** has no cost.
- **Orders refresh** every few minutes, based on demand for each product.
- **Higher reputation** unlocks bigger orders, better prices and rarer buyers.

### 2. Shipments

Every package you sent, with its stage and the clock.

| Stage | Meaning |
| --- | --- |
| **Made** | Packed, not sent |
| **Collected** | The mailman or clerk took it |
| **In transit** | On its way |
| **Delivered** | Reached the buyer |
| **Waiting for release** | Escrow is held until the buyer confirms or the timer ends |
| **Released** | You were paid |
| **Disputed** | The buyer claimed a problem |
| **Seized** | An inspector took it |

### 3. Trends

How demand and price move, per product.

- A **chart** of price and demand for the last hour and day.
- **Arrows**: rising, steady, falling.
- **Hot** and **Cold** tags.
- **Chatter**: short NPC lines hinting at what is about to change ("everyone's asking for Frost").
- **Forecast** is not shown. Players guess from chatter and trends.

See "Demand and trends" below.

### 4. Shop (supplies and ingredients)

Buy what you need. Delivery is by mail, with a delay, to your mailbox.

| Category | Examples |
| --- | --- |
| **Cultures and seeds** | Culture, trays |
| **Ingredients** | Reagents, precursors, nutrient mix, filler (more expensive and unrestricted compared with the pharmacy) |
| **Equipment** | Tent, Cabinet, Grow Controller, filters |
| **Press gear** | Stabiliser, filter |
| **Packaging** | Boxes, padding, vacuum bags, odour seals, decoy items, labels |
| **Security** | VPN, shredders |

- Orders from the Shop arrive in your mailbox. They can be stolen from it (see below), and can be inspected on the way in.
- Buying without a VPN adds trace.

### 5. Wallet

| Item | Meaning |
| --- | --- |
| **Balance** | Your dollars in the market's account |
| **In escrow** | Money held for orders in progress (yours to get) |
| **Fees paid** | Total, with the current rate |
| **History** | Every payment |

- **Deposit** moves dollars from your Payments account into the market account.
- **Withdraw** moves them back (with a small delay and a fee).

### 6. Reviews

What buyers wrote about you, and your numbers.

| Item | Meaning |
| --- | --- |
| **Rating** | Average stars, weighted to recent trades |
| **Reviews** | A short list of lines, such as "Pure, perfect." or "Slow, but good." |
| **Scam flags** | Disputes you lost |
| **Level** | Newcomer, Known, Trusted, Kingpin |

### 7. Security

| Item | Meaning |
| --- | --- |
| **Connection** | Direct or VPN. VPN costs money per hour. |
| **Trace** | 0 to 100. Rises with each action without a VPN, and a bit even with one. Falls slowly. |
| **Session history** | The log of what you did. Police can read it if they get your computer. |
| **Wipe history** | A short puzzle (the existing log-cleaning one). A bad wipe leaves marks. |

## Packaging

Every order gets a package. The player decides how well it is made.

| Choice | Effect |
| --- | --- |
| **Box** | Plain, padded, or a decoy type (looks like something else). Bigger boxes hold more. |
| **Padding** | Protects the product (no damage in transit). |
| **Seal** | Vacuum or odour seal cuts the **smell** tell. |
| **Decoy** | A harmless item on top lowers the inspector's suspicion. |
| **Label** | Printed from the order. A fake **return address** or none. |
| **Photo** | A picture of the sealed package, taken with the camera. It is evidence in a dispute. |

The package gets a **stealth rating** from these. A higher rating lowers inspection odds and raises the cost.

## Shipping

| Option | Where | Cost | Speed | Risk |
| --- | --- | --- | --- | --- |
| **Mailbox** | Your own box outside the base | Cheap | Next round of the mailman (a few minutes) | Links the package to your home. A rival can open or steal from your mailbox. |
| **Post office** | The NPC post office in town | Medium | Standard or express | Cameras and a clerk see you. Clerks may weigh or X-ray it. You can get a receipt. |
| **Street box** | A public box away from home | Cheap | Slower pick-up | Anonymous. A camera may cover it. Not all weights fit. Needs a trip. |

- **Tracking number:** every option except the street box gives one (a receipt is evidence).
- **Signature on delivery:** costs extra. Some buyers refuse it. Helps in disputes.
- **Weight and size limits.** Big batches must be split.
- **Pick-up times.** The mailman passes at set times. Miss one and the order is late.

### Inspection

Mail can be opened.

- A package has an **inspection chance** based on its stealth rating, weight, smell and how suspicious your account or address is.
- **Seized** package: you lose the goods and the order. A **case file entry** is added with your label, your fingerprints and your location.
- A package sent from your mailbox ties the case to your address. From a street box it ties to the area only. From the post office it ties to a camera.
- **Incoming** parcels from the Shop face the same risk.

### Mailbox theft

The mailbox is outside your home, so anyone can reach it.

- A rival can **open the mailbox** (a short lockpick or a hack), **steal** a package, or **read** the labels and see who you sell to.
- You can **lock** it, add a **camera**, or fit an **alarm**.
- This gives players a cheap way to hurt a rival and a reason to guard their street.

## Escrow and release

| State | Meaning |
| --- | --- |
| **Held** | The buyer's money is with the market. |
| **Released** | The buyer confirmed. You are paid (minus the fee). |
| **Auto-released** | The buyer did not respond within the timer (about 12 minutes after delivery). |
| **Refunded** | You lost a dispute, or the order failed. |

- Higher reputation can unlock **Finalize Early**: the buyer's money is released the moment the package is delivered.
- **Fee:** a percent of the trade, set by your reputation.

## Buyers (who you might meet)

| Type | Behaviour |
| --- | --- |
| **Layman** | Not an expert. Does not notice a small shortfall, and rates better-than-asked even higher. |
| **Connoisseur** | Knows what they want. No tolerance for a shortfall, little extra for more. |
| **Impatient** | Wants speed. Lateness costs double, a quick delivery earns a bonus. |
| **Easygoing** | Generous. Tolerates some shortfall and rarely rates low. |
| **Skeptic** | Stingy. Rates a star lower and hardly ever gives five. |
| **Scammer** | Claims "not received" or "not as described" to get a refund. More likely with new accounts or big orders. |
| **Sting** | An undercover officer. The package opens a case (see the sting section). |

Tells (visible in the app): account age, past reviews, how the messages read, whether they ask for odd details, whether they refuse tracking, whether the order is much larger than usual.

### Scams and disputes

The buyer can open a **dispute** after delivery:

- **Not received:** "I never got it."
- **Not as described:** "The quality is wrong."
- **Late:** "It missed the deadline."

You defend with **evidence**:

| Evidence | What it proves |
| --- | --- |
| **Tracking log** | It was collected and delivered |
| **Post office receipt** | It was sent, from where, when |
| **Pre-seal photo** | What was inside |
| **Signature** | The buyer received it |
| **Quality proof** | Your batch's purity log |
| **Past chats** | The buyer's behaviour |

A small dispute screen shows what each side claims. You pick evidence and submit. An NPC arbiter weighs it:

- **Strong evidence** (tracking plus signature) wins.
- **Weak evidence** (a mailbox drop with no receipt) can lose.
- **A known scammer** loses on repeat, and gets flagged (a reward for the player).
- **Losing** means a refund, a scam mark, and a rating hit.

This makes **packaging discipline** a real tradeoff: more evidence costs time and risk (a post office visit leaves a camera record).

### Stings

A sting buyer is a postal inspector or an undercover cop. How evidence on the package is read (fingerprints, DNA, handwriting, printer marks) and how the case file grows is in [crime-rp-police-forensics.md](crime-rp-police-forensics.md). Wearing gloves, printing the label and using other precautions are **optional**: they cost time and money and cut the risk. Without a meeting, the sting works through the mail:

- The package arrives and the **case file** gains your label, handwriting, mailing location and the order.
- You are not arrested on the spot. A **controlled-delivery** raid is scheduled based on the evidence.
- Careful shipping (a street box, no return address, a decoy, a fresh VPN) leaves weaker evidence.
- About 1 in 10 early orders is a sting. A player learns to read them.

## Reviews and prices

Buyers leave a **rating (1 to 5)** and a short line chosen from templates. The personality is hidden; it shows only in how the buyer writes. The first build (all but Scammer and Sting) is in [../crime.md](../crime.md), "The rating rule".

| Factor | Weight |
| --- | --- |
| **Quality vs what was ordered** | Highest. Under the ask is a lower rating (some buyers do not notice). Above it is a higher one. |
| **Amount accuracy** | A short package is punished. |
| **Speed** | Late means lower, and the review says it took too long. The deadline only governs sending: a late parcel is still paid. |
| **Packaging and stealth** | A sloppy package gets a note. |
| **Buyer personality** | Changes how each factor counts. |

- **Reviews build reputation** and also a **price premium**: a highly rated vendor can charge more.
- **Price per unit** = base price x quality multiplier x reputation premium x demand multiplier.
- **Quality multipliers** (from the product doc): Rough 0.4, Street 1.0, Clean 1.8, Pure 3.2.
- **Reputation premium:** from about 0.9 (new) to 1.3 (Kingpin).
- **Demand multiplier:** from about 0.5 (glut) to 2.0 (shortage).

## Demand and trends

**Making the same thing forever should not pay.** Demand per product changes on its own, so players have to learn more recipes and read the market.

Demand is built from layers:

| Layer | What it does |
| --- | --- |
| **Slow cycle** | Each product has its own wave over hours: it rises, peaks and falls. |
| **Fashion** | A product becomes "hot" for a while, then cools. |
| **Events** | A festival, a crackdown, a holiday, a news story shifts demand for some products. |
| **Supply shocks** | A police sweep or a rival flooding the market raises or lowers prices. |
| **Saturation** | Selling a lot of one product lowers its price in that district. |
| **Player pressure** | If many players make the same thing, the pool dries up. |

- Hot products pay up to 2x. Cold ones pay half.
- **There is always something rewarding to make,** but not the same thing every time.
- **Chatter** (NPC messages in Trends) hints at changes a few minutes ahead.
- This pushes players toward **several products**. The first product (Static) is one of many. Each new product needs different ingredients, equipment and minigames. See the product doc.

## Ingredients (summary)

Players need materials to make a product. They come from three places, each with a cost.

| Source | How it works | Risk | Cost |
| --- | --- | --- | --- |
| **Pharmacy** | An NPC shop in town. Legal items, a daily limit, ID required. Controlled items are logged. | Big or odd purchases are reported to police. Records can be read. | Cheap |
| **Dark web** | Order any amount through Hush. Arrives by mail. | Mail inspection. A parcel from a strange sender. Mailbox theft. | Expensive |
| **Craft** | Make precursors at a bench from basic materials. | Time, a skill, and the equipment's tells. No record. | Cheapest at scale |

- **Ingredient quality** (Technical, Lab, Reagent) caps the quality of the output.
- **Controlled items** are gated: a pharmacy limit, a record in the system, and a case-file hit if bought in bulk.
- **The pharmacy is a target.** A player can hack its records to remove purchases, or rob its stock.
- Ingredients and crafting are designed in [crime-rp-first-product.md](crime-rp-first-product.md).

## Reputation levels

| Level | Needs | Unlocks |
| --- | --- | --- |
| **Newcomer** | Start | Small orders, 10 percent fee |
| **Known** | 5 good trades | Medium orders, 8 percent fee |
| **Trusted** | 20 good trades, no lost disputes | Large orders, 6 percent fee, Finalize Early, private buyers |
| **Kingpin** | 60 good trades | The best orders, 4 percent fee, a price premium, bragging rights |

- **Good trades** (4 and 5 stars) raise it. A **scam**, a **no-show**, a **seizure** or a **lost dispute** lowers it.
- Rank shows next to your handle.

## Messages

Buyers write to you through **Messages** (the existing app) as NPCs: confirmation, a note, a thank-you, a complaint, a threat. The text carries tells. Game code writes these, never a player.

## State and saves

(Host-authoritative, as for the Hardware Store.)

| What | Where |
| --- | --- |
| **Open orders, buyer profiles, demand and trends** | Host memory, saved with the world |
| **A player's rep, reviews, scam count, fee tier** | The player's progress record |
| **Escrow and wallet** | The player's progress record (dollars) |
| **Shipments in transit and their evidence** | The player's progress record |
| **Session history and trace** | The player's progress record and the computer's file system |
| **Mailbox contents** | World objects, saved |

All changes go through the host. Actions carry ids, not prices. The host checks the price, the stock, the rep, the deadline, the VPN state and the package.

## Security notes

- The seat decides the player (`GameAppSeat`), never what an action says.
- Orders, buyer profiles, the **sting flag** and the **scammer flag** live only on the host. They never reach the UI state, only the tells do.
- The market is **game code**, not a Store app, so money and listings cannot be forged. See the Security section in AGENTS.md.
- The app must not take a buyer, price, amount or package contents from the client beyond an id the host knows.
- Messages from NPC buyers are sent by game code only (`NaviNpc.Send`). No `messages.*` call may send as a buyer.
- Mailbox access is checked on the host. A client cannot open a box it is not near.

## How it fits the code

An ordinary Navi app, three files, like the Hardware Store (see `Libraries/navi/docs/app-architecture.md`):

- `HushUiState.cs`: records for rows (orders, shipments, trends, reviews), enums for the tab, and a `Revision`.
- `HushViewModel.cs`: all the calls, filters and wording.
- `Hush.razor`: stateless markup with `BuildHash()`.

Host systems it needs: a `MarketSystem` (orders, buyers, demand, trends), a `PostalSystem` (mailboxes, post office, street boxes, transit, inspection), `PlayerProgress` additions for rep, reviews and wallet, new inventory items (products with a quality attribute, packages), and the world objects (mailbox, post office counter, street box, pharmacy).

## Version 1 scope

**In**

1. The Hush app with Orders, Shipments, Trends, Shop, Wallet, Reviews, Security.
2. A VPN switch and a trace number.
3. 5 to 10 NPC buyers with profiles and tells (honest, picky, scammer, sting).
4. Accept, then package and **ship by mailbox or post office** (the street box can wait).
5. Escrow, a fee, three reputation levels, and **reviews** driven by quality, speed and amount.
6. A small **dispute** screen for "not received" with a few kinds of evidence.
7. **Demand cycles** for at least three products (so trends matter).
8. A shop with supplies and ingredients, and a **pharmacy** with a daily limit and a purchase record.
9. A session history and a wipe puzzle (reuse the Cover one).
10. Mailbox in the world (lockable, can be raided).

**Out (later)**

- The street box, signature options beyond a basic one, insured shipping.
- Listings (sell without an order).
- Player-run markets, directories, ads.
- Player-to-player trade and reviews between players.
- Market hacking and escrow theft.
- Reputation decay, blacklists.
- The "controlled-delivery" raid in full (a simple case-file entry first).
- Crafting precursors in depth (one example first).

## Fun tests

| Test | Pass if |
| --- | --- |
| First sale, no instructions | The player finds Orders, accepts, packages and ships in under 10 minutes |
| Reading buyers | Players notice the tells and say "that looks like a scammer" |
| Packaging | Players take the photo and keep the receipt after losing one dispute |
| Mailbox vs post office | Players choose by risk and cost, not by default |
| Trends | Players switch product after a price drops, and talk about what is hot |
| Pharmacy | Players try to split purchases or look for another source |
| Repeat | They come back for the next order |

## Later: player-run markets

The same app, with a **market picker** at the top. The player can browse several markets, join one, or **open their own** (set the fee, rules, escrow and skin). Everything else in the app stays. Design is in [crime-rp.md](crime-rp.md) under "Player-run markets".

## Open questions

1. How long should shipping take (a few minutes real time)? Too slow is a chore, too fast has no tension.
2. Should a package be physically carried to the post office or mailbox, or is it a menu action at home? (Recommendation: carried, at least to the mailbox.)
3. How are stings drawn: a fixed share, or tied to the player's heat and rep?
4. Is an inspection a loss of goods only, or does it always open a case?
5. How many products are needed at launch for demand shifts to feel real (three, five)?
6. Does the arbiter decide by fixed rules, or with a random element?
7. Can a player scam a buyer by shipping nothing or the wrong thing, and what happens?
8. Should players be able to sell to each other directly by mail without the market?
9. How do NPC buyers get names, voices and message variety without generated text? (A written pool of handles and message templates.)
10. Where is the post office, and how many mailboxes and street boxes does the district have?
