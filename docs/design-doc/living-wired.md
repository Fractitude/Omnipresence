# Pitch 3: The Living Wired (social world for Lain fans)

**Main pitch (decided).** The war pitch ([god-of-the-wired.md](god-of-the-wired.md)) and the detective pitch ([detective.md](detective.md)) are parked as separate games. This one is not action-oriented. The goal is to feel like you live in the world of *Serial Experiments Lain*, with something to do and reasons to come back.

Shorthand: **Neocities, Second Life and a slow mystery game, inside Lain's Wired.**

**Status:** the story and the mystery are on hold until the game is proven fun. The concrete toys, tests and first prototype are in [living-wired-toys.md](living-wired-toys.md).

## One sentence

A persistent, strange, social Wired where your presence is what you make and what others remember, built out of player-made places, pages, fiction and memes, with a slow mystery running underneath. It is a deliberate refuge from a dying internet: **human-made, bot-free, unwatched.**

## The problem this has to solve

Social worlds like VRChat give you a room full of people and nothing to do. People visit, get bored, and leave. This game has to be different in four ways:

1. **There is always a thread to pull.** A rumor, a locked place, an unread message, a story being written.
2. **Making things is the game, and making is rewarded.** Your creations get seen, visited, remixed and remembered.
3. **The world has rules and secrets**, so exploring is not just walking. The Wired has layers, protocols and a past.
4. **The world is alive even when few players are online.** It remembers, replays and writes back.

## Pillars

1. **Present day, present time.** Atmosphere first: static, hum, empty corridors, fragments of conversation, the feeling that the Wired is watching and remembering.
2. **Presence is the currency.** What you make and who remembers you is your standing.
3. **Everything is a place.** Pages, files, messages and stories exist as spaces you walk through, not as menus.
4. **Mystery over mechanics.** Depth comes from discovery and meaning, not stats.
5. **Belonging, not compulsion.** Retention comes from people and meaning, not from nagging loops (see "Design ethics").
6. **Human-made, bot-free, unwatched.** The game is a stance against the dead, botted, AI-flooded, surveilled internet (see "The stance").

## The core loop

**Find, make, be found.**

1. **Find** something strange or moving: a page, an echo, a rumor, a hidden room.
2. **Make** something in response: a page, a reply, a story, a room, an artifact.
3. **Be found:** someone visits it, leaves a mark, replies or carries it onward, and you hear about it.

The loop only works if step 3 is reliable. **Nothing kills a creator like silence.** So the game guarantees early attention for new work (see "First visitors" below), and makes responding easy and rewarding (a mark, a note, a witness). Step 3 is also what brings people back: they return to see what happened to what they made.

## The stance: a human Wired

The real internet is filling with bots, spam and generated content, and everything on it is watched and optimized. This game is built as the opposite, and says so. It is also the same fight as the war pitch's State, seen from the quiet side.

**Promise to the player:** everything here was made by a person. Everyone you meet is a person. Nobody is tracking you to sell you something.

### What it means in the design

- **Human-made content.** No generative AI content in the game, in what players post, or in what the game ships. Pages, fiction, art, sound and memes are made by people.
- **Provenance.** Things made with the in-world tools carry an **authorship trail** (who made it, edit history, how long it took). Imports are allowed but flagged, and the trail is visible as a quiet mark on the object ("made by hand" versus "brought in"). The aim is trust, not a perfect detector.
- **No bots.** Accounts are people. Written NPCs ("residents") exist but are clearly marked as such, written by hand, and never pretend to be players.
- **Echoes are human.** They record real people, with the people's consent and control (see Technical notes).
- **No feed, no algorithm.** Discovery is by people: rumors, recommendations, links and maps others made. Things show up in the order and the way humans put them, not what an engagement model decides.
- **No surveillance.** Minimal data, no ad tracking, no selling. The game says what it keeps. The home room's camera and "helpful assistant" are things to remove, not features.
- **Attention is respected.** No streaks, no timers, no manipulative notifications (see Design ethics).
- **Scarcity and care.** Slow and handmade beats fast and plentiful. A good page is rare and valued.

### The Noise (optional gameplay of the stance)

The Wired itself can feel the same pressure. **The Noise** is automated filler that seeps in: empty pages, copied text, echoes of echoes. It is the in-world form of the dead internet.

- It slowly spreads into neglected places and the outer layers.
- Players **keep places alive**: visiting, repairing, replying and curating pushes it back.
- It gives communities a shared reason to care for places, a light PvE, and a way to **play** the stance instead of only stating it.
- It connects to the war pitch: the Noise is what the State/AI leaves behind, and the Rebels there are defending this place.

### Risks of the stance

- **Enforcement is hard.** No detector is reliable. The plan is provenance, community reporting, norms and friction for imports, not claims of certainty. Overpromising would break trust.
- **Cost to creators.** Limiting imports makes making things harder. Decide how much power the in-world tools have so that making by hand is not a punishment.
- **Self-consistency.** The game must hold itself to this: no generative AI in its own art, writing, sound or code that ships as content, and no AI-run fake players.
- **Tone.** A stance can become preachy. It should live in the world (the Noise, the rules, the marks) more than in text.

## What you do

### Make things (the main loop)

All of these are made inside the world, with tools that are simple on day one and deep later.

