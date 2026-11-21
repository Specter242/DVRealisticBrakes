using System;
using DV.CabControls;
using DV.HUD;
using DV.Utils;
using RealisticBrakes.Core;
using RealisticBrakes.Sim;
using UnityEngine;

namespace RealisticBrakes.Controls
{
 internal static class VacuumHover
    {
        /// <summary>Adds the text to <paramref name="car"/>'s controls in <paramref name="cab"/> (null: only its outside ones). Fitting one again changes nothing.</summary>
        public static void Attach(TrainCar car, InteriorControlsManager cab)
        {
            foreach (var control in CabHandles.Of(car, cab, InteriorControlsManager.ControlType.TrainBrake))
                Add(control, car, (air, engine) => VacuumCabText.TrainBrake(engine, air.Brake.PipeSection.Pressure, AnotherValveCutIn(air)));
            foreach (var control in CabHandles.Of(car, cab, InteriorControlsManager.ControlType.IndBrake))
                Add(control, car, (air, engine) => air.System.hasIndependentBrake ? VacuumCabText.Independent(engine, Main.Settings.ClickToBailOff) : null);
            foreach (var control in CabHandles.Of(car, cab, InteriorControlsManager.ControlType.TrainBrakeCutout))
                Add(control, car, (air, engine) => VacuumCabText.Cutout(engine));
            foreach (var control in CabHandles.Of(car, cab, InteriorControlsManager.ControlType.AirPump))
                Add(control, car, (air, engine) => VacuumCabText.EjectorValve(engine));
        }

        private static bool AnotherValveCutIn(CarAir air)
        {
            var set = air.System?.brakeset;
            var line = set != null && BrakeSets.TryGetLine(set, out var current) ? current : air.Line;
            if (line == null)
                return false;
            foreach (var other in line.Cars)
                if (!ReferenceEquals(other, air.Brake) && other.VacuumEngine?.CutIn == true)
                    return true;
            return false;
        }

        private static void Add(ControlImplBase reference, TrainCar car, Func<CarAir, VacuumEngine, string> describe)
        {
            if (reference == null)
                return;
            var control = reference.gameObject;
            var hover = control.GetComponent<VacuumHoverText>();
            if (hover == null)
                hover = control.AddComponent<VacuumHoverText>();
            hover.Source = () =>
            {
                if (!Main.Active || !Main.Settings.ShowValveText || car == null || car.brakeSystem == null || !BrakeSets.TryGet(car.brakeSystem, out var air) || air.Brake.VacuumEngine == null)
                    return null;
                return describe(air, air.Brake.VacuumEngine);
            };
        }
    }

 internal sealed class VacuumHoverText : MonoBehaviour, IHoverReaction
    {
        private bool hovered;

        public Func<string> Source { get; set; }

        public void OnHovered() => hovered = true;

        public void OnUnhovered() => hovered = false;

        private void Update()
        {
            if (!hovered || Source == null)
                return;
            var text = Source();
            var cursorText = SingletonBehaviour<InteractionTextControllerNonVr>.Instance;
            if (!string.IsNullOrEmpty(text) && cursorText != null)
                cursorText.DisplayText(text);
        }
    }
}
