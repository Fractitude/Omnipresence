# Hardware, the store, hacking and the workbench

How the real world and the Wired feed each other. Dollars (real world) buy hardware. Hardware, installed at home, makes the Wired easier. The Wired gives blueprints and XP. The two currencies never mix, see [currency.md](currency.md).

```
Wired: crack dead servers (Intrusion) ──► blueprints, XP, credits
                                              │ blueprint unlocks special orders
Real world: Hardware Store ◄──────────────────┘
   buy with $  ─or─  hack the staff panel for a discount and free rerouted orders
        │ package arrives after a delay
        ▼
   Workbench (at home): install parts into the rig
        │
        ▼
Wired: every terminal gives more attempts and removes wrong words
```

## The apps

| App | Where | Kind | What it does |
| --- | --- | --- | --- |
| **Intrusion** | Home computer, the Wired | System app | Cracks dead servers: a word puzzle that pays XP, credits and blueprints. |
| **Hardware Store** | The store's computer only | Normal app | Sells parts for dollars. Has the staff login hack. |
| **Workbench** | Home computer | System app | Installs and removes parts. Sells nothing. |

Code: `Code/Apps/Intrusion`, `Code/Apps/HardwareStore`, `Code/Apps/Workbench`. Each is a shared Navi app split into a view model, a UI state and a stateless panel (see `Libraries/navi/docs/app-architecture.md`). Manifests are in `Assets/navi/system/` (Intrusion, Workbench) and `Assets/navi/shop/` (Hardware Store).

## Hardware

Defined in `Code/Hardware/HardwareCatalog.cs`. The rig has 4 slots: **RAM x2, CPU x1, Card x1**. A part adds attempts (extra wrong guesses a terminal allows) and removed wrong words (already gone when a terminal opens). Effects add up across installed parts.

| Part | Slot | Effect | How you get it |
| --- | --- | --- | --- |
| Mismatched RAM Stick | RAM | +1 attempt | In stock, $120 |
| Used Pentium CPU | CPU | 1 wrong word removed | In stock, $250 |
| Signal Tuner | Card | 1 wrong word removed | Special order, $200. Needs the Signal Tuner blueprint (Hollow Pines Radio). |
| RAM Stack | RAM | +2 attempts | Special order, $500. Needs the RAM Stack blueprint (The Copy Shop). |
| Mainframe CPU Module | CPU | 3 wrong words removed | Restricted: only by rerouting an order |
| Server ECC RAM | RAM | +3 attempts | Restricted: only by rerouting an order |
| Resonance Card | Card | +2 attempts, 1 wrong word removed | Restricted: only by rerouting an order |

Kinds of stock:

- **In stock:** anyone can buy it.
- **Special order:** needs a blueprint, which a dead server gives. The store lists it as locked until you have the blueprint.
- **Restricted:** never sold. It exists only as another customer's order that the staff panel can reroute.

Part ids are saved in player progress, so never rename one.

## Hardware Store

A Navi computer in the store, with a `StoreTerminal` component on it (`Code/Hardware/StoreTerminal.cs`). I placed one named "Hardware Store Computer" in `home.scene` as a placeholder. Move it into the real store.