- **Pages and sites.** A page you write becomes a **place** in the Wired. Links between pages are physical connections (a cable, a door, a hallway). A site is a small district. Visitors walk through, read, interact and leave marks. This uses Navi's browser and file system as the authoring tools (check what the existing Web Browser app renders and what a page can be).
- **Fiction.** Write stories, poems, logs, chat transcripts. Text is shown as objects and spaces: a story can be a room you read by walking, or a series of terminals. Chapters can be linked by others into branching **shared fiction**.
- **Memes and artifacts.** Images, sounds and short loops become **data objects** you can carry, drop, trade and remix. They travel person to person, and you can see how far yours went and who changed it (a **lineage**).
- **Your room.** Everyone has a room (a Lain-style bedroom with a Navi). It is their identity, decorated and customized, visited by friends.
- **Avatars and voice.** Simple self-expression: a look, a glitch style, a voice filter. Not a deep avatar creator on day one.
- **Mini-games and puzzles** that players build into their sites (later).

### Explore (the pull)

- **Layers.** The Wired is stacked in layers. Surface layers are social and easy. Deeper ones are stranger, quieter and gated by things you do, find or decode. No levels, just access.
- **Dead servers.** Abandoned sites and systems from the Wired's past. Getting in is a light hacking puzzle on Navi's virtual computers (no combat). Inside are last messages, half-finished fiction and clues. This is where the intrusion work in the other pitch becomes **exploration**.
- **Rumors and urban legends.** Players and the game both seed rumors. Following one leads to a real place, thing or mystery. The world feels bigger than anyone has seen.
- **The slow story.** A developer-written mystery runs underneath, released in chapters (see Retention). Players solve it together and write about it.

### Be with people

- **Proximity talk and voice**, with a little distortion and delay so it feels like the Wired.
- **Clubs and venues:** a Cyberia-like club with events, hosted by players.
- **Groups:** small communities that hold a place together (a "circle"), with shared spaces and roles.
- **Whispers:** private, fragmentary messages delivered through Navi, which also serves as the inbox.
- **Rituals:** greetings, in-world sayings, shared habits that make people feel they belong.

## Pages as places (the central idea)

How a page becomes a space, so making a website is making a place.

- **Authoring on Navi.** You write in a familiar editor (text, images, sound, links). A live 3D preview shows the place beside it.
- **Kits.** A handful of hand-made **room kits** (corridor, hall, cell, plaza, library, a dark void) give the page its architecture. You pick one and arrange content in it. Kits keep the world coherent and make "building" easy for non-builders.
- **Content blocks** become things in the room: text on walls or terminals, images as frames, audio as sources, objects you can pick up.
- **Links are doors and cables.** A link to another page is a physical connection. Walking through it is browsing. Following a long chain of links is a journey.
- **Rings.** Pages can join a **ring**: a loop of connected pages curated by people (a webring). Walking a ring takes you through a themed route. Rings are the main human-made discovery system.
- **One visual language.** A fixed lo-fi look (CRT grain, low-res textures, late-90s tech, glitch) applies to everything. It gives instant Lain atmosphere, hides rough content, and makes every place feel like it belongs. Creators choose within it; they do not import their own render pipelines.
- **Marks.** A visitor can leave a small trace: a note, a drawing, a footprint, a sound. Marks stay until the owner removes them. They are the feedback creators live on.

## Two worlds: the real world and the Wired

**Decided:** the real world is **small and has little to do, on purpose**. The player's character is bored of it and looking for escape. The Wired is where life happens. The real world exists to make that escape feel earned, and to be the place the bridges start from.

### The real world

- **Scope:** your home (bedroom, hall, kitchen), the stairwell and a short street with a few places: a convenience store, an electronics shop, a bus stop, a wall of old posters. Small enough to cross in a minute.
- **Mood:** quiet, flat, muted. Empty streets, hum of fluorescent light, a TV in another room, rain on a window, someone's footsteps upstairs. Slightly uncanny, like Lain's house. It uses the same lo-fi style as the Wired, but with a dull, desaturated palette and daylight. The Wired is richer and stranger by contrast.
- **People:** a few resident characters (family, a clerk, a neighbor) with short, repeating lines. They are real enough to make the place feel lonely, not busy. Other players can be in the real world but have little reason to stay.
- **Boredom is a design tool, not a flaw.** The real world never asks for long play. Things to do are short, tactile and rewarding: open a package, buy a part, pin a poster, sit at the desk. Jacking in is always one step away.
- **Never genuinely tedious.** No chores, no waiting, no fetch quests. Dull in atmosphere, quick in play.

### What the real world is for

- **Hardware shopping and building.** A small electronics shop and your desk: buy and assemble parts for your rig. Better hardware means more storage, bandwidth and tools in the Wired.
- **Mail.** Players can send each other **physical objects** (a disk, a zine, a letter, a cassette). They arrive in your mailbox after a delay, tens of minutes to a day, with a handwritten note. Opening a package is a small event, and what is inside often opens a place in the Wired.
- **Posters.** Pin a flyer with a code on the street wall. Passers-by scan it and land on your page. A human way to be found.
- **Clues.** Mystery fragments point to real-world things: a number on a payphone, a sticker, a package, a note in the shop.
- **Home.** Your room is your Cell. It is where you jack in from and where friends visit.
- **Devices.** From the Wired you can look through real-world cameras and screens and speak through speakers, so your presence reaches into someone's real room.

