using System;

namespace RealisticBrakes.Core
{
    /// <summary>
    /// Air as the brake model treats it: an ideal gas held at 20 °C. Pugi et
    /// al. (2004) found isothermal flow the best fit for brake pipes, as the
    /// pipe's walls exchange heat with the air faster than the flows change.
    /// Pressures are absolute pascals throughout. The atmosphere is taken as
    /// exactly 1 bar, as the game's brake takes it (its
    /// <c>ATMOSPHERE_PRESSURE</c> is 1), so gauge bar is absolute bar less 1
    /// everywhere.
    /// </summary>
    public static class Air
    {
        /// <summary>Specific gas constant of dry air, J/(kg·K).</summary>
        public const double GasConstant = 287.05;

        /// <summary>The air's temperature, K (20 °C).</summary>
        public const double Temperature = 293.15;

        /// <summary>R·T, J/kg: pressure over density.</summary>
        public const double RT = GasConstant * Temperature;

        /// <summary>The atmosphere, Pa absolute: 1 bar, as the game's brake has it.</summary>
        public const double Atmosphere = 1e5;

        /// <summary>Dynamic viscosity of air at 20 °C, Pa·s.</summary>
        public const double Viscosity = 1.81e-5;

        /// <summary>Pascals in a bar.</summary>
        public const double Bar = 1e5;

        /// <summary>The isothermal speed of sound, m/s: how fast a pressure change runs down a frictionless pipe.</summary>
        public static readonly double SoundSpeed = Math.Sqrt(RT);

        /// <summary>The mass of air, kg, at <paramref name="pressure"/> Pa in <paramref name="volume"/> m³.</summary>
        public static double Mass(double pressure, double volume) => pressure * volume / RT;

        /// <summary>The pressure, Pa, of <paramref name="mass"/> kg of air in <paramref name="volume"/> m³.</summary>
        public static double Pressure(double mass, double volume) => mass * RT / volume;

        /// <summary>The density, kg/m³, of air at <paramref name="pressure"/> Pa.</summary>
        public static double Density(double pressure) => pressure / RT;

        /// <summary>Absolute pascals from gauge bar.</summary>
        public static double FromGaugeBar(double bar) => Atmosphere + bar * Bar;

        /// <summary>Gauge bar from absolute pascals.</summary>
        public static double ToGaugeBar(double pascals) => (pascals - Atmosphere) / Bar;

        /// <summary>Absolute pascals from absolute bar, the game's brake unit.</summary>
        public static double FromAbsoluteBar(double bar) => bar * Bar;

        /// <summary>Absolute bar, the game's brake unit, from absolute pascals.</summary>
        public static double ToAbsoluteBar(double pascals) => pascals / Bar;
    }
}
