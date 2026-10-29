using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using DV.CabControls;
using DV.Simulation.Brake;
using HarmonyLib;
using RealisticBrakes.Core;
using RealisticBrakes.Sim;
using UnityEngine;
using Lever = DV.CabControls.Spec.Lever;

namespace RealisticBrakes.Controls
{
 internal sealed class TrainBrakeHandle : MonoBehaviour
    {
        private static readonly ConditionalWeakTable<BrakeSystem, TrainBrakeHandle> byBrakes = new ConditionalWeakTable<BrakeSystem, TrainBrakeHandle>();

        // The game's own members a widened handle calls, found once (Supported).
        private static Action<ControlImplBase, float> requestValueUpdate;
        private static Action<ControlImplBase, float> acceptSetValue;
        private static Action<SteppedJoint> restartSteppedJoint;
        private static bool? supported;

        /// <summary>Whether the game still has the members a widened handle calls; looked up once, and logged if not.</summary>
        public static bool Supported
        {
            get
            {
                if (supported == null)
                {
                    try
                    {
                        requestValueUpdate = AccessTools.MethodDelegate<Action<ControlImplBase, float>>(AccessTools.Method(typeof(ControlImplBase), "RequestValueUpdate", new[] { typeof(float) }));
                        acceptSetValue = AccessTools.MethodDelegate<Action<ControlImplBase, float>>(AccessTools.Method(typeof(ControlImplBase), "AcceptSetValue", new[] { typeof(float) }));
                        restartSteppedJoint = AccessTools.MethodDelegate<Action<SteppedJoint>>(AccessTools.Method(typeof(SteppedJoint), "Start"));
                        supported = true;
                    }
                    catch (Exception e)
                    {
                        supported = false;
                        Main.Mod?.Logger.Warning($"The train brake handles stay stock: {e.GetBaseException().Message}");
                    }
                }
                return supported.Value;
            }
        }

        /// <summary>The handle's layout.</summary>
        public WidenedHandle Layout { get; private set; }

        /// <summary>The notch it stands at, from the release end.</summary>
        public int Notch { get; private set; }

        // Let go past release longer than this, in s, without the game's spring taking it home: the handle is put back.
        private const float SpringBackTime = 0.5f;

        private bool invert;
        private bool settingRaw;
        private float followedAt = float.NegativeInfinity;
        private ControlImplBase control;
        private SteppedJoint stepped;
        private float heldPastReleaseSince = float.NaN;
        private float letGoInFillAt = float.NaN;

        /// <summary>How long, in s, the handle has been held past release; 0 when it isn't.</summary>
        public float HeldPastRelease => Notch == 0 && !float.IsNaN(heldPastReleaseSince) ? Time.time - heldPastReleaseSince : 0f;

        // In a hand, or kept there by a scroll or the brake keys, whose spring the game holds off until 0.3 s after the last notch.
        private bool Held
        {
            get
            {
                if (stepped == null)
                    stepped = GetComponent<SteppedJoint>();
                return control.IsGrabbed() || (stepped != null && !stepped.isSpringActive);
            }
        }

        /// <summary>The player a zone set here is from: this game's own hand.</summary>
        public const byte LocalHand = 255;

        // Where an engine's handle stands in its added notches: one per engine, the last hand to set it wins, this game's or
        // another player's (multiplayer: the added notches aren't in the handle's value). It outlives the cab, as a handle
        // left in emergency stays there (a review, 2026-10-27, found another player's emergency outliving the next driver's
        // release, and a handle's added notch vanishing with its cab).
        private sealed class Zone
        {
            public HandleZone Value;
            public byte Hand = LocalHand;
            public TrainCar Car;
        }

        private static readonly ConditionalWeakTable<BrakeSystem, Zone> zones = new ConditionalWeakTable<BrakeSystem, Zone>();

        // Other players' sprung filling strokes, which end with their hand.
        private static readonly List<Zone> remoteFills = new List<Zone>();

        /// <summary>Where <paramref name="system"/>'s handle stands in its added notches; <see cref="HandleZone.Travel"/> when in none.</summary>
        public static HandleZone ZoneOf(BrakeSystem system) =>
            system != null && zones.TryGetValue(system, out var zone) ? zone.Value : HandleZone.Travel;

