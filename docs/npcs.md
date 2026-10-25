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

## Ammo, reloading and melee

An NPC's gun is never added to an inventory, so the engine never fills its magazine and treats it as unlimited. `CombatNpc` loads it on spawn (`Weapon.Clip1`) and `FireWeapon` takes a round off the magazine after each shot, since the engine only spends rounds on the holder's side. A burst ends when the magazine is dry, and the engage schedule then runs `ReloadWeapon`: it plays the reload gesture, waits the weapon's `ReloadTime`, and refills the magazine from `SpareMagazines` (default 2, one used per reload; there is no ammo reserve, so the engine's own reload isn't used).

With no spare magazines left and the gun empty, the NPC switches to its melee weapon (`MeleeChoices`, the gunman carries the crowbar; `UsingMelee` is synced, `ApplyHeld` swaps the world model on every peer) and chases its target to within 0.4 of the weapon's range. It doesn't strafe away or rest long. Both weapons are dropped on death.

Pistols (hold type `pistol`) and any weapon not set to automatic are fired one shot per 0.25-0.45 s (`FireWeapon.SemiAutoInterval`); the weapon prefabs all have `PrimaryAutomatic` on, so the hold type decides.

`MeleeChoices` must be declared before `WeaponChoices` in `CombatNpc`: the NPC is armed when `WeaponChoices` loads, and a property loaded after it is still empty at that point. A gunman placed by hand in a scene keeps the values it was saved with, so replace it after changing the prefab.

## Not shoving heavy objects

A walking NPC is a kinematic body (its Rigidbody motion is off while it is on the ground), so its mass does nothing and it would push a car out of the way. `Npc.Movement.cs` takes over the body's movement: the nav agent no longer teleports the body (`UpdatePosition` is off), and `MoveBody` moves it to the agent's position each frame after sweeping its box along the step. If that sweep hits a rigidbody heavier than `MaxPushMass` (300) the NPC drops the part of the step into it and slides along it, or, walking straight at it, goes round on whichever side it is on; lighter objects are still shoved. The agent is moved back to the body when the body was held back. Rigidbody mass 75 only matters once an NPC is off its feet (thrown, physgunned, ragdolled).
