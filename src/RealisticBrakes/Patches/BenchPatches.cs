using DV;
using HarmonyLib;
using UnityEngine;

namespace RealisticBrakes.Patches
{
 [HarmonyPatch(typeof(CommsRadioCarSpawner), "IsCarLiveryUnlocked")]
    [OptionalPatch]
    internal static class BenchSpawnerPatch
    {
        private static void Postfix(ref bool __result)
        {
            var key = Main.Settings?.DevBenchKey;
            if (key != null && key.keyCode != KeyCode.None)
                __result = true;
        }
    }
}
