#!/usr/bin/env bash
# macOS installer (Steam + NotProton / CrossOver). Builds if needed, then installs:
#  - CouchCoop + NotProtonFixes into the game;
#  - NotProtonFixes into the dedicated server (if installed);
#  - the mod loader temp-folder workaround (RemoveDirectory is broken under NotProton).
# The game and the server must be closed.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
STEAM="${STEAM_DIR:-$HOME/Library/Application Support/Steam}"
GAME_DIR="${GAME_DIR:-$STEAM/steamapps/common/Core Keeper}"
SERVER_DIR="${SERVER_DIR:-$STEAM/steamapps/common/Core Keeper Dedicated Server}"
PREFIX="${PREFIX:-$STEAM/steamapps/compatdata/1621690/pfx}"

if pgrep -f "CoreKeeper(Server)?\.exe" >/dev/null 2>&1; then
  echo "Close Core Keeper (and the dedicated server) before installing." >&2; exit 1
fi
[[ -d "$GAME_DIR/CoreKeeper_Data" ]] || { echo "Core Keeper not found: $GAME_DIR (set GAME_DIR=...)" >&2; exit 1; }

if [[ "${SKIP_BUILD:-0}" != 1 ]]; then GAME_DIR="$GAME_DIR" "$ROOT/scripts/build.sh"; fi

MODS="$GAME_DIR/CoreKeeper_Data/StreamingAssets/Mods"
mkdir -p "$MODS"
rm -rf "$MODS/MacPad" # old development build
for mod in NotProtonFixes CouchCoop; do
  rm -rf "$MODS/$mod"; cp -R "$ROOT/build/$mod" "$MODS/$mod"
  touch "$MODS/$mod/ModManifest.json"
done
echo "Mods installed in: $MODS"

if [[ -d "$SERVER_DIR/CoreKeeperServer_Data" ]]; then
  SMODS="$SERVER_DIR/CoreKeeperServer_Data/StreamingAssets/Mods"
  mkdir -p "$SMODS"; rm -rf "$SMODS/MacPad" "$SMODS/NotProtonFixes"
  cp -R "$ROOT/build/NotProtonFixes" "$SMODS/NotProtonFixes"
  echo "NotProtonFixes installed in the dedicated server"
else
  echo "Dedicated server not installed (optional, needed for split screen): Steam > Library > Tools > Core Keeper Dedicated Server"
fi

# NotProton: RemoveDirectory fails, so the mod loader crashes when its temp folder already exists.
# Each folder is replaced by a FILE with the same name: the loader then loads the DLL from the mod folder.
TMP="$PREFIX/drive_c/users/steamuser/AppData/Local/Temp/Pugstorm/Core Keeper/ModLoader"
if [[ -d "$PREFIX/drive_c" ]]; then
  mkdir -p "$TMP"
  for mod in NotProtonFixes CouchCoop MacPad; do
    [[ -d "$TMP/$mod" ]] && rm -rf "$TMP/$mod"
    [[ -f "$TMP/$mod" ]] || printf 'placeholder (see scripts/install-macos.sh)\n' > "$TMP/$mod"
  done
  echo "NotProton loader workaround applied: $TMP"
else
  echo "NotProton prefix not found ($PREFIX). Launch the game once through NotProton, then run this again."
fi
echo "Done."
