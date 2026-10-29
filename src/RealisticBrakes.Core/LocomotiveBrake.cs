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

        /// <summary>
        /// Whether the engine's automatic brake is the E-T's No. 6 distributing
        /// valve, worked through the H-6: it applies with the reduction, but
        /// releases only with the automatic handle in running, and then all at
        /// once (Wood pp. 103, 115: "graduated off" only with the independent).
        /// In release, holding, and lap it stays applied as the train releases
        /// (Wood pp. 140, 142). Otherwise the engine follows its distributor,
        /// graduated, as a European engine does.
        /// </summary>
        public bool HoldsAutomaticBrake { get; set; }

        /// <summary>
        /// The engine's cylinders in an emergency, gauge Pa: its full service,
        /// unless the equipment gives more. The No. 6 equalizes "at 65 pounds
        /// instead of the 50 pounds obtainable at full service" (Wood p. 91).
        /// </summary>
        public double EmergencyCylinder { get; set; } = 3.8 * Air.Bar;

        /// <summary>
        /// The engine's cylinders in an emergency made by its own brake valve,
        /// gauge Pa: the No. 6's maintaining port "raises the latter pressure
        /// to 68 pounds", its safety valve's setting (Wood p. 91).
        /// </summary>
        public double OwnEmergencyCylinder { get; set; } = 3.8 * Air.Bar;

        /// <summary>A reduction past this, Pa, is an emergency (well past full service's 1.5 bar).</summary>
        public double EmergencyReduction { get; set; } = 2.5 * Air.Bar;

        /// <summary>
        /// Whether the air pump's governor is the E-T's duplex governor: in
        /// running and holding it holds the main reservoir "only 20 pounds or so
        /// in excess of that in the brake pipe", and in lap and the application
        /// positions lets it rise to 110 pounds (Wood pp. 208, 265; ICS
        /// pp. 102, 106). Otherwise the game's own governor runs it.
        /// </summary>
        public bool DuplexGovernor { get; set; }

        /// <summary>The duplex governor's excess pressure over the pipe, Pa: 20 psi.</summary>
        public double ExcessPressure { get; set; } = 1.4 * Air.Bar;

        /// <summary>The duplex governor's maximum-pressure head, gauge Pa: 110 psi.</summary>
        public double GovernorMaximum { get; set; } = 7.6 * Air.Bar;

        /// <summary>How far under its setting the governor starts the pump again, Pa (inferred: about 3 psi).</summary>
        public double GovernorDifferential { get; set; } = 0.2 * Air.Bar;

        /// <summary>
        /// An American steam engine's E-T equipment (Wood; ICS): the No. 6
        /// distributing valve held through release and holding, its emergency
        /// at 65 and 68 lb, the S-6 independent from a 45 lb reducing valve
        /// (Wood p. 181), the duplex governor, and a dead-engine feed through a
        /// choke "about 1/16-inch diameter" (Wood p. 275). The distributing
        /// valve has no quick service on the pipe: "the venting of brake pipe
        /// air is not included as an emergency feature" (Wood p. 56).
        /// </summary>
        public static LocomotiveBrakeSpec EtSteam()
        {
            var spec = new LocomotiveBrakeSpec
            {
                HoldsAutomaticBrake = true,
                EmergencyCylinder = 4.5 * Air.Bar,
                OwnEmergencyCylinder = 4.7 * Air.Bar,
                IndependentMaximum = 3.1 * Air.Bar,
                DuplexGovernor = true,
                DeadEngineFeed = new Orifice(1.6),
            };
            spec.Distributor.QuickService = new Orifice(0);
            return spec;
        }
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
        /// The automatic application an E-T engine holds, gauge Pa
        /// (<see cref="LocomotiveBrakeSpec.HoldsAutomaticBrake"/>): the
        /// highest its distributor has asked for, until the automatic handle
        /// in running lets it go.
        /// </summary>
        public double HeldAutomatic { get; set; }

        // Whether the automatic handle lets an E-T engine's brake go: in running, or with no valve of its own working (a trailing or dead engine).
        private bool LetsAutomaticGo => DriverValve == null || !DriverValve.CutIn || DriverValve.Position == DriverValvePosition.Running;

        // The automatic application standing, gauge Pa, before any bail.
        private double StandingAutomatic => spec.HoldsAutomaticBrake ? HeldAutomatic : Math.Max(Distributor.Applied ? Distributor.TargetCylinder : 0, Pilot);

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
            if (!Distributor.Applied && !(spec.HoldsAutomaticBrake && HeldAutomatic > 0))
                return 0;
            // In the engine's own emergency on the E-T it lets off only for the moment it's held (BailHold).
            bailing = Math.Max(bailing, BailHold);
            var standing = StandingAutomatic;
            var bailed = Math.Max(0, standing - BailedOff);
            BailedOff = standing;
            return bailed;
        }

        /// <summary>
        /// Bails off up to <paramref name="amount"/> gauge Pa of the automatic
        /// application, graduated, as holding the S-6 independent in release
        /// does on an E-T engine: what's left "will remain constant (unless
        /// automatically increased by brake pipe leakage)" (Wood p. 120).
        /// Returns what was bailed, gauge Pa. The independent counts as held in
        /// release for <paramref name="hold"/> s; an amount of 0 only keeps it
        /// held.
        /// </summary>
        public double BailOffBy(double amount, double hold = BailHold)
        {
            // Held in release, even with nothing more to let out: in the engine's own emergency the bail lasts while it's held.
            bailing = Math.Max(bailing, hold);
            if (amount <= 0)
                return 0;
            var standing = StandingAutomatic;
            var was = BailedOff;
            BailedOff = Math.Min(standing, BailedOff + amount);
            return Math.Max(0, BailedOff - was);
        }

        /// <summary>
        /// How long, in s, a bail counts as the independent still held in
        /// release after its last <see cref="BailOffBy"/> or
        /// <see cref="BailOff"/>: this game's lever calls once a frame while
        /// it's held. Another player's comes over the network, with a longer
        /// hold (<paramref name="hold"/> of <see cref="BailOffBy"/>).
        /// </summary>
        public const double BailHold = 0.25;

        /// <summary>The independent let go of release: in the engine's own emergency, the maintaining port puts the bail back at once.</summary>
        public void EndBail() => bailing = 0;

        // The time left, s, that the independent counts as held in release.
        private double bailing;

        /// <summary>Whether the engine's automatic brake stands applied (held, on an E-T engine).</summary>
        public bool AutomaticApplied => spec.HoldsAutomaticBrake ? HeldAutomatic > 0 : Distributor.Applied;

        /// <summary>
        /// The duplex governor's setting now, gauge Pa, or NaN where the game's
        /// governor runs the pump: the pipe at the engine plus the excess
        /// pressure in running and holding (so charging an empty train, "the
        /// air pump will stop working" until the main reservoir has come down
        /// near the pipe, Wood p. 228); release's 20 lb over the feed valve,
        /// where main reservoir and pipe "would have equalized at 90 pounds"
        /// (p. 265); and the maximum head in lap, service, and emergency, or
        /// with no valve of its own working.
        /// </summary>
        public double GovernorSetting(Chamber pipe)
        {
            if (!spec.DuplexGovernor)
                return double.NaN;
            var valve = DriverValve;
            if (valve == null || !valve.CutIn)
                return spec.GovernorMaximum;
            switch (valve.Position)
            {
                case DriverValvePosition.Running:
                case DriverValvePosition.Holding:
                    return Math.Min(spec.GovernorMaximum, Math.Max(0, pipe.Pressure - Air.Atmosphere) + spec.ExcessPressure);
                case DriverValvePosition.Fill:
                    return Math.Min(spec.GovernorMaximum, valve.Spec.Running + spec.ExcessPressure);
                default:
                    return spec.GovernorMaximum;
            }
        }

        // The automatic demand: the distributor's pilot cylinder, gauge Pa.
        private double Pilot => Math.Max(0, Distributor.Cylinder.Pressure - Air.Atmosphere);

        // Whether the distributor stood applied last step: a new application after a release starts without a bail.
        private bool wasApplied = true;

        /// <summary>The compressor's free air delivery, kg/s, into the main reservoir.</summary>
        public double CompressorFlow { get; set; }

        /// <summary>The automatic brake's demand on the engine after any bail, gauge Pa.</summary>
        public double AutomaticCylinder { get; private set; }

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
            {
                // A held E-T brake keeps what wasn't bailed: the bail emptied its application chamber.
                HeldAutomatic = Math.Max(0, HeldAutomatic - BailedOff);
                BailedOff = 0;
            }
            wasApplied = Distributor.Applied;
            double automatic;
            if (spec.HoldsAutomaticBrake)
            {
                // The No. 6: applied with the reduction, never graduated off; released by the pipe, it lets go only with the
                // automatic handle in running, and then at once.
                if (Distributor.Applied)
                    HeldAutomatic = Math.Max(HeldAutomatic, pilot);
                else if (LetsAutomaticGo)
                    HeldAutomatic = 0;
                var own = DriverValve != null && DriverValve.CutIn && DriverValve.Position == DriverValvePosition.Emergency;
                if (Distributor.Applied && Distributor.ControlReservoir.Pressure - pipe.Pressure > spec.EmergencyReduction)
                    HeldAutomatic = Math.Max(HeldAutomatic, own ? spec.OwnEmergencyCylinder : spec.EmergencyCylinder);
                // In the engine's own emergency the H-6's maintaining port puts back what the independent lets out as soon as
                // it's no longer held in release (Wood p. 120): a bail lasts only while the lever is held.
                if (own && bailing <= 0)
                    BailedOff = 0;
                bailing = Math.Max(0, bailing - dt);
                BailedOff = Math.Max(0, Math.Min(BailedOff, HeldAutomatic));
                automatic = Math.Max(0, HeldAutomatic - BailedOff);
            }
            else
            {
                BailedOff = Math.Max(0, Math.Min(BailedOff, Distributor.Applied ? Math.Max(Distributor.TargetCylinder, pilot) : pilot));
                automatic = Math.Max(0, pilot - BailedOff);
            }
            var independent = spec.IndependentMaximum * Math.Max(0, Math.Min(1, Independent));
            AutomaticCylinder = automatic;
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
