namespace RealisticBrakes.Core
{
 public static class FreshCut
    {
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
