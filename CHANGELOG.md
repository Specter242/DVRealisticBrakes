# Changelog

## 1.4.0 (2026-11-11)

- A notice when you apply the train brake before the cars have charged, as
  on a train you have just coupled. Cars start empty and take minutes to
  fill, and until then the engine brakes nearly alone. The readme and the
  booklet say how long to allow.

## 1.3.5 (2026-11-09)

- Engines whose cab handles are wired to controls of their own, such as the
  WE-4U, now get the added brake handle notches (emergency and the filling
  stroke), the bail-off click, and the text by the crosshair.
- Another mod's vigilance device or dead man that vents the brake pipe,
  such as the WE-4U's, now brakes the train.
- Fixed: with the realistic brake turned off, or an American steam engine
  put on European equipment, its independent brake rested a quarter applied
  until you left the cab and came back.
- Fixed: a car's G/P lever, cut-out cock, and retainer went back to where
  they started when the brake was turned off and on or the equipment was
  changed.
- Fixed: on every engine but the American steam engines, the bail-off hint
  didn't come back after the first bail, and a bail did nothing while the
  engine's brake was still letting go.
- Fixed: a tender or slug uncoupled from its engine with the brake on
  couldn't be bled.
- Fixed: cutting an American car back in during a brake application put the
  train into emergency.
- Fixed: cars the game spawns on a grade could roll away before their hand
  brakes were set.
- Less work each frame with many parked cars.
- If a game update stops the mod from working, it now says so in game and
  on its settings page, where before it only left the game's own brake
  running.

## 1.3.4 (2026-11-07)

- For other mods: `RealisticBrakes.Api` now reads each car's brake pressures
  (`Pressure`, in gauge bar), its equipment (`Equipment`), its G/P mode
  (`BrakeMode`), and its cut-outs (`CutOut`, `BrakeValveCutOut`), with fixed
  names and units. The GitHub readme lists them.

## 1.3.3 (2026-11-07)

- The EMD GP7 and the ALCo RS-11s now have American equipment, the 26-L.
- Works with the Caboose Brake Handle mod: its handle brakes the caboose
  from the caboose's own air, as it does without this mod.
- Fixed: on engines with a cab at each end, such as the Class DCB and the
  Class 750, both cabs' brake handles get the added notches, bail-off,
  and the text by the crosshair, not just one.

## 1.3.2 (2026-11-06)

- Fixed: on American equipment, the S282's own bleed valve now lets the
  engine's brakes off while the train's stay applied.
- Fixed: with European cars behind a steam engine on American equipment
  (the S282 by default), recharging in release left the front cars
  dragging once the handle went back to running, until each was bled.

## 1.3.1 (2026-11-05)

- Brakes no longer overheat and fade within a minute on a long downhill.
  Loaded American cars brake with their empty weight's force, so they
  needed a much harder application, and the game heated them by how hard
  they were applied rather than by the force they made. Now heat follows
  the force, so a grade heats the brakes as much as in the stock game.

## 1.3.0 (2026-11-04)

- **Each engine has its own prototype's brake equipment.** The S282,
  S060, and the American custom engines are American, with the H-6 and
  S-6 on the steam engines and the 26-L on the diesels. The stock diesels
  and the BR95 are European. The cars keep the brake equipment setting,
  and any engine's brake valve works any car. To have every engine use
  the setting, as before, tick "Every engine uses the brake equipment
  above".
- A new cut still settling when it was checked, three seconds after it
  appeared, is now held and gets its hand brakes once it stands, instead
  of being left unsecured to roll on a grade.

## 1.2.5 (2026-11-03)

- Trains the AI Traffic mod spawns and drives no longer get every hand
  brake put on as they spawn, which left them stuck with red-hot brakes.
  A parked cut that its brakes can't hold still gets its hand brakes.

## 1.2.4 (2026-11-02)

- The brake controls' text setting also hides the notices after a
  bail-off or the booklet button: off, they go to the log only. A warning
  that a multiplayer host runs another version still shows.

## 1.2.3 (2026-11-01)

- The S-6 independent (American equipment): lap holds the engine's brakes
  where they stand, applying or releasing, and a moment in running and
  back to lap lets off only part. Slow application is about twice as
  fast, about 10 seconds to the full 45 lb. The equalizing reservoir
  charges with the brake pipe.
- European equipment: a filling stroke leaves the train overcharged a
  little, up to 0.4 bar, which running lets down over about four minutes.
  The cars keep it: a cut left standing stays overcharged, and given an
  engine at the usual 5 bar, its brakes come on, as on the real thing.
  Give it a filling stroke and let it come down, or bleed the cars.
- A bail-off key, set in the mod's settings, bails off the engine you're
  on, as the click on the independent does.
- The brake controls' text setting hides the car levers' text too.
- A train with an M62 in it keeps the M62's own brake, which runs its
  whole train, and the log no longer warns that the two fight.
- The brake booklet covers the bail-off key and the overcharge.

## 1.2.2 (2026-10-31)

