using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DV.Simulation.Brake;
using HarmonyLib;
using RealisticBrakes.Sim;

namespace RealisticBrakes.Patches
{
    /// <summary>
    /// Runs a brake set's air under the model before the game's own tick of
    /// the set. The stock tick still runs: its leak and handbrake flags, the
    /// cut-out count, and each car's braking force, heat, angle cocks, and
    /// readouts. Only its air methods stand aside (<see cref="StockAirPatches"/>).
    /// If the model throws, the set goes back to the stock brake, with the
    /// game's brake force, for the frame; after a few such frames running, for
    /// as long as the set lasts (until its train is coupled or parted).
    /// </summary>
    [HarmonyPatch(typeof(Brakeset), "Tick")]
    internal static class BrakesetTickPatch
    {
        // How many frames running a set's model may fail before the set is left to the stock brake.
        private const int FailuresBeforeLeaving = 3;

        // The most different failures written to the log in full.
        private const int MostLogged = 8;

        private sealed class Failures
        {
            public int Running;
        }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Brakeset, Failures> failures =
            new System.Runtime.CompilerServices.ConditionalWeakTable<Brakeset, Failures>();
        private static readonly HashSet<string> logged = new HashSet<string>();
        private static bool anyFailed;
        private static bool warned;

        private static void Prefix(Brakeset __instance, float dt)
        {
            if (!Main.Active)
            {
                BrakeSets.MarkStale(__instance);
                return;
            }
            Main.ReportOtherBrakeModsOnce();
            Failures failing = null;
            if (anyFailed && failures.TryGetValue(__instance, out failing) && failing.Running >= FailuresBeforeLeaving)
                return;
            try
            {
                if (BrakeSets.AiDriven(__instance) || BrakeSets.RunByM62(__instance))
                {
                    BrakeSets.LeaveToStock(__instance);
                    return;
                }
                BrakeSets.Tick(__instance, dt);
                if (failing != null)
                    failing.Running = 0;
            }
            catch (Exception e)
            {
                Failed(__instance, e);
            }
        }

        // The stock brake takes the set from the pressures the model last wrote, with the game's own brake force: left with
        // the model's, a loaded car braked at a fraction of what the stock brake expects.
        private static void Failed(Brakeset set, Exception e)
        {
            anyFailed = true;
            var failing = failures.GetOrCreateValue(set);
            failing.Running++;
            try
            {
                BrakeSets.LeaveToStock(set);
            }
            catch (Exception)
            {
                BrakeSets.Unhandle(set);
            }
            var left = failing.Running >= FailuresBeforeLeaving;
            var kind = e.GetType().FullName + ": " + e.Message;
            if (logged.Count < MostLogged && logged.Add(kind))
                Main.Mod.Logger.Error($"The brake model failed on a set of {set.cars.Count} cars; the stock brake runs it this frame: {e}");
            if (left)
                Main.Mod.Logger.Error($"The brake model failed {failing.Running} frames running on a set of {set.cars.Count} cars ({kind}); the stock brake runs it until its train changes.");
            if (left && !warned)
            {
                warned = true;
                Notice.Warn("Realistic Brakes failed on a train, which the game's own brake now runs. Please report it with the log.");
            }
        }

        private static void Postfix(Brakeset __instance)
        {
            if (Main.Active && __instance.cars.Count > 0 && BrakeSets.Handles(__instance.firstCar))
                BrakeSets.AfterStockTick(__instance);
        }
    }

    /// <summary>
    /// The stock air methods stand aside for a car the model ran this frame:
    /// the compressor, the driver's valve, the engine's and the car's control
    /// valves, and the stock leaks. Postfixes other mods put on them still run
    /// (Leak Down's main reservoir leak, on <c>SimulateLeakage</c>, reaches the
    /// model through <see cref="BrakeSystem.SetMainReservoirPressure"/>).
    /// </summary>
    [HarmonyPatch]
    internal static class StockAirPatches
    {
        internal static readonly string[] Methods =
        {
            "SimulateCompressor", "SimulateTrainBrake", "SimulateLocoControlValve", "SimulateCarControlValve", "SimulateLeakage",
        };

        private static IEnumerable<MethodBase> TargetMethods() =>
            Methods.Select(name => (MethodBase)AccessTools.DeclaredMethod(typeof(BrakeSystem), name));

        private static bool Prefix(BrakeSystem __instance) => !BrakeSets.Handles(__instance);
    }

    /// <summary>Engines share main reservoir air through the model's hoses, not the stock instant balance.</summary>
    [HarmonyPatch(typeof(Brakeset), "BalanceMainReservoirs")]
    internal static class BalancePatch
    {
        private static bool Prefix(Brakeset __instance) => __instance.cars.Count == 0 || !BrakeSets.Handles(__instance.firstCar);
    }

