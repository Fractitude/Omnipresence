# Pitch 4: Crime RP with two worlds (DarkRP + Rust + the Wired)

Captures the direction described on 2026-10-23. It replaces the Lain-fan social pitch as the working direction, but reuses its pieces (the Wired, Navi, the lo-fi look). Earlier pitches stay as references: [living-wired.md](living-wired.md), [living-wired-loop.md](living-wired-loop.md), [god-of-the-wired.md](god-of-the-wired.md), [detective.md](detective.md).

Existing systems this builds on: Navi (a full operating system with apps), Intrusion and Operations ([hacking.md](../hacking.md)), hardware and the store ([hardware.md](../hardware.md)), two currencies ([currency.md](../currency.md)), the Wired as a second map under the real one, health, weapons, vehicles, inventory, abilities.

## One sentence

A persistent crime roleplay server with no jobs: grow and cook drugs, hack for money and documents, sell on the dark web to NPC buyers, fortify a house, raid your rivals, dodge NPC police, and when you want a break from crime, jack into the Wired for powers, minigames and an arena.

## Pillars

1. **Do what you want, nobody gives you a job.** Crime is the main way to earn, but nothing is forced.
2. **The computer is the crime tool.** The dark web, VPNs, traces, smart locks and hacking are the thing this game has that DarkRP and Rust do not.
3. **The world is staffed by NPCs.** Buyers, cops, workers, shopkeepers, so no player ever has to do a boring job.
4. **Everything leaves a trace.** Real-world actions raise heat, digital actions leave logs, and both lead to the same investigation.
5. **Two worlds, two kinds of power.** In the real world you rely on guns, cars and cash. In the Wired you rely on powers and levels.
6. **Players make the stories.** Rivalries, betrayals, raids, takeovers.

## The two worlds

| | **Real world (home map)** | **The Wired** |
| --- | --- | --- |
| Role | Where you live, earn, fight and get caught | Where you hack, play and grow powerful |
| Power | Guns, gear, cars, money | Abilities, levels, exploits |
| Currency | Dollars | Credits |
| Danger | Police, rivals, raiders | Rival hackers, hazards, arena enemies |
| Persistence | Houses, labs, stock, cars | Skills, powers, cosmetics |
| What you do | Production, selling, driving, raiding, hiding | Hacking spaces, minigame modes, powers |

Players switch at their computer, as today. The Wired is a second 3D map, not a menu.

## The core loop

```
 MAKE ──► SELL ──► LAUNDER ──► INVEST ──► DEFEND
  │         │          │           │          │
 grow     dark web    clean       bigger     locks, cameras,
 cook     NPC buyers  money       lab, cars  guards, alarms
 steal                (hack,      hardware
 hack                 front)      powers
        ▲                                      │
        └───── police heat, rivals, raids ◄────┘
```

- **Make:** produce goods or steal them (money, documents, data).
- **Sell:** list on the dark web, or sell to NPC street buyers.
- **Launder:** turn dirty money into clean (optional, a mid-game system).
- **Invest:** a bigger lab, a better rig, cars, guns, staff.
- **Defend:** locks, cameras, alarms, guards. Heat and rivals keep coming.
- **The Wired** is the side loop for fun, powers and credits.

### By time scale

| Time | What you do | Why you return |
| --- | --- | --- |
| **Minutes** | Tend a plant, run a cook, check the dark web, drive a delivery | Small wins |
| **A session** | A grow-cook-sell cycle, one hack, a raid or a defence | A visible profit and a story |
| **Daily** | Restock, collect, defend, re-price, avoid heat | The business is waiting |
| **Weekly** | Expand, rivalries, a big hack, a base upgrade | Growth |
| **A season** | Wipes and rankings | Fresh start with a legacy |

## Systems

### Inventory

A detailed grid inventory (the game has `PlayerInventory`). Items have size, weight and quality. Containers (bags, safes, car boots, lab shelves) matter because **what you carry can be found**. Police can search. Raiders loot what is in reach.

### Production (minigames that stay fun)

Production is the main hands-on loop. Each product has a chain, quality tiers and a risk profile.

| Product (placeholder) | Chain | Minigame | Risk |
| --- | --- | --- | --- |
| **Plants** | Seed, soil, light, water, harvest, dry | Tend: keep temperature, humidity and light in range; spot pests | Smell and power use raise suspicion |
| **Pills** | Chemicals, mix, press | Mix: ratios and timing; unstable mixes | Fumes, fire |
| **Powder** | Precursors, cook, cut | Cook: temperature and stirring; a bad cook spoils | Explosion, noise, heat |
| **Data goods** | Hacks, copy, package | Hacking (see below) | Digital traces |

