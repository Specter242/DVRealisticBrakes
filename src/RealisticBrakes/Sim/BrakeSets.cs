using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using DV.Simulation.Brake;
using HarmonyLib;
using RealisticBrakes.Core;
using UnityEngine;

namespace RealisticBrakes.Sim
{
    /// <summary>
    /// Runs each of the game's brake sets as a <see cref="BrakeLine"/>. A
    /// set's cars get their <see cref="CarAir"/> as they first come under
    /// the model, and a set's line is built when the game makes the set
    /// (cars couple or uncouple, cocks open or close), carrying over the
    /// hoses' flows from the lines its cars ran in before.
    /// </summary>
    internal static class BrakeSets
    {
        private static readonly PipeGeometry AirPipe = PipeGeometry.Uic();

        /// <summary>The 1¼ in air pipe.</summary>
        public static PipeGeometry Geometry => AirPipe;

        private static readonly ConditionalWeakTable<BrakeSystem, CarAir> cars = new ConditionalWeakTable<BrakeSystem, CarAir>();
        private static readonly ConditionalWeakTable<Brakeset, BrakeLine> lines = new ConditionalWeakTable<Brakeset, BrakeLine>();

        // Bumped when the model comes back on, so every car is read afresh from the stock brake that ran meanwhile.
        private static int generation;
        private static bool stale;

        // The car last found, and its air. The game asks after the same car
        // some ten times running in its tick (each stock air method, the
        // readouts, the heat), each a look in the weak table: about 1 µs a
        // car a frame, 0.4 ms at 414 cars (in game, 2026-11-09 and 11-11).
        private static BrakeSystem lastSystem;
        private static CarAir lastAir;

        // Bumped whenever a car's air is dropped or taken on: a set's airs
        // (SetAirs) are known still to be its cars' while it stands.
        private static int carsChanged;

        /// <summary>The air of the car with <paramref name="system"/>, if the model has taken it on.</summary>
        public static bool TryGet(BrakeSystem system, out CarAir air)
        {
            if (lastAir != null && ReferenceEquals(system, lastSystem))
            {
                air = lastAir;
                return air.Generation == generation;
            }
            if (!cars.TryGetValue(system, out air))
                return false;
            lastSystem = system;
            lastAir = air;
            return air.Generation == generation;
        }

        // Drops a car's air from the table, and from what is remembered of it.
        private static void Drop(BrakeSystem system)
        {
            cars.Remove(system);
            carsChanged++;
            if (ReferenceEquals(system, lastSystem))
            {
                lastSystem = null;
                lastAir = null;
            }
        }

        /// <summary>Re-reads every car's air on the next tick, as when the equipment setting changes.</summary>
        public static void Invalidate() => stale = true;

        /// <summary>Whether every car's air is about to be read afresh, its present valves to be replaced.</summary>
        public static bool Stale => stale;

        /// <summary>
        /// Notes that the stock brake is running <paramref name="set"/>, so the
        /// model's state goes stale until it runs again, and gives the set's
        /// cars back the game's own brake force.
        /// </summary>
        public static void MarkStale(Brakeset set)
        {
            stale = true;
            foreach (var system in set.cars)
                if (cars.TryGetValue(system, out var air))
                    air.RestoreForce();
        }

        // AI Traffic's driver (Killermops27's AITraffic.Driver.AIEngineer, put on its train's lead engine), looked up by name once.
        private static Type aiDriver;
        private static bool aiDriverLooked;

 public static bool AiDriven(Brakeset set)
        {
            if (AiDriver == null)
                return false;
            foreach (var system in set.cars)
                if (system != null && system.hasCompressor && AiDrives(TrainCar.Resolve(system.gameObject)))
                    return true;
            return false;
        }

        /// <summary>Whether AI Traffic drives any engine of <paramref name="set"/>, a cut of coupled cars (<see cref="HandBrakeHold"/>).</summary>
        public static bool AiDriven(Trainset set)
        {
            if (AiDriver == null || set == null)
                return false;
            foreach (var car in set.cars)
                if (car != null && car.brakeSystem != null && car.brakeSystem.hasCompressor && AiDrives(car))
                    return true;
            return false;
        }

