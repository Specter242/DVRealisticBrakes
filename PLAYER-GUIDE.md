# Realistic Brakes for Derail Valley

The game's air brake, rebuilt car by car. Every car has its own length of
brake pipe, its own reservoirs behind a check valve, and its own valve, so a
brake application travels down the train as it does on a real one. Long
trains take time to apply and minutes to release and recharge, loaded cars
stop slower than empty ones, and the steam engines' brake valves make a
reduction you can hold.

## What changes from the stock brake

- **The train's pipe:** the stock game gives the whole train one brake pipe
  pressure, so every car brakes at once. Here each car has its own length
  of pipe, and an application travels down the train.
- **Loaded and empty cars:** stock brake force grows with the car's weight,
  so every car stops alike. Here a car brakes with its own force: a full
  car stops as before, a lighter one shorter.
- **The steam engines' and DM3's valve:** the stock handle vents the pipe directly. Here
  an equalizing reservoir sets the reduction, and lap holds it.
- **The other diesels' handle:** the stock one stops at full service. Here it has
  emergency, and a filling stroke past release.
- **Car equipment:** stock cars have one kind of valve and nothing to set.
  Here they're European (G/P) or American (retainers, emergency
  reservoirs), with a lever row beside each bleed handle.
- **The bleed handle:** the stock one empties the cylinder at once. Here it
  works the car's release valve and keeps its air.
- **Hand brakes:** a stock hand brake is a full application and can hold a
  whole train on a grade. Here it's half an application and holds only
  what it can.

## Install

Requires Unity Mod Manager 0.28.2 or newer. Tested on Derail Valley Build 99.

1. In Unity Mod Manager, open the Mods tab and install this zip, or extract
   the `RealisticBrakes` folder into the game's `Mods` folder.
2. Start the game. The settings are in Unity Mod Manager's window
   (Ctrl+F10).

## How fast the air works

The brakes run at **real time**: a freight car's cylinder takes its real
18–30 s to fill, and a long train takes minutes to release and recharge.
The game's **Main Reservoir Fill Time** (in the session settings) speeds up
only the engines' air pumps, as in the stock game. At the default **100 %**
they fill ten times as fast as real ones.

A steam engine's air pump takes about the steam a real pump of its time
took for the air it makes, a fifth of what the stock S282's and S060's
pumps took, since a train's real reservoirs keep it running for minutes
while they recharge.

## Driving

- **Self-lapping handles** (the diesels): move the handle as before. Each
  position asks for a reduction of the brake pipe, up to full service at
  1.5 bar. The engine holds the pipe there. The handle now goes one notch
  further each way:
  - **Past release** is a **filling stroke**, to charge a long train
    quickly. It starts once you've held the handle there for 0.4
    seconds (a quick scroll past release does nothing), and the handle
    springs back to release when you let go. Holding the brake-release key
    or turning the wheel on works too. American equipment has no filling
    stroke, as the real 26-L valve has none.
  - **Past full service** is **emergency**. It stays until you move it
    back.

  The remote and driver mods use the handle's usual range only.
- **Manual-lap handles** (the S282, S060, DM3, and the Custom Car Loader
  steamers): the handle's positions are release, running, lap, service,
  and emergency.
  - **Release**, one notch before running, is new. It feeds the main
    reservoir straight into the pipe to release a long train quickly. It
    stays where you leave it, as on the real valve. Left there, it
    overcharges the train, and running then lets the extra pressure down
    over a few minutes.
  - In **service** the equalizing reservoir comes down while you hold the
    handle there. Move to **lap** when it reaches the reduction you want,
    and the pipe follows and holds.
  - The **main reservoir gauge** now has two hands: red for the main
    reservoir, blue for the equalizing reservoir (ER). The pipe and
    cylinder gauge is labeled too. The overlay shows the equalizing
    reservoir as well.
- **Long trains:** the rear brakes later and slower than the front. A
  release reaches the rear last, and the cars' reservoirs take minutes to
  recharge.
- **Leaks and slow drifts** don't apply the brakes, but a reduction does.
- **The bleed handle** on each car works its release valve:
  - **Click it** for a short pull. The car lets go of its brake and stays
    released, keeping its reservoirs' air:
    - a European car lets go within seconds, then its cylinder empties at
      its G or P release time;
    - an American car's cylinder empties in about 20 s, and the car stays
      released until its pipe is recharged.
  - **Hold it** past 3 seconds to drain the car's reservoirs too, for as
    long as you hold it (about 50 s to empty). This takes a car off air
    before you set it out.
