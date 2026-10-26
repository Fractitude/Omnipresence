# Organizations

Many players can work in one drug operation, but an order pays only the player who packed and posted it. An **organization**
groups teammates and splits that revenue automatically, so a Grower, a Presser and a Courier can all be paid for one
order. The players run it in the **Organizations** app; the rules and the payout live in `Code/Org/`.

## The model

| Thing | Where | Notes |
| --- | --- | --- |
| `OrgRecord` | `Code/Org/OrgRecord.cs` | Id, Name, IconId (one of 16), IconColor (`#rrggbb`), FounderId (Steam id), Created (real unix seconds), Members, the last 50 `Payouts`. |
| `OrgMember` | same | Steam id, last known nickname, `Role`, `SharePercent` (0 to 100), Joined. |
| `OrgInvite` | same | An offer to one player: organization, role, share, who sent it, when. |
| `OrgRejection` | same | How many times a player rejected an organization in a row, and when last (the invite cooldown). |
| `OrgInviteSystem` / `OrgInviteMenu` | `Code/Org/` | Carries invitations to the invited player's client and answers back; the popup that shows them. |
| `OrgRules` | `Code/Org/OrgRules.cs` | **Pure functions**, no scene: names, colours, shares, permissions, invitations, the payout split. Testable alone. |
| `OrgCatalog` | `Code/Org/OrgCatalog.cs` | The 16 icons, the colour palette, the limits and the registration fee. |
| `OrgRegistry` | `Code/Org/OrgRegistry.cs` | The host's data and every change to it (each is checked by `OrgRules` first, then saved at once). Finds a player's organization from their Steam id, also while they are away. |
| `OrgPayouts` | `Code/Org/OrgPayouts.cs` | `Distribute`: the one function that shares a payment. |
| `OrgSystem` | `Code/Org/OrgSystem.cs` | Every 5 s delivers the shares that wait for absent members. |
| Organizations app | `Code/Apps/Organizations/`, `Assets/navi/game/organizations.napp` | `OrganizationsViewModel` / `OrganizationsUiState` / a stateless `Organizations.razor`, like Hush and Payments. Shared app: the host's copy runs it; every action carries only an id, a role or icon name, a small step or typed text, and acts as the player in the seat (`GameAppSeat`). |

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
by the organization), a role and a share, and sends it. The invited player gets a short chat line ("Org invited you.") and a
**popup** (below); the same invitation is in **their own** Organizations app (Invitations tab). Membership starts only when they
**accept**, since a share changes their income. They can reject it. The sender (or any Owner or Manager of the organization)
can cancel it.

- **An invitation never runs out.** It waits until it is accepted, rejected, cancelled by the organization, or **void**. It is
  kept in `orgs.json`, so it survives a restart and an invitee who is away finds it next time. Void means it can never be
  accepted: the organization was disbanded, or the player joined another one. The host removes those within a second
  (`OrgRegistry.SweepVoid`, from `OrgInviteSystem`) and tells the invitee if they are online. A **full** organization or shares
  that **no longer fit** are not void (a member may leave, a share may be lowered): they are checked when it is accepted, and
  then the invitation is removed and the invitee is told why (`OrgRules.CheckAccept`).
- An organization has at most **5 waiting** invitations and **16 members**, and can't send a second invitation to a player
  who already has one from it (another organization can).
- Accepting checks everything again (`CheckAccept`): the organization exists, the player is still in no organization, it is
  not full, the shares fit. A failed accept removes the invitation. Accepting removes the player's other invitations.
- **After a reject the organization must wait to invite that player again:** 5 minutes after the first rejection, 10 after
  the second, 20 after the third, then 30 minutes at most (`OrgRules.RejectionCooldown`, real UTC time). Only **Reject** counts:
  not Escape in the popup, not a cancel by the organization, not a void invitation. The record is per organization and player
  (`OrgRejection`, saved in `orgs.json`, so a restart does not reset it). The streak is forgotten when the player **accepts** an
  invitation of that organization, when the organization is disbanded, or **24 hours** after the last rejection
  (`OrgCatalog.RejectResetSeconds`). The host enforces it in `OrgRegistry.Invite`, refusing with "NAME declined recently. You
  can invite them again in 4 min." (`OrgRules.CheckInvite`); the invite form in the app lists such players greyed out with
  "(wait 4 min)" and they can't be picked. A wait ends exactly at its end (`RejectionWait` is 0 then).

### The invitation popup

`OrgInviteMenu` (`Code/Org/OrgInviteMenu.razor`, a `MenuWindow`, attached in `HudSystem.Add`, registered with `GameMenus` as
`orginvite`) shows one invitation at a time: the organization's icon in its colour, its name, who invited you, the role and the
revenue share offered, **Accept** and **Reject**, and "1 of 3" in the title when more wait.

