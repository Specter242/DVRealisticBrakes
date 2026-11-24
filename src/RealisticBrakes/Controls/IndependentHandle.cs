using DV.CabControls;
using HarmonyLib;
using RealisticBrakes.Core;
using UnityEngine;
using Lever = DV.CabControls.Spec.Lever;

namespace RealisticBrakes.Controls
{
 internal sealed class IndependentHandle : MonoBehaviour
    {
        /// <summary>The S-6's positions as notches: release, running, lap, slow application, quick application.</summary>
        public const int Notches = 5;

        // Whether this is the No. 254 (Russian equipment): I (sprung bail-off), II, and III to VI, sprung only out of I.
        private bool matrosov;

        // This handle's notches: the S-6's or the No. 254's.
        private int HandleNotches => matrosov ? LocomotiveBrakeSpec.Matrosov254Notches : Notches;

        /// <summary>Whether <paramref name="car"/>'s independent is the No. 254: Russian equipment, every engine with one.</summary>
        public static bool Is254(TrainCar car) =>
            car != null && car.brakeSystem != null && car.brakeSystem.hasIndependentBrake && Sim.CarAir.IsRussianEngine(car);

        // Whether the handle at value stands in its bail-off notch: the S-6's release, the No. 254's I.
        private bool InBailOff(float value) => matrosov ? LocomotiveBrakeSpec.Matrosov254InBailOff(value) : LocomotiveBrake.InRelease(value);

        // Whether this handle is still what it was fitted as: the S-6 or the No. 254.
        private bool StillFitted() => matrosov ? Is254(car) : IsS6(car);

        // Slow application on the game's scale, where quick application springs back to.
        private const float SlowApplication = 0.75f;

        private ControlImplBase control;
        private TrainCar car;
        private bool sprungByJoint;
        private SteppedJoint joint;
        private IndependentBailClick bail;

        // Let go by a hand here, and not yet home out of release or quick application: the spring takes it there. A handle
        // as the cab loads is taken home too, unless this game is a guest, whose handles are the host's.
        private bool springHome = !Sim.Net.IsGuest;

        // The notch a controller's axis holds the handle at; -1 before the axis takes it.
        private int axisNotch = -1;

        /// <summary>Whether this handle has the release notch: a stepped lever, given the S-6's five notches.</summary>
        public bool HasReleaseNotch => sprungByJoint;

        // The game's notched lever re-aims its spring only when its angle changes (SteppedJoint.Update, against this). A key,
        // the scroll wheel, or the mouse holding the handle in quick application (or release) turns the spring off; let go
        // once the handle had settled there, the spring came back on still aimed there, and the handle stayed (Torun,
        // 2026-10-30: "the spring kind of breaks"; Lonoxmont's handle "jam[med] into fast apply").
        private static readonly AccessTools.FieldRef<SteppedJoint, float> cachedAngle = AccessTools.FieldRefAccess<SteppedJoint, float>("cachedAngle");

        /// <summary>Whether <paramref name="car"/>'s independent is the S-6: an American engine with a manual-lap train brake (as <see cref="Sim.CarAir"/> fits the E-T).</summary>
        public static bool IsS6(TrainCar car)
        {
            var system = car != null ? car.brakeSystem : null;
            return system != null && Sim.CarAir.IsAmericanEngine(car) && system.hasTrainBrake && !system.selfLappingController
                && system.hasIndependentBrake;
        }

        /// <summary>
        /// Fits <paramref name="control"/>, <paramref name="car"/>'s
        /// independent, as the S-6, as the cab's controls are found and before
        /// the lever builds its joint a frame later.
        /// </summary>
        public static void Fit(ControlImplBase control, TrainCar car)
        {
            var matrosov = Is254(car);
            if (!(control is LeverBase) || !(matrosov || IsS6(car)) || control.GetComponent<IndependentHandle>() != null)
                return;
            var handle = control.gameObject.AddComponent<IndependentHandle>();
            handle.control = control;
            handle.car = car;
            handle.matrosov = matrosov;
            var spec = control.GetComponent<Lever>();
            var built = control.GetComponent<HingeJoint>() != null;
            if (spec != null && spec.useSteppedJoint && spec.steppedValueUpdate && spec.useLimits && !built)
            {
                Renotch(spec, matrosov);
                handle.sprungByJoint = true;
            }
        }

