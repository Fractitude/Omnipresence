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

An NPC with no gun at all (`WeaponChoices` empty) has its melee weapon in hand from the start. With no spare magazines left and the gun empty, the NPC switches to its melee weapon (`MeleePrefab`, the gunman carries the crowbar; `UsingMelee` is synced, `ApplyHeld` swaps the world model on every peer) and chases its target to within 0.4 of the weapon's range. It doesn't strafe away or rest long. A swing only happens while the target is within 0.75 of the weapon's range (not the gun-style engagement band), each `FireWeapon` task is a single swing, and if the target is out of reach for 0.4 s the task ends so the schedule walks to it again. Both weapons are dropped on death.

Pistols (hold type `pistol`) and any weapon not set to automatic are fired one shot per 0.25-0.45 s (`FireWeapon.SemiAutoInterval`); the weapon prefabs all have `PrimaryAutomatic` on, so the hold type decides.

`MeleePrefab` must be declared before `WeaponChoices` in `CombatNpc`: the NPC is armed when `WeaponChoices` loads, and a property loaded after it is still empty at that point. It is a `PrefabFile` (written `{ "Id": <the prefab's guid>, "Path": ... }` in a prefab; a plain path is ignored), because a reference to a prefab's object does not survive the NPC being cloned. An NPC has one melee weapon only: `MeleePrefab` is a single value, `Melee` has no public setter, and the prefab must have a `MeleeWeapon`. A gunman placed by hand in a scene keeps the values it was saved with, so replace it after changing the prefab.

## Not shoving heavy objects

A walking NPC is a kinematic body (its Rigidbody motion is off while it is on the ground), so its mass does nothing and it would push a car out of the way. `Npc.Movement.cs` takes over the body's movement: the nav agent no longer teleports the body (`UpdatePosition` is off), and `MoveBody` moves it to the agent's position each frame after sweeping its box along the step. If that sweep hits a rigidbody heavier than `MaxPushMass` (100) the NPC drops the part of the step into it and slides along it, or, walking straight at it, goes round on whichever side it is on; lighter objects are still shoved. The agent is moved back to the body when the body was held back. Rigidbody mass 75 only matters once an NPC is off its feet (thrown, physgunned, ragdolled).

`MoveTo` walks to a player's or NPC's body (`WorldPosition`), not to the closest point on its bounds: a person's bounds take in everything attached to them and are much bigger than the body, so when you stood within about 130 units the closest point was where the NPC already stood, the move ended at once, and a melee NPC stood still (swinging at air) until you got further off. Props still use their bounds.

## Guns, ammo, health and what an NPC picks up

`CombatNpc` (`CombatNpc.Weapons.cs`) carries up to `MaxGuns` (3) guns, each with its own spare magazines (`SparesOf`; the one it spawns with has `SpareMagazines`, a gun picked up has one, ammo boxes add more), and one melee weapon. It holds the strongest gun that still has ammo (a gun's strength is one shot's `Ballistics.Damage` times its pellets) and re-checks each time it picks what to do, so a dry gun gives way to the next one at once, and only when none is left does it take out the melee weapon. A gun with no magazine in it and no spare magazine is unusable.

- **Out of ammo** (no usable gun, which includes an NPC that spawned without a gun): it goes for the nearest loose weapon, or ammo box, within `PickupRange` (900). With several kinds of ammo box about, it takes the kind for its strongest gun, even if a box for a weaker gun is nearer; boxes for the weaker guns only matter when there is none for the strongest.
- **Upgrades:** with ammo still left, it goes for a loose weapon at least `UpgradeFactor` (1.25) times as strong as its best gun, within `UpgradeRange` (500). The weapon is added to the others and held at once. If all `MaxGuns` slots are full, the weakest (an empty one first) is dropped.
- **Health:** below `HealthKitBelow` (50%) of the health it spawned with, it goes for a `HealthPickup` within `PickupRange` first.
- **Enemy nearer:** it skips any errand when an enemy it can see is nearer than the pickup. A pickup it could not reach or take is left alone for 20 s (`IgnorePickup`).

A loose weapon counts when it is `IsDropped`, unowned and not mounted, `UsableByNpcs` and `Npc.CanBePickedUp`, has a magazine that is not empty, and is not a melee weapon. `CombatPickupSchedule` walks to within 30 units, then `CombatNpc.Collect` takes it, but only when the NPC is as close as a player must be (the pickup's `Interactable.Range`, 80 if it has none, never less than 60). Ammo boxes and health kits are taken through `AmmoPickup.TakeByNpc` and `HealthPickup.TakeByNpc`; a weapon is parented to the NPC (holstered: `IsDropped` off, no world model) and `ActivateGun` makes it the one in hand on every peer. Everything carried is dropped on death.

`MoveTo` walks to a player's or NPC's body (`WorldPosition`), not to the closest point on its bounds: a person's bounds take in everything attached to them and are much bigger than the body, so when you stood within about 130 units the closest point was where the NPC already stood, the move ended at once, and a melee NPC stood still (swinging at air) until you got further off. Props still use their bounds.

## Fetching ammo and weapons

When the gunman's gun is empty and it has no spare magazines, `CombatNpc.GetSchedule` looks within `PickupRange` (900) for the nearest `AmmoPickup` of the gun's ammo type or a loose weapon (`IsDropped`, no owner, `UsableByNpcs` and `Npc.CanBePickedUp`, a magazine that is not empty, not a melee weapon, not mounted) and runs `CombatPickupSchedule`: walk there, then `CombatNpc.Collect`. An ammo box is destroyed through `AmmoPickup.TakeByNpc` and counts as spare magazines (its rounds divided by the magazine size, at least one). A weapon is parented to the NPC and becomes its gun (`AdoptWeapon` makes the world model on every peer): never-fired guns get a full magazine, it gets one spare magazine, and the old empty gun is dropped, where it is ignored because its magazine is empty. With an enemy in sight nearer than the pickup it skips the errand and goes to the melee weapon as before. A pickup it could not reach or take is left alone for 20 s (`IgnorePickup`). Once it has ammo again after switching to the melee weapon it takes the gun back out. An NPC that spawned without a gun counts as out of ammo and looks for a loose weapon the same way (an ammo box is no use to it until it has a gun). It collects only when it stands as close as a player must (the pickup's `Interactable.Range`, 80 if it has none, never less than 60), measured from the pickup's origin; the walk stops 30 units from it.
