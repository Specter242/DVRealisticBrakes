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

## For other mods

`RealisticBrakes.Api` ([`src/RealisticBrakes/Api.cs`](src/RealisticBrakes/Api.cs))
is the public API. Call it by reflection, so your mod runs with or without
this one. Its names, units, and meanings stay the same from version to
version. Everything else in this mod (`RealisticBrakes.Sim`, the model's
classes) is internal and changes without notice, so don't read it.

```csharp
var api = UnityModManager.FindMod("RealisticBrakes")?.Assembly?.GetType("RealisticBrakes.Api");
var pressure = api?.GetMethod("Pressure");
var pipe = pressure != null ? (float)pressure.Invoke(null, new object[] { car, "BrakePipe" }) : float.NaN;
```

- `bool Active`: whether this mod is running the brake now. When it isn't,
  the game's own brake runs.
- `float Pressure(TrainCar car, string part)`: a part's pressure on the
  car now, in gauge bar (0 is atmospheric). The parts, named without
  regard to case:
  - `BrakePipe`: the car's own section of the brake pipe (the train's
    pipe pressure varies along it);
  - `AuxiliaryReservoir`;
  - `ControlReservoir`: European cars, and engines' distributors;
  - `EmergencyReservoir`: American cars;
  - `BrakeCylinder`;
  - `MainReservoir` and `EqualizingReservoir`: engines.

  It returns NaN where the car has no such part, for a name that isn't one
  of these, and when this mod isn't running the car's brake.
- `string Equipment(TrainCar car)`: `"European"` or `"American"`, what the
  car's brake runs on now (each engine has its own prototype's, the cars
  the setting's); null when this mod isn't running the car's brake.
- `string BrakeMode(TrainCar car)`: the G/P mode a European car's
  distributor runs in, `"Goods"` (slow to apply and release, for a long
  train) or `"Passenger"` (quick); engines and coaches run in Passenger.
  Null on American equipment, which has no changeover.
- `bool? CutOut(TrainCar car)`: whether the car's own brake is cut out by
  its isolating cock, the cut-out lever by its bleed handle: it runs
  unbraked, the pipe passing through.
- `bool? BrakeValveCutOut(TrainCar car)`: whether an engine's brake valve
  is cut out, so its train brake handle doesn't work the train's pipe, as
  on a trailing engine. This is apart from `CutOut`, the engine's own
  brake. Null for a car with no brake valve.

  `BrakeMode`, `CutOut`, and `BrakeValveCutOut` all return null when this
  mod isn't running the car's brake. They only read; the levers stay the
  player's.
- `float BailOff(TrainCar car)`: bails off an engine, as a click on its
  independent brake lever does. The engine lets go of the train brake's
  application then on, while the train's brakes stay applied, until the
  next release. It returns the cylinder pressure let off, in gauge bar,
  and 0 when there was nothing to bail or the car isn't an engine.

The game's own `BrakeSystem` fields carry this mod's pressures too, every
frame, in the game's absolute bar (1.0 is atmospheric): `brakePipePressure`
(that car's own section of pipe), `auxReservoirPressure`,
`controlReservoirPressure` (on American cars, their auxiliary reservoir's),
`brakeCylinderPressure` (through the game's smoothing), and
`mainReservoirPressure` on engines.

If you need something the API doesn't give, open an issue.
