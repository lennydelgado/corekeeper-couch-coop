# Couch Co-op for Core Keeper

**Local split-screen co-op for Core Keeper**: two players, two controllers, one screen (your TV). Each player has their own character, inventory and camera, exactly like two players online.

The repository also contains **NotProton Fixes**, a separate optional mod that makes Core Keeper playable on **macOS through NotProton** (Steam + CrossOver).

🇫🇷 [Version française](README.fr.md)

---

## Features

**Couch Co-op** (all platforms)
- **Controller picker** in the style of FIFA: push your controller left (P1) or right (P2). Each window only listens to its own controller.
- **One-button split screen**: starts the official dedicated server in the background with one of your worlds, then opens a second game window for P2.
- **Seamless layout**: borderless windows, P2 on top and P1 at the bottom (or side by side). The render is widened to fill each half, so there are **no black bars**.
- **"Join With IP" pre-filled** in both windows, so you only have to press *Join*.
- **Both players are admins** of the local server.
- **Separate P2 saves**: characters and game settings. P2's music is muted by default, but sound effects stay on.
- **In-game hub drawn with the game's own fonts and UI frames**, usable with a controller (TV / Steam Deck friendly), in English and French.
- **Configurable scale**: one setting (Auto, x1 to x4) for the game's UI and zoom in each half, and for the mod's menus. It is always an integer scale, so everything stays crisp.

**NotProton Fixes** (macOS + NotProton only; does nothing elsewhere)
- Skips the game's Steam Cloud sync, which crashes the first load under NotProton.
- Fixes saving: under NotProton's Wine, `File.Exists` lies and `File.Delete`/`Replace` fail, so saves never got written.
- Keeps the game running when macOS takes focus away.
- Also works inside the dedicated server.

## Requirements
- Core Keeper 1.3 (Steam).
- For split screen, the free **Core Keeper Dedicated Server** (Steam › Library › Tools), installed in the same Steam library as the game.
- Two controllers.

## Installation

### Windows *(untested — feedback welcome)*
1. Download `CouchCoop-<version>.zip` and extract it.
2. Copy the `CouchCoop` folder to `…\steamapps\common\Core Keeper\CoreKeeper_Data\StreamingAssets\Mods\`, or run `scripts\install-windows.ps1`.

### macOS (NotProton)
First time on Mac? Read the full guide: [docs/MACOS.md](docs/MACOS.md).

1. Download `CoreKeeper-CouchCoop-macOS-<version>.zip` and extract it.
2. With the game closed, double-click `INSTALL-macOS.command`.

This installs both mods in the game, NotProton Fixes in the dedicated server, and the NotProton loader workaround.

### Linux / Steam Deck (Proton) *(untested)*
Same as Windows: copy `CouchCoop` into `CoreKeeper_Data/StreamingAssets/Mods/`.

> The game may show a warning that mods are active. That is expected.

## Playing in split screen

| | Controller | Keyboard |
|---|---|---|
| Open the hub | hold **Select + Start** for 1 s | **F8** |
| Switch tab | **LB / RB** | Q / E |
| Start split screen | hub › *Split screen* › *Start* | **F9** |
| Half screen ⇄ full screen | hub › *Split screen* | **F10** |
| Copy connection info | hub › *Split screen* | **F6** |
| Show/hide connection card | — | **F7** |

1. In the main menu, open the hub. On the *Controllers* tab, move one controller to **P1** (left) and the other to **P2** (right), then press **A**.
2. Start split screen. The local server boots; the first time, it takes up to a minute while it loads your world. **The P2 window opens once the server is ready.**
3. In **each** window, go to **Join Game**. *Join With IP* is already filled in, so press **Join**.
4. Play! When P1 quits the game, the server stops cleanly and saves the world.

### About the world
- The first time, the world chosen in *Options › World copied to the server* (default `0`) is **copied** to the local server.
- From then on, the co-op world lives in `…/Pugstorm/Core Keeper/CouchCoop/Server/` and is separate from your solo save.

## Settings
Edit them in the hub's *Options* tab. They are stored through the game's official mod config API in `…/Pugstorm/Core Keeper/Steam/<id>/mods/<Mod>/`.

| Setting | Default | |
|---|---|---|
| `Duo-layout` | `stacked` | `stacked` (P2 top / P1 bottom) or `side` (P1 left / P2 right) |
| `Duo-fillWindow` | `true` | Widen the render instead of black bars |
| `Duo-muteJ2Music` | `true` | Mute P2's music only |
| `Duo-autoFillJoin` | `true` | Pre-fill *Join With IP* |
| `Duo-sourceWorld` | `0` | World copied into the server the first time |
| `Duo-serverPassword` | random | Local server password |
| `Display-scale` | `0` | `0` = auto, `1`-`4` = fixed pixel scale for split screen and the mod's menus |
| `Controllers-autoOpenSelector` | `true` | Open the hub when a new controller appears |
| `NotProtonFixes` › `safeWrites`, `skipCloudSync` | `auto` | `auto` (only under Wine), `on`, `off` |

## How it works
- **Why a dedicated server?** The Steam client can only host through Steam's relay, and both windows use the same Steam account. The official dedicated server accepts direct IP connections (`-port`), so both windows connect to it on `127.0.0.1`.
- **Two instances, one license.** P2 is a second instance of the game launched by the mod. This is the same approach other split-screen tools use. It is not officially endorsed by Pugstorm.
- **Mod permissions.** Both mods are precompiled DLLs that require `accessesExtraAssemblies` and `skipSafetyChecks`. They use Harmony (shipped with the game), call Win32 to place windows, and start processes: the dedicated server and the P2 window. The full source code is in this repository.

## Troubleshooting
- **Logs**:
  - P1: `…/Pugstorm/Core Keeper/Player.log`
  - P2: `…/Core Keeper/J2/Player.log`
  - Server: `…/CouchCoop/Server/CoreKeeperServerLog.txt`
  - Mod lines start with `[CouchCoop]` / `[NotProtonFixes]`.
- **P2 window stuck on a black screen**: close it, then use *Restart P2 window* in the hub (or Shift+F9).
- **macOS: "mod load failed"**: run the installer again. It re-applies the NotProton loader workaround.
- **Display issues** (black bars, blurry text): press **F11** in the affected window, then share `Player.log`. It lists every camera and its render size.

## Building from source
```bash
scripts/build.sh            # needs the .NET SDK (macOS: brew install dotnet) and the game installed
scripts/install-macos.sh    # macOS: build + install
scripts/package.sh          # release archives in dist/
```
On Windows, use `dotnet build src/CouchCoop -c Release -p:GameDir="…\Core Keeper"`. The game's assemblies are referenced from your install and are never redistributed.

## License
MIT — see [LICENSE](LICENSE). Core Keeper is © Pugstorm / Fireshine Games. This project is not affiliated with them.
