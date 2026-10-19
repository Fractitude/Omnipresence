# Technical notes

How the game's systems are built, and why. Navi has its own docs under `Libraries/navi/docs/`.

## Player progression

Each player has a level, experience (XP) and money. Later: inventory and other progress. The game runs on dedicated
servers, and the server is the only place this data is kept and changed.

### The rules

- **The host owns it.** Only the host (the dedicated server) reads, changes and saves progress. A client never sends
  a value; it can only ask for something to happen, and the host decides. Clients get a synced copy to draw the HUD.
- **A player is a Steam ID.** Everything is keyed by `Connection.SteamId`. The host knows it, and a client can't
  fake it.
- **Progress outlives the player object.** `WiredTravel` changes the scene for everyone, and the player object is
  destroyed and spawned again. So the data lives in a static host-side service (`PlayerProgression`) and the
  `PlayerProgress` component on the player only shows it and forwards changes to it.
- **Level is derived.** The record stores XP, not level. `ProgressCurve` turns XP into a level, so changing the curve
  later never leaves saved data out of step.

### The code

All in `Code/Progression/`:

| Type | Job |
|---|---|
| `ProgressRecord` | What is saved for one player: `Version`, `Revision`, `Xp`, `Money`. Plain JSON. |
| `IProgressStore` | `LoadAsync( steamId )` and `SaveAsync( steamId, record )`. The only thing that knows where data lives. |
| `FileProgressStore` | The store for now: one JSON file per player in `FileSystem.Data`, `progress/{steamId}.json`. |
| `PlayerProgression` | Static, host only. Holds the store and a cache of loaded records. Load, change, save, evict. |
| `ProgressCurve` | XP to level, and back. |
| `PlayerProgress` | Component on the player prefab. `[Sync]` copies of level, XP and money for the HUD. |
| `ProgressionHud` | Panel that draws them for the local player. |
| `ProgressCommands` | Console commands to grant XP and money while testing. |

Saves happen when a player's record changes (after a short delay, so a burst of changes is one write), when the player
leaves, and when the server shuts down. A record is evicted from the cache once its player has disconnected.

`FileSystem.Data` is the game's data folder on the machine running the game. On a dedicated server it is the server's
disk. `FileSystem.OrganizationData` is not an option: it is shared only between games on one machine, never between
servers. `Services.Stats` is not an option either: it is Facepunch's backend for numbers and leaderboards, and the game
has no control over it. Use it only for a public leaderboard, never as the source of truth.

### Later: a shared backend

One server's disk is fine until players move between servers (for example because a server is full). Then every
server must see the same record. The plan:

1. A small service of our own (SQLite or Postgres behind a web API) with `GET /players/{steamId}` and
   `PUT /players/{steamId}`.
2. `HttpProgressStore : IProgressStore` calls it with `Http.RequestJsonAsync`. In `PlayerProgression`, one line picks
   this store instead of `FileProgressStore`. Nothing else in the game changes. That is the reason the interface
   exists.
3. **Authentication:** each server sends a secret API key. The key is only on the servers, never in a client, and
   never in code that clients run. The backend should also accept a write only from a server it knows.
4. If the backend can't be reached on connect, the player is kept out of progression for that session (or the server
   refuses the connection): never start them from an empty record, or the next save wipes their real one.
5. If it can't be reached on save, keep the record dirty and retry. Don't drop it.

### Cross-server caveat

When a player jumps from server A to server B, A may still be holding the record and may still write it. If both
write, the later write wins and the other server's changes are lost. Two things prevent that:

- **Revision number.** Every record has a `Revision`. A save says which revision it started from; the backend
  accepts it only if that is still the current one, then increments it. A refused save means the record changed
  elsewhere: the server reloads it, applies its change again (adds are safe to reapply; money and XP are changed by
  deltas, not by sending totals) and retries.
- **Short lock.** Loading a record for a session takes a lease with an expiry (a few minutes) that the server renews
  while the player is there. A second server loading the same player waits, or takes the lease only once it
  expired or the first server released it on disconnect. This is what stops two servers playing the same player at
  once, which the revision number only catches after the fact.

`ProgressRecord.Revision` and the "change by deltas" style are in the first pass already, so the file store needs no
migration for this. The lease and the HTTP store come with the backend.

### Security

- A client must never be able to say "set my money to X". The console commands that grant XP or money run on the host
  and refuse unless the caller is the host or the game is in a development build.
- Treat the record as untrusted input when loading (clamp negatives, ignore unknown fields): files can be edited, and a
  future backend is another trust boundary.
- Never log or sync other players' records to clients beyond what the HUD shows for themselves.