- **Tag.** The computer has the tag `hardware-store` (`StoreTerminal` adds it when the computer wakes, so the scene doesn't need it, though you can also set `Tags` on the computer). The app's manifest lists the same tag in `ComputerTags`, so Packages doesn't offer it and `Install` refuses it on any computer without the tag. See "Computer tags" in `Libraries/navi/README.md`.
- **Kiosk.** `StoreTerminal` sets the computer's `OnlyApps` to just the store app, so the computer has no other apps, not even Settings or Packages. This is a Navi feature, see "Kiosk computers" in `Libraries/navi/README.md`. The app also refuses to work on a computer without a `StoreTerminal`: tags only limit installing, and a scene can still list the app on another computer.
- **Shop tab.** Lists in-stock parts and special orders. Buy takes the dollars and puts a package on its way.
- **Orders tab.** Your packages on their way, with a countdown.
- **Delivery.** A package arrives at home after `OrderDeliverySeconds` (60 s, short for testing). Arrival is checked on the host once a second and on load, so time spent offline counts. Real mail should be slower: tune the constant.
- **Money.** Only dollars are used here.

## Hacking the store

On the store's Staff tab, **Break in** plays the same word puzzle as the dead servers. Installed hardware helps.

- **Success:** the staff panel opens for 10 minutes (`StoreAccess.UnlockSeconds`). While it is open:
  - every price is cut to 60% (`DiscountPercent`);
  - the Staff tab lists three orders from other customers (`HardwareCatalog.Orders`), each holding a restricted part. **Reroute to my home** sends that package to you for free, arriving after `RerouteDeliverySeconds` (120 s). Each order can be rerouted once per player.
- **Failure:** the terminal locks and you can't try again for 2 minutes (`LockoutSeconds`). Leaving the puzzle is not a failure.
- You can break in again as often as you like once the panel closes. XP (50) is paid on the first success only.

The unlock and the lockout are in memory on the host (`StoreAccess`), so a host restart clears them. Everyone watching the screen sees the puzzle, but the password never reaches the shared state.

Not built: rerouting another *player's* shipment. The orders to reroute are the store's own made-up customers.

## Workbench

Home only: in the Wired it says "Your tools are at home". It never sells anything.

- **Your rig:** all 4 slots, filled or empty, with a Remove button on each installed part.
- **Parts:** what has arrived and isn't installed, grouped (RAM x2). Install puts it in a free slot of its kind. A full kind disables Install: remove something first.
- **Summary:** what the installed parts add to every terminal.
- **Incoming:** how many packages are still on their way.

Old data: parts the workbench "built" before it became an installer are moved into the rig when the record loads (`ProgressRecord.Built`, migrated in `Sanitize`).

## Intrusion (the Wired side)

Dead servers are word puzzles in the style of a memory dump. Click a word to try it as the password; the terminal says how many letters are in the right place. Yellow brackets are tricks that remove a wrong word or give the attempts back. Wrong guesses use up attempts, and at zero the terminal locks (try again for a new dump).

| Server | Difficulty | Reward (first clear only) |
| --- | --- | --- |
| Hollow Pines Radio | Easy | 80 XP, 120 credits, Signal Tuner blueprint |
| Cyberia Basement | Medium | 200 XP, 400 credits |
| The Copy Shop | Hard | 500 XP, 1,000 credits, RAM Stack blueprint |

Only works in the Wired ("No signal" at home). Pays credits, never dollars. The puzzle logic (`IntrusionPuzzle`) has no engine objects, and the store reuses it.

## What is saved

Per player, in the progress record (`Code/Progression/ProgressRecord.cs`, one JSON file per player in the game's data folder):

| Field | Meaning |
| --- | --- |
| `Cleared` | Dead servers already looted |
| `Blueprints` | Blueprints found |
| `Inventory` | Parts owned and not installed (one entry per piece) |
| `Installed` | Parts in the rig (one entry per piece) |
| `Shipments` | Packages on their way: part, arrival time, source (`order` or `reroute`) |
| `Rerouted` | Store orders this player has rerouted |

All changes go through `PlayerProgress` on the host (`TryOrder`, `TryReroute`, `TryInstall`, `TryUninstall`, `CompleteIntrusion`). Clients can't change them.

## Security notes

- The seat decides who acts. Apps work out the player from the computer's seat, never from what an action says. Actions carry only an id, and the host checks the price, the blueprint, the stock, whether the staff panel is open and the slot room.
- The puzzle's password stays in the host's puzzle object. It is never put in the UI state, which goes to every player.
- Tags and the kiosk list only limit where an app goes. Core system apps (Intrusion, Workbench) are installed everywhere and tags don't change that, so they check the world themselves (Wired or home).
- The kiosk setting is a scene property that is never saved and that no app or `User...` call can change.
- Known limit from AGENTS.md: hostile code in the game can still read or change host state. None of this stops that.

## Tuning

| What | Where |
| --- | --- |
| Part effects, prices, slot counts, restricted orders | `HardwareCatalog.cs` |
| Delivery times | `HardwareCatalog.OrderDeliverySeconds`, `RerouteDeliverySeconds` |
| Discount, unlock time, lockout time | `StoreAccess.cs` |
| Servers, words, difficulty, rewards, blueprints | `IntrusionServers.cs` |

## Open work

- A real source of dollars at home. Right now the only way to get dollars in testing is `progress_give_money`.
- Rerouting other players' real shipments.
- More dead servers. Rewards are first clear only, so the three run out.
