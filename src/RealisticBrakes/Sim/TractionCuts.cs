using System.Collections.Generic;
using System.Runtime.CompilerServices;
using LocoSim.Implementations;
using RealisticBrakes.Core;
using UnityEngine;

namespace RealisticBrakes.Sim
{
 internal static class TractionCuts
    {
        private sealed class Engine
        {
            public TractionCut Cut;
            public Port Throttle;
            // The throttle the driver asks for while the cut holds the port at idle, 0 to 1.
            public float Asked;
            // Another mod writes the throttle port directly: left alone.
            public bool Foreign;
            // The equipment setting it was made under.
            public int Generation;
            // When the model last updated it, unscaled s.
            public float UpdatedAt;
        }

        // Bumped as the equipment setting changes, so each engine's style is read again.
        private static int generation;

        private static readonly ConditionalWeakTable<TrainCar, Engine> engines = new ConditionalWeakTable<TrainCar, Engine>();

        // The throttle ports held at idle now, for the port's patch.
        private static readonly Dictionary<Port, Engine> heldPorts = new Dictionary<Port, Engine>();

        // Set while this writes a held port itself, so the patch lets it through.
        private static bool writing;

        /// <summary>Updates the cut of each engine in <paramref name="airs"/> after its set's tick.</summary>
        public static void Update(IReadOnlyList<CarAir> airs)
        {
            if (Net.IsGuest)
                return;
            for (var i = 0; i < airs.Count; i++)
            {
                var air = airs[i];
                if (air.Brake.Locomotive != null)
                    Update(air);
            }
        }

        private static void Update(CarAir air)
        {
            var car = air.Car;
            if (car == null)
                return;
            var engine = engines.GetValue(car, Make);
            if (engine.Generation != generation)
            {
                engines.Remove(car);
                engine = engines.GetValue(car, Make);
            }
            if (engine.Cut.Style == TractionCutStyle.None || engine.Throttle == null || engine.Foreign)
                return;
            engine.UpdatedAt = Time.unscaledTime;
            if (Main.Settings == null || !Main.Settings.TractionCut || !Main.Active)
            {
                if (engine.Cut.Cut)
                    Release(car, engine, notice: false);
                return;
            }
            // Something wrote the held port behind the patch: another mod drives this throttle, so the cut stands aside.
            if (engine.Cut.Cut && engine.Throttle.Value > TractionCut.ThrottleClosed)
            {
                engine.Foreign = true;
                Release(car, engine, notice: false);
                Main.Mod?.Logger.Log($"{car.ID}'s throttle is written by another mod, so its power isn't cut by the brakes");
                return;
            }
            var valve = air.Brake.Locomotive.DriverValve;
            var emergency = valve != null && valve.CutIn && valve.Position == DriverValvePosition.Emergency;
            var asked = engine.Cut.Cut ? engine.Asked : engine.Throttle.Value;
            if (!engine.Cut.Update(air.Brake.PipeSection.Pressure - Air.Atmosphere, emergency, asked))
                return;
            if (engine.Cut.Cut)
            {
                engine.Asked = 0f;
                heldPorts[engine.Throttle] = engine;
                writing = true;
                try
                {
                    engine.Throttle.ExternalValueUpdate(0f);
                }
                finally
                {
                    writing = false;
                }
                if (PlayerManager.Car == car)
                    Notice.Show(engine.Cut.Style == TractionCutStyle.PcSwitch
                        ? "Power cut: the PC switch opened on an emergency. Close the throttle once the brake pipe has recharged"
                        : "Power cut: the brake pipe is under 3.5 bar. Close the throttle once it has recharged");
            }
            else
                Release(car, engine, notice: true);
        }

        private static void Release(TrainCar car, Engine engine, bool notice)
        {
            engine.Cut.Clear();
            heldPorts.Remove(engine.Throttle);
            if (notice && PlayerManager.Car == car)
                Notice.Show("Power restored");
        }

        private static Engine Make(TrainCar car)
        {
            var throttle = FindThrottle(car);
            var style = throttle == null || CarAir.IsSteam(car) ? TractionCutStyle.None
                : CarAir.IsAmericanEngine(car) ? TractionCutStyle.PcSwitch : TractionCutStyle.BrakePipe;
            return new Engine { Cut = new TractionCut(style), Throttle = throttle, Generation = generation };
        }

        // The engine's throttle port: the one its throttle control feeds.
        private static Port FindThrottle(TrainCar car)
        {
            var sim = car.SimController;
            var control = sim != null && sim.controlsOverrider != null ? sim.controlsOverrider.Throttle : null;
            var flow = sim != null ? sim.SimulationFlow : null;
            if (control == null || flow == null || string.IsNullOrEmpty(control.portId))
                return null;
            return flow.TryGetPort(control.portId, out var port, true) ? port : null;
        }

        /// <summary>
        /// A value fed to <paramref name="port"/> from outside (the cab's lever,
        /// the keys, a multiple-unit cable, the remote): on a throttle the cut
        /// holds, it is kept as what the driver asks and the port stays at idle.
        /// With the model or the setting off, every cut is let go.
        /// </summary>
        public static void Feed(Port port, ref float value)
        {
            if (writing || heldPorts.Count == 0 || !heldPorts.TryGetValue(port, out var engine))
                return;
            if (!Main.Active || Main.Settings == null || !Main.Settings.TractionCut)
            {
                ReleaseAll();
                return;
            }
            // The model no longer runs this engine's brake: nothing to hold the throttle for.
            if (!engine.Cut.Holds(Time.unscaledTime - engine.UpdatedAt))
            {
                engine.Cut.Clear();
                heldPorts.Remove(port);
                return;
            }
            engine.Asked = value;
            value = 0f;
        }

        /// <summary>
        /// Lets the cuts of <paramref name="set"/>'s engines go, as the set is
        /// left to the game's own brake (another mod's driver, the M62, a
        /// failure): the model no longer watches its pipe.
        /// </summary>
        public static void ReleaseSet(DV.Simulation.Brake.Brakeset set)
        {
            if (heldPorts.Count == 0 || set == null)
                return;
            foreach (var system in set.cars)
            {
                var car = system != null ? TrainCar.Resolve(system.gameObject) : null;
                if (car != null && engines.TryGetValue(car, out var engine) && engine.Cut.Cut)
                    Release(car, engine, notice: false);
            }
        }

        /// <summary>Lets every cut go and reads each engine's style again, as the equipment setting changes.</summary>
        public static void Reset()
        {
            ReleaseAll();
            generation++;
        }

        /// <summary>Lets every cut go, as the model or the setting is turned off.</summary>
        public static void ReleaseAll()
        {
            foreach (var engine in heldPorts.Values)
                engine.Cut.Clear();
            heldPorts.Clear();
        }

        /// <summary>Whether <paramref name="car"/>'s power is cut by its brakes now.</summary>
        public static bool IsCut(TrainCar car) => car != null && engines.TryGetValue(car, out var engine) && engine.Cut.Cut;
    }
}
