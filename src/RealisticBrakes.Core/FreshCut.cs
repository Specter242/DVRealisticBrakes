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
