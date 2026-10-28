#!/usr/bin/env bash
# Run the game adapter's contract checks against the installed game's
# assemblies under Mono. Unity's native side is not needed.
set -euo pipefail

repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dv="${DV_INSTALL_DIR:-$HOME/.local/share/Steam/steamapps/common/Derail Valley}"
project="$repo/tools/RealisticBrakes.AdapterCheck"

command -v mono >/dev/null || { echo "mono is required" >&2; exit 1; }
# Fail on a failed build or any compiler warning; never run a stale checker.
if ! build="$(dotnet build "$project/RealisticBrakes.AdapterCheck.csproj" -c Release -p:DvInstallDir="$dv" --no-incremental -nologo 2>&1)"; then
    grep -E "error" <<<"$build" | sort -u >&2
    echo "adapter check build failed" >&2
    exit 1
fi
if grep -q -E "warning CS" <<<"$build"; then
    grep -E "warning CS" <<<"$build" | sort -u >&2
    exit 1
fi
# A player may not have the Multiplayer mod: the check runs without its assembly.
rm -f "$project/bin/Release/net48/MultiplayerAPI.dll"
mono "$project/bin/Release/net48/RealisticBrakes.AdapterCheck.exe"
