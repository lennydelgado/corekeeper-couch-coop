#!/usr/bin/env bash
# Keeps macOS from throttling Core Keeper while you play split screen.
#
# Why: split screen runs 3 Wine processes (P1, P2, the dedicated server) plus wineserver, which relays
# input and window messages between them. macOS treats the windowless server (and the window that does not
# have the focus) as background work: lower CPU priority, App Nap, coalesced timers. The server then misses
# its 20 ticks/s and both players get input lag and freezes.
#
# What it does: every few seconds, moves those processes out of the background band and gives them the
# best throughput / latency tiers (taskpolicy -B -t 0 -l 0). Nothing is installed or changed permanently;
# it stops by itself once the game is closed.
#
# Usage: scripts/macos-boost.sh        (start it before or after launching the game, leave it running)
set -uo pipefail

PATTERN='CoreKeeper(Server)?\.exe|wineserver'
declare -a done_pids=()
seen_game=0
echo "Core Keeper boost: waiting for the game... (Ctrl+C to stop)"

while true; do
  pids=$(pgrep -f -E "$PATTERN" 2>/dev/null || true)
  game=$(pgrep -f -E 'CoreKeeper(Server)?\.exe' 2>/dev/null || true)
  if [[ -n "$game" ]]; then seen_game=1; elif (( seen_game )); then echo "Game closed, done."; exit 0; fi

  for pid in $pids; do
    [[ " ${done_pids[*]-} " == *" $pid "* ]] && continue
    if taskpolicy -B -t 0 -l 0 -p "$pid" 2>/dev/null; then
      name=$(ps -o command= -p "$pid" | grep -oE 'CoreKeeperServer\.exe|CoreKeeper\.exe|wineserver' | head -1)
      [[ "$(ps -o command= -p "$pid")" == *-couchcoop-p2* ]] && name="$name (P2)"
      echo "boosted $pid ${name:-?}"
      done_pids+=("$pid")
    fi
  done
  sleep 3
done
