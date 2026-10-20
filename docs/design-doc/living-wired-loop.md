# The Living Wired: gameplay loop and mechanics (fun first)

Companion to [living-wired.md](living-wired.md) and [living-wired-toys.md](living-wired-toys.md). **Story is off.** This file is about what keeps a player playing: the loop, the powers, the games and the reasons to come back.

It builds on what already exists: Intrusion and Operations ([hacking.md](../hacking.md)), the hardware store, workbench and rig ([hardware.md](../hardware.md)), the two currencies ([currency.md](../currency.md)), and character plus hacking levels.

## The fantasy

**Home is small and dull. The Wired is a playground where the rules bend.** You jack in and you are a full body in a place with no real-world rules. You can fly, phase through walls, shrink, bend gravity, and play with friends. You get those abilities by **hacking** the Wired itself.

## What keeps people playing

Five sources of fun, so the game is never one thing:

| Source | What it feels like | Already built? |
| --- | --- | --- |
| **Bodily play** | Flying, dashing, phasing, bending space. Fun with no goal. | No |
| **Hacking** | A tense puzzle with a clean-versus-loud grade. | Yes (Intrusion, Operations) |
| **Games** | Short rounds with friends that use your powers as the rules. | No |
| **Hanging out** | Places to be with people, with something to do together. | Partly (the Wired scene) |
| **Making** | Rule-bending places, marks, artifacts, rooms. | No |

The loop connects them: **hacking earns powers, powers make play fun, play with friends earns more to hack for.**

## The core loop

```
HOME (2 min)                    THE WIRED (30 to 60 min)
 pick loadout, install parts ─► plaza: a round starts every 5 min ─► play
 odd jobs for dollars               │                                    │
 open packages                      ▼                                    ▼
        ▲                    hack a server / operation ◄──── want a new power
        │                           │
        └─── parts, blueprints ◄────┴──► credits, hacking XP, exploits
```

### By time scale

| Time | What you do | Why you come back |
| --- | --- | --- |
| **10 seconds** | Fly across the plaza, glitch someone's trail, throw an artifact | It feels good to move |
| **5 minutes** | A plaza round (tag, race, hide and seek) | Short, funny, one more |
| **30 minutes** | A hack or operation, then rounds with a crew | A grade, a new power, bragging |
| **One session** | Pick tonight's goal: new exploit, a Ghost run, a win streak | A visible target |
| **Daily** | Home: parcels, jobs, rig; the plaza's rule of the day | A reason to log in |
| **Weekly** | A bigger event, a new server, a new round | Variety |
| **Long term** | Hacking level 30, a full collection, a place with your name | Mastery and identity |

## Exploits (the powers)

An **exploit** is a single, small rule-break you load into your rig and trigger in the Wired. It is the core toy.

### How they work

- You own exploits. You **load up to N** into your deck before jacking in (your loadout).
- Each has a **key**, a short **effect**, and a **Heat cost**.
- **Heat** is the one resource. Using powers raises it, and it cools over a few seconds. At max you **glitch**: a short, funny failure (a stutter, a blink to somewhere nearby, your avatar tearing). It is a joke, not a punishment, and other players can see it.
- Heat shows as a visible **aura** around you. Others can read your heat and your loadout style. That makes powers social.

### The starter set (about 12)

| Group | Exploit | What it does |
| --- | --- | --- |
| **Move** | **Glide** (free at start) | Hold to float and steer; slow Heat. |
| | **Dash** | A short burst in your look direction. |
| | **Phase** | Pass through thin, shimmering walls for a moment. |
| | **Blink** | Teleport to where you are looking. |
| | **Rail Surf** | Ride cables and conduits at speed. |
| **Body** | **Resize** | Shrink small or grow big for a few seconds. |
| | **Overclock** | Run faster for a short time, with high Heat. |
| | **Mimic** | Turn into a prop or another avatar. Great for hide and seek. |
| | **Decoy** | Leave a copy of yourself that walks away. |
| **World** | **Grab** | Pull and throw objects and artifacts at a distance. |
| | **Slow Field** | A bubble where time runs slow for everyone inside. |
| | **Rewrite** | Change a surface for a few seconds: bouncy, sticky, slippery, solid, invisible. |
| | **Spawn** | Place a simple platform, ramp or cube (limited count, fades). |

- **Combos are the fun:** Slow Field plus Grab, Mimic plus Phase, Rewrite plus Dash, Spawn plus Glide.
- **Mastery:** some exploits have upgrades (cooler, longer, wider) bought with credits.
- **Variety over power:** exploits sidestep each other. There is no strictly best one.

### How you get them

| Source | What it costs |
| --- | --- |
| **Starter** | Glide is free at awakening. |
| **Buy** | Credits (from the Wired) at an in-world exploit shop, gated by hacking level. |
| **Blueprint** | A dead server or operation gives a blueprint for a rare exploit. |
| **Teach** | A friend who owns one can teach you a copy (once, costs them a cooldown). Teaching is a social action. |
| **Event** | Live events and rare servers give exclusive ones. |

### How hardware fits

