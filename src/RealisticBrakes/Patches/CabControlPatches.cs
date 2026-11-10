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
    /// <item>makes an E-T engine's independent the S-6 (<see cref="IndependentHandle"/>);</item>
    /// <item>gives the brake controls text by the cursor saying where they stand (<see cref="CabValveHover"/>);</item>
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
        private static void Postfix(InteriorControlsManager __instance) => Fit(__instance.Car, __instance);

        /// <summary>
        /// Fits <paramref name="car"/>'s brake handles, in <paramref name="cab"/> (null for an engine with no cab interior,
        /// driven from its outside controls, as the WE-4U is) and among its outside controls; and with a cab, its gauges.
        /// Each fitting leaves a handle already fitted as it is.
        /// </summary>
        internal static void Fit(TrainCar car, InteriorControlsManager cab)
        {
            // Every cab's handles: an engine with a cab at each end has a pair on each port (CabHandles).
            var independents = CabHandles.Of(car, cab, InteriorControlsManager.ControlType.IndBrake);
            var trainBrakes = CabHandles.Of(car, cab, InteriorControlsManager.ControlType.TrainBrake);
            Try("the bail-off click", () =>
            {
                foreach (var lever in independents)
                    IndependentBailClick.Attach(lever, car);
            });
            // Shown only while the model runs, so fitted either way.
            Try("brake valve text", () => CabValveHover.Attach(car, cab));
            if (!Main.Active)
                return;
            Try("the S-6 independent", () =>
            {
                foreach (var lever in independents)
                    IndependentHandle.Fit(lever, car);
            });
            // A handle widened without both of its value patches would read the new notches as stock values.
            Try("the train brake handle", () =>
            {
                if (Main.Settings.WidenTrainBrakeHandles
                    && !Main.FailedOptionalPatches.Contains(typeof(WidenedHandleNotchPatch)) && !Main.FailedOptionalPatches.Contains(typeof(WidenedHandleSetPatch)))
                    foreach (var handle in trainBrakes)
                        TrainBrakeHandle.Widen(handle, car);
            });
            if (cab == null)
                return;
            Try("the equalizing reservoir's hand", () =>
            {
                if (Main.Settings.EqualizingHand)
                    BrakeGauges.Fit(cab, car);
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
    /// fits its brake levers beside it (<see cref="CarLevers"/>); on an engine
    /// with no cab interior, fits its brake handles (<see cref="CabControlsPatch.Fit"/>).
    /// Not part of the brake itself: if this fails, a pull is always a short
    /// one, the car has no levers, and the handles stay stock.
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
            // An engine with no cab interior is driven from its outside controls (the WE-4U's desks), which no cab's
            // controls are set up from: its brake handles are fitted here. An engine with a cab is fitted as its cab sets up.
            if (__instance.loadedExternalInteractables != null && __instance.carLivery != null && __instance.carLivery.interiorPrefab == null
                && __instance.SimController != null && __instance.SimController.controlsOverrider != null)
                CabControlsPatch.Fit(__instance, null);
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
