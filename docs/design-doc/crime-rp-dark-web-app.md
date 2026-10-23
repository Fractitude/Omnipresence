# The dark-web market app, version 1

Part of [crime-rp.md](crime-rp.md). Pairs with [crime-rp-first-product.md](crime-rp-first-product.md). This is what the player sees and does the first time they sell something. Numbers are starting guesses.

## What version 1 is

**One market, run by the game** (placeholder name **Hush**), with NPC buyers and NPC sellers. It is the only market at first, so the economy works on day one. Player-run markets come later and plug into the same app (see "Later").

## Why it is an app

The player sits at a computer in their base, opens Navi, and uses **Hush**. That is deliberate:

- The computer is the crime tool. Everything in this app is a real action on a real machine.
- It leaves traces on that machine, which police can find.
- It uses the existing Navi pieces: the app pattern, Messages, Payments, the file system and the Intrusion puzzles.

## The player's first sale (the flow)

1. **Sit at your computer** in the base. Open **Hush**.
2. **Connect through a VPN** (a switch at the top). Without it, your trace rises.
3. Open **Orders**. See a buyer: *Dex_44 wants 10 Street tabs, $38 each, 30 minutes.*
4. **Read the buyer.** Account age, past trades, message tone. Is it a real buyer or a sting?
5. **Accept.** Choose how to hand over the goods: **Meet** or **Dead drop**.
6. **Go there** with the goods. Hand over or leave them.
7. **Payment** is released from escrow. You pay the market's fee. Your reputation goes up.
8. Back home, **clean your history** (optional), or leave it and take the risk.

Fun lives in steps 4, 5 and 6: reading a buyer, picking the risk, and the walk.

## Screens

### Layout

```
+----------------------------------------------------------+
| HUSH            [ VPN: ON ]  Trace 12  Rep: Trusted  $2,140 |
+---------+------------------------------------------------+
| Orders  |                                                |
| Shop    |   (the selected tab fills this area)           |
| Listings|                                                |
| Wallet  |                                                |
| Rep     |                                                |
| Security|                                                |
+---------+------------------------------------------------+
```

### 1. Orders (the main screen)

A list of open buyer orders. Each row shows:

| Field | Meaning |
| --- | --- |
| **Buyer name** | A handle (for example `Dex_44`) |
| **Wants** | Item, amount and minimum purity tier |
| **Pays** | Price each, and the total |
| **Deadline** | Time left |
| **Buyer rating** | Stars and number of trades |
| **Account age** | New accounts are riskier |
| **Risk** | A small hint: *Looks normal*, *New account*, *Asks to meet*, *Odd wording* |

- **Open** a row to see the buyer's profile, past orders and a few messages.
- **Accept** reserves the order. You cannot take it back for free.
- **Decline** has no cost.
- **Orders refresh** every few minutes, based on demand.
- **Higher reputation** unlocks bigger orders, better prices and rarer buyers.

#### The buyer page

A short profile in plain lo-fi UI: handle, account age, number of trades, a few ratings, the last messages. A buyer may be a **sting** (an undercover officer). Tells are subtle and consistent:

- A very new account that wants a big order.
- An unusual hurry, or a polished tone.
- Asks for a specific meeting place or time, and does not accept dead drops.
- Few or no reviews, or reviews from accounts that look new.
- Wants a quantity much bigger than the market usually asks for.

About **1 in 10** orders is a sting at the start. A player learns to read them. A **sting meeting** gets you arrested and your goods seized. A **sting dead drop** gets the drop watched, and the case file grows.

### 2. Shop (supplies)

Buy what you need to make Static, delivered by mail with a delay (like the Hardware Store).

| Category | Examples |
| --- | --- |
| **Cultures** | Culture, trays |
| **Equipment** | Tent, Cabinet, Grow Controller, filters, blackout kits |
| **Press gear** | Stabiliser, filter |
| **Tools** | Tweezers, test bench |

- **Prices** come from the dark web's own catalogue, a bit above the Hardware Store for common things, and with items you can't buy there.
- **Delivery** takes a few minutes to start. A package arrives at your base, and a parcel with a strange sender is a small tell.
- **Nothing here needs a VPN to browse.** Buying without a VPN adds trace.

### 3. Listings (sell without an order)

Put your own stock up for sale at a price you choose.

| Field | Meaning |
| --- | --- |
| **Item and amount** | From your inventory |
| **Price** | Your ask |
| **Purity shown** | Your claim (can be honest or a lie) |
| **Market average** | For this tier, today |
| **Demand** | High, Medium or Low |
| **Views and sales** | How many NPCs looked and bought |

- NPC buyers browse listings and buy the best deal for them: price, purity and your reputation.
- **Undercutting** works: a lower price sells faster, but the demand pool is shared and falls with every sale.
- **Overclaiming purity** can sell more, but if the buyer tests and finds a lie, you take a **scam** mark.
- Listings are slower than orders but need no meeting. Items are picked up by a courier fee or a dead drop.

### 4. Wallet

| Item | Meaning |
| --- | --- |
| **Balance** | Your dollars in the market's account |
| **In escrow** | Money held for trades in progress |
| **Fees paid** | Your total, with the current rate |
| **History** | Every payment |

- **Deposit** moves dollars from your Payments account into the market account.
- **Withdraw** moves them back (with a small delay and a fee).
- **The market account can be hacked** in later versions. For now it is safe.

### 5. Reputation

| Level | Needs | Unlocks |
| --- | --- | --- |
| **Newcomer** | Start | Small orders, higher fee (10 percent) |
| **Known** | 5 good trades | Medium orders, 8 percent fee |
| **Trusted** | 20 good trades, no scams | Large orders, 6 percent fee, private buyers |
| **Kingpin** | 60 good trades | The best orders, 4 percent fee, bragging rights |