The rig already has RAM x2, CPU x1, Card x1. Proposed meaning for the Wired:

| Part | Effect on powers (proposed) | Already does |
| --- | --- | --- |
| **CPU** | More loadout slots | Wrong words removed in puzzles |
| **RAM** | Larger Heat capacity | Extra attempts in puzzles |
| **Card** | Cooler Heat, longer duration | Wrong words removed in puzzles |

This makes hardware matter in the **fun** part of the game, and the existing "buy at the store, install at the bench" loop becomes the way to grow your powers.

## Protocols (rules a place can bend)

A **protocol** is a rule a place can set. Places have 2 or 3, picked from a short list:

- Gravity (low, normal, high, none)
- Time scale (slow, normal)
- Bounce (floors bounce)
- Allowed exploits (all, none, only a set)
- Heat rules (cool fast, no heat limit, heat spikes)
- Visibility (fog, dark, mirror)

Examples: a **zero-g hall**, a **no-power court** where everyone is equal, a **heat-spike arena**, a **bouncy city**.

- **The plaza has a rule of the day**, so the same place feels different each day.
- **Players can set protocols on their own rooms** (see "Making"), which is the simplest form of creating a game space.
- Protocols are the cheapest way to get a lot of variety from a small number of exploits.

## Games (the plaza rounds)

A round starts in the plaza **every 5 minutes**, with a 30-second signup and a one-button join. Rounds are 3 to 5 minutes. They award **credits** (Wired activity pays credits).

| Round | Players | How it works |
| --- | --- | --- |
| **Virus** (tag) | 4 to 16 | One carrier infects others on touch. Last clean player wins. Exploits matter. |
| **Hide and Mimic** | 4 to 12 | A seeker looks for players disguised as props. |
| **Ring Run** | 2 to 16 | A race through rings with flight and rails. |
| **Capture the Packet** | 4 to 12, two teams | Carry a data cube to your goal, steal by touching, no damage. |
| **King of the Node** | 4 to 16 | Stay in a moving zone while others spike your Heat. |
| **Garble** and **Co-draw** | 3 to 8 | Party rounds from the toys doc. |

- **Rotation:** a different round each time, so the plaza never repeats. A simple vote or a schedule.
- **Loadout rules per round:** a max of 3 exploits in some, a "no Blink" in others, and a set of equal loadouts in a few. This keeps it fair for newcomers.
- **No injury, no death.** A hit is a glitch, a tag, or a respawn at the start of the round.
- **Players can run their own** rounds with protocols and a template (later).

## Hacking (the way to earn powers)

The Intrusion app and Operations already work as a **mental game**: word puzzles, heists, grades (Ghost, Clean, Noisy, Sloppy), alert, hacking XP and level. Keep it. In this loop its job is to **pay out the things the rest of the game uses**:

| Reward | Use |
| --- | --- |
| **Credits** | Buy exploits, upgrades, cosmetics (a currency sink the game lacks today) |
| **Blueprints** | Unlock rare exploits and special orders at the store |
| **Hacking XP and level** | Unlock harder servers and the stronger exploits |
| **Parts via reroute** | Hardware that makes the Wired better |

- **Co-op later:** in an Operation, one player is the **Operator** at the terminal, others are **Runners** who move through the space and open doors or disable cameras. The existing four stages (Entry, Cross, Exfil, Cover) become roles.
- **Spatial later:** put an operation's four stages in 3D places, so a heist is a place you move through, with powers helping the stealthy.
- **New servers regularly**, since rewards are first-clear only and the three run out. Needs a content pipeline.
- **Difficulty tracks powers:** the more you can do, the harder the best targets are.

## Hanging out

- **The plaza** is the main place, with the round clock, a rule of the day, a show and tell table, a radio, and an exploit shop.
- **Side rooms** for quiet: a reading room, a listening room, a lounge.
- **Reasons to stay:** rounds, a visible loadout and aura to talk about, things to throw and mark, other people's rooms to visit.
- **Crews:** 3 to 4 people who queue together, share a room and a color.
- **Showing off:** your hacking level and best grade show as a small badge; your aura shows your style.

## Making

Simplest to hardest, in the order to build:

1. **Marks and artifacts** (toys doc).
2. **Rule-bent rooms:** pick a room, set 2 or 3 protocols, place props. That is a game space.
3. **Rounds from templates:** tag, race or capture in your room with your rules.
4. **Pages as places** (main pitch).
5. **Shared builds** in a crew.

A room with a good rule and good props can be a hit. Visits are the reward.

## The real world: small and dull, but not empty

The real world stays small. Its jobs in the loop:

- **Workbench:** install parts and see them change your powers (existing).
- **Hardware Store:** buy and hack the staff panel (existing).
- **Packages:** from the store, from friends (existing, with delay).
- **Odd jobs for dollars (new, the missing source of dollars).** Short, slightly dull, satisfying tasks, 1 to 2 minutes each and capped per day (about 3): fix a neighbor's computer, sort mail, deliver a parcel, rewire a lamp. They pay dollars and feed the store. The dullness is the point, and the cap keeps it small.
- **Loadout bench:** where you choose your exploits before you jack in, with a satisfying physical flash of the chips into the deck.
- **A window and a TV** to make the room feel like a room you want to leave.

