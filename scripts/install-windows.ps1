# Windows installer (untested by the author, feedback welcome).
# Copies CouchCoop into the game (NotProtonFixes is not needed on Windows).
# Usage: powershell -ExecutionPolicy Bypass -File scripts\install-windows.ps1 [-GameDir "D:\SteamLibrary\steamapps\common\Core Keeper"]
param(
    [string]$GameDir = "C:\Program Files (x86)\Steam\steamapps\common\Core Keeper",
    [string]$Package = ""
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

if (Get-Process -Name CoreKeeper, CoreKeeperServer -ErrorAction SilentlyContinue) {
    throw "Close Core Keeper (and the dedicated server) before installing."
}
if (-not (Test-Path "$GameDir\CoreKeeper_Data")) { throw "Core Keeper not found: $GameDir (use -GameDir)" }

# Source: release archive (CouchCoop folder next to the script) or local build.
$src = if ($Package) { $Package } elseif (Test-Path "$root\CouchCoop\CouchCoop.dll") { "$root\CouchCoop" } else { "$root\build\CouchCoop" }
if (-not (Test-Path "$src\CouchCoop.dll")) { throw "CouchCoop.dll not found in $src (build first: dotnet build src\CouchCoop -c Release)" }

$mods = "$GameDir\CoreKeeper_Data\StreamingAssets\Mods"
New-Item -ItemType Directory -Force -Path $mods | Out-Null
Remove-Item -Recurse -Force "$mods\CouchCoop" -ErrorAction SilentlyContinue
Copy-Item -Recurse $src "$mods\CouchCoop"
Write-Host "CouchCoop installed in $mods"

$server = Join-Path (Split-Path -Parent $GameDir) "Core Keeper Dedicated Server"
if (-not (Test-Path "$server\CoreKeeperServer.exe")) {
    Write-Host "For split screen, also install 'Core Keeper Dedicated Server' (Steam > Library > Tools)."
}
