# NPCs

Ported from Facepunch's open-source sandbox game (`github.com/Facepunch/sandbox`, `Code/Npcs/`). Code: `Code/Npcs/`. The upstream README explains the idea: an NPC thinks once a tick, `GetSchedule()` picks a schedule, the schedule runs tasks, and a higher-priority schedule interrupts a lower one when awareness changes.

## What is ported

All of upstream's `Code/Npcs`: the `Npc` base (health, ground and airborne handling, factions and dispositions, schedules), its four layers (senses, navigation, animation, speech), the shared tasks and schedules, `CombatNpc`, `ScientistNpc`, `RollerNpc`, `ExampleNpc` and `NpcOutfits`.

Changed from upstream: `Player` is the engine's `PlayerController`, no `Ownable`, no per-player stats for the scientist, and `GameManager.OnNpcDeath` only tells the attacker's `IKillSource` and logs. `BaseSandboxWeapon.NpcAimTarget` is added for aiming. Upstream localizes the combat lines (`#npc.speech.combat.*`); this game has no localization, so they are plain English in the code.

## Voice and subtitles

`SpeechLayer.TrySay` plays a `SoundEvent` (a random clip of it) or just shows a line. The scientist talks with real clips, `Assets/sounds/npc/darren/` (follow, stay, scared, idle, stuck), set on the prefab's `Voice` group. Combat NPCs call out text only. `NpcSubtitles` (`Code/Npcs/`, one line in `HudSystem`) draws each speaking NPC's `Speech.CurrentSpeech` over its head, within 1500 units.

## Outfits

`NpcOutfits` picks one outfit and one face (head, hair, beard) at random on the host when the NPC is spawned, before it is network spawned, and puts them on its `Dresser`. The scientist prefab has six outfits and several faces.

## Spawning

`spawn_gunman`, `spawn_scientist` and `spawn_rollermine` clone `Assets/npcs/<name>.prefab` in front of you. The rollermine's effects and sound are in `Assets/entities/sents/npc/rollermine/` and its model in `Assets/entities/hoverball/`, both unchanged from upstream.

## The test NPC

`spawn_gunman` spawns `Assets/npcs/gunman.prefab`, a hostile gunman (the human male citizen model, in one of six outfits: army, raider, biker, riot, street, drifter, with one of five faces, picked by `NpcOutfits`) with a Glock (`WeaponChoices`; the weapon is cloned onto the NPC before it is network spawned). It sees 1500 units (`CombatNpc.SightRange`), hates players, and closes in, fires bursts and sometimes strafes. Hurt NPCs turn on whoever hit them; a dead one drops its gun and a ragdoll.

- **NPCs walk on the navmesh**, so the scene's NavMesh must be enabled. It is on in `playground.scene` and off in the other scenes.
- A player counts as hostile only if its root has the tag `player`.
- Change the gun by editing `WeaponChoices` on the prefab; the weapon needs `UsableByNpcs` / the `Npc` usage settings of the engine `BaseCombatWeapon`.
