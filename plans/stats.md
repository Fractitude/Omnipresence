# TODO: stats log, the parts the game can't do yet

The stats log ([../docs/stats.md](../docs/stats.md)) already has the place for each of these. Do them when the feature
they belong to arrives.

## Purchases the game does not have yet

`StatsLog.Purchase( connection, itemKind, itemId, itemName, price )` is called today for placeables (Depot) and hardware
(Hardware Store). When these exist, call it next to the spend, with the mechanic `StatMechanic.Shop`:

- [ ] **Buying a house** (`itemKind: "house"`). Ownership of homes is undecided; see [ownership.md](ownership.md).
- [ ] **Buying a car** (`"car"`). Vehicles exist (the buggy spawns with `spawn_buggy`) but nobody can buy one.
- [ ] **Buying a weapon** (`"weapon"`). Weapons come from the loadout; there is no shop.
- [ ] **Any other item** a player pays for: pick a new `itemKind`, and add it to the table in `docs/stats.md`.

## Money that does not exist yet

- [ ] **Picking up money from the ground.** There is no cash item in the world. When there is, pass
  `StatMechanic.GroundPickup` (already defined) to `AddCash`, with the dropper in `Detail` if a player dropped it.
- [ ] **A player dropping money**: the opposite line, `TrySpendCash` with a mechanic of its own.

## Bans and kick reasons

- [ ] **There is no ban system.** `StatsLog.Banned( steamId, name, reason, by )` is written and counts into `Bans`, but nothing
  calls it. Build the ban list (a file in the data folder, checked when a connection is accepted), a `ban` command and an
  admin RPC like `GameManager.RpcKickPlayer`, and call `StatsLog.Banned` from them. Decide: permanent or timed, and how an
  unban is written (an `unban` kind in `StatKind`).
- [ ] **Kick reasons come from the admin only.** `GameManager.Kick` takes an optional reason; the engine's own `kick`
  console command bypasses it and is not logged. Replace it with our own `kick` command that goes through `GameManager.Kick`
  (or find an engine hook for it), and give any admin UI a reason field.
- [ ] **Who is an admin** is the engine's `HasPermission( "admin" )`. Check it is set up for a dedicated server.

## Later

- [ ] A reader: an admin page or app that shows a player's totals and lines (today: `stats_show` and the files).
- [ ] Rotate or compress old `events_*.jsonl` files; nothing deletes them.
- [ ] Log the pending organization shares when they are queued, not only when paid.
- [ ] Decide whether the log should also go to an external database (a webhook, or a sidecar that reads the `.jsonl`).
