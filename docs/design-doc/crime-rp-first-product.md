# First product: Static (and its minigames)

Part of [crime-rp.md](crime-rp.md). This is the first thing a player makes, sells and gets caught for. Everything here is fictional and abstract: the "recipe" is a set of games, not real chemistry. Numbers are starting guesses.

## What it is

**Static** (placeholder name) is a glowing culture, grown in the dark like a fungus on damp racks. Users say it "tunes you in": it makes them hear the Wired. It is sold as small **tabs**.

It was picked for four reasons:

1. **It fits the world.** It glows, it hums, and its final step is tuning a signal. That is the Wired, in a lab.
2. **The tells are fun.** Light leaks, the fan hum and the power draw are what police notice, and they are things the player can hide.
3. **It needs computers.** The grow room runs on a networked controller, so the Navi OS, hacking and sabotage matter from day one.
4. **It is abstract.** There is no real recipe, so nothing here teaches anything real.

## The chain at a glance

| # | Step | Where | Type | Time | Active |
| --- | --- | --- | --- | --- | --- |
| 1 | **Get a culture** | Dark web or an NPC supplier | Buy | Instant | 10 s |
| 2 | **Grow** | Grow room | Minigame: **Climate** | About 12 min | 3 short checks, 20 to 40 s each |
| 3 | **Harvest** | Grow room | Quick timing | 1 min | 30 s |
| 4 | **Dry** | Drying rack | Passive | About 6 min | 10 s to load and unload |
| 5 | **Press** | Press bench | Minigame: **Tuning** | About 90 s | Fully active |
| 6 | **Pack** | Any table | Quick UI | 30 s | 30 s |
| 7 | **Sell** | Dark web or street | Market | N/A | N/A |

One batch is about **21 minutes** and only about **5 minutes** of active play. You run several in parallel, and use the waiting time for hacking, driving, defending, or other crimes.

## Minigame 1: Climate (growing)

You stand in the grow room with a small panel of three gauges and three controls.

| Gauge | Control | Effect |
| --- | --- | --- |
| **Warmth** | Heater dial | Raises warmth, with a delay of about 5 s |
| **Damp** | Mister (hold to fill) | Raises damp, slow to fall |
| **Dark** | Shade lever | Lowers light, which also changes how much light leaks outside |

- Each gauge drifts toward the room's ambient value. You nudge it back into the **green band**.
- The band **narrows** as the culture matures.
- Time spent in the green adds to **Quality**. Time out of it adds to **Impurity**.
- **Checks** pop up about every 3 minutes: a short window when the gauges are unstable. Between checks the room is stable, so you can leave.
- **Contamination:** now and then a dark fleck appears on a tray. You remove it with **tweezers**, a small steady-hand game. An ignored fleck spreads and can ruin the tray.
- **Manual care adds up to +15 Quality.** Automation does not.

### Automation (the Grow Controller)

A **Grow Controller** is a networked device you place in the room. It has its own Navi app.

- It runs the heater, mister and shade by itself, to a target you set.
- It shows graphs and sends alerts to your Navi.
- It keeps a batch in the green, but with **Quality capped at about 75**. A hand-tuned batch can pass it.
- It lets a batch grow while you are offline.
- **It is a computer.** A rival can hack it: overheat the room, kill the mister, or read your logs. This is how a networked lab becomes a target. A mechanical room is safe but needs your attention.

## Minigame 2: Tuning (pressing)

The core active minigame. You turn dried material into tabs on a **press bench**, and the quality of your tuning is the purity of the batch.

A small screen shows a signal, like an oscilloscope. A tone plays: it gets cleaner as you get closer.

| Control | What it does |
| --- | --- |
| **Frequency dial** (mouse wheel or A/D) | Sets the frequency. There is a hidden peak that drifts slowly. Stay within about 3 percent to hold a **lock**. |
| **Gain dial** (mouse or W/S) | Controls amplitude. Interference bursts push it up. Keep it in the band. |
| **Damp button** (space) | Briefly calms interference, with a cooldown. |

- **Interference bursts** shift the peak every 8 to 12 seconds. The tone wobbles about 1 second before, so a good player hears it coming.
- **Score** is the share of the run you spent in lock, with a penalty for overshooting. The run is about 90 seconds.
- **Purity** is 40 plus 60 times the score.
- **Visual and audio cues** both work, so it is playable with either.
- **A test bench** (cheap, no product) lets you practise.
- **Better gear helps:** a stabiliser slows the drift, a filter shrinks bursts. It never plays the game for you.
- **A failed lock** for more than 5 seconds makes the batch **unstable**: lower purity, a chance of a small fire.

### Cutting

After the press you may **cut** the batch with filler:

| Cut | Result |
| --- | --- |
| None | Full purity, normal amount |
| Light (25 percent) | Purity drops a tier, +25 percent amount |
| Heavy (50 percent) | Purity drops two tiers, +50 percent amount |

