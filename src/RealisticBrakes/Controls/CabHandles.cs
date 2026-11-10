using System;
using System.Collections.Generic;
using System.Reflection;
using DV.CabControls;
using DV.HUD;
using DV.KeyboardInput;
using DV.Simulation.Cars;
using DV.Simulation.Controllers;
using RealisticBrakes.Core;
using UnityEngine;

namespace RealisticBrakes.Controls
{
 internal static class CabHandles
    {
        /// <summary>The handles on <paramref name="cab"/>'s engine of <paramref name="type"/>: the one the game keeps first.</summary>
        public static List<ControlImplBase> Of(InteriorControlsManager cab, InteriorControlsManager.ControlType type) =>
            Of(cab != null ? cab.Car : null, cab, type);

        /// <summary>
        /// The handles on <paramref name="car"/>, an engine, of <paramref name="type"/>: the one <paramref name="cab"/>
        /// keeps first, if its cab has loaded, then the rest in the cab and among the outside controls.
        /// </summary>
        public static List<ControlImplBase> Of(TrainCar car, InteriorControlsManager cab, InteriorControlsManager.ControlType type)
        {
            var found = new List<ControlImplBase>();
            if (cab != null && cab.TryGetControl(type, out var reference) && reference.controlImplBase != null)
                found.Add(reference.controlImplBase);
            if (car == null || car.SimController == null)
                return found;
            var controls = car.SimController.controlsOverrider;
            var port = PortOf(controls, type);
            if (string.IsNullOrEmpty(port))
                return found;
            var controllers = new List<InteractablePortFeedersController>();
            if (cab != null)
                controllers.Add(cab.GetComponent<InteractablePortFeedersController>());
            if (car.AreExternalInteractablesLoaded && car.loadedExternalInteractables != null)
                controllers.Add(car.loadedExternalInteractables.GetComponent<InteractablePortFeedersController>());
            foreach (var controller in controllers)
                Add(controller, port, found);
            if (found.Count == 0 && AllLoaded(car))
                AddKeyed(controllers, controls, KeysOf(type), found);
            return found;
        }

        private static string PortOf(BaseControlsOverrider controls, InteriorControlsManager.ControlType type)
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

        private static BrakeKeyControl KeysOf(InteriorControlsManager.ControlType type)
        {
            switch (type)
            {
                case InteriorControlsManager.ControlType.TrainBrake:
                    return BrakeKeyControl.TrainBrake;
                case InteriorControlsManager.ControlType.IndBrake:
                    return BrakeKeyControl.Independent;
                case InteriorControlsManager.ControlType.TrainBrakeCutout:
                    return BrakeKeyControl.BrakeCutout;
                default:
                    return BrakeKeyControl.None;
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

        /// <summary>Whether every part of <paramref name="car"/> that carries controls has loaded: a port none of them feeds has no handle.</summary>
        private static bool AllLoaded(TrainCar car)
        {
            var livery = car.carLivery;
            if (livery == null)
                return false;
            var interior = livery.interiorPrefab == null || car.loadedInterior != null;
            var outside = livery.externalInteractablesPrefab == null || (car.AreExternalInteractablesLoaded && car.loadedExternalInteractables != null);
            return interior && outside;
        }

        // The handles the game's keys for the control move, other than those on another of the game's own controls' ports (a
        // maker's handle given the wrong keys stays what its port makes it).
        private static void AddKeyed(List<InteractablePortFeedersController> controllers, BaseControlsOverrider controls, BrakeKeyControl wanted,
            List<ControlImplBase> found)
        {
            if (wanted == BrakeKeyControl.None)
                return;
            var games = GamePorts(controls);
            foreach (var controller in controllers)
            {
                if (controller == null || controller.entries == null)
                    continue;
                foreach (var feeder in controller.entries)
                {
                    if (feeder == null || string.IsNullOrEmpty(feeder.portId) || games.Contains(feeder.portId))
                        continue;
                    var control = feeder.GetComponent<ControlImplBase>();
                    if (control != null && !found.Contains(control) && MovedBy(control, wanted))
                        found.Add(control);
                }
            }
        }

        private static HashSet<string> GamePorts(BaseControlsOverrider controls)
        {
            var ports = new HashSet<string>();
            foreach (InteriorControlsManager.ControlType type in Enum.GetValues(typeof(InteriorControlsManager.ControlType)))
            {
                var control = controls.GetControl(type);
                if (control != null && !string.IsNullOrEmpty(control.portId))
                    ports.Add(control.portId);
            }
            return ports;
        }

        /// <summary>Whether a key action on <paramref name="control"/> is one of <paramref name="wanted"/>'s.</summary>
        private static bool MovedBy(ControlImplBase control, BrakeKeyControl wanted)
        {
            foreach (var input in control.GetComponents<AKeyboardInput>())
                if (input != null)
                    foreach (var field in ActionFields(input.GetType()))
                    {
                        var value = field.GetValue(input);
                        if (value is AKeyboardInput.ActionReference one)
                        {
                            if (BrakeKeyActions.Of(one.name) == wanted)
                                return true;
                        }
                        else if (value is IEnumerable<AKeyboardInput.ActionReference> many)
                        {
                            foreach (var action in many)
                                if (action != null && BrakeKeyActions.Of(action.name) == wanted)
                                    return true;
                        }
                    }
            return false;
        }

        // Each key input's actions, found once per type: the scroll's, the joystick's, a toggle's, whatever it names them.
        private static readonly Dictionary<Type, FieldInfo[]> actionFields = new Dictionary<Type, FieldInfo[]>();

        /// <summary>The fields of a key input of <paramref name="type"/> holding its actions.</summary>
        internal static FieldInfo[] ActionFields(Type type)
        {
            if (actionFields.TryGetValue(type, out var fields))
                return fields;
            var found = new List<FieldInfo>();
            for (var t = type; t != null && t != typeof(MonoBehaviour); t = t.BaseType)
                foreach (var field in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    if (field.FieldType == typeof(AKeyboardInput.ActionReference)
                        || typeof(IEnumerable<AKeyboardInput.ActionReference>).IsAssignableFrom(field.FieldType))
                        found.Add(field);
            fields = found.ToArray();
            actionFields[type] = fields;
            return fields;
        }
    }
}