    /// <summary>
    /// Each car's readouts: the audio's flows from the model (the stock tick
    /// zeroes them first), and the pipe gauge from the car's own section of
    /// pipe rather than the set's one stock pressure.
    /// </summary>
    [HarmonyPatch(typeof(BrakeSystem), "UpdateReadouts")]
    internal static class ReadoutsPatch
    {
        private static void Prefix(BrakeSystem __instance)
        {
            if (!BrakeSets.Handles(__instance) || !BrakeSets.TryGet(__instance, out var air))
                return;
            __instance.pipeMassFlow = air.PipeFlow;
            __instance.exhaustMassFlow = air.ExhaustFlow;
        }

        private static void Postfix(BrakeSystem __instance)
        {
            if (BrakeSets.Handles(__instance) && BrakeSets.TryGet(__instance, out var air))
                __instance.brakePipePressure = air.PipeBar;
        }
    }

 [HarmonyPatch(typeof(BrakeSystem), "SimulateBrakingForce")]
    internal static class BrakeHeatPatch
    {
        // The car whose braking force the game is working out, while it does: its heat step is called from there.
        [ThreadStatic] internal static BrakeSystem Current;

        private static void Prefix(BrakeSystem __instance) => Current = __instance;

        private static void Finalizer() => Current = null;
    }

    /// <summary>The heat step of <see cref="BrakeHeatPatch"/>'s car reads its braking factor scaled to its force.</summary>
    [HarmonyPatch(typeof(BrakeSystem.HeatController), nameof(BrakeSystem.HeatController.UpdateTemperature))]
    internal static class BrakeHeatStepPatch
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix(BrakeSystem.HeatController __instance, ref float brakingFactor)
        {
            var system = BrakeHeatPatch.Current;
            if (system == null || system.heatController != __instance || !BrakeSets.Handles(system) || !BrakeSets.TryGet(system, out var air))
                return;
            brakingFactor *= air.HeatShare;
        }
    }

    /// <summary>
    /// Values the game or another mod sets directly (a save restored, the
    /// startup helper, Leak Down's main reservoir leak, the console) go into
    /// the model too, so it doesn't overwrite them on the next frame.
    /// </summary>
    [HarmonyPatch(typeof(BrakeSystem))]
    internal static class SetterPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(BrakeSystem.SetBrakePipePressure))]
        private static void PipeSet(BrakeSystem __instance, float value)
        {
            // The setter writes a lone car's set's pipe pressure: a value the model takes, not another mod venting it.
            if (__instance.brakeset != null && __instance.brakeset.cars.Count == 1)
                BrakeSets.TookStockPipe(__instance.brakeset);
            if (BrakeSets.TryGet(__instance, out var air) && __instance.brakeset != null && __instance.brakeset.cars.Count == 1)
            {
                air.Brake.PipeSection.Pressure = FromStock(air, CarAir.Pa(value));
                air.Touch();
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(BrakeSystem.SetMainReservoirPressure))]
        private static void MainReservoirSet(BrakeSystem __instance, float value)
        {
            var reservoir = BrakeSets.TryGet(__instance, out var air) ? air.Brake.Locomotive?.MainReservoir : null;
            if (reservoir != null)
            {
                reservoir.Pressure = CarAir.Pa(value);
                air.Touch();
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(BrakeSystem.SetAuxReservoirPressure))]
        private static void AuxiliarySet(BrakeSystem __instance, float value)
        {
            if (!BrakeSets.TryGet(__instance, out var air))
                return;
            var valve = air.Brake.Valve ?? air.Brake.Locomotive?.Distributor;
            if (valve != null)
                valve.AuxiliaryReservoir.Pressure = FromStock(air, CarAir.Pa(value));
            air.Touch();
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(BrakeSystem.SetControlReservoirPressure))]
        private static void ControlSet(BrakeSystem __instance, float value)
        {
            if (!BrakeSets.TryGet(__instance, out var air))
                return;
            var valve = air.Brake.Distributor ?? air.Brake.Locomotive?.Distributor;
            if (valve != null)
                valve.ControlReservoir.Pressure = CarAir.Pa(value);
            air.Touch();
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(BrakeSystem.ForceCylinderPressure))]
        private static void CylinderForced(BrakeSystem __instance) => CylinderSet(__instance);

        [HarmonyPostfix]
        [HarmonyPatch(nameof(BrakeSystem.ClearCylinderPressure))]
        private static void CylinderCleared(BrakeSystem __instance) => CylinderSet(__instance);

        private static void CylinderSet(BrakeSystem system)
        {
            if (!BrakeSets.TryGet(system, out var air) || air.Brake.Cylinder == null)
                return;
            air.Brake.Cylinder.Pressure = CarAir.Pa(system.brakeCylinderPressureUnsmoothed);
            air.Touch();
        }

        // A value set in a stock field.
        private static double FromStock(CarAir air, double pascals) => pascals;

        [HarmonyPostfix]
        [HarmonyPatch(nameof(BrakeSystem.ResetState))]
        private static void Reset(BrakeSystem __instance) => BrakeSets.Forget(__instance);
    }
}