## Progression (what grows)

| What | Grows from | Unlocks |
| --- | --- | --- |
| **Character level** | Everything | Loadout slots and cosmetics |
| **Hacking level** | Puzzles | Harder servers, strong exploits, quieter play |
| **Exploits owned** | Credits, blueprints, teaching | More toys and combos |
| **Hardware** | Dollars and blueprints | Heat capacity, slots |
| **Rooms and places** | Credits | Your own space, with protocols |
| **Cosmetics and aura** | Credits, events | Identity |

No power creep that decides rounds. Exploits are sidegrades, rounds equalise loadouts when needed, and the ceiling is skill and creativity.

## The first ten minutes

| Min | What happens | Goal |
| --- | --- | --- |
| 0 to 2 | Wake in your room, boot the deck, jack in. | Atmosphere |
| 2 to 5 | Arrive in the plaza. **Glide is yours.** Fly. | The first "oh, nice" |
| 5 to 8 | A round starts. Join. Play Virus with strangers and Glide. | Social fun |
| 8 to 12 | Win some credits. See a terminal. **Hack a weak one** (the existing easy server). Get a blueprint or a new exploit unlock. | The loop closes once |
| 12 to 15 | Back home: install a part, see Heat capacity grow. Open a package. Jack back in. | The two worlds connect |

## Retention, without tricks

- **Variety:** a different round and rule of the day every 5 minutes and every day.
- **Mastery:** combos, ghost runs, harder targets.
- **Collection:** exploits, cosmetics, blueprints.
- **Social:** crews, friends in rounds, rooms to visit.
- **Creation:** your own space and rules, with visitors.
- **Clear targets:** a short list of "tonight's goals" on the deck.
- **No dark patterns:** no streak punishments, no daily login pressure, and a gentle, honest end to every session.

## Prototype order (each step answers "is it fun?")

| # | Build | Fun test |
| --- | --- | --- |
| 1 | **Flight toy:** Glide, Dash and Phase with Heat in the existing Wired scene | Do players fly around for 5 minutes without being told? |
| 2 | **Virus round** in the plaza for 4 to 8 players | Do they ask for another round? |
| 3 | **Three more exploits** (Blink, Grab, Mimic) and a loadout screen | Do players try combos and share them? |
| 4 | **Exploit shop** for credits and the hardware link to Heat and slots | Do players chase the next exploit? |
| 5 | **Protocols** on the plaza and one custom room | Do players make a room others visit? |
| 6 | **Odd jobs** for dollars at home | Is the loop between worlds smooth, and short? |
| 7 | **More rounds** (Hide and Mimic, Ring Run) | Which round do they play most? |
| 8 | **Co-op operations** | Do crews form? |

**Playtest rule:** 4 to 6 friends, 20 minutes, no instructions beyond controls. Watch what they do first, what they keep doing, and what they ignore. Change the game, not the story.

## Implementation notes (for later)

- Movement: the engine's `PlayerController` with `MoveMode*` components. An exploit adds a mode or an impulse. Check what the controller supports for flight and phase.
- **Host authority:** Heat, cooldowns, spawns and round results are decided on the host. Clients send intents. This also fits `AGENTS.md` (the seat and the host decide).
- **Persistence:** owned exploits, upgrades and cosmetics live in `ProgressRecord` through `PlayerProgress` on the host. Never rename an exploit id once saved.
- **Teaching exploits** must go through the host, with the teacher's consent and a cooldown.
- **Currencies:** exploit shop and rounds use credits only; odd jobs and hardware use dollars only (see `currency.md`).
- **Anti-grief:** a personal space bubble, a way to turn off other players' effects on you, and a mute for visual spam.

## Risks

- **Flying breaks level design.** Places must be built for it: vertical, open, with ceilings where needed.
- **Heat balance is hard.** Too low and nothing matters; too high and nobody flies.
- **Rounds need players.** A round every 5 minutes needs 4 to 6 in the plaza. With few players, bots should not stand in (stance), so allow solo modes (time trials, Ring Run for one) and show who else is online.
- **Cheating:** movement abilities need server checks.
- **Hacking is solo and silent** today, and the plaza is loud. Co-op and spatial heists solve it.
- **Real-world dullness** can slide into tedium. Keep jobs short and capped.

## Open questions

1. Is Glide-first the right hook, or should the first power be Dash or Phase?
2. How many loadout slots at start (proposed 3)?
3. How big is the Heat bar, and how fast does it cool (proposed 4 to 6 seconds of continuous Glide)?
4. Do exploits work in the real world? (Proposed no.)
5. Which rounds need teams, and which are free for all?
6. Should rounds be a fixed 5-minute clock, a queue, or a vote?
7. Are exploits permanent once bought, or do they need keys, charges or upkeep?
8. Does teaching an exploit make it weaker, or the same?
9. How does a solo player enjoy the plaza when it is empty?
10. What does the first co-op operation look like in 3D?
