using DV.Simulation.Brake;
using DV.ThingTypes;
using HarmonyLib;

namespace RealisticBrakes.Patches
{
 [HarmonyPatch(typeof(TrainCar), "Awake")]
    [OptionalPatch]
    internal static class TenderFollowsEnginePatch
    {
        private static void Postfix(TrainCar __instance)
        {
            var system = __instance.brakeSystem;
            if (system == null || system.hasCompressor || __instance.carLivery == null || !CarTypes.IsTender(__instance.carLivery)
                || system.brakeCylinderPressureCalculation != BrakeSystem.BrakeCylinderPressureCalculation.Regular)
                return;
            system.brakeCylinderPressureCalculation = BrakeSystem.BrakeCylinderPressureCalculation.CopyFront;
        }
    }
}
