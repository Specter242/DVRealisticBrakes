using DV.CabControls;
using RealisticBrakes.Core;
using UnityEngine;
using Lever = DV.CabControls.Spec.Lever;

namespace RealisticBrakes.Controls
{
 internal sealed class IndependentHandle : MonoBehaviour
    {
        /// <summary>The S-6's positions as notches: running, lap, slow application, quick application.</summary>
        public const int Notches = 4;

        // Slow application on the game's scale, where quick application springs back to.
        private const float SlowApplication = 2f / 3f;

        private ControlImplBase control;
        private bool sprungByJoint;

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
                // Counted from running, which the joint finds at either end (SteppedJoint.AngleForNotch): let go past slow
                // application, the spring brings the handle back to it.
                spec.useInnerLimitSpring = true;
                spec.innerLimitMinNotch = 0;
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

        // Let go in quick application, a smooth handle goes back to slow application, as the S-6's return spring takes it.
        private void OnUngrabbed(ControlImplBase lever)
        {
            if (!sprungByJoint && Main.Active && LocomotiveBrake.PositionOf(lever.Value) == IndependentPosition.QuickApplication)
                lever.SetValue(SlowApplication);
        }
    }
}
