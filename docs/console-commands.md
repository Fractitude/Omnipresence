# Console commands

Every console command in the project. Open the console in the s&box editor and type the name. Arguments in `[brackets]` are optional.

Keep this list current: add a row here when you add a `[ConCmd]`.

## Progression

Testing only. They ask the host, which refuses unless it is the editor (`PlayerProgress.DebugGrant`, `DebugSetTime`). Code: `Code/Progression/ProgressCommands.cs`.

| Command | What it does |
|---|---|
| `progress_give_xp <amount>` | Give yourself XP. |
| `progress_give_money <amount>` | Give yourself dollars. |
| `progress_give_credits <amount>` | Give yourself credits. |
| `progress_show` | Log your level, XP, dollars and credits. |

## World clock

The game's own time. A new world starts at 12:00 on Day 1, which is a Monday, and the host saves it to `world.json`. Same rules as above: editor only, and the host moves the clock for everyone. Clients follow on the next sync. Moving the clock back delays shipments; moving it forward makes them arrive.

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

## Navi: plugins

Plugins are part of the OS and run with no sandbox, so only the host may use these. They refuse to run on a client (`NaviPluginManager.CanManage`). Code: `Libraries/navi/Code/Navi/Plugins/NaviPluginManager.cs`.

| Command | What it does |
|---|---|
| `navi_plugin_install <packageIdent>` | Download and install a plugin, like `fractitude.nplug_themes`. |
| `navi_plugin_remove <packageIdent>` | Take a plugin off the list. It stops loading next time. |
| `navi_plugins` | Log the saved and the running plugins. |