- **Only the invitee's client learns of an invitation.** `OrgInviteSystem` (host) tells each connection its own list with an rpc
  filtered to that connection (also when they log in with some waiting, and again every 20 s while some wait, in case the first
  went before the client could take it). The client keeps it in `OrgInviteSystem.Mine`.
- **Pressing sends only the invitation id** (`OrgInviteSystem.AnswerRpc`, `[Rpc.Host]`). The host takes the caller from
  `Rpc.Caller`, and `OrgRegistry.Accept` / `Decline` only find an invitation whose invitee is that caller, so one player can't
  answer another's. Accept joins through the normal rules; Reject removes it, records the rejection and tells the inviter in
  the chat ("X rejected your invitation to Org."). The answer's result goes to the invitee's chat.
- **Escape** (or Close) decides later: the window closes and the invitations waiting then are not offered again until the next
  login (their ids are kept in a static set for the session). They stay in the app's Invitations tab, which has the same
  Accept and Reject. A new invitation opens the window again.
- It **queues** instead of opening while the player is dead or not spawned, sits in a seat (`ISitTarget`), another menu is open
  (`GameMenus.IsAnyOpen`) or a Navi computer has the keyboard (`NaviInputCapture`); it opens when none of that holds. It takes
  the mouse and the controls like the other menus.

## Payouts

`OrgPayouts.Distribute( receiverSteamId, receiverRecord, amount, currency, reason, orderId )` is called wherever a source of
revenue credits a player. Today that is the escrow release in `CrimeRules.Release`; **a new source calls the same function**.
It credits the receiver's own record itself, as one group of transactions ([transactions.md](transactions.md)): the payer (`hush-escrow` for an order) pays the receiver's part (`OrderPayout`) and each live teammate's share (`OrgShare`) directly, so the receiver never holds the whole amount and the history of each reads right.

- The **receiver** is the player who did the work: for a Hush order, whoever packed the parcel (the parcel carries its sender,
  and the order is shipped and paid in the sender's record, even when someone else posted it). Only the organization the
  receiver is in **at the moment of payment** counts. A receiver in no organization is paid in full, as before.
- Every other member with a share gets `floor( amount x share / 100 )` at once.
- **The remainder stays with the receiver** (decision): the rounding, the share nobody holds, the receiver's own share, and
  any share a member could not hold under the wallet cap. The work was theirs, so what is not given away is theirs, and the
  total paid out is always exactly the amount (the split is `OrgRules.Split`, tested with 20 000 random cases, including
  33/33/33, 1 crypto and caps). A receiver who can't hold what they keep gets `false`: nothing is paid and escrow tries again a
  minute later, as before.
- **Online or in-memory** members are credited at once, in the same all-or-none batch as the receiver (a player who is there through
  their own progress, so the HUD updates and nothing is overwritten; a record that is only loaded is saved by the system). **Any other member** gets
  the share written in `orgs.json` (`Pending`) and it goes into their saved record within 5 s (`OrgSystem`, `FlushPendingAsync`):
  the record is loaded, credited with a transaction (`UpTo`), saved, and only then the entry removed. If it doesn't fit under the cap, the rest stays and is
  retried (known limit: that share is not redirected to the receiver, because the receiver has been paid already).
- **Idempotent:** escrow pays an order once (it leaves the Delivered state in the same call). On top of that a payout is
  written to the organization's list under `reason + orderId + receiver`; calling again for the same one gives the receiver only
  what they kept the first time, never the members twice (a replay after a crash).
- Each payout is kept in the organization's last 50 (who got what, which order), shown in the Payouts tab, and every online
  teammate who got a share sees "You got N crypto from Org (order #id)" in the chat. A payout nobody shared in is not written down.
- `OrgCurrency` has Crypto (used) and Cash (works, for a future source). Credits never leave the Wired and are not shared.

## Files and test data

`orgs.json` in the game's data folder (`orgs_test.json` under `progress_test_data on`), written atomically with a `.bak`
(`FileProgressStore.WriteSafely`). It holds organizations, invitations, rejection records and the waiting shares. The index from Steam id to
organization is rebuilt on load. The registry's statics are not `readonly` (hot reload).

## Organizations app

Tabs: **Organization** (a register form when you are in none: name, a grid of the 16 icons, 12 colours or a hex colour, a live
preview; when you are in one: the card, the members table, and for Owner and Manager the member editor (select a row: role chips,
share -5 -1 +1 +5, remove with a second press), the invite form and the sent invitations; the Owner also edits name, icon and
colour, and disbands with a second press; everyone else leaves with a second press), **Invitations** (yours, Accept or Reject),
**Payouts** (the organization's recent splits with your part highlighted). It needs the computer to be online.

## Dev commands

`org_create <name>`, `org_member_add <steamId> <nick> [share]`, `org_invite_test`, `org_dump`, `org_clear`: see
[console-commands.md](console-commands.md). Editor and host only; the last three need `progress_test_data on`.
