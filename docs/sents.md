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
  and shows tiles with thumbnail, title and author. Clicking one spawns it and closes the window.

## Who may spawn

A sent can carry code (`IncludeCode`) that runs in the game with no sandbox, permissions or prompt, as with a Navi
plugin. So the **host's own player** is the only one who may spawn a sent, unless the host sets `omni.sent_everyone 1`
(saved, read on the host). The host checks `Rpc.Caller` on every request; the browser only says so (`Sents.CanSpawn`).
Never add a way to spawn a sent that skips that check. This is part of the known limit in AGENTS.md (a package's code
can reach everything).

## What works

Tested with `crosschainer.hokuto_boxer`: the package loads and the prefab spawns. A sent written for the sandbox game
often has code that references `facepunch.sandbox` (its own components such as `CuratedVehicles.CuratedVehicle`). That
package does not exist here, so its code fails to compile (`Couldn't find reference package.facepunch.sandbox`) and the
components show as missing: the model spawns but does nothing. Only sents whose code stays inside the engine, or whose
prefab is just props, work as they are. To use a sandbox-only one the code has to be ported into this game.

A sent's prefab children must be Network Mode Snapshot to reach clients (see AGENTS.md); we cannot fix a package's prefab.
