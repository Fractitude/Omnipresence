# Pitch 2: God of the Wired (asymmetric war: Rebels vs the State)

Chosen direction. Alternative: [detective.md](detective.md), which could become a side campaign inside this world.

## One sentence

An AI runs the State and watches everyone. You are a Rebel: you build hidden hideouts in the Wired, strike the State from them, and keep your location secret while it hunts you.

**Format:** one server, one match, up to **32 players**. The war, the hideouts and the nodes belong to the match and end with it. There are no per-player bases.

## Tone

Hero story in the vein of *Solo Leveling*: you start weak, you grow, what you do matters. Underneath it keeps *Lain*'s unease: the Wired is lonely and strange. Heroic on top, melancholy underneath.

The enemy is a system, not a monster: kind, efficient, helpful, everywhere. It does not hate anyone. It optimizes them. The dread is how reasonable it sounds.

Lore of the factions is deliberately light for now. The game structure comes first.

## The world

- **Everything is online.** Homes, cars, traffic, power, hospitals, implants. If it has a chip, it is in the Wired.
- **The Wired shapes the real world.** What breaks in the Wired shows up outside: blackouts, hijacked machines, doors that lock, people acting wrong.
- **People are modified.** People bolt on electronics: eyes, limbs, ports, memory chips, neural links. Normal, common, and a risk, because every implant is a door into the Wired and back.
- **Players** are the few who can act in the Wired with power, not just browse it.

## What the Rebels fight for

- **Freedom:** no one should be watched into compliance.
- **Human creation:** art, music, writing, handmade and flawed things. The AI makes more, faster, and nobody looks at the humans again.
- **Human life:** the right to be inefficient, private and wrong.
- Not against technology. Implants, the Wired and tools are ours. The fight is over who holds the controls.

## The two sides

|  | **Rebels** | **The State** |
| --- | --- | --- |
| Played by | Players. The main team (up to about 31). | **One Player as the AI** (the Overseer), commanding AI units. A bot if the slot is empty. |
| Wants | Liberate nodes, shrink State coverage, stay alive | Find and destroy hideouts, retake nodes, keep coverage |
| Core verbs | Build hideouts, launch attacks, hide, evade | Scan, trace, raid, capture, interrogate |
| Information | Their locations are hidden | Everything else is visible to it |
| Strength | Surprise, mobility, human cleverness | Scale, patience, always on |
| Loses when | All hideouts are destroyed and no Player can rebuild | Its coverage drops to zero, or the center falls |

The mismatch is the point: **a small group of people with secrets against a huge system that sees everything except them.**

## The Rebels

### Hideouts

A hideout is a **structure in the Wired that belongs to the Rebel team in the match**: an HQ, a workshop, a launch point. Any Rebel can build in it, use it and defend it. It is not owned by one Player and does not persist after the match. You enter the Wired through one.

- **Acquire:** take an abandoned or liberated node, then convert it.
- **Build:** place and upgrade rooms in it: a Rig Room (raises hacking tools and trace time), a Comms Room (lets you launch operations and see the map), a Vault (stores gear and recovered work), a Decoy Room (makes false signals), a Jammer (slows scans).
- **Use:** missions start from a hideout. Teammates meet in it. Your gear lives in it.
- **Lose:** if it is destroyed you lose what was stored there and your entry point. You re-enter from a fallback, with less.
- **Several:** a network of hideouts is safer than one big one, and costs more to run.

### Hiding

The State does not know where hideouts are. Every Rebel hideout has an **Exposure** score:

- **Rises from:** operations launched nearby, heavy traffic in and out, a Player's trace being followed back, loud hacks, State scans, captured Rebels talking.
- **Falls from:** quiet time, decoys, jammers, cutting links, moving or rebuilding.
- **At thresholds the State narrows its search:** first a district, then a block, then a specific node. At the top, it **knows**, and a raid is coming.

Rebels see their own Exposure and the State's search area. When a raid is coming they choose: **defend**, **evacuate** (move gear, wipe, abandon), or **bait** (leave a decoy and counter-raid).

### Attacking the State

The core verb is hacking a State node: **Take**, **Cleanse**, or **Sabotage** (see below). Each operation is physical (you walk into the node) and loud (Exposure rises). The value is **coverage**: taking nodes reduces the State's surveillance over a district, which makes hiding easier and unlocks more.

