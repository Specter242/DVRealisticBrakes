# Realistic Brakes for Derail Valley

The game's air brake, rebuilt car by car. Every car has its own length of
brake pipe, its own reservoirs behind a check valve, and its own valve, so a
brake application travels down the train as it does on a real one. Long
trains take time to apply and minutes to release and recharge, loaded cars
stop slower than empty ones, and the steam engines' brake valves make a
reduction you can hold. A setting puts the whole world on the automatic
vacuum brake instead.

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

**A train you have just coupled needs time to charge.** Cars start with
empty reservoirs, and they fill in real time: about five minutes for
fifteen cars, ten for thirty. Until then the engine brakes nearly alone,
though the pipe gauge already reads almost full. Apply the train brake
while many of the cars are still short of air and a notice says how many;
the pressure overlay (in the settings) shows them filling.

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
  - A stroke leaves the train **overcharged** a little, up to 0.4 bar,
    which running lets down over about four minutes. The cars keep it: a
    cut left standing stays overcharged, and given an engine at the usual
    5 bar, its brakes come on, as on the real thing. Give it a filling
    stroke and let it come down, or bleed the cars.
  - **Past full service** is **emergency**. It stays until you move it
    back.

  The remote and driver mods use the handle's usual range only.
- **Manual-lap handles** (the S282, S060, DM3, and the Custom Car Loader
  steamers): the handle's positions are release, running, lap, service,
  and emergency. The American engines among them (the S282, S060, and
  American custom engines) have the E-T: the H-6 brake valve and the S-6
  independent. On European equipment their independent is self-lapping.
  - **Release**, one notch before running, is new. It feeds the main
    reservoir straight into the pipe to release a long train quickly. It
    stays where you leave it, as on the real valve. Left there, it
    overcharges the train, and running then lets the extra pressure down
    over a few minutes.
  - In **service** the equalizing reservoir comes down while you hold the
    handle there. Move to **lap** when it reaches the reduction you want,
    and the pipe follows and holds.
  - On an **American engine** the handle has the real H-6's
    **holding** position between running and lap: the train releases and
    recharges as in running, but the engine's own brakes stay on. Release,
    holding, then running is the real drill on a long train. In release
    the valve blows a steady hiss, as the real one's warning port did, to
    remind you the train is overcharging. The engine's
    train brake lets go only in running, all at once. Service brings the
    pipe down about 0.35 bar every 2 seconds, and in lap or service the
    valve doesn't make up leaks, so a leaky train keeps braking harder.
  - A handle left in release, holding, or emergency stays there through a
    save. A remote or a driver mod that applies the brakes takes it out.
  - The **main reservoir gauge** now has two hands: red for the main
    reservoir, blue for the equalizing reservoir (ER). On the game's own
    engines it and the pipe and cylinder gauge are labeled. A custom
    engine whose gauge has an equalizing reservoir hand of its own, as
    the FPD-4's white hand, keeps it, and the hand reads the equalizing
    reservoir. The overlay shows the equalizing reservoir as well.
- **Looking at the brake handles** (not in VR) shows where they stand by
  the crosshair: the train brake's position (with the equalizing
  reservoir or the reduction), the independent's application and the
  train brake's on the engine, and the brake valve's cut-out; on a car,
  its brake levers. A setting turns all of it off.
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
    pressure, slow exhaust) and the cut-out cock. On LP or HP the
    cylinder comes down to 10 or 20 lb and stays there for as long as
    the handle is up; only EX lets it go, or SD, slowly.

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
  brakes let go, and the train's stay on. On **European** engines, a quick
  click still lets off the whole application at once, while clicking and
  holding the lever still lets the engine's brakes off a little at a time
  (about 1 bar a second) until you let go; what's left stays on. Engines
  joined by MU cable bail off together. A further reduction applies the
  engine again; a release clears the bail. A setting turns the click off.
  A bail-off key, set in the mod's settings, does the same from wherever
  you stand in the cab.
  - An **American** steam engine bails off a little at a
    time, as the real E-T does: hold the independent in release, the notch
    below running, and the engine's brakes let off for as long as you hold
    it there; it springs back to running. On a handle without notches,
    click and hold the lever instead (in VR, each trigger press lets off
    some). What's left stays on. After an emergency from the engine's own
    handle, the brakes come back as soon as you let go, as on the real E-T.
  - Its independent is the E-T's S-6, which isn't self-lapping: release,
    running, lap, slow application, and quick application (release springs
    back to running, quick application to slow). Slow application builds
    gradually up to 3.1 bar; lap it where you want it. It lets go in
    running only with the train brake in running too, and in lap it holds
    the engine's brakes through the train's release. A moment in running
    and back to lap lets off only part.

## Brakes won't release?

Check these first, or turn on the **Brake pressure overlay** in the settings
to see each car's pressures and flags (`HB`, `CUT OUT`, `VALVE OUT`,
`HOSE OFF`, `COCK SHUT`):