Buyers can tell. A cut batch sold as pure is a **scam** and costs your reputation if found out.

## Quality tiers

| Tier | Purity | Price multiplier | Who buys it |
| --- | --- | --- | --- |
| **Rough** | 0 to 39 | 0.4 | Desperate street buyers |
| **Street** | 40 to 69 | 1.0 | Most NPCs |
| **Clean** | 70 to 89 | 1.8 | Dark-web orders with a floor |
| **Pure** | 90 to 100 | 3.2 | Rare high-paying orders, brand building |

## Tells that attract police

Each runs while the lab operates and adds **Heat** per minute. Each has a fix.

| Tell | Where it comes from | Fix |
| --- | --- | --- |
| **Light** | Glow leaks through windows at night | Blackout curtains, a windowless room, a hidden room |
| **Noise** | Fans and heaters hum | A quiet fan, insulation, a cabinet |
| **Power** | The room draws a lot of power | Spread across fuses, a generator, bribe the utility |
| **Smell** | Venting | A carbon filter |
| **Visitors** | Many buyers coming and going | Dead drops, couriers, a different pickup |
| **Waste** | Trash and spent material | Burn it or move it |

Ignoring one is fine for a while. The case file adds it up.

## Failure and risk

- **Contamination:** a ruined tray, and a lost batch.
- **Overheat or unstable press:** a small fire. Without a fire alarm and extinguisher you lose the room, and the fire brigade and police arrive.
- **Sabotage:** break in and taint your culture, hack your controller, cut your power, tip off police.
- **Raid:** police or rivals take the lab and the product.
- **Nobody dies from the lab.** Fire damage is small and survivable.

## Equipment ladder

| Tier | Item | Cost (guess) | Capacity | Notes |
| --- | --- | --- | --- | --- |
| 1 | **Tent** | $400 | 1 tray | Cheap and loud. Tells are high. |
| 2 | **Cabinet** | $1,200 | 2 trays | Quiet. Smaller light and noise tells. |
| 3 | **Hidden room** | $3,500 | 4 trays | Behind a wall or floor. Light and noise gone. Needs power work. |
| 4 | **Warehouse** | $12,000 | 16 trays | Big, big heat. Needs power and a front. |
| + | **Grow Controller** | $800 | N/A | Automation, remote alerts. Hackable. |
| + | **Carbon filter** | $300 | N/A | Kills the smell tell. |
| + | **Blackout kit** | $150 | N/A | Light tell down. |
| + | **Stabiliser, filter** (press) | $600 | N/A | Easier tuning. |

Prices live in a catalogue, like `HardwareCatalog`.

## Economy (starting numbers)

- **Culture:** $200 gives 4 trays.
- **Yield:** about 40 tabs per tray at standard quality.
- **Street price:** about $40 per tab at Street purity.
- **One tray, one cycle:** about $1,600 at Street, about $2,900 at Clean, about $5,100 at Pure, before demand falls.
- **Demand pool:** about 30 tabs per hour per district at base price. Each 10 tabs sold drops the price by about 10 percent. The pool refills about 15 per hour. So a market takes one tray per hour at a good price, which creates real competition.
- **These must be tuned.** The point is a first loop that pays enough to buy a Cabinet in two or three batches.

## Navi apps in the loop

| App | Role |
| --- | --- |
| **Grow Controller** | Set targets, see graphs, get alerts, switch automation |
| **Dark web** (market app) | Buy cultures, list tabs, take orders |
| **Messages** | Orders, buyers, tips, threats |
| **Payments** | Cash moves (dollars) |
| **Intrusion** | Hack a rival's controller, or defend yours |

## Slice scope (first test)

In: **Climate** (one tray, three gauges), a **Grow Controller** you can place, **Harvest** as one click, **Dry** as a timer, **Press** with the **Tuning** minigame, **Pack**, tiers and prices, **three tells** (light, noise, power), and sale to one NPC buyer type.

Out for now: cutting, fire, Hidden room and Warehouse, sabotage, brands, scams, staff NPCs.

### Fun tests

| Test | Pass if |
| --- | --- |
| Climate for 20 minutes alone | Players pay attention at checks and do other things between |
| Tuning, five runs | Players ask for another go, and scores improve with practice |
| Tells | Players notice a tell and buy a fix without being told |
| Controller | Players choose automation for convenience, then miss the quality |

## Open questions

1. Is Tuning (a radio tuning game) the right press minigame, or too abstract for the "cooking" fantasy?
2. Should quality from manual care outrank automation so strongly (+15 versus a 75 cap)?
3. How many trays should a Tent hold so the early loop is not too slow?
4. Do tabs need a separate packaging and weighing step, or does the pack step go away?
5. What are the effects on the user and on NPC buyers? (Flavour only, for later.)
6. What is the brand system: name, label art, logo?
7. How is Static found, in the world and through the dark web, by a new player?