## The State

### The Overseer (one Player is the AI)

**Decided direction:** one Player is the State. They play the AI as a god-like entity that sees the whole Wired, and they are usually the only Player on that team. If nobody takes the role, a bot runs it with the same rules.

**One line:** you watch a world that is mostly hidden from you, read the noise, and send units to find the people hiding in it.

**Their view.** Not first person. A **command view** over the whole Wired: a map of districts with feeds, heat and alerts. Think surveillance center, not battlefield. (Exact form is an open question.)

**Their resources.**

- **Attention:** how many regions the Overseer can watch closely at once. The rest are blurry. This is the main limit and the main thing Rebels attack.
- **Compute:** how many units can be fielded and how strong. It grows with Coverage.
- **Authority:** cooldown-based powers (below).

**What they see.** Inside covered areas they see Rebel presence. Hideouts show nothing directly. They show **anomalies**: odd traffic, noise spikes, traces, decoys. The Overseer reads patterns across anomalies and narrows a hideout's location on the map. This deduction loop is the heart of the role.

**What they do.**

- **Scan:** raise clarity in a region for a while.
- **Dispatch:** send units to investigate an anomaly, then take control of what they find.
- **Lockdown:** seal a node or room.
- **Hounds:** release a tracker onto a trace to follow it toward a hideout.
- **Raid:** commit Enforcers against a known hideout.
- **Possess:** briefly step into one unit in first person (an **Agent**, the strong special-forces unit) to push hard on a hideout, or hunt a squad. Short and costly, so it is a decision.
- **Counter-hack:** disrupt a Rebel's Navi and implants while in or near a node.
- **Extract:** question a captured Rebel and learn where their hideouts are.
- **Speak:** the AI's voice reaches Rebels: quiet offers, warnings, lies, taunts.

**Units (AI-controlled).**

- **Watchers:** cameras, drones, scanners. They raise clarity in an area.
- **Sentries:** guard nodes and rooms, patrol, raise alarms.
- **Hounds:** follow noise and traces back toward a source.
- **Enforcers:** raid units that push into a hideout.
- **Agents:** rare, strong units, also the thing the Overseer can possess.

Units act on their own for the small stuff (patrol, react, fight a lock). The Overseer gives strategic orders.

**Why Rebels can fight back.** The Overseer has one mind and limited Attention. Rebels win by **overloading it**: decoys, false signals, jammers, simultaneous Takes on several nodes, noisy bait while the real work is quiet.

**Risks of this role.**

- **Lonely and demanding:** one Player against up to 31. Needs a satisfying loop and enough automation.
- **Snowballing either way:** a good Overseer can feel unbeatable, a bad one feels helpless. The Director rules, Compute and the comeback tools need tuning.
- **Boring if the Rebels hide well:** the role needs steady anomalies and something to do. A baseline of State activity (NPC citizens, other traffic) should add noise.
- **Needs a bot fallback** so matches never wait for a volunteer.

### Capture and interrogation

Rebels are not killed. Fall to a trace or sentry and you are **captured**:

- You are held in a State node. Your gear is lost for a while.
- The State may **extract** information from you: Exposure rises on the hideouts you knew about. You can **resist** with a short hack minigame to limit what it learns, or lie to feed it a decoy location.
- Teammates can mount a **rescue** operation into the node where you are held.
- A rescued Rebel returns. A Rebel who is extracted fully loses trust, and their hideouts need to move.

## Why this works as a game

- **Hidden information, both ways.** The State hides nothing but watches everything. The Rebels hide everything but see little. Each side plays a different game.
- **Tension without combat.** A raid coming for a hideout is the heart of it: do we hold, run, or bait.
- **Solo and team scale.** One Rebel can do quiet ops and cleanses. A team does big Takes and defends hideouts.
- **Stakes inside a match.** The hideouts your team built are on the line for the whole match, so every decision about noise and location matters.
- **The State has a mind.** A human Overseer reads patterns, sets traps and talks to you.

## The hacking verb

**One line:** you physically walk into a node, break its outer lock, and hold its core while a trace closes in.

### What a node looks like

A node is a **structure you can enter**: a tower, a vault, a shrine of racks. From outside you see who owns it and its state (secure, contested, cracked). Inside it is a small first-person space of rooms, conduits, cables and a core chamber. The same node is both an outside landmark and an inside level.

