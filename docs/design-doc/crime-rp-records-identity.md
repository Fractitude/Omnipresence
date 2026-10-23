# Records and identities

Part of [crime-rp.md](crime-rp.md). Follows the principle "do it yourself, or use the public service". **Every legal provider keeps detailed records** of purchases and customer information, so crimes can later be **linked** back to people. Players can use **stolen or forged identities** to cover their tracks. Numbers are starting guesses.

## The idea

Using a public service is easy, and **it writes down who you are**. That is the price of the easy road. The records sit in the provider's database, and can later be:

- **Requested by police** (on a warrant, or automatically for some providers).
- **Stolen by hackers** and sold or used.
- **Leaked** in a breach.
- **Bought** from an insider.

The way to avoid linking is to use someone else's identity, do it yourself, or pay in ways that leave different traces. Every choice is a trade-off, and each leaves traces of its own.

## Identities

An **identity** is a fictional person record that services can check.

| Field | Meaning |
| --- | --- |
| **Name, ID number** | Generated from name pools. Never a real person. |
| **Address** | Where mail goes. A home, a mail drop, or an empty flat. |
| **Contact** | A phone number and an email. |
| **Payment methods** | A bank account, cards, an exchange account. |
| **Devices** | Computers and phones linked to it. |
| **Age and history** | How long it has existed and what it has done. A thin file is suspicious. |
| **Verification** | Which documents it holds (an ID card, a utility bill, a selfie check). |
| **Flags** | Reported stolen, under investigation, burned. |

### Kinds of identity

| Kind | Where it comes from | Strength | Risk |
| --- | --- | --- | --- |
| **Your own** | You start with one. | Fully verified, aged | Every record points straight at you. |
| **Forged** | You make it: an ID printer, templates, skill. | Depends on the quality of the forgery | A strong check spots a bad forgery. Has no history. |
| **Stolen from a citizen** | Hack a provider's customer database, bin diving, stolen mail, social engineering. | Aged, real history | The owner (a simulated citizen) notices charges and letters, reports it, and the identity burns after some time. |
| **Stolen from a player** | Phishing, hacking their computer, a leaked record. | Aged, matches a real account | The player sees activity, can dispute and report. A player's identity is their in-game persona, never a real person. |
| **Bought** | A dark-web seller or another player. | Varies, shown on the listing | The seller may have reused or sold it to others. |
| **Synthetic** | Built from pieces of several identities. | Can look healthy | Collapses when someone checks the parts. |

Players can hold several identities. Each is its own set of linked accounts, addresses and devices.

### The identity vault

A Navi app, **ID Vault** (placeholder name), shows:

- Every identity the player holds, with its verification level, age, flags and exposure.
- What is linked to it: accounts, addresses, devices, payment methods.
- **Contamination warnings** (see below).
- A quick choice for which identity to use at a given service.

## Providers and their records

Every provider has a **records policy**:

| Field | Meaning |
| --- | --- |
| **Detail** | What it keeps: name, ID, address, items, amount, payment method, device, location, time. |
| **Retention** | How long: 30 days, a year, forever. |
| **Verification** | How hard it checks identity: none, light, medium, strong. |
| **Sharing** | With police: never, on a warrant, or **automatic** above a threshold. |
| **Security** | How hard its database is to hack. |
| **Insider risk** | How easy it is to bribe someone with access. |

### Examples (draft)

| Provider | Detail | Retention | Verification | Sharing | Security |
| --- | --- | --- | --- | --- | --- |
| **Pharmacy** | Full: identity, items, lot numbers, time, payment | A year | Medium (ID) | Automatic for controlled items above a limit, otherwise on a warrant | Medium |
| **Hosting company** | Account, payment, IP, visitor logs | 90 days | Light (email, card) | On a warrant | Medium |
| **Online exchange** | Full account, every transfer | Forever | **Strong** (documents and a selfie check) | Automatic above a limit | High |
| **Crypto ATM** | A camera image, small amounts only | 30 days | None below a limit | On a warrant | Low |
| **VPN company** | Account, payment, sometimes connection logs | 30 days | Light | On a warrant | Medium |
| **Post office** | A receipt, tracking, camera | 90 days | None | On a warrant | Low |
| **Hardware store** | Sales, cards | A year | None | On a warrant | Low |
| **Bank** | Everything | Forever | **Strong** | Automatic above limits | High |
| **Gun shop** | A licence and a record of every sale | Forever | **Strong** | Automatic | High |
| **Clinic** | Injuries, reported to police if gunshot wounds | Forever | Medium | Automatic for some injuries | Medium |

