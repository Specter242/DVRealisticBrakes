using System;
using System.Collections.Generic;

namespace RealisticBrakes.Core
{
    /// <summary>
    /// The size of a train's brake pipe. UIC freight stock runs a 1¼-inch
    /// pipe (Melzi and Grasso, 2018), 35.9 mm bore as DIN 2440 medium tube.
    /// The hoses, couplings, angle cocks, and bends between cars resist the
    /// flow more than the bare pipe does. Melzi and Grasso matched their
    /// 400–1500 m test bench with an effective diameter of 70 % of the
    /// pipe's, and this model uses the same figure for friction.
    /// </summary>
    public sealed class PipeGeometry
    {
        /// <summary>The pipe's bore, m: its volume and the air's inertia go by this.</summary>
        public double Bore { get; set; } = 0.0359;

        /// <summary>The bore, m, that gives the pipe's friction with its fittings.</summary>
        public double FrictionBore { get; set; } = 0.7 * 0.0359;

        /// <summary>One car's pair of hose halves and its angle cocks, m³, beyond its pipe.</summary>
        public double HoseVolume { get; set; } = 0.0006;

        /// <summary>The hole an open angle cock at an unconnected end vents through.</summary>
        public Orifice OpenEnd { get; set; } = new Orifice(25);

        /// <summary>
        /// The small vent a closed European end cock opens on its hose side
        /// (EN 14601: "a small vent hole"), through which an open cock coupled
        /// to that hose bleeds the pipe. Its size is inferred: enough to empty
        /// a hose in a few seconds, and slow enough that closing the two cocks
        /// one after the other doesn't apply the brakes, even on a single car
        /// with two seconds between them.
        /// </summary>
        public Orifice ClosedCockVent { get; set; } = new Orifice(1.0);


        /// <summary>The pipe's cross-section, m².</summary>
        public double Area => Math.PI * Bore * Bore / 4;

        /// <summary>The volume of a car's length of pipe with its hoses, m³.</summary>
        public double SectionVolume(double carLength) => Area * carLength + HoseVolume;

        public static PipeGeometry Uic() => new PipeGeometry();

 public static PipeGeometry VacuumTrainPipe()
        {
            const double bore = 2 * 0.0254;
            return new PipeGeometry
            {
                Bore = bore,
                FrictionBore = 0.8 * bore,
                HoseVolume = 2 * 0.635 * Math.PI * bore * bore / 4,
                OpenEnd = new Orifice(bore * 1000),
            };
        }
    }

    /// <summary>
    /// A train's brake pipe as one section per car, joined through the hoses
    /// between them. It is the one-dimensional, isothermal duct of Pugi et
    /// al. (2004): each section is a lumped volume, and the air between two
    /// sections' middles has inertia and Darcy–Weisbach friction (laminar,
    /// then Blasius). So a pressure change runs down the train at about the
    /// speed of sound and is smeared by friction on the way: the brakes at
    /// the rear apply later and slower than at the front, and recharging a
    /// long train is held back by the pipe as well as by the feed.
    /// </summary>
    public sealed class BrakePipe
    {
        private readonly Chamber[] sections;
        private readonly double[] linkLength;
        private readonly double[] linkFlow;
        private readonly double area;
        private readonly double frictionBore;
        private readonly double frictionArea;

        /// <summary>
        /// A pipe through <paramref name="sections"/>, front to rear, whose
        /// cars are <paramref name="carLengths"/> m long.
        /// </summary>
        public BrakePipe(IReadOnlyList<Chamber> sections, IReadOnlyList<double> carLengths, PipeGeometry geometry)
        {
            if (sections == null || sections.Count == 0)
                throw new ArgumentException("A brake pipe needs at least one section.", nameof(sections));
            if (carLengths == null || carLengths.Count != sections.Count)
                throw new ArgumentException("A brake pipe needs one length per section.", nameof(carLengths));
            Geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
            this.sections = new Chamber[sections.Count];
            for (var i = 0; i < sections.Count; i++)
                this.sections[i] = sections[i];
            var links = sections.Count - 1;
            linkLength = new double[links];
            linkFlow = new double[links];
            for (var i = 0; i < links; i++)
            {
                var length = (carLengths[i] + carLengths[i + 1]) / 2;
                linkLength[i] = double.IsNaN(length) || double.IsInfinity(length) || length < 1.0 ? 1.0 : length;
            }
            area = geometry.Area;
            frictionBore = geometry.FrictionBore;
            frictionArea = Math.PI * frictionBore * frictionBore / 4;
        }

        public PipeGeometry Geometry { get; }

        /// <summary>The number of sections (cars).</summary>
        public int Count => sections.Length;

        /// <summary>The section of the car at <paramref name="index"/>, front first.</summary>
        public Chamber this[int index] => sections[index];

        /// <summary>Whether the front end's angle cock stands open to the air.</summary>
        public bool FrontOpen { get; set; }

        /// <summary>Whether the rear end's angle cock stands open to the air.</summary>
        public bool RearOpen { get; set; }

        /// <summary>Whether the front end's open cock is coupled to a closed one, and bleeds through its vent.</summary>
        public bool FrontHoseVent { get; set; }

        /// <summary>Whether the rear end's open cock is coupled to a closed one, and bleeds through its vent.</summary>
        public bool RearHoseVent { get; set; }


