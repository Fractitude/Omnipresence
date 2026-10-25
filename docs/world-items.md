# World items: dropping and picking up

An item the player drops becomes a `WorldItem` on the ground, and picking it up gives back exactly what was dropped. World
items are **saved between sessions** (see "Saved between sessions"): what was dropped is still on the ground the next time
the scene starts on the host. There is no despawn timer; the only limit is `WorldItem.MaxInScene` (300) per scene, beyond
which a drop is refused with a message and the item stays in the bag.

## How the player drops

Drag an item out of the bag's window and let go outside it (`InventoryGrid.EndDrag` -> `PlayerInventory.Drop( slot )`), or
right click it and choose **Drop** (the Character menu's bag and the bag half of the storage and cargo windows; the same
`PlayerInventory.Drop( slot )`, so the same host checks). Unloading a vehicle's cargo makes world items too: the stack
comes out whole as one `WorldItem` behind the vehicle (`WorldItem.Spawn`, see [storage.md](storage.md)).
Dropped onto a slot it moves; dropped inside the window but not on a slot it stays. The whole stack drops as **one** world
item. A parcel drops like anything else, with its order, addressee and contents intact (`ItemDef.CanDrop` can still forbid a kind).

## Data flow

1. `Drop` asks the host (`HostDrop` RPC, checked against the caller). `DoDrop` checks `CanDrop` and the scene limit, then
   `WorldItem.SpawnFrom( player, item.Clone() )` **before** removing the item, so a drop that cannot be made loses nothing.
2. `SpawnFrom` traces a sphere from the eye forward 60 units (ignoring the player) and places the item at the end of the
   trace, so it is never inside the player or a wall. When nothing is hit it is tossed forward and up a little.
3. The prefab comes from `ItemCatalog.WorldPrefabOf( kind )`: the kind's `ItemDef.WorldPrefab`
   (`items/{kind}.prefab`, set for every drug item in `ProductCatalog.WithDefaults`), else the shared
   `items/world_item.prefab` (`ItemCatalog.WorldFallbackPrefab`: the parcel, an unknown kind, an ordinary item). An
   ordinary item whose `PrefabPath` is a prefab with a `PickupItem`, a `Rigidbody` and Network Mode Object drops as that
   prefab (so existing `PickupItem` users keep working); otherwise it drops as the shared world item carrying its data.
4. `WorldItem.Set` stores the whole `ItemData` as JSON in a host-synced string. Clients parse it lazily (`Data`). Kind,
   count, quality, name, description, paths, order id, addressee and a parcel's contents all survive.
   `ItemData.SameAs` compares two items field by field (through their JSON).
5. `OnItemDropped` is posted on the host after the item has left the bag.

Prefabs under `Assets/items/` are a box model with a tint, a box collider, a `Rigidbody`, a network object, an
`Interactable` with one `pickup` option (runs on the host) and a `WorldItem`. To give a new kind its own look, copy one,
change the model and size, and name it in the kind's `ItemDef.WorldPrefab`. The generic one is tinted with the kind's
`ItemDef.Color` at run time.

The root is Network Mode **Object** and the `Model` child is Network Mode **Snapshot**, never Never: a `Never` child of a
network object is not sent, so clients got every dropped item with no model (invisible, while the host saw it). Any child
a client must have — the model, the collider — has to be Snapshot, the way `mailbox.prefab` has it.

## Picking up