- **Car levers:** beside each car's bleed handle, a row of levers sets
  the car's brake. Each lever's positions are printed round it (G and P,
  IN and OUT, EX, LP, HP, and SD), so its setting reads at a glance; hover
  one for more.
  - **European freight cars:** the G/P lever and the isolating cock.
    Coaches have the cock only.
  - **American cars:** the retaining valve (exhaust, low pressure, high
    pressure, slow exhaust) and the cut-out cock.

  A car with its cock closed runs unbraked.
- **Hand brakes** give half a full application, and they hold only what
  they can. A loaded car's hand brake holds it alone on a steep grade, but
  a cut on a grade needs several: about one car in three on 1 %. A cut that
  can't hold rolls. Cars the game spawns or loads get enough hand brakes
  set to stay put.
- **The independent brake** applies in about 3 seconds and lets off in
  about 4. The engine's train brake lets off at a passenger brake's pace,
  about 18 seconds.
- **Bailing off:** with the train brake applied, click the independent
  brake lever (in VR, hold it and press the trigger). The engine's own
  brakes let go, and the train's stay on. Engines joined by MU cable bail
  off together. A further reduction applies the engine again; a release
  clears the bail. A setting turns the click off.

## European or American equipment

A setting chooses the cars' equipment. **European** is the default.

- **European (UIC distributors):**
  - Releases in steps: let the pipe up a little and the brakes come off a
    little.
  - Each freight car runs in **G** (goods: the cylinders take 18–30 s to
    fill and 45–60 s to release) or **P** (passenger: 3–5 s and 15–20 s). A
    setting picks the freight cars' lever; coaches run in P.
  - The reservoirs top up whenever the pipe stands above them, so repeated
    applications don't use the brake up.
- **American (AB valves):**
  - Any release releases in full: there is no partial release.
  - The reservoirs recharge only once released. Apply again too soon and
    the brakes come up short; keep doing it and the train runs out of air.
  - An emergency adds each car's emergency reservoir, about a fifth more
    braking, and vents the pipe at every car, so it runs down the train
    fast. The pipe can't be recharged until about a minute has passed.
  - Holding the steam engines' handle in release overcharges the cars'
    reservoirs. Back in running, the extra pressure bleeds away over up to
    ten minutes, and the brakes stay off. Brake in that time and the
    application comes on harder than usual.

## Loaded and empty cars

A freight car brakes with its own force, not its load's. A full car stops as
it did in the stock game, and a lighter load stops shorter. Each car's
empty/loaded valve is set from its load: empty cars brake gently enough
not to slide their wheels. A setting turns this off.

## Settings

- **Run the realistic brake:** off gives the game's own brake back at once,
  from the pressures this one left.
- **Car brake equipment:** European or American.
- **Brake force from the car:** loaded and empty cars stop differently.
- **Freight cars' G/P lever** as cars come into service.
- **Train brake handles' added notches:** the diesels' filling stroke and
  emergency, and the steam engines' release. On by default; it takes
  effect as a cab loads.
- **Equalizing reservoir hand** on the steam engines' main reservoir
  gauge: on by default; it takes effect as a cab loads.
- **Click to bail off:** on by default.
- **Show each car's brake pressures** (off by default): an overlay for the train you're on:
  pipe, control (or, on American cars, emergency) reservoir, auxiliary
  reservoir, and cylinder, in gauge bar.

## Compatibility

- **Leak Down:** works.
- **Realistic Firing:** use 1.16.2 or newer, which leaves bailing off to
  this mod; older versions also act on the click to bail off. Its air
  pumps work.
- **Valve 394, Self-Lapping Brakes, and other brake mods:** use one brake
  mod at a time. The log names any that replace the same parts.
- **Multiplayer:** everyone in a session needs this mod. Guests use the
  host's brake settings for the session, and what anyone does to the car
  levers, a held bleed, a bail-off, or a diesel handle's added notches
  reaches everyone. The host's brakes run the physics. Not yet tried in a
  session.

## Not yet

- Faults: leaks, sticking valves, and the like.
