using System;

namespace RealisticBrakes.Core
{
 public static class AxisNotches
    {
        /// <summary>How far past halfway to the next notch the axis must go to reach it, in notches.</summary>
        public const double Deadband = 0.2;

        /// <summary>The least deadband in the axis's own travel, 0 to 1: a handle of many notches still rides out an axis's noise.</summary>
        public const double MinimumTravel = 0.01;

        /// <summary>
        /// The notch, of <paramref name="notches"/> spread evenly over the
        /// axis's 0 to 1, that the axis at <paramref name="axis"/> takes the
        /// handle to from <paramref name="current"/>: the nearest, unless
        /// that's another notch the axis hasn't gone a deadband past halfway
        /// to. A <paramref name="current"/> under 0 (no notch yet, as the axis
        /// takes the handle over) takes the nearest.
        /// </summary>
        public static int Snap(double axis, int notches, int current)
        {
            if (notches < 2)
                return 0;
            var last = notches - 1;
            var position = (double.IsNaN(axis) ? 0 : Math.Max(0, Math.Min(1, axis))) * last;
            var nearest = (int)Math.Round(position, MidpointRounding.AwayFromZero);
            if (current < 0 || current > last)
                return nearest;
            var deadband = Math.Max(Deadband, MinimumTravel * last);
            return Math.Abs(position - current) > 0.5 + deadband ? nearest : current;
        }

        /// <summary>Notch <paramref name="notch"/> of <paramref name="notches"/> on the 0 to 1 scale.</summary>
        public static float Value(int notch, int notches) => notches < 2 ? 0f : Math.Max(0, Math.Min(notch, notches - 1)) / (float)(notches - 1);
    }
}
