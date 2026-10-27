# Ownership: who a thing belongs to

Every thing that can belong to somebody carries the same title: a storage box, a car, a door, a computer, a house, a
plot, a business, a licence. One component on an object (or one field in a record for a thing that is not a GameObject),
one file that keeps them, and one matrix that says what title lets a player do.

| Piece | Code | Notes |
| --- | --- | --- |
| **The title** | `Ownership.cs` | A plain class, not a component: `Kind`, `SteamId`, `OrgId`. A `[Property]` on anything that has a field, so an item, a business or a bank account can hold one as easily as a component can. `Key` is the written form (`player:76561197960287930`, `org:a1b2c3d4e5f6`, `public`, `government`, `""` for nobody). |
| **The component** | `OwnershipComponent.cs` | On the object's root, and only there. `OwnerKey` is synced from the host; `Owner` decodes it; `DefaultOwner` is the title the object is made with. `ThingId` gives an object a key of its own where its object id will not do. |
| **The registry** | `OwnershipRegistry.cs`, `OwnershipRecord.cs` | `ownership.json`: one record per thing (key, owner, since, the last 12 changes of hands with who and why). `ownership_test.json` under `progress_test_data`. Host only, written at once. |
| **The rules** | `OwnershipRules.cs` | Pure: `Allows( title, right, actor, role )`. Nothing but ownership and roles go in. |
| **The event** | `OwnershipEvents.cs` | `Global.IOwnershipEvents.OnOwnershipChanged( change, thing )`, posted on the host when a thing changes hands. |

## Nobody, the public, the government

| Kind | What it means | Who may use it | Who may change or sell it |
| --- | --- | --- | --- |
| `Nobody` | No legal owner: a free thing. Also what releasing a title sets. | anyone | anyone (claiming it is the usual move) |
| `Player` | One player, by Steam id. | the owner | the owner |
| `Organization` | The crew's property, by `OrgRecord.Id`. | every member | the founder (`OrgAction.Transfer`) |
| `Public` | A road, a park, a bench. | anyone | nobody: game code only |
| `Government` | A licence, a municipal service, a police station. | anyone | game code only |

The public and the government are not the same thing even when a player sees one answer: the game gives and takes a
government title (an auction, a repossession, a permit expiring), and does not give or take a public one, because the
public's things are not property.

## A thing's key

The key is what everything saved about a thing is keyed by, so it has to outlive a session:

- A **scene-placed object** is scene key + object id (`scenes/downtown.scene/aa0d578d-…`). The scene file keeps the
  object id.
- A **bought object** is scene key + `PlacedObject.ObjectId`, the permanent id the Depot gives it, which survives a
  restart and a car's cargo unload. `PlacedObject.IdOf` works both of those out; `OwnershipRegistry.KeyOf` is the one
  that adds the scene.
- A thing that is **not a GameObject** (a business, a licence, a named plot) is any key the caller makes:
  `OwnershipRegistry.Key( "business", "acme" )`, and the record goes in the same file.
- An object that needs a key of its own sets `OwnershipComponent.ThingId` (a car saved by a name rather than an object
  id), and the key is that verbatim.

A thing made at run time with none of those has an object id that changes every session, so its title does not survive
one. Give it a permanent id first.

## The registry is the truth

`ownership.json` holds the title. The component's `OwnerKey` is a synced copy of it so a client can read the owner
without the file; nothing writes a title anywhere but the registry:

```csharp
OwnershipRegistry.Transfer( key, Ownership.Player( buyer ), buyer, "bought at the Depot" );
OwnershipRegistry.Transfer( key, Ownership.Organization( org.Id ), actor, "the crew bought it" );
OwnershipRegistry.Release( key, actor, "left in the street" );
```

`OwnershipComponent.DefaultOwner` is only the seed: the title an object is **made** with, written down once, when the
registry has no record for it (a house in the scene set to `government`, a bought box set to its buyer). After that the
record wins, and `DefaultOwner` is never read again — otherwise a scene saved during play would carry an owner the
object no longer has.

`OwnershipComponent.Transfer` is the player's path: it checks `OwnershipRules` and the actor's organization role first,
then writes the registry. Game code that is allowed to do the thing anyway (a sale, a repossession, a licence) calls
the registry direct.

## Owning something in the scene

1. Add **Ownership Component** to the object's root (never to a child: the title is the object's, and everything on it
   belongs to whoever owns it).
2. Set **Default Owner**: `kind` `government`, `public`, `player` with a Steam id, or `organization` with the id.
3. Save the scene. The first time the scene runs, the host writes the record and from then on the file decides.

Objects with no component are nobody's, and that is not an error: it is a free thing.

## What a title is asked

```csharp
OwnershipComponent.TitleOf( go )              // who owns it, or nobody
OwnershipComponent.IsOwnedBy( go, steamId )   // is it theirs
box.CanUse( steamId )                         // may they open it
door.CanManage( steamId )                     // may they change the name on it
car.CanTransfer( steamId )                    // may they sell it
OwnershipComponent.OwnedBy( steamId )         // everything they own, in the scene
OwnershipComponent.OwnedBy<FourWheelVehicle>( steamId )   // every car they own
OwnershipRegistry.OfPlayer( steamId )         // every record, in the world or not
```

There is no type on a title. What a thing **is** comes from the thing: `OwnedBy<FourWheelVehicle>` asks each owned
object for that component, so a new kind of owned object needs no new ownership code.

## Title is not access

Title says whose a thing is. What a player may do with it is `OwnershipRules` (use, manage, transfer), and a lock, a
key or a picked door is a separate rule on top that can only ever be stricter or looser than this. That is what lets
raiding land later without touching title.

**A mailbox is the first thing whose owner is a title.** Its root carries an `OwnershipComponent`, so a box can belong
to a player or to an organization, and the first player to use a box that belongs to nobody claims it
(`MailBox.UseBy`). The title grants nothing there: anyone may post from any box and collect what is in any box, which is
how a delivery is stolen. What the title decides is where a home delivery from the Depot lands
(`DeliverySpot.HomePlaces`). Its contents moved to the box itself for this (`mailboxes.json`, `MailboxMail`), because a
title that can change hands must not leave its contents in the old owner's record (see [depot.md](depot.md),
"Ownership").

The storage box's claim is still its own file (`StorageRegistry`), and its contents are still in its owner's record.
Both have to move before a box can be sold.

## A client knows less than the host

`CanUse` and the rest ask the host's `OrgRegistry` for the player's organization, so on the host the answer is exact.
A client only knows its own player, so it gets the answer a plain member would: right for everything a crew shares,
wrong for the founder-only rights. A client's prompt can therefore say "this isn't yours" to a founder who may sell it.
That is a display gap, not a hole: what is done is done on the host, which knows. Closing it means syncing the
player's organization id onto their player (there is none today).

## Not yet

- The storage box's claim is not a title yet; the contents have to move to the thing's id first (see above).
- Locks, keys, breaking in, and "hard, not immune" offline protection: access policy on top of a title.
- A season wipe must call `OwnershipRegistry.Wipe( scene )`, like `PlacedObjectRegistry.Wipe`.
- Nothing in a prefab sets `DefaultOwner` to anything but `<nobody>` yet (a mailbox's is `<nobody>`, and a claim does
  the rest): the Depot's objects get theirs at run time, from the buyer.

Console commands are in [console-commands.md](console-commands.md), "Ownership".
