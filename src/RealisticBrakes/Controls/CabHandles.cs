using System.Collections.Generic;
using DV.CabControls;
using DV.HUD;
using DV.Simulation.Controllers;

namespace RealisticBrakes.Controls
{
 internal static class CabHandles
    {
        /// <summary>The handles on <paramref name="cab"/>'s engine of <paramref name="type"/>: the one the game keeps first.</summary>
        public static List<ControlImplBase> Of(InteriorControlsManager cab, InteriorControlsManager.ControlType type)
        {
            var found = new List<ControlImplBase>();
            if (cab == null)
                return found;
            if (cab.TryGetControl(type, out var reference) && reference.controlImplBase != null)
                found.Add(reference.controlImplBase);
            var car = cab.Car;
            var port = car != null && car.SimController != null ? PortOf(car.SimController.controlsOverrider, type) : null;
            if (string.IsNullOrEmpty(port))
                return found;
            Add(cab.GetComponent<InteractablePortFeedersController>(), port, found);
            if (car.AreExternalInteractablesLoaded)
                Add(car.loadedExternalInteractables.GetComponent<InteractablePortFeedersController>(), port, found);
            return found;
        }

        private static string PortOf(DV.Simulation.Cars.BaseControlsOverrider controls, InteriorControlsManager.ControlType type)
        {
            if (controls == null)
                return null;
            switch (type)
            {
                case InteriorControlsManager.ControlType.TrainBrake:
                    return controls.Brake?.portId;
                case InteriorControlsManager.ControlType.IndBrake:
                    return controls.IndependentBrake?.portId;
                case InteriorControlsManager.ControlType.TrainBrakeCutout:
                    return controls.BrakeCutout?.portId;
                default:
                    return null;
            }
        }

        // As the game finds them: a feeder on the port with a control on it.
        private static void Add(InteractablePortFeedersController controller, string port, List<ControlImplBase> found)
        {
            if (controller == null || controller.entries == null)
                return;
            foreach (var feeder in controller.entries)
            {
                if (feeder == null || feeder.portId != port)
                    continue;
                var control = feeder.GetComponent<ControlImplBase>();
                if (control != null && !found.Contains(control))
                    found.Add(control);
            }
        }
    }
}