- **Still charging or releasing:** long trains take real time. A freshly
  coupled cut charges in minutes (about five for fifteen cars, ten for
  thirty), and a freight car in **G** takes 45–60 s to release after the
  pipe comes back up at that car.
- **Hand brakes on spawned cuts:** the mod sets enough hand brakes on
  spawned or loaded cuts to hold them on a grade. Check the cut (or look
  for `HB` in the overlay) and release them.
- **Brake valve cut out:** make sure the leading engine's train brake
  cut-out cock is **cut in** (not `VALVE OUT`), and trailing engines are
  cut out.
- **S282 / S060 E-T engine brake stuck on:** the S282, S060, and American
  custom steam engines have the E-T equipment. Their own cylinders release
  only when **both** the train brake (H-6) and the independent (S-6) are in
  **running** — leaving the train brake in release or holding, or the S-6
  in lap, holds the engine's brakes on.
- **Overcharged cut behind a 5 bar engine:** a cut left overcharged after a
  filling stroke or release position applies its brakes when coupled to an
  engine holding 5.0 bar. On a European engine, give the train a filling
  stroke (or use the Angleichen key) and let it come down; on an American
  diesel (which has no filling stroke) or any engine, pull each car's bleed
  handle.
- **American emergency lockout:** after an emergency on American (AB)
  equipment, the valves lock out recharge for about 60 seconds before the
  pipe will build back up and release.
- **Uncoupled hoses, closed end cocks, cut-out cars, or retainers:** check
  that every coupled end has its air hose connected (`HOSE OFF` in the
  overlay) and both end cocks open (`COCK SHUT`), that no car's
  isolating/cut-out cock is closed (`CUT OUT`), and on American cars that
  no retaining valve is left up in LP, HP, or SD (set it to **EX** to let
  the cylinder exhaust).

## European or American equipment

Each engine carries its own prototype's equipment. The **S282**, **S060**,
and the American custom engines (the FEF-3, the Union Pacific 4-8-4s, the
2901, the Hudson, the C-16, the EMC E-series, the FPD-4, the GP7, the
RS-11s, the JK3, and the QJ) are **American**. The steam engines among them get the E-T, the H-6
brake valve with holding and the S-6 independent, and the diesels the 26-L.
The stock diesels and the BR95 are **European**.

The brake equipment setting chooses the cars' equipment, all of them, and
that of any engine not named here. **European** is the default. The
setting under it, "Every engine uses this equipment", puts every engine on
it instead. Any engine's brake valve works any car, as both
kinds work off the same 5 bar pipe.

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
    fast. The pipe can't be recharged until about a minute has passed. The
    emergency reservoir keeps its air through a release; it gives it only
    in an emergency or to the bleed rod.
  - Holding the steam engines' handle in release overcharges the cars'
    reservoirs. Back in running, the extra pressure bleeds away over up to
    ten minutes, and the brakes stay off. Brake in that time and the
    application comes on harder than usual.
  - On a long train the cars near the engine let off more slowly and
    recharge more slowly, so the whole train releases at about the same
    time.
  - Each car brakes a little harder or softer than the next, as real
    cars' piston travel varied.
  - Coaches release in steps, like the European brake, and an emergency
    gives them about a sixth more than full service.
  - The steam engines' air pump keeps the main reservoir about 1.4 bar
    over the pipe in running and holding, and up to 7.6 bar in lap and
    service, as the real duplex governor did. The independent brake is the
    S-6 (see Bailing off), up to 3.1 bar, and an emergency puts 4.5 bar in
    the engine's cylinders (4.7 bar from the engine's own handle).

## Loaded and empty cars

A freight car brakes with its own force, not its load's. A full car stops as
it did in the stock game, and a lighter load stops shorter. Each car's
empty/loaded valve is set from its load: empty cars brake gently enough
not to slide their wheels. A setting turns this off.

American cars have no empty/loaded valve by default, as in the steam era:
the cylinders take their full pressure, but every car brakes with the force
sized for it empty, so a loaded train takes much longer to stop. Use the retainers on long grades. A setting gives them the modern
automatic empty/load equipment instead.

## Vacuum brake

Choose **Vacuum** as the brake equipment and every engine and car in the
world runs the automatic vacuum brake at 21 inches, as British railways did.
The Air-Brake Pocket-Book's chapter 7 has the whole of it.

- **Steam engines:** the air pump's handwheel is the small ejector's steam
  valve, wherever the engine has it (on the S282 and S060, outside the cab).
  Open it and running holds the vacuum. The handle's release position works
  the large ejector, even with the wheel shut. Lap cuts the pipe off, and
  apply lets air in while it's held. The engine and tender brake by steam,
  following the pipe, and that can't be bailed off. The ejectors are heard
  at the chimney; "Vacuum ejectors' volume" in the settings sets how loud,
  from 0 to 200 %.
- **Diesels:** the exhausters run with the engine. The handle sets the vacuum
  from 21 inches down to none, and the filling stroke runs them at full speed.
  The engine's own air brake follows the pipe; click the independent lever to
  bail it off.
