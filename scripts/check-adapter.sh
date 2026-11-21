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
# Build the optional Multiplayer integration when MultiplayerAPI.dll is installed so its packet contract is checked too.
mp_dll=""
if [[ -f "$dv/Mods/Multiplayer/MultiplayerAPI.dll" ]]; then
    if ! mp_build="$(dotnet build "$repo/src/RealisticBrakes.Multiplayer/RealisticBrakes.Multiplayer.csproj" -c Release -p:DvInstallDir="$dv" --no-incremental -nologo 2>&1)"; then
        grep -E "error" <<<"$mp_build" | sort -u >&2
        echo "multiplayer integration build failed" >&2
        exit 1
    fi
    if grep -q -E "warning CS" <<<"$mp_build"; then
        grep -E "warning CS" <<<"$mp_build" | sort -u >&2
        exit 1
    fi
    mp_dll="$repo/src/RealisticBrakes.Multiplayer/bin/Release/net48/RealisticBrakes.Multiplayer.dll"
fi
# A player may not have the Multiplayer mod: the check runs without its assembly.
bin="$project/bin/Release/net48"
rm -f "${bin:?}/MultiplayerAPI.dll"
DV_INSTALL_DIR="$dv" MP_DLL="$mp_dll" mono "$bin/RealisticBrakes.AdapterCheck.exe"
