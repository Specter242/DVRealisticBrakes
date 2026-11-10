namespace RealisticBrakes.Core
{
 public static class FreshCut
    {
        /// <summary>What to do with a fresh cut this frame.</summary>
        public enum Step
        {
            /// <summary>Keep it frozen and watched: its couplings may still be forming, or it hasn't come to a stand yet.</summary>
            Wait,

            /// <summary>Weigh it now and, if its brakes can't hold it, set its hand brakes.</summary>
            Weigh,

            /// <summary>Let it go unweighed: a driver runs it, or it never came to a stand.</summary>
            Drop,
        }

        /// <summary>A cut is weighed this long after its newest car appeared, s, once its couplings have formed.</summary>
        public const double WeighAfter = 3;

        /// <summary>
        /// A cut still moving this long after its newest car appeared, s, is
        /// let go: something is moving it, a player's engine or a shunt.
        /// </summary>
        public const double GiveUpAfter = 30;

        /// <summary>
        /// The step for a fresh cut <paramref name="age"/> s after its newest
        /// car appeared. It used to be weighed once, at 3 s, and forgotten if
        /// it wasn't standing just then: a cut still settling, its couplers
        /// taking up slack on a grade (as ZCouplers' delayed coupling can),
        /// was never secured, and lost its freeze (the in-game check of
        /// 2026-11-03). Now it's watched until it stands, for half a minute.
        /// </summary>
        public static Step Next(double age, bool standing, bool driven)
        {
            if (driven)
                return Step.Drop;
            if (age < WeighAfter)
                return Step.Wait;
            if (standing)
                return Step.Weigh;
            return age >= GiveUpAfter ? Step.Drop : Step.Wait;
        }

        /// <summary>
        /// The fastest a car of a fresh cut may move, m/s, and the cut still
        /// be held as it settles (<see cref="Settling"/>): twice the 0.49 m/s
        /// the fastest car read in the jolt, held (job 1703).
        /// </summary>
        public const double SettlingSpeed = 1.0;

        /// <summary>
        /// Whether a fresh cut that isn't standing is held all the same, as
        /// one still settling. The game jolts a cut about three quarters of a
        /// second after it appears, its cars up to 0.3 m/s against each other;
        /// that is over the speed a cut counts as standing under, so the
        /// freeze let go, and on a grade the cut was caught again only if
        /// every car happened to be slow at one moment. Of 20 cuts of six
        /// spawned on 1.2 %, five rolled away unsecured (the in-game checks
        /// of 2026-11-09, jobs 1687, 1697, and 1703). Not a cut with an engine, which
        /// its driver may be starting, nor one another mod's driver runs.
        /// </summary>
        public static bool Settling(double fastest, bool hasEngine, bool driven) =>
            !hasEngine && !driven && fastest <= SettlingSpeed;

        /// <summary>Whether to set the hand brakes of a fresh cut: standing, driven by no other mod's driver, and not held by its brakes.</summary>
        public static bool Secures(bool standing, bool driven, bool holds) => standing && !driven && !holds;

        /// <summary>
        /// Whether to take off a car's hand brake that was set here: a driver
        /// now runs its cut, and the hand brake is still on as it was left.
        /// One anyone has moved since is theirs.
        /// </summary>
        public static bool Frees(bool driven, double handBrake) => driven && handBrake >= FullOn;

        /// <summary>A hand brake as it was set here, all the way on.</summary>
        public const double FullOn = 0.999;
    }
}