        /// <summary>
        /// Gives a stepped lever's <paramref name="spec"/>, before its joint is built, the S-6's notches and spring, or the
        /// No. 254's (<paramref name="matrosov"/>), one notch to a scroll step.
        /// </summary>
        internal static void Renotch(Lever spec, bool matrosov = false)
        {
            var notches = matrosov ? LocomotiveBrakeSpec.Matrosov254Notches : Notches;
            spec.notches = notches;
            // Counted from release, which the joint finds at either end (SteppedJoint.AngleForNotch): let go in release
            // or quick application, the spring brings the handle back to running or slow application. The No. 254 is
            // sprung only out of I, its bail-off; III to VI stay where they're put.
            spec.useInnerLimitSpring = true;
            spec.innerLimitMinNotch = 1;
            spec.innerLimitMaxNotch = matrosov ? notches - 1 : Notches - 2;
            // The game turns a notched lever by its notch angle times this a scroll step (LeverBase.ScrollWheelRotate), and a
            // lever made for its own notches may be set to step several: the 8F's 15-notch independent steps 2, which on
            // the S-6's 5 skipped a position (Torun, 2026-11-22: "running goes straight to slow apply, skipping lapping").
            // Its sign stays, as the game reads the lever's scroll direction from it (LeverNonVR).
            spec.scrollWheelHoverScroll = spec.scrollWheelHoverScroll < 0f ? -1f : 1f;
        }

        private void OnEnable()
        {
            if (control == null)
                control = GetComponent<ControlImplBase>();
            if (control != null)
                control.Ungrabbed += OnUngrabbed;
        }

        private void OnDisable()
        {
            if (control != null)
                control.Ungrabbed -= OnUngrabbed;
        }

        // Held in release, the handle bails off. Let go in release or quick application, a stepped handle's spring is made to
        // re-aim, at running or slow application, the next frame.
        private void LateUpdate()
        {
            if (control == null)
                return;
            if (!sprungByJoint || (joint == null && (joint = GetComponent<SteppedJoint>()) == null))
                return;
            // The spring out of release and quick application is the S-6's. With the model off, the engine put on
            // European equipment, or its train left to the game's brake, the handle is an ordinary independent again, and
            // sprung out of its bottom notch it rested a quarter applied until the cab was loaded afresh.
            var s6 = Main.Active && StillFitted() && Sim.BrakeSets.TryGet(car.brakeSystem, out _);
            if (!s6)
            {
                Spring(false);
                springHome = false;
                HoldRelease(false);
                return;
            }
            var value = control.Value;
            var atEnd = InBailOff(value) || (!matrosov && LocomotiveBrake.PositionOf(value) == IndependentPosition.QuickApplication);
            // Held by a hand (grabbed), or by a key or the scroll wheel (the spring off while they turn it); or by an axis.
            var hand = control.IsGrabbed() || !joint.isSpringActive;
            var axis = Axis(control);
            if (hand && !axis)
                springHome = true;
            else if (!hand && !atEnd)
                springHome = false;
            // The spring acts for a hand here and as that hand lets go, until the handle is home; never on a value set from
            // outside, nor under an axis, which holds the handle where it puts it.
            var spring = !axis && (hand || springHome);
            Spring(spring);
            if (spring && !hand && atEnd)
                cachedAngle(joint) = float.NaN;
            HoldRelease((hand || axis) && InBailOff(value));
        }

        // The S-6's spring out of release and quick application, on or off; the joint re-aims either way.
        private void Spring(bool on)
        {
            if (joint.useInnerLimitSpring == on)
                return;
            joint.useInnerLimitSpring = on;
            cachedAngle(joint) = float.NaN;
        }

        // Whether a controller's axis drives the handle: the game writes it every frame, until a hand takes the handle.
        private static bool Axis(ControlImplBase control) => control.LastSetValueSource == ControlImplBase.SetValueSource.Analog;

        /// <summary>
        /// Where a controller's axis at <paramref name="value"/> puts the S-6 handle: on a notch, moved only past the deadband
        /// (<see cref="AxisNotches"/>). The value as it is, where the handle isn't the S-6 now.
        /// </summary>
        public float FromAxis(float value)
        {
            if (!sprungByJoint || control == null || !Main.Active || !StillFitted())
                return value;
            if (!Axis(control))
                axisNotch = -1;
            axisNotch = AxisNotches.Snap(value, HandleNotches, axisNotch);
            return AxisNotches.Value(axisNotch, HandleNotches);
        }

        private void HoldRelease(bool held)
        {
            if (bail == null)
                bail = GetComponent<IndependentBailClick>();
            if (bail != null)
                bail.HoldRelease(held);
        }

        // Let go in quick application, a smooth handle goes back to slow application, as the S-6's return spring takes it.
        private void OnUngrabbed(ControlImplBase lever)
        {
            if (!sprungByJoint && !matrosov && Main.Active && LocomotiveBrake.PositionOf(lever.Value) == IndependentPosition.QuickApplication)
                lever.SetValue(SlowApplication);
        }
    }
}
