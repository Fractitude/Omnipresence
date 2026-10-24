# Storage boxes

A storage box holds what does not fit in the bag. A player walks up, opens it, and moves items between the bag and the box. A box belongs to the first player who opens it.

## What exists, and where

| Piece | Code | Notes |
| --- | --- | --- |
| **The box** | `Code/Crime/StorageBox.cs` | A component with `Columns` and `Rows` (default 6 x 4 = 24 slots, at most 12 x 8). It adds one `Interactable` option, "Open storage", which runs on the player's own client and opens the panel. It also holds the `[Rpc.Host]` requests. |
| **The panel** | `Code/Crime/StorageMenu.razor` (+ `.scss`) | A `MenuWindow` that `HudSystem` puts on the HUD object of every client (see [ui.md](ui.md)). Bag on the left, box on the right. Registered with `GameMenus` as `storage`, so the crosshair and prompts stand down. It takes the player's controls while open (`MenuControls.Free`) and closes on Close, Escape, walking out of reach, dying or `OnDisabled`. |
| **The rules** | `Code/Crime/StorageRules.cs` | Pure functions on a `CrimeRecord` and a `PlayerInventory`: `Store`, `Take`, `MoveInBox`, `RemoveParcelsOf`, `Clean`. No box, no connection. |
| **The record** | `Code/Crime/StorageRecord.cs`, `CrimeRecord.Storage` | One `StorageBoxRecord` per box (by box id), each a list of `{ Slot, Item }`. Only filled slots are listed. |
| **The claims** | `Code/Crime/StorageRegistry.cs` | `storage.json` in the data folder: box id to Steam id. Read by the box when it starts, so an owner who is away keeps their box after a restart. A player may own many boxes. `MailboxRegistry` is untouched. |

## Using it

- Look at the box and use it (**Open storage**).
- **Left mouse, drag:** press on an item (bag or box), drag, and release on any slot of the bag or the box. An empty slot takes the item; an occupied slot swaps the two items, also between bag and box (the target's item goes to the slot the dragged one came from). Reordering inside the box and inside the bag works the same way. A ghost icon follows the mouse and the slot under it is highlighted. Releasing outside any slot, on the slot it came from (a plain click, which does nothing), pressing Escape (once; the next Escape closes the panel) or closing the panel cancels the drag.
- **Right click** on an item sends it to the other side: from the bag to the first free box slot, from the box to the first free bag slot.
- Hover an item for its details: name, description, batch lines, and what a parcel holds.
- A full box or a full bag says so in the panel and in chat; nothing moves.
- Parcels may be stored. A stored parcel is not posted: Send all parcels sends the bag's parcels only.

## Security

- All moves run on the host as `[Rpc.Host]` requests on the box (`RequestRefresh`; right click: `RequestStore( bagSlot )`, `RequestTake( boxSlot )`; drag: `RequestStoreTo( bagSlot, boxSlot )`, `RequestTakeTo( boxSlot, bagSlot )`, `RequestMoveInBox( from, to )`, `RequestMoveInBag( from, to )`). The box is the network object the request goes to, so only slot indices travel.
- The host takes the player from `Rpc.Caller` (`PlayerCrime.For`), never from the message. It checks the record is loaded, that the player stands within `StorageBox.Reach` (300) of THIS box, and that they may use it, then calls `StorageRules`.
- `StorageBox.CanUse( steamId )` is the one place that decides who may open a box: the owner, or anyone while it is unclaimed. Raiding will change it there. Anyone else gets "This isn't your storage." and the panel closes.
- A move checks everything before it changes anything. `Store` and `StoreTo` remove from the bag and then put in the box (a swap first puts the displaced item in the freed bag slot, and undoes the removal if that fails); `TakeTo` does the mirror image; `Take` adds to the bag first and only then empties the box slot; `MoveInBox` swaps two entries. A failure leaves both sides as they were, so an item is never lost or doubled. An item moves whole, with every field (`Kind`, `Count`, `Quality`, `OrderId`, `Contents`, paths).
- The contents are in the owner's record on the host. The host sends them to the player who asked, to nobody else (`Rpc.FilterInclude`); the box's synced state is only the owner's Steam id. A client keeps the contents only while the panel is open.
- The claim is made by the host on the first request from a player in reach, written to `storage.json` at once.

## Saved

- The contents live in `CrimeRecord.Storage`, a section of the player's record next to the bag's items. They are saved with the record (the debounced save), restored when the player loads, and a season wipe of the crime state resets them with the rest. A record with no `Storage` (an old save) loads as empty.
- Nothing needs the player's object: the box reads and writes the owner's record. The record is checked on load (`StorageRules.Clean`): ids must be plain, at most 20 boxes per player, slot indexes 0 to 95 and unique, an item of an unknown kind is dropped, counts (1 to 9999) and qualities (0 to 100) are clamped, parcel contents are capped, text is cut short. An ordinary item (not from the catalogue) keeps its name, description and paths.
- A cancelled order (the deadline passed) removes its parcel from the bag and from every box, for a player who is playing and for one who is away (`CrimeRules.Cancel` calls `StorageRules.RemoveParcelsOf` on the record).
- If a box is made smaller later, items beyond the new size stay in the record and can still be taken, but nothing new goes there.
- `progress_test_data on` also keeps the claims in `storage_test.json`.

## Tuning

| Setting | Where | Now |
| --- | --- | --- |
| Slots | `Columns` and `Rows` on the component | 6 x 4 |
| Reach | `StorageBox.Reach` | 300 |
| Panel refresh | `StorageMenu.RefreshSeconds` | 2 s |
| Boxes per player, slots per box | `StorageRules.MaxBoxes`, `MaxSlots` | 20, 96 |

## Placing a box

The prefab is `Assets/crime/storage_box.prefab`: a root with `StorageBox`, `Interactable` (range 200) and Network Mode **Object**, and a child with a model and a collider (like `packing_table.prefab`). Make and place them with the editor tools, not by hand, and never in play mode: `create_asset` / `asset_write` for the prefab, `create_game_object` with the prefab for the scene, then `save_scene`. Each box in the scene has its own id (its object id), so every placed box is its own storage.

## Not done

- Raiding another player's box (`CanUse` is the place for it).
- Stacking: two batches of the same kind stay in two slots.