- **Cash at a counter** leaves no identity record, but a camera and a witness.
- **Paying online** leaves a record linked to the payer.
- **Paying in crypto** is linked at the exchange where it was cashed in or out.

## Linking: how a crime finds a person

Investigators follow **links**, not magic. The case builds a **graph** of things that are connected.

| Node | Examples |
| --- | --- |
| **Identity** | A name and ID |
| **Account** | A bank, exchange, site or market account |
| **Address** | A home, a mail drop |
| **Device** | A computer, a phone |
| **Payment** | A card, a wallet |
| **Place** | A camera view, a mailbox, a post office |
| **Item** | A lot of ingredients, a package |
| **Pattern** | The times of day, the handwriting, the same VPN |

**Links come from records.** A package seized from a Hush sting carries a lot number from an ingredient. The pharmacy's record for that lot names an identity. The identity has an address. The address is a person. The investigation walks the graph.

**Linking works by overlap.** Two identities that share a device, an address, a payment source, a VPN, a posting time pattern or a handwriting sample get **merged** in the graph. The more overlap, the stronger the merge.

### Compartmentalization (the skill)

To keep an identity clean, a player keeps its parts **separate**:

- **A separate device** for each identity (a burner phone, a spare machine).
- **A separate mail drop** and a separate address.
- **A separate payment source,** funded in ways that do not link back.
- **A separate VPN chain** or hosting.
- **A separate rhythm,** at different hours and places.
- **Separate handwriting and tools.**

Any link between a clean identity and a dirty one is **contamination**. The ID Vault warns about it. Once contaminated, the clean identity is at risk, and its records can be used against the dirty one.

## Using an identity

When a service asks "who is this?", the player picks an identity. The service's **check** is compared against the identity's strength.

- **Weak check** (hardware store, ATM below a limit): almost any identity passes.
- **Medium check** (pharmacy, hosting): a decent forgery or a stolen identity passes. A bad forgery or a thin file may be refused or flagged.
- **Strong check** (bank, exchange, gun shop): needs documents and a selfie check. Only a strong stolen identity, a high-quality forgery, or the player's own passes. Getting one may need extra steps (a utility bill, a fake photo, a second device).

If the check fails, the provider may refuse, delay, or **flag the account** and tell the authorities.

### What stolen and forged identities protect, and do not

- **Protect:** the purchase record points at the fake, not at you. A seized package traces to someone else.
- **Do not protect against:** physical evidence you leave (fingerprints, DNA, handwriting, cameras), device and network links, and the overlap of two identities.
- **Each use leaves a pattern.** The same identity buying ingredients, hosting a site and cashing out becomes a profile of its own that police track.
- **Burn:** a used identity eventually gets flagged by its owner, or by an investigation. Then it fails checks and its records become evidence.

## Stealing identities

| Method | How | Result |
| --- | --- | --- |
| **Breach a provider** | Hack its database (an Intrusion operation) and extract customer records. | A batch of identities with their history. The provider also learns of the breach and may report it. |
| **Phish** | A phishing site or email collects a victim's account and personal details (see [crime-rp-websites.md](crime-rp-websites.md)). | One identity, for a player or a citizen. |
| **Mail theft** | Steal post from mailboxes (see the mailbox theft in [crime-rp-dark-web-app.md](crime-rp-dark-web-app.md)). | Letters, statements, a card. |
| **Bin diving** | Search bins near a target. | A few details, a lot of work. |
| **Social engineering** | Messages or a call pretending to be a provider. | One identity, with luck. |
| **Insider** | Bribe a person with access. | Records, at a price and a risk. |
| **Buy** | A dark-web listing. | Varies, may be resold. |

- **A stolen identity has a shelf life.** The owner discovers misuse (charges, letters, alerts) and reports it, after a delay that depends on their habits. The identity gets flagged, and then it fails checks.
- **Prices rise with quality:** age, history, verification level, and freshness.
- **Citizens** are simulated people with accounts and habits. Their reaction is part of the system (they check their statements, or they do not).

## Forging identities

