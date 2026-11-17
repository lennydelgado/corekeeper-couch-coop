# Changelog

## Unreleased

### CouchCoop
- macOS: the top half no longer overlaps the bottom one. Halves are placed in the screen's work area, below the menu bar (hide the menu bar automatically to use the whole screen).
- The server world is backed up each time split screen starts (5 rotating copies in `CouchCoop/Backups/session`).
- If P1 crashes, P2 asks the server to save and stop, then closes. A server left running is reused next time instead of starting a second one.
- The hub also opens by holding both stick clicks (L3 + R3) for 1 s, for controllers whose Select is not seen (some Xbox controllers over Bluetooth). The first Select / Start press of each controller is logged.
- *Split screen* tab: stop split screen, bring the server world back to the solo worlds, load a solo world into the server. Both copies ask for confirmation and back up the world they replace.
- Split screen limits: Steam copy of the game required, local server capped at 2 players, P2 window only started by P1 (one-time token), P2 can only join the local server.
- Each player is told when their controller disconnects and reconnects.
- Zoomed-out split screen: lights no longer stay black for a moment. The game updates a few light shadows per frame, sized for its normal view; the budget now grows with the visible area.

## 1.1.0

### CouchCoop
- Fixed blurry, pixelated text at higher zooms: the game's UI now always renders at its native height, whatever the world zoom.
- Fixed the selection bar on the title menu sitting one line too high.
- P2 can open its own options (Select + Start or F8) and set its own zoom (`Display-scaleP2`).
- Fixed input lag in split screen: the game drops the window without focus to 30 fps. Both windows now run at the same cap (`Performance-fpsLimit`, 60 by default), with V-Sync off and one queued frame.
- The local server runs at a higher priority.
- The mod's menus no longer scan all sprites every 2 seconds.
- Freezes longer than 250 ms are logged with a timestamp (`[perf]` lines), with a summary every minute.
- macOS: optional `scripts/macos-boost.sh` keeps macOS from throttling the game processes (see docs/MACOS.md).

## 1.0.0 — first public release
Split into two mods: **CouchCoop**, for every platform, and **NotProtonFixes**, for macOS.

### CouchCoop
- Controller picker in the style of FIFA, inside a controller-friendly hub with three tabs: Controllers, Split screen and Options.
- Split screen from a single action:
  - starts the official dedicated server locally, with a copy of one of your worlds;
  - opens the P2 window only once the server is ready.
- Borderless half-screen windows, stacked (default) or side by side.
- Widened render with no black bars, using an exact integer scale.
- "Join With IP" pre-filled in both windows.
- P1 and P2 are automatically admins of the local server.
- P2 keeps separate saves and game settings. Its music is muted by default, sound effects stay on.
- Settings use the official `PugMod.API.Config` and can be edited in-game.
- English and French texts, following the game's language.
- UI drawn with the game's own bitmap fonts and UI frames (menu frame, tooltip box, hotbar slot, check boxes), at an integer scale.
- One "Scale" setting (Auto, x1 to x4) for the split-screen render (game UI and zoom) and the mod's menus.
- F11 writes a camera and render-size report to Player.log, for display issues.
- Migrates controller assignments and the server world from the "MacPad" development builds.

### NotProtonFixes
- Skips the Steam Cloud sync that crashes the first load under NotProton.
- Safe save writing: atomic `MoveFileEx` with no delete, and a reliable `FileExists`.
- Keeps running in the background under Wine. Also active in the dedicated server.
- Full macOS setup guide in docs/MACOS.md.
