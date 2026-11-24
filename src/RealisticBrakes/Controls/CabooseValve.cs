using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DV;
using DV.CabControls;
using HarmonyLib;
using RealisticBrakes.Core;
using RealisticBrakes.Sim;
using UnityEngine;
using Lever = DV.CabControls.Spec.Lever;
using Object = UnityEngine.Object;

namespace RealisticBrakes.Controls
{
 internal static class CabooseValve
    {
        /// <summary>The handle's notches over its travel, a twentieth each (as Caboose Brake Handle's handle on the same spindle).</summary>
        internal const int Notches = 21;

        /// <summary>The handle's turn from closed to wide open, degrees, counterclockwise seen from the cabin.</summary>
        internal const float Travel = 50f;

        /// <summary>The handle's lean when closed, degrees counterclockwise from upright, seen from the cabin.</summary>
        internal const float ClosedLean = 25f;

        private const string SourceLivery = "LocoDE2";
        private const string SourcePath = "RightCluster/BrakeIndependent";
        private const string SourceControl = "C_BrakeIndependent";
        private const string FrameName = "RealisticBrakes caboose valve";

        /// <summary>
        /// Where the valve's spindle is, by livery, in its interior's frame: the face its handle's back sits on, with the
        /// cabin toward +z. The stock caboose's, as Caboose Brake Handle 0.1.9 measured it (<c>CabooseBrake.Spindle</c>:
        /// the DE2 handle's back, at its model's z −0.002, on the valve's face at z −4.8066). A custom caboose has none
        /// until its spindle is added here.
        /// </summary>
        internal static readonly Dictionary<string, Vector3> Spindles = new Dictionary<string, Vector3>
        {
            ["CabooseRed"] = new Vector3(-0.885909f, 2.4713f, -4.804524f),
        };

        // Each car's valve, as last set by anyone this session: its notch, 0 closed. Not saved.
        private static readonly Dictionary<TrainCar, int> notches = new Dictionary<TrainCar, int>();

        /// <summary>Whether any car's valve is open, so the model's step need look no further.</summary>
        internal static bool AnyOpen => notches.Count > 0;

        /// <summary><paramref name="car"/>'s valve's notch: 0 closed, <see cref="Notches"/> − 1 wide open.</summary>
        internal static int NotchOf(TrainCar car) => car != null && notches.TryGetValue(car, out var notch) ? notch : 0;

        /// <summary><paramref name="car"/>'s valve's travel, 0 closed to 1 wide open, for the model.</summary>
        internal static double TravelOf(TrainCar car) => notches.Count == 0 ? 0 : NotchOf(car) / (double)(Notches - 1);

        /// <summary>The cars whose valves are open, for a joining player.</summary>
        internal static IEnumerable<(TrainCar Car, int Notch)> Open()
        {
            Prune();
            return notches.Select(p => (p.Key, p.Value)).ToList();
        }

        /// <summary>Sets <paramref name="car"/>'s valve to <paramref name="notch"/>, as a hand here or another player's did; its handle, if loaded, follows.</summary>
        internal static void Set(TrainCar car, int notch)
        {
            if (car == null)
                return;
            Prune();
            notch = Mathf.Clamp(notch, 0, Notches - 1);
            if (notch == 0)
                notches.Remove(car);
            else
                notches[car] = notch;
            if (car.brakeSystem != null && BrakeSets.TryGet(car.brakeSystem, out var air))
                air.Touch();
        }

        /// <summary>The game resets <paramref name="car"/> for reuse: its valve is closed again.</summary>
        internal static void Forget(TrainCar car)
        {
            if (car != null)
                notches.Remove(car);
        }

        // Cars the game has destroyed come off the list.
        private static void Prune()
        {
            foreach (var gone in notches.Keys.Where(c => c == null).ToList())
                notches.Remove(gone);
        }

        /// <summary>What the valve is on <paramref name="air"/>'s car, by its equipment.</summary>
        internal static string NameFor(CarAir air)
        {
            if (air?.Brake.ControlValve != null)
                return "Conductor's valve";
            if (air?.Brake.VacuumCylinder != null)
                return "Guard's valve";
            return "Emergency brake valve";
        }

