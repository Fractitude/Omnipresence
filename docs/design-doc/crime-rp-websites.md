# Player-made websites, email and phishing

Part of [crime-rp.md](crime-rp.md). Players can build **any kind of website** in the game: shops, forums, markets, fan pages, fronts, and **scam sites and phishing pages** that steal other players' in-game logins. They can send **phishing emails** to bring victims in. Everything here is fictional and in-game: the credentials, money and accounts are the game's, never real ones. Numbers are starting guesses.

## The idea

The dark web in this game is not one app. It is **a web of sites that players and NPCs run**. Hush (the first market, see [crime-rp-dark-web-app.md](crime-rp-dark-web-app.md)) is only the first site. Players can build their own, copy someone else's, impersonate it, or defend against those who do.

This gives the game a second kind of crime that needs **no lab and no gun**: social engineering, fake storefronts, and account theft, with a real defence game on the other side.

## Site packages: `.nsite` (decided 2026-10-23)

Websites are **packages**, added to Navi next to apps. Navi supports `.napp` for apps today. A new **`.nsite`** type is built in the same way (a manifest, pages, and a panel with a view model and UI state), but it is not an app.

**What decided means here:**

- A site is **not installed and not run from the start menu.** It runs **only inside the Navi browser, by its URL.**
- A site needs a **server** to host it. You **own** one or **rent** one.
- A site **can be taken down.**
- The work happens in the **Navi library** (a separate repository, a git submodule), so library changes are committed there first. See `Libraries/navi/README.md`.

### How a visit works