- **Quality** comes from skill and equipment. It sets the price and the buyer's loyalty.
- **Equipment** is bought, placed and powered. Better gear is faster, quieter and more stable.
- **Automation:** networked sensors and controllers make a lab run itself, which also makes it hackable.
- **Tells that attract police:** smell, noise, electricity use, deliveries, visitors, trash.
- **Brands:** you can name and label your product. Reputation builds on the brand.

### Economy and NPC buyers

Buyers are NPCs, so players never stand in a shop all day.

- **Street buyers:** NPCs in districts who want certain goods. Fast, low pay, high exposure.
- **Dark-web buyers:** orders posted by NPCs with a quality floor, a price and a deadline.
- **Shared demand:** each product has a **demand pool** per district that refills over time. Selling lowers the price, and high prices lose buyers. This makes **undercutting** and **market saturation** real and gives players a reason to compete.
- **Reputation:** each seller (and brand) has a score. High reputation gets better orders.
- **Price memory:** buyers remember who sold them bad goods.
- **Staff NPCs:** hire growers, cooks, couriers, lookouts and guards for pay. They have their own risks (snitching, quitting).
- **Player-to-player trade** exists too, through the dark web and in person.
- **Balance rule:** NPC demand must be the limiting resource so the economy cannot be botted to infinity.

### The dark web

A Navi app and a browser site. This is the game's central marketplace and where the computer matters.

- **Storefronts and listings:** list goods with photos, price and quality claim.
- **Orders:** NPC buyers and players place and take orders.
- **Reputation and reviews:** a seller score, scam flags.
- **Escrow:** a payment hold so players can trade with strangers.
- **Delivery:** dead drops, couriers (NPC or player) or in-person handoff.
- **Messages:** private chat (the Messages app).
- **Safety:** a VPN hides where you list from. Careless listing leaves a trace to your IP and your home.
- **Sabotage tools:** defacing a rival's store, leaking their customer list, posting fake bad reviews, tipping the police. All cost effort and leave traces.

### Hacking (the existing system, extended)

Hacking already works as puzzles with alerts and grades (Intrusion, Operations). In this game it **steals things that matter**.

- **Targets:** banks (money), companies (documents, databases), rival players' computers (their stock lists, their dark-web access), police systems (wipe a warrant), home devices (cameras, locks).
- **Rewards:** dollars moved from accounts (Payments), documents and databases to sell, access to other systems.
- **Traces and VPN:** every hack leaves log lines pointing at your source. A **VPN chain** hides the source: more hops cost money and time, and break if one is compromised. A **clean-up** puzzle (existing Cover stage) removes lines.
- **Evidence:** every hack adds evidence to a case file. See "Heat and law."
- **Hardware matters:** better rigs make puzzles easier (existing).
- **Hacking inside the Wired as a space:** a spatial version (walk into the server, bypass sentries) can come later as a heist mode. The terminal puzzle stays as the core.
- **Spend:** stolen money is **dollars** (a real-world transfer). This changes the current rule that Intrusion pays credits only (see open questions).

### Heat, police and law

Two linked meters, one investigation.

| Meter | What raises it | What it causes |
| --- | --- | --- |
| **Heat (real world)** | Seen crime, smell, noise, gunfire, carrying contraband, kills | Police patrols, stops, searches, warrants, raids |
| **Trace (digital)** | Hacks, dark-web activity, careless VPN, unscrubbed logs | Forensics NPCs build a case |

- **A case file** per player collects evidence from both. When evidence reaches a threshold, the case moves: patrols watch your area, a warrant is issued, a raid is scheduled. You get tells: cars on your street, a drone, a warning on your phone.
- **It is slow on purpose.** "You will be slowly found out" means a sloppy player has days, a careful one has weeks, a ghost may never be found.
- **Wanted levels (1 to 5):** from patrols and fines up to armed raids and helicopters.
- **Laws:** a visible list of offences with severities (possession, production, theft, assault, hacking, murder). Fixed per server at first. Later: laws can change by server events.
- **Police are NPCs.** They patrol, respond, arrest, raid and investigate.
- **Jail is short and playable:** not dead time. A cell with minigames, a bribe option, a bail option via the dark web, and an escape mission.
- **Counter-play:** bribe cops, hack the case file, plant evidence on rivals, hide goods, lawyers (NPC), burn evidence.
- **Players can tip police on each other.** Sabotage by snitching is a core rivalry tool.