        /// <summary>Whether AI Traffic is loaded.</summary>
        public static bool AiTrafficPresent => AiDriver != null;

        private static bool AiDrives(TrainCar car) => car != null && car.GetComponent(AiDriver) != null;

        private static Type AiDriver
        {
            get
            {
                if (!aiDriverLooked)
                {
                    aiDriverLooked = true;
                    aiDriver = AccessTools.TypeByName("AITraffic.Driver.AIEngineer");
                    if (aiDriver != null)
                        Main.Mod?.Logger.Log("AI Traffic is here: the trains its drivers run keep the game's own brake");
                }
                return aiDriver;
            }
        }

        // The M62's own brake (helmutgross's M62-130, Nexus mod 1692; its M62Logic.M62_394_Behavior on each M62), looked up by
        // name once.
        private static Type m62Brake;
        private static bool m62BrakeLooked;

 public static bool RunByM62(Brakeset set)
        {
            if (!m62BrakeLooked)
            {
                m62BrakeLooked = true;
                m62Brake = AccessTools.TypeByName("M62Logic.M62_394_Behavior");
                if (m62Brake != null)
                    Main.Mod?.Logger.Log("The M62 is here: trains with an M62 in them keep its own brake");
            }
            if (m62Brake == null)
                return false;
            foreach (var system in set.cars)
                if (system != null && system.hasCompressor && system.GetComponent(m62Brake) is Behaviour brake && brake.isActiveAndEnabled)
                    return true;
            return false;
        }

        /// <summary>
        /// Leaves <paramref name="set"/> to the game's own brake: its cars'
        /// air is forgotten and their force given back, so the stock brake
        /// runs them; if the driver leaves (a worker hands the train over), the
        /// model takes them on afresh from the stock brake's pressures.
        /// </summary>
        public static void LeaveToStock(Brakeset set)
        {
            lines.Remove(set);
            foreach (var system in set.cars)
                if (system != null && cars.TryGetValue(system, out var air))
                {
                    air.RestoreForce();
                    Drop(system);
                }
        }

        /// <summary>The line running <paramref name="set"/>, if built.</summary>
        public static bool TryGetLine(Brakeset set, out BrakeLine line) => lines.TryGetValue(set, out line);

        /// <summary>
        /// Forgets a car's air as the game resets the car for reuse, giving its
        /// bogies back the game's force first (the game sets it only once, as
        /// the car is first made) and dropping any save entry held for it.
        /// The game's reset leaves the auxiliary reservoir as the last car had
        /// it: an American car reused with air in it applied from that air as
        /// it spawned, where a new one stands released. It gets a new car's.
        /// </summary>
        public static void Forget(BrakeSystem system)
        {
            if (cars.TryGetValue(system, out var air))
                air.RestoreForce();
            system.auxReservoirPressure = NewAuxiliaryReservoir;
            Drop(system);
            SavedAir.Discard(system);
            Net.Forget(system);
        }

        /// <summary>A new car's auxiliary reservoir, bar absolute (the stock field's initial value).</summary>
        internal const float NewAuxiliaryReservoir = 1f;

        /// <summary>Whether the model ran <paramref name="system"/>'s car this frame.</summary>
        public static bool Handles(BrakeSystem system) => Main.Active && TryGet(system, out var air) && air.Handled;

 internal static bool Vents(HoseAndCock hose) =>
            hose != null && hose.cockOpen
            && (hose.connectedTo == null || (!hose.connectedTo.cockOpen && Main.Settings.Equipment == Equipment.European));


