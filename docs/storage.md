# Storage boxes

A storage box holds what does not fit in the bag. A player walks up, opens it, and moves items between the bag and the box. Anyone may open any box: a box is not private. It belongs to the first player who opens it — that owner's record is where its contents are kept — but whoever stands at it can put things in and take things out.

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
- **Left mouse, drag:** press on an item (bag or box), drag, and release on any slot of the bag or the box. An empty slot takes the item; an occupied slot swaps the two items, also between bag and box (the target's item goes to the slot the dragged one came from). Reordering inside the box and inside the bag works the same way.
- **Shift while dragging combines** two stacks of the same drug product or raw material (counts added up to the kind's max stack, 256 for the drugs' items; quality averaged weighted by quantity, rounded). **Ctrl while dragging splits** the stack in half (rounded down) into an empty slot; Ctrl + Shift adds that half to another stack. Both work between bag and box in every direction. The keys are read when the mouse is released and a label by the dragged icon says what will happen. Parcels and ordinary items do not stack. The rules are in [crime.md](crime.md), "Combining and splitting stacks". A ghost icon follows the mouse and the slot under it is highlighted. Releasing outside any slot, on the slot it came from (a plain click, which does nothing), pressing Escape (once; the next Escape closes the panel) or closing the panel cancels the drag.
- **Right click** on an item sends it to the other side: from the bag to the first free box slot, from the box to the first free bag slot.
- Hover an item for its details: name, description, batch lines, and what a parcel holds.
- A full box or a full bag says so in the panel and in chat; nothing moves.
- Parcels may be stored. A stored parcel is not posted: Send all parcels sends the bag's parcels only.

## Security

- All moves run on the host as `[Rpc.Host]` requests on the box (`RequestRefresh`; right click: `RequestStore( bagSlot )`, `RequestTake( boxSlot )`; drag: `RequestStoreTo( bagSlot, boxSlot )`, `RequestTakeTo( boxSlot, bagSlot )`, `RequestMoveInBox( from, to )`, `RequestMoveInBag( from, to )`; with Shift or Ctrl: `RequestStack( fromSide, from, toSide, to, mode )`, sides 0 bag and 1 box). The box is the network object the request goes to, so only slot indices travel.
- The host takes the player from `Rpc.Caller` (`PlayerCrime.For`), never from the message. It checks the record is loaded, and that the player stands within `StorageBox.Reach` (300) of THIS box, then calls `StorageRules`.
- **Anyone may use any box.** There is no ownership check: the box is the only gate, and it is where the box is, not whose it is. The one thing that must be checked is which record the contents are in, `StorageBox.RecordOf( ownerId )`: the owner's own player object when they are here, else the copy the host has in memory, else the owner's record read from the store — so a box whose owner is away still holds what they left in it, and anyone can add to or empty it. Nothing is read from the caller's record.
- A move checks everything before it changes anything. `Store` and `StoreTo` remove from the bag and then put in the box (a swap first puts the displaced item in the freed bag slot, and undoes the removal if that fails); `TakeTo` does the mirror image; `Take` adds to the bag first and only then empties the box slot; `MoveInBox` swaps two entries; `Stack` plans the result in `StackRules.Plan`, checks it, and only then writes the two slots. A failure leaves both sides as they were, so an item is never lost or doubled. An item moves whole, with every field (`Kind`, `Count`, `Quality`, `OrderId`, `Contents`, paths).
- The contents are in the owner's record on the host. The host sends them to the player who asked, to nobody else (`Rpc.FilterInclude`); the box's synced state is only the owner's Steam id. A client keeps the contents only while the panel is open.
- The claim is made by the host on the first request from a player in reach, written to `storage.json` at once.

## Saved

- The contents live in `CrimeRecord.Storage`, a section of the player's record next to the bag's items. They are saved with the record (the debounced save), restored when the player loads, and a season wipe of the crime state resets them with the rest. A record with no `Storage` (an old save) loads as empty.
- Nothing needs the player's object: the box reads and writes the owner's record. When the owner is here their own player object saves it (`PlayerProgress.NotifyChanged`); when they are away the box writes it (`PlayerProgression.SaveAsync`) and lets go of the record unless something of theirs is still waiting on the clock. The record is checked on load (`StorageRules.Clean`): ids must be plain, at most 20 boxes per player, slot indexes 0 to 95 and unique, an item of an unknown kind is dropped, counts (a stack above its kind's size is split into several stacks in free slots of the same box, up to slot 96, not clamped; 9999 tabs become 39 x 256 + 15) and qualities (0 to 100) are clamped, parcel contents are capped, text is cut short. An ordinary item (not from the catalogue) keeps its name, description and paths.
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

The prefab is `Assets/crime/storage_box.prefab`: a root with `StorageBox`, `Interactable` (range 200) and Network Mode **Object**, and a child with a model and a collider (like `packing_table.prefab`), in Network Mode **Snapshot**. Make and place them with the editor tools, not by hand, and never in play mode: `create_asset` / `asset_write` for the prefab, `create_game_object` with the prefab for the scene, then `save_scene`. Each box in the scene has its own id (its object id), so every placed box is its own storage. A box bought in the Depot has a permanent id instead (`PlacedObject.ObjectId`, see [depot.md](depot.md)): it keeps its contents wherever it is moved. `StorageBox.BoxId` is `PlacedObject.IdOf( this )`.

**The child's Network Mode matters as much as the root's.** The engine builds a client's copy of a network object from its networked children only: a child in Network Mode **Never** is not sent, so clients got the box with no children at all — no model to see, no collider to look at, so nobody but the host could see or use it (the same bug made dropped items invisible to clients). A child a client must have is Snapshot, as the model child is here and in `mailbox.prefab`.

## Not done

- Boxing items in: nothing stops a player from emptying a box they come across — there is no lock, no permission and no crime cost for it yet.
- Stacking of seeds, parcels and ordinary items (only drug products and raw materials combine).
