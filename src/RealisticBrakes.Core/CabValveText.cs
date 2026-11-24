using System;
using System.Globalization;

namespace RealisticBrakes.Core
{
 public static class CabValveText
    {
        /// <summary>
        /// The train brake handle's position, on a self-lapping valve if <paramref name="selfLapping"/>;
        /// when cut out, <paramref name="anotherValveCutIn"/> says whether another engine in the train works the pipe.
        /// </summary>
        public static string TrainBrake(LocomotiveBrake engine, bool selfLapping, bool anotherValveCutIn = true)
        {
            var valve = engine?.DriverValve;
            if (valve == null)
                return null;
            if (!valve.CutIn)
                return anotherValveCutIn
                    ? "Train brake: cut out (the leading engine's valve works the pipe)"
                    : "Train brake: cut out (no brake valve working the pipe — cut it in to control the train)";
            var er = Bar(valve.EqualizingReservoir.Pressure - Air.Atmosphere);
            if (valve.Spec.Matrosov394)
                return Matrosov394(valve, er);
            switch (valve.Position)
            {
                case DriverValvePosition.Fill:
                    return selfLapping
                        ? "Train brake: filling stroke (overcharging the pipe to release a long train quickly)"
                        : "Train brake: release (main reservoir air straight into the pipe; back to running before the train overcharges)";
                case DriverValvePosition.Holding:
                    return "Train brake: holding (the train releases and recharges; the engine's brakes stay on)";
                case DriverValvePosition.Lap:
                    return $"Train brake: lap (equalizing reservoir {er} bar)";
                case DriverValvePosition.LapWithoutFeed:
                    return $"Train brake: lap without feed (the pipe cut off; equalizing reservoir {er} bar)";
                case DriverValvePosition.SlowApply:
                    return $"Train brake: slow service (equalizing reservoir coming down slowly, {er} bar; lap to hold it)";
                case DriverValvePosition.Apply:
                    return $"Train brake: service (equalizing reservoir coming down, {er} bar; lap to hold it)";
                case DriverValvePosition.Service:
                    // Made from running and any overcharge the valve carries through the application.
                    var reduction = valve.Spec.Running + valve.OverchargeLeft + Air.Atmosphere - valve.EqualizingTarget;
                    return reduction >= valve.Spec.FullService - 1
                        ? $"Train brake: full service ({Bar(reduction, "0.0#")} bar reduction)"
                        : $"Train brake: service ({Bar(reduction, "0.0#")} bar reduction)";
                case DriverValvePosition.Emergency:
                    return "Train brake: emergency";
                default:
                    return "Train brake: running";
            }
        }

        // The No. 394's positions by their numbers, as its handle is marked.
        private static string Matrosov394(DriverBrakeValve valve, string er)
        {
            switch (valve.Position)
            {
                case DriverValvePosition.Fill:
                    return "Train brake: I, release and charging (main reservoir straight into the pipe; back to II before the train overcharges)";
                case DriverValvePosition.LapWithoutFeed:
                    return $"Train brake: III, lap without feed (the pipe cut off; equalizing reservoir {er} bar)";
                case DriverValvePosition.Lap:
                    return $"Train brake: IV, lap with feed (equalizing reservoir {er} bar)";
                case DriverValvePosition.SlowApply:
                    return $"Train brake: VA, slow service (equalizing reservoir coming down slowly, {er} bar; III or IV to hold it)";
                case DriverValvePosition.Apply:
                case DriverValvePosition.Service:
                    return $"Train brake: V, service (equalizing reservoir coming down, {er} bar; III or IV to hold it)";
                case DriverValvePosition.Emergency:
                    return "Train brake: VI, emergency";
                default:
                    var charging = Bar(valve.Spec.Running);
                    return valve.OverchargeLeft > 0.005 * Air.Bar
                        ? $"Train brake: II, train (charging pressure {charging} bar; the overcharge coming down slowly, equalizing reservoir {er} bar)"
                        : $"Train brake: II, train (charging pressure {charging} bar)";
            }
        }

        /// <summary>
        /// The independent brake's application, and the train brake's on the
        /// engine with what's bailed off; with how to bail, if
        /// <paramref name="clickToBail"/>, or by the S-6 handle's release notch
        /// where it has one (<paramref name="releaseNotch"/>).
        /// </summary>
        public static string Independent(LocomotiveBrake engine, bool clickToBail, bool releaseNotch = false)
        {
            if (engine == null)
                return null;
            var s6 = engine.Spec.ManualLapIndependent;
            string text;
            if (s6)
            {
                // The S-6 stands in its positions and holds what it put in until it's let out.
                var held = engine.IndependentCylinder;
                text = "Independent brake: " + (engine.HeldInRelease ? "release" : PositionName(engine.IndependentAt));
                if (held >= Shown)
                {
                    text += $", {Bar(held)} bar";
                    if (engine.IndependentAt == IndependentPosition.Running && !engine.ReleasePipeOpen)
                        text += ", held until the train brake is in running";
                }
            }
            else
            {
                // What the handle asks for, by the independent's own steps where it has them (the No. 254).
                var independent = engine.Spec.IndependentFor(engine.Independent);
                text = independent < Shown
                    ? "Independent brake: released"
                    : $"Independent brake: {Bar(independent)} bar (of {Bar(engine.Spec.IndependentMaximum)})";
            }
            var automatic = engine.AutomaticCylinder;
            var bailed = engine.AutomaticApplied ? engine.BailedOff : 0;
            var notch = s6 && releaseNotch;
            var hint = (clickToBail || notch) && !engine.HeldInRelease
                && (automatic >= Shown || (s6 && engine.IndependentCylinder >= Shown && !engine.ReleasePipeOpen));
            if (automatic >= Shown || bailed >= Shown)
            {
                text += $"; train brake on the engine {Bar(automatic)} bar";
                if (bailed >= Shown)
                    text += $", {Bar(bailed)} bailed off";
            }
            if (hint)
                text += notch ? " (hold the handle in release, below running, to bail off)"
                    : engine.Spec.HoldsAutomaticBrake ? " (click and hold to bail off)"
                    : engine.Spec.GraduatedBail ? " (click to bail off, hold to graduate)"
                    : " (click to bail off)";
            return text;
        }

        private static string PositionName(IndependentPosition position)
        {
            switch (position)
            {
                case IndependentPosition.Lap: return "lap";
                case IndependentPosition.SlowApplication: return "slow application";
                case IndependentPosition.QuickApplication: return "quick application";
                default: return "running";
            }
        }

        /// <summary>The brake valve's cut-out cock.</summary>
        public static string Cutout(DriverBrakeValve valve)
        {
            if (valve == null)
                return null;
            return valve.CutIn
                ? "Brake valve: cut in (this engine's train brake handle works the train)"
                : "Brake valve: cut out (the handle leaves the pipe alone, as on a trailing engine)";
        }

        // Less than this, gauge Pa (0.05 bar), reads as nothing.
        private const double Shown = 0.05 * Air.Bar;

        private static string Bar(double pascals, string format = "F1") =>
            (pascals / Air.Bar).ToString(format, CultureInfo.InvariantCulture);
    }
}