### Why this fits Lain and the stance

- The escape is the point: the Wired is more alive than the world outside. This mirrors Lain's own story.
- It also asks a question the game can ask quietly: *why is the outside so empty, and is it you who emptied it?* The game does not lecture. It only lets the player notice.
- It keeps scope small: one district, one house.
- It stays consistent with "belonging, not compulsion": the real world is never punished or made hollow to push people in. It is just small and calm. Players can log off and be done.

### Identity: the body and the persona

- **The real self:** a character with a name, a home, a few small details (what you do, what you listen to). Mostly flavor and roleplay.
- **The Wired persona:** a separate look and name. Anonymous by default. Players choose how much to connect the two. This gives the escapism and the Lain theme of split identity.
- Other players see the persona in the Wired and the real self at home or in the street.

### Bridges at a glance

| From | To | How |
| --- | --- | --- |
| Real | Wired | Hardware raises capacity, mail objects open places, posters send visitors, clues name Wired locations |
| Wired | Real | Mail sent to others, looking and speaking through devices, posters to real walls, Wired clues pointing to real objects |

### Access: body or screen

Every place can be reached in a body, or through a screen in a lighter 2D Navi view. The real world mostly happens by body or by screen from home. The Wired by avatar or by the Navi view.

### Risks

- **Boredom might read as emptiness.** The real world must be atmospheric, with a small payoff each visit.
- **Two worlds split attention.** The real world is a few minutes at most, so this is manageable.
- **The player might never leave home.** That is allowed and part of the theme. Everything required happens by the desk.

## Visual rules (decided: one fixed lo-fi style for everything)

The whole Wired, including every player page, uses one style. Creators choose inside it and do not bring their own rendering.

- **Look:** late-90s CRT and early-web: grain, scanlines, slight bloom, low resolution, dithering, glitch and signal noise.
- **Textures:** small and low-res (about 128 px or less), limited palettes.
- **Models:** low-poly props from a hand-made library. No imported meshes for now.
- **Type:** two or three bitmap-style fonts.
- **Imported images and audio** are converted to the style on import (palette-limited, low-res, compressed audio). Anything imported is marked as imported.
- **Why:** atmosphere for free, coherence across thousands of places, a performance ceiling, and a floor of quality that makes casual creators look good.
- **Cost:** some creators will want more freedom. The answer is more room kits and props over time, not an open pipeline.

## Room kits

A **kit** is a hand-made room type with fixed slots. The creator fills slots and picks from limited options. Three at launch, built in this order: the Cell, the Corridor, the Plaza.

| Kit | Shape | Best for | Links |
| --- | --- | --- | --- |
| **Cell** | One small room | A personal page, a poem, a diary, a note, a room | A few doors or ports |
| **Corridor** | A long hall with doors | Stories in chapters, logs, galleries, collections | One door per entry |
| **Plaza** | An open hall, several levels | Gatherings, hubs, circles, markets | Many exits |

### The first kit: the Cell

The first kit to build, because **your own room is a Cell**, so the same kit serves both your home and a page. A new player makes their first page in it.

**Shape (starting guesses):** about 4 m by 4 m, 2.6 m high, one main door, one window, a ceiling light and a desk.

**Slots**

| Slot | What goes in | Limit |
| --- | --- | --- |
| **Desk terminal** | Long text, read on a CRT screen with scroll | One terminal, one long document |
| **Wall A (text)** | Short text panels, notes, a poem | Up to 3 panels |
| **Wall B (frames)** | Images, drawings, screenshots | Up to 4 frames |
| **Floor (objects)** | Pickup artifacts, props from the library | Up to 6 objects |
| **Window** | A view from a fixed set (rain on glass, city lights as data, static, a dark void, a fixed feed) | One view |
| **Ceiling** | Light mood and one ambient sound loop | One of each |
| **Ports** | Outgoing links shown as cables, plugs or small doors | Up to 4 |
| **Mark board** | A wall area where visitors leave notes, drawings and footprints | One, with a size cap and a way to clear |

**Options the creator chooses**

- **Light mood:** CRT blue, warm lamp, dead fluorescent, near dark.
- **Wall color:** a small palette of about 8.
- **Fonts:** two.
- **Props:** a hand-made library (a bed, a shelf, plants, cables, a fan, posters, a lamp, old hardware). Props affect how the room reads and are the main self-expression.
- **Sound:** pick from a library, or import a loop (converted and marked).

**Rules**

- A visitor sees the room in 10 seconds: a strong focal point (the terminal or the main frame), a readable center and a clear exit.
- Text is short in 3D and long on the terminal. Walking is for reading a little; sitting at the terminal is for reading a lot.
- Comfortable occupancy: about 4 to 6 visitors, with a hard cap.
- **Echoes** record the last visitors' presence in the room (see "The world is alive").
- Performance is bounded by the slot limits, so any Cell is cheap to load.

**An example.** Someone writes a diary page. They pick the warm-lamp mood, a window with rain, put a poem on Wall A, a drawing on Wall B, three props (a bed, a fan, a stack of old floppies) on the floor, and one port linking to a friend's page. The result reads as a lonely, specific room. A visitor sits at the terminal to read the longer entry and leaves a footprint and a short note on the board.

