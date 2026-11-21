using System;
using System.Globalization;

namespace RealisticBrakes.Core
{
 public static class VacuumCabText
    {
        // Less than this share of the engine's full brake reads as nothing: 0.05 bar of a diesel's 3.8.
        private const double Shown = 0.013;

        // The bail is offered from this share: a self-lapping valve's small wander after a bail is nothing to bail.
        private const double WorthBailing = 0.05;

 public static string TrainBrake(VacuumEngine engine, double pipe, bool anotherValveCutIn = true)
        {
            if (engine == null)
                return null;
            if (!engine.CutIn)
                return anotherValveCutIn
                    ? "Train brake: cut out (the leading engine's valve works the pipe)"
                    : "Train brake: cut out (no brake valve working the pipe — cut it in to control the train)";
            var inches = Inches(pipe);
            var steam = engine.Spec.SteamBrake;
            switch (engine.Position)
            {
                case VacuumValvePosition.Release:
                    return steam
                        ? $"Train brake: release ({(engine.PumpOn ? "both ejectors" : "large ejector")} on the pipe, {inches} in)"
                        : $"Train brake: filling (exhausters at full speed, {inches} in)";
                case VacuumValvePosition.Lap:
                    return $"Train brake: lap (pipe cut off at {inches} in; leaks creep the brake on)";
                case VacuumValvePosition.Apply:
                    return $"Train brake: applying (air into the pipe, {inches} in; lap to hold it)";
                case VacuumValvePosition.Service:
                    return $"Train brake: service (holding {Inches(engine.Target)} in, the pipe at {inches} in)";
                case VacuumValvePosition.Emergency:
                    return $"Train brake: emergency (pipe open wide, {inches} in)";
                default:
                    if (!steam)
                        return engine.PumpOn
                            ? $"Train brake: running (exhausters on the pipe, {inches} in)"
                            : $"Train brake: running (exhausters stopped with the engine, {inches} in)";
                    return engine.PumpOn
                        ? $"Train brake: running (small ejector on the pipe, {inches} in)"
                        : $"Train brake: running (the small ejector's steam valve is shut, {inches} in)";
            }
        }

        /// <summary>The independent's application, and the train brake's on the engine with what's bailed off; with how to bail, if <paramref name="clickToBail"/>, on an engine that can.</summary>
        public static string Independent(VacuumEngine engine, bool clickToBail)
        {
            if (engine == null)
                return null;
            var independent = Math.Max(0, Math.Min(1, engine.Independent));
            var steam = engine.Spec.SteamBrake;
            var text = independent < Shown
                ? "Independent brake: released"
                : steam
                    ? $"Independent brake: {Amount(engine, independent)} of steam (of {Amount(engine, 1)})"
                    : $"Independent brake: {Amount(engine, independent)} (of {Amount(engine, 1)})";
            var automatic = Math.Max(0, engine.AutomaticShare - engine.BailedOff);
            if (automatic >= Shown || engine.BailedOff >= Shown)
            {
                text += $"; train brake on the engine {Amount(engine, automatic)}";
                if (engine.BailedOff >= Shown)
                    text += $", {Amount(engine, engine.BailedOff)} bailed off";
            }
            if (clickToBail && engine.CanBailOff && automatic >= WorthBailing)
                text += " (click to bail off)";
            return text;
        }

        /// <summary>The brake valve's cut-out cock.</summary>
        public static string Cutout(VacuumEngine engine)
        {
            if (engine == null)
                return null;
            return engine.CutIn
                ? "Brake valve: cut in (this engine's train brake handle works the train)"
                : "Brake valve: cut out (the handle leaves the pipe alone, as on a trailing engine)";
        }

 public static string EjectorValve(VacuumEngine engine)
        {
            if (engine == null || !engine.Spec.SteamBrake)
                return null;
            if (!engine.PumpOn)
                return "Small ejector steam valve: shut (only the handle's release position draws vacuum)";
            return engine.Supply < ShortOfSteam
                ? "Small ejector steam valve: open, short of boiler pressure"
                : "Small ejector steam valve: open";
        }

        private const double ShortOfSteam = 0.5;

        private static string Inches(double pascals) =>
            Math.Max(0, Vacuum.ToInches(pascals)).ToString("F1", CultureInfo.InvariantCulture);

        private static string Amount(VacuumEngine engine, double share) => engine.Spec.SteamBrake
            ? $"{(share * engine.Spec.SteamBrakeMaximum / 6894.757).ToString("F0", CultureInfo.InvariantCulture)} psi"
            : $"{(share * engine.Spec.AirBrakeMaximum / Air.Bar).ToString("F1", CultureInfo.InvariantCulture)} bar";
    }
}
