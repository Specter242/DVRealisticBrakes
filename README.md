# Realistic Brakes for Derail Valley

A Unity Mod Manager mod that rebuilds Derail Valley's air brake car by car.
Every car has its own length of brake pipe, its own reservoirs, and its own
valve, so a brake application travels down the train in real time. European
(UIC) or American (AB) equipment, steam brake valves with an equalizing
reservoir, car levers, hand brakes that hold only what they can, and
bailing off, with optional Multiplayer integration.

Requires Derail Valley and Unity Mod Manager 0.28.2 or newer. Game and
third-party mod assemblies are not included.

Download the installable ZIP from
[GitHub Releases](https://github.com/Specter242/DVRealisticBrakes/releases/latest).
The [player guide](PLAYER-GUIDE.md) explains what changes from the stock brake
and how to drive with it; the ZIP carries it as `README.md`. See the
[changelog](CHANGELOG.md) for version history. Release tags contain sanitized
source snapshots; older tags describe the behavior of that version.

Build with the .NET 10 SDK and your own installed game assemblies:

```sh
dotnet build RealisticBrakes.slnx -c Release -p:DvInstallDir="/path/to/Derail Valley"
dotnet test tests/RealisticBrakes.Core.Tests -c Release
```

The brake model (`src/RealisticBrakes.Core`) has no game references, and its
tests run without the game. On Linux, `scripts/check-adapter.sh` also checks
the game adapter against your installed game under Mono; set
`DV_INSTALL_DIR` for a custom game location.

```sh
DV_INSTALL_DIR="/path/to/Derail Valley" scripts/package.sh
```

The package is written to `dist/RealisticBrakes-<version>.zip`. Extract its
`RealisticBrakes` folder into the game's `Mods` directory.
