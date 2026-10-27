# The Depot: buying big objects

A storage box, a grow tray, a press bench or a packing table is not an item in the bag: it is an object the player puts
down in the world with the Physgun. The **Depot** (a Navi app, `Assets/navi/game/depot.napp`, installed from
Packages) sells them for cash and has them delivered.

| Piece | Code | Notes |
| --- | --- | --- |
| **The catalog** | `Code/Placeables/PlaceableCatalog.cs` | `Placeable( Id, Name, Description, Icon, Price, Prefab, DeliverSeconds )`. Data only: a new object is one more line. Prices are first guesses (box $150, tray $400, press $600, table $350; 2 to 5 minutes). |
| **The shop** | `PlaceableShop.cs`, `Code/Apps/Depot/` | `Buy( buyer, id, now, destination, around )` on the host: the order is made first, then `Transactions.Pay` to `depot`; a failed payment takes the order back. The app is Shared like Hush, works at home with the network on, and sends an object id and one of the two words for where to deliver, never a place. |
| **The id** | `PlacedObject.cs` | A component with a permanent `ObjectId` (synced from the host). `PlacedObject.IdOf( this )` is what `StorageBox.BoxId` and `GrowTray.TrayId` use: a bought object's id, else its object id (scene-placed objects keep the id they always had). |
| **The registry** | `PlacedObjectRegistry.cs`, `PlacedObjectRules.cs` | `placed_objects.json` in the data folder (`placed_objects_test.json` under `progress_test_data`). Objects: id, placeable, buyer, scene, position, angles. Deliveries: id, buyer, arrival time, where it goes and
the terminal it was ordered at. Written 3 s after the last change, atomically (`FileProgressStore.WriteSafely`). |
| **The system** | `PlacedObjectSystem.cs` | Host only. Restores the scene's objects on the first tick the scene is ready (`SceneReadiness`, in batches of 50; about 200 ms after the system starts, where it used to wait 1.5 s), by id so never twice, makes due deliveries arrive once a second, tells buyers, saves. |
| **The spot** | `DeliverySpot.cs` | Where a delivery lands: a delivery point for a store pickup, the buyer's mailbox for a home delivery. |

## The two places

The app asks where the next order goes, in a row of two at the top of its window, and `PlaceableShop.Buy` settles it on
the host. **Collecting at the store is the default**: it is chosen wherever a bay is in reach, and home only when the
player picks it or there is no bay near the computer.

- **Collect at the store, now.** The object is put down at a delivery point within `DeliverySpot.Range` (1500 units,
  38 m) of the computer the order is placed at, in the same call that takes the money. The choice is only offered where
  the app can see a bay, and a bay has to have room before the money is taken, so nothing is bought that would have to
  wait.
- **Deliver home, later.** Mailed, as it always was: it arrives beside the buyer's mailbox after the object's
  `DeliverSeconds`. The row says the range in minutes ("Mailed to your mailbox in 2-5 mins."), the item's own row in
  the shop its exact time. Where the mailboxes are full it arrives in front of a spawn point.

A delivery point is any object the level tags **`delivery_point`**; an empty is enough, and its yaw is the way round the
deliveries are laid, so a bay should face out of its wall. None is tagged yet: a store needs a tagged empty per bay and
a computer that has the Depot. The Hardware Store's own computer is a kiosk with only its app on it
([hardware.md](hardware.md)), so it needs a second one.

## Delivery

An order is in the world file, not in the buyer's record, so it arrives on time whether the buyer is playing or away
(the shop's seed deliveries sit in `CrimeRecord.Deliveries` and need `CrimeRules.NextDue`; this does not). The price of
that: a season wipe of the crime state does not clear it, so the wipe must call `PlacedObjectRegistry.Wipe( scene )`
(also `placed_objects_clear`). At most `PlaceableCatalog.MaxInTransit` (6) per player on their way.

When it is due the place is picked: for a store pickup, the delivery points in range, nearest first; for a home
delivery, beside the buyer's mailbox (or any mailbox that is not another player's), else in front of a spawn point. At
each of those places, in order:

1. **The ground**, on rings of 8 spots at 0, 130, 200, 270 and 340 units around the anchor. A spot is used if it is
   within 24 units of the anchor's height (not on a wall's top), the object's box fits there and nothing stands in it.
