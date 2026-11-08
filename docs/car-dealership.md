# The car dealership

A Navi app (`Assets/navi/shop/cardealership.napp`, panel `OmnipresenceApps.CarDealership`) on a computer in a car lot.
The player sees the cars in a list, looks one over in 3D (turn it, tilt the view, zoom), and buys it with cash. The car
is made at a free **delivery point** near the computer, on the ground, facing the way the point faces.

| Piece | Code | Notes |
| --- | --- | --- |
| **The catalog** | `Code/Dealership/CarCatalog.cs` | `CarModel( Id, Name, Description, Price, Prefab, Ident )`. Data only: a new car is one more line. The cars are the buggy and the integrated crosschainer sents (`Assets/sents/crosschainer/`). Prices and descriptions are first guesses. |
| **The sale** | `Code/Dealership/CarDealer.cs` | `CarDealer.Buy( buyer, car, around )`, host only: cash is checked, a free point is found, the money is taken (`Transactions.Pay` to `car-dealership`, kind `CarPurchase`), the car is made and the buyer gets the title. If the car can't be made the money is paid back (`Receive`, kind `Refund`). Logged as a `purchase` of kind `car` in the stats log. |
| **The place** | `Code/Dealership/CarDelivery.cs` | Finds the delivery points, tells if one is free, and works out where the car stands. |
| **The picture** | `Code/Dealership/CarPreviewScene.cs` | The 3D scene the panel draws. |
| **The paint** | `Code/Vehicles/CarPaint.cs` | A component on the car prefab that tints its body. See "Paint". |
| **The app** | `Code/Apps/CarDealership/` | `CarDealershipViewModel` (the logic, host only), `CarDealershipUiState` (what is drawn), `CarDealership.razor` (the panel). Shared like the Hardware Store. |

## Setting a lot up

1. Give the computer the **computer tag** `store` (Inspector, Apps section, `ComputerTags`). The manifest lists the same
   tag, so Packages doesn't offer the app and `Install` refuses it on any other computer, and the app itself refuses to work (and
   `CarDealer` to sell) on a computer without it.
2. Put the app on the computer: list `navi/shop/cardealership.napp` in `InstalledApps` (or, for a kiosk, in `OnlyApps` and
   `OpenApp`; see "Kiosk computers" in `Libraries/navi/README.md`).
3. Tag one or more objects **`delivery_point`**, each with a `BoxCollider` the size of a bay, within **300 units** of the
   computer (`CarDelivery.Range`, measured from the computer's origin to the point's origin). The Depot's store pickup uses the
   same tag (`DeliverySpot.Tag`), so a bay that is near both a Depot computer and the dealership serves both.

## Which bay, and what free means

The points within `Range` of the computer, nearest first; the first one with room is used. A point is **free** when nothing is
inside its box: no body that moves (a prop, a car), no player and no NPC. The ground, walls and anything fixed to the map don't
count, nor does the point itself or its children, and the bottom 6 units of the box are left out so the floor under it is never
"something in it". The check asks the engine for what is in the box's outline (`Scene.FindInPhysics`), then tests each collider
against the box's own axes, so a bay turned to any yaw is measured as it is. A car that has been bought stays in its bay until it
is driven away, so it takes the bay.

With no free point the app says **"There is no free space to deliver a car to."** and takes no money. With no point at all near
the computer it says "There is no delivery point near this computer." The app also shows "2 of 3 delivery bays free", counted
twice a second.

## Where the car stands

The car is made at the middle of the point's box, and put down on the ground below it: a ray goes straight down from the middle
of the box (ignoring the point and players and NPCs), and the car's lowest point (the bottom of its prefab's bounds, as for
`spawn_sent`) is put 2 units above the hit. It gets the point's **world rotation exactly**, so the car faces along the point's
forward (+X) axis: turn a bay so its forward points the way cars should face. With no ground below the box, the box's bottom is used.

## Buying

The assets of an integrated sent are mounted from the workshop first (`SentLocal.Mount`, asynchronous), and the sale is checked
again afterwards: the player may have stood up and the bay may have been taken. Cash, room and spawn then happen in one step.
The car is made like any sent (`GameObject.Clone` of its prefab, then `NetworkSpawn`), with an `OwnershipComponent` whose
`DefaultOwner` is the buyer ([ownership.md](ownership.md)). The title is written under the car's object id, which changes every
session, and a bought car is not saved, so the record outlives the car; there is no car saving yet.

## The 3D picture

