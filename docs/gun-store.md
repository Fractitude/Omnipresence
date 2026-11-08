# The gun store

A Navi app (`Assets/navi/shop/gunshop.napp`, panel `OmnipresenceApps.GunStore`) meant as a **kiosk** on a computer in a
gun shop (the kiosk is set up by hand on the computer, see "Kiosk computers" in `Libraries/navi/README.md`). Two tabs:
**Weapons** and **Ammunition**. The player looks an item over in 3D (drag to turn and tilt, wheel to zoom) and buys it with
cash. It goes straight into their inventory; there is no delivery point.

| Piece | Code | Notes |
| --- | --- | --- |
| **The catalog** | `Code/GunStore/GunCatalog.cs` | `GunItem( Id, Kind, Name, Description, Price, Preview, Stats, AmmoPath, Amount )`. Data only: a new weapon or pack is one more line. The weapons are the ones in `Assets/weapons/` except the physgun and the camera (tools). Prices are first guesses. |
| **The sale** | `Code/GunStore/GunSeller.cs` | `GunSeller.Buy( buyer, item )`, host only. Cash is checked, the money is taken (`Transactions.Pay` to `gun-store`, kind `GunPurchase`), the goods are handed over, and the money is paid back (`Receive`, kind `Refund`) if they could not be. Logged as a `purchase` of kind `weapon` or `ammo` in the stats log. |
| **The picture** | `Code/GunStore/GunPreviewScene.cs` | The 3D scene the panel draws, built like the car dealership's. Weapons draw their `_wm` world-model prefab, ammo its ammo box model. |
| **The app** | `Code/Apps/GunStore/` | `GunStoreViewModel` (the logic, host only), `GunShelf` (what is on show, per computer), `GunStoreUiState` (what is drawn), `GunStore.razor` (the panel). Shared like the car dealership. |

## Setting a shop up

1. Give the computer the **computer tag** `store`. The manifest lists the same tag, so Packages doesn't offer the app and the
   app refuses to work (and `GunSeller` refuses to sell for it) on a computer without it.
2. Put `navi/shop/gunshop.napp` in the computer's `InstalledApps`, and for a kiosk in `OnlyApps` and `OpenApp`.

## Weapons

A weapon is given with `WeaponInventory.Pickup` (what the `give` command does), which also gives its starting ammo. A weapon
the player already carries is refused ("Already carried"), and nothing is charged if it could not be handed over.

## Ammunition

A pack is a number of rounds of one ammo resource: `9mm` 30, `rifle` 60, `shotgun` 18, `sniper` 10, `rocket` 2, `grenade`
5. It is given with `WeaponInventory.GiveAmmo`, so the buyer gets the pickup notice. The reserve is capped by the ammo's
`MaxReserve`: with room for only part of a pack, only that part is given and **the price is the same part of the pack's price**
(`GunSeller.PriceFor`). With no room at all the button says "Reserve full" and nothing is charged.

## What is on show is the computer's

Like the car dealership: the screen is the same for everyone and is never "locked". With nobody seated it still shows the
shelves and the item turning by itself. The tab, the item picked on each tab and how the picture is turned live in a
`GunShelf` on the host, one per computer (a static dictionary keyed by the computer's object id), and are published in
`GunStoreUiState`; the turning is sent without a new `Revision`, exactly as in the car dealership (see `car-dealership.md`,
"The 3D picture"). Only the wallet and what the buyer carries (`Carried`, `120 / 300 in reserve`) are the seated player's.

## Security

The panel sends only an item id (`select`, `buy`), a tab name and the picture's numbers (clamped on the host). The price, the
cash, the tag, the room and who buys (always the player in the computer's seat) are all checked on the host.