2. **A stack**, at the anchor itself: on top of whatever already stands there, laid flat (the anchor's yaw, level), if the
   top is no more than `MaxStackRise` (300 units, five of the biggest object) above the floor and the box fits above it.

The ground is tried at every place before a stack at any of them, so an object only goes on top when there is no floor
left anywhere. With no free spot the delivery waits and is retried.

Everything put down at a place is turned to that place's yaw, and turned a quarter more when the object's longest side
runs along its own left axis (`PlacedObjectRegistry.LongSideIsLeft`), so the deliveries at a bay all point the same way,
long side first and level. A square object is left as the prefab has it, and so is one whose bounds cannot be read yet.
A restore turns nothing: a saved object comes back exactly as it was left.

**The stack is what keeps a bay that someone has blocked usable.** An object left on a delivery point, a car parked over
it, a wall built on it: the next delivery goes on top instead of failing, so a bay takes five objects of the blocker's
own before it is full, and any other bay in range is tried first. Past that it is the level's job: three or four bays
per store is much harder to shut down than one.

Two deliveries in the same pass are the one case the physics world cannot answer, because an object that was just made
has no body yet: the spots chosen are kept in a list, and a stack climbs over them (`DeliverySpot.AboveTaken`).

The buyer is told in chat ("Your Storage Box has arrived beside your mailbox."), at once if they are playing, or the next
time they are here.

## The object in the world

`PlacedObjectRegistry.Make` clones the prefab disabled, adds a `PlacedObject`, adds a `Rigidbody` to the root if there is
none, turns every collider's `Static` off (the prefabs' colliders are static, as a scene-placed object should be), then
enables it, network-spawns it, and sets the orphan mode to Host so it is not destroyed when the player who last held
it leaves. Its children are Snapshot so clients get the model and collider (see AGENTS.md). A rigidbody with a
non-static collider is what the Physgun grabs.

`PlacedObject` (host) writes down where the object comes to rest (still for half a second, moved more than 1 unit), as
`WorldItemKeeper` does. An object the engine destroys out of bounds is not forgotten: it comes back at its last
resting place at the next start.

## Moving it

A bought object has a `Rigidbody`, so the Physgun moves it. It can also go in a vehicle: with a vehicle that has a
cargo area within 50 units (`VehicleCargo.LoadReach`) and in clear sight, the object offers "Load into <vehicle>" (one row per vehicle, by type name), and the cargo window
unloads it behind the vehicle with the same id ([storage.md](storage.md), "Vehicle cargo").

## Putting a cargo bed on a car

A car has cargo only if its prefab has one, placed by hand. On a new car:

1. Open the car's prefab in the editor. Add a child GameObject (call it "Cargo bed") under the car's root and set its Network Mode to **Snapshot** (never "Never": clients would not get it).
2. Add the component **VehicleCargo** to it. It adds a `BoxCollider` (trigger) and an `Interactable` by itself. Set the Interactable's **Range** to about 250.
3. Move and size the **BoxCollider** over the car's bed (the area things are loaded into). In the scene view it is drawn orange and labelled "Cargo bed". The box is the cargo area: you look at it to open the cargo, and an object must be within `LoadReach` of it to be loaded.
4. Set the properties: **TypeName** ("Buggy", shown in "Load into Buggy"; empty uses the car's name), **Columns** and **Rows** (the grid), **MaxBigObjects**, and if wanted **LoadReach** (default 50) and **UnloadMargin** (default 6). Save the prefab.

The car must have a Rigidbody (the bed finds its vehicle as the nearest rigidbody above it). A car without the component has no "Manage cargo", is never listed in "Load into ...", and spills nothing. The buggy's bed is at local (-62, 0, 36), 52 x 70 x 44, 6 x 4 slots, 3 big objects.

## Frozen by the Physgun

Freezing an object with the Physgun (`PhysFreeze.Set`, see [world-items.md](world-items.md)) writes `Frozen` and its place to
its entry in `placed_objects.json`; a restart makes it again frozen where it stood (`PhysFreeze.Apply` on restore), and
unfreezing writes `Frozen: false`. An object loaded into a vehicle and unloaded comes out **unfrozen**: `PlacedObjectRegistry.Create`
clears the flag, because it is put on the floor again. A frozen object still aboard when the game ends comes back frozen at its
last resting place.

## Ownership

A bought object's legal owner is its buyer: `PlacedObjectRegistry.Make` gives it an `OwnershipComponent` whose
`DefaultOwner` is the buyer, and the first run writes that title to `ownership.json` ([ownership.md](ownership.md)).
The title is the record there; `BuyerId` here and `PlacedObjectRecord.Owner` are copies kept for this file's own sake.

A title still grants nothing to use: a bought box is claimed by its first use exactly like a scene-placed one
(`StorageBox.CanUse`, `StorageRegistry`), and a grow tray's crop is the world's and the tray's rather than the owner's
(`GrowTray`, in `trays.json`): anyone may plant in it and whoever harvests it keeps the raw. Moving a container's claim onto
its title needs its contents to move first: they live in the owner's record, and a sale must not leave them behind.

## Limits

- 200 placed objects per scene (`PlacedObjectRules.MaxInScene`, the oldest records go).
- Not saved: a position that is not finite or beyond 100000 units.
