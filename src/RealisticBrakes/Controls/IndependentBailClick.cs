using System.Collections.Generic;
using System.Runtime.CompilerServices;
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

        private const double MinNoticedShare = 0.013;

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

        private const float SendEvery = 0.25f;

        // Unified graduated-bail state per engine so simultaneous lever, key, and notch inputs (or two cabs on one engine)
        // never double-apply BailRate in one frame or send BailLetGo while another input is still holding.
        private sealed class EngineHold
        {
            public int ActiveInputs;
            public int LastBailedFrame = -1;
            public double Unsent;
            public double BailedThisHold;
            public float SentAt;
            public bool HoldingRelease;

            public void Step(TrainCar car, double amount, bool byKey, bool sendNow = true)
            {
                if (!Main.Active || (!byKey && !Main.Settings.ClickToBailOff) || car == null)
                    return;
                HoldingRelease = true;
                if (LastBailedFrame == Time.frameCount)
                    return;
                LastBailedFrame = Time.frameCount;
                var bailed = BailConsist(car, false, amount);
                Unsent += bailed;
                BailedThisHold += bailed;
                if (sendNow && Time.time - SentAt >= SendEvery)
                    Send(car, final: false);
            }

            public void StepOnce(TrainCar car, double amount, bool byKey)
            {
                if (!Main.Active || (!byKey && !Main.Settings.ClickToBailOff) || car == null)
                    return;
                var bailed = BailConsist(car, false, amount);
                Unsent += bailed;
                BailedThisHold += bailed;
                if (ActiveInputs == 0)
                    End(car);
            }

            public void End(TrainCar car)
            {
                Send(car, final: true);
                if (HoldingRelease && car != null)
                {
                    HoldingRelease = false;
                    LetGoConsist(car);
                    Net.Report(new NetAction(car, NetActionKind.Bail, BailLetGo, 0));
                }
                if (BailedThisHold > MinNoticed)
                    Notice.Show($"Bailed off {BailedThisHold / Core.Air.Bar:F1} bar of the engine's brake; the train's stays on");
                BailedThisHold = 0;
            }

            // Sends quantized GraduatedBail steps, carrying the signed remainder across intervals so repeated sends never
            // drift between guests and host; only clears Unsent when the hold ends.
            private void Send(TrainCar car, bool final)
            {
                SentAt = Time.time;
                if (car == null)
                    return;
                var sent = false;
                while (Unsent >= NetUnit / 2)
                {
                    var units = (byte)Mathf.Clamp(Mathf.RoundToInt((float)(Unsent / NetUnit)), 1, 255);
                    Net.Report(new NetAction(car, NetActionKind.Bail, GraduatedBail, units));
                    Unsent -= units * NetUnit;
                    sent = true;
                }
                if (final)
                    Unsent = 0;
                else if (!sent && HoldingRelease)
                    Net.Report(new NetAction(car, NetActionKind.Bail, GraduatedBail, 0));
            }
        }

        private static readonly ConditionalWeakTable<TrainCar, EngineHold> holds = new ConditionalWeakTable<TrainCar, EngineHold>();

        private ControlImplBase control;
        private IndependentHandle handle;
        private TrainCar holdCar;
        private bool leverHeld;
        private bool notchHeld;
        private bool keyHeld;
        private bool keyGraduated;
        private float keyDownAt = float.NegativeInfinity;
        // One lever component polls BailOffKey per car so two-cab engines don't double-bail or double-tap.
        private static IndependentBailClick keyPoller;
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
            grabbedAt = float.NegativeInfinity;
            keyHeld = false;
            keyDownAt = float.NegativeInfinity;
            SetInput(ref leverHeld, false);
            SetInput(ref keyGraduated, false);
            SetInput(ref notchHeld, false);
            if (keyPoller == this)
                keyPoller = null;
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
            SetInput(ref leverHeld, false);
            if (!click || !Main.Active || !Main.Settings.ClickToBailOff || Car == null)
                return;
            if (GraduatedOnly(Car))
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
            if (Car != null && GraduatedOnly(Car))
                holds.GetOrCreateValue(Car).StepOnce(Car, BailStep, byKey: false);
            else
                Bail();
        }

        // An E-T or European engine's lever held still past a click bails it off graduated while held. Not in VR, where a hand
        // rests on a lever while it's held and the trigger bails (the review, 2026-10-28).
        private void Update()
        {
            PollBailOffKey();
            // A handle with the release notch bails there instead (HoldRelease).
            if (float.IsNegativeInfinity(grabbedAt) || control == null || vrLever != null || ReleaseNotch)
                return;
            var still = Mathf.Abs(control.Value - valueAtGrab) < ClickTravel;
            if (!still || Time.time - grabbedAt <= ClickTime || !Main.Active || !Main.Settings.ClickToBailOff || Car == null || !CanGraduate(Car))
            {
                SetInput(ref leverHeld, false);
                return;
            }
            SetInput(ref leverHeld, true);
            holds.GetOrCreateValue(Car).Step(Car, BailRate * Time.deltaTime, byKey: false);
        }

        // The bail-off key, on the engine the player is on: on American 26-L diesels a press bails in full; on the E-T it
        // bails graduated while held; on European engines a quick tap bails in full and holding past ClickTime graduates off.
        private void PollBailOffKey()
        {
            var key = Main.Settings?.BailOffKey;
            var mine = key != null && key.keyCode != KeyCode.None && Main.Active && Car != null && PlayerManager.Car == Car;
            if (mine)
            {
                if (keyPoller != null && keyPoller != this && keyPoller.isActiveAndEnabled && keyPoller.Car == Car)
                    return;
                keyPoller = this;
            }
            else if (keyPoller == this)
                keyPoller = null;

            if (mine && !CanGraduate(Car))
            {
                if (keyHeld)
                {
                    keyHeld = false;
                    keyDownAt = float.NegativeInfinity;
                    SetInput(ref keyGraduated, false);
                }
                if (key.Down())
                    Bail(byKey: true);
                return;
            }
            if (mine && GraduatedOnly(Car))
            {
                if (key.Pressed())
                {
                    keyHeld = true;
                    SetInput(ref keyGraduated, true);
                    holds.GetOrCreateValue(Car).Step(Car, BailRate * Time.deltaTime, byKey: true);
                }
                else if (keyHeld)
                {
                    keyHeld = false;
                    keyDownAt = float.NegativeInfinity;
                    SetInput(ref keyGraduated, false);
                }
                return;
            }
            // European engines: start timing only on the key-down edge so a second cab taking over mid-press never
            // triggers a spurious full bail on release.
            if (!keyHeld)
            {
                if (mine && key.Down())
                {
                    keyHeld = true;
                    keyDownAt = Time.time;
                }
                return;
            }
            var elapsed = Time.time - keyDownAt;
            if (mine && key.Pressed())
            {
                if (elapsed > ClickTime)
                {
                    SetInput(ref keyGraduated, true);
                    holds.GetOrCreateValue(Car).Step(Car, BailRate * Time.deltaTime, byKey: true);
                }
                return;
            }
            var wasGraduated = keyGraduated;
            keyHeld = false;
            keyDownAt = float.NegativeInfinity;
            if (wasGraduated)
            {
                SetInput(ref keyGraduated, false);
            }
            else if (mine)
            {
                if (elapsed <= ClickTime)
                    Bail(byKey: true);
                else
                {
                    var hold = holds.GetOrCreateValue(Car);
                    if (hold.ActiveInputs == 0)
                    {
                        var overshoot = Mathf.Min(Time.deltaTime, elapsed - ClickTime);
                        if (overshoot > 0f)
                        {
                            hold.Step(Car, BailRate * overshoot, byKey: true, sendNow: false);
                            hold.End(Car);
                        }
                    }
                }
            }
        }

        private void SetInput(ref bool flag, bool active)
        {
            if (flag == active)
                return;
            flag = active;
            var target = active ? Car : holdCar;
            if (target == null)
                return;
            var hold = holds.GetOrCreateValue(target);
            if (active)
            {
                holdCar = target;
                hold.ActiveInputs++;
            }
            else if (hold.ActiveInputs > 0 && --hold.ActiveInputs == 0)
            {
                hold.End(target);
            }
            if (!leverHeld && !keyGraduated && !notchHeld)
                holdCar = null;
        }

        private void Bail(bool byKey = false)
        {
            if (!Main.Active || (!byKey && !Main.Settings.ClickToBailOff) || Car == null)
                return;
            BailFull(Car, notice: true);
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
            if (held && Car != null)
            {
                SetInput(ref notchHeld, true);
                holds.GetOrCreateValue(Car).Step(Car, BailRate * Time.deltaTime, byKey: true);
            }
            else if (notchHeld)
            {
                SetInput(ref notchHeld, false);
            }
        }

        // Whether car's engine bails off only graduated (no full-bail click): the E-T's (Core.LocomotiveBrakeSpec.HoldsAutomaticBrake).
        private static bool GraduatedOnly(TrainCar car) =>
            car.brakeSystem != null && BrakeSets.TryGet(car.brakeSystem, out var air) && air.Brake.Locomotive != null
            && air.Brake.Locomotive.Spec.HoldsAutomaticBrake;

        // Whether car's engine supports graduated bail-off while held: E-T engines and European engines.
        private static bool CanGraduate(TrainCar car) =>
            car.brakeSystem != null && BrakeSets.TryGet(car.brakeSystem, out var air) && air.Brake.Locomotive != null
            && (air.Brake.Locomotive.Spec.HoldsAutomaticBrake || air.Brake.Locomotive.Spec.GraduatedBail);

        private static bool AutomaticApplied(TrainCar car) =>
            car.brakeSystem != null && BrakeSets.TryGet(car.brakeSystem, out var air) && air.Brake.Locomotive != null
            && air.Brake.Locomotive.AutomaticApplied && air.Brake.Locomotive.BailedOff < air.Brake.Locomotive.HeldAutomatic;

        internal delegate bool TryGetBrake<TCar>(TCar car, out Core.CarBrake brake, out System.Action touch);

        /// <summary>
        /// Whether <paramref name="car"/> is itself a modeled engine whose
        /// brake can be bailed off (guards multiple-unit traversal so a
        /// non-engine such as a slug on an MU cable cannot bail a coupled
        /// engine).
        /// </summary>
        internal static bool CanBailCar(TrainCar car) =>
            TryGetCarBrake(car, out var brake, out _) && brake.CanBail;

        private static bool TryGetCarBrake(TrainCar car, out Core.CarBrake brake, out System.Action touch)
        {
            if (car != null && car.brakeSystem != null && BrakeSets.TryGet(car.brakeSystem, out var air))
            {
                brake = air.Brake;
                touch = air.Touch;
                return true;
            }
            brake = null;
            touch = null;
            return false;
        }

        /// <summary>
        /// Bails off <paramref name="car"/> and the engines on its multiple-unit
        /// cables in full, and reports the bail to multiplayer. Tells the player
        /// when <paramref name="notice"/>. Returns the cylinder pressure bailed
        /// on <paramref name="car"/>, gauge Pa (0 when inactive, null, not an
        /// engine that can bail, or nothing to bail).
        /// </summary>
        internal static double BailFull(TrainCar car, bool notice) =>
            !Main.Active || car == null
                ? 0
                : BailFull(car, notice, TryGetCarBrake, Consist, c => Net.Report(new NetAction(c, NetActionKind.Bail, FullBail, 0)));

        internal static double BailFull<TCar>(
            TCar car,
            bool notice,
            TryGetBrake<TCar> tryGetBrake,
            System.Func<TCar, IEnumerable<TCar>> consist,
            System.Action<TCar> report) where TCar : class
        {
            if (car == null || !tryGetBrake(car, out var startBrake, out _))
                return 0;
            if (!startBrake.CanBail)
            {
                if (notice && startBrake.VacuumEngine != null && !startBrake.VacuumEngine.CanBailOff)
                    Notice.Show("A steam brake can't be bailed off: it follows the train pipe, and comes off as the vacuum comes back");
                return 0;
            }
            BailConsist(car, notice, double.PositiveInfinity, Core.LocomotiveBrake.BailHold, tryGetBrake, consist, out var carBailed);
            // Everyone else's engines bail the same (multiplayer).
            report(car);
            return carBailed;
        }

        /// <summary>
        /// Bails off <paramref name="car"/> and the engines on its multiple-unit
        /// cables: in full, or up to <paramref name="amount"/> gauge Pa,
        /// graduated. Tells the player of a full bail, when
        /// <paramref name="notice"/>. Returns the most bailed off any engine, Pa.
        /// </summary>
        internal static double BailConsist(TrainCar car, bool notice, double amount = double.PositiveInfinity, double hold = Core.LocomotiveBrake.BailHold) =>
            !Main.Active || car == null ? 0 : BailConsist(car, notice, amount, hold, TryGetCarBrake, Consist, out _);

        private static double BailConsist<TCar>(
            TCar car,
            bool notice,
            double amount,
            double hold,
            TryGetBrake<TCar> tryGetBrake,
            System.Func<TCar, IEnumerable<TCar>> consist,
            out double carBailed) where TCar : class
        {
            carBailed = 0;
            if (car == null || !tryGetBrake(car, out var startBrake, out _) || !startBrake.CanBail)
                return 0;
            var engines = 0;
            var bailed = 0;
            var most = 0.0;
            foreach (var each in consist(car))
            {
                if (each == null || !tryGetBrake(each, out var brake, out var touch))
                    continue;
                var vacuum = brake.VacuumEngine;
                if (vacuum != null)
                {
                    engines++;
                    if (vacuum.CanBailOff && double.IsPositiveInfinity(amount))
                    {
                        var dropped = BailEngine(brake, touch);
                        most = System.Math.Max(most, dropped);
                        if (ReferenceEquals(each, car))
                            carBailed = dropped;
                    }
                    if (vacuum.BailedOff > MinNoticedShare)
                        bailed++;
                    continue;
                }
                if (brake.Locomotive == null)
                    continue;
                engines++;
                double droppedAir;
                if (double.IsPositiveInfinity(amount))
                    droppedAir = BailEngine(brake, touch);
                else
                {
                    touch?.Invoke();
                    droppedAir = brake.Locomotive.BailOffBy(amount, hold);
                }
                most = System.Math.Max(most, droppedAir);
                if (ReferenceEquals(each, car))
                    carBailed = droppedAir;
                if (brake.Locomotive.BailedOff > MinNoticed)
                    bailed++;
            }
            if (engines == 0 || !notice)
                return most;
            Notice.Show(bailed == 0 ? "Nothing to bail off: the train brake isn't applied"
                : bailed == 1 ? "Bailed off: the engine's brakes release, the train's stay on"
                : $"Bailed off {bailed} engines: their brakes release, the train's stay on");
            return most;
        }

        private static double BailEngine(Core.CarBrake brake, System.Action touch)
        {
            var vacuum = brake.VacuumEngine;
            if (vacuum != null)
            {
                touch?.Invoke();
                return vacuum.BailOff() * vacuum.Spec.AirBrakeMaximum;
            }
            touch?.Invoke();
            return brake.Locomotive.BailOff();
        }

        /// <summary>Another player let go of the independent: <paramref name="car"/>'s consist's bail holds no longer.</summary>
        internal static void LetGoConsist(TrainCar car)
        {
            if (!Main.Active || !CanBailCar(car))
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
