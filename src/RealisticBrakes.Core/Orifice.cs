using System;

namespace RealisticBrakes.Core
{
    /// <summary>
    /// A choke or a valve's port: a sharp-edged hole whose flow follows ISO
    /// 6358, as Pugi et al. (2004) model the brake's chokes. Flow is choked
    /// (sonic) while the downstream pressure is under the critical ratio of
    /// the upstream, then falls along an ellipse to nothing as the pressures
    /// meet. Sized by its bore and a discharge coefficient, as brake chokes
    /// are specified.
    /// </summary>
    public readonly struct Orifice
    {
        /// <summary>The critical pressure ratio of an ideal nozzle in air (γ = 1.4).</summary>
        public const double CriticalRatio = 0.528;

        /// <summary>A sharp-edged hole's discharge coefficient.</summary>
        public const double SharpEdged = 0.8;

        /// <summary>
        /// Choked mass flux, kg/s per m² of effective area per Pa upstream:
        /// √(γ/RT)·(2/(γ+1))^((γ+1)/(2(γ−1))) for air at <see cref="Air.Temperature"/>.
        /// </summary>
        public static readonly double ChokedFlux = Math.Sqrt(1.4 / Air.RT) * Math.Pow(2.0 / 2.4, 2.4 / 0.8);

        /// <summary>A hole of <paramref name="boreMillimeters"/> bore.</summary>
        public Orifice(double boreMillimeters, double discharge = SharpEdged)
        {
            if (boreMillimeters < 0 || discharge < 0)
                throw new ArgumentOutOfRangeException(nameof(boreMillimeters), "An orifice's bore and discharge coefficient can't be negative.");
            var bore = boreMillimeters / 1000.0;
            EffectiveArea = discharge * Math.PI * bore * bore / 4.0;
        }

        private Orifice(double effectiveArea, bool _) => EffectiveArea = effectiveArea;

        /// <summary>The flow area times the discharge coefficient, m².</summary>
        public double EffectiveArea { get; }

        /// <summary>Whether air can pass at all.</summary>
        public bool IsOpen => EffectiveArea > 0;

        /// <summary>This orifice opened to <paramref name="fraction"/> of its area, as a valve opening partway.</summary>
        public Orifice Opened(double fraction) => new Orifice(EffectiveArea * Math.Max(0, Math.Min(1, fraction)), true);

        /// <summary>This orifice with its area times <paramref name="factor"/>.</summary>
        public Orifice Scaled(double factor) => new Orifice(EffectiveArea * Math.Max(0, factor), true);

        /// <summary>
        /// The mass flow, kg/s, from the side at <paramref name="from"/> Pa to
        /// the side at <paramref name="to"/> Pa: negative when it runs back.
        /// </summary>
        public double MassFlow(double from, double to)
        {
            if (EffectiveArea <= 0 || from == to)
                return 0;
            var sign = from > to ? 1.0 : -1.0;
            var up = Math.Max(from, to);
            var down = Math.Min(from, to);
            if (up <= 0)
                return 0;
            var ratio = down / up;
            var flowFunction = 1.0;
            if (ratio > CriticalRatio)
            {
                var x = (ratio - CriticalRatio) / (1 - CriticalRatio);
                flowFunction = Math.Sqrt(Math.Max(0, 1 - x * x));
            }
            return sign * ChokedFlux * EffectiveArea * up * flowFunction;
        }

        /// <summary>
        /// Moves air through this orifice from <paramref name="from"/> to
        /// <paramref name="to"/> (null: the atmosphere) for
        /// <paramref name="dt"/> s, never past level. Returns the mass moved,
        /// kg: negative when it ran back.
        /// </summary>
        public double Pass(Chamber from, Chamber to, double dt)
        {
            var moved = MassFlow(from.Pressure, to?.Pressure ?? Air.Atmosphere) * dt;
            moved = Flows.LimitTo(moved, from.MassToEqualize(to));
            Flows.Move(from, to, moved);
            return moved;
        }

        /// <summary>
        /// Moves air through this orifice from <paramref name="from"/> into
        /// <paramref name="to"/> for <paramref name="dt"/> s, only that way
        /// (a check valve), and no further than <paramref name="toCeiling"/>
        /// Pa in <paramref name="to"/>. Returns the mass moved, kg.
        /// </summary>
        public double PassOneWay(Chamber from, Chamber to, double dt, double toCeiling = double.PositiveInfinity)
        {
            var moved = MassFlow(from.Pressure, to.Pressure) * dt;
            if (moved <= 0)
                return 0;
            moved = Math.Min(moved, from.MassToEqualize(to));
            if (!double.IsPositiveInfinity(toCeiling))
                moved = Math.Min(moved, Math.Max(0, to.MassToReach(toCeiling)));
            Flows.Move(from, to, moved);
            return moved;
        }

        /// <summary>
        /// Vents <paramref name="chamber"/> through this orifice to the
        /// atmosphere for <paramref name="dt"/> s, no lower than
        /// <paramref name="floor"/> Pa. Returns the mass let out, kg.
        /// </summary>
        public double Vent(Chamber chamber, double dt, double floor = Air.Atmosphere)
        {
            var moved = MassFlow(chamber.Pressure, Air.Atmosphere) * dt;
            if (moved <= 0)
                return 0;
            moved = Math.Min(moved, Math.Max(0, -chamber.MassToReach(Math.Max(floor, Air.Atmosphere))));
            chamber.Mass -= moved;
            return moved;
        }

        public override string ToString() => $"{Math.Sqrt(EffectiveArea / SharpEdged * 4 / Math.PI) * 1000:F2} mm";
    }

    /// <summary>Bookkeeping shared by the flow elements.</summary>
    internal static class Flows
    {
        /// <summary><paramref name="moved"/>, no further than <paramref name="level"/> in its direction.</summary>
        internal static double LimitTo(double moved, double level)
        {
            if (moved > 0)
                return Math.Min(moved, Math.Max(0, level));
            if (moved < 0)
                return Math.Max(moved, Math.Min(0, level));
            return 0;
        }

        /// <summary>Takes <paramref name="mass"/> from <paramref name="from"/> into <paramref name="to"/> (null: the atmosphere).</summary>
        internal static void Move(Chamber from, Chamber to, double mass)
        {
            from.Mass -= mass;
            if (to != null)
                to.Mass += mass;
        }
    }
}
