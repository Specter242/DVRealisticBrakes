using System;

namespace RealisticBrakes.Core
{
    /// <summary>The sizes of an engine's own brake (inferred unless noted; checked by the tests).</summary>
    public sealed class LocomotiveBrakeSpec
    {
        /// <summary>The main reservoir, m³. The game gives each engine's; 1,000 L is the stock steam engines'.</summary>
        public double MainReservoirVolume { get; set; } = 1.0;

        /// <summary>The engine's brake cylinders together, m³.</summary>
        public double CylinderVolume { get; set; } = 0.03;

        /// <summary>The independent brake's full application, gauge Pa: the same 3.8 bar as the automatic.</summary>
        public double IndependentMaximum { get; set; } = 3.8 * Air.Bar;

        /// <summary>The relay's supply from the main reservoir to the cylinders (set to fill in about 4 s, Passenger timing).</summary>
        public Orifice CylinderSupply { get; set; } = new Orifice(5.5);

 public Orifice CylinderExhaust { get; set; } = new Orifice(8.5);

        /// <summary>
        /// The dead-engine feed: a choke and check valve from the brake pipe
        /// into the main reservoir, so an engine hauled dead, or standing cold
        /// in a charged train, gets air for its brakes from the train (inferred
        /// bore; the game's own brake refills the reservoir from the pipe the
        /// same way, <c>SimulateCompressor</c>).
        /// </summary>
        public Orifice DeadEngineFeed { get; set; } = new Orifice(3.2);

        /// <summary>
        /// The engine's distributor for the automatic brake. Its cylinder is
        /// only the relay's pilot, so it is small, and it runs in Passenger:
        /// its chokes set the engine's automatic application and release
        /// times, which the relay follows.
        /// </summary>
        public UicDistributorSpec Distributor { get; set; } = new UicDistributorSpec
        {
            AuxiliaryReservoirVolume = 0.05,
            CylinderVolume = 0.002,
            ApplyPassenger = new Orifice(2.0),
            ReleasePassenger = new Orifice(1.05),
        };
    }

    /// <summary>
    /// An engine's brake: its main reservoir, the driver's brake valve if it
    /// has one, and its own cylinders. A relay valve fills the cylinders from
    /// the main reservoir to the greater of the automatic brake's demand, the
    /// pressure in the engine's own distributor's small pilot cylinder, and
    /// the independent's.
    /// A bail-off takes the automatic part away, as Realistic Firing's does.
    /// The relay only holds or vents toward that demand, so a cylinder the
    /// game forced on (a train found standing braked) stays on while the
    /// main reservoir is too low to fill it.
    /// </summary>
    public sealed class LocomotiveBrake
    {
        private readonly LocomotiveBrakeSpec spec;

        public LocomotiveBrake(LocomotiveBrakeSpec spec = null, DriverBrakeValve driverValve = null)
        {
            this.spec = spec ?? new LocomotiveBrakeSpec();
            MainReservoir = new Chamber(this.spec.MainReservoirVolume);
            Cylinder = new Chamber(this.spec.CylinderVolume);
            Distributor = new UicDistributor(this.spec.Distributor) { Mode = BrakingMode.Passenger };
            DriverValve = driverValve;
        }

        public LocomotiveBrakeSpec Spec => spec;

        public Chamber MainReservoir { get; }

        public Chamber Cylinder { get; }

        public UicDistributor Distributor { get; }

        /// <summary>The driver's brake valve, or null on an engine without one.</summary>
        public DriverBrakeValve DriverValve { get; }

        /// <summary>The independent brake handle, 0 released to 1 full.</summary>
        public double Independent { get; set; }

        /// <summary>The automatic application bailed off, gauge Pa.</summary>
        public double BailedOff { get; set; }

        /// <summary>
        /// Bails off the independent (Realistic Firing's rules, its design
        /// "Bailing off", 2026-10-16): the engine lets go of all the automatic
        /// application then on, the train's brakes staying applied. It stays
        /// released while the application holds; a further reduction applies
        /// it again by that much; a release clears the bail. Returns what was
        /// bailed, gauge Pa (0: nothing to bail).
        /// </summary>
        public double BailOff()
        {
            if (!Distributor.Applied)
                return 0;
            var standing = Math.Max(Distributor.TargetCylinder, Pilot);
            var bailed = Math.Max(0, standing - BailedOff);
            BailedOff = standing;
            return bailed;
        }

        // The automatic demand: the distributor's pilot cylinder, gauge Pa.
        private double Pilot => Math.Max(0, Distributor.Cylinder.Pressure - Air.Atmosphere);

        // Whether the distributor stood applied last step: a new application after a release starts without a bail.
        private bool wasApplied = true;

        /// <summary>The compressor's free air delivery, kg/s, into the main reservoir.</summary>
        public double CompressorFlow { get; set; }

        /// <summary>The cylinder pressure the relay holds toward, gauge Pa.</summary>
        public double TargetCylinder { get; private set; }

        /// <summary>The air the relay put into the cylinders in the last step, kg/s.</summary>
        public double FillFlow { get; private set; }

        /// <summary>The air let out of the cylinders in the last step, kg/s.</summary>
        public double ExhaustFlow { get; private set; }

        /// <summary>The air taken from the main reservoir in the last step, kg/s, not counting the compressor.</summary>
        public double MainReservoirDraw { get; private set; }

        /// <summary>Advances the engine's brake <paramref name="dt"/> s against its section of <paramref name="pipe"/>.</summary>
        public void Step(Chamber pipe, double dt)
        {
            MainReservoir.Mass += CompressorFlow * dt;
            spec.DeadEngineFeed.PassOneWay(pipe, MainReservoir, dt);
            var before = MainReservoir.Mass;
            DriverValve?.Step(MainReservoir, pipe, dt);
            Distributor.Step(pipe, dt);

            // A release clears the bail; a partial release shrinks it with the application, as Realistic Firing's does. The pilot
            // comes down at the Passenger release time, so the bail comes down with it, never under it: the engine stays off.
            var pilot = Pilot;
            if (Distributor.Applied && !wasApplied)
                BailedOff = 0;
            wasApplied = Distributor.Applied;
            BailedOff = Math.Max(0, Math.Min(BailedOff, Distributor.Applied ? Math.Max(Distributor.TargetCylinder, pilot) : pilot));
            var automatic = Math.Max(0, pilot - BailedOff);
            var independent = spec.IndependentMaximum * Math.Max(0, Math.Min(1, Independent));
            TargetCylinder = Math.Max(automatic, independent);
            var target = Air.Atmosphere + TargetCylinder;

            ExhaustFlow = 0;
            FillFlow = 0;
            if (Cylinder.Pressure < target)
                FillFlow = spec.CylinderSupply.PassOneWay(MainReservoir, Cylinder, dt, target) / dt;
            else if (Cylinder.Pressure > target)
                ExhaustFlow = spec.CylinderExhaust.Vent(Cylinder, dt, target) / dt;
            MainReservoirDraw = (before - MainReservoir.Mass) / dt;
        }
    }
}
