using DV.CabControls;
using DV.Simulation.Brake;
using RealisticBrakes.Sim;
using UnityEngine;

namespace RealisticBrakes.Controls
{
 internal sealed class BleedHandle : MonoBehaviour
    {
        /// <summary>
        /// How long, in s, a pull must be held to drain the reservoirs. An
        /// ABDX's release valve wants a pull of about 3 s; a UIC release
        /// device a short pull, and a long one to drain.
        /// </summary>
        public const float DrainAfter = 3f;

        private ControlImplBase control;
        private float pressedAt = float.NaN;

        public BrakeSystem System { get; set; }

        /// <summary>Times the bleed handle <paramref name="control"/> on <paramref name="system"/>'s car.</summary>
        public static void Attach(ControlImplBase control, BrakeSystem system)
        {
            if (control == null || system == null)
                return;
            // The game's hold mode: pulled while held, back in when let go (ButtonBase.IsHoldMode).
            var spec = control.GetComponent<DV.CabControls.Spec.Button>();
            if (spec != null)
            {
                spec.isToggle = true;
                spec.isTogglingBack = true;
            }
            var handle = control.GetComponent<BleedHandle>();
            if (handle == null)
                handle = control.gameObject.AddComponent<BleedHandle>();
            handle.control = control;
            handle.System = system;
        }

        private void Update()
        {
            if (control == null || System == null)
                return;
            // Pulled: on, in the button's hold mode, while held.
            var held = control.Value >= 0.5f || control.IsGrabbed();
            if (!held)
                pressedAt = float.NaN;
            else if (float.IsNaN(pressedAt))
                pressedAt = Time.time;
            Set(held && Time.time - pressedAt > DrainAfter);
        }

        private void OnDisable()
        {
            pressedAt = float.NaN;
            Set(false);
        }

        private void Set(bool draining)
        {
            if (System != null && BrakeSets.TryGet(System, out var air) && air.LocalReleaseHeld != draining)
            {
                air.LocalReleaseHeld = draining;
                // Everyone else's car drains the same (multiplayer).
                Net.Report(new NetAction(air.Car, NetActionKind.BleedHold, 0, (byte)(draining ? 1 : 0)));
            }
        }
    }
}
