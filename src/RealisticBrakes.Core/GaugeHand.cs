using System;

namespace RealisticBrakes.Core
{
 public static class GaugeHand
    {
        /// <summary>The gap between the dial's hand and the added one, m.</summary>
        public const double Gap = 0.0015;

        /// <summary>
        /// <see cref="Gap"/> in the units of a frame one unit of which, along the hand's axis, is
        /// <paramref name="metresPerUnit"/> m in the cab; none for a frame of no size or of no finite one.
        /// </summary>
        public static double LocalGap(double metresPerUnit) =>
            metresPerUnit > 0 && !double.IsInfinity(metresPerUnit) ? Gap / metresPerUnit : 0;
    }
}