- **Gauges:** the brake pipe gauge becomes a 0–30 inch vacuum gauge, the red
  hand showing the nearest car's chamber. On steam engines the main reservoir
  gauge shows the steam brake.
- **Cars:** the bleed handle is the release cord. There are no lever rows.

## The brake booklet

*The Air-Brake Pocket-Book*, after the Westinghouse instruction books: how
the brakes work, each engine's brake valve on European, American, and
vacuum equipment, the cars' valves, the levers at the car, the independent and
bailing off, and the settings. It's given once, into your inventory with
the game's own booklets: on a new game, or the first time a save loads with
the mod. It's saved with the game and yours to keep, drop, or throw away. To
get another, open the mod's settings in a loaded game and press **Spawn the
brake booklet**; it appears in front of you. With the Comms Tablet 0.1.4 or
newer, **Give brake booklet** at the top of Realistic Brakes' section in the
tablet's Settings app does the same. Press a line on its contents page, or a
tab down its edge, to turn to that chapter.

## Settings

In the mod's settings window, in four groups. Hover over a setting's "?"
for what it does.

**Brake model**

- **Run the realistic brake:** off gives the game's own brake back at once,
  from the pressures this one left.
- **Brake equipment:** European or American, for the cars and for the
  engines whose own isn't known; or Vacuum, for every engine and car.
- **Every engine uses this equipment:** off by default, so each engine has
  its own prototype's equipment; on, every engine uses the setting. Not
  shown with Vacuum, which every engine uses.
- **Vacuum cars have direct admission valves** (on by default, shown with
  Vacuum): a long train brakes in about half a minute; off, as before
  them, it takes about two.
- **Freight cars' braking for their load:** Period (no empty/load valve,
  the default) or EmptyLoad (automatic). Shown with American and vacuum
  equipment.
- **Freight cars brake with their own force:** loaded and empty cars stop
  differently.
- **Freight cars' G/P lever** as cars come into service.
- **Power cut by the brakes** (on by default): a diesel or electric loses
  power while a European engine's brake pipe is under 3.5 bar, or once an
  American engine's PC switch opens on an emergency. The throttle drops to
  idle; power comes back once the pipe has recharged (over 4.5 bar) and
  the throttle is closed. Steam engines aren't affected.

**Cab**

- **Train brake handles gain notches:** self-lapping handles gain a filling
  stroke and emergency, and manual-lap handles (steam engines and the DM3)
  gain release (and holding, on American engines). On by default; it takes
  effect as a cab loads.
- **Click the independent brake to bail off:** on by default.
- **Equalizing reservoir hand on the main reservoir gauge** of the
  manual-lap engines: on by default; it takes effect as a cab loads.
- **Vacuum ejectors' volume, %** (shown with Vacuum): 100 by default, 0
  silences them; it takes effect at once.

**Display**

- **Brake handle positions by the crosshair:** on by default. It covers the
  bleed handle text and the notices after a bail-off, reservoir draining,
  or the booklet button too; off, notices go to the log only. A warning
  that a multiplayer host runs another version still shows.
- **Brake pressure overlay** (off by default): for the train you're on:
  pipe, control (or, on American cars, emergency) reservoir, auxiliary
  reservoir, and cylinder, in gauge bar, plus status flags (`HB`,
  `CUT OUT`, `VALVE OUT`, `HOSE OFF`, `COCK SHUT`).

**Keys**

- **Bail-off key:** unbound until you set it. It bails off the engine
  you're on, as the click does (on European engines, a quick tap bails in
  full and holding graduates off), even with the click turned off.
- **Angleichen key** (European brake valves): unbound until you set it.
  Held, it raises the brake pipe slowly, to at most 5.5 bar, to free car
  brakes that stick at 5.0; let go, it comes down at 0.1 bar a minute.

## Compatibility

- **Leak Down:** works.
- **Caboose Brake Handle:** works. Its handle brakes the caboose from the
  caboose's own air.
- **Realistic Firing:** use 1.16.2 or newer, which leaves bailing off to
  this mod; older versions also act on the click to bail off. Its air
  pumps work.
- **Valve 394, Self-Lapping Brakes, and other brake mods:** use one brake
  mod at a time. The log names any that replace the same parts.
- **The M62:** it brakes its train with its own 394 valve, so a train with
  an M62 in it keeps the M62's brake and the game's; your other trains
  keep this mod's.
- **Custom Car Loader:** its steamers get the steam engines' brake valve,
  and their tenders brake with the engine. Its cars get the levers where
  they have a bleed handle.
- **Multiplayer:** everyone in a session needs this mod. Guests use the
  host's brake settings for the session, and what anyone does to the car
  levers, a held bleed, a bail-off, or a diesel handle's added notches
  reaches everyone. The host's brakes run the physics. Not yet tried in a
  session.

## Not yet

- Faults: leaks, sticking valves, and the like.
