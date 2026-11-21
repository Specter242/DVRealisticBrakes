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
        private double mass;

        public Chamber(double volume, double pressure = Air.Atmosphere)
        {
            if (!(volume > 0) || double.IsInfinity(volume))
                throw new ArgumentOutOfRangeException(nameof(volume), volume, "A chamber needs a positive volume.");
            var target = IsFinite(pressure) ? (pressure > 0 ? pressure : 0) : Air.Atmosphere;
            var nextMass = Air.Mass(target, volume);
            if (!IsFinite(nextMass) || !IsFinite(Air.Pressure(nextMass, volume)))
            {
                nextMass = Air.Mass(Air.Atmosphere, volume);
                if (!IsFinite(nextMass) || !IsFinite(Air.Pressure(nextMass, volume)))
                    nextMass = 0;
            }
            Volume = volume;
            mass = nextMass;
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
            if (!(volume > 0) || double.IsInfinity(volume))
                throw new ArgumentOutOfRangeException(nameof(volume), volume, "A chamber needs a positive volume.");
            var pressure = Pressure;
            var nextMass = Air.Mass(pressure, volume);
            if (!IsFinite(nextMass) || !IsFinite(Air.Pressure(nextMass, volume)))
                return;
            Volume = volume;
            mass = nextMass > 0 ? nextMass : 0;
        }

        /// <summary>
        /// Changes the volume, keeping the air: a piston moving in its
        /// cylinder compresses one side and expands the other.
        /// </summary>
        public void Displace(double volume)
        {
            if (!(volume > 0) || double.IsInfinity(volume))
                throw new ArgumentOutOfRangeException(nameof(volume), volume, "A chamber needs a positive volume.");
            if (!IsFinite(Air.Pressure(mass, volume)))
                return;
            Volume = volume;
        }

        /// <summary>The air held, kg.</summary>
        public double Mass
        {
            get => mass;
            set
            {
                if (!IsFinite(value))
                    return;
                var clamped = value > 0 ? value : 0;
                if (IsFinite(Air.Pressure(clamped, Volume)))
                    mass = clamped;
            }
        }

        /// <summary>The pressure, Pa absolute.</summary>
        public double Pressure
        {
            get => Air.Pressure(mass, Volume);
            set
            {
                if (!IsFinite(value))
                    return;
                var next = Air.Mass(value, Volume);
                if (IsFinite(next))
                {
                    var clamped = next > 0 ? next : 0;
                    if (IsFinite(Air.Pressure(clamped, Volume)))
                        mass = clamped;
                }
            }
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

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