- Tenders brake with their engine: the independent, the train brake, and
  bailing off act on the tender too, and it has no car levers of its own.
- The S-6 independent has a release position, the notch below running,
  sprung back to running: hold the handle there to bail the engine off. It
  replaces the click and hold, which a handle without notches keeps. An S-6
  handle saved in lap may come back in running; set it again.
- American cars' emergency reservoirs keep their air through a release, as
  the AB valve's did, and give it only in an emergency or to the bleed rod.
- The brake booklet is given once, into your inventory with the game's own
  booklets, on a new game or the next time a save loads. It's saved with
  the game and yours to keep or throw away; the settings' button spawns
  another.

## 1.2.1 (2026-10-31)

- The S-6 independent's handle springs back from quick application when let
  go, even after it's been held there with a key, the scroll wheel, or the
  mouse.
- On American equipment a closed angle cock keeps its hose's air, as AAR
  cocks do: close the side you keep, and the open side dumps when the hoses
  part. Only European end cocks vent.
- A standing cut whose brakes hold it comes back to rest after a nudge,
  instead of creeping away.
- The Wardale 5AT's Kss brake plugin works with Realistic Brakes.

## 1.2.0 (2026-10-30)

- **The Air-Brake Pocket-Book:** an in-game booklet on how the brakes work,
  for each engine's brake valve on European and American equipment, the
  cars, and the settings. Spawn it from the mod's settings; press a line on
  its contents page or a tab down its edge to turn to that chapter.
- **The S-6 independent, with American equipment:** set the brake
  equipment to American in the settings, and the steam engines' and DM3's
  independent is the E-T's S-6, to go with their H-6 train brake. It isn't self-lapping: running, lap,
  slow application, and quick application. It releases in running only
  with the train brake in running too, and in lap it holds the engine's
  brakes through a release. European equipment, the default, keeps the
  self-lapping independent.
- With American equipment, the H-6's equalizing reservoir charges from the
  main reservoir in release and follows the brake pipe in running and
  holding, and running feeds the pipe only up to 5 bar.
- Closing an angle cock vents its hose, so an open cock coupled to a closed
  one dumps the brake pipe, as on real cars.
- Car levers on cars without a set position stand farther apart, and the
  levers no longer knock each other back.
- Trains driven by AI Traffic's AI keep the game's own brakes, which its
  drivers are made for; a train a worker hands back gets this mod's.

## 1.1.0 (2026-10-28)

- On American equipment, the steam engines and the DM3 have the E-T brake
  valve. A holding position between running and lap keeps the engine's
  brakes on while the train releases, and release blows a warning hiss.
  The engine's train brake comes off only in running, all at once.
- On those engines, click and hold the independent lever to bail off a
  little at a time.
- Their service is slower, lap no longer makes up leaks, an emergency
  brakes the engine harder, the independent gives 3.1 bar, and the air pump
  keeps the main reservoir about 1.4 bar over the pipe in running.
- American cars near the engine release and recharge more slowly, so a
  long train releases together. Retainers let off at the real pace, and
  each car brakes a little differently.
- American coaches release in steps and brake harder in an emergency.
- New setting: American cars' load braking. By default they have no
  empty/load valve, as in the steam era, so loaded trains stop much longer;
  the other choice is automatic empty/load.
- Looking at an engine's train brake, independent, or brake valve cut-out
  shows where it stands (not in VR). A setting turns it off.
- A brake handle left in release, holding, or emergency stays there through
  a save, and a remote or driver mod that applies the brakes takes it out.

## 1.0.2 (2026-10-28)

- A car's levers keep showing their setting when you walk away and come
  back, instead of jumping to their first position.
- Steam engines' stock air pumps take about the steam a real pump of their
  time did, a fifth of what they took, so recharging a train no longer
  drains the boiler. Realistic Firing's pumps are unchanged.

## 1.0.1 (2026-10-28)

- Holding a steam engine's release or a diesel's filling stroke no longer
  leaves the brakes dragging once the handle is back in running, and
  braking soon after a filling stroke releases fully again.
- American equipment: coming back from a steam engine's release no longer
  sets off an emergency, and the diesels have no filling stroke, as the
  real 26-L valve has none.
- The independent brake lets off in about 4 seconds instead of 20.

## 1.0.0 (2026-10-27)

First release.

- The air brake, car by car: every car has its own brake pipe, reservoirs,
  and valve. Applications travel down the train, and long trains release
  and recharge in minutes, in real time.
- European (UIC) equipment by default, American (AB) by a setting.
- The steam engines' and DM3's brake valves make and hold a reduction,
  with an equalizing reservoir hand on the main reservoir gauge. The other
  diesels' handles gain a filling stroke and emergency.
- Car levers beside each bleed handle, with their positions printed round
  them.
- Freight cars brake with their own force.
- Dead engines take their brakes' air from the train.
- The bleed handle releases a car and keeps its air; holding it drains the
  car.
- Click the independent brake lever to bail off.
- Hand brakes hold only what they can.
- Multiplayer: the brake settings and controls sync through the host.
- Saves keep each car's brake state.
