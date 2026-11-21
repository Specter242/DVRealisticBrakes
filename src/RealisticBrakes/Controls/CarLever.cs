using DV.CabControls;
using DV.Utils;
using RealisticBrakes.Core;
using RealisticBrakes.Sim;
using UnityEngine;

namespace RealisticBrakes.Controls
{
    /// <summary>One of a car's brake levers, joined to its valve both ways: a move sets the valve, and the valve's setting (a save, the automatic lever) moves it.</summary>
    internal sealed class CarLever : MonoBehaviour
    {
        public CarLeverKind Kind { get; set; }

        public TrainCar Car { get; set; }

        public int Notches { get; set; } = 2;

        private ControlImplBase control;

        // Whether the lever has been turned to its valve's setting since its joint was built.
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
            if (control == null || control.IsGrabbed() || !TryGetAir(out var air))
                return;
            var notch = Notch(air);
            if (notch < 0)
                return;
            // A lever turns to a value only once its joint is built, a frame after it's enabled (LeverBase.Initialize): set before
            // that, it kept the value but stood at its first notch. Its row is fitted anew each time the player comes back near the
            // car, so the levers looked reset (GenerousMediaPlayer, 2026-10-28: they "come to the initial position" a few meters away).
            if (!placed)
            {
                if (GetComponent<HingeJoint>() == null)
                    return;
                placed = true;
                control.SetValue(notch / (float)(Notches - 1));
            }
            else if (notch != NotchOf(control.Value))
                control.SetValue(notch / (float)(Notches - 1));
        }

        private void OnValueChanged(ValueChangedEventArgs e)
        {
            if (!TryGetAir(out var air))
                return;
            var notch = NotchOf(e.newValue);
            if (notch == Notch(air))
                return;
            Set(air, notch);
            air.Touch();
            // A hand's move, not the valve's setting followed: everyone else's car is set the same (multiplayer).
            Net.RememberLever(Car, Kind, notch);
            Net.Report(new NetAction(Car, NetActionKind.Lever, (byte)Kind, (byte)notch));
        }

        /// <summary>Sets <paramref name="air"/>'s valve as <paramref name="kind"/>'s lever at <paramref name="notch"/> would; its lever, if loaded, follows.</summary>
        internal static void SetLever(CarAir air, CarLeverKind kind, int notch)
        {
            if (air == null)
                return;
            var current = NotchOf(air, kind);
            if (notch < 0 || notch >= NotchesOf(kind) || current < 0 || current == notch)
                return;
            SetOf(air, kind, notch);
            air.Touch();
        }

        /// <summary>The notch <paramref name="kind"/>'s lever stands at on <paramref name="air"/>'s valve; -1 when the car has no such lever.</summary>
        internal static int LeverNotch(CarAir air, CarLeverKind kind) => air == null ? -1 : NotchOf(air, kind);

        private static int NotchesOf(CarLeverKind kind) => kind == CarLeverKind.Retainer ? 4 : 2;

        private int NotchOf(float value) => Mathf.Clamp(Mathf.RoundToInt(value * (Notches - 1)), 0, Notches - 1);

        private bool TryGetAir(out CarAir air)
        {
            air = null;
            return Main.Active && Car != null && Car.brakeSystem != null && BrakeSets.TryGet(Car.brakeSystem, out air);
        }

        // The valve's setting as this lever's notch; -1 when the car's valve isn't of this lever's family.
        private int Notch(CarAir air) => NotchOf(air, Kind);

        private static int NotchOf(CarAir air, CarLeverKind kind)
        {
            var uic = air.Brake.Distributor;
            var aar = air.Brake.ControlValve;
            switch (kind)
            {
                case CarLeverKind.BrakingMode:
                    return uic == null ? -1 : uic.Mode == BrakingMode.Goods ? 0 : 1;
                case CarLeverKind.EmptyLoaded:
                    return uic == null ? -1 : uic.Load == LoadSetting.Empty ? 0 : 1;
                case CarLeverKind.Retainer:
                    return aar == null ? -1 : RetainerNotch(aar.Retainer);
                default:
                    var valve = air.Brake.Valve;
                    return valve == null ? -1 : valve.Isolated ? 1 : 0;
            }
        }

        private void Set(CarAir air, int notch) => SetOf(air, Kind, notch);

        private static void SetOf(CarAir air, CarLeverKind kind, int notch)
        {
            var uic = air.Brake.Distributor;
            var aar = air.Brake.ControlValve;
            switch (kind)
            {
                case CarLeverKind.BrakingMode:
                    if (uic != null)
                        uic.Mode = notch == 0 ? BrakingMode.Goods : BrakingMode.Passenger;
                    break;
                case CarLeverKind.EmptyLoaded:
                    if (uic != null)
                        uic.Load = notch == 0 ? LoadSetting.Empty : LoadSetting.Loaded;
                    break;
                case CarLeverKind.Retainer:
                    if (aar != null)
                        aar.Retainer = Retainers[notch];
                    break;
                default:
                    if (air.Brake.Valve != null)
                        air.Brake.Valve.Isolated = notch == 1;
                    break;
            }
        }

        // The retaining valve's handle, in its order: exhaust, low pressure, high pressure, slow direct.
        private static readonly Retainer[] Retainers = { Retainer.Exhaust, Retainer.LowPressure, Retainer.HighPressure, Retainer.SlowDirect };

        private static int RetainerNotch(Retainer retainer) => System.Array.IndexOf(Retainers, retainer);

        /// <summary>What this lever is and how it's set, for the text by the cursor.</summary>
        public string Describe()
        {
            if (!TryGetAir(out var air))
                return null;
            var uic = air.Brake.Distributor;
            var aar = air.Brake.ControlValve;
            switch (Kind)
            {
                case CarLeverKind.BrakingMode:
                    return uic == null ? null : uic.Mode == BrakingMode.Goods
                        ? "Brake mode: G (goods: slow to apply and release)"
                        : "Brake mode: P (passenger: quick to apply and release)";
                case CarLeverKind.EmptyLoaded:
                    if (uic == null || air.Rigging == null)
                        return null;
                    var mass = Car.massController != null ? Car.massController.TotalMass / 1000f : 0f;
                    return $"Empty/loaded: {(uic.Load == LoadSetting.Empty ? "empty" : "loaded")} " +
                           $"(loaded above {air.Rigging.ChangeoverMass / 1000:F0} t; this car {mass:F0} t)";
                case CarLeverKind.Retainer:
                    if (aar == null)
                        return null;
                    switch (aar.Retainer)
                    {
                        case Retainer.HighPressure: return "Retaining valve: HP (holds 20 psi in the cylinder on a release)";
                        case Retainer.LowPressure: return "Retaining valve: LP (holds 10 psi in the cylinder on a release)";
                        case Retainer.SlowDirect: return "Retaining valve: SD (slow exhaust)";
                        default: return "Retaining valve: EX (normal exhaust)";
                    }
                default:
                    var valve = air.Brake.Valve;
                    if (valve == null)
                        return null;
                    var name = aar != null ? "Cut-out cock" : "Isolating cock";
                    return valve.Isolated ? $"{name}: closed (this car's brake cut out)" : $"{name}: open (brake in)";
            }
        }
    }

    /// <summary>Text by the cursor while the control is hovered, as the game shows for its own controls.</summary>
    internal sealed class HoverText : MonoBehaviour, IHoverReaction
    {
        private bool hovered;

        public System.Func<string> Source { get; set; }

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