### Bases and raiding (Rust's loop)

- **Houses are claimable bases** (the world has houses). Buy or claim one.
- **Fortify:** doors, locks, reinforced walls, safes, windows, alarms, cameras, turrets, guard NPCs.
- **Smart devices:** many of these are Navi-networked. A lock, a camera and an alarm are computers on your base network.
- **Raiding has more than one path:**
  - **Physical:** lockpicks, breaching tools, explosives, vehicles, noise and a time limit.
  - **Digital:** hack the smart lock or camera, disable the alarm, loop the camera feed. Quiet and clever.
  - **Social:** a snitch, a stolen key, a bribed guard.
  - **Police:** tip them to raid your rival.
- **The defender gets alerts** on their phone and Navi and can respond.
- **Offline raiding** needs a plan (see risks). Candidate: stock is protected while the owner is offline unless the base is hacked or the owner has a lot of heat; or raids work only inside raid windows.
- **Loot:** what is not in a safe or locked container is lootable. Safes are tough and recorded.
- **Decay / wipes:** bases and stock need care so the world does not freeze, with seasonal wipes (see Seasons).

### Cars

Players buy and drive cars (the game already has vehicles). Roles: deliveries, escapes, raids, status. Cars have a plate and a record, can be stolen, can be hacked (disable, track), and can be hidden. A car in a car chase is a core police moment.

### Combat (real world)

Guns, melee and health already exist. Keep combat simple, lethal enough to matter and costly: death drops carried items and clears heat, and a short respawn delay. **Avoid pure deathmatch:** the economy and the law punish senseless killing.

### The Wired

A separate, escapist space with its own rules. Its activities pay **credits** and **levels**, not dollars.

- **Powers:** abilities (Glide, Phase, Dash, Resize, Mimic and others, see [living-wired-loop.md](living-wired-loop.md)). They only work in the Wired. They level up with use and credits.
- **Modes (separate, each with a lobby):**
  - **Arena shooter:** the Wired's guns and powers, short rounds.
  - **Zombie mode:** co-op waves.
  - **Hacking dives:** spatial, timed, for credits, blueprints and hacking XP.
  - **Party rounds:** tag, race, hide and mimic.
- **Hub:** a plaza with a shop for powers and cosmetics.
- **Why it exists:** it gives players something to do when they are hot, jailed, broke or tired of crime, and it is where the "hard to kill" fantasy lives.
- **Bridge to the real world:** hardware, blueprints and a few reward items (not currency). Credits never become dollars.
- **Safe zone logic:** the Wired is not a place to hide from police, because your body is at your computer in the real world. See the next section.

### Your body when you are in the Wired

While you are in the Wired, your real body sits at the computer. This makes jacking in a **risk**:

- A rival can find you, rob you, or raid your base while you play.
- Police can arrest you at the computer.
- A guard NPC, a locked door and an alarm protect you.
- A time-out pops you out if something happens (a door breaks, an alarm), costing you an escape moment.

This is Rust's "AFK is vulnerable" idea, made fun and tied to the Wired.

## What is fresh here

| Rival | What they have | What this game adds |
| --- | --- | --- |
| **DarkRP** | Jobs, laws, roleplay | No jobs, NPC staff and buyers, a real economy |
| **Rust** | Bases, raiding, wipes | Digital raiding, smart locks, NPC police, a dark web |
| **GTA Online** | Heists, cars, wanted | A persistent base war and a digital layer |
| **Hacknet, Uplink** | Hacking sim | In a living multiplayer world |
| **Escape from Tarkov** | Loot and risk | An NPC economy and a safe-ish escape (the Wired) |

The big idea: **the computer is the crime tool, and the same case file connects hacking, drugs, bases and the police.**

## Risks (read this)