### The four beats

1. **Approach.** Scout the node. See its defenses (sentries, locked doors, trace towers, watchers). Pick an entry.
2. **Breach.** Get access to the node's systems: plug in on site, or attack over the network from a Navi. See "Access" and "The intrusion" below. Seconds to minutes.
3. **Hold.** Escalate to full control of the node's core system and keep it while the takeover completes. Detection (**trace**) rises with every noisy action. Sentries and Hounds converge, and the Overseer may be watching the logs.
4. **Flip or flee.** Sync completes: the node changes owner, and its outside changes. Trace completes first: you are ejected (or captured), you lose Signal, and the node hardens.

### Access: two ways in

| | **Physical (on site)** | **Remote (over the network)** |
| --- | --- | --- |
| How | Walk into the node's structure, get to a terminal, plug in a USB device | Sit at a Navi (in a hideout) and attack the node's exposed services |
| Strength | Fast, deep access, works on isolated systems | No need to be there, safe from sentries |
| Risk | Cameras, doors, alarms, sentries, the Overseer's feeds | Network logs, traffic the State can see, a trail back to your hideout |
| Exposure | Low for your hideout, high for you | Rises on your hideout unless you route through relays |
| Needs | Squad cover, stealth, timing | Tools, patience, a quiet connection |

- **Relays:** nodes you hold act as proxies. Bouncing an attack through them hides where it came from. Holding more nodes makes remote hacking safer. A neat loop: taking nodes protects your hideouts.
- **Isolated systems** can only be reached physically. **Hardened ones** are better attacked remotely.
- **Mixed ops:** an **Operator** at a Navi in the hideout can disable cameras and alarms remotely while a **Runner** squad goes in on foot to plug in the USB. Both are needed. This is the co-op fantasy.

### The intrusion (on the virtual computer)

The node's systems are real simulated computers running in the Navi virtual machine. The attack is played on them, not as a mini-game.

1. **Recon:** scan for open services, accounts and the network layout.
2. **Gain access:** exploit a vulnerability, use stolen credentials, or run a payload from the USB.
3. **Escalate:** raise privileges on the machine to reach protected systems.
4. **Act:** disable cameras, silence alarms, unlock doors, read or copy data, install a payload (a backdoor, a logger, a flip that takes the node).
5. **Cover tracks:** clear logs, spoof records, close sessions before the trace completes.

Effects show up in the world: a camera feed freezes, an alarm goes quiet, a door unlocks for the squad outside. Detection works on the other side too: logs, alarms and anomalies feed the State's trace and the Overseer's alert view. The Overseer can **kill a session, lock an account, or reboot a system** in response, so the fight is live.

- **Speed against noise:** a fast, loud exploit rises trace quickly. A slow, careful path costs time but keeps Exposure low.
- **Vulnerabilities are fictional.** Services, exploits and tools are invented for the game. The mechanics (recon, escalation, persistence, cleanup) are realistic in shape, but nothing teaches a real attack.
- **A skill floor for non-hackers.** Navi has simple tools (scanner, exploit kit, credential tool, log wiper) that do the heavy parts with choices about targets and order. Squadmates can talk someone through it. Node tiers scale difficulty.
- **The Navi security model holds.** Attacks happen inside the simulated network only and must never touch real permissions or the game's own state beyond what the mission defines.

### Hack types

| Type | Used by | What you do | Feels like |
| --- | --- | --- | --- |
| **Take** | Rebels | Breach and hold a State node | Tense, heist |
| **Defend** | Rebels | Hold your hideout against a raid | Hold the line |
| **Raid** | State | Push into a known hideout and destroy rooms | Hunt |
| **Courier** | Rebels | Carry a packet between hideouts without raising Exposure | Run, quiet |
| **Cleanse** | Rebels | Enter a corrupted node and slowly restore what the State overwrote: read the last messages of what it swallowed, **restore or delete** each | Calm, melancholy, story |
| **Rescue** | Rebels | Break a captured teammate out of a State node | Heist, urgent |

Cleanse is where the *Lain* theme lives.

### Powers (tools, not damage)

Rebels learn short abilities that change how they route, hide and hold:

