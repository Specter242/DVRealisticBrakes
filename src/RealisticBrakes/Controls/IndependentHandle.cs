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
            if (!(control is LeverBase) || !IsS6(car) || control.GetComponent<IndependentHandle>() != null)
                return;
            var handle = control.gameObject.AddComponent<IndependentHandle>();
            handle.control = control;
            handle.car = car;
            var spec = control.GetComponent<Lever>();
            var built = control.GetComponent<HingeJoint>() != null;
            if (spec != null && spec.useSteppedJoint && spec.steppedValueUpdate && spec.useLimits && !built)
            {
                spec.notches = Notches;
                // Counted from release, which the joint finds at either end (SteppedJoint.AngleForNotch): let go in release
                // or quick application, the spring brings the handle back to running or slow application.
                spec.useInnerLimitSpring = true;
                spec.innerLimitMinNotch = 1;
                spec.innerLimitMaxNotch = Notches - 2;
                handle.sprungByJoint = true;
            }
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
            var s6 = Main.Active && IsS6(car) && Sim.BrakeSets.TryGet(car.brakeSystem, out _);
            if (!s6)
            {
                Spring(false);
                springHome = false;
                HoldRelease(false);
                return;
            }
            var value = control.Value;
            var atEnd = LocomotiveBrake.InRelease(value) || LocomotiveBrake.PositionOf(value) == IndependentPosition.QuickApplication;
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
            HoldRelease((hand || axis) && LocomotiveBrake.InRelease(value));
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
            if (!sprungByJoint || control == null || !Main.Active || !IsS6(car))
                return value;
            if (!Axis(control))
                axisNotch = -1;
            axisNotch = AxisNotches.Snap(value, Notches, axisNotch);
            return AxisNotches.Value(axisNotch, Notches);
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
            if (!sprungByJoint && Main.Active && LocomotiveBrake.PositionOf(lever.Value) == IndependentPosition.QuickApplication)
                lever.SetValue(SlowApplication);
        }
    }
}