- **Equipment:** an ID printer, blank cards, a photo setup, a document kit.
- **Skill:** a small minigame for fine detail. Quality depends on skill and kit.
- **Quality tiers:** crude, passable, good, excellent. Each provider's check has a strength.
- **Documents needed** for strong checks: a utility bill, a bank statement, a selfie match. Each can be forged or stolen too.
- **Risk:** a forgery that fails a strong check is **flagged**, which records your attempt, your device and your location.

## Players are linkable too

- Every player has a **real identity** from the start, with records.
- The game's own accounts (Hush, the exchange, the hardware store) are tied to identities.
- The player's **Navi computer** holds logs, which police can seize.
- A player can **steal another player's identity.** The victim sees charges, gets alerts, and can report. It makes players careful.

## Players act on records too

- **Hack a provider** to delete your own purchase records (cover), plant records on a rival, or read someone else's.
- **Bribe an insider** for a copy.
- **Sell data:** customer databases are goods on the dark web.
- **Tip off the police** with real records on a rival.
- **Defend:** a player who runs a provider (hosting, a market, a courier) chooses its records policy: keep logs to resolve disputes, or keep none to protect users, at the cost of trust or risk.

## Police use of records

(See [crime-rp-police-forensics.md](crime-rp-police-forensics.md).)

- Police request records through the system interface: **request records from provider X for identity Y**, which needs a warrant level, except for providers that report automatically.
- Records feed the **Financial**, **Digital**, **Physical** and **Witness** categories of the case file.
- Records can be **wrong** (a stolen identity), and a good investigator checks for overlap.
- **Automatic reporting** (pharmacies, exchanges, banks, gun shops) produces alerts without a warrant when a limit is crossed.

## Safety and fiction

- All names, IDs and documents are **generated**. Never a real person, never a real document.
- A stolen identity of a **player** is their in-game persona.
- Forging is a minigame with made-up documents, not a guide to forging real ones.
- All identity data lives on the host. A client cannot read or change another player's identities.
- The ID Vault shows a player only their own.

## Build order (later, after the first loop and the Pharmacy)

| Step | What | Fun test |
| --- | --- | --- |
| **R1** | One real identity per player. The pharmacy and the exchange keep records tied to it. Police read them at the "suspicion" level. | Does an old purchase come back to bite a player? |
| **R2** | The records policy on every provider, with retention and sharing. | Do players choose providers by their policy? |
| **R3** | Fake identities: forging and buying, with a verification check. | Do players take on a fake to buy an ingredient? |
| **R4** | Stolen identities from citizens, through a provider breach. | Does a breach pay off and then bring consequences? |
| **R5** | Linking by overlap and contamination warnings. | Do players separate their identities, and get caught when they do not? |
| **R6** | Player-to-player theft and the victim's reaction. | Does being the victim feel fair? |

### Smallest first version (R1 and R3)

Each player has one real identity. The pharmacy records every purchase against it with lot numbers. A seized package traces through its lot number to the purchaser. The player can buy a **forged ID** that works at the pharmacy, and the trace then points at the fake. The test: a player is surprised by a trace, then avoids it next time with a fake and cash.

## Risks

1. **Scope.** Records, identities, linking and verification are a large system. The first version must be R1 and R3.
2. **Complexity of linking.** Overlap graphs are hard to read. The investigation needs a clear picture for players (tells and a readable ID Vault).
3. **Feel-bad theft.** A player whose identity is stolen may feel cheated. Alerts, disputes and limits on harm help.
4. **Bookkeeping.** Every service writing detailed records could be heavy on the host. Records need compact formats and retention limits.
5. **Privacy.** Records must be fictional and never contain real-world data about players. Names are generated, IDs are made up.
6. **Balance.** If records are too strong, nobody dares to use any service. If weak, nobody bothers to hide.
7. **Security.** Records and identities are sensitive state. The host decides everything, and clients only see their own.

## Open questions

1. How many identities can a player hold, and does holding many cost something?
2. How is the check strength compared to forgery quality: a threshold, or a roll?
3. Do citizens have simulated lives and habits, or only data and reactions?
4. How long do records stay (retention), and does the server clock fit the season length?
5. What does the ID Vault show of an investigation, and how much is hidden?
6. Are automatic reports too harsh for casual players? (Keep them to the "suspicious" level.)
7. Can a player buy their own record back or delete it by hacking? How is that countered?
8. How are identities recycled at a season wipe?
