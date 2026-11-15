using HarmonyLib;
using LocoSim.Implementations;
using RealisticBrakes.Sim;

namespace RealisticBrakes.Patches
{
    /// <summary>
    /// Holds a throttle the brakes have cut at idle (<see cref="TractionCuts"/>):
    /// whatever feeds it from outside is kept as what the driver asks, and the
    /// port takes 0. Only the ports of engines cut now are touched.
    /// </summary>
    [HarmonyPatch(typeof(Port), nameof(Port.ExternalValueUpdate))]
    [OptionalPatch]
    internal static class TractionThrottlePatch
    {
        private static void Prefix(Port __instance, ref float newValue) => TractionCuts.Feed(__instance, ref newValue);
    }
}
