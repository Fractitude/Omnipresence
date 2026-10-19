# Hacking

Hacking has its own skill. **Hacking XP** comes only from hacking puzzles (Intrusion servers, operations, the store's staff login). It never adds character XP. `HackingCurve` turns it into a **hacking level** (1 to 30, level 2 at 50 XP, then 200, 450, ...). Both show in the HUD under the character level.

A higher hacking level makes the player quieter: every alert gain is cut by 2% per level above 1, down to 70%. It also unlocks operations.

## Operations

Intrusion's **Operations** tab. Four stages, each with a quick way and a puzzle. Code: `Code/Apps/Intrusion/Operations/`.

| Stage | Quick (easy, loud) | Puzzle (quiet if solved well) |
|---|---|---|
| 1. Entry | Exploit an old bug: +20 alert, 2 traces | **Clone a badge**: crack a digit code (Mastermind-style). Wrong codes +4 alert; running out forces the door (+18, 1 trace). |
| 2. Cross | Crash the sweep daemon: +18 alert, 1 trace | **Slip past the sweep**: guard daemons patrol; the next 5 ticks are shown. Advance when clear. Waiting or backing off +2 alert; caught +12 and 1 trace, back to the door. |
| 3. Exfil | Dump it out: +22 alert, 2 traces, only 60% of the data | **Ghost route**: pick a relay per column, one row apart. Audit and sensor banners are watched (+16, 1 trace). Unknown banners cost a ping (+3). |
| 4. Cover | Wipe the logs: +10 alert, leaves 1 trace | **Scrub your lines**: remove exactly the log lines with your session tag (one per trace). Near-miss tags are other people's: each wrong delete +8 alert and a trace. |

Cover can also be skipped by walking away (traces stay), and is skipped for free when no traces were left.

### Alert

0 to 100. At 50 (suspicious) every gain is x1.25. At 80 (lockdown) every gain is x1.5 and the quick ways close. At 100 the player is **burned**: the run ends with no loot and 10% of the hacking XP.

### Grades

| Grade | Needs | Hacking XP | Credits |
|---|---|---|---|
| Ghost | no traces, alert never above 30, all the data | x2.0 | x1.75 |
| Clean | no traces, alert never above 49 | x1.4 | x1.25 |
| Noisy | at most 2 traces, alert under 70 | x1.0 | x1.0 |
| Sloppy | anything else | x0.6 | x0.6 |

Credits also scale with the share of the data taken. Operations can be repeated: hacking XP is paid every time, credits in full the first finished run and a quarter after. The first ghost run of an operation pays a one-time bonus of the operation's base credits. The best grade is saved per operation (`OperationRecord`).

| Operation | Hacking level | Base hacking XP | Base credits |
|---|---|---|---|
| Bluebird Archive | 1 | 120 | 300 |
| Tachibana Labs Subnet | 3 | 320 | 1,100 |
| Eastern Calculus Vault | 6 | 800 | 3,200 |

Puzzle secrets (the code, watched relays, which lines are the player's) live only in the host's puzzle objects and never in the UI state. RAM-type hardware that adds attempts also adds badge tries.
