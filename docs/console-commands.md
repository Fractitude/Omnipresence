# Console commands

Every console command in the project. Open the console in the s&box editor and type the name. Arguments in `[brackets]` are optional.

Keep this list current: add a row here when you add a `[ConCmd]`.

## Progression

Testing only. They ask the host, which refuses unless it is the editor (`PlayerProgress.DebugGrant`, `DebugSetTime`, `PlayerWallet.DebugGrant`). Code: `Code/Progression/ProgressCommands.cs`.

| Command | What it does |
|---|---|
| `progress_give_xp <amount>` | Give yourself XP. |
| `progress_give_hack_xp <amount>` | Give yourself hacking XP. |
| `progress_give_money <amount>` | Give yourself dollars. |
| `progress_give_credits <amount>` | Give yourself credits. |
| `progress_give_crypto <amount>` | Give yourself crypto. |
| `progress_show` | Log your level, XP, hacking level and XP, dollars, credits and crypto. |
| `progress_test_data [on\|off]` | Keep progress in `progress_test/`, the clock in `world_test.json` and the mailbox claims in `mailboxes_test.json`, so a test never touches the real save (`off` goes back). Editor only, on the host, and only before the player has spawned. |

## Crime

Testing only, for the grow, press, post and Hush loop. They ask the host, which refuses unless it is the editor (`PlayerCrime`). Code: `Code/Crime/CrimeCommands.cs`. Skipping time works too (`world_time_skip_minutes`).

| Command | What it does |
| --- | --- |
| `crime_give <kind> [count] [quality]` | Give yourself a batch: kind is any item of the catalogue: `static_culture`, `static_raw`, `static_tab`, `haze_seed`, `haze_raw`, `haze_vial` or `parcel` (a wrong kind logs the list). Count defaults to 1, quality to 50. |
| `crime_tray_ready` | Every tray you planted is ready to harvest now. |
| `crime_postal_now` | The mailman, the escrow release and the Exchange cash-outs are all due now. |
| `crime_orders [count]` | Put more orders on the Hush board (up to 10). |
| `crime_deliver_now` | Every delivery you bought in the Hush shop has reached your mailbox now. |

## Offline processing

Testing only, for the processing that carries on for players who are away (`OfflineCrimeSystem`, see [crime.md](crime.md), "When a player is away"). Editor only, on the host: they refuse anywhere else. Code: `Code/Crime/OfflineCommands.cs`.

| Command | What it does |
| --- | --- |
| `offline_away` | Take your player object away without disconnecting, like a player who left or who is dead. The processor carries on with your record. |
| `offline_back` | Respawn your player after `offline_away`. |
| `offline_status` | Log the clock, the ids with work waiting, and for each whether a player is attached, whether the record is in memory and when the next event and the next look are. |
| `offline_rescan` | Look at every saved record and rebuild the index of players with work waiting (`progress/_pending.json`). |

## Health

Code: `Code/Health/`.

| Command | What it does |
|---|---|
| `kill` | Kill yourself. You watch your ragdoll, then respawn after a few seconds or on a click. |
| `god` | Toggle invulnerability (host, cheats). |
| `omni.spawnprotection <seconds>` | Seconds of invulnerability after spawning. Default 5, 0 turns it off. Saved. |

## Weapons

| Command | What it does |
|---|---|
| `give <weapon>` | Give yourself a weapon by its short name: `m4a1`, `colt1911`, `glock`, `revolver`, `mp5`, `shotgun`, `sniper`, `rpg`, `grenade`, `crowbar`, `camera`, `physgun`. |
| `omni.loadout "<weapons>"` | The weapons you spawn with, separated by spaces, e.g. `omni.loadout "m4a1 glock physgun"`. The default is `m4a1 glock crowbar physgun`. Saved. Applies from your next spawn. |

## Vehicles

| Command | What it does |
|---|---|
| `spawn_buggy` | Spawn a buggy in front of you. Walk up to it and press Use to drive: WASD, Shift to boost, Space for the handbrake, F to switch the headlights. |
| `omni.screenshake <0..2>` | How hard the camera shakes, such as in a fast vehicle. Default 0.3. Saved. |

## World clock

The game's own time. A new world starts at 12:00 on Day 1, which is a Monday, and the host saves it to `world.json`. Same rules as above: editor only, and the host moves the clock for everyone (the host in the editor moves it itself, so it works while your player is away). Clients follow on the next sync. Moving the clock back delays shipments; moving it forward makes them arrive.

| Command | What it does |
|---|---|
| `world_time_show` | Log the time and day, like `12:00, Monday, Day 1`. |
| `world_time_set <HH:mm> [day]` | Set the time of day (24 hour). `day` is 1-based; leave it out to keep the current day. `world_time_set 09:30 3` is 09:30 on Wednesday, Day 3. |
| `world_time_skip_day [days]` | Skip forward whole days. One by default. Negative goes back. |
| `world_time_skip_hours <hours>` | Skip forward by hours. Decimals work: `2.5`. Negative goes back. |
| `world_time_skip_minutes <minutes>` | Skip forward by minutes. Negative goes back. |

## Navi: files

Look at and change a computer's files while no app does it. Host only. Each takes an optional computer `SaveKey` as its last argument; without it the command uses the computer you sit at, else the first one in the scene. Code: `Libraries/navi/Code/Navi/NaviComputer.Files.cs`. See also `Libraries/navi/README.md`.

| Command | What it does |
|---|---|
| `navi_fs_ls [path]` | List a folder. `/` by default. |
| `navi_fs_cat <path>` | Log a file's text. |
| `navi_fs_write <path> <text>` | Write text to a file. |
| `navi_fs_mkdir <path>` | Make a folder, and any missing parents. |
| `navi_fs_ln <target> <path>` | Make a symlink at `path` that points to `target`. |
| `navi_fs_alias <target> <path>` | Make an alias at `path` for `target`. |
| `navi_fs_mv <from> <to>` | Move or rename. |
| `navi_fs_rm <path>` | Delete. |
| `navi_fs_drives` | List the drives with their size and use. |
| `navi_fs_usb [label]` | Put in a new, empty 8 MB removable drive, to try `/Volumes`. `USB` by default. |
| `navi_fs_eject <path>` | Eject the removable drive at `path`. |

## Navi: hardware

Host only. They work on the computer you sit at, or else the nearest one. Code: `Libraries/navi/Code/Navi/NaviComputer.Hardware.cs`.

| Command | What it does |
|---|---|
| `navi_hardware` | Log the computer's hardware, whether it is online, its IP address, and the hardware there is. |
| `navi_hardware_install <name>` | Install hardware by its id or file name, e.g. `networkcard`, `soundcard`. |
| `navi_hardware_remove <name>` | Take hardware out, e.g. `navi_hardware_remove networkcard` to take the computer offline. |

## Navi: plugins

Plugins are part of the OS and run with no sandbox, so only the host may use these. They refuse to run on a client (`NaviPluginManager.CanManage`). Code: `Libraries/navi/Code/Navi/Plugins/NaviPluginManager.cs`.

| Command | What it does |
|---|---|
| `navi_plugin_install <packageIdent>` | Download and install a plugin, like `fractitude.nplug_themes`. |
| `navi_plugin_remove <packageIdent>` | Take a plugin off the list. It stops loading next time. |
| `navi_plugins` | Log the saved and the running plugins. |
