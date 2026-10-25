# Sents: sandbox entity packages

A sent is a prefab published to the cloud as a package of type `sent` (the asset type `ScriptedEntity`, extension
`.sent`). Facepunch's sandbox game spawns them; this game can too. Code: `Code/Sents/`. Ported from the sandbox game's
`ScriptedEntity`, `EntitySpawner` and `CloudSpawnList`, much smaller.

Files in `Code/Sents/`, each with one job:

- `ScriptedEntity.cs`: the same class name and extension as upstream, because the cloud packages are made of that type.
  It has no `IDefinitionResource` (that marker lives in the sandbox project, not the engine).
- `Sents.cs`: `spawn_sent org.package`. Asks the host, which checks who is asking (`CanSpawn`), the ident (`org.package`
  only, never a path), a half-second gap between one player's spawns, and that the player has a body; then loads the
  package (`Cloud.Load<ScriptedEntity>( ident, true )`), builds it where the player looks (500 units, stood on the floor)
  and `NetworkSpawn`s it. A failure is logged with the ident, never thrown.
- `SentBuilder.cs`: the one place that turns a prefab into an object (below).
- `SentPackage.cs`: `[SentPackage( ident, namespaces )]` and `SentPackages`, what the game knows about imported code.
- `SentCatalog.cs` and `SentBrowser.razor` (`sents` command, a HUD piece): the cloud search, and the window that draws it, titled Workshop Entities, resizable, four columns of tiles at its default size.
  The catalog searches `Package.FindAsync( "sort:popular type:sent <text>" )` a page of 40 at a time; the window shows tiles
  with thumbnail, title and author. Clicking one spawns it and closes the window; right-click copies its ident or opens its
  package page. The sidebar filters by tag (`#vehicle`, `#weapon`, `#npc`, `#explosives`; `tag:` finds nothing), and the
  next page loads when the list is scrolled near the bottom.
- `Ported/`: types a sent needs and its author never published (`ClaymoreMine`).
- `Imported/<ident>/`: the code of imported sents (below).

## How a sent is built

`SentBuilder.Build` has two ways, and a sent takes the first that gives all of its components:

1. **Clone** the prefab, as the sandbox game does. It is checked: a component the clone lacks means the engine had the prefab
   cached before its code was imported (the clone has it as `MissingComponent`, no restart needed), or the sent's own
   package failed to compile (the component is left out without a trace, as with the friendly FPV drone, which shares code
   with the enemy).
2. **Build from the prefab's data**, which looks every type up again. Always used for an imported sent that declares
   namespaces. It first gives every object and component a **new id** and rewrites every reference to it: a prefab keeps its
   own ids, its components refer to each other by them (an NPC's `Renderer`), and a second copy built with the same ids
   found them taken, got new ones, and had references into the first copy. The second NPC A-posed on the first one's
   renderer and could not die. `GameObject.Clone` does this itself; building from data does not.

**Two sents with the same type name.** In the sandbox game each sent is its own package, so four Combine sents can each have
a `CombineNpc`. Here they share one assembly, and a prefab's plain `"CombineNpc"` finds only one. `sent_import` writes a
`SentPackage.cs` beside the code with the namespaces the package declares, and the data build names each component by the
first of them that has the type (`Hl2Metrocop.CombineNpc`). Nothing is kept by hand.

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

1. `sent_import org.package` (editor console, `Editor/SentImporter.cs`) downloads the package if it is not there, and writes its source to
   `Code/Sents/Imported/org.package/`, and the `SentPackage.cs` manifest. Files another import already has, unchanged, are
   skipped, because one author's sents often share a library (`takeda_ascend` shares all 14 files with Hokuto Boxer: nothing is
   written twice). It is safe to run again: a folder that exists gets only the manifest and a report of what the package has
   that the folder has not (`new`) or has changed (`edited`); `sent_import org.package update` also writes the new files. **A
   file edited here is never overwritten**, so the hand fixes below survive.
   Then it waits for the game to build and reports what does not compile (`Editor/SentDiagnosis.cs`): each kind of error once,
   with how many, and what to do. For a type the code uses that this game lacks, it looks it up in the sandbox clone
   (`omni.sandbox_clone`) and names the file to port it from; a hook of a sandbox base class that was not ported is named as
   such.
2. Read the code. It runs in the game with no sandbox, and it is now in the repo, so review it before committing.
3. Fix what does not compile. Most of a sent's code only uses the engine, and then it just works. Where it uses a type
   of the sandbox game (`Player`, `Ownable`, `GameManager`, a sandbox tool), that type has to exist here under the same
   name and members: port it from the sandbox clone, as the weapons were.

4. `sent_test org.package` (the game playing) spawns it, watches it for ten seconds and says whether it works: did it
   spawn, are components missing, did anything throw or log an error (`Editor/SentTest.cs`). Repeat 3 and 4 until it says OK.

If a sent is spawned with components the game does not have, the log says so and, for a sent with code that was never
imported, names the command.

Not every sent can work: one whose code depends on much of the sandbox game (its NPC system, its tools, its save
system) needs that part ported first. Props and models with no code, and code that only uses the engine, work as they are.

### Imported so far

- `crosschainer.hokuto_boxer` (and `takeda_ascend`, same author, the same `CuratedVehicles` code): a driveable car.
  Its `VehicleSeat` is an engine `BaseChair`, which is itself a `Component.IPressable`, so the player's Use key sits in it
  without our `Interactable`. `CuratedVehicle` implements `IVehicleGauges` (added by hand after the import) and so shows the game's own speedometer
  (`Code/Vehicles/Hud/VehicleHud.razor`, shared with the buggy). The package's own HUD was not kept (running `sent_import ... update` would bring it back as a duplicate `VehicleHud`, so do not use `update` on it). The dial picture and the
  sounds are assets of the package and come with it.
- `gmk.fpv_drone_enemy` (and `fpv_drone_friendly`, same code): an NPC (`FpvDroneNpc`) on the ported NPC system, which flies at and explodes on its target. It needed no edits once our player was named `Player` like upstream's.
- `gmk.aura`: a weapon (`AuraWeapon` on the engine's weapon base; click toggles the aura, which heals the holder and throws things away). Two edits: `Owner` is the engine's `PlayerController` here (upstream's is `Player`), so the heal looks the `Player` up, and upstream's `OnPrimaryDown` hook is not ported, so it overrides `PrimaryAttack` instead.
- `hl2npcs.metrocop`, `combine_soldier`, `combine_elite`, `combine_prisonguard` and `manhack` (one author, one codebase in five variants): Half-Life 2 NPCs (`CombineNpc` and `ManhackNpc`). No edits: the `TaskStatus` clash is a global alias in `Assembly.cs`, and `Feed` / `IKillIcon` (the kill feed) exist as empty types in `Code/Game/Feed.cs`. A metrocop spawned before the import stays broken (`AnimationLayer` NullReference, its `Npc` is a missing component): delete it and spawn a new one.

### Known not working

- `sftatlas.claymoremine`: the package has no code, only two ActionGraphs (on the prop's damage event and the trigger's enter event) that call `ClaymoreMine.ActivateMine`, a type its author never published. With the type missing, every touch logged `ActionGraph... Unable to resolve type binding` against whatever component was updating (the player's `WeaponInventory`, a drone's `FpvDroneNpc`). `Code/Sents/Ported/ClaymoreMine.cs` is a stand-in written from the prefab: it spawns the prefab's explosion and removes the mine. Any other sent whose graphs log this error is missing a type the same way: look for `"_type"` in its graphs (`Cloud.Load<ScriptedEntity>`, `Prefab.RootObject`).
