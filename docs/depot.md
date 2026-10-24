# The Depot: buying big objects

A storage box, a grow tray, a press bench or a packing table is not an item in the bag: it is an object the player puts
down in the world with the Physgun. The **Depot** (a Navi system app, `Assets/navi/system/depot.napp`) sells them for
cash and has them delivered.

| Piece | Code | Notes |
| --- | --- | --- |
| **The catalog** | `Code/Placeables/PlaceableCatalog.cs` | `Placeable( Id, Name, Description, Icon, Price, Prefab, DeliverSeconds )`. Data only: a new object is one more line. Prices are first guesses (box $150, tray $400, press $600, table $350; 2 to 5 minutes). |
| **The shop** | `PlaceableShop.cs`, `Code/Apps/Depot/` | `Buy( buyer, id, now )` on the host: the order is made first, then `Transactions.Pay` to `depot`; a failed payment takes the order back. The app is Shared like Hush, works at home with the network on, and sends an object id only. |
| **The id** | `PlacedObject.cs` | A component with a permanent `ObjectId` (synced from the host). `PlacedObject.IdOf( this )` is what `StorageBox.BoxId` and `GrowTray.TrayId` use: a bought object's id, else its object id (scene-placed objects keep the id they always had). |
| **The registry** | `PlacedObjectRegistry.cs`, `PlacedObjectRules.cs` | `placed_objects.json` in the data folder (`placed_objects_test.json` under `progress_test_data`). Objects: id, placeable, buyer, scene, position, angles. Deliveries: id, buyer, arrival time. Written 3 s after the last change, atomically (`FileProgressStore.WriteSafely`). |
| **The system** | `PlacedObjectSystem.cs` | Host only. Restores the scene's objects 1.5 s after start (by id, so never twice), makes due deliveries arrive once a second, tells buyers, saves. |
| **The spot** | `DeliverySpot.cs` | Where a delivery lands. |

## Delivery

An order is in the world file, not in the buyer's record, so it arrives on time whether the buyer is playing or away
(the shop's seed deliveries sit in `CrimeRecord.Deliveries` and need `CrimeRules.NextDue`; this does not). The price of
that: a season wipe of the crime state does not clear it, so the wipe must call `PlacedObjectRegistry.Wipe( scene )`
(also `placed_objects_clear`). At most `PlaceableCatalog.MaxInTransit` (6) per player on their way.

When it is due the system picks a spot: beside an object tagged **`delivery_point`** if the scene has one (none yet: add
a tagged empty object in the editor to choose the place), else beside the buyer's mailbox (or any mailbox that is not
another player's), else in front of a spawn point. Rings of 8 spots at 130 to 340 units around the anchor are tried; a
spot is used if it is within 24 units of the anchor's height (not on a wall's top), the object's box fits there and
nothing stands in it, and no object was put there in the same pass. With no free spot the delivery waits and is
retried. The buyer is told in chat ("Your Storage Box has arrived beside your mailbox."), at once if they are playing, or
the next time they are here.

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
cargo within 400 units, the object offers "Load into <vehicle>" (one row per vehicle, by type name), and the cargo window
unloads it behind the vehicle with the same id ([storage.md](storage.md), "Vehicle cargo").

## Ownership

`BuyerId` and the record's `Owner` are the buyer's Steam id, kept for the ownership design
([plans/ownership.md](../plans/ownership.md)). They grant nothing: a bought box is claimed by its first use exactly like
a scene-placed one (`StorageRegistry`), and a tray shows each player their own crop.

## Limits

- 200 placed objects per scene (`PlacedObjectRules.MaxInScene`, the oldest records go).
- Not saved: a position that is not finite or beyond 100000 units.
