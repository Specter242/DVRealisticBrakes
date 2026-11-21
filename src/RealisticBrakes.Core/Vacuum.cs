using System;

namespace RealisticBrakes.Core
{
 public static class Vacuum
    {
        /// <summary>Pascals in an inch of mercury.</summary>
        public const double Inch = 3386.389;

 public const double Working = 21;

 public static double FromInches(double inches) => Air.Atmosphere - inches * Inch;

 public static double ToInches(double pascals) => (Air.Atmosphere - pascals) / Inch;

        /// <summary>The charged air pipe that stands for <see cref="Working"/>: 5 bar gauge, the game's.</summary>
        private const double ChargedAir = 5 * Air.Bar;

 public static double AirEquivalent(double pascals) => Air.Atmosphere + ChargedAir * Math.Max(0, ToInches(pascals)) / Working;

 public static double FromAirEquivalent(double airPascals) =>
            FromInches(Math.Max(0, Math.Min(Working * 1.4, (airPascals - Air.Atmosphere) / ChargedAir * Working)));

 public static double ReadInches(CarBrake brake, string part)
        {
            if (brake == null || string.IsNullOrEmpty(part))
                return double.NaN;
            if (string.Equals(part, "BrakePipe", StringComparison.OrdinalIgnoreCase))
                return ToInches(brake.PipeSection.Pressure);
            var cylinder = brake.VacuumCylinder;
            if (cylinder == null)
                return double.NaN;
            if (string.Equals(part, "VacuumChamber", StringComparison.OrdinalIgnoreCase)
                || string.Equals(part, "Chamber", StringComparison.OrdinalIgnoreCase)
                || string.Equals(part, "AuxiliaryReservoir", StringComparison.OrdinalIgnoreCase))
                return ToInches(cylinder.Chamber.Pressure);
            if (string.Equals(part, "Underside", StringComparison.OrdinalIgnoreCase))
                return ToInches(cylinder.Underside.Pressure);
            if (string.Equals(part, "BrakeCylinder", StringComparison.OrdinalIgnoreCase))
                return Math.Max(0, ToInches(cylinder.Chamber.Pressure) - ToInches(cylinder.Underside.Pressure));
            return double.NaN;
        }
    }
}
