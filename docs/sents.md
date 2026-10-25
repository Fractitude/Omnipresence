# Sents: sandbox entity packages

A sent is a prefab published to the cloud as a package of type `sent` (the asset type `ScriptedEntity`, extension
`.sent`). Facepunch's sandbox game spawns them; this game can too. Code: `Code/Sents/`. Ported from the sandbox game's
`ScriptedEntity`, `EntitySpawner` and `CloudSpawnList`, much smaller.

- `ScriptedEntity` (`ScriptedEntity.cs`) has the same class name and extension as upstream, because the cloud packages
  are made of that type. It has no `IDefinitionResource` (that marker lives in the sandbox project, not the engine).
- `Sents.Spawn( ident )` (`spawn_sent org.package`, `Sents.cs`) asks the host. The host checks the ident
  (`org.package` only, never a path), loads the package with `Cloud.Load<ScriptedEntity>( ident, true )`, clones its
  prefab where the player looks (500 units, stood on the floor) and `NetworkSpawn`s it.
- `SentBrowser` (`sents` command, a HUD piece) searches the cloud (`Package.FindAsync( "sort:popular type:sent <text>" )`)
  and shows tiles with thumbnail, title and author. Clicking one spawns it and closes the window; right-click copies its ident
  or opens its package page. The sidebar filters by tag (`#vehicle`, `#weapon`, `#npc`, `#explosives`; `tag:` finds nothing), and the list loads the
  next 40 when scrolled near the bottom.

## Who may spawn

A sent can carry code (`IncludeCode`) that runs in the game with no sandbox, permissions or prompt, as with a Navi
plugin. So the **host's own player** is the only one who may spawn a sent, unless the host sets `omni.sent_everyone 1`
(saved, read on the host). The host checks `Rpc.Caller` on every request; the browser only says so (`Sents.CanSpawn`).
Never add a way to spawn a sent that skips that check. This is part of the known limit in AGENTS.md (a package's code
can reach everything).

## A sent that carries code

A sent with `IncludeCode` was compiled against the sandbox game (`package.facepunch.sandbox`), a package this game does
not have, so its code fails to compile here and its components load as **missing** (`Couldn't find reference
package.facepunch.sandbox`, then `Missing Component`). The source is inside the package the engine downloaded, so the
fix is to compile it as part of this game:

1. Spawn the sent once (`spawn_sent org.package`), so the engine has downloaded it.
2. `sent_import org.package` (editor console, `Editor/SentImporter.cs`) writes its source to
   `Code/Sents/Imported/org.package/`. Files another import already has, unchanged, are skipped, because one author's
   sents often share a library. Tested on `crosschainer.takeda_ascend`, which shares all 14 files with Hokuto Boxer: nothing is written twice.
3. Read the code. It runs in the game with no sandbox, and it is now in the repo, so review it before committing.
4. Fix what does not compile. Most of a sent's code only uses the engine, and then it just works. Where it uses a type
   of the sandbox game (`Player`, `Ownable`, `GameManager`, a sandbox tool), that type has to exist here under the same
   name and members: port it from the sandbox clone, as the weapons were.

`Sents.SpawnAsync` builds the object from the prefab's data when the engine's cached copy of the prefab still has the
components as missing (the prefab was loaded before the import), so no restart is needed.

Not every sent can work: one whose code depends on much of the sandbox game (its NPC system, its tools, its save
system) needs that part ported first. Props and models with no code, and code that only uses the engine, work as they are.

### Imported so far

- `crosschainer.hokuto_boxer` (and `takeda_ascend`, same author, the same `CuratedVehicles` code): a driveable car.
  Its `VehicleSeat` is an engine `BaseChair`, which is itself a `Component.IPressable`, so the player's Use key sits in it
  without our `Interactable`. `CuratedVehicle` implements `IVehicleGauges` (added by hand after the import) and so shows the game's own speedometer
  (`Code/Vehicles/Hud/VehicleHud.razor`, shared with the buggy). The package's own HUD was not kept; running `sent_import` again would bring it back as a duplicate `VehicleHud`, so delete that copy. The dial picture and the
  sounds are assets of the package and come with it.
- `gmk.fpv_drone_enemy` (and `fpv_drone_friendly`, same code): an NPC (`FpvDroneNpc`) on the ported NPC system, which flies at
  and explodes on its target. It used sandbox's `Player`; those uses were changed by hand to the engine's `PlayerController` (eyes) and our
  `PlayerHealth`, so a new import of it needs the same edits.
- `gmk.aura`: a weapon (`AuraWeapon` on the engine's weapon base; click toggles the aura, which heals the holder and throws things away). Its sandbox `Player` uses became `PlayerController` and `PlayerHealth`, and `OnPrimaryDown` (an upstream hook not ported) became an empty `PrimaryAttack`.

### Known not working

- `sftatlas.claymoremine`: the package has no code, only two ActionGraphs (on the prop's damage event and the trigger's enter event) that call `ClaymoreMine.ActivateMine`, a type its author never published. With the type missing, every touch logged `ActionGraph... Unable to resolve type binding` against whatever component was updating (the player's `WeaponInventory`, a drone's `FpvDroneNpc`). `Code/Sents/Ported/ClaymoreMine.cs` is a stand-in written from the prefab: it spawns the prefab's explosion and removes the mine. Any other sent whose graphs log this error is missing a type the same way: look for `"_type"` in its graphs (`Cloud.Load<ScriptedEntity>`, `Prefab.RootObject`).
