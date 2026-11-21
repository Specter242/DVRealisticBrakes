using DV.CabControls;
using DV.Simulation.Brake;
using DV.VR;
using RealisticBrakes.Sim;
using UnityEngine;
using VRTK;

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
        private VRTK_InteractableObject vrControl;
        private TelegrabbableButton telegrab;
        private float pressedAt = float.NaN;
        private bool draining;

        public BrakeSystem System { get; set; }

        /// <summary>Whether <paramref name="air"/> has a car valve or locomotive distributor whose reservoirs a held bleed drains.</summary>
        private static bool CanDrain(CarAir air) => air != null && (air.Brake.Valve != null || air.Brake.Locomotive != null);

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
            var touch = control.GetComponent<SpeedZoneControlTouchBehaviour>();
            if (touch != null)
                touch.useOnUntouch = true;
            var handle = control.GetComponent<BleedHandle>();
            if (handle == null)
                handle = control.gameObject.AddComponent<BleedHandle>();
            handle.control = control;
            handle.vrControl = control.GetComponent<VRTK_InteractableObject>();
            handle.telegrab = control.GetComponent<TelegrabbableButton>();
            handle.System = system;
            var hover = control.GetComponent<HoverText>();
            if (hover == null)
                hover = control.gameObject.AddComponent<HoverText>();
            hover.Source = () =>
            {
                if (!Main.Active || !Main.Settings.ShowValveText || handle.System == null || !BrakeSets.TryGet(handle.System, out var air) || !CanDrain(air))
                    return null;
                return handle.draining || air.ReleaseHeld
                    ? "Bleed handle: draining reservoirs..."
                    : "Bleed handle: pull to release cylinder; hold 3 s to drain reservoirs";
            };
        }

        private void OnEnable()
        {
            if (control == null)
                return;
            if (vrControl == null)
                vrControl = control.GetComponent<VRTK_InteractableObject>();
            if (telegrab == null)
                telegrab = control.GetComponent<TelegrabbableButton>();
            var touch = control.GetComponent<SpeedZoneControlTouchBehaviour>();
            if (touch != null)
                touch.useOnUntouch = true;
        }

        private void Update()
        {
            if (control == null || System == null)
                return;
            if (!Main.Active)
            {
                pressedAt = float.NaN;
                Set(false);
                return;
            }
            var inVr = vrControl != null || telegrab != null || VRManager.IsVREnabled();
            // Pulled: on, in the button's hold mode, while held. In VR, a single touch poke can latch Value at 1
            // without holding the handle, so require an active grab, VRTK use hold, or active telegrab instead of Value alone.
            var telegrabHeld = telegrab != null && telegrab.enabled && control.Value >= 0.5f;
            var held = inVr
                ? control.IsGrabbed() || (vrControl != null && vrControl.IsUsing()) || telegrabHeld
                : (control.Value >= 0.5f || control.IsGrabbed());
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
            if (System == null || !BrakeSets.TryGet(System, out var air) || !CanDrain(air))
            {
                this.draining = false;
                return;
            }
            var started = draining && !this.draining;
            this.draining = draining;
            if (air.LocalReleaseHeld != draining)
            {
                air.LocalReleaseHeld = draining;
                if (started)
                    Notice.Show("Draining car's brake reservoirs...");
                // Everyone else's car drains the same (multiplayer).
                Net.Report(new NetAction(air.Car, NetActionKind.BleedHold, 0, (byte)(draining ? 1 : 0)));
            }
        }
    }
}