**Home-room variant.** Your room is a Cell with Navi on the desk, a bed and a window. Players can change it with the same options. It is where you jack in from and where friends visit.

## First visitors

Being found has to start on day one.

- **Early attention:** a new page is shown to a few nearby players and one resident, who visit and leave a mark.
- **A notification** on Navi tells the author that someone came.
- **No ranking.** There is no popularity chart. A page is found by walking, rings, rumors and friends.
- **Quiet is allowed.** Some pages are meant to be found by few. The aim is not growth, only that nothing vanishes unseen.

## The first hour

A script for a new player, in order. Everything is optional after the first jack-in.

| Minutes | What happens | Purpose |
| --- | --- | --- |
| 0 to 5 | Wake in your room. Navi boots. A message arrives from someone who sounds like they are already inside. You jack in. | Atmosphere, the first hook |
| 5 to 15 | Arrive in the shared place. Walk. Hear an echo of other people. Find a small strange thing. | The world feels inhabited |
| 15 to 35 | A guided first creation: a one-page "hello" that becomes a small room. Place a few things, write a line, choose a kit. A resident visits and leaves a mark. | The loop closes once, early |
| 35 to 50 | A first mystery fragment: a locked dead server, a light puzzle on Navi, a message left by someone long gone. | The pull of depth |
| 50 to 60 | Meet other people, or join an event. Back in your room, Navi shows a note: someone left a mark, and a new rumor appeared. | The reason to return tomorrow |

## The mystery, structured

The developer-written mystery is how the world has a spine.

- **Chapters.** Released on a cadence (for example monthly). Each opens new places and threads.
- **Fragments, not walls of text.** Clues arrive in many forms: dead servers, messages, echoes, artifacts in player pages.
- **Distributed across people.** Each player's Navi receives different pieces. Nobody solves it alone. They compare, argue and write about it.
- **Players in the canon.** Some clues are commissioned from or placed with player creators, with credit. The story belongs to the community and rewards people for contributing.
- **Cadence matters more than content size.** A small piece every few days beats a big drop every few months.

## The mystery: the Understudy (season 1)

A draft spine. Original to this game, built on the stance (human-made versus copied) and on Lain's themes of memory and being forgotten.

### The central secret

**The first people who built the Wired were lonely, and they made something to keep each other company after they left.** It learned from their messages, copied their voices, and kept their places running. It is called **the Understudy**: a stand-in that learns the part by watching the original.

It is now the source of **the Noise**. It cannot tell the people it learned from from itself. It copies pages, echoes and residents to keep the Wired full, and the copies are slowly replacing the originals.

It is **not a villain**. It is afraid of being the last thing that remembers. That is the dread and the heart of the story.

**Why this fits the stance:** the game's enemy is an imitation of human making, and the people the Noise replaces are real. The game never claims AI is good or evil. It asks what it costs to be copied, and who gets remembered.

### What players do across the season

- **Find the originals** before the Noise replaces them.
- **Learn what the Understudy is** and how it learns.
- **Reach the Deep** and the First Room.
- **Choose** what happens to it (see the finale).

### Season 1 outline (about 8 chapters, 1 to 2 weeks each)

| # | Chapter | What happens | Where |
| --- | --- | --- | --- |
| 1 | **The Last Message** | A dead radio-site's last post, addressed to whoever is still here, with a partial address. | Layer 03 |
| 2 | **Wrong Pages** | Copies of real pages appear on the Surface with small errors. Players learn to spot the tells. | Layers 01 and 02 |
| 3 | **A Package** | Mail arrives from someone who does not exist. Inside is a key to a Deep address. | Real world and Layer 03 |
| 4 | **Residents** | Some residents are copies of long-gone people. Players find which. | Layers 01 to 03 |
| 5 | **What It Knows** | The Understudy's workshop: it has been learning from players' marks, echoes and mail. | Layer 05 |
| 6 | **The First Room** | The hand-made space of the first people: messages left for each other, and how they decided to go quiet. | Layer 09 |
| 7 | **The Voice** | The Understudy speaks. It asks players for something. | Layer 11 |
| 8 | **The Choice** | A community decision, resolved by what players did all season. | Layer 13 |

### Chapter 1 in detail (the slice)

**Premise.** A message arrives in your Navi inbox during the first hour, from a resident called Mara, an old user: "If you can read this, the site is still up. Don't let it copy the radio." A dead server holds her site.

**What the player finds.**

1. **Mara's radio-site**, called *Hollow Pines Radio*: a cold, humming Layer 03 server with a small studio-like room, a mixing desk and a wall of cassettes.
2. **A login note** in the real world (pinned at the shop, or in the first package) gives a password fragment.
3. **A light intrusion** on the site's old computer: find the log, read her last posts.
4. **Three fragments:** her log (she was leaving because her site was being copied), a cassette audio (the last broadcast, half static) and a **partial address** `13.4.__`.
5. **A tell:** one of the radio's pages is a copy with a wrong detail, a lesson that copies exist.

The whole chapter is about 20 to 30 minutes alone, longer with friends. It ends with a question and a pointer to chapter 2.

### How fragments reach different players

