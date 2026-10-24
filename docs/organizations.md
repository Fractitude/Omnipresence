# Organizations

Many players can work in one drug operation, but an order pays only the player who packed and posted it. An **organization**
groups teammates and splits that revenue automatically, so a Grower, a Presser and a Courier can all be paid for one
order. The players run it in the **Government** app; the rules and the payout live in `Code/Org/`.

## The model

| Thing | Where | Notes |
| --- | --- | --- |
| `OrgRecord` | `Code/Org/OrgRecord.cs` | Id, Name, IconId (one of 16), IconColor (`#rrggbb`), FounderId (Steam id), Created (real unix seconds), Members, the last 50 `Payouts`. |
| `OrgMember` | same | Steam id, last known nickname, `Role`, `SharePercent` (0 to 100), Joined. |
| `OrgInvite` | same | An offer to one player: organization, role, share, who sent it, when. |
| `OrgRules` | `Code/Org/OrgRules.cs` | **Pure functions**, no scene: names, colours, shares, permissions, invitations, the payout split. Testable alone. |
| `OrgCatalog` | `Code/Org/OrgCatalog.cs` | The 16 icons, the colour palette, the limits and the registration fee. |
| `OrgRegistry` | `Code/Org/OrgRegistry.cs` | The host's data and every change to it (each is checked by `OrgRules` first, then saved at once). Finds a player's organization from their Steam id, also while they are away. |
| `OrgPayouts` | `Code/Org/OrgPayouts.cs` | `Distribute`: the one function that shares a payment. |
| `OrgSystem` | `Code/Org/OrgSystem.cs` | Every 5 s delivers the shares that wait for absent members. |
| Government app | `Code/Apps/Government/`, `Assets/navi/system/government.napp` | `GovernmentViewModel` / `GovernmentUiState` / a stateless `Government.razor`, like Hush and Payments. Shared app: the host's copy runs it; every action carries only an id, a role or icon name, a small step or typed text, and acts as the player in the seat (`GameAppSeat`). |

## Rules

- **Names:** 3 to 24 characters after trimming (runs of spaces become one), letters, digits, spaces and `- _ . ' & !` only (no control
  characters), **unique case-insensitively** across the server (`OrgRules.CheckName`).
- **One organization per player.** The sum of all shares is **at most 100**, checked on every change (`CheckShare`), on an
  invitation and again when it is accepted.
- **Roles** are fixed labels for now: Owner, Manager, Grower, Presser, Packer, Courier, Member. The work a Grower does is not
  checked; the role only says what a member may do inside the organization:

| | Owner (the founder) | Manager | Others |
| --- | --- | --- | --- |
| Invite | any role but Owner | any role but Owner and Manager | no |
| Remove a member | anyone but themselves | members who are not Owner or Manager | no |
| Change a role | anyone but the Owner, to any role but Owner | members who are not Owner or Manager, not themselves, never to Manager | no |
| Change a share | anyone, also their own | members who are not Owner or Manager, not their own | no |
| Rename, icon, colour | yes | no | no |
| Disband | yes | no | no |
| Leave | no (they disband) | yes | yes |

  The founder holds Owner for good: not kicked, not demoted, only the organization's disbanding ends it. A Manager can't raise
  their own pay (their share, and every Manager's, is the Owner's to set).
- **Founding** costs `OrgCatalog.RegistrationFee` dollars (0 for now; tune it there). The founder starts with a 0% share, so
  nothing changes in their income until they share some.
- A new organization's name, icon and colour are checked again by the host; the icon is one of the 16 ids and the colour a hex
  colour.
- The 16 icons are Material icons looked up in the ligature table of the engine's icon font
  (`core/fonts/MaterialIcons-Regular.ttf`). That font has no "skull". If one ever stops drawing, swap it in `OrgCatalog.Icons`.

## Invitations

