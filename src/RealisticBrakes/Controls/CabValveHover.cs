using System;
using DV.CabControls;
using DV.HUD;
using RealisticBrakes.Core;
using RealisticBrakes.Sim;

namespace RealisticBrakes.Controls
{
 internal static class CabValveHover
    {
        /// <summary>Adds the text to <paramref name="cab"/>'s brake controls, read from <paramref name="car"/>'s engine.</summary>
        public static void Attach(InteriorControlsManager cab, TrainCar car)
        {
            Add(cab, InteriorControlsManager.ControlType.TrainBrake, car,
                (air, engine) => CabValveText.TrainBrake(engine, air.System.selfLappingController));
            // The S-6 handle is fitted after this, so it's looked for as the text is drawn.
            var independent = cab.TryGetControl(InteriorControlsManager.ControlType.IndBrake, out var lever) ? lever.controlImplBase : null;
            Add(cab, InteriorControlsManager.ControlType.IndBrake, car,
                (air, engine) => air.System.hasIndependentBrake
                    ? CabValveText.Independent(engine, Main.Settings.ClickToBailOff, HasReleaseNotch(independent)) : null);
            Add(cab, InteriorControlsManager.ControlType.TrainBrakeCutout, car,
                (air, engine) => CabValveText.Cutout(engine.DriverValve));
        }

        private static bool HasReleaseNotch(ControlImplBase lever)
        {
            var handle = lever != null ? lever.GetComponent<IndependentHandle>() : null;
            return handle != null && handle.HasReleaseNotch;
        }

        private static void Add(InteriorControlsManager cab, InteriorControlsManager.ControlType type, TrainCar car, Func<CarAir, LocomotiveBrake, string> describe)
        {
            if (!cab.TryGetControl(type, out var reference) || reference.controlImplBase == null)
                return;
            var control = reference.controlImplBase.gameObject;
            var hover = control.GetComponent<HoverText>();
            if (hover == null)
                hover = control.AddComponent<HoverText>();
            hover.Source = () =>
            {
                if (!Main.Active || !Main.Settings.ShowValveText || car == null || car.brakeSystem == null || !BrakeSets.TryGet(car.brakeSystem, out var air) || air.Brake.Locomotive == null)
                    return null;
                return describe(air, air.Brake.Locomotive);
            };
        }
    }
}