        /// <summary>
        /// Another player's (<paramref name="hand"/>) handle on <paramref name="car"/>'s
        /// engine stands at <paramref name="zone"/>; <see cref="LocalHand"/>
        /// when it is this game's own, as the host confirms it.
        /// </summary>
        public static void SetRemoteZone(TrainCar car, HandleZone zone, byte hand)
        {
            var system = car != null ? car.brakeSystem : null;
            if (system == null)
                return;
            var entry = zones.GetOrCreateValue(system);
            entry.Value = zone;
            entry.Hand = hand;
            entry.Car = car;
            remoteFills.RemoveAll(z => z.Value != HandleZone.Fill || z.Hand == LocalHand);
            // A self-lapping handle's filling stroke springs back as the hand leaves it; a steam engine's release stays where
            // it's put (a review, 2026-10-27, found a departing driver's release cancelled).
            if (zone == HandleZone.Fill && hand != LocalHand && system.selfLappingController && !remoteFills.Contains(entry))
                remoteFills.Add(entry);
        }

        /// <summary>
        /// Whether a handle in <paramref name="zone"/> can stand at the stock
        /// value <paramref name="stock"/>: its added notch reads exactly as
        /// release (a filling stroke, a steam engine's release, holding) or
        /// full service (emergency), within a float's rounding. Wider bands
        /// took a fine Custom Car Loader handle's first or last service notch
        /// for them (the second review, 2026-10-28: 1/23 on 24 notches).
        /// </summary>
        public static bool Stands(HandleZone zone, float stock) =>
            zone == HandleZone.Emergency ? stock >= 1f - EndTolerance : zone == HandleZone.Travel || stock <= EndTolerance;

        // A stock value this close to an end is at it: far under any handle's notch spacing.
        private const float EndTolerance = 1e-3f;

        /// <summary>A value set from outside took <paramref name="system"/>'s handle out of its added notch.</summary>
        public static void ClearZone(BrakeSystem system)
        {
            if (system == null || !zones.TryGetValue(system, out var entry) || entry.Value == HandleZone.Travel)
                return;
            entry.Value = HandleZone.Travel;
            remoteFills.Remove(entry);
        }

        /// <summary>Whether a handle left in <paramref name="zone"/> stays there, to be saved: all but a diesel's sprung filling stroke.</summary>
        public static bool Stays(HandleZone zone, BrakeSystem system) =>
            zone == HandleZone.Holding || zone == HandleZone.Emergency || (zone == HandleZone.Fill && system != null && !system.selfLappingController);

        /// <summary>The game resets <paramref name="system"/>'s engine for reuse: its handle's added notch goes with it.</summary>
        public static void Forget(BrakeSystem system)
        {
            if (system == null || !zones.TryGetValue(system, out var entry))
                return;
            zones.Remove(system);
            remoteFills.Remove(entry);
        }

        /// <summary>Where <paramref name="system"/>'s handle stands in its added notches, and whose hand set it; false when no hand has.</summary>
        public static bool TryGetZone(BrakeSystem system, out HandleZone zone, out byte hand)
        {
            zone = HandleZone.Travel;
            hand = LocalHand;
            if (system == null || !zones.TryGetValue(system, out var entry))
                return false;
            zone = entry.Value;
            hand = entry.Hand;
            return true;
        }

        /// <summary>
        /// A player's hand leaves the session (<paramref name="hand"/>; all,
        /// when null): their sprung filling strokes end (a diesel's; a steam
        /// engine's release stays). Returns the engines
        /// whose stroke ended, for the host to tell its guests.
        /// </summary>
        public static List<TrainCar> ReleaseHands(byte? hand)
        {
            var ended = new List<TrainCar>();
            foreach (var zone in remoteFills)
                if (zone.Value == HandleZone.Fill && (hand == null || zone.Hand == hand))
                {
                    zone.Value = HandleZone.Travel;
                    if (zone.Car != null)
                        ended.Add(zone.Car);
                }
            remoteFills.RemoveAll(z => z.Value != HandleZone.Fill);
            return ended;
        }

        // Whether this game's hand set the engine's zone last.
        private bool OwnsZone => car != null && car.brakeSystem != null && zones.TryGetValue(car.brakeSystem, out var zone) && zone.Hand == LocalHand;

        private TrainCar car;
        private HandleZone localZone = HandleZone.Travel;