`CarPreviewScene` is a small scene of its own (a pivot, a light and a camera) drawn by a `ScenePanel` that is given the scene
before it exists (AGENTS.md, "UI pitfalls"). The car in it is a copy of the prefab with only what is drawn kept
(`ItemIcons.CreatePrefabContent`), so it does nothing. The scene is registered in `CarPreviewScene.IsPreviewScene`, which
`CharacterMenu.IsPreview` asks, so `Hud.Applies` and the Wired system keep out of it.

- **Which car is shown is shared** (`CarDealershipUiState.SelectedId`): everyone near the screen sees the car the player picked.
- **Turning is not.** Dragging on the picture turns the car (across) and tilts the view (up and down), the wheel zooms, and the car
  turns by itself after 3 seconds untouched. All of it is that client's own, like a scroll position: a player watching the
  screen sees the car they would see, not where the seated player has turned it.
- A sent's assets are mounted on each client the first time its car is shown; the car appears when they are there.
- The picture keeps the car's `CarPaint` (`ItemIcons.CreatePrefabContent( …, keepPaint: true )`), so it is painted by the same code as the car in the world, and `CarPreviewScene.Show( car, paint )` sets the colour on it.
- The wheels of a car posed by its code (the buggy's bones) are shown as the prefab has them.

## Paint

`CarPaint` (`Code/Vehicles/CarPaint.cs`, "Car Paint" in the component list) is on every crosschainer car prefab, after
`CuratedVehicle`. It has three synced colours, all editable in the prefab's inspector (they are the car's stock look):

- `Paint` tints the car's `primary` materials and `Secondary` the `secondary` ones (`g_vColorTint`).
- `GlassTint` tints the windows: its colour is the tint and its **alpha is how strong it is**, so the default (a pale grey at
  alpha 0.05) is clear, and black at alpha 1 is a blacked-out window. Each window slot (a material on the `drift_glass`
  shader, or `dark_cabin.vmat`) is put on one copy of `vehicles/shared/clear_glass.vmat` (the `drift_lens` shader), whose
  `LensTint` and `LensOpacity` it sets. The prefab's own window slots also name that material, so a car without `CarPaint`
  still has clear glass.

`CarLook( Paint, Secondary, Glass )` is the three together (`CarPaint.Look`, `SetLook`), with `Format`/`Parse` for the
`r,g,b,a` string the app sends. The tint is set once when the car starts and again when a colour changes, never every frame: a
change from the host arrives through a `[Change]` handler, and one made on this machine goes through `SetLook` (a local set does
not reach the handler). It looks again for up to 3 seconds if the model was not loaded at start. **A `ScenePanel` draws its scene but does not tick it**, so the dealership's picture never runs `OnUpdate`: `CarPreviewScene.Update` calls `CarPaint.Refresh()` each frame until the materials are found (without it the preview stayed unpainted, as the model is not loaded at the first frame). For the same reason the old car is removed with `DestroyImmediate`: a plain `Destroy` waits for a tick that never comes, and the next car's paint lookup found the old car's `CarPaint`, so only the first car shown could be painted. It runs in the editor too, so a
prefab shows its colours and the inspector edits them live (compared each frame there only); the overrides it makes are saved
with the prefab as entries naming the stock materials: the same look, but a diff.

- **The stock look is the prefab's** (`CarCatalog.StockLook` reads it). A car whose prefab has no `CarPaint` (the buggy) cannot be
  painted, and the app shows no colour controls for it.
- **The player picks in the app** (`paint` action: the part, `body`, `trim` or `glass`, and a colour or `stock`), kept by the
  host's view model per selected car (picking another car goes back to its own look) and published in `CarDealershipUiState`,
  so the preview and everyone watching show it. Body and trim are made opaque on the host.
  - **Body and trim use the engine's `ColorPickerControl`**: clicking a swatch opens it over the picture. It edits a
    `CarColorHolder` (the control needs a serialized property), and the panel sends what is picked to the host while it is
    dragged, at most every 0.08 s.
  - **Glass uses fixed swatches** (`GlassTints` in the panel): the car's own, light and dark smoke, blacked out, then blue,
    green, amber, red and purple. "Stock" puts all three back.
- **A bought car gets the look** through `CarDealer.Buy( …, look )`, set on the cloned `CarPaint` before `NetworkSpawn`, so it is
  synced with the car. The buy takes the colours on the screen when the button was pressed.
- `CuratedVehicle` only does the lamps now (headlight and taillight emission, set when it changes).

## Security

The panel sends only a car id (`select`, `buy`) and colours (`paint`, parsed as `r,g,b,a` and clamped; body and trim are made opaque). The price, the cash, the tag and the room are all checked on the host, and the
buyer is always the player in the computer's seat. No car, bay or price comes from a client.
