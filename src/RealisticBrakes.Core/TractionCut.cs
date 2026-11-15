namespace RealisticBrakes.Core
{
 public enum TractionCutStyle
    {
        /// <summary>None: steam engines, and engines another mod drives.</summary>
        None,

        /// <summary>
        /// European: a pressure switch on the brake pipe cuts power while the
        /// pipe at the engine stands under <see cref="TractionCut.PipeCutBelow"/>.
        /// </summary>
        BrakePipe,

        /// <summary>
        /// American: the PC (pneumatic control) switch opens on an emergency
        /// application and stays open until it is reset.
        /// </summary>
        PcSwitch,
    }

    /// <summary>
    /// One engine's traction cut: whether its power is cut now, from the brake
    /// pipe at the engine, its brake valve's handle, and its throttle. Power
    /// comes back only once the pipe has recovered and the throttle is closed.
    /// </summary>
    public sealed class TractionCut
    {
        /// <summary>
        /// The European pressure switch's trip, Pa gauge: under 3.5 bar (cj187,
        /// a German driver, 2026-11-14: "TM should be taken down when 5bar pipe
        /// goes below 3.5 bar"), less 0.05 bar so a full service, which the
        /// brake valve holds at 3.5 bar, never trips it. Under 3.4 bar is the
        /// emergency range (a rail braking course on the Saxon education portal: "0,0…3,4 bar Schnellbremsung").
        /// </summary>
        public const double PipeCutBelow = 3.45 * Air.Bar;

        /// <summary>
        /// The American PC switch's trip, Pa gauge: the pipe at the engine under
        /// 2.0 bar, deeper than any service reduction (full service leaves about
        /// 3.5 bar), so an emergency from anywhere in the train, or another mod's
        /// penalty vent, opens it as the real switch's emergency piping does
        /// (inferred). The brake valve's emergency position opens it at once.
        /// </summary>
        public const double PcSwitchTripBelow = 2.0 * Air.Bar;

        /// <summary>
        /// The pipe at the engine back over this, Pa gauge, counts as recovered
        /// (provisional): a release well under way toward 5 bar.
        /// </summary>
        public const double RecoveredAbove = 4.5 * Air.Bar;

        /// <summary>A throttle at or under this counts as closed.</summary>
        public const double ThrottleClosed = 0.001;

        /// <summary>
        /// A cut not updated for this long, s, no longer holds: the model has
        /// stopped running the engine's brake (another mod's driver took the
        /// train, as AI Traffic's does, or the game's own brake runs it), so
        /// there is no pipe it can watch. UserPaddingFTW, 2026-11-15: "With
        /// the release of v1.4.2, AI traffic trains become stuck on spawn."
        /// </summary>
        public const double StaleAfter = 0.5;

        public TractionCut(TractionCutStyle style)
        {
            Style = style;
        }

        public TractionCutStyle Style { get; }

        /// <summary>Whether the engine's power is cut.</summary>
        public bool Cut { get; private set; }

        /// <summary>
        /// Updates the cut from the pipe at the engine (Pa gauge), whether the
        /// brake valve's handle stands in emergency, and the throttle the driver
        /// asks for (0 to 1). Returns whether it changed.
        /// </summary>
        public bool Update(double pipeGauge, bool handleInEmergency, double throttle)
        {
            var was = Cut;
            switch (Style)
            {
                case TractionCutStyle.BrakePipe:
                    if (pipeGauge < PipeCutBelow)
                        Cut = true;
                    else if (Cut && pipeGauge > RecoveredAbove && throttle <= ThrottleClosed)
                        Cut = false;
                    break;
                case TractionCutStyle.PcSwitch:
                    if (handleInEmergency || pipeGauge < PcSwitchTripBelow)
                        Cut = true;
                    else if (Cut && pipeGauge > RecoveredAbove && throttle <= ThrottleClosed)
                        Cut = false;
                    break;
                default:
                    Cut = false;
                    break;
            }
            return Cut != was;
        }

        /// <summary>
        /// Whether the cut holds the throttle, <paramref name="sinceUpdate"/> s
        /// after it was last updated: only while the model still runs it.
        /// </summary>
        public bool Holds(double sinceUpdate) => Cut && sinceUpdate <= StaleAfter;

        /// <summary>Clears the cut, as the setting is turned off or the engine is left to another mod.</summary>
        public void Clear() => Cut = false;
    }
}