        // This game's hand sets the engine's zone, and everyone else in the session is told.
        private void Write(HandleZone zone)
        {
            var system = car != null ? car.brakeSystem : null;
            if (system == null)
                return;
            var entry = zones.GetOrCreateValue(system);
            if (entry.Value == zone && entry.Hand == LocalHand)
                return;
            entry.Value = zone;
            entry.Hand = LocalHand;
            entry.Car = car;
            Net.Report(new NetAction(car, NetActionKind.HandleZone, 0, (byte)zone));
        }

        /// <summary>
        /// Widens <paramref name="control"/>, <paramref name="car"/>'s train
        /// brake handle, if it is a notched lever. Done as the cab's controls
        /// are found, before the lever builds its joint a frame later; a lever
        /// already built is widened in place.
        /// </summary>
        public static void Widen(ControlImplBase control, TrainCar car)
        {
            var system = car != null ? car.brakeSystem : null;
            if (!Supported || !(control is LeverBase) || system == null || !system.hasTrainBrake
                || control.GetComponent<TrainBrakeHandle>() != null)
                return;
            var spec = control.GetComponent<Lever>();
            if (spec == null || !spec.useSteppedJoint || !spec.steppedValueUpdate || !spec.useLimits || spec.notches < 2)
                return;
            // The American 26-L has no filling stroke (DriverBrakeValveSpec.FillingStroke), and the H-6 has holding; the
            // equipment is read as the cab loads.
            var american = Main.Settings.Equipment == Equipment.American;
            var layout = system.selfLappingController
                ? WidenedHandle.SelfLapping(spec.notches, fillingStroke: !american)
                : WidenedHandle.ManualLap(spec.notches, holding: american);
            var step = (spec.jointLimitMax - spec.jointLimitMin) / (spec.notches - 1);
            // Value 0, release, is at the joint's lower limit, or its upper on an inverted lever. Holding comes in after running,
            // so running moves a notch toward release with it, and the stock notches past it keep their places.
            var atRelease = ((layout.ReleaseNotch ? 1 : 0) + (layout.HoldingNotch ? 1 : 0)) * step;
            var atFull = layout.EmergencyNotch ? step : 0f;
            var min = spec.jointLimitMin - (spec.invertDirection ? atFull : atRelease);
            var max = spec.jointLimitMax + (spec.invertDirection ? atRelease : atFull);
            // The game clamps a lever's travel to ±177°; one beyond that couldn't keep its notches even.
            if (step <= 0f || min < -177f || max > 177f)
                return;

            spec.jointLimitMin = min;
            spec.jointLimitMax = max;
            spec.notches = layout.Notches;
            if (layout.SpringsBack)
            {
                // The game's notched-lever spring holds the handle within these, so the filling stroke springs back to release.
                spec.useInnerLimitSpring = true;
                spec.innerLimitMinNotch = 1;
                spec.innerLimitMaxNotch = layout.Notches - 1;
            }
            else if (spec.useInnerLimitSpring && layout.ReleaseNotch)
            {
                // A handle's own spring zone, counted from release, moves with the notches added there.
                spec.innerLimitMinNotch = layout.NotchOf(spec.innerLimitMinNotch);
                spec.innerLimitMaxNotch = layout.NotchOf(spec.innerLimitMaxNotch);
            }

            var handle = control.gameObject.AddComponent<TrainBrakeHandle>();
            handle.control = control;
            handle.car = car;
            handle.Layout = layout;
            handle.invert = spec.invertDirection;
            handle.Notch = layout.NotchFor(control.Value);
            byBrakes.Remove(system);
            byBrakes.Add(system, handle);

            var joint = control.GetComponent<HingeJoint>();
            if (joint != null)
            {
                // Built already: widen the joint and its notches in place.
                var limits = joint.limits;
                limits.min = spec.jointLimitMin;
                limits.max = spec.jointLimitMax;
                joint.limits = limits;
                var stepped = control.GetComponent<SteppedJoint>();
                if (stepped != null)
                {
                    stepped.notches = spec.notches;
                    stepped.useInnerLimitSpring = spec.useInnerLimitSpring;
                    stepped.innerLimitMinNotch = spec.innerLimitMinNotch;
                    stepped.innerLimitMaxNotch = spec.innerLimitMaxNotch;
                    restartSteppedJoint(stepped);
                }
                control.SetValue(control.Value);
            }
        }