The Pick up option runs on the host (the `Interactable` re-checks reach and line of sight from the caller's eye).
`WorldItem.OnInteract` calls `PlayerInventory.AddPartial( item )`, which plans on the slot array and writes in one step
(`PickupRules.Plan`, a pure function on plain arrays):

- A **batch** first tops up stacks of its kind that have room and that `StackRules.CanMerge` allows, then fills empty
  slots with new stacks of at most the kind's `MaxStack`, keeping the batch's own quality. It stops when nothing fits.
- An **ordinary item** (no kind) takes one empty slot or nothing.
- `AddPartial` returns how many went in. All of it: the world item is marked taken and destroyed. Part of it: the bag keeps
  that part and the world item stays with the remainder (count reduced and re-synced; the prompt updates). None: the pickup
  is refused with "Your bag is full." or "Nothing more fits." and the world item is unchanged. The player is told in chat
  (`PlayerCrime.Tell`), for example "Picked up 10 of 12 Static tabs. 2 left on the ground."
- Both the picked part and the remainder keep kind, quality, order id, addressee and contents. Nothing is created or lost:
  the count that goes in is exactly the count taken off the world item.
- The host handles one pickup at a time; the `taken` flag is set before the destroy.

The prompt reads "Pick up {name} x{count}" with the same details as the inventory (`ItemCatalog.Describe`). It is greyed
out with "Your bag is full." when `PlayerInventory.RoomFor` (the dry run, no changes) says not even one fits, and says
"Only N fit in your bag." when part fits.

## Cash

Cash is a world item with kind `cash` (`ItemCatalog.Cash`, prefab `items/cash.prefab`) whose dollars are `ItemData.Amount`; it is made by `CashDrops.Drop`, never by the bag. It counts toward `MaxInScene`. `WorldItem` picks it up in its own branch (`PickUpCash`): the dollars go to the interactor's wallet, not the bag, as many as the wallet cap allows (the rest stays, `Amount` reduced and re-synced), and there is no bag-full refusal. `ItemCatalog.Make` and `MakeStacks` return nothing for cash (`MakeCash` makes a pile). `WorldItemRules.CleanItem` keeps a cash item with its amount cut to 1..`MaxMoney` and a count of 1, drops one with no dollars, and sets `Amount` to 0 on every other kind. The details line is the amount, "$1,250". See [currency.md](currency.md), "Cash on the ground".

## The merge rule

`StackRules.CanMerge( a, b )` is the one place that says whether the game may combine two stacks on its own: same kind,
both stackable (`MaxStack` above 1), same order id and addressee, and, when the kind's `ItemDef.QualityMatters` is set,
the same quality. `QualityMatters` is true for every drug item (`ProductCatalog.WithDefaults`), so stacks of different
quality are never auto-merged and no quality is ever averaged. A kind with it off merges whatever the quality (the stack in
the bag keeps its quality). Use `CanMerge` or `PickupRules.Plan` for anything that puts a stack into the bag on its own.
The player's own Shift-combine (`StackRules.Plan`) still averages, because they chose it. `PlayerInventory.Add`, `AddItem`
and the storage code do not top up stacks and were left as they were.

## Saved between sessions

The host keeps every dropped item in `world_items.json` (the data folder, like `mailboxes.json`; `world_items_test.json`
under `progress_test_data on`). Code: `WorldItemRegistry` (the file and the list), `WorldItemRules` (the pure rules),
`WorldItemKeeper` (a host-only component on each tracked object) and `WorldItemSystem` (restore and debounce). Only the
host reads or writes it; clients just see the networked objects.

- **Format.** `{ "Version": 1, "Items": [ ... ] }`, oldest first. Each entry has `Id` (a unique id, the key), `Scene` (the
  scene's resource path, like `scenes/home.scene`, from `WorldItemRegistry.SceneKey`), `Prefab` (the prefab it was made
  from, for the record), `Item` (the whole `ItemData` as JSON: kind, count, quality, `OrderId`, `To`, sender and order
  facts, `Contents`), and `Position` and `Angles`. The file is written with `FileProgressStore.WriteSafely` (a `.bak` of the
  previous good copy, read back if the file is damaged).
- **What is tracked.** `WorldItem.Spawn` calls `WorldItemRegistry.Track` for everything it makes: a `WorldItem` and an
  ordinary item that dropped as its own `PickupItem` prefab. Items pre-placed in a scene are not part of this.
- **When it is written.** A change only marks the registry dirty; `WorldItemSystem` writes the file 3 seconds
  (`SaveDelay`) after the last change, never every frame. The changes are: a drop (`Track`), a pickup of all (`Forget`),
  a partial pickup (`SetItem`, the new count), an item that went out of bounds (`GameManager`, `Forget`), and an item that
  came to rest. `WorldItemKeeper` watches the rigidbody and reports the position only after it has been still
  (under 2 units/s) for half a second and lies more than 1 unit from the last saved place, so a tumbling item is not
  saved mid-air and a pushed one is saved again where it stops. The system also writes once when the scene goes
  (`Dispose`).
- **Not saved.** A position that is not finite or beyond 100000 units on any axis. There is no "below the map" test: The
  Wired lies under the map in the same scene, and an item that falls out of the world is destroyed by the engine's
  out-of-bounds rule, which also forgets it. An object destroyed by anything else (not a pickup, not out of bounds) is
  not forgotten and comes back at the next start.
- **Restore.** One second after play starts on the host, `WorldItemSystem` calls `WorldItemRegistry.Restore( scene )`: for
  each entry whose `Scene` is this scene and whose id is not already live (`WorldItemKeeper.ItemId`), it calls
  `WorldItem.Spawn` with the saved item, position and rotation, zero velocity and the saved id: the same prefab choice and
  fallback as a drop, then the rigidbody wakes and settles at (or just above) where it lay. A second call, or a hot
  reload, makes nothing new. Entries of other scenes are left in the file untouched.
- **Sanitising on load** (`WorldItemRules.Sanitise`). Malformed entries are dropped, never thrown on: no scene, no item,
  a bad position, a repeated id. A missing id gets a new one. The item is rebuilt: a known kind keeps its count within the
  kind's stack size and its quality within 0..100; an unknown kind is kept only if it has a name, as an ordinary item
  (one, no kind); texts are cut, a parcel's contents are capped, a prefab path that is not a plain `.prefab` path is
  cleared (a prefab that cannot be made falls back to the shared world item). A scene keeps at most `MaxInScene` entries:
  the oldest are dropped, in the file too.
- **Wipe.** `WorldItemRegistry.Wipe( scene )` destroys every world item of the scene and empties the file. The console
  command `world_items_clear` calls it (editor, host). There is no season wipe in the game yet: when one is added (the
  place that resets `CrimeRecord` and `mailboxes.json`), call `WorldItemRegistry.Wipe` there too.
- **Hot reload.** The registry's state is in non-readonly statics, so a hot reload keeps it.
- **Frozen with the Physgun.** The Physgun freezes an object by setting `Rigidbody.MotionEnabled = false` on the host (and
  unfreezes by setting it back, also when it picks the object up). Both go through `PhysFreeze.Set`
  (`Code/Weapons/PhysGun/PhysFreeze.cs`; the only change to the ported `Physgun.cs`), which also writes `Frozen` and where
  the item stands now to the entry (`WorldItemRegistry.SetFrozen`, same debounce as a moved item). `Frozen` is additive:
  a file without it loads as false. A restore passes it to `WorldItem.Spawn( ..., frozen )`, which calls
  `PhysFreeze.Apply`: the same assignment the Physgun makes, so the item comes back not falling and not pushable and the
  Physgun can unfreeze it. `WorldItemKeeper` treats a body with motion off as still, so a frozen item is never reported as
  moving. Cash piles and parcels are world items and are covered.
- Parcels persist like anything else: a packed order stays `Packed` until someone picks the parcel up and posts it.