        /// <summary>The text by the cursor on the handle.</summary>
        internal static string Describe(TrainCar car)
        {
            if (!Main.Active || car == null || car.brakeSystem == null || !BrakeSets.TryGet(car.brakeSystem, out var air))
                return null;
            var name = NameFor(air);
            var notch = NotchOf(car);
            if (notch == 0)
                return $"{name}: closed";
            var what = air.Brake.VacuumCylinder != null ? "letting air into the train pipe" : "venting the brake pipe";
            var share = notch * 100 / (Notches - 1);
            return notch == Notches - 1 ? $"{name}: wide open ({what})" : $"{name}: {share}% open ({what})";
        }

        /// <summary>Fits the valve's frame in <paramref name="car"/>'s just-loaded <paramref name="interior"/>, if its livery has a spindle.</summary>
        internal static void Fit(TrainCar car, Transform interior)
        {
            var livery = car != null && car.carLivery != null ? car.carLivery.id : null;
            if (interior == null || livery == null || !Spindles.TryGetValue(livery, out var spindle) || interior.Find(FrameName) != null)
                return;
            var frame = new GameObject(FrameName);
            frame.transform.SetParent(interior, false);
            frame.transform.localPosition = spindle;
            // The DE2 handle lies along its control's −x, its back on z 0: turned so it leans ClosedLean from upright,
            // counterclockwise seen from the cabin (toward +x), its front toward the cabin.
            frame.transform.localRotation = Quaternion.Euler(0f, 0f, -90f - ClosedLean);
            frame.AddComponent<CabooseValveFrame>().Car = car;
        }

        // Whether the first fitting this session has been logged.
        private static bool logged;

        // An inactive holder the handle is built under, so the game's control isn't made until it's set up.
        private static GameObject stage;

        /// <summary>A handle under <paramref name="frame"/>: the DE2's independent handle, copied without its bindings or its link to the DE2's air.</summary>
        internal static GameObject BuildHandle(Transform frame, TrainCar car)
        {
            if (!Globals.G.Types.TryGetLivery(SourceLivery, out var de2) || de2.interiorPrefab == null)
                return null;
            var source = de2.interiorPrefab.transform.Find(SourcePath);
            if (source == null || source.Find(SourceControl) == null)
                return null;
            if (stage == null)
            {
                stage = new GameObject("RealisticBrakes stage");
                stage.SetActive(false);
                Object.DontDestroyOnLoad(stage);
            }
            var holder = Object.Instantiate(source.gameObject, stage.transform, false);
            holder.name = "RealisticBrakes caboose valve handle";
            var control = holder.transform.Find(SourceControl);
            control.localPosition = Vector3.zero;
            control.localRotation = Quaternion.identity;
            var spec = control.GetComponent<Lever>();
            if (spec == null)
            {
                Object.Destroy(holder);
                return null;
            }
            // Only the lever stays: the DE2's keys, axis, scroll binding, and its feed to the DE2's independent go.
            var removed = new List<string>();
            foreach (var component in control.GetComponents<MonoBehaviour>().Where(c => !(c is DV.CabControls.Spec.ControlSpec)).ToList())
            {
                removed.Add(component.GetType().Name);
                Object.DestroyImmediate(component);
            }
            spec.jointAxis = Vector3.back;
            spec.invertDirection = false;
            spec.useLimits = true;
            spec.jointLimitMin = 0f;
            spec.jointLimitMax = Travel;
            spec.useSteppedJoint = true;
            spec.steppedValueUpdate = true;
            spec.notches = Notches;
            spec.useInnerLimitSpring = false;
            control.gameObject.AddComponent<CabooseValveHandle>().Car = car;
            control.gameObject.AddComponent<HoverText>().Source = () => Main.Settings.ShowValveText ? Describe(car) : null;
            holder.transform.SetParent(frame, false);
            holder.transform.localPosition = Vector3.zero;
            holder.transform.localRotation = Quaternion.identity;
            holder.transform.localScale = Vector3.one;
            if (!logged)
            {
                logged = true;
                Main.Mod?.Logger.Log($"Caboose valve fitted (the DE2 handle's {string.Join(", ", removed)} taken off)");
            }
            return holder;
        }

        // Caboose Brake Handle's per-car component and whether it has a handle on the spindle; null without that mod.
        private static bool otherLooked;
        private static Type otherType;
        private static PropertyInfo otherHasHandle;

