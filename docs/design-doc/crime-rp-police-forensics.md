# Police, the case file and forensics

Part of [crime-rp.md](crime-rp.md). Pairs with [crime-rp-dark-web-app.md](crime-rp-dark-web-app.md) and [crime-rp-first-product.md](crime-rp-first-product.md). It covers how NPC police find players: undercover buyers on the dark web, physical evidence (DNA, fingerprints and more), and the case file that ties it all together. Numbers are starting guesses.

## The idea

**Precautions are optional.** A player can wear gloves, a hairnet and a suit, use a clean bench, and print labels away from home. Or they can skip all of it to be faster and cheaper. Skipping works, until it does not: **evidence waits** in a database, and one later arrest can link old crimes to the player.

The game always shows the player what they are leaving, so risk is a choice, not a surprise.

## Two kinds of police work

| | **Digital** | **Physical** |
| --- | --- | --- |
| Source | Hush, accounts, VPN, session history | Packages, labs, stashes, mailboxes |
| Tools | Undercover buyers, account watching, forensics on seized computers | Fingerprints, DNA, handwriting, fibres, printer marks |
| Player protection | VPN, wiping history | Gloves, hairnet, mask, suit, clean bench, wipes, printed labels |

Both feed one **case file**.

## Undercover cops on the dark web

NPC officers pose as **buyers** (and later as sellers, vendors, or moderators).

- **Sting buyer:** places an order like any other. Tells are subtle (new account, hurry, polished tone, odd quantity, refuses nothing).
- **What a sting does:** the package it receives goes into the **evidence pipeline**. The label, the handwriting, the fingerprints on the box, any DNA, the printer, the postage source and the mailing location are all examined.
- **No instant arrest.** A controlled delivery raid or arrest comes only after enough evidence, and only if the evidence points to someone.
- **Account watching:** if an account is flagged, police read its orders, and cross-check shipping times with cameras at the post office or street box.
- **Rate:** about 1 in 10 early orders is a sting. The rate rises with a player's heat and volume.
- **Cop accounts can be burned:** a player who works out a sting and reports or scams it can make the department lose that identity for a while.

## Evidence types

What a player can leave, where, and how to avoid it.

| Evidence | Left by | Found on | How to avoid | Cost of avoiding |
| --- | --- | --- | --- | --- |
| **Fingerprints** | Handling without gloves | Boxes, tape, bags, containers, doors, benches | Gloves for every handling step. Replace gloves between dirty and clean steps. Wipe down items (imperfect). | A few seconds per step, a pair of gloves each time |
| **DNA** | Hair, skin cells, saliva, blood | Anywhere you spend time, especially packaging | Hairnet, mask and coverall. A clean bench with a filter hood is better. | Time to dress, money for kit, laundry for suits |
| **Handwriting** | Writing a label | Labels, notes | Print labels. | A printer |
| **Printer marks** | The printer used | Printed labels | Print at a public machine, or use a burner printer bought with cash. | A trip, or a one-off purchase |
| **Fibres and residue** | Your clothes, your lab | Inside the package | Dedicated clean clothes, a wipe-down, vacuum sealing | Clothes, wipes, sealer |
| **Odour** | The product | The package | Odour seals, carbon filter | Money per package |
| **Location** | Where you sent it from | Post office cameras, street box | Choose the place, change it, wear a disguise | Time and a trip |

- **Evidence level** per package is the sum of traces, each graded None, Light, Medium or Heavy.
- **Degradation:** old evidence is weaker. A fingerprint is good for days, DNA lasts longer, residue fades quickly.
- **Contamination:** evidence from a rival, left on purpose, can frame them.

## Precaution equipment

