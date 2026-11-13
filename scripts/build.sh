#!/usr/bin/env bash
# Builds both mods into build/<Mod>/ (DLL + manifest), ready to copy into the game.
# Usage: scripts/build.sh                       (game path detected automatically)
#        GAME_DIR="/path/to/Core Keeper" scripts/build.sh
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"

if ! command -v dotnet >/dev/null 2>&1; then
  for d in /opt/homebrew/opt/dotnet/bin /usr/local/share/dotnet "$HOME/.dotnet"; do
    [[ -x "$d/dotnet" ]] && export PATH="$d:$PATH"
  done
fi
[[ -d /opt/homebrew/opt/dotnet/libexec ]] && export DOTNET_ROOT="${DOTNET_ROOT:-/opt/homebrew/opt/dotnet/libexec}"
command -v dotnet >/dev/null || { echo ".NET SDK not found (macOS: brew install dotnet)" >&2; exit 1; }

EXTRA=()
[[ -n "${GAME_DIR:-}" ]] && EXTRA+=("-p:GameDir=$GAME_DIR")

for mod in NotProtonFixes CouchCoop; do
  dotnet build "$ROOT/src/$mod/$mod.csproj" -c Release -nologo -v q "${EXTRA[@]}"
  cp "$ROOT/mods/$mod/ModManifest.json" "$ROOT/build/$mod/"
  # Keep only what goes into the game.
  find "$ROOT/build/$mod" -type f ! -name "$mod.dll" ! -name ModManifest.json -delete
done
echo "OK: build/NotProtonFixes, build/CouchCoop"