- **Good trades** raise it. A **scam**, a **no-show** or a **bust** lowers it.
- Rank shows next to your handle on everything you do.

### 6. Security

| Item | Meaning |
| --- | --- |
| **Connection** | Direct or VPN. VPN costs money per hour and slows nothing. |
| **Trace** | A number from 0 to 100. Rises with each action without a VPN, and a bit even with one. Falls slowly if you stay away. |
| **Session history** | The log of what you did. Police can read it if they get your computer. |
| **Wipe history** | A short puzzle (the existing log-cleaning one). Cleans lines. A bad wipe leaves marks. |

- **High trace** adds to your case file (see [crime-rp.md](crime-rp.md)).
- **A VPN** is a purchasable item with a quality: cheap ones leak sometimes.

## Handing over the goods

| Method | How | Risk | Reward |
| --- | --- | --- | --- |
| **Meet** | The buyer shows up at a place and time. You hand over in person. | A sting arrests you. Bystanders see. | Highest price, instant. |
| **Dead drop** | You leave the goods in a locker or a hollow at a place. The buyer collects later. | Others (players or police) can find it before the buyer. | Safer for you, slower, slightly lower price. |

- **Both need you to go there** with the goods in your inventory.
- Drop points have names and are seen in the world. Players can learn the usual spots and watch them.
- A drop that is raided means a lost order and a case-file entry.
- **Later:** courier, mail, locker rental, vehicle swaps.

## Payments and fees

- **Escrow** holds the buyer's money. It releases when the handover is confirmed (the buyer collected, or the meeting ended well).
- **Fee** is a percent of each trade, set by your reputation level.
- **No-shows:** if you fail to deliver before the deadline, the order is cancelled, you lose standing, and the buyer is refunded.
- **Disputes** are not in version 1.

## Messages

Buyers write to you through **Messages** (the existing app) as NPCs, with a few short texts: confirmation, a place, a time, a thank-you or a complaint. The text carries tells (see the buyer page). Game code writes these, never a player.

## State and saves

(Host-authoritative, as for the Hardware Store.)

| What | Where |
| --- | --- |
| **Open orders, buyer profiles, demand pools** | Host memory, saved with the world |
| **A player's rep, trades, scam count, fee tier** | The player's progress record |
| **Escrow and wallet** | The player's progress record (dollars) |
| **Session history and trace** | The player's progress record and the computer's file system |
| **Packages in transit** | Shipments, like the Hardware Store |

All changes go through the host. Actions carry ids, not prices. The host checks the price, the stock, the rep, the deadline and the VPN state.

## Security notes

- The seat decides the player (`GameAppSeat`), never what an action says.
- Orders, buyer profiles and the sting flag live only on the host. The **sting flag never reaches the UI state**, only the tells do.
- The market is **game code**, not a Store app, so money and listings cannot be forged. See the Security section in AGENTS.md.
- The app must not take a buyer, price or amount from the client beyond an id the host knows.
- Messages from NPC buyers are sent by game code only (`NaviNpc.Send`). No `messages.*` call may send as a buyer.

## How it fits the code

An ordinary Navi app, three files, like the Hardware Store (see `Libraries/navi/docs/app-architecture.md`):

- `HushUiState.cs`: records for rows (orders, listings, wallet, rep), enums for the tab, and a `Revision`.
- `HushViewModel.cs`: all the calls, filters and wording.
- `Hush.razor`: stateless markup with `BuildHash()`.

Host systems it needs: a `MarketSystem` (orders, buyers, demand), `PlayerProgress` additions for rep and wallet, new inventory items (tabs with a purity attribute), and the handover points in the world.

## Version 1 scope

**In**

1. The Hush app with Orders, Shop, Wallet, Reputation, Security.
2. One VPN switch and a trace number.
3. 5 to 10 NPC buyers with profiles, tells, and an orders board.
4. Accept, then **Meet** or **Dead drop**, with a sting chance.
5. Escrow, a fee, three reputation levels.
6. A shop with the supplies for Static.
7. A session history and a wipe puzzle (reuse the Cover one).

**Out (later)**

- Listings (sell without an order). Maybe version 1.1.
- Player-run markets, directories, ads.
- Disputes, player-to-player trade, reviews between players.
- Courier, mail, locker rental.
- Market hacking and escrow theft.
- Reputation decay, blacklists.

## Fun tests

| Test | Pass if |
| --- | --- |
| First sale, no instructions | The player finds Orders, accepts, and delivers in under 10 minutes |
| Reading buyers | Players notice the tells after a few trades and say "that looks like a sting" |
| Meet vs drop | Players deliberately choose between them for risk and price |
| Trace and VPN | Players turn the VPN on without being told |
| Wipe | Players decide whether it is worth the time |
| Repeat | They come back for the next order |

## Later: player-run markets

The same app, with a **market picker** at the top. The player can browse several markets, join one, or **open their own** (set the fee, rules, escrow and skin). Everything else in the app stays. Design is in [crime-rp.md](crime-rp.md) under "Player-run markets".

## Open questions

1. Should the handover really need a walk, or can a fast option exist for time-poor players?
2. How are stings drawn: a fixed share, or tied to the player's heat and rep?
3. Is the sting arrest too harsh for a first offence?
4. Does a new player start with a small order to learn the tells safely?
5. Should the VPN be bought, rented by the hour, or both?
6. Where do the drop points sit in the world, and how many?
7. Does the app need a map view for meetings and drops?
8. How do NPC buyers get names and personalities without generated text? (A written pool of handles, voices and message templates.)