| Item | What it does | Cost (guess) | Upkeep |
| --- | --- | --- | --- |
| **Gloves** | Stops fingerprints. Tears with use. | $15 for 20 pairs | One per dirty step |
| **Hairnet** | Cuts hair DNA | $5 | Reusable |
| **Mask** | Cuts breath and saliva DNA | $10 | A few uses |
| **Coverall suit** | Cuts skin, hair and fibres | $60 | Washed after use |
| **Cleaning wipes** | Remove surface prints and residue (partly) | $20 | Used up |
| **Clean bench with a hood** | Keeps the work area clean. Greatly cuts airborne DNA and residue. | $1,500 | None |
| **Printer** | Makes printed labels | $120 | Ink |
| **Burner printer** | A printer bought with cash, thrown away after | $90 | Single use |
| **Public print trip** | Print at a library or shop | Free | A trip |
| **UV lamp** | Shows the traces you left on a package | $200 | None |
| **Forensic kit** | A fuller check, with a clear report | $600 | Refills |
| **Vacuum sealer and odour bags** | Cut odour and fibres | $180 | Bags |
| **Disguise** | Cuts camera identification at the post office | $50 | A few uses |

### The trade-off

- **Full precautions** cut the forensic risk by about 90 percent and add about 30 to 40 percent to the time of making a package (60 to 90 seconds).
- **Partial precautions** are a choice: gloves only, or gloves plus printed labels.
- **No precautions** are fastest and cheapest, and every package leaves heavy evidence.
- **Buying gear** is a one-off cost that pays for itself if the player is careful.

## Showing the player the risk

Before a package is sealed, the packaging screen shows an **evidence panel**:

```
Evidence on this package
  Fingerprints   None     (gloves on)
  DNA            Light    (no hairnet)
  Handwriting    None     (printed label)
  Printer marks  Medium   (home printer)
  Fibres         Light
  Odour          Low      (odour bag)
  Location       Post office, camera in view
```

- The panel is **honest**: it shows what will be left, in clear grades.
- A **UV lamp or forensic kit** reveals traces that a bare panel would call "Unknown" (for example, residue the player did not notice).
- The player can **fix** items before sealing: change the label, replace the box, wipe it.

## The forensic lab (the pipeline)

When police seize something, evidence goes through a lab.

1. **Collected:** from a seized package, a raided lab, a mailbox, a stash, a crime scene.
2. **Queued:** the lab has a limited number of tests per hour. A busy lab is slow.
3. **Processed:** after a delay (minutes to an hour of real time), the lab produces **profiles**:
   - A **DNA profile**.
   - A **fingerprint set**.
   - A **handwriting sample**.
   - A **printer signature**.
   - A **residue signature** (batch and materials).
4. **Stored:** the profile goes into the **evidence database**, anonymous at first.
5. **Matched:** a profile is matched to a person when one of the following happens:
   - The person is **arrested or booked**. Their profile joins the record, and every earlier unnamed profile that matches is linked to them at once.
   - Police **sample a suspect** (a warrant lets them collect from the home, trash or person).
   - The person's **printer, handwriting or residue** matches a seized item.
   - A **witness or informer** names them.

**The time bomb:** careless early packages leave unnamed profiles. They sit in the database. A later arrest, even for something small, can link them all and open a big case.

## The case file

Each player has a **case file** (hidden from the player, with visible tells). It collects evidence in four categories.

| Category | Fed by |
| --- | --- |
| **Digital** | Trace, VPN failures, account behaviour, session history on seized computers |
| **Physical** | Fingerprints, DNA, handwriting, printer marks, residue |
| **Witness** | Seen by civilians, cameras, informers, other players' tips |
| **Financial** | Large deposits, odd transfers, spending against declared income |

- **Strength per category** goes from 0 to 100.
- **Thresholds** decide what police can do:

| Level | Needs | Police can |
| --- | --- | --- |
| **Interest** | Any category above 20 | Watch the account or the address |
| **Suspicion** | Any category above 40 | Search warrants for mail and trash, tail the player |
| **Warrant** | Two categories above 40, or one above 70 | A search warrant for the home or lab |
| **Arrest** | Two categories above 60, with one **Physical** or **Digital** item that identifies the player | Arrest warrant, a raid is scheduled |

