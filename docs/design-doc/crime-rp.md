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
5. **Two worlds, two kinds of power, one reality.** In the real world you rely on guns, cars and cash. In the Wired you rely on powers and levels. Wired power reaches into the real world through its networked systems, and the Wired leaks into it.
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

### Player-run markets

Players can **run their own dark-web markets**, charge fees, and fight over customers. The game starts with one weak NPC market so the economy works on day one. Player markets compete with it and with each other.

**Setting one up**

- A market is a **server** running on hardware the operator owns, in their base or a rented host. Its size depends on the hardware: bandwidth sets how many users it can serve, storage sets how many listings.
- The operator buys an **address** (the market's name in the dark web directory), picks a **skin** from a small set of lo-fi templates, and sets the rules.
- The market software is **the game's own system app**. The operator configures it. They do not write code for it, so money and listings can't be forged (see Security).

**What the operator controls**

| Setting | Effect |
| --- | --- |
| **Transaction fee** (percent) | Operator income. High fees push vendors and buyers to rivals. |
| **Listing fee, vendor bond** | Income and a barrier to scammers. |
| **Categories allowed** | Drugs, data, weapons, services. Narrow markets earn trust in one niche. |
| **Entry rules** | Open, invite only, vouched by a member, or paid. |
| **Escrow** | On (safer for buyers, the operator holds funds) or off. |
| **Logging** | On (easier dispute handling, but evidence if seized) or off. |
| **Moderators and staff** | NPC or player staff for disputes and support, for a wage. |

**How a market grows (the fight for customers)**

NPC buyers pick a market by weighing:

- **Selection:** how many listings fit what they want.
- **Price and fees.**
- **Trust:** the market's reputation, scam rate and uptime.
- **Safety:** has it been busted or hacked lately.
- **Reach:** how easy it is to find (directory rank, ads, referrals).

Market **size** is the number of customers and the volume of trade. It shows in the directory ranking, so everyone can see who is winning. Player buyers and vendors also choose among markets.

**Competing, clean**

- Lower fees, run promotions, offer better vendor terms.
- Invest in uptime and security.
- Win vendors with exclusive access and bonds.
- Pay directories and ads for visibility.

**Competing, dirty** (each one is a hack or action with traces)

- **Take the rival down:** DDoS their server for a while, using hacked machines.
- **Deface or leak:** break in and publish their user list or vendor list, hurting trust.
- **Steal the escrow:** hack the vault and take the funds held for trades.
- **Infiltrate:** sign up as a vendor or moderator and scam from inside.
- **Poach:** buy off their vendors.
- **Smear:** fake reviews and rumors.
- **Tip off the police:** push a rival market onto the case list.

**Risks of being an operator**

- **Big means hot.** A market's case file grows with traffic and volume. A large market draws a **takedown raid** on its server and its operator.
- **Logs are evidence.** Logging helps you settle disputes and sinks you if seized.
- **Escrow is a target.** Holding user funds makes you a hacking target. If the funds are stolen you must repay or lose your name.
- **Exit scam.** An operator can empty the escrow and vanish. It pays well once, and ends their reputation for the season. It is a deliberate emergent story.
- **Staff can betray you.** A moderator can leak or steal.
- **You must keep hardware alive.** A powered-off or raided server means a dead market.

**Why it works**

- It gives players a **business to build**, not just a shop.
- Competition is **visible** (directory rank) and **personal**.
- It uses the hacking, hardware, traces and base-raiding systems you already plan, so nothing here needs a new subsystem.
- It makes the **dark web a player-run place** with politics and drama.

**Numbers to tune (guesses)**

- NPC market fee: 10 percent. Player markets typically run 3 to 12 percent.
- Server capacity grows with hardware tiers.
- Customer allocation by weighted scoring, with a floor so small markets can survive.
- A takedown risk that scales with size and logging.

**Slice note:** not in the first test. The first slice has one NPC market. Player-run markets are the second test (see build order).

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
- **Bridge to the real world:** hardware, blueprints and a few reward items (not currency). Credits never become dollars. **Wired power also acts on the real world's networked systems, and the Wired leaks**, see "The Wired leaks into the real world" below.
- **Safe zone logic:** the Wired is not a place to hide from police, because your body is at your computer in the real world. See the next section.

### The Wired leaks into the real world

**Decided 2026-10-23, replacing the earlier "no power over the real world".** Both worlds are real in their own way. Everything in the real world is networked, so **power in the Wired is power over the real world's systems**. The Wired also **leaks**: odd things cross over.

**The rule that keeps the balance:** Wired power acts on **systems, not on bodies**. It can lock a door, kill the lights, take over a car's controls or blind a camera. It cannot shoot a person, and a person with a gun is still dangerous. Killing stays a real-world, gun-and-explosion matter.

#### 1. Reach: Wired power over networked things

Your Wired level (and hardware) sets your **Reach**: which real-world devices you can touch from the Wired, from how far and with what force.

| Tier | Reach | Examples |
| --- | --- | --- |
| **1. Nudge** | Weak, nearby, one device | Flicker a light, open a weak lock, read a camera, play a sound through a speaker |
| **2. Control** | Several devices, a building | Cut a building's power, lock or unlock smart doors, disable an alarm, take a car's engine and doors, loop a camera |
| **3. Influence** | A block or district | Change traffic lights, jam police radio, delay a dispatch, read an ATM's records, shut down a lab's sensors |
| **4. Rewrite** | Rare, huge cost | A short, local **glitch field**: doors fail, lights stutter, props jitter, cameras show the wrong thing, time stutters |

- **You do it from the Wired.** Real-world devices appear in the Wired as objects. Touching one acts on the real device. A town map in the Wired shows lit-up nodes.
- **Powers are active, with cooldowns and Heat.** The Wired powers (Glide, Phase, Resize, Mimic and others) still work in the Wired. A few have real-world effects through networked things.
- **Leaving a body:** you are in the Wired, so your body sits at the computer (see below). Acting in the real world from the Wired is a deliberate risk.

#### 2. Signatures: it all leaves traces

Every real-world action from the Wired leaves a **Wired signature** in the case file.

- Heavier Reach leaves a bigger signature.
- Police forensics NPCs follow signatures to your computer.
- A VPN or relay helps, but strong effects are loud.
- A rival with a Wired detector can see your signature and learn who did it.
- A clean-up mission in the Wired (existing Cover stage) removes lines.

#### 3. Leaks: the Wired spills into the world

A **leak** is a spot in the real world where Wired energy has crossed over. Leaks are part of the world.

- **Cause:** big Wired events (a boss dies, a Reach 4 effect, a huge hack, a Wired mode finishes) and slow build-up around heavy Wired traffic.
- **What happens:** a small area behaves oddly for a few minutes: flickering lights, static on radios, a ghostly figure, objects drifting, a phone that rings by itself, a street sign that shows a Wired address.
- **What it gives:** leaks drop **Wired artifacts**, strange items that exist in the real world: rare hardware, tools, valuables. They sell on the dark web and unlock Wired content.
- **Competition:** the first people to find and reach a leak get the loot. Rivals can race, ambush or camp it. Police can cordon one off.
- **Safety:** leaks never kill. They are strange and risky in a different way.

#### 4. Presence: ghosts and voices

- **Watch:** see through a camera, a TV or a phone camera that you reach.
- **Speak:** talk through a speaker or a phone to a player or an NPC. Useful for scams, fear, instructions and bribes.
- **Appear:** at higher tiers your Wired avatar can flicker as a **ghost** on screens and glass near your reach. Strictly for show.

#### 5. Defence: how real-world people push back

- **Go analog.** A base with no network and mechanical locks cannot be touched, but it also has no smart alarms, cameras or automation. A real trade.
- **Air gaps and shielding.** Offline machines, shielded rooms and surge protectors block Reach.
- **Jammers and detectors.** Buy gear that blocks signals or shows Wired activity near you.
- **Hardware and a hacker of your own.** A friend in the Wired can defend your devices, or hunt the intruder's signature.
- **Police.** Wired crime is crime. It leaves signatures.
- **Cost:** a heavily networked base is easy to run and easy to attack. A mechanical base is slow and safe.

#### 6. Both are real: stakes in the Wired

- **Property in the Wired persists.** A Wired home, a server, powers and ranks all keep their value across seasons.
- **Danger in the Wired.** Losing in the Wired costs credits and items there and a short stun at your real body (a few seconds of blur), because your mind took the hit. No death in the Wired.
- **Death in the real world** drops you out of the Wired at once.
- **Reputation crosses over.** Your Wired rank and your real-world standing are one name.

#### Why this works

- It makes the two worlds **one game**, so the Wired is not a separate arcade.
- It gives the Wired real value to the crime loop: powerful Wired players become kingpins of infrastructure.
- It sets Wired levels against guns and cash, so every kind of player has a path.
- Leaks add events and a reason to go out and explore.
- It uses the existing hacking, hardware and Navi systems.

#### Risks

- **Balance.** If Wired power scales too far, real-world players feel helpless. Tiers, cooldowns, signatures and the "systems, not bodies" rule are the safeguards. Playtest heavily.
- **Two kinds of players.** Players who never touch the Wired must still have a real path (analog defence, guns, cars).
- **Complexity.** Mirroring real devices in the Wired and keeping them in sync is a big build. Start with a handful of device types.
- **Griefing.** Locking someone in, turning off their car and killing their lights can feel awful. Needs limits (duration, immunity windows, protections near spawn).
- **Leaks may feel random.** They need clear telegraphing (a warning, a signal on Navi) so players can plan.
- **Security.** Real-world effects from the Wired must run through the host with permission checks, signatures and rate limits. A Wired action must never act as a different player.

#### Slice note

Not in the first test. The earliest version is one Tier 1 effect (open a weak smart lock from the Wired), a signature in the case file, and one leak event. See the build order.

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

Player-run markets (the second test), multiple products, laundering, staff NPCs, brands, the zombie and shooter modes, laws that change, helicopters, seasons, clans, base building beyond placing devices, abilities beyond a couple.

### Build order

1. **One crime cycle, one player:** plants, dark-web listing, NPC buyer, cash. Is it fun alone for 20 minutes?
2. **Heat and case file:** police notice, a slow investigation, a raid. Does being found out create tension?
3. **A second player and a raid:** digital and physical breaches, alerts. Does a raid make a story?
4. **Hacking for money with VPN and clean-up** feeding the same case file.
5. **Cars and a chase.**
6. **The Wired as a break:** one mode and powers.
7. **Playtest with 6 to 12 friends** before adding anything.
8. **Second test: player-run markets.** One player sets up a market, charges a fee, and competes with the NPC market for customers. Does anyone want to run one, and does the rivalry matter?

### Questions the slice answers

- Is the production cycle fun for more than a few minutes?
- Does the case file make hiding feel meaningful?
- Does a raid (either kind) make a story people retell?
- Does the Wired feel like a break or a different game?

## Decisions

Recorded 2026-10-23. Numbers are starting guesses to tune in playtests.

| # | Topic | Decision |
| --- | --- | --- |
| 1 | **Direction** | This crime RP is the working direction. The Lain pitches stay as **flavour**: the Wired, the lo-fi look, the themes of identity and memory. The Lain story, mystery and social features are parked as separate pitches. |
| 2 | **Offline raiding, test** | None. A base is sealed while its owner is logged off. At 6 to 12 friends it will not matter. |
| 2 | **Offline raiding, launch** | **Hard, not immune.** Offline bases get time-locked doors and safes (breach time about 3 times longer), and guard NPCs stay on duty. One raid can take at most about 25 percent of the stock; the rest sits in a time-locked vault. Raiding an offline base raises the raider's heat sharply. No raid windows. |
| 2 | **Online and in the Wired** | A player who is online, or jacked into the Wired at their computer, is fully raidable. Jacking in is a risk. |
| 3 | **Wipes, test** | No seasons. Wipe by hand whenever. |
| 3 | **Wipes, launch** | **Seasons of 4 to 6 weeks.** A wipe resets houses, stock, dollars, cars and case files. It keeps character level, hacking level, Wired powers, credits and cosmetics. The Wired is the progression that carries over. |
| 4 | **Player police** | No. Police are NPCs. A player cop role could be an optional job much later. |
| 5 | **Currency** | The **target** decides the pay. Real-world targets (banks, companies, rival players) pay **dollars** and leave traces in the case file. Wired dead servers pay **credits** and are safe practice. Same puzzles, different risk. **Not yet done:** `docs/currency.md` and the Intrusion rewards still say the Wired pays only credits and need updating when this is built. |
| 6 | **Wired and the real world** | **Changed 2026-10-23:** the Wired leaks into the real world and Wired power affects it. Wired power acts on networked **systems, not bodies**, in tiers (Nudge, Control, Influence, Rewrite), leaves signatures in the case file, and can be countered by going analog. The Wired also causes **leaks**, real-world anomalies that drop Wired artifacts. Hardware and blueprints still bridge too. See "The Wired leaks into the real world". |
| 7 | **NPC types** | Police and buyers first. Staff NPCs after the first test. Civilians and traffic later. |
| 8 | **Server size** | Design and tune for **32 players**, even though the engine allows 64. |
| 9 | **The dark web** | One shared dark web for everyone, including player-run markets. Interaction and abuse are accepted for the drama. |
| 10 | **First product** | A **fictional** substance, so no real recipes. Theme still open. |

## Open questions

1. **Mature-content policy (action item).** Check s&box and Steam rules on drug themes, and the age rating, before the identity depends on it. A stylised, made-up substance is the fallback.
2. **First product and its theme.** Name, look, the chain and what is special about it.
3. **Wipe details.** What happens to a base whose owner is away for weeks (decay, repossession)? Is there a starter bonus for returning players?
4. **Offline raiding numbers.** The 3 times breach time, the 25 percent cap and the heat penalty need tuning.
5. **Market hosting.** Do markets run on a physical computer in the base (raidable, can be unplugged), or in rented hosting (safer, a recurring cost, a different target)?
6. **How many markets can exist** on a 32-player server? A cap, or whoever can afford it.
7. **How is escrow money stored?** On the market's server (stealable) or by the game (safe)? Stealing it is a core fantasy, so the first.
8. **Can players tax each other?** A market fee is one. Are protection rackets or regional monopolies in scope?
9. **Can one operator run several markets?** Cartels and market mergers add depth and risk.
10. **Real money.** State a hard rule: no real-money tie, no real-money trading.
11. **Market security.** The market software must be game code, with host-authoritative money and listings, so a Store app cannot forge anything. See AGENTS.md on untrusted apps.
12. **Reach numbers.** How far, how long and how strong are each tier, and what hardware and level do they need?
13. **Which real-world devices exist** at first (lights, locks, cameras, cars, alarms, ATMs, traffic lights) and which are built first?
14. **How does the Wired show real devices?** A mirrored town, a list on Navi, or objects floating in a map?
15. **Leak design.** How often, how large, how are they announced, and what artifacts do they drop?
16. **Griefing limits.** Maximum lock-in time, immunity near spawn, and a way to break free from a locked-in situation.
17. **Wired defeat.** Is the stun at your body enough, or should a real-world consequence be stronger?
18. **Should the Wired map mirror the real map,** so a Wired position maps to a real-world position, or stay separate with only devices linking?