1. **Scope is enormous.** This is three games: a crime RP, a base-raid survival and a minigame hub. A full build is years. A slice must be small.
2. **An empty server kills it.** Persistent RP needs players online. Offline players' bases cannot be strictly raidable, or nobody sleeps.
3. **Offline raiding and griefing.** Rust's solution is the wipe, the raid cost and the clan. A 64-slot server with casual players needs protection rules.
4. **The economy can be exploited.** NPC demand, prices and refills need tuning and anti-bot limits.
5. **Production minigames can become chores.** Each must be short, varied and skippable by automation, with the interesting choices being what to risk.
6. **NPCs are expensive.** Police, buyers, staff and traffic need pathing, state and networking. Count the cost.
7. **Hacking can be solved by wikis.** Puzzles need variation (the existing word puzzles are drawn from a pool; more variety is needed).
8. **Mature content.** Drug-themed play affects platform rules, age rating and store listing. Check s&box and Steam rules before building the identity around it. A fictional, stylised treatment (made-up substances, no real recipes) is safer.
9. **Two currencies strain the economy.** Stolen cash is dollars. The Wired pays credits. Keep them separate or rethink.
10. **Does the Wired fit?** Powers and a shooter in the Wired might feel like a different game next to a grounded crime RP. They need clear boundaries.
11. **Security.** Hacking and a dark web in a multiplayer game stress the host-authority model (see AGENTS.md): the host decides everything, clients send intents, and puzzles keep secrets on the host.

## Smallest playable slice

**Goal:** 6 to 12 friends on one server for a week. Everyone has a house and a lab, makes something, sells it to NPCs through a dark-web app, gets slowly noticed by police, and sometimes gets raided or raids someone.

**The test it must pass:** someone says "I'm not logging off until I've moved my stock" or "I'm going to raid him tonight."

### In

1. **One district:** a few houses, a street, a shop, a police station.
2. **Houses as bases:** claim one, with a lockable door, a safe, a camera and an alarm (smart devices on a Navi base network).
3. **One product** with one production minigame (plants, tend and harvest, three quality tiers).
4. **One dark-web app** with storefront, listings and orders from NPC buyers with shared demand and falling prices.
5. **One hack type** (steal money from a bank server) with VPN and log clean-up, feeding the case file.
6. **Heat and trace into one case file**, with a warrant and a raid scheduled by NPC police after threshold. Visible tells.
7. **One raid path of each kind:** physical (a breach tool) and digital (hack the smart lock).
8. **Police NPCs** that patrol, arrest and raid, and short playable jail.
9. **Inventory** with containers and searches.
10. **Persistence** of houses, stock, cash, case file.
11. **A basic Wired area** reached from the computer (what exists) with one minigame and credits (can be a stub).
12. **Cars:** one buyable car for deliveries and escapes.

### Out (for now)

Multiple products, laundering, staff NPCs, brands, the zombie and shooter modes, laws that change, helicopters, seasons, clans, base building beyond placing devices, abilities beyond a couple.

### Build order

1. **One crime cycle, one player:** plants, dark-web listing, NPC buyer, cash. Is it fun alone for 20 minutes?
2. **Heat and case file:** police notice, a slow investigation, a raid. Does being found out create tension?
3. **A second player and a raid:** digital and physical breaches, alerts. Does a raid make a story?
4. **Hacking for money with VPN and clean-up** feeding the same case file.
5. **Cars and a chase.**
6. **The Wired as a break:** one mode and powers.
7. **Playtest with 6 to 12 friends** before adding anything.

### Questions the slice answers

- Is the production cycle fun for more than a few minutes?
- Does the case file make hiding feel meaningful?
- Does a raid (either kind) make a story people retell?
- Does the Wired feel like a break or a different game?

## Open questions

1. **Is this the game?** It replaces the Lain-fan social pitch as the working direction. The Lain themes (the Wired, identity, the human-made stance) are now flavour, not the point. Is that intended?
2. **Offline raiding.** Safe-while-offline, raid windows, or open raiding with a cost?
3. **Seasons and wipes.** Does the server wipe (weekly, monthly), and what carries over (levels, powers, cosmetics, hacking skill)?
4. **Do players ever play police?** "No jobs" suggests no, but a player cop role could be a plain optional job later.
5. **Currency rules.** Hacking pays dollars (stolen cash) in this design. Intrusion currently pays credits. Which holds?
6. **Does the Wired give power over the real world?** Proposed no, but a few bridges (hardware, blueprints) exist.
7. **How many NPC types and how deep?** Police, buyers, staff, civilians, traffic. Start with police and buyers.
8. **Server size.** 64 slots with persistence, or smaller servers (16 to 32) with a better feel?
9. **Do rival players share the dark web, or are there separate markets?** Shared increases interaction and abuse.
10. **What is the first product and its theme?** A fictional substance keeps it clear of real recipes.
11. **Mature-content policy.** Does the platform allow it, and what rating?
12. **Relationship to the earlier pitches.** Are the Lain story, mystery and social features parked, merged as flavour, or dropped?
