using System.Collections.Generic;
using System.Runtime.CompilerServices;
using DV.Simulation.Brake;
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
        public static readonly PipeGeometry Geometry = PipeGeometry.Uic();

        private static readonly ConditionalWeakTable<BrakeSystem, CarAir> cars = new ConditionalWeakTable<BrakeSystem, CarAir>();
        private static readonly ConditionalWeakTable<Brakeset, BrakeLine> lines = new ConditionalWeakTable<Brakeset, BrakeLine>();

        // Bumped when the model comes back on, so every car is read afresh from the stock brake that ran meanwhile.
        private static int generation;
        private static bool stale;

        /// <summary>The air of the car with <paramref name="system"/>, if the model has taken it on.</summary>
        public static bool TryGet(BrakeSystem system, out CarAir air) =>
            cars.TryGetValue(system, out air) && air.Generation == generation;

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

        /// <summary>The line running <paramref name="set"/>, if built.</summary>
        public static bool TryGetLine(Brakeset set, out BrakeLine line) => lines.TryGetValue(set, out line);

        /// <summary>
        /// Forgets a car's air as the game resets the car for reuse, giving its
        /// bogies back the game's force first (the game sets it only once, as
        /// the car is first made) and dropping any save entry held for it.
        /// </summary>
        public static void Forget(BrakeSystem system)
        {
            if (cars.TryGetValue(system, out var air))
                air.RestoreForce();
            cars.Remove(system);
            SavedAir.Discard(system);
            Net.Forget(system);
        }

        /// <summary>Whether the model ran <paramref name="system"/>'s car this frame.</summary>
        public static bool Handles(BrakeSystem system) => Main.Active && TryGet(system, out var air) && air.Handled;

        // A pipe end is open to the air with its cock open and no hose coupled to it.
        private static bool Open(HoseAndCock hose) => hose != null && hose.connectedTo == null && hose.cockOpen;

        /// <summary>Advances <paramref name="set"/>'s air <paramref name="dt"/> s and writes it back to the stock fields.</summary>
        public static void Tick(Brakeset set, float dt)
        {
            if (stale)
            {
                stale = false;
                generation++;
            }
            var line = LineFor(set);
            var pumpSpeedUp = set.firstCar.gameParams?.CompressorProductionModifier ?? 1f;
            var pipe = line.Pipe;
            var frontOpen = Open(set.GetEndmost(front: true));
            var rearOpen = Open(set.GetEndmost(front: false));
            var changed = frontOpen != pipe.FrontOpen || rearOpen != pipe.RearOpen;
            pipe.FrontOpen = frontOpen;
            pipe.RearOpen = rearOpen;

            airs.Clear();
            foreach (var system in set.cars)
            {
                TryGet(system, out var air);
                changed |= air.BeforeStep(pumpSpeedUp);
                airs.Add(air);
            }
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

        // Reused each tick, as the game ticks hundreds of sets a frame.
        private static readonly List<CarAir> airs = new List<CarAir>();

        /// <summary>
        /// The set's one stock pipe pressure, bar absolute, for code that
        /// reads it (the remote's brake readout): the pipe at the engine whose
        /// driver's valve is cut in, or at the first car when none is.
        /// </summary>
        private static float SetPressure(Brakeset set, BrakeLine line)
        {
            for (var i = 0; i < line.Cars.Count; i++)
            {
                var valve = line.Cars[i].Locomotive?.DriverValve;
                if (valve != null && valve.CutIn)
                    return CarAir.Bar(line.Pipe[i].Pressure);
            }
            return CarAir.Bar(line.Pipe[0].Pressure);
        }

        /// <summary>
        /// After the stock tick: each end hose's exhaust, for its sound and
        /// spray, from the model's ends, and the set's pipe pressure again,
        /// as the stock leak code lowers it when an end stands open.
        /// </summary>
        public static void AfterStockTick(Brakeset set)
        {
            if (!lines.TryGetValue(set, out var line))
                return;
            set.pipePressure = SetPressure(set, line);
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
                    cars.Remove(system);
                    // Each car's own pipe readout, which a save restores per car; the set's is an average.
                    var pipe = system.brakePipePressure > 0.5f ? system.brakePipePressure : set.pipePressure;
                    air = CarAir.Create(system, pipe, generation);
                    SavedAir.Apply(air);
                    Net.ApplyPending(air);
                    cars.Add(system, air);
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