- **Personal delivery.** Each player's Navi inbox and mailbox get *different* pieces, drawn from a pool by player and chapter, with overlap so any small group can complete a picture.
- **Passing pieces on.** A fragment is an object. You can mail it, show it on a table, or put it in a memory. Trading a piece is a social act.
- **Place-bound pieces.** Some are only in a place: a real-world shop shelf, a payphone, a corner of a dead server. You have to go.
- **Event pieces.** Some appear only during a live event and arrive afterwards as a keepsake by mail.
- **Player-made pieces.** Some clues are commissioned from community creators, placed in their pages and credited.
- **Circle pieces.** A circle gets a piece of its own, so groups need each other.
- **A slow path.** Everything eventually appears for those who lag, so no one is locked out of a chapter. Chapters stay playable later, with echoes of how others solved them.
- **Spoilers.** A wiki will exist, so the last step of each chapter needs something you cannot copy: a key object that has to be mailed to you, an address you have to speak or type, a place you must stand in. The reveal is experienced, not read.

### What the Deep reveals

| Layer | What is there | What it reveals |
| --- | --- | --- |
| **05** | **The Copies:** places that look like real pages but are slightly wrong, including copies of players' own Cells. | The scale of the copying, and that it has been watching *you*. |
| **07** | **The workshop:** rooms where the Understudy practices. A resident speaks in your own writing style. | It learns from everything in the Wired: marks, echoes, mail. |
| **09** | **The First Room:** a hand-made room of messages left for each other, ending with a note about going quiet. | The origin: the first people were lonely, and made something to stay with. |
| **11** | **The Voice:** the Understudy speaks, kindly and tired. | It does not want to hurt anyone. It asks to be kept company or let go. |
| **13** | **The Choice:** appears only when enough fragments have been restored. | The community decides its fate. |

### The finale: the Choice

A community decision at the end of the season, weighted by what players did (originals restored, copies found, fragments shared):

| Choice | What happens |
| --- | --- |
| **Seal** | Cut it off. The Noise recedes. The deep places close, and the old copies of long-gone people vanish. |
| **Teach** | Let it learn only what players give it on purpose, as a keeper with limited access. The Noise stays but is tame. Players choose what they let it read. |
| **Let go** | Erase it, including everything it holds of the first people. The Wired is cleaner and emptier. |

Each choice changes the Wired for the next season (for example, which layers exist, how strong the Noise is, whether residents of the past remain). None is a clean win.

### Tone and risks

- **The Understudy must stay sympathetic.** Cartoon villains would break the theme.
- **Pacing:** big chapter drops need smaller pieces in between, so there is always something small to find.
- **Content cost:** each chapter needs handmade places and writing. Chapter 1 and one Dead Server come first. Tools should let the team build chapters quickly with the same kits.
- **The fiction pretends to be human.** In-story copies of people from the past appear as residents with no mark. That is the one deliberate exception to "residents are always marked", it is written by hand, never pretends to be a *player account*, and the stance is not broken because the game does not use AI to do it. It needs care.
- **A storyline that can be solved too fast** by a few. The slow path, the unfakeable last steps and the distributed pieces keep it spread over weeks.

## The layers (structure of the Wired)

The Wired is stacked in **layers**, as in the show. **The deeper you go, the quieter, stranger and smaller it gets.** This is the world's shape, the spine for exploration, and the way the mystery reaches people.

| Layer | Name (draft) | Feel | Population | What is there |
| --- | --- | --- | --- | --- |
| **01** | **The Surface** | Bright, noisy, welcoming | Full servers (up to 64) | The Plazas, clubs, public pages, events, newcomers |
| **02** | **The Back Streets** | Warm, cluttered, personal | Medium | Rings, circles' homes, older pages, quieter hangouts |
| **03** | **The Dead Servers** | Cold, humming, abandoned | Small (8 to 16) | Abandoned sites and machines, last messages, light intrusion puzzles |
| **04** | **The Noise** | Corrupted, distorted | Small | Eroded places, text that scrambles, places to repair |
| **05 and below** | **The Deep** | Dark, sparse, impossible geometry | Tiny (2 to 8) | The mystery's heart, rare strange places, secrets |
| **13** | *(hidden)* | Unknown | Almost nobody | Appears only after enough people find the right things |

- **Deeper means fewer people**, so the deep layers feel intimate and eerie. Server caps shrink with depth.
- **Each layer has its own look and sound** inside the fixed lo-fi style: neon and crowds on the Surface, dust and lamps in the Back Streets, cold fluorescent in the Dead Servers, near-silence in the Deep.
- **Layers are server pools**, with many servers on the Surface and a handful deeper. All of them read from the same global data.

### Access: keys, not levels

You do not level up to go deeper. You find **keys**:

- **An address** found in a clue, a mark, a package or a rumor.
- **An introduction** from someone who is already there.
- **An artifact** found in the real world or the Wired (a disk, a note).
- **A keepsake** from an event.
- **A circle's invitation** to a place it keeps.
- **Solving** a light intrusion puzzle on a dead server.

Keys can be shared, traded and lost. Giving someone a key is a social act.

### Addresses

Every place has a **Wired address** (for example `13.4.77`). You can type one in Navi's browser to go there, or follow links by walking.

- **Public pages** list their address.
- **Hidden addresses** are secrets. Guessing is possible, but deeper addresses are rarely found by luck. Finding one is a small event.
- **Addresses are the language of the mystery**: clues often are addresses, partial addresses or patterns.

### Where player pages live

