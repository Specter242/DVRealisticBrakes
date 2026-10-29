using System;

namespace RealisticBrakes.Core
{
 public static class CabValveText
    {
        /// <summary>The train brake handle's position, on a self-lapping valve if <paramref name="selfLapping"/>.</summary>
        public static string TrainBrake(LocomotiveBrake engine, bool selfLapping)
        {
            var valve = engine?.DriverValve;
            if (valve == null)
                return null;
            if (!valve.CutIn)
                return "Train brake: cut out (the leading engine's valve works the pipe)";
            var er = Bar(valve.EqualizingReservoir.Pressure - Air.Atmosphere);
            switch (valve.Position)
            {
                case DriverValvePosition.Fill:
                    return selfLapping
                        ? "Train brake: filling stroke (overcharging the pipe to release a long train quickly)"
                        : "Train brake: release (main reservoir air straight into the pipe; back to running before the train overcharges)";
                case DriverValvePosition.Holding:
                    return "Train brake: holding (the train releases and recharges; the engine's brakes stay on)";
                case DriverValvePosition.Lap:
                    return $"Train brake: lap (equalizing reservoir {er:F1} bar)";
                case DriverValvePosition.Apply:
                    return $"Train brake: service (equalizing reservoir coming down, {er:F1} bar; lap to hold it)";
                case DriverValvePosition.Service:
                    var reduction = valve.Spec.Running + Air.Atmosphere - valve.EqualizingTarget;
                    return reduction >= valve.Spec.FullService - 1
                        ? $"Train brake: full service ({Bar(reduction):0.0#} bar reduction)"
                        : $"Train brake: service ({Bar(reduction):0.0#} bar reduction)";
                case DriverValvePosition.Emergency:
                    return "Train brake: emergency";
                default:
                    return "Train brake: running";
            }
        }

        /// <summary>
        /// The independent brake's application, and the train brake's on the
        /// engine with what's bailed off; with how to bail, if
        /// <paramref name="clickToBail"/>.
        /// </summary>
        public static string Independent(LocomotiveBrake engine, bool clickToBail)
        {
            if (engine == null)
                return null;
            var independent = engine.Spec.IndependentMaximum * Math.Max(0, Math.Min(1, engine.Independent));
            var text = independent < Shown
                ? "Independent brake: released"
                : $"Independent brake: {Bar(independent):F1} bar (of {Bar(engine.Spec.IndependentMaximum):F1})";
            var automatic = engine.AutomaticCylinder;
            var bailed = engine.AutomaticApplied ? engine.BailedOff : 0;
            if (automatic < Shown && bailed < Shown)
                return text;
            text += $"; train brake on the engine {Bar(automatic):F1} bar";
            if (bailed >= Shown)
                text += $", {Bar(bailed):F1} bailed off";
            if (clickToBail && automatic >= Shown)
                text += engine.Spec.HoldsAutomaticBrake ? " (click and hold to bail off)" : " (click to bail off)";
            return text;
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

        private static double Bar(double pascals) => pascals / Air.Bar;
    }
}
