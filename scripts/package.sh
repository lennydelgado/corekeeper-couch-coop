#!/usr/bin/env bash
# Builds the release archives in dist/:
#  - CouchCoop-<v>.zip                      : CouchCoop (all platforms) + docs + Windows installer   -> GitHub
#  - NotProtonFixes-<v>.zip                 : macOS/NotProton fixes + docs                           -> GitHub
#  - CoreKeeper-CouchCoop-macOS-<v>.zip     : both mods + one-click macOS installer                  -> GitHub
#  - modio/CouchCoop-<v>.zip, modio/NotProtonFixes-<v>.zip : ModManifest.json + DLL at the zip root -> mod.io
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$ROOT/Directory.Build.props" | head -1)"
"$ROOT/scripts/build.sh"

DIST="$ROOT/dist"; STAGE="$DIST/.stage"
rm -rf "$DIST"; mkdir -p "$STAGE" "$DIST/modio"
DOCS=("$ROOT/README.md" "$ROOT/README.fr.md" "$ROOT/LICENSE" "$ROOT/CHANGELOG.md" "$ROOT/docs/MACOS.md")

zipdir() { (cd "$1" && zip -qr "$2" .); echo "${2#$ROOT/}"; }

# CouchCoop (all platforms)
mkdir -p "$STAGE/couch/scripts"
cp -R "$ROOT/build/CouchCoop" "$STAGE/couch/CouchCoop"
cp "${DOCS[@]}" "$STAGE/couch/"
cp "$ROOT/scripts/install-windows.ps1" "$STAGE/couch/scripts/"
zipdir "$STAGE/couch" "$DIST/CouchCoop-$VERSION.zip"

# NotProtonFixes alone
mkdir -p "$STAGE/fixes"
cp -R "$ROOT/build/NotProtonFixes" "$STAGE/fixes/NotProtonFixes"
cp "${DOCS[@]}" "$STAGE/fixes/"
zipdir "$STAGE/fixes" "$DIST/NotProtonFixes-$VERSION.zip"

# Complete macOS pack (one-click install, no .NET SDK needed)
mkdir -p "$STAGE/mac/build" "$STAGE/mac/scripts"
cp -R "$ROOT/build/CouchCoop" "$ROOT/build/NotProtonFixes" "$STAGE/mac/build/"
cp "$ROOT/scripts/install-macos.sh" "$STAGE/mac/scripts/"
cp "${DOCS[@]}" "$STAGE/mac/"
cat > "$STAGE/mac/INSTALL-macOS.command" <<'EOF'
#!/usr/bin/env bash
# Double-click to install (Core Keeper must be closed).
cd "$(dirname "$0")" && SKIP_BUILD=1 ./scripts/install-macos.sh; read -r -p "Press Enter to close..."
EOF
chmod +x "$STAGE/mac/INSTALL-macOS.command" "$STAGE/mac/scripts/install-macos.sh"
zipdir "$STAGE/mac" "$DIST/CoreKeeper-CouchCoop-macOS-$VERSION.zip"

# mod.io: the game's loader expects ModManifest.json at the root of the mod.
for mod in CouchCoop NotProtonFixes; do zipdir "$ROOT/build/$mod" "$DIST/modio/$mod-$VERSION.zip"; done

rm -rf "$STAGE"
