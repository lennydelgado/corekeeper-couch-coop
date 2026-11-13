# Playing Core Keeper on macOS (Apple Silicon)

Core Keeper has no macOS version. This guide shows how to run the Windows version from the regular macOS Steam client with **NotProton**, and what the **NotProton Fixes** mod does to make it work.

Tested with: Core Keeper 1.3.0.7, NotProton with CrossOver 26.3, Apple Silicon. Other setups may work, but they are untested.

## What you need

| | Notes |
|---|---|
| Apple Silicon Mac | NotProton does not support Intel Macs |
| macOS 26 or later | NotProton requirement |
| Steam for macOS | The normal Mac client |
| [CrossOver](https://www.codeweavers.com/crossover) | **Paid** (free trial available). NotProton uses CrossOver's Wine and D3DMetal to run Windows games |
| [NotProton](https://github.com/NotProtonNot/NotProton) | Free. Adds "Steam Play" to the Mac Steam client, so Windows games install and launch from your library |

Check the NotProton README for the exact CrossOver and Steam versions it supports. It is picky about versions.

## 1. Install NotProton

1. Install CrossOver and activate it (the trial is enough to start).
2. Install NotProton:
   ```bash
   brew install --cask notprotonnot/notproton/notproton
   ```
3. Open NotProton once, then restart Steam.
4. In **Steam › Settings › Compatibility**, choose the CrossOver version NotProton installed as the default tool.
5. If Steam asks for **Input Monitoring** permission, allow it. Controllers need it.

## 2. Install Core Keeper

1. Install Core Keeper from your library. Since there is no Mac version, Steam downloads the Windows one through NotProton.
2. **Before the first launch**, install the mods (next step). Without NotProton Fixes, the game crashes on its first load and does not save.
3. Optional, for split screen: also install **Core Keeper Dedicated Server** (Steam › Library › Tools).

No launch options are required. If you get a black screen, try `%command% -force-d3d11` in the game's launch options.

## 3. Install the mods

1. Download `CoreKeeper-CouchCoop-macOS-<version>.zip` and extract it.
2. **Launch the game once and quit**, even if it crashes. This creates the NotProton prefix the installer needs.
3. Double-click `INSTALL-macOS.command`.

The installer copies NotProton Fixes and Couch Co-op into the game, puts NotProton Fixes in the dedicated server, and applies the loader workaround described below.

## What NotProton Fixes does (and why)

NotProton runs the game through a patched copy of CrossOver's Wine. That Wine has a few file system bugs that break Core Keeper:

| Problem | Effect in the game | Fix |
|---|---|---|
| The game's Steam Cloud sync fails through NotProton's Steam bridge | The game quits during its first load ("Init failed for Save Manager") | The mod skips the game's own cloud sync. Your saves stay local |
| `File.Exists` returns true for files that do not exist, and `File.Delete` / `File.Replace` fail | Saves are written as `*.pugtmp` and never finalized, so **your progress is lost** when you quit | The mod writes saves with an atomic rename (`MoveFileEx`) and checks existence by opening the file |
| Deleting a folder fails | The mod loader crashes **every second launch** ("mod load failed"), because it cannot clear its temp folder | The installer replaces that temp folder with a file of the same name, so the loader loads the mod from its own folder |
| Unity pauses the game when its window loses focus | The game freezes when you click elsewhere | The mod keeps the game running in the background |

Every fix is set to `auto` and only turns on under Wine, so the mod does nothing on Windows. Settings are in `.../Core Keeper/Steam/<id>/mods/NotProtonFixes/`: `safeWrites`, `skipCloudSync`, `runInBackground`.

> **Steam Cloud**: since the game's cloud sync is skipped, your Mac saves are **not uploaded** to Steam Cloud. Back up `.../Pugstorm/Core Keeper/Steam/` if you care about them. To get existing cloud saves onto the Mac, download them from Steam's web page (Account › Steam Cloud) and copy them into that folder.

### Where things are on disk
- Game saves and logs: `~/Library/Application Support/Steam/steamapps/compatdata/1621690/pfx/drive_c/users/steamuser/AppData/LocalLow/Pugstorm/Core Keeper/`
- Game log: `Player.log` in that folder (mod lines start with `[NotProtonFixes]` / `[CouchCoop]`)
- Mods: `~/Library/Application Support/Steam/steamapps/common/Core Keeper/CoreKeeper_Data/StreamingAssets/Mods/`

## Controllers
- Controllers that work in macOS apps generally work in the game once Steam has Input Monitoring permission.
- NotProton has a known issue with **two controllers in local co-op games** (inputs get mixed). Couch Co-op avoids it, because each game window only keeps its own controller. If you still have trouble, check NotProton's issue tracker.

## Can I avoid paying for CrossOver?
NotProton needs CrossOver. Free alternatives exist, but **none of them is tested with these mods**:
- **[Bridgeplay](https://bridgeplay.dev/)**, a free fork of NotProton. It says it uses CodeWeavers' open-source Wine and Apple's Game Porting Toolkit instead of CrossOver. It re-signs the Steam app, so read its documentation first.
- **Wine wrappers** like Sikarugir / Kegworks run the Windows Steam client in a bottle instead of the Mac one. They are more manual, and performance may differ from CrossOver.

The file system bugs above come from NotProton's patched Wine. Other setups may not need NotProton Fixes, but it is safe to keep: it only changes how files are written.

## Troubleshooting
- **"Mod load failed"**: run `INSTALL-macOS.command` again.
- **The game quits during the first load**: NotProton Fixes is not loaded. Check that `Mods/NotProtonFixes` exists, then run the installer again.
- **My progress disappeared**: check for `*.pugtmp` files in the save folder. Rename them without the `.pugtmp` extension (game closed) to recover the last save.
