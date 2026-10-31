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
        private bool sprungByJoint;
        private SteppedJoint joint;
        private IndependentBailClick bail;

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
            return system != null && Main.Settings.Equipment == Equipment.American && system.hasTrainBrake && !system.selfLappingController
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
            var value = control.Value;
            // Held by a hand (grabbed), or by a key or the scroll wheel (the spring off while they turn it).
            var held = control.IsGrabbed() || !joint.isSpringActive;
            if (!held && (LocomotiveBrake.InRelease(value) || LocomotiveBrake.PositionOf(value) == IndependentPosition.QuickApplication))
                cachedAngle(joint) = float.NaN;
            HoldRelease(held && LocomotiveBrake.InRelease(value));
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