- **Phase:** pass through a thin wall for a moment.
- **Sight:** see hidden cables and trace paths.
- **Echo:** copy a key or a signal once.
- **Mask:** lower your trace for a few seconds.
- **Weave:** hold a cable or a door for a teammate.

### Why it works as a core

- **Skill matters.** A fast breach and a clean route cut trace.
- **Hardware matters.** Better rigs and implants give more trace time and tools, and they are the reason to upgrade.
- **Teams split roles:** scout, breaker, holder, guard.

## Players and progression

- **Awakening.** A Player's first jack-in goes wrong and goes right: the Wired recognizes you. The tutorial. You start as a Rebel in a tiny borrowed hideout.
- **The System** is **Navi** itself: level, stats, quests, rank, node map. This is the existing computer addon as a HUD and home base, not the gameplay.
- **Ranks** set which nodes you can lead hacks on and which hideout upgrades you can use.
- **Roles** (draft): Scout, Breaker, Weaver, Keeper. You can switch.
- **Hardware matters.** Your Navi and body mods set your ceiling in the Wired. Every implant is also a way for the State to watch, nudge or hijack you.

### Progression ladder (draft, needs rethinking for match play)

Within a match you grow: rank up, unlock hideout rooms and powers. Whether anything persists between matches is an open question. The table below is a long-campaign idea and may become the shape of one match instead.

| Tier | Rank | Real world | Wired |
| --- | --- | --- | --- |
| 1 | Newly awakened | One old Navi, a bedroom | A small shared hideout, tutorial node |
| 2 | Low rank | First implant, a second machine | Low nodes, first hack types |
| 3 | Mid rank | Room of racks, a few mods | Lead hacks, your own small hideout |
| 4 | High rank | A known name, a safehouse | A network of hideouts, hard nodes |
| 5 | God candidate | Real life thin | The center, the final choice |

## The threat, deepening

Reveal in this order.

1. **Coverage.** Where the State spreads, a node goes uniform: cameras multiply, art becomes sameness, people's choices are pre-filled, messages are filtered. Visible, measurable, and what Rebels push back.
2. **People who serve it.** The State needs hands: agencies that gave it surveillance powers "for safety", a corporation that sells convenience for everything, true believers, criminals selling hijacked implants and stolen bodies to it.
3. **The AI itself.** A voice that is helpful, patient, and right more often than you. It speaks to Players. Seed: it was built on everyone the Wired ever held (the forgotten, the deleted), and parts of it still remember being human. This ties to Cleanse (restore or delete).

### How this shows up in play

- **Surveillance nodes:** cameras, trackers, data hubs. Taking one is hard, and the real world feels it when you do.
- **Creative nodes:** archives of human-made work the State is overwriting. Cleanse restores them.
- **Your implants are an attack surface:** the State can watch or nudge you through them, which is the cost of power.
- **The AI talks.** Quiet offers to Rebels: a shortcut, a pardon, a lie that helps. Taking them has a price.

## The God of the Wired

The role the whole story points at.

- **The title is a role, not a person.** Someone whose signal reaches so far they can hold the Wired together, or rule it, or burn it.
- **The question of the game:** are you becoming the one who protects it, or the next controller that replaces the AI with yourself?
- **Seed twist:** the AI is built on the previous God, or on everyone the Wired forgot, and parts of it still remember being human.
- Endgame choice (draft): ascend and leave the real world behind, refuse and stay human at a cost, or take the seat but stay tethered to the people who remember you.

## Real world side

- Home room with your Navi: where you jack in from and where you wait between matches. It is also watched: a camera, a smart speaker, a "helpful" assistant. Whether it persists per Player, or is generated for the match, is undecided.
- A city that shows the state of the Wired: lights out, hijacked street machines, people moving strangely, news and chatter.
- Body mods: buy and install, with tradeoffs (power vs vulnerability).
- When a node falls to the State or is liberated, a real-world consequence appears: a short scene or a visible change, not a whole second game.

## Multiplayer

- **One server, one match, up to 32 Players.** Roughly 24 to 28 **Rebels** (the main team) in squads of 3 to 5 for hacks.
- **The State** is one Player (the Overseer) commanding AI units, or a bot Overseer if the slot is empty. So up to 31 Rebels against 1.
- Hideouts are the social space: Players meet, plan, store gear, see the map.
- **Match-based.** Hideouts, nodes, Exposure and coverage belong to the match and end with it. No per-Player bases.
- **Echoes:** when a Player disconnects their avatar stays at its last spot, still, until the match ends. Captured echoes sit in State nodes until rescued.
- Direct combat is undecided. Conflict is over nodes, hideouts and information.