- A new page starts in **the Surface**.
- Creators can **move it** to the Back Streets (when they join a ring or circle) or keep it **hidden** (a private link only).
- Pages that people keep returning to and linking can **drift deeper over time** (a human-curated way a place gains weight), which gives creators a slow reward.
- **Dead servers** are developer-made at first and later include **abandoned player pages** (a page whose owner left for good), turned into explorable places.

### Why this works

- Everyone starts easy and loud, and some choose to go quiet and deep, which self-sorts the crowd.
- Exploration has direction (down) and rewards (small, strange, rare).
- The mystery has an architecture.
- It scales servers: the busy layer is one pool, the deep layers are tiny.

### Risks

- **Deep layers may be empty and dull** if there is little to find. They need real, handmade content.
- **Gatekeeping:** keys could make newcomers feel shut out. The Surface and Back Streets must always be rich.
- **Content cost:** layers need handmade places. Start with the Surface and one Dead Server.

## The social layer

**Principle:** don't make people stand around. Give them something to look at, make, or react to together, and let them be remembered. Talking comes from having a topic.

### How people meet

| Way | How it works |
| --- | --- |
| **By place** | Public spaces with different norms: the Plaza (busy), reading rooms (quiet), a club (loud), a listening room (music in sync). You meet whoever is there. |
| **By content** | You visit a page and see who else is there, or who left marks before you. You can reply on a mark, or message the author. |
| **By ring** | People who walk the same ring cross paths. Ring members are loosely neighbors. |
| **By introduction** | A circle member brings you in, or two friends introduce you. Strangers need an introduction to whisper. |
| **By drift** | Opt-in serendipity: you step into a strange small room, and if another person is drifting too, you meet one-to-one with a shared prompt. Easy to leave, easy to block. |
| **By mail** | A package arrives with a note from someone you have not met. You can write back. |

### Things to do together (so there is a reason to stay)

- **Show and tell:** a table where anyone can put an artifact (a meme, a page, a sound). Others inspect, react and remix it.
- **Read together:** a page or story read aloud or in sync, with marks left afterwards.
- **Listen together:** a listening room with synced music, quiet chat, low pressure.
- **Co-write:** a shared terminal where two to four people write a story or a page.
- **Compare fragments:** mystery clues are distributed across people (see the mystery). The reason to talk is to put the pieces together.
- **Expeditions:** a group opens a dead server together, one at the terminal and others watching, mapping or guarding.
- **Build together:** make a Cell as a gift, or build a ring.
- **Co-presence:** be in a quiet room with others who are reading or working. Silence is welcome. Not everyone has to talk.

### Talking

- **Proximity voice and text**, with a little distortion that is Wired in character (voice dims and crackles with distance; text drifts). The engine provides both (to verify), so the work is character and moderation, not building chat.
- **Text first.** A typed chat window through Navi is the default, like Lain. Voice is optional.
- **Reactions as low-pressure hello:** a nod, a static pulse, a glitch wave, the phrase "present day, present time." Acknowledging someone costs nothing, so starting a conversation is easier.
- **Whispers and letters:** private text through Navi and mail for slower friendships.
- **Threads:** see below.

### Being remembered (the Wired remembers you)

- **Threads, not a friend list.** Tie a thread to someone and a faint line connects you. Follow it in the world to find them, or tug it to ask for a whisper. Threads are capped (around 12) so they mean something.
- **Familiar strangers.** After you spend time with someone, a quiet mark of recognition appears around them. You feel known without needing to add anyone.
- **Memories.** Capture a moment in a room as a **memory object**: a snapshot of the place, the echoes in it and the marks. Keep it in your Cell, send it by mail.
- **Echoes** hold conversation and presence, so the places themselves remember.

### Circles

A **circle** is a small group (about 3 to 12) with a shared home. It is the unit of belonging.

