# World items: dropping and picking up

An item the player drops becomes a `WorldItem` on the ground, and picking it up gives back exactly what was dropped. World
items are **runtime only**: they are never saved, so a restart (or a map change) clears the ground, and what was dropped
is gone. There is no despawn timer; the only limit is `WorldItem.MaxInScene` (300), beyond which a drop is refused with a
message and the item stays in the bag.

## How the player drops

Drag an item out of the bag's window and let go outside it (`InventoryGrid.EndDrag` -> `PlayerInventory.Drop( slot )`).
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

## The merge rule

`StackRules.CanMerge( a, b )` is the one place that says whether the game may combine two stacks on its own: same kind,
both stackable (`MaxStack` above 1), same order id and addressee, and, when the kind's `ItemDef.QualityMatters` is set,
the same quality. `QualityMatters` is true for every drug item (`ProductCatalog.WithDefaults`), so stacks of different
quality are never auto-merged and no quality is ever averaged. A kind with it off merges whatever the quality (the stack in
the bag keeps its quality). Use `CanMerge` or `PickupRules.Plan` for anything that puts a stack into the bag on its own.
The player's own Shift-combine (`StackRules.Plan`) still averages, because they chose it. `PlayerInventory.Add`, `AddItem`
and the storage code do not top up stacks and were left as they were.
