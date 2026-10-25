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
| Find the local player: `Players.LocalObject`, `Player.FindLocalPlayer()` | works |
| Set `Player.Health` (100 to 42) and `Armour` | **works** |
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
| `Player.Kill`, `GameManager.Current`, `NaviHardwareResource.All`, `NaviComputer.Active` | refused (not public, or not found) |

Calls into `NaviComputer` that check `IsAuthority` or `Authorize` (host and seat checks) still apply, because those run
inside the method. In the editor the player is the host, so those checks pass.

I could not test: a client in a multiplayer game, a published build (the whitelist), or whether `Compile()` finds
unknown members (it does not: it said true for a field that does not exist; binding happens at run).

## Narrowing what a script can reach

Tried on engine 26.10.13a. The engine builds the resolver itself (`new() { Resolver = new TypeLibraryScriptResolver(
context.TypeLibrary ) }`), so there is no setting to shorten the list.

How the list is made (`TypeLibrary.AddAssembly`): the game's and its libraries' assemblies are "dynamic", so **every
type in them is registered**. Engine types are registered only with `[Expose]`, plus a fixed set of safe system types.

| Way to hide something | Result |
| --- | --- |
| Make a **member** `private` or `internal` (property, setter, method, field) | **hidden** |
| Make a **class** `internal` | still reachable (its public members work: `VehicleMath.Sign`, `GamePreferences.Screenshake`) |
| `[Hide]` on a class or a member | still reachable |
| `[System.Obsolete]`, `[Title]` on a member | still reachable |
| `[EditorBrowsable]` | cannot compile (`SB1000`, not on the API whitelist) |
| Remove the type from the `TypeLibrary` | no public way |

So only `private`/`internal` members are safe. That is not enough alone: Navi is a separate assembly, and the game calls
`NaviComputer.Lock`, `InstallHardware`, `Launch`, ... from `Code/`. s&box has no `InternalsVisibleTo`, so they must stay
`public`.

### The guard (tested, works)

Run every script through one runner that counts a depth, and make sensitive public methods refuse while it is above zero:

```csharp
public static class ScriptGuard
{
	static int depth;                                    // private: a script cannot set it
	public static bool Active => depth > 0;
	internal static void Enter() => depth++;             // internal: a script cannot call it
	internal static void Exit() => depth--;

	public static T Run<T>( string source, int limit = 100000 )
	{
		var script = Game.Scripting.CreateScript( source );
		Enter();
		try { return script.RunAndReturn<T>( limit ); }
		finally { Exit(); }
	}
}

public static string Sensitive() => ScriptGuard.Active ? "refused" : "ran";
```

Results: called from game code it ran; called from a script run through `ScriptGuard.Run` it was refused; a script
calling `ScriptGuard.Exit()` or setting `depth` was refused as unavailable; a nested `ScriptGuard.Run` stayed refused;
the flag was back to false afterwards. Scripts are synchronous and cannot start tasks or lambdas, so the flag covers all
their calls.

Rules for it:

- Nothing but the runner may call `Game.Scripting`. A second path that skips the runner skips the guard.
- The guard class and the checks must live where the sensitive code lives. For Navi: in the library, so
  `NaviComputer` can check it. The game's runner calls the library's `Run`.
- Default to refusing, then allow. Walk the public surface of what scripts may touch (a short list) and add the check to
  every method and setter that changes state or leaks secrets, not just the ones we thought of. A new public method is
  open to scripts until someone guards it.
- A better design is to give scripts a small facade: do not let them run in a game with the full `TypeLibrary` at all if
  the facade will do. We cannot hide the rest, so the guard is what makes a facade enforceable.

## What this means for Navi (security)

**The type list is the only wall, and only private/internal members are behind it.** A script can hurt the game as much
as any public API can: set health, teleport the player, give weapons, lock the computer, read the saved lock secrets,
install hardware.

Before a script engine goes into Navi or the game:

- Do not run player-written or Store-app-written scripts without the guard above. Navi's permissions
  (`NaviSystemCalls`) would be skipped: the script calls `NaviComputer` itself.