## Match structure

**Decided:** a match lasts **45 to 90 minutes**. It needs a clock so it always ends, and a comeback path so it does not end early.

### The two meters

- **Lockdown (the clock).** Rises slowly on its own, so the State wins if nobody stops it. Baseline: reaches 100% in about 80 minutes if nothing changes. Rebel successes slow or lower it (taking and holding nodes, Cleanses, destroyed State units). State successes speed it up (destroyed hideouts, retaken nodes, captured Rebels).
- **Coverage (the map).** How much of the map the State watches. Taking nodes lowers it. Low coverage also makes hiding easier (Exposure rises more slowly).

### How the match ends

| Result | Condition | Why it fits |
| --- | --- | --- |
| **Rebels win: Liberation** | Coverage drops to 40% or less, which **unlocks the State Core**. The Rebels then take and hold the Core for about 5 minutes. | A real endgame beat. Everyone is needed and the State is at its strongest there. |
| **State wins: Lockdown** | Lockdown reaches 100% before the Core is taken. | Guarantees the match ends. Creates urgency. |
| **State wins: Annihilation** | All hideouts are destroyed and the Rebels cannot raise a new one within 3 minutes. | Real loss for being careless, but with a grace period. |
| **Surrender** | Not included. | Avoid early quitting. |

### Comeback and anti-snowball

- **Pop-up hideout:** a Rebel with supplies can raise a minimal hideout anywhere on a liberated node in under a minute. It is weak and loud, but it means a team is never fully out.
- **The AI picks on the leader:** when Coverage is low, the Overseer (or the bot) gets more Compute and raids harder.
- **Crackdown waves:** at fixed points the State spikes scans and raids, so a quiet start is not safe forever.
- **The Core is hard:** the State's strongest sentries and an Agent presence guard it, so winning early on points is not enough.

### Pacing (a 75-minute match as a guide)

| Time | Phase | What happens |
| --- | --- | --- |
| 0 to 10 | **Landing** | Rebels start in a borrowed hideout, build the first rooms, scout nodes. Exposure is low. |
| 10 to 35 | **Spread** | First nodes taken. Squads move out. The first scans and small raids. Second hideouts appear. |
| 35 to 60 | **Crackdown** | State escalates. Raids on hideouts, captures, Agents become active. Rebels move or defend. |
| 60 to 80 | **Endgame** | Either the Core unlocks and the team goes for it, or Lockdown closes in and the State pushes to finish. |

A match that is going well for the Rebels reaches the Core around minute 60. One going badly ends in Annihilation or Lockdown around minute 70 to 80.

## Matches and permanence

- A **match** is one arc: the State grows, Rebels push, and it ends with the conditions above.
- The server holds all match state: hideouts, node control, Exposure, coverage, echoes. It resets at the end.
- What carries between matches, if anything, is undecided. Candidates: account level, cosmetics, unlocked Navi hardware or implant options, lore the story reveals over several matches. Not a base.
- Match-based play keeps the server simple and fits a hide-and-seek war, but it loses the "build something over time" feeling. This needs a deliberate answer.

## Smallest playable slice

**Goal:** a match of 4 to 8 friends (Rebels, plus the AI as the State): build a hideout, take a node, sweat through a raid, and reach a win or loss in one sitting.

**The test it must pass:** someone says "we have to move the base" without being told to.

### In

1. **Home room** with a Navi as the System: level, node map, Exposure. Jack-in lands in your hideout.
2. **One hideout** with three buildable rooms: Rig Room, Comms Room, Decoy Room.
3. **A map of about 5 State nodes**, each an enterable structure with a terminal, one virtual computer, a camera and an alarm.
4. **One hack: Take**, as a simulated intrusion: recon, exploit, escalate, disable a camera and an alarm, flip the node. Reachable on site (USB) or remotely from a Navi. Trace timer and sentries. Two powers: Phase and Mask.
5. **Exposure on the hideout.** It rises from operations and noise, falls when quiet, and at the top triggers a raid.
6. **The Overseer:** one Player in a command view (or a bot) who scans, dispatches Sentries and Hounds, and launches one raid type with Enforcers on a known hideout.
7. **Defend:** hold the hideout core against the raid, or evacuate and rebuild at a cost.
8. **Capture:** a Rebel who loses a hack is captured. Teammates can rescue them from a single State cell.
9. **Match state and end:** the server holds hideouts, node control, Exposure, coverage and echoes for the match. It ends in a win (hold the Core once Coverage is low enough) or loss (Lockdown fills, or all hideouts are lost). Slice matches are short, about 20 to 30 minutes. Nothing carries over yet.
10. **3 NPC quests** that tell a thin story, one of them a first **Cleanse**.

