using HarmonyLib;
using LocoSim.Implementations;
using RealisticBrakes.Sim;

namespace RealisticBrakes.Patches
{
 [HarmonyPatch(typeof(SteamCompressor), nameof(SteamCompressor.Tick))]
    [OptionalPatch]
    internal static class EjectorSteamPatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(SteamCompressor __instance)
        {
            if (Main.Active && CarAir.TryGetEjectors(__instance, out var engine))
                __instance.steamConsumptionReadOut.Value = (float)engine.SteamFlow;
        }
    }
}
