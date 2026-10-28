using System.Collections.Generic;
using DV.CabControls;
using RealisticBrakes.Sim;
using UnityEngine;
using VRTK;

namespace RealisticBrakes.Controls
{
 internal sealed class IndependentBailClick : MonoBehaviour
    {
        // A grab let go within this, in s, having moved the lever less than this share of its travel, is a click.
        private const float ClickTime = 0.35f;
        private const float ClickTravel = 0.02f;

        // Less than this bailed, gauge Pa (0.05 bar), is nothing bailed.
        private const double MinNoticed = 5000;

        private ControlImplBase control;
        private VRTK_InteractableObject vrLever;
        private float grabbedAt = float.NegativeInfinity;
        private float valueAtGrab;

        public TrainCar Car { get; set; }

        /// <summary>Watches <paramref name="lever"/>, the independent brake of <paramref name="car"/>, for a click.</summary>
        public static void Attach(ControlImplBase lever, TrainCar car)
        {
            if (lever == null || car == null)
                return;
            var click = lever.GetComponent<IndependentBailClick>();
            if (click == null)
                click = lever.gameObject.AddComponent<IndependentBailClick>();
            click.Car = car;
        }

        private void OnEnable()
        {
            control = GetComponent<ControlImplBase>();
            if (control != null)
            {
                control.Grabbed += OnGrabbed;
                control.Ungrabbed += OnUngrabbed;
            }
            // The VR lever's interactable, added in its Awake; none on a desktop lever.
            vrLever = GetComponent<VRTK_InteractableObject>();
            if (vrLever != null)
            {
                vrLever.isUsable = true;
                vrLever.useOnlyIfGrabbed = true;
                vrLever.InteractableObjectUsed += OnUsed;
            }
        }

        private void OnDisable()
        {
            if (control != null)
            {
                control.Grabbed -= OnGrabbed;
                control.Ungrabbed -= OnUngrabbed;
            }
            if (vrLever != null)
                vrLever.InteractableObjectUsed -= OnUsed;
        }

        private void OnGrabbed(ControlImplBase lever)
        {
            grabbedAt = Time.time;
            valueAtGrab = lever.Value;
        }

        private void OnUngrabbed(ControlImplBase lever)
        {
            var click = Time.time - grabbedAt <= ClickTime && Mathf.Abs(lever.Value - valueAtGrab) < ClickTravel;
            grabbedAt = float.NegativeInfinity;
            if (click)
                Bail();
        }

        // The use button pressed and let go while this lever is held, in VR.
        private void OnUsed(object sender, InteractableObjectEventArgs e) => Bail();

        private void Bail()
        {
            if (!Main.Active || !Main.Settings.ClickToBailOff || Car == null)
                return;
            BailConsist(Car, true);
            // Everyone else's engines bail the same (multiplayer).
            Net.Report(new NetAction(Car, NetActionKind.Bail, 0, 0));
        }

        /// <summary>Bails off <paramref name="car"/> and the engines on its multiple-unit cables; tells the player, when <paramref name="notice"/>.</summary>
        internal static void BailConsist(TrainCar car, bool notice)
        {
            if (!Main.Active || car == null)
                return;
            var engines = 0;
            var bailed = 0;
            foreach (var each in Consist(car))
            {
                if (each.brakeSystem == null || !BrakeSets.TryGet(each.brakeSystem, out var air) || air.Brake.Locomotive == null)
                    continue;
                engines++;
                Api.BailOff(each);
                if (air.Brake.Locomotive.BailedOff > MinNoticed)
                    bailed++;
            }
            if (engines == 0 || !notice)
                return;
            Notice.Show(bailed == 0 ? "Nothing to bail off: the train brake isn't applied"
                : bailed == 1 ? "Bailed off: the engine's brakes release, the train's stay on"
                : $"Bailed off {bailed} engines: their brakes release, the train's stay on");
        }

        /// <summary><paramref name="car"/> and every car joined to it by multiple-unit cable, near to far.</summary>
        private static IEnumerable<TrainCar> Consist(TrainCar car)
        {
            var seen = new HashSet<TrainCar>();
            var queue = new Queue<TrainCar>();
            queue.Enqueue(car);
            while (queue.Count > 0)
            {
                var next = queue.Dequeue();
                if (next == null || !seen.Add(next))
                    continue;
                yield return next;
                var mu = next.muModule;
                if (mu == null)
                    continue;
                foreach (var cable in new[] { mu.FrontCable, mu.RearCable })
                {
                    var other = cable?.connectedTo?.muModule;
                    if (other != null)
                        queue.Enqueue(other.train);
                }
            }
        }
    }
}