        /// <summary>Advances <paramref name="set"/>'s air <paramref name="dt"/> s and writes it back to the stock fields.</summary>
        public static void Tick(Brakeset set, float dt)
        {
            if (stale)
            {
                stale = false;
                generation++;
            }
            var kept = AirsFor(set);
            var line = kept.Line;
            var pumpSpeedUp = set.firstCar.gameParams?.CompressorProductionModifier ?? 1f;
            var pipe = line.Pipe;
            var frontOpen = Vents(set.GetEndmost(front: true));
            var rearOpen = Vents(set.GetEndmost(front: false));
            var changed = frontOpen != pipe.FrontOpen || rearOpen != pipe.RearOpen;
            pipe.FrontOpen = frontOpen;
            pipe.RearOpen = rearOpen;
            changed |= VentFromOutside(set, line, dt);

            var airs = kept.Airs;
            foreach (var air in airs)
                changed |= air.BeforeStep(pumpSpeedUp);
            if (!changed && line.ChangeRate < SettledRate)
            {
                // Standing still (parked, lapped, level): nothing to run until something changes.
                foreach (var air in airs)
                {
                    air.Rest();
                    air.Handled = true;
                }
                return;
            }
            line.Advance(dt);
            foreach (var air in airs)
            {
                air.AfterStep();
                air.Handled = true;
            }
            set.pipePressure = SetPressure(set, line);
        }

        /// <summary>
        /// A line whose air moves slower than this, Pa/s, has settled and
        /// sleeps until an input changes: a hundred-thousandth of a bar a
        /// second, far under the 0.3 bar a minute of a leak the cars ignore.
        /// </summary>
        private const double SettledRate = 1.0;

        /// <summary>
        /// The set's one stock pipe pressure, bar absolute, for code that
        /// reads it (the remote's brake readout): the pipe at the engine whose
        /// driver's valve is cut in, or at the first car when none is, as
        /// the stock fields hold it.
        /// </summary>
        private static float SetPressure(Brakeset set, BrakeLine line)
        {
            var pressure = line.Pipe[DrivingIndex(line)].Pressure;
            return CarAir.Bar(pressure);
        }

        /// <summary>The car of <paramref name="line"/> whose driver's valve is cut in, working the train, or the first when none is.</summary>
        private static int DrivingIndex(BrakeLine line)
        {
            for (var i = 0; i < line.Cars.Count; i++)
            {
                var car = line.Cars[i];
                if (car.Locomotive?.DriverValve?.CutIn == true)
                    return i;
            }
            return 0;
        }

        // Each set's stock pipe pressure as this mod last wrote it, watched for another mod venting it.
        private static readonly ConditionalWeakTable<Brakeset, OutsideVentWatch> watches = new ConditionalWeakTable<Brakeset, OutsideVentWatch>();
        private static bool outsideVentLogged;

 private static bool VentFromOutside(Brakeset set, BrakeLine line, float dt)
        {
            var watch = watches.GetOrCreateValue(set);
            var open = watch.Check(set.pipePressure, dt, Time.frameCount, watching: !Net.IsGuest);
            var at = open ? DrivingIndex(line) : -1;
            var changed = false;
            for (var i = 0; i < line.Cars.Count; i++)
            {
                var vent = i == at;
                if (line.Cars[i].OutsideVent != vent)
                {
                    line.Cars[i].OutsideVent = vent;
                    changed = true;
                }
            }
            if (watch.Fell && !outsideVentLogged)
            {
                outsideVentLogged = true;
                var engine = TrainCar.Resolve(set.cars[Mathf.Clamp(at, 0, set.cars.Count - 1)].gameObject);
                Main.Mod?.Logger.Log($"Another mod vented the brake pipe of {(engine != null ? engine.ID : "a train")}'s train "
                    + "(a vigilance device or a dead man); the model vents it at the engine working the train while it does (not logged again)");
            }
            return changed || open;
        }

        /// <summary>A stock setter put a value in <paramref name="set"/>'s pipe pressure, which the model takes: not a vent.</summary>
        public static void TookStockPipe(Brakeset set)
        {
            if (set != null && watches.TryGetValue(set, out var watch))
                watch.Took(set.pipePressure);
        }

        /// <summary>
        /// After the stock tick: each end hose's exhaust, for its sound and
        /// spray, from the model's ends, and the set's pipe pressure again,
        /// as the stock leak code lowers it when an end stands open. What's
        /// written is watched for another mod venting it before the next tick.
        /// </summary>
        public static void AfterStockTick(Brakeset set)
        {
            if (!lines.TryGetValue(set, out var line))
                return;
            set.pipePressure = SetPressure(set, line);
            watches.GetOrCreateValue(set).Wrote(set.pipePressure, Time.frameCount);
            // The stock flow is the pipe's fall, bar/s over 6; an open 25 mm end blows about 0.5 kg/s at first (inferred scale).
            set.GetEndmost(front: true).exhaustFlow = Mathf.Clamp01((float)line.Pipe.FrontVentFlow * 2f);
            set.GetEndmost(front: false).exhaustFlow = Mathf.Clamp01((float)line.Pipe.RearVentFlow * 2f);
        }