- Candidates to guard in `NaviComputer`: `Lock`, `Unlock`, `InstallHardware`, `RemoveHardware`, `CaptureSave`,
  `RestoreSave`, `Files`, `Launch`, `Install`, and the `NaviMessages`, `NaviNpc` and `NaviPluginManager` statics. Game
  types: `Player` setters, `WeaponCommands.Give`, `PlayerInventory`, `GameManager`.
- Run scripts only on the host (Navi does: `RunScript` is host only) or on the seated player's client, never from a remote request, and keep the instruction
  limit low.

This matches the known limit in `AGENTS.md`: hostile package code can already reach public members. Scripts make that
easier, since no package has to be published.

## Testing it

- Run scripts from `sbox-cli --call run_csharp` (see `Libraries/navi/docs/driving-navi.md`), with `Game.Scripting.CreateScript(
  src )` and `RunAndReturn<string>`. Return strings (`"" + value`) to read results.
- `Script.Compile()` does not tell you whether a member is reachable: it said `true` for a field that does not exist.
  Binding happens at run, so a test must run the script.
- Probes change the live play-mode scene (health, position, destroyed objects). Run them against objects you made, or
  put the old value back after, and do not run `Destroy` on a scene object. `Destroy` is also deferred to the end of the
  frame, so a count taken right after still shows the object.
- A test that adds a type to `Code/` (to try an attribute) triggers a hot reload. Delete it afterwards and wait for
  the compile to finish (`editor_status`).
- Do not save the scene or prefabs after probing in play mode.

## What Navi has built on it

Code Edit has a **Run** button (`Libraries/navi`, see its README, "Scripts"). The pieces:

- `NaviComputer.ScriptingEnabled`: inspector, group **Other**, **off by default**. Only the game or the inspector sets
  it (a script setting it is ignored). Code Edit draws Run dimmed with the tooltip "Running scripts is disabled" while it
  is off; the host's copy refuses too.
- Permission `navi.scripts.run` ("Run scripts"). The app asks for it the first time, through the secure layer.
- Calls `scripting.enabled` and `scripting.run` (`Navi.Scripts.IsEnabledAsync()`, `RunAsync( source )`). `RunAsync` runs
  on the host through `NaviComputer.RunScript` (internal, so a script cannot reach it), with an instruction limit of
  1,000,000, and answers `ScriptResult( Ok, Message, Start, Length )`. Code Edit shows the line of an error. A script
  returns nothing to the app.
- `NaviScriptGuard` (Navi library). `RunScript` is the only caller of `Game.Scripting`. While a script runs, these return
  without doing anything: `Lock`, `Unlock`, `InstallHardware`, `RemoveHardware`, `CaptureSave`, `RestoreSave`, `Install`,
  `Uninstall`, `InstallPackageAsync`, `UninstallPackage`, `SetPermissions`, `AnswerPermissionRequest`,
  `ReviewPermissions`, `MountDrive`, `EjectDrive`, `SetIpAddress`, and the `ScriptingEnabled` setter.

Tested in play mode: flag off gives UiState `ScriptingEnabled = false` and the Run tool has the `unavailable` class; flag
on flips it within a poll (0.25 s); with the permission, `return 1;` and `var a = 1 + 2;` run, `var x = ;` fails with the
range 8..9, `while ( true ) { }` stops at the limit; a script calling `Lock()` left `IsLocked` false, one setting
`ScriptingEnabled = false` left it true, one calling `RemoveHardware` left both pieces installed, and one calling
`SetPermissions` to take Code Edit's own permission away left it intact; with the flag off and the permission held, the
call answers "Running scripts is disabled." Not tested: the real permission prompt (it needs a real mouse press), a
client in multiplayer.

**The guard is a list, not a wall.** A script can still reach what is not on it: `NaviComputer.Files`, `NaviMessages`,
`NaviNpcs`, `NaviPluginManager`, other `NaviComputer` setters and the game's own types (`Player`, the player's
position, `WeaponCommands.Give`). Any new public method that changes state or leaks a secret must check
`NaviScriptGuard.Active`, or it is open to scripts. Turn the flag on only where you trust scripts as much as game code.

## Open questions

- Is the language really "C#-like", or its own language with C# names? (Breen has no public docs.)
- Does it work in a published build, and does the API whitelist change what `TypeLibrary` contains there?
- Can a game restrict which types a script sees? Not today (see "Narrowing"). Ask in the s&box issue tracker.
- How stable is it? It is marked experimental.
