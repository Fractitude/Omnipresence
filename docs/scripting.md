# Scripting (Breen, `Game.Scripting`)

s&box 26.09.22 added an experimental scripting system: `Game.Scripting` (`Sandbox.ScriptSystem`), backed by **Breen**
and `TypeLibrary`. It runs source text at run time, in a game. This file is what we know. Most of it comes from our own
tests on engine 26.10.13a, so check it again after an engine update.

## Sources

- Release notes 26.09.22: "Experimental scripting - added Game.Scripting with reusable scripts, typed return values,
  bound arguments and local/global stores, backed by Breen and TypeLibrary" (<https://sbox.game/release-notes>).
- Update post 26.09.22 (<https://sbox.game/news/update-26-09-22>): Garry describes it as a C#-style take on Lua, a text
  alternative to ActionGraph, with a syntax-highlighted `ScriptControl` that can also run in game. We could not read this
  page ourselves; this line is from a search summary.
- Commit `3cc4a463` "Scripting (#5852)" in `Facepunch/sbox-public`: `engine/Sandbox.Engine/Systems/Scripting/` (the
  `ScriptSystem`, `Script`, `ScriptStore`, `TypeLibraryScriptResolver`) and
  `engine/Sandbox.Engine/Systems/UI/Controls/ScriptControl/` (the editor control).
- API: <https://sbox.game/api/Sandbox.Game> (`Scripting`), <https://superdocs.sboxcool.com/Sandbox/ScriptSystem>, and the
  XML docs in `<engine>/bin/managed/Sandbox.Engine.xml` (search for `Sandbox.Script`).
- **Breen itself is closed source.** It is `<engine>/bin/managed/Breen.dll`, not in the public repo. There is no language
  documentation. Everything about the syntax below was found by trying it.

## API

Get the system from `Game.Scripting` (an instance, not a static class). Use it on the game's main thread, in the context
that owns it.

```csharp
int n = Game.Scripting.Run<int>( "return 1 + 2;" );             // throws Script.ScriptException on failure

var script = Game.Scripting.CreateScript( "return go.Name;" );
script.SetArgument( "go", someGameObject );                     // named input, next run only
string name = script.RunAndReturn<string>( instructionLimit: 100000 );
if ( script.LastError is { } e ) Log.Warning( $"{e.Code}: {e.Message} at {e.Start}+{e.Length}" );
```

- `Run<T>( source )`: cached code, instruction limit 10,000,000, throws on failure.
- `CreateScript( source, ScriptStore locals = null )` gives a `Script`: `SetArgument`, `HasArgument`, `Compile()`,
  `Run( instructionLimit )` (false on failure), `RunAndReturn<T>( instructionLimit )`, `With( name, value )` (stack-only
  arguments, chainable, may be ref structs), `LastError` (`Code`, `Message`, `Start`, `Length`).
- `ScriptStore` (`Game.Scripting.Globals`, `CreateStore()`): typed values kept across runs and edits. Script fields marked
  `[Global]` or `[Local]` live in the global store or the script's local store.
- `Game.Scripting.Analyze( source, inputs )`: for editors. Tokens for highlighting, diagnostics, completions, signature
  help and symbol info, without running anything.
- UI: the `ScriptControl` panel (highlighting, completion, hover, signature help, gutter, status bar).
- Depth limit: 32 nested script calls. A script must run on its owner's thread.

## Language, as found by testing

A C#-like subset.

- Works: `var`, typed locals, `for` loops, `return`, `new Vector3( 1, 2, 3 )`, `new List<int>()`, string methods
  (`ToUpper`, `Length`), `+` on strings, property get and set, public method calls, public constructors.
- A script needs an explicit `return value;`. `1 + 2` alone fails with "did not return a value".
- Read the result with the exact type: `RunAndReturn<float>`. `RunAndReturn<object>` fails with "Specified cast is not
  valid", and so does a mismatched type.
- Not supported: lambdas and `Func<>`, `class` declarations, `typeof`, `GetType()`.
- Generic methods mostly fail: `Components.Get<T>()` and `Components.Create<T>()` say "unavailable". Some work:
  `scene.Get<T>()` and `go.GetComponent<T>()` did. `GetAll<T>()` fails with a message about boxed value types.
- Some plain calls are refused too: `ToString()` on `bool`, `float` and `Nullable`. Use `"" + value`.
- Runaway loops stop: `while ( true ) { }` with `Run( 10000 )` gives `Instruction limit exceeded.`
- Errors have codes, such as `SCR_REENTRY` (a script ran or compiled inside another script run).

## What a script can reach

`TypeLibraryScriptResolver` decides. A script can name **any type registered in the running game's `TypeLibrary`**, by
full name or short name. No arguments are needed. It sees **public** methods, fields and properties, and public
constructors. It does not see private or internal members.

Not reachable: `System.*` (so no `System.IO`), `Game`, `Log`, `MathX`, `TypeLibrary`, `Networking`, `FileSystem`,
`Application`, `Steam`, `Http`, `ConsoleSystem`, `Input.Down`, `Scene.Active`, reflection. Reachable: `Vector3`, `Time`,
`Connection`, `GameObject`, `Scene`, `Components`, and all of this game's and Navi's public types.

### Tests on the running game (editor, play mode), scripts given no arguments

| Test | Result |
| --- | --- |
| Find the local player: `Players.LocalObject`, `PlayerHealth.FindLocalPlayer()` | works |
| Set `PlayerHealth.Health` (100 to 42) and `Armour` | **works** |
| Teleport: set `Players.LocalObject.WorldPosition` | **works** |
| `WeaponCommands.Give( "name" )` (static helper, tried with a bad name) | **callable** |
| Reach `PlayerInventory` and `WeaponInventory` | found; `PlayerInventory.Add` was refused |
| Spawn a `GameObject`, add a component, clone a prefab | **works** |
| Find the scene's `NaviComputer` (`player.Scene.Get<NaviComputer>()`) | **works** |
| Read `IsLocked`, `IsSeated`, call `CaptureSave()` (includes lock secrets) | **works** |
| Call `Lock()` then `Unlock()` | **works** |
| `InstallHardware( "bad id" )` (returns false only because the id is unknown) | **callable** |
| Reach `c.Files` | **works** |
| `new NaviComputer()`, `NaviMessages.Identify`, `NaviHardware.Find` | callable |
| Set a private-set property (`IpAddress`), internal set (`SecureEntryActive`) | refused |
| Call a private method (`UpdateBackPlane`), read an internal property (`CurrentDrag`) | refused |
| `PlayerHealth.Kill`, `GameManager.Current`, `NaviHardwareResource.All`, `NaviComputer.Active` | refused (not public, or not found) |

Calls into `NaviComputer` that check `IsAuthority` or `Authorize` (host and seat checks) still apply, because those run
inside the method. In the editor the player is the host, so those checks pass.

I could not test: a client in a multiplayer game, a published build (the whitelist), or whether `Compile()` finds
unknown members (it does not: it said true for a field that does not exist; binding happens at run).

## What this means for Navi (security)

**The type list is the only wall.** A script can hurt the game as much as any public API can: set health, teleport the
player, give weapons, lock the computer, read the saved lock secrets, install hardware. Private and internal members are
safe, so the project's rule of keeping security state `private`/`internal` matters.

Before a script engine goes into Navi or the game:

- Do not run player-written or Store-app-written scripts with the full `TypeLibrary`. Navi's permissions
  (`NaviSystemCalls`) would be skipped: the script calls `NaviComputer` itself.
- Public methods that must not be reachable by scripts need a different guard. Candidates in `NaviComputer`:
  `Lock`, `Unlock`, `InstallHardware`, `RemoveHardware`, `CaptureSave`, `RestoreSave`, `Files`, `Launch`, `Install`.
  Make them `internal` where Navi's own code is the only caller, or check a "script is running" flag inside them.
- Game helpers like `WeaponCommands.Give` and the `PlayerHealth` setters are open to scripts. Decide per type.
- Check whether the resolver honours `[Expose]`/attribute filters or can be given a smaller list. We found no setting for
  it: `new() { Resolver = new TypeLibraryScriptResolver( context.TypeLibrary ) }` is built by the engine.
- Run scripts only on the seated player's client or on the host, never from a remote request, and keep the instruction
  limit low.

This matches the known limit in `AGENTS.md`: hostile package code can already reach public members. Scripts make that
easier, since no package has to be published.

## Open questions

- Is the language really "C#-like", or its own language with C# names? (Breen has no public docs.)
- Does it work in a published build, and does the API whitelist change what `TypeLibrary` contains there?
- Can a game restrict which types a script sees? Ask in the s&box issue tracker.
- How stable is it? It is marked experimental.
