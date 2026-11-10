#!/usr/bin/env bash
# Build a Unity Mod Manager package from the public source tree:
# dist/RealisticBrakes-<version>.zip, one RealisticBrakes folder.
set -euo pipefail

repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dv="${DV_INSTALL_DIR:-$HOME/.local/share/Steam/steamapps/common/Derail Valley}"
out="$repo/src/RealisticBrakes/bin/Release/net48"
mp="$repo/src/RealisticBrakes.Multiplayer/bin/Release/net48"
booklet="$repo/src/RealisticBrakes/Booklet"
version="$(python3 -c 'import json, sys; print(json.load(open(sys.argv[1]))["Version"])' "$repo/src/RealisticBrakes/Info.json")"
zipfile="$repo/dist/RealisticBrakes-$version.zip"
# The commit the mod's log names: this checkout's, or SOURCE_REVISION's where the tree isn't a checkout.
revision=()
[[ -n "${SOURCE_REVISION:-}" ]] && revision=(-p:SourceRevisionId="$SOURCE_REVISION")

dotnet test "$repo/tests/RealisticBrakes.Core.Tests" -c Release -nologo -v quiet
# Every failed check, and the count.
"$repo/scripts/check-adapter.sh" | grep -E "^FAIL|adapter checks"
# The Multiplayer integration builds the mod and its model with it.
dotnet build "$repo/src/RealisticBrakes.Multiplayer/RealisticBrakes.Multiplayer.csproj" \
    -c Release -p:DvInstallDir="$dv" "${revision[@]}" --no-incremental -warnaserror -nologo

mkdir -p "$repo/dist"
stage="$(mktemp -d "$repo/dist/package.XXXXXX")"
trap 'rm -rf "${stage:?}"' EXIT
mkdir "$stage/RealisticBrakes"
cp "$out/Info.json" "$out/RealisticBrakes.dll" "$out/RealisticBrakes.Core.dll" \
    "$mp/RealisticBrakes.Multiplayer.dll" "$repo/LICENSE" "$repo/CHANGELOG.md" "$stage/RealisticBrakes/"
# The players' guide, as the mod's README.
cp "$repo/PLAYER-GUIDE.md" "$stage/RealisticBrakes/README.md"
# The brake booklet: its layout and exactly the pages it names, each of which must be there.
mkdir "$stage/RealisticBrakes/Booklet"
pages="$(python3 -c 'import json, sys; print(" ".join(json.load(open(sys.argv[1]))["pages"]))' "$booklet/booklet.json")"
files=(RealisticBrakes/Booklet/booklet.json)
cp "$booklet/booklet.json" "$stage/RealisticBrakes/Booklet/"
for page in $pages; do
    [[ "$page" =~ ^page-[0-9][0-9]\.jpg$ && -f "$booklet/$page" ]] || { echo "Missing or unexpected booklet page: $page" >&2; exit 1; }
    cp "$booklet/$page" "$stage/RealisticBrakes/Booklet/"
    files+=("RealisticBrakes/Booklet/$page")
done

# Select the archive's contents explicitly; never include assemblies copied
# for adapter checks, local settings, logs, or development documents.
rm -f "${zipfile:?}"
(cd "$stage" && zip -q -X "$zipfile" RealisticBrakes/Info.json RealisticBrakes/RealisticBrakes.dll \
    RealisticBrakes/RealisticBrakes.Core.dll RealisticBrakes/RealisticBrakes.Multiplayer.dll \
    RealisticBrakes/LICENSE RealisticBrakes/CHANGELOG.md RealisticBrakes/README.md "${files[@]}")
echo "Built $zipfile"
(cd "$(dirname "$zipfile")" && sha256sum "$(basename "$zipfile")")