1. The player types or clicks a URL in the Navi browser.
2. A host-side **name registry** (the game's DNS) maps the domain to a **server**: a computer in the world with the server role. If the domain is unregistered, suspended, or its server is down, the browser shows an error.
3. The server holds the site's package (its data, and any panel code) and answers requests.
4. The browser draws the site, in a panel, with the site's own view model and a small **Site API**. It cannot call Navi system functions.
5. Forms and clicks go to the server, handled on the **host**. Money, accounts and logins are always checked by the game.

### Servers

| Option | How | Risks |
| --- | --- | --- |
| **Own a server** | Buy hardware, put it in your base, give it a public address, install the package. | It is a real object: it needs power and network, can be raided, hacked, seized or unplugged, and it ties the site to your base and your address. |
| **Rent a server** | Rent space from an **NPC hosting provider** (cheap, rules, monitored) or a **player-run hosting service**. | The provider can suspend you, log you, hand data to police, or go down. Bulletproof hosts ignore complaints and can scam you. |

- **Capacity** (visitors at once, storage, uptime) depends on the hardware or the plan.
- **Rent is paid** in crypto or cash, per day.
- **A host can run many sites.**

### Takedowns

A site goes down when:

- **Police seize** the server or its data (a raid on a base, a warrant to a provider).
- **The provider suspends** the account (a complaint, a report, unpaid rent).
- **The registrar suspends** the domain (the name stops resolving).
- **A rival takes it out** (hacking, deleting files, overloading the server).
- **The owner's server is switched off,** loses power, or is destroyed.

A takedown loses the site's data on that server unless there is a backup, and the money in any store or vault on it.

### How players make a site

| Path | What it is | Who can use it |
| --- | --- | --- |
| **In-game Site Builder** | An app in Navi that builds a **declarative** site (blocks, forms, themes) and writes an `.nsite` onto the server. No code. | Everyone |
| **Editor and publish** | Build a `.nsite` with a panel in the s&box editor, as for a `.napp`, publish it as a package, and install it on a server by its package ident. | People who can make s&box addons |

### Two kinds of site (recommended, needs your decision)

Site code is the sensitive part. A site's panel code runs **on the visitor's own machine**, and any package's code can see private game members, because s&box gives every package the same sandbox (see AGENTS.md, "Known limit"). Anything that runs code from a stranger when you click a link needs a gate.

| | **Declarative site** | **Scripted site** |
| --- | --- | --- |
| Made with | Blocks, forms, themes, built-in components. Data only. | A published package with a panel and view model, like a `.napp`. |
| Runs code on the visitor's machine | No. The browser draws it. | Yes. |
| Gate | None needed. | The visitor's **first visit to a scripted site** asks in the secure layer: this site runs code, allow? Off by default. A warning for new authors. |
| Permissions | None. | None. The Site API only. |
| Good for | Almost every site: shops, blogs, forums, phishing pages | Mini-games on a site, custom tools, richer UI |

**If you want to keep one kind only, keep declarative.** It is safe by construction, and it covers shops, scams and phishing.

### Credential capture needs a secure input (recommended)

My earlier safety rule was *capture only matches*: the host compares what a victim typed against their real in-game credential and discards anything else. If a site's code can read keystrokes, a scripted site can bypass that and read anything a visitor types.

The fix mirrors the lock screen (see AGENTS.md, "The lock screen lives in the secure layer too"):

- **A login field is drawn by the browser, not by the site.** The site asks for a `login-form` for a named service. The browser shows the fields.
- **What the player types goes to the host, never to the site's code.** The host checks it against the real credential.
- **If it matches, the attacker's server records the captured login** in the vault. If it does not, the input is thrown away.
- So a phishing page can **look** like the real one (the page, the layout, the address), but cannot read a real password, even if a victim mistakenly types one.
- The page cannot read the field's value, move focus to hide it, or copy it.

This keeps credential theft a real, playable crime and keeps real passwords safe.

### Other safety rules for site packages

1. **No permissions.** A site has no access to Navi system calls, files, messages, notifications or the player's computer. It only has the Site API.
2. **No navigation to local addresses.** A site cannot send the browser to `browser://` or `file:///` addresses (the existing Web Browser rule), and cannot open a real web address.
3. **A hostile server can name any package.** A scripted site could point visitors at any published package. Scripted sites are therefore limited to the visit prompt, and the server's package list is checked by the game.
4. **Limits.** Size, request rate, CPU time, and memory per site, with a kill switch for the browser.
5. **Everything that matters is on the host:** forms, logins, money, captures, mail. The site's panel only draws and asks.
6. **Reports and takedowns** for sites that break the rules, handled by the registrar and the provider.
7. **Be honest in the UI:** these protections stop careless sites and UI tricks. They do not stop hostile code (AGENTS.md). Do not claim more.

## What players can build

| Kind of site | What it does | Crime angle |
| --- | --- | --- |
| **Storefront** | Sells goods or services | Can be a front that launders crypto through fake sales |
| **Player-run market** | Escrow, listings, fees (see "Player-run markets" in `crime-rp.md`) | Exit scams, takedowns |
| **Forum / blog / fan page** | Posts, comments, a community | Reputation, recruiting, doxing rivals |
| **Fake shop** | Looks like a store, takes payment, never ships | A pure scam |
| **Lookalike site** | Copies an existing in-game site at a similar address | Phishing |
| **Credential trap** | A login page that records what a victim types | Account theft |
| **Information site** | News, tips, rumours | Moves the market, spreads misinformation |
| **Hosting service** | Sells space to others | A business, and a target |

## The site builder

A new Navi app, **Site Builder** (placeholder name). Version 1 is **blocks**, not code.

- **Pages** made from blocks: heading, text, image, button, link, list, listing (a product with a price and a buy button), login form, contact form, countdown, comment box, counter.
- **Style:** a small set of themes, colours, fonts, and a layout grid. The look stays inside the game's lo-fi style.
- **Copy a page:** a tool to clone any public in-game site as a starting point. That is the phishing kit.
- **Forms:** a form has fields and an action. Actions: **send to my inbox**, **log in to this service** (a real game login), or **capture credentials** (see below).
- **Rules, not scripts:** pages can have simple conditions (show this block to visitors from this region, hide the trap from visitors with a security tool). There is **no scripting**.
- **Preview** shows exactly what a visitor sees, including browser warnings.
- **Advanced mode (later):** a restricted markup language for people who want finer control. Still no scripts, no external links.

### What a page cannot do (safety)

- No JavaScript, no iframes, no external images or fonts, no links to anything outside the game's own addresses.
- It is drawn by **the game**, not by a real browser engine. Visitors never load real web content from a player. (See "Safety rules" below.)

## Addresses, domains and hosting

- **Domains** have names and cost money. Names are unique. Lookalikes are possible: `hush.market` and `hush-market.co` are different domains. A domain has an **age** that visitors and security tools can see.
- **Hosting** is where a site runs. Options:
  1. **Your own computer** in your base. Cheap, but it uses your connection and your address. Police and rivals can trace it to you. If the machine is off or raided, the site is down.
  2. **Rented hosting** from an NPC provider (cheap, monitored) or a **player-run hosting service** (varied, can ignore complaints, can be raided or hacked).
  3. **Bulletproof hosting** (rare, expensive): ignores takedowns, but the provider can be a scam too.
- **Uptime and capacity** depend on the host. A busy site on a weak host goes down.
- **Certificates:** a site can show a lock. Real certificates cost; fake ones are detectable by careful visitors.
- **Logs:** a host keeps visitor logs. Logging helps you, and is evidence if seized.

## Email

A new Navi app, **Mail** (separate from Messages). It sends and receives email.

- **Compose, attach, send.** An email has a sender, a subject, a body and links.
- **Sender spoofing:** a tool lets a player fake the display name or the domain of the sender. Spoofing a domain that has strong protection often fails or lands in spam.
- **Mailing lists:** players can build lists (bought on the dark web, scraped, or stolen from hacked sites).
- **Spam filters:** the mail system has a filter. A new domain with a bad reputation goes to spam. The filter learns from player reports.
- **Targets:** NPCs open mail by habit, with chances based on subject and sender. Players open mail when they choose.
- **Reports:** a player can mark an email as phishing. Reports make the filter stricter against that domain.
- **Email is also a delivery channel for malicious attachments** (later), which are simulated, game-only files.

## The phishing flow

1. **Build** a lookalike page of a service, using the clone tool (a fake Hush login, a fake bank).
2. **Register** a lookalike domain and **host** it.
3. **Write** an email from a spoofed sender ("Your account will be locked, verify here") with a link to the fake page.
4. **Send** to a list of NPCs and players.
5. **A victim clicks.** The fake page loads.
6. **The victim enters a login.** The form **captures** it.
7. **The attacker opens the Credential Vault** in Site Builder and sees captured logins that worked.
8. **The attacker logs in** as the victim to Hush, Payments, a bank, and steals crypto or cash, or reads their data.
9. **The victim notices** (an alert, a missing balance), changes credentials, and may report the site.
10. **Reports** push the site towards a **blocklist**, and the attacker moves to a new domain.

## Credential capture, safely

This is the sensitive part, so it is built to protect real people.

- **Credentials in the game are game-made.** An in-game service gives the player a generated login (a username and a passphrase like `violet-otter-47`), stored in the player's Navi password manager. Players do not invent passwords for in-game services. The UI says: **never use a real password here.**
- **A capture form keeps only a match.** On the host, a submitted value is compared to the real in-game credential of the player who submitted it. If it matches, the attacker's Vault records it. **Anything that does not match is discarded and never stored or shown.** An attacker cannot harvest arbitrary text, including a real password someone mistakenly typed.
- **No real accounts, brands or people.** Sites cannot impersonate real companies, and cannot target real-world individuals. A name filter and reports enforce it. Fake brands are the game's own (Hush and its rivals).
- **No data goes outside the game.** Nothing a site or email collects can leave the host.
- **Warnings** appear on any page that asks for a login, to every player, with a plain notice that credentials in this game are game-only.
- See the full list under "Safety rules" below.

## Defending against phishing

Realistic defence is part of the fun.

| Defence | How it works |
| --- | --- |
| **Password manager that autofills only on the real domain** | Navi saves your logins and fills them only on the real address. If autofill does nothing, the page is wrong. |
| **Address bar and lock** | Always visible. A lookalike domain is detectable by reading it. |
| **Browser warnings** | A red page for known phishing, a yellow one for new or unverified domains. |
| **Domain age** | Shown on the page info. Very new domains are suspect. |
| **Two-factor codes** | Codes arrive in Messages. A captured password alone is not enough. |
| **Hardware key** (item) | Immune to simple phishing. Rare and costly. |
| **Login alerts** | A message when a new device logs in. A fast victim locks the account. |
| **Spam filter and reporting** | Mark phishing, and the filter learns. |
| **Bookmarks** | Reaching a service from a saved bookmark avoids lookalikes. |
| **Verified badge** | A registrar-verified mark on official sites. Can be faked by a good fake site, if the checker is not careful. |
| **Security vendor** (NPC) | Sells protection and runs the blocklist. Slow to react to brand-new domains. |

### Attackers' counters

- **Rotate domains** faster than the blocklist.
- **Cloak the page:** show the trap only to visitors who look like real targets, and a harmless page to scanners and police.
- **Relay two-factor codes in real time** with a proxy kit (a hard, optional tool).
- **Use stolen mailing lists** with targets who are more likely to click.
- **Time the attack** to when a victim is online and distracted.

### Social engineering beyond email (later)

Fake support messages in Messages, calls that sound official, QR codes on posters in the real world that go to a fake page.

## Other scams players can run

- **Exit scam:** run a market, take escrow, vanish.
- **Fake shop:** accept payments, never ship.
- **Counterfeit listings:** cut or fake products on a legitimate market.
- **Advance fee:** "send some crypto and I will send more."
- **Pump and spread:** spread rumours on a forum to move the market.
- **Account takeover:** use stolen credentials to scam the victim's contacts.
- **Honeypot:** a fake site run to bait other criminals into revealing themselves. (Also what police do.)

## Police, evidence and consequences

Websites feed the same case file as everything else (see [crime-rp-police-forensics.md](crime-rp-police-forensics.md)).

- **Hosting leaves a trail.** A site hosted on your own computer links to you. Rented hosting leaves records. A takedown request can seize the server.
- **Domain registrations** have a payment trail. Paying in crypto and mixing helps.
- **Victim reports** become evidence: the more people report a scam site, the stronger the **Digital** and **Financial** categories grow.
- **Police undercover** can visit, buy from, and pose as victims on scam sites.
- **Takedowns:** police can seize a domain or a server. The player loses the site, the vault, and the money on it.
- **Fraud is an offence** with its own penalty tier, and large losses to many victims count more.
- **Victims fight back:** a player who is scammed can hunt the scammer (hack their site, expose their identity, tip off police, steal the vault).

## The economy of sites

- **Revenue:** shop sales, ads (NPC traffic networks pay for views), referral fees, subscriptions, captured credentials, stolen goods.
- **Costs:** domain, hosting, certificate, ads, spam lists, protection.
- **Traffic:** NPC visitors come through the **search app**, links, ads, mail and rumours. A search ranking depends on age, traffic, links and reports.
- **Reputation:** reviews and reports. A scam site loses reputation after the first reports.
- **Money laundering:** a legit-looking shop can pass crypto through fake sales to cash out, with lower evidence at some risk.
- **Defacement and DDoS:** rival players can hack, deface or overload a site as sabotage. A site that is down loses traffic.

## Safety rules (the important list)

1. **All credentials and money are in-game.** No real accounts, no real brands, no real names.
2. **Game-issued credentials.** In-game services generate logins. Players do not choose passwords for them, and are told never to use a real one.
3. **Capture only matches.** Anything a victim types that is not their real in-game credential is thrown away and never reaches an attacker.
4. **Sites are `.nsite` packages drawn by the game's own panels,** inside the Navi browser, by address. No real browser engine loads a player's page, no iframes, no external resources. (The Navi Web Browser loads real web pages in the player's real browser engine, so a site's content must never be loaded that way. See AGENTS.md on the Web Browser.)
5. **Declarative sites are the default; scripted sites need a visit prompt.** The engine's run-time scripting (`Game.Scripting`, Breen) sees every public type in the game (AGENTS.md, `docs/scripting.md`) and is never used for site content. A scripted site is package code and has the same sandbox limit as any package. Logins are drawn by the browser (the secure input above), never by site code.
6. **Host authority.** Sites, forms, captures, logins, mail and money all live on the host. Clients send ids. A client cannot read another player's vault, cannot forge a capture, and cannot change a victim's balance.
7. **No identity leaks.** A site owner never sees a visitor's real name or Steam id. Visitors are handles.
8. **Rate limits and caps** on mail volume, form submissions and sites per player.
9. **Reports and takedowns** for abuse beyond the fiction (harassment, real names, real brands, slurs, sexual content, anything targeting a real person). A text and name filter helps.
10. **A plain notice** on any page that asks for a login: *this game's credentials are game-only; never reuse a real password.*
11. **Platform rules.** Check s&box and Steam policy on scams and fraud as game themes, user-generated content, and age rating before shipping this.
12. **Opt-out.** Servers can switch player scams and phishing off ("safe mode") for communities that do not want them.

## Where it fits in the build

This comes **after** the first loop (grow, ship, get paid) and the first Hush market.

| Step | What | Fun test |
| --- | --- | --- |
| **W1** | In **Navi**: the `.nsite` type, the browser opening a site by address, a server role, and the name registry. Hush becomes one declarative site. | Can a player browse to Hush by address, and does a server going down take it offline? |
| **W2** | Site Builder with blocks, a theme, a domain, and basic hosting on your own computer. | Can a player make a page and a friend visit it? |
| **W3** | Forms: contact form and a login form tied to a game service. | Can a friend log in through the form? |
| **W4** | **Mail app** and a mailing list. NPC recipients open mail. | Do NPCs click? |
| **W5** | **Clone tool, lookalike domain, capture form, vault.** Victims are NPCs only. | Can a player run a phishing attack on an NPC and see the haul? |
| **W6** | **Defences:** password manager autofill only on real domains, warnings, domain age, 2FA, login alerts. | Do players notice a fake page, and are they right? |
| **W7** | Player victims, reports, blocklist, takedowns, police evidence. | Does a scam get caught, and does being scammed feel fair and fun? |
| **W8** | Hosting services, ads, search rank, player-run markets. | Do players run businesses on sites? |

### Smallest first version (W1 to W5, NPCs only)

A page format, one viewer, one builder, one domain, one hosting option, one mailing list of NPCs, one lookalike target (a fake Hush), one capture form, and a vault. NPC victims have accounts with a little crypto. The test: a player steals crypto from an NPC with a phishing email, and a second player can spot the fake.

## Risks

1. **Scope.** A site builder, a viewer, mail and a defence layer is a big build. The first version must be tiny.
2. **Real harm.** Credential capture and impersonation need the safety rules above or the feature is off. A bug in the match check could leak real text.
3. **Abuse of the content tools.** Player pages can carry slurs, doxing and harassment. Needs moderation tools from day one.
4. **Fairness.** Players who lose money to scams may feel cheated. Clear alerts, reports and recovery help; some losses should be recoverable (limits, delays, an arbitration).
5. **Balance.** If phishing is too easy it dominates the economy. If defences are too strong, nobody tries. Tune the open rate and the warnings.
6. **Dependencies.** `.nsite` support lives in the Navi library, a separate repository (a git submodule). The package type, browser support, server role and name registry are library work, committed there first. The existing Web Browser loads real pages in the real engine, so it needs a distinct path for `.nsite` addresses.
6b. **Scripted sites run stranger code on visitors' machines.** That is the same risk as Store apps, without a consent step by default. The visit prompt and the "declarative only" option are the answer; skipping both is a big risk.
7. **Platform risk.** Scam themes and UGC may need a rating or a safe mode.
8. **Complexity of NPCs.** NPC victims need accounts, habits and money to be convincing without generated text.

## Open questions

1. **Scope of "any website."** Blocks only, or a restricted markup mode too? How far can players customise?
2. **Where do pages live and who sees them?** On the host, replicated on demand. How is storage limited?
3. **How realistic is email?** Headers and spoofing as a puzzle, or just a menu choice of sender?
4. **Do players get a search engine,** and who controls ranking?
5. **How are in-game passphrases generated and shown,** so they cannot be mistaken for real passwords?
6. **Can phishing target the Navi file system or the player's whole computer,** or only service logins? (Proposed: service logins only.)
7. **Two-factor design.** Codes in Messages, or a hardware item only?
8. **What does a recovery look like** after an account is stolen (a helpdesk NPC, a delay, a cap)?
9. **Safe mode.** Is it a server setting, a per-player setting, or both?
10. **Does impersonating a real player's site count?** Allowed in the fiction, or limited?
11. **Declarative only, or both kinds?** Recommended: declarative for everyone first, scripted sites later behind the visit prompt.
12. **Is the secure login input accepted?** It keeps real passwords away from site code, but limits what a phishing page can do visually.
13. **Where does the name registry live?** On the host with the world, or on a global backend shared across servers?
14. **What is a server physically?** An existing Navi computer with a role, or a new object? How is it connected and given an address?
15. **How are packages delivered to visitors?** As data from the server, or as packages the client downloads by ident? The second is the Store-app route and carries its trust problems.
16. **Do rented servers sit in the world,** or do they only exist as accounts with an NPC provider?
17. **Who can take a site down,** and how does the owner appeal or recover?
