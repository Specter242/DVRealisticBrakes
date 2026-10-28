using DV.CabControls;
using DV.HUD;
using DV.Simulation.Brake;
using HarmonyLib;
using RealisticBrakes.Controls;

namespace RealisticBrakes.Patches
{
    /// <summary>
    /// As each cab's controls are found:
    /// <list type="bullet">
    /// <item>watches the independent brake lever for a click to bail off (<see cref="IndependentBailClick"/>);</item>
    /// <item>widens the train brake handle (<see cref="TrainBrakeHandle"/>) before it builds its joint a frame later;</item>
    /// <item>adds the equalizing reservoir's hand to a steam engine's gauges (<see cref="BrakeGauges"/>).</item>
    /// </list>
    /// Not part of the brake itself: if these patches fail to apply, or one of
    /// these throws, the brake runs without it and the cab sets up as before.
    /// </summary>
    [HarmonyPatch(typeof(InteriorControlsManager), "SetBaseControlsReferences")]
    [OptionalPatch]
    internal static class CabControlsPatch
    {
        private static void Postfix(InteriorControlsManager __instance)
        {
            Try("the bail-off click", () =>
            {
                if (__instance.TryGetControl(InteriorControlsManager.ControlType.IndBrake, out var lever) && lever.controlImplBase != null)
                    IndependentBailClick.Attach(lever.controlImplBase, __instance.Car);
            });
            if (!Main.Active)
                return;
            // A handle widened without both of its value patches would read the new notches as stock values.
            Try("the train brake handle", () =>
            {
                if (Main.Settings.WidenTrainBrakeHandles
                    && !Main.FailedOptionalPatches.Contains(typeof(WidenedHandleNotchPatch)) && !Main.FailedOptionalPatches.Contains(typeof(WidenedHandleSetPatch))
                    && __instance.TryGetControl(InteriorControlsManager.ControlType.TrainBrake, out var handle) && handle.controlImplBase != null)
                    TrainBrakeHandle.Widen(handle.controlImplBase, __instance.Car);
            });
            Try("the equalizing reservoir's hand", () =>
            {
                if (Main.Settings.EqualizingHand)
                    BrakeGauges.Fit(__instance, __instance.Car);
            });
        }

        private static void Try(string what, System.Action fit)
        {
            try
            {
                fit();
            }
            catch (System.Exception e)
            {
                Main.Mod?.Logger.Warning($"A cab's {what} stays stock: {e.GetBaseException().GetType().Name}: {e.GetBaseException().Message}");
            }
        }
    }

    /// <summary>
    /// As a car's outside controls load near the player: times its bleed
    /// handle (<see cref="BleedHandle"/>), so a long pull drains the car, and
    /// fits its brake levers beside it (<see cref="CarLevers"/>). Not part of
    /// the brake itself: if this fails, a pull is always a short one and the
    /// car has no levers.
    /// </summary>
    [HarmonyPatch(typeof(TrainCar), nameof(TrainCar.LoadExternalInteractables))]
    [OptionalPatch]
    internal static class CarControlsPatch
    {
        private static void Postfix(TrainCar __instance)
        {
            try
            {
                var controls = __instance.loadedExternalInteractables;
                if (controls == null)
                    return;
                foreach (var feeder in controls.GetComponentsInChildren<BrakeCylinderReleaseButtonFeeder>(true))
                    BleedHandle.Attach(feeder.GetComponent<ControlImplBase>(), __instance.brakeSystem);
            }
            catch (System.Exception e)
            {
                Main.Mod?.Logger.Warning($"A car's bleed handle stays a short pull only: {e.GetBaseException().GetType().Name}: {e.GetBaseException().Message}");
            }
            try
            {
                if (Main.Active && __instance.loadedExternalInteractables != null)
                    CarLevers.Fit(__instance, __instance.loadedExternalInteractables.transform);
            }
            catch (System.Exception e)
            {
                Main.Mod?.Logger.Warning($"A car's brake levers weren't fitted: {e.GetBaseException().GetType().Name}: {e.GetBaseException().Message}");
            }
        }
    }

    /// <summary>A widened handle moved a notch: it reports the stock value, the two new notches reading as release and full service.</summary>
    [HarmonyPatch(typeof(LeverBase), "OnSteppedJointValueChanged")]
    [OptionalPatch]
    internal static class WidenedHandleNotchPatch
    {
        private static bool Prefix(LeverBase __instance, ValueChangedEventArgs e)
        {
            var handle = __instance.GetComponent<TrainBrakeHandle>();
            if (handle == null)
                return true;
            handle.OnNotch(__instance, e.newValue);
            return false;
        }
    }

    /// <summary>A stock value set on a widened handle from outside (a save, the remote, a cable, a driver mod) puts it among the stock notches.</summary>
    [HarmonyPatch(typeof(LeverBase), "AcceptSetValue")]
    [OptionalPatch]
    internal static class WidenedHandleSetPatch
    {
        private static void Prefix(LeverBase __instance, ref float newValue)
        {
            var handle = __instance.GetComponent<TrainBrakeHandle>();
            if (handle != null)
                newValue = handle.HandleValue(newValue);
        }
    }
}
