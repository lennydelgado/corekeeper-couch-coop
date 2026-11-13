# Changelog

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
