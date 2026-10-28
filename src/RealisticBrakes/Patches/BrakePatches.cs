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
    /// If the model throws, the set falls back to the stock brake for the frame.
    /// </summary>
    [HarmonyPatch(typeof(Brakeset), "Tick")]
    internal static class BrakesetTickPatch
    {
        private static bool failed;

        private static void Prefix(Brakeset __instance, float dt)
        {
            if (!Main.Active)
            {
                BrakeSets.MarkStale(__instance);
                return;
            }
            Main.ReportOtherBrakeModsOnce();
            try
            {
                BrakeSets.Tick(__instance, dt);
            }
            catch (Exception e)
            {
                BrakeSets.Unhandle(__instance);
                if (!failed)
                {
                    failed = true;
                    Main.Mod.Logger.Error($"The brake model failed on a set of {__instance.cars.Count} cars; the stock brake runs it this frame (further failures not logged): {e}");
                }
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
            if (BrakeSets.TryGet(__instance, out var air) && __instance.brakeset != null && __instance.brakeset.cars.Count == 1)
            {
                air.Brake.PipeSection.Pressure = CarAir.Pa(value);
                air.Touch();
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(BrakeSystem.SetMainReservoirPressure))]
        private static void MainReservoirSet(BrakeSystem __instance, float value)
        {
            if (BrakeSets.TryGet(__instance, out var air) && air.Brake.Locomotive != null)
            {
                air.Brake.Locomotive.MainReservoir.Pressure = CarAir.Pa(value);
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
                valve.AuxiliaryReservoir.Pressure = CarAir.Pa(value);
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
            if (BrakeSets.TryGet(system, out var air) && air.Brake.Cylinder != null)
            {
                air.Brake.Cylinder.Pressure = CarAir.Pa(system.brakeCylinderPressureUnsmoothed);
                air.Touch();
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(BrakeSystem.ResetState))]
        private static void Reset(BrakeSystem __instance) => BrakeSets.Forget(__instance);
    }
}
