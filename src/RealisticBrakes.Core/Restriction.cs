using System;

namespace RealisticBrakes.Core
{
    /// <summary>
    /// A fine choke whose flow is laminar and so in proportion to the
    /// pressure difference: a distributor's control-reservoir charging choke.
    /// Unlike an <see cref="Orifice"/>, its flow stays gentle as the
    /// pressures meet. That makes a chamber behind it follow slow changes
    /// with a lag and ignore them, while quick ones outrun it.
    /// </summary>
    public readonly struct Restriction
    {
        /// <summary>A restriction passing <paramref name="conductance"/> kg/s per Pa.</summary>
        public Restriction(double conductance)
        {
            if (conductance < 0)
                throw new ArgumentOutOfRangeException(nameof(conductance), conductance, "A restriction's conductance can't be negative.");
            Conductance = conductance;
        }

        /// <summary>Mass flow per pressure difference, kg/(s·Pa).</summary>
        public double Conductance { get; }

        /// <summary>
        /// The restriction through which a chamber of <paramref name="volume"/>
        /// m³ follows a steady pressure with the time constant
        /// <paramref name="seconds"/>.
        /// </summary>
        public static Restriction WithTimeConstant(double volume, double seconds) =>
            new Restriction(volume / (Air.RT * seconds));

        /// <summary>
        /// Moves air through this restriction between <paramref name="a"/>
        /// and <paramref name="b"/> for <paramref name="dt"/> s, never past
        /// level. Returns the mass moved from a to b, kg.
        /// </summary>
        public double Pass(Chamber a, Chamber b, double dt) => Pass(a, b, dt, double.PositiveInfinity);

        /// <summary>
        /// Moves air between <paramref name="a"/> and <paramref name="b"/> as
        /// <see cref="Pass(Chamber, Chamber, double)"/> does, but never fills
        /// b past <paramref name="ceiling"/> Pa; it can still empty back into
        /// a. Returns the mass moved from a to b, kg.
        /// </summary>
        public double Pass(Chamber a, Chamber b, double dt, double ceiling)
        {
            var moved = Conductance * (a.Pressure - b.Pressure) * dt;
            moved = Flows.LimitTo(moved, a.MassToEqualize(b));
            if (moved > 0)
                moved = Math.Min(moved, Math.Max(0, b.MassToReach(ceiling)));
            Flows.Move(a, b, moved);
            return moved;
        }
    }
}
