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

        /// <summary>How fast a held lever bails an E-T engine off, Pa/s: its 3.8 bar full service in about 4 s.</summary>
        public const double BailRate = 1.0 * Core.Air.Bar;

        /// <summary>What a VR use bails off an E-T engine, Pa.</summary>
        public const double BailStep = 0.5 * Core.Air.Bar;

        /// <summary>The unit of a graduated bail sent to other players, Pa (0.02 bar; a byte's worth is 5.1 bar).</summary>
        public const double NetUnit = 0.02 * Core.Air.Bar;

        /// <summary>
        /// How long, in s, another player's lever counts as held in release
        /// after their last word (<see cref="Core.LocomotiveBrake.BailOffBy"/>'s
        /// hold): four of their sends, for the network's delays. Their let-go
        /// ends it at once.
        /// </summary>
        public const double RemoteHold = 1.0;

        /// <summary>The <see cref="NetActionKind.Bail"/> subs: a full bail (0), a graduated one of the value in <see cref="NetUnit"/>s, 0 for still held (1), and the lever let go (2).</summary>
        public const byte FullBail = 0, GraduatedBail = 1, BailLetGo = 2;

        // A graduated bail made here and not yet sent, Pa, and when it was last sent; whether the lever is held in release.
        private double unsent;
        private double bailedThisHold;
        private float sentAt;
        private bool holdingRelease;
        private const float SendEvery = 0.25f;

        private ControlImplBase control;
        private IndependentHandle handle;
        private bool notchHeld;
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
            EndHold();
            if (!click || !Main.Active || !Main.Settings.ClickToBailOff || Car == null)
                return;
            if (Graduated(Car))
            {
                if (AutomaticApplied(Car))
                    Notice.Show(ReleaseNotch
                        ? "Hold the independent in release, below running, to bail off: the S-6 bails off for as long as you hold it there"
                        : vrLever != null
                        ? "Hold the independent lever and press the trigger to bail off: each press lets off some"
                        : "Click and hold the independent lever to bail off: the E-T bails off for as long as you hold it");
                return;
            }
            Bail();
        }

        // The use button pressed and let go while this lever is held, in VR.
        private void OnUsed(object sender, InteractableObjectEventArgs e)
        {
            if (Car != null && Graduated(Car))
                BailBy(BailStep, true);
            else
                Bail();
        }

        // An E-T engine's lever held still past a click bails it off while it's held. Not in VR, where a hand rests on a lever
        // while it's held and the trigger bails (the review, 2026-10-28).
        private void Update()
        {
            // A handle with the release notch bails there instead (HoldRelease).
            if (float.IsNegativeInfinity(grabbedAt) || control == null || vrLever != null || ReleaseNotch)
                return;
            var still = Mathf.Abs(control.Value - valueAtGrab) < ClickTravel;
            if (!still || Time.time - grabbedAt <= ClickTime || !Main.Active || !Main.Settings.ClickToBailOff || Car == null || !Graduated(Car))
            {
                EndHold();
                return;
            }
            holdingRelease = true;
            BailBy(BailRate * Time.deltaTime, false);
            if (Time.time - sentAt >= SendEvery)
                Send();
        }

        private void Bail()
        {
            if (!Main.Active || !Main.Settings.ClickToBailOff || Car == null)
                return;
            BailConsist(Car, true);
            // Everyone else's engines bail the same (multiplayer).
            Net.Report(new NetAction(Car, NetActionKind.Bail, FullBail, 0));
        }

        // Whether this lever is the S-6 handle with the release notch (IndependentHandle).
        private bool ReleaseNotch
        {
            get
            {
                if (handle == null)
                    handle = GetComponent<IndependentHandle>();
                return handle != null && handle.HasReleaseNotch;
            }
        }

        /// <summary>
        /// The S-6 handle held in release (<see cref="IndependentHandle"/>), or no longer: it bails off at
        /// <see cref="BailRate"/> while held, as the S-6 in release does, and the hold ends when it's let go. The S-6's own
        /// release, not the click, so the click setting doesn't turn it off.
        /// </summary>
        internal void HoldRelease(bool held)
        {
            if (held)
            {
                notchHeld = true;
                holdingRelease = true;
                BailBy(BailRate * Time.deltaTime, false, notch: true);
                if (Time.time - sentAt >= SendEvery)
                    Send();
            }
            else if (notchHeld)
            {
                notchHeld = false;
                EndHold();
            }
        }

        // A graduated bail of amount, Pa, here; sent on now if send, or with the next. The click's unless notch.
        private void BailBy(double amount, bool send, bool notch = false)
        {
            if (!Main.Active || (!notch && !Main.Settings.ClickToBailOff) || Car == null)
                return;
            var bailed = BailConsist(Car, false, amount);
            unsent += bailed;
            bailedThisHold += bailed;
            if (send)
                EndHold();
        }

        // The hold ends: the rest of its bail goes to the other players, then the let-go, and the player hears what was bailed.
        private void EndHold()
        {
            Send();
            if (holdingRelease && Car != null)
            {
                holdingRelease = false;
                Net.Report(new NetAction(Car, NetActionKind.Bail, BailLetGo, 0));
            }
            if (bailedThisHold > MinNoticed)
                Notice.Show($"Bailed off {bailedThisHold / Core.Air.Bar:F1} bar of the engine's brake; the train's stays on");
            bailedThisHold = 0;
        }

        // The bail made since the last send; while the lever is held in release with nothing more to let out, a word that it's
        // still held, as in the engine's own emergency the bail lasts only while it's held (the second review, 2026-10-28).
        private void Send()
        {
            sentAt = Time.time;
            if (Car == null)
                return;
            var sent = false;
            while (unsent >= NetUnit / 2)
            {
                var units = (byte)Mathf.Clamp(Mathf.RoundToInt((float)(unsent / NetUnit)), 1, 255);
                Net.Report(new NetAction(Car, NetActionKind.Bail, GraduatedBail, units));
                unsent -= units * NetUnit;
                sent = true;
            }
            unsent = 0;
            if (!sent && holdingRelease)
                Net.Report(new NetAction(Car, NetActionKind.Bail, GraduatedBail, 0));
        }

        // Whether car's engine bails off graduated: the E-T's (Core.LocomotiveBrakeSpec.HoldsAutomaticBrake).
        private static bool Graduated(TrainCar car) =>
            car.brakeSystem != null && BrakeSets.TryGet(car.brakeSystem, out var air) && air.Brake.Locomotive != null
            && air.Brake.Locomotive.Spec.HoldsAutomaticBrake;

        private static bool AutomaticApplied(TrainCar car) =>
            car.brakeSystem != null && BrakeSets.TryGet(car.brakeSystem, out var air) && air.Brake.Locomotive != null
            && air.Brake.Locomotive.AutomaticApplied && air.Brake.Locomotive.BailedOff < air.Brake.Locomotive.HeldAutomatic;

        /// <summary>
        /// Bails off <paramref name="car"/> and the engines on its multiple-unit
        /// cables: in full, or up to <paramref name="amount"/> gauge Pa,
        /// graduated. Tells the player of a full bail, when
        /// <paramref name="notice"/>. Returns the most bailed off any engine, Pa.
        /// </summary>
        internal static double BailConsist(TrainCar car, bool notice, double amount = double.PositiveInfinity, double hold = Core.LocomotiveBrake.BailHold)
        {
            if (!Main.Active || car == null)
                return 0;
            var engines = 0;
            var bailed = 0;
            var most = 0.0;
            foreach (var each in Consist(car))
            {
                if (each.brakeSystem == null || !BrakeSets.TryGet(each.brakeSystem, out var air) || air.Brake.Locomotive == null)
                    continue;
                engines++;
                if (double.IsPositiveInfinity(amount))
                    Api.BailOff(each);
                else
                {
                    air.Touch();
                    most = System.Math.Max(most, air.Brake.Locomotive.BailOffBy(amount, hold));
                }
                if (air.Brake.Locomotive.BailedOff > MinNoticed)
                    bailed++;
            }
            if (engines == 0 || !notice)
                return most;
            Notice.Show(bailed == 0 ? "Nothing to bail off: the train brake isn't applied"
                : bailed == 1 ? "Bailed off: the engine's brakes release, the train's stay on"
                : $"Bailed off {bailed} engines: their brakes release, the train's stay on");
            return most;
        }

        /// <summary>Another player let go of the independent: <paramref name="car"/>'s consist's bail holds no longer.</summary>
        internal static void LetGoConsist(TrainCar car)
        {
            if (!Main.Active || car == null)
                return;
            foreach (var each in Consist(car))
                if (each.brakeSystem != null && BrakeSets.TryGet(each.brakeSystem, out var air) && air.Brake.Locomotive != null)
                {
                    air.Brake.Locomotive.EndBail();
                    air.Touch();
                }
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
