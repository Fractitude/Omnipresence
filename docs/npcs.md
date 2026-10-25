# NPCs

Ported from Facepunch's open-source sandbox game (`github.com/Facepunch/sandbox`, `Code/Npcs/`). Code: `Code/Npcs/`. The upstream README explains the idea: an NPC thinks once a tick, `GetSchedule()` picks a schedule, the schedule runs tasks, and a higher-priority schedule interrupts a lower one when awareness changes.

## What is ported

The `Npc` base (health, ground and airborne handling, factions and dispositions, schedules), its four layers (senses, navigation, animation, speech), the shared tasks and schedules, and `CombatNpc` with its schedules. The roller, scientist, example NPC and outfits are not ported.

Changed from upstream: no speech lines (the localized sound events don't exist here), no follow-on-press (`IPressable`), no `Ownable`, `Player` is the engine's `PlayerController`, and `GameManager.OnNpcDeath` only tells the attacker's `IKillSource` and logs. `BaseSandboxWeapon.NpcAimTarget` is added for aiming.

## The test NPC

`spawn_combat_npc` spawns `Assets/npcs/combat_npc.prefab`, a hostile gunman with a Glock (`WeaponChoices`; the weapon is cloned onto the NPC before it is network spawned). It sees 1500 units (`CombatNpc.SightRange`), hates players, and closes in, fires bursts and sometimes strafes. Hurt NPCs turn on whoever hit them; a dead one drops its gun and a ragdoll.

- **NPCs walk on the navmesh**, so the scene's NavMesh must be enabled. It is on in `playground.scene` and off in the other scenes.
- A player counts as hostile only if its root has the tag `player`.
- Change the gun by editing `WeaponChoices` on the prefab; the weapon needs `UsableByNpcs` / the `Npc` usage settings of the engine `BaseCombatWeapon`.
