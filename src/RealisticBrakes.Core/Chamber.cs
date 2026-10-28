using System;

namespace RealisticBrakes.Core
{
    /// <summary>
    /// A fixed volume of air at one pressure: a reservoir, a cylinder, a
    /// valve's chamber, or one car's length of brake pipe. Its mass is the
    /// state. Its pressure follows from the mass, as the air is isothermal
    /// (<see cref="Air"/>).
    /// </summary>
    public sealed class Chamber
    {
        public Chamber(double volume, double pressure = Air.Atmosphere)
        {
            if (!(volume > 0))
                throw new ArgumentOutOfRangeException(nameof(volume), volume, "A chamber needs a positive volume.");
            Volume = volume;
            Pressure = pressure;
        }

        /// <summary>The volume, m³.</summary>
        public double Volume { get; private set; }

        /// <summary>
        /// Changes the volume, keeping the pressure: an engine's main
        /// reservoir resized after it was built (Realistic Firing fits its
        /// prototype's).
        /// </summary>
        public void Resize(double volume)
        {
            if (!(volume > 0))
                throw new ArgumentOutOfRangeException(nameof(volume), volume, "A chamber needs a positive volume.");
            var pressure = Pressure;
            Volume = volume;
            Pressure = pressure;
        }

        /// <summary>The air held, kg.</summary>
        public double Mass { get; set; }

        /// <summary>The pressure, Pa absolute.</summary>
        public double Pressure
        {
            get => Air.Pressure(Mass, Volume);
            set => Mass = Air.Mass(value, Volume);
        }

        /// <summary>The pressure, gauge bar.</summary>
        public double GaugeBar => Air.ToGaugeBar(Pressure);

        /// <summary>
        /// The mass, kg, that would have to move from this chamber to
        /// <paramref name="other"/> to bring them level: negative when it
        /// would move the other way. A null <paramref name="other"/> is the
        /// atmosphere, which no flow changes.
        /// </summary>
        public double MassToEqualize(Chamber other)
        {
            if (other == null)
                return (Pressure - Air.Atmosphere) * Volume / Air.RT;
            return (Pressure - other.Pressure) * Volume * other.Volume / ((Volume + other.Volume) * Air.RT);
        }

        /// <summary>The mass, kg, to add to bring this chamber to <paramref name="pressure"/>: negative to take away.</summary>
        public double MassToReach(double pressure) => (pressure - Pressure) * Volume / Air.RT;

        public override string ToString() => $"{GaugeBar:F3} bar in {Volume * 1000:F1} L";
    }
}