        /// <summary>Whether Caboose Brake Handle (Nexus 1774) has its handle on <paramref name="car"/>'s spindle.</summary>
        internal static bool OtherHandleOn(TrainCar car)
        {
            if (!otherLooked)
            {
                otherLooked = true;
                otherType = AccessTools.TypeByName("DVCabooseBreak.CabooseBrake");
                otherHasHandle = otherType?.GetProperty("HasHandle", BindingFlags.Public | BindingFlags.Instance);
            }
            if (otherType == null || otherHasHandle == null || car == null)
                return false;
            var other = car.GetComponent(otherType);
            try
            {
                return other != null && otherHasHandle.GetValue(other) is bool has && has;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// The valve's place in a caboose's interior: it puts the handle there while the model runs and the spindle is free,
    /// and takes it off otherwise. A valve left open when its handle goes (another mod's handle fitted, the model turned
    /// off) is closed.
    /// </summary>
    internal sealed class CabooseValveFrame : MonoBehaviour
    {
        private const float Every = 0.5f;

        public TrainCar Car;

        private GameObject handle;
        private float next;
        private bool failed;

        private void Update()
        {
            if (Time.unscaledTime < next)
                return;
            next = Time.unscaledTime + Every;
            var wanted = Main.Active && !CabooseValve.OtherHandleOn(Car);
            if (wanted && handle == null && !failed)
            {
                handle = CabooseValve.BuildHandle(transform, Car);
                failed = handle == null;
                if (failed)
                    Main.Mod?.Logger.Warning("The caboose valve isn't fitted: the DE2's independent handle wasn't found to copy.");
            }
            else if (!wanted && handle != null)
            {
                Destroy(handle);
                handle = null;
                if (CabooseValve.NotchOf(Car) != 0 && !Net.IsGuest)
                {
                    CabooseValve.Set(Car, 0);
                    Net.Report(new NetAction(Car, NetActionKind.CabooseValve, 0, 0));
                }
            }
        }
    }

    /// <summary>The handle, joined to the car's valve both ways: a move sets the valve, and the valve's setting (another player's move) moves it.</summary>
    internal sealed class CabooseValveHandle : MonoBehaviour
    {
        public TrainCar Car;

        private ControlImplBase control;

        // Whether the handle has been turned to the valve's setting since its joint was built.
        private bool placed;

        private void Start()
        {
            control = GetComponent<ControlImplBase>();
            if (control != null)
                control.ValueChanged += OnValueChanged;
        }

        private void OnDestroy()
        {
            if (control != null)
                control.ValueChanged -= OnValueChanged;
        }

        private void Update()
        {
            if (control == null || control.IsGrabbed())
                return;
            var notch = CabooseValve.NotchOf(Car);
            // A lever turns to a value only once its joint is built, a frame after it's enabled (as CarLever).
            if (!placed)
            {
                if (GetComponent<HingeJoint>() == null)
                    return;
                placed = true;
                if (notch != 0)
                    control.SetValue(notch / (float)(CabooseValve.Notches - 1));
            }
            else if (notch != NotchOf(control.Value))
                control.SetValue(notch / (float)(CabooseValve.Notches - 1));
        }

        private static int NotchOf(float value) => Mathf.Clamp(Mathf.RoundToInt(value * (CabooseValve.Notches - 1)), 0, CabooseValve.Notches - 1);

        private void OnValueChanged(ValueChangedEventArgs e)
        {
            if (Car == null)
                return;
            // Not the value the lever starts at as its joint is built: only a hand's move, until it has been placed.
            if (!placed)
            {
                if (!control.IsGrabbed())
                    return;
                placed = true;
            }
            var notch = NotchOf(e.newValue);
            if (notch == CabooseValve.NotchOf(Car))
                return;
            CabooseValve.Set(Car, notch);
            Net.Report(new NetAction(Car, NetActionKind.CabooseValve, 0, (byte)notch));
        }
    }

    /// <summary>Fits the caboose valve's place as a car's interior loads.</summary>
    [HarmonyPatch(typeof(TrainCar), nameof(TrainCar.LoadInterior))]
    [OptionalPatch]
    internal static class CabooseValveInteriorPatch
    {
        private static void Postfix(TrainCar __instance)
        {
            try
            {
                var interior = __instance.loadedInterior;
                if (interior != null)
                    CabooseValve.Fit(__instance, interior.transform);
            }
            catch (Exception e)
            {
                Main.Mod?.Logger.Warning($"The caboose valve isn't fitted: {e.GetBaseException().GetType().Name}: {e.GetBaseException().Message}");
            }
        }
    }
}