        /// <summary>The lever moved to the notch <paramref name="fromMinimum"/> counts from its joint's lower limit: reports the stock value.</summary>
        public void OnNotch(ControlImplBase control, float fromMinimum)
        {
            var notch = Mathf.RoundToInt(fromMinimum);
            var notches = Layout.Notches;
            Notch = Mathf.Clamp(invert ? notches - 1 - notch : notch, 0, notches - 1);
            requestValueUpdate(control, Layout.StockValue(Notch));
            // A hand on this handle takes the engine's zone over from whoever set it last (a value set from outside never moves it into the added notches).
            if (Held)
                Write(Layout.Zone(Notch, HeldPastRelease));
        }

        // This handle's added notches, as this game's hand moves them: under a hand, or its own filling stroke springing back
        // (a review, 2026-10-27: a value set from outside moved the handle and took the zone without a hand on it).
        private void ReportZone()
        {
            if (Layout == null)
                return;
            var zone = Layout.Zone(Notch, HeldPastRelease);
            if (zone == localZone)
                return;
            var was = localZone;
            localZone = zone;
            if (Held || (OwnsZone && was == HandleZone.Fill && Layout.SpringsBack))
                Write(zone);
        }

        // The cab unloads: a sprung filling stroke this game's hand holds ends with it; anything else stays, a handle left in
        // emergency, or a steam engine's release, which stays where it's put.
        private void OnDisable()
        {
            if (localZone == HandleZone.Fill && Layout != null && Layout.SpringsBack && OwnsZone)
                Write(HandleZone.Travel);
            localZone = HandleZone.Travel;
        }

        /// <summary>Where on the widened travel, 0 to 1, a stock value set from outside puts the lever; a value this handle sets on itself stands.</summary>
        public float HandleValue(float value) => settingRaw ? value : Layout.HandleValue(value);

        // Let go, the handle follows the engine's added notch when another hand or a save set it, or a value from outside
        // took it out (a review, 2026-10-28: a save in holding loaded with the handle at running). Not a diesel's sprung
        // filling stroke, which its spring takes home. Tried at most twice a second, in case the lever won't move.
        private void Follow()
        {
            if (Layout == null || control == null || car == null || car.brakeSystem == null || Held || Time.time - followedAt < 0.5f)
                return;
            var zone = ZoneOf(car.brakeSystem);
            var at = Layout.Zone(Notch, 0);
            if (zone == at || (Layout.SpringsBack && (zone == HandleZone.Fill || at == HandleZone.Fill)))
                return;
            int target;
            switch (zone)
            {
                case HandleZone.Fill:
                    target = Layout.ReleaseNotch ? 0 : -1;
                    break;
                case HandleZone.Holding:
                    target = Layout.HoldingNotch ? Layout.NotchOf(0) + 1 : -1;
                    break;
                case HandleZone.Emergency:
                    target = Layout.EmergencyNotch ? Layout.Notches - 1 : -1;
                    break;
                default:
                    target = Layout.NotchFor(control.Value);
                    break;
            }
            // A handle widened before the equipment changed may lack the notch.
            if (target < 0 || target == Notch)
                return;
            // The lever reports the notch it reaches (OnNotch); one not built yet, or held, doesn't move, and is tried again.
            followedAt = Time.time;
            settingRaw = true;
            try
            {
                // Straight to the lever: the control's own setter would report the travel's value as the stock one.
                acceptSetValue(control, target / (float)(Layout.Notches - 1));
            }
            finally
            {
                settingRaw = false;
            }
        }

        // A sprung handle: times the hold past release. And the game's spring moves the target only as the lever's angle changes: one let go dead still past release would stay there.
        private void Update()
        {
            Follow();
            ReportZone();
            if (Layout == null || !Layout.SpringsBack || Notch != 0 || control == null)
            {
                heldPastReleaseSince = float.NaN;
                letGoInFillAt = float.NaN;
                return;
            }
            if (Held)
            {
                if (float.IsNaN(heldPastReleaseSince))
                    heldPastReleaseSince = Time.time;
                letGoInFillAt = float.NaN;
                return;
            }
            heldPastReleaseSince = float.NaN;
            if (float.IsNaN(letGoInFillAt))
                letGoInFillAt = Time.time;
            else if (Time.time - letGoInFillAt > SpringBackTime)
            {
                letGoInFillAt = float.NaN;
                control.SetValue(0f);
            }
        }
    }
}