        /// <summary>The air through the front end's open cock in the last step, whichever way it ran, kg/s, for the hoses' sound.</summary>
        public double FrontVentFlow { get; private set; }

        /// <summary>The air let out at the rear end in the last step, or let in, kg/s.</summary>
        public double RearVentFlow { get; private set; }

        /// <summary>The mass flow, kg/s, from section <paramref name="link"/> to the one behind it.</summary>
        public double LinkFlow(int link) => linkFlow[link];

        /// <summary>
        /// The longest step, s, that keeps the waves stable: half the time
        /// sound takes along the shortest link.
        /// </summary>
        public double MaxStableStep
        {
            get
            {
                var shortest = double.PositiveInfinity;
                foreach (var l in linkLength)
                    shortest = Math.Min(shortest, l);
                return double.IsPositiveInfinity(shortest) ? 0.01 : 0.5 * shortest / Air.SoundSpeed;
            }
        }

        /// <summary>
        /// Takes over the flows in the hoses this pipe shares with
        /// <paramref name="old"/>, by the sections they join, so rebuilding
        /// the pipe as cars couple and uncouple elsewhere doesn't jolt the air
        /// already moving.
        /// </summary>
        public void CarryFlowsFrom(BrakePipe old)
        {
            if (old == null)
                return;
            var oldLinks = new Dictionary<(Chamber, Chamber), double>();
            for (var i = 0; i < old.linkFlow.Length; i++)
                oldLinks[(old.sections[i], old.sections[i + 1])] = old.linkFlow[i];
            for (var i = 0; i < linkFlow.Length; i++)
            {
                if (oldLinks.TryGetValue((sections[i], sections[i + 1]), out var flow) && IsFinite(flow))
                    linkFlow[i] = flow;
                else if (oldLinks.TryGetValue((sections[i + 1], sections[i]), out flow) && IsFinite(flow))
                    linkFlow[i] = -flow;
            }
        }

        /// <summary>Advances the air in the pipe <paramref name="dt"/> s; keep <paramref name="dt"/> under <see cref="MaxStableStep"/>.</summary>
        public void Step(double dt)
        {
            if (!(dt > 0) || double.IsInfinity(dt))
            {
                FrontVentFlow = 0;
                RearVentFlow = 0;
                return;
            }
            for (var i = 0; i < linkFlow.Length; i++)
            {
                var a = sections[i].Pressure;
                var b = sections[i + 1].Pressure;
                var mean = 0.5 * a + 0.5 * b;
                var density = Air.Density(mean > 1 ? mean : 1);
                var flow = linkFlow[i];
                // Friction as a drag on the column, treated implicitly: dṁ/dt = (A/ℓ)Δp − A·λ·ṁ|ṁ|/(2ρ·D_f·A_f²).
                var drag = area * FrictionTimesFlow(Math.Abs(flow)) / (2 * density * frictionBore * frictionArea * frictionArea);
                var next = (flow + dt * area / linkLength[i] * (a - b)) / (1 + dt * drag);
                linkFlow[i] = IsFinite(next) ? next : 0;
            }
            for (var i = 0; i < linkFlow.Length; i++)
            {
                var moved = linkFlow[i] * dt;
                if (!IsFinite(moved))
                    continue;
                sections[i].Mass -= moved;
                sections[i + 1].Mass += moved;
            }
            var frontVent = Vent(FrontOpen, FrontHoseVent, sections[0], dt);
            var rearVent = Vent(RearOpen, RearHoseVent, sections[sections.Length - 1], dt);
            FrontVentFlow = IsFinite(frontVent) ? frontVent : 0;
            RearVentFlow = IsFinite(rearVent) ? rearVent : 0;
            foreach (var s in sections)
                if (s.Mass < 0)
                    s.Mass = 0;
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        // An end's flow through its open cock, whichever way it runs, or through the closed cock's vent beside it, kg/s.
        private double Vent(bool open, bool hoseVent, Chamber end, double dt) =>
            open ? Math.Abs(Geometry.OpenEnd.OpenToAtmosphere(end, dt)) / dt : hoseVent ? Geometry.ClosedCockVent.Vent(end, dt) / dt : 0;

        /// <summary>
        /// The Darcy friction factor times the flow's magnitude, kg/s:
        /// laminar 64/Re below Re 2000, Blasius 0.316·Re^−¼ above 4000, and a
        /// straight line between. Kept as a product so it stays finite as the
        /// flow stops.
        /// </summary>
        private double FrictionTimesFlow(double flow)
        {
            // Re = ṁ·D/(A·μ), so 64/Re·ṁ = 64·A·μ/D, whatever the flow.
            var laminar = 64 * frictionArea * Air.Viscosity / frictionBore;
            var reynolds = flow * frictionBore / (frictionArea * Air.Viscosity);
            if (reynolds <= 2000)
                return laminar;
            var atFour = 0.316 * Math.Pow(4000, -0.25) * flow;
            if (reynolds >= 4000)
                return 0.316 * Math.Pow(reynolds, -0.25) * flow;
            var atTwo = 64.0 / 2000 * flow;
            var t = (reynolds - 2000) / 2000;
            return atTwo + (atFour - atTwo) * t;
        }
    }
}