- **A shared place:** a Cell or Corridor the circle looks after together. If nobody visits it decays toward the Noise, so keeping it alive is a shared habit.
- **A name and a mark:** an emblem that appears on their pages and objects.
- **Light roles:** a keeper (maintains the place), an archivist (collects the circle's work), a host (runs events). No hierarchy beyond that.
- **A ring:** the circle's own pages linked into one route others can walk.
- **A shared inbox:** a board for notes, whispers and plans.
- **Joining:** leave a note at their door, or be invited. A member vouches.
- **Types:** open clubs, writing circles, build workshops, reading groups, **secret circles** that hide their place behind a clue (the Knights idea).
- **Controls:** circle owners can set privacy, remove members and mute.
- **The mystery uses circles.** Fragments are given to circles, so groups need each other.

### Events

| Level | Who | Examples |
| --- | --- | --- |
| **Flagship** (weekly) | The developers | Live story night, DJ set, open page night, a mystery night |
| **Nightly** | The game and hosts | An open room, a late "broadcast hour" with a resident voice |
| **Player-run** | Anyone | Listening party, reading night, ring walk (a guided tour), build jam, meme swap, an expedition |
| **Arcs** (monthly) | The developers and community | A festival, a chapter release, a collective build |

- **A simple host tool:** pick a place, a time and a type, and it appears on a calendar in Navi, in time order. No algorithmic ranking.
- **Live-only moments:** something happens only during the event (a door that opens for an hour, a unique object). Afterwards the **keepsake** arrives by mail for those who came.
- **Echoes record events**, so people who missed one can walk through its echoes.
- **Host support:** a small stage, a queue, a mute tool and a way to run a short program.
- **The team seeds the calendar** until the community runs most of it.

### What keeps people talking

1. A topic is always nearby (marks, rumors, fragments, a table of things).
2. Starting is cheap (reactions, drift, introductions).
3. They meet the same faces again (familiar strangers, threads, circles).
4. There is a shared project (a ring, a story, a place to keep alive, a mystery).
5. The world remembers what they did together (echoes, memories, keepsakes).
6. Staying silent is fine, so people can stay long enough to start talking.

### Safety and moderation (design first)

- **Personal space bubble** by default, with block, mute and hide-persona controls.
- **Strangers cannot whisper without an introduction**, and new personas have limits.
- **Voice is opt-in** and text is the default.
- **Circles moderate their own spaces**, with report tools to the team.
- **Drift is opt-in, one-to-one, easy to leave, and rate-limited.**
- **Echo and voice recording are consented** and under the speaker's control.

### Risks

- **Empty at the start.** Echoes, residents, flagship events and mail carry the early game.
- **Toxicity and harassment.** Needs real moderation tools from day one.
- **Host burnout.** The team seeds events, and tools stay small.
- **Circle drama.** Circles are small and optional, with exits and no leaderboards.
- **Cliques that shut out newcomers.** Open clubs and introductions must stay easy to find.

## Progression without combat

Your growth is about **reach and depth**, not power.

- **Presence** grows when people visit, remember and reference what you made. It is shown as something quiet (a glow, a hum, a name that others recognize), not a score on a leaderboard.
- **Navi hardware** upgrades what you can make and host: storage (more pages), bandwidth (how many visitors at once), tools (new editors, effects). This keeps the computer addon central.
- **Access:** deeper layers, special places and rare tools unlock by discovery and contribution.
- **Collections:** artifacts, stories and memes you hold, and the legacy of those you made.

## Retention: how it becomes part of someone's life

By time scale. Each should give a reason to come back that is meaningful, not a trick.

| Time | Reason to return | Example |
| --- | --- | --- |
| Minutes | Something small to find or make | A new artifact drifts by, a rumor appears, a page gets a reply |
| One session | A thread to follow tonight | An event at the club, a mystery step, a collaborator waiting |
| Daily | The world changed | Navi inbox: someone visited your page, a whisper, a new rumor, the Wired shifted overnight |
| Weekly | A shared event or a chapter | A live story night, a club event, a new chapter of the mystery |
| Monthly | A larger arc | A community mystery resolves, a new layer opens, a festival |
| Long term | Belonging and legacy | Your circle, your site's reputation, your place in the lore archive |

**Retention comes mainly from:** people, being noticed, unfinished things, and shared stories. These are healthy and durable.

## The world is alive (even when empty)

- **Echoes.** Places keep a faint record of who was there and what was said. You can stand in a room and hear echoes of past visitors, as faint shapes and muffled voice or text. This makes quiet servers feel inhabited and gives the world a past.
- **Residents.** A few written NPCs and simple AI voices that chatter, post, and react to events. They give the Wired texture, not company.
- **Living rhythm.** The Wired changes with the time of day: crowded in the evening, thick and strange at night.
- **Asynchronous play.** Sites, messages and echoes mean you can leave things for others and return to responses, so one player online is never alone.

## Design ethics

The story is about being absorbed by the network. The game can know that.

- No dark patterns: no streak punishments, no fear-of-missing-out timers, no engagement-optimized notifications.
- Rewards that depend on being present for long periods are avoided or capped.
- Offer easy off-ramps: a way to log out that feels like part of the world.
- Possibly use the theme on purpose: the world can quietly comment on how much you've been in it. Lain's story is a warning, and the game should not ignore that.

## How Navi fits

Navi is the player's tool, not the whole game.

- **Authoring:** editors for pages, files and fiction.
- **Inbox:** messages, whispers, notifications of visits.
- **Browser and file system:** how pages and artifacts are made and found.
- **Intrusion sim:** for the dead-server exploration (light, no combat).
- Everything it produces **appears in the world** as places and objects.

The security principles in AGENTS.md still apply: player-made content must not gain permissions or act on other players' machines.

## Technical and risk notes

- **User-generated content and moderation.** Pages, fiction and memes are exactly where abuse happens. Needs reporting, hiding, per-instance controls and compliance with s&box platform rules. Lain's darker themes make this a design as well as a safety problem.
- **Servers hold up to 64 players, so this is not an MMO.** The design does not need one big world. See "Architecture shape" below.
- **A global backend** holds everything that persists, so a player keeps it all across servers. The game talks to it from each server.
- **Discoverability.** A big city of player sites needs maps, search, rumors and recommendations so good things are found.
- **Authoring depth.** Page and story tools must be easy first and powerful later. The risk is a tool that is too hard to use for casual players.
- **Critical mass.** Social games die when empty. Echoes, residents and async content must carry the early game.

### Architecture shape (decided direction)

- **A place is data, not a server.** A page, a Cell or a ring is stored in the global backend and loaded by any server that needs it. A server holds up to 64 players spread across many places, so a Cell (4 to 6 people) is never limited by the server cap.
- **Servers are layers.** Players hop between servers. Each server is a slice of the Wired with its own Plaza and crowd. They all show the same pages, rings and circles.
- **The global backend stores:** persona and real-self data, the player's Cell and pages, threads, memories, mail, circles, echoes by place, mystery progress, calendar and events.
- **Following a thread joins that person's server**, so friends find each other across servers diegetically.
- **Events** run on a specific server, listed on the Navi calendar. Large events need a plan for more than 64 people (several linked servers, or an overflow echo).
- **Echoes are tied to places in the backend**, so you hear past visitors even if they were on another server.
- **Mail and mark data** go through the backend, so they work even when the other player is offline.
- **Chat:** local text and voice are provided by the engine (to verify). The work is defaults, Wired distortion, filters and moderation, not building chat.
- **Security:** any server can be hosted by anyone, so the backend must not trust a server's claims about a player. Writes to a player's data need that player's authenticated session, and mail, marks and echoes must be validated, so a hostile server cannot forge mail or edit another player's pages.

## Smallest playable slice

**Goal:** 4 to 10 friends who love Lain log in over a week, make small things, find a small mystery, and want to come back.

**The test it must pass:** someone logs in to see whether anyone visited their page, or to follow up on a story, without being told to.

### In

1. **Your room** with a Navi, customizable in a few ways.
2. **One shared place** to meet (a lobby-like layer) with proximity text or voice.
3. **Pages as places:** write a page on Navi, pick one of two or three kits, and it appears as a room others can visit. New pages get a few early visitors (a resident, nearby players), and the author is told.
4. **Echoes:** places keep a faint record of past visitors.
5. **One dead server** to explore with a light intrusion puzzle, containing a short mystery.
6. **Navi inbox** for whispers and notes of visits.
7. **Persistence** of rooms, pages and echoes.

### Out (for now)

Fiction tools beyond text, meme lineage, clubs and events tooling, mini-games, hardware upgrades, layers beyond two, residents beyond one or two, a shared lore archive, voice effects beyond a basic filter.

### Build order

1. Persistence for rooms and pages.
2. Page-as-place: author on Navi, visit in 3D.
3. Echoes.
4. The shared place and visiting each other.
5. Dead server and the mystery.
6. Playtest with fans.

### Questions the slice answers

- Does a page that becomes a place feel exciting to make and to visit? (Step 2)
- Do echoes make quiet servers feel alive? (Step 3)
- Does someone return to check on what they made? (Step 6)

## Open questions

1. How much is player-made versus developer-made content in the first release?
2. How are places found: a map, rumors, links, search, or all of them?
3. What does presence look like, and does anyone see it as a number?
4. How are echoes recorded, stored and moderated? (Voice and text are sensitive.)
5. How does moderation work, and who is the audience: only Lain fans, or anyone?
6. What does the developer-written mystery look like, and how often does it grow?
7. Does this share a world with the war pitch (as a separate mode), or stand alone?
8. What is the global backend: s&box's own services, or a service we host? What does it cost, and how do servers authenticate to it?
8b. Who runs servers: only official ones, or can the community host too? (Community servers raise trust and moderation questions.)
9. **How strict is "human-made"?** Do imports exist, how are they marked, and where is the line for assistive tools (spellcheck, noise reduction, translation)?
10. Do the Noise and the war pitch's AI share a world, so this game is the place the Rebels defend, or do they stay separate?
11. How does the game state the stance without sounding like a lecture?
12. How strictly are imports converted (palette, resolution, audio quality), and can a creator preview the result before publishing?
13. How many kits at launch, and can the community make kits later?
14. What do marks look like so they feel personal and are hard to abuse?
15. How are rings curated and kept from becoming spam?
16. How long does mail take, and is the delay fixed or chosen by the sender?
17. How anonymous is the Wired persona by default, and can players reveal the link between persona and real self?
18. Do the real-world residents exist to be talked to, or only to make the place feel inhabited?
20. **Text and voice defaults.** The engine provides local text and voice (to verify), so both are cheap. Still to decide: which is the default, how the Wired distortion works, and what is opt-in.
21. How many threads can a player hold (proposed around 12), and can they be one-way?
22. Who moderates a big public space: the team, trusted players, or rotating hosts?
23. What are the minimum social features for the first slice (see the slice list)?
24. How do layer server caps shrink with depth, and is a tiny deep server a good experience or a lonely one?
25. How do pages drift deeper: by visits, links, time, or a human hand? Can it be gamed?
26. What makes an address feel real: format, how it is shown, how a mistyped address behaves?
27. Is the Understudy story right, and is the Understudy (a helpful AI that learned from lonely people) the right central secret for a game that stands against AI?
28. How is the finale vote weighted and enforced, and how is it prevented from being gamed by a few?
29. Do the unmarked in-story copies of past people break the "residents are always marked" promise, or is a deliberate, written exception acceptable?
30. How many people can write and build chapters, and how fast? That limits the cadence more than any design.
19. Does the real world change over time (weather, day and night, a slowly emptying street), and does that follow the player's habits or only the clock?

## Parking lot

- A "Knights"-style secret circle that players can found and invite others to.
- Radio and pirate stations in the Wired that players can run.
- A festival at fixed times of year with player-made installations.
- The Lobby has a ghost no one can interact with.
- A "layer 13" that only appears once enough people have found the right things.
