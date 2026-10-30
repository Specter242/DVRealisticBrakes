#!/usr/bin/env bash
# Build a Unity Mod Manager package from the public source tree:
# dist/RealisticBrakes-<version>.zip, one RealisticBrakes folder.
set -euo pipefail

repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dv="${DV_INSTALL_DIR:-$HOME/.local/share/Steam/steamapps/common/Derail Valley}"
out="$repo/src/RealisticBrakes/bin/Release/net48"
mp="$repo/src/RealisticBrakes.Multiplayer/bin/Release/net48"
version="$(python3 -c 'import json, sys; print(json.load(open(sys.argv[1]))["Version"])' "$repo/src/RealisticBrakes/Info.json")"
zipfile="$repo/dist/RealisticBrakes-$version.zip"

dotnet test "$repo/tests/RealisticBrakes.Core.Tests" -c Release -nologo -v quiet
"$repo/scripts/check-adapter.sh"
# The Multiplayer integration builds the mod and its model with it.
dotnet build "$repo/src/RealisticBrakes.Multiplayer/RealisticBrakes.Multiplayer.csproj" \
    -c Release -p:DvInstallDir="$dv" -warnaserror -nologo

mkdir -p "$repo/dist"
stage="$(mktemp -d "$repo/dist/package.XXXXXX")"
trap 'rm -rf "$stage"' EXIT
mkdir "$stage/RealisticBrakes"
cp "$out/Info.json" "$out/RealisticBrakes.dll" "$out/RealisticBrakes.Core.dll" \
    "$mp/RealisticBrakes.Multiplayer.dll" "$repo/LICENSE" "$repo/CHANGELOG.md" "$stage/RealisticBrakes/"
# The players' guide, as the mod's README.
cp "$repo/PLAYER-GUIDE.md" "$stage/RealisticBrakes/README.md"
# The brake booklet: its pages and their layout.
mkdir "$stage/RealisticBrakes/Booklet"
cp "$repo/src/RealisticBrakes/Booklet/booklet.json" "$repo/src/RealisticBrakes/Booklet/"page-*.jpg "$stage/RealisticBrakes/Booklet/"

# Select the archive's contents explicitly; never include assemblies copied
# for adapter checks, local settings, logs, or development documents.
rm -f "$zipfile"
(cd "$stage" && zip -q -X "$zipfile" RealisticBrakes/Info.json RealisticBrakes/RealisticBrakes.dll \
    RealisticBrakes/RealisticBrakes.Core.dll RealisticBrakes/RealisticBrakes.Multiplayer.dll \
    RealisticBrakes/LICENSE RealisticBrakes/CHANGELOG.md RealisticBrakes/README.md \
    RealisticBrakes/Booklet/booklet.json RealisticBrakes/Booklet/page-*.jpg)
echo "Built $zipfile"
(cd "$(dirname "$zipfile")" && sha256sum "$(basename "$zipfile")")