        /// <summary>Marks a set's cars as not run by the model this frame, so the stock brake runs them.</summary>
        public static void Unhandle(Brakeset set)
        {
            foreach (var system in set.cars)
                if (TryGet(system, out var air))
                    air.Handled = false;
        }

        /// <summary>Whether <paramref name="line"/> still runs exactly <paramref name="set"/>'s cars' air, in order.</summary>
        private static bool Matches(BrakeLine line, Brakeset set)
        {
            if (line.Cars.Count != set.cars.Count)
                return false;
            for (var i = 0; i < line.Cars.Count; i++)
                if (!TryGet(set.cars[i], out var air) || air.Brake != line.Cars[i])
                    return false;
            return true;
        }

        // A set's line and its cars' airs in the set's order, as last
        // matched: while no car's air anywhere has been dropped or taken on
        // since, and the set holds the same cars in the same order, they
        // still match, with no look in the table for any car.
        private sealed class SetAirs
        {
            public BrakeLine Line;
            public BrakeSystem[] Systems;
            public CarAir[] Airs;
            public int Generation;
            public int CarsChanged;
        }

        private static readonly ConditionalWeakTable<Brakeset, SetAirs> setAirs = new ConditionalWeakTable<Brakeset, SetAirs>();

        private static SetAirs AirsFor(Brakeset set)
        {
            if (setAirs.TryGetValue(set, out var kept) && kept.Generation == generation && kept.Systems.Length == set.cars.Count
                && lines.TryGetValue(set, out var still) && ReferenceEquals(still, kept.Line))
            {
                var same = true;
                for (var i = 0; same && i < kept.Systems.Length; i++)
                    same = ReferenceEquals(kept.Systems[i], set.cars[i]);
                // Some car's air has changed since, perhaps one of these: looked up again.
                if (same && kept.CarsChanged != carsChanged && Matches(kept.Line, set))
                    kept.CarsChanged = carsChanged;
                if (same && kept.CarsChanged == carsChanged)
                    return kept;
            }
            var line = LineFor(set);
            var airs = new CarAir[set.cars.Count];
            for (var i = 0; i < airs.Length; i++)
                TryGet(set.cars[i], out airs[i]);
            setAirs.Remove(set);
            kept = new SetAirs { Line = line, Systems = set.cars.ToArray(), Airs = airs, Generation = generation, CarsChanged = carsChanged };
            setAirs.Add(set, kept);
            return kept;
        }

        private static BrakeLine LineFor(Brakeset set)
        {
            if (lines.TryGetValue(set, out var line) && Matches(line, set))
                return line;
            lines.Remove(set);
            var brakes = new List<CarBrake>(set.cars.Count);
            var piped = new List<bool>(set.cars.Count);
            var previous = new HashSet<BrakeLine>();
            foreach (var system in set.cars)
            {
                if (!TryGet(system, out var air))
                {
                    Drop(system);
                    // Each car's own pipe readout, which a save restores per car; the set's is an average.
                    var pipe = system.brakePipePressure > 0.5f ? system.brakePipePressure : set.pipePressure;
                    air = CarAir.Create(system, pipe, generation);
                    SavedAir.Apply(air);
                    Net.ApplyPending(air);
                    cars.Add(system, air);
                    carsChanged++;
                }
                if (air.Line != null)
                    previous.Add(air.Line);
                brakes.Add(air.Brake);
                piped.Add(system.hasMainResConnections);
            }
            line = new BrakeLine(brakes, Geometry, piped, previous);
            foreach (var system in set.cars)
                if (TryGet(system, out var air))
                    air.Line = line;
            lines.Add(set, line);
            return line;
        }
    }
}