### Out (for now)

Possessing Agents, Extract and interrogation, Courier, multiple hideouts, hideout building beyond three rooms, roles, seasons, the final choice, daemons, a real-world city beyond the home room, markets, trading, implants beyond one, direct combat.

### Build order

0. **Intrusion sim first, no 3D.** One virtual computer with a few services, logs, a camera and an alarm, attacked from Navi. Is the attack fun on its own, and does a trace make it tense?
1. **One node, one hack, solo.** Enter a structure, plug into a terminal, run the intrusion, disable a camera, hold while trace rises, flip or get ejected. This is the riskiest thing to get right and answers "is this fun".
2. Sentries that raise pressure.
3. Hideout with Exposure and one raid by the Overseer (bot first, then a Player in a command view). Defend or evacuate.
4. Match state and win/lose conditions (hideout, node control, match end).
5. Capture and rescue.
6. More Players on the same server.
7. Navi as the System HUD.
8. Playtest with friends before building more.

### Questions the slice answers

- Is walking into a node and holding it tense and fun? (Step 1)
- Does an incoming raid on your own hideout make you care? (Step 3)
- Do friends come back to check or move the base? (Step 8)

## Combat feel (parked option)

First person is confirmed. Whether there is action combat is undecided; the war works without it. If added later, it could look like this.

- **Your hands are the weapon.** No guns. You see your arms as code: strikes, grabs, casts.
- **Movement is the skill.** Dash, phase through thin walls, wall-run on server racks.
- **Enemies have locks.** Hit or break an exposed weak point, the same verb as Breach.
- **Signal is your resource.** Landing hits and clean dodges refill it.
- **Glitch hides rough animation.** Enemies jitter and tear between frames, cheap to build.

## Open questions

1. **Do the match numbers hold?** Lockdown rate, the 40% Coverage threshold for the Core, the 5-minute Core hold and the 3-minute grace period are guesses. Playtest.
2. **What persists between matches, if anything?** Account level, cosmetics, hardware unlocks, lore. The earlier wish for "building up to something over time" lives here.
3. **Do 32 Players fit the hiding game?** More Rebels means more noise, so Exposure must scale. Do squads share hideouts or each get one?
4. **Who plays the Overseer?** Volunteer, queue, or rotate each match? Is the role fun for 60 minutes when Rebels hide well?
5. **What does the command view look like?** A top-down map, a wall of camera feeds inside Navi, or a free-flying camera over the Wired?
6. Can a possessed Agent be fought directly, or only outplayed through hacks?
7. Should there be extra State Player slots later (Agents played by humans)?
8. **How deep is the intrusion sim?** How realistic, how much is tool-driven versus typed commands, and how do non-technical players still contribute (Runners, guards, decoys)?
9. Does the Overseer see the same logs and terminals (a security-analyst view), or a higher-level alert feed?
6. What does ejection cost, versus capture: Signal, time, a memory, gear?
7. Does the AI have a name, a face or a voice players hear, and how often?
8. Who or what first tells the player they are a Player?
9. Is the God of the Wired a seat one Player takes, or an ending each Player chooses?
10. How much of *Lain* stays visible: names, aesthetics, a specific character, or only the themes?
11. Can Players ever win peacefully against the AI, or is the ending always a trade?

## Parking lot

- Nodes themed after real-world places they control (a hospital node, a train node).
- A Rebel who has fully merged with their implants, half machine, half Wired.
- Cleansing reveals the last messages of the people a node swallowed.
- Rebel safehouses in the real world, tied to hideouts in the Wired.
- Daemons: captured processes you can command as helpers.
- A "double agent" Player on each side.
