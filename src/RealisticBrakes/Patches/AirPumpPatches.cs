using HarmonyLib;
using LocoSim.Implementations;
using UnityEngine;

namespace RealisticBrakes.Patches
{
 [HarmonyPatch(typeof(SteamCompressor), nameof(SteamCompressor.Tick))]
    [OptionalPatch]
    internal static class AirPumpSteamPatch
    {
        /// <summary>
        /// Steam a bar·L of free air, kg: the 9½-in. single-stage pump's 60 lb
        /// of steam a 100 cu ft (ICS, The Westinghouse Air-Brake Handbook,
        /// 1918, p.59); the 8½-in. cross-compound took 19.65 lb.
        /// </summary>
        internal const float SteamPerAir = 60f * 0.45359237f / (100f * 28.316847f);

        // Before other mods' postfixes, so one setting its pump's steam (Realistic Firing's period pumps) has the last word.
        [HarmonyPriority(Priority.First)]
        private static void Postfix(SteamCompressor __instance)
        {
            if (Main.Active)
                Limit(__instance);
        }

        /// <summary>Lowers <paramref name="pump"/>'s steam to <see cref="SteamPerAir"/> times the free air it pumps now, at its own rate.</summary>
        internal static void Limit(SteamCompressor pump)
        {
            var most = Mathf.Max(0f, pump.productionRateReadOut.Value) * SteamPerAir;
            if (pump.steamConsumptionReadOut.Value > most)
                pump.steamConsumptionReadOut.Value = most;
        }
    }
}
