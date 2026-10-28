using DV.Simulation.Brake;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using RealisticBrakes.Sim;

namespace RealisticBrakes.Patches
{
    /// <summary>Adds the model's own air to each car's save entry (<see cref="SavedAir"/>).</summary>
    [HarmonyPatch(typeof(CarsSaveManager), nameof(CarsSaveManager.GetCarSaveData))]
    internal static class CarSavePatch
    {
        private static void Postfix(TrainCar car, JObject __result)
        {
            var entry = SavedAir.Write(car != null ? car.brakeSystem : null);
            if (entry != null && __result != null)
                __result[SavedAir.Key] = entry;
        }
    }

    /// <summary>Holds a saved car's own air until the model builds the car's (<see cref="SavedAir"/>).</summary>
    [HarmonyPatch(typeof(CarsSaveManager), "InstantiateCarFromSavegame")]
    internal static class CarLoadPatch
    {
        private static void Postfix(JObject carData, TrainCar __result)
        {
            if (__result != null && carData != null && carData[SavedAir.Key] is JObject entry)
                SavedAir.Hold(__result.brakeSystem, entry);
        }
    }
}