- **Burden of proof:** one weak category is not enough. A careful player with tidy physical evidence but poor digital hygiene can still be arrested on digital proof plus witness.
- **Tells:** a car on the street, a drone, a notice in Messages, a letter, a clerk who asks questions, a changed mail round. They are there to be noticed.
- **Counters:** lawyers (NPC) can raise the bar for a warrant, bribes lower a category, and hacking the police system can delete or alter entries (an Intrusion operation).

## Cops posing as other things

Later versions:

- **Undercover vendor:** an officer sells to the player to learn who buys, to build a case on the buyer.
- **Undercover moderator** in a player-run market: reads logs, learns users.
- **Informant:** a recruited NPC (staff, buyer, a rival) who reports.
- **Honeypot listing:** a "too good" product to catch careless players.

## What stays optional

- No equipment is required to play. A player can ship a box barehanded.
- The precautions are **choices about risk**, not gates.
- The honest evidence panel makes it clear what a player is accepting.
- The pressure to take precautions comes from **seeing arrests happen to other players**, and from the case file.

## Where it appears in play

| Moment | What happens |
| --- | --- |
| At the **press or the grow room** | Dress up (gloves, hairnet, suit) before working. Shed accumulates with time. |
| At the **packing table** | Put on gloves, pick a label method, see the evidence panel, seal. |
| At the **mailbox or post office** | Cameras and a clerk. A disguise helps. |
| At **home** | Police tells appear, a raid is possible. |
| In **Hush** | Reading buyers for stings. |
| In **Intrusion** | Hack police systems, wipe entries. |

## Fun tests

| Test | Pass if |
| --- | --- |
| Players read the evidence panel | They fix at least one item before sealing |
| A player is arrested after a sloppy period | They say "that is from that first package" |
| Players buy a UV lamp | They check their own work |
| Players skip precautions on purpose | They accept risk to save time and say so |
| Players read stings | They avoid a suspicious buyer and are right |
| Players find the process fair | They do not feel arrested "for nothing" |

## Version 1 scope

**In**

1. Fingerprints and handwriting/printer marks (two evidence types).
2. Gloves and a printer choice (public or home).
3. The evidence panel on the packing screen.
4. Sting buyers in Hush.
5. A single forensic lab with a queue and delays.
6. Unnamed profiles that link on arrest.
7. A case file with two categories (Digital and Physical) and the thresholds above, simplified.
8. Tells: a car on the street and a notice in Messages.
9. Arrest and a short jail.

**Out (later)**

DNA and fibres, clean benches, disguises, the Financial and Witness categories in full, lawyers and bribes, undercover vendors and moderators, informants, honeypots, hacking police systems.

## Security and host rules

- The case file, evidence database, sting flags and profiles live **only on the host**. The client sees tells and the evidence panel for its own package.
- The evidence panel is computed on the host. A client cannot lower its own risk.
- Actions carry ids. The host checks the gear, the gloves and the steps taken.
- The lab and matching run on the host. No call lets a client read another player's file.
- Police NPCs are game code. They cannot be given orders by players, only misled by evidence.

## Open questions

1. How strict is the "two categories" rule? It protects careless-but-lucky players, but may make police feel slow.
2. Do unnamed profiles ever expire, or last the whole season?
3. Is jail short enough, and how does it feel after an arrest caused by something long ago?
4. How much of the equipment ritual is a minigame, and how much a menu toggle? (Dressing up, sealing and cleaning must not become a chore.)
5. Are DNA and fingerprints from other players on shared items (a mailbox, a bench) fair to the owner?
6. Can a player frame another by planting evidence? If so, what is the penalty and the counter?
7. Do police share information across districts and markets, and how fast?
8. How does a modified client see its own evidence panel without leaking a stealth value?
