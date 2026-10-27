# Revisit: how ownership works

A reminder to come back to this. Today ownership is a quick placeholder: **the first player to use a thing claims it**, and everyone else is locked out. That was enough to get the loop working, but it will not hold up once there is more than one player in a home.

**Update:** the ownership system itself has landed — `OwnershipComponent` and `ownership.json`, a title that is a
player, an organization, the public or the government, transfer with a history, and queries (see
[ownership.md](../docs/ownership.md)). A mailbox has moved onto it (a title that grants nothing, see below); a storage box is
still claimed by first use, and the questions at the bottom of this note are still open.

## What exists now

| Thing | Who owns it | How |
| --- | --- | --- |
| **Mailbox** | A title: a player or an organization | **Moved onto the new system.** `OwnershipComponent` on the box's root, claimed by the first player who uses a box that belongs to nobody (`MailBox.UseBy`). The title grants nothing: anyone posts from any box and collects what is in any box, so a delivery can be stolen. `CrimeRecord.MailboxId` is only the box a player's mail arrives in. `mailboxes.json` now holds the mail in each box (`MailboxMail`), not claims. |
| **Storage box** | The first player who uses it (in progress, like the mailbox) | Contents are saved in the owner's record. Others see it greyed out. |
| **Grow tray** | Per player: a tray shows each player their own state | State is saved in the player's record by tray id. Two players share one object but not its contents. |
| **Press bench, packing table** | Nobody | Any player can use them; they hold no state. |
| **The house itself** | Nobody | One home scene, shared by everyone. |

## Why to revisit it

- **Race to claim.** Whoever touches the mailbox or storage first owns it. A new player joining a server where someone else already claimed the only box has nowhere to put things.
- **One home, one mailbox.** There is a single home and a single mailbox. With several players there need to be several homes, or several boxes per home, and a clear rule for who gets which.
- **Inconsistent models.** Trays are per player on a shared object, mailbox and storage are claimed by one player. These should follow one rule, or a deliberate reason for each.
- **No way to give up or hand over a claim.** A claim cannot be released, shared or transferred. A player who leaves forever keeps their box for ever (until a season wipe).
- **Raiding is coming.** The design is "Rust, but business": bases get raided, locks get picked, smart locks get hacked. Ownership has to become something that can be contested, not a permanent lock. Today other players are simply refused.
- **Offline protection.** The decided rule is "hard, not immune" for offline bases. Storage and mailbox contents need to fit that: what can a raider take from an offline player, and how much?
- **Crews and shared stashes.** The design mentions crews with a shared stash. Ownership needs a group concept.
- **Seasons.** A wipe resets crime state, including claims and storage contents. Check that every claim (the `mailboxes.json` file in particular) is reset with it.
- **Identity.** Ownership is by Steam id today. The records-and-identities design has players use several identities. Decide whether things belong to a player or to an identity.

## Questions to answer when revisiting

1. Does each player get their own home (an instance or a plot), or do players share one home?
2. Is ownership of a box a claim by first use, an assignment (a deed, a key, a rented place), or a purchase?
3. Can a claim be released, shared with a friend or crew, or transferred?
4. What can another player do to a box they do not own: nothing, pick the lock, hack it, break it, take part of it?
5. What happens to a box whose owner has been away for weeks (decay, repossession, a season wipe only)?
6. Should trays follow the same rule as boxes (per-player state on a shared object versus a claimed object)?
7. Where is the single source of truth for claims: each player's record, a world file like `mailboxes.json`, or both (as now)? Make sure they cannot disagree.
8. How does ownership interact with the offline processor (a claim for an absent player) and with a season wipe?

## Where the code is

- `Code/Objects/MailBox.cs`, `Code/Crime/MailboxMail.cs` (the mail in each box), `PlayerCrime.CollectParcels`, `ClaimedMailBox`.
- The storage box component and its claim, from the storage work (see `docs/storage.md` when it lands).
- `CrimeRecord` (`MailboxId`, trays, storage).
- `docs/design-doc/crime-rp.md`, "Bases and raiding" and the decisions on offline raiding and seasons.
