using HarmonyLib;
using DV.HUD;
using RealisticBrakes.Controls;

namespace RealisticBrakes.Patches
{
 [HarmonyPatch(typeof(TrainCar), nameof(TrainCar.LoadInterior))]
    [OptionalPatch]
    internal static class VacuumInteriorPatch
    {
        private static void Postfix(TrainCar __instance)
        {
            try
            {
                var interior = __instance.loadedInterior;
                if (interior != null && interior.GetComponentInChildren<InteriorControlsManager>(true) == null)
                    VacuumGauges.Fit(interior.transform, __instance);
            }
            catch (System.Exception e)
            {
                Main.Mod?.Logger.Warning($"An interior's gauges stay stock: {e.GetBaseException().GetType().Name}: {e.GetBaseException().Message}");
            }
        }
    }
}