An Owner or Manager picks one of the **players online** who is in no organization (not themselves, nor someone already invited
by the organization), a role and a share, and sends it. The invited player gets a chat line and sees it in **their own**
Government app (Invitations tab); membership starts only when they **accept**, since a share changes their income. They can
decline. The sender (or any Owner or Manager of the organization) can cancel it.

- An invitation **runs out after 10 minutes** (real time, `OrgCatalog.InviteSeconds`; skipping the game clock doesn't
  matter). An organization has at most **5 waiting** invitations and **16 members**.
- Accepting checks everything again (`OrgRules.CheckAccept`): the organization still exists, the invitation is not old, the
  player is still in no organization, the shares still fit. A failed accept removes the invitation. Accepting removes the
  player's other invitations.

## Payouts

`OrgPayouts.Distribute( receiverSteamId, receiverRecord, amount, currency, reason, orderId )` is called wherever a source of
revenue credits a player. Today that is the escrow release in `CrimeRules.Release`; **a new source calls the same function**.
It replaces the plain `record.AddCrypto`: it credits the receiver's own record itself.

- The **receiver** is the player who did the work: for a Hush order, whoever packed the parcel (the parcel carries its sender,
  and the order is shipped and paid in the sender's record, even when someone else posted it). Only the organization the
  receiver is in **at the moment of payment** counts. A receiver in no organization is paid in full, as before.
- Every other member with a share gets `floor( amount x share / 100 )` at once.
- **The remainder stays with the receiver** (decision): the rounding, the share nobody holds, the receiver's own share, and
  any share a member could not hold under the wallet cap. The work was theirs, so what is not given away is theirs, and the
  total paid out is always exactly the amount (the split is `OrgRules.Split`, tested with 20 000 random cases, including
  33/33/33, 1 crypto and caps). A receiver who can't hold what they keep gets `false`: nothing is paid and escrow tries again a
  minute later, as before.
- **Online or in-memory** members are credited at once: through `PlayerWallet` for a player who is there (one source of truth,
  so the HUD updates and nothing is overwritten), directly in their record if only the record is loaded. **Any other member** gets
  the share written in `orgs.json` (`Pending`) and it goes into their saved record within 5 s (`OrgSystem`, `FlushPendingAsync`):
  the record is loaded, credited, saved, and only then the entry removed. If it doesn't fit under the cap, the rest stays and is
  retried (known limit: that share is not redirected to the receiver, because the receiver has been paid already).
- **Idempotent:** escrow pays an order once (it leaves the Delivered state in the same call). On top of that a payout is
  written to the organization's list under `reason + orderId + receiver`; calling again for the same one gives the receiver only
  what they kept the first time, never the members twice (a replay after a crash).
- Each payout is kept in the organization's last 50 (who got what, which order), shown in the Payouts tab, and every online
  teammate who got a share sees "You got N crypto from Org (order #id)" in the chat. A payout nobody shared in is not written down.
- `OrgCurrency` has Crypto (used) and Cash (works, for a future source). Credits never leave the Wired and are not shared.

## Files and test data

`orgs.json` in the game's data folder (`orgs_test.json` under `progress_test_data on`), written atomically with a `.bak`
(`FileProgressStore.WriteSafely`). It holds organizations, invitations and the waiting shares. The index from Steam id to
organization is rebuilt on load. The registry's statics are not `readonly` (hot reload).

## Government app

Tabs: **Organization** (a register form when you are in none: name, a grid of the 16 icons, 12 colours or a hex colour, a live
preview; when you are in one: the card, the members table, and for Owner and Manager the member editor (select a row: role chips,
share -5 -1 +1 +5, remove with a second press), the invite form and the sent invitations; the Owner also edits name, icon and
colour, and disbands with a second press; everyone else leaves with a second press), **Invitations** (yours, Accept or Decline),
**Payouts** (the organization's recent splits with your part highlighted). It needs the computer to be online.

## Dev commands

`org_create <name>`, `org_member_add <steamId> <nick> [share]`, `org_dump`, `org_clear`: see
[console-commands.md](console-commands.md). Editor and host only; the last two need `progress_test_data on`.
