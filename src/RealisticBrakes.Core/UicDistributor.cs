using System;
using System.Collections.Generic;

namespace RealisticBrakes.Core
{
    /// <summary>The braking mode a car's G/P lever selects (UIC 543).</summary>
    public enum BrakingMode
    {
        /// <summary>Goods: the cylinders fill in 18–30 s and release in 45–60 s, to keep a long train's couplers whole.</summary>
        Goods,

        /// <summary>Passenger: fill in 3–5 s, release in 15–20 s.</summary>
        Passenger,
    }

    /// <summary>The empty/loaded changeover lever on a freight car.</summary>
    public enum LoadSetting
    {
        Loaded,
        Empty,
    }

    /// <summary>
    /// The sizes and settings of a UIC car's brake: its reservoirs, cylinder,
    /// and the distributor's chokes and thresholds. The defaults are a
    /// two-axle-bogie freight car's. Each value's source is on it; the
    /// orifice bores are set to meet UIC 543's times and are checked by the
    /// tests, not taken from a drawing.
    /// </summary>
    public sealed class UicDistributorSpec
    {
        /// <summary>The control (command) reservoir, m³: 15 L (Pugi et al. 2004, the Westinghouse U).</summary>
        public double ControlReservoirVolume { get; set; } = 0.015;

        /// <summary>The auxiliary reservoir, m³: 125 L on a GS freight car (Pugi et al. 2004).</summary>
        public double AuxiliaryReservoirVolume { get; set; } = 0.125;

        /// <summary>The brake cylinder at working stroke with its pipe, m³ (inferred: a 16-inch cylinder).</summary>
        public double CylinderVolume { get; set; } = 0.016;

        /// <summary>The quick-service chamber the first stage vents the pipe into, m³ (inferred; sized for the pipe's drop to run down the train).</summary>
        public double QuickServiceVolume { get; set; } = 0.001;

        /// <summary>
        /// How far under the control reservoir the pipe must fall to apply,
        /// Pa (inferred). Together with
        /// <see cref="ControlFollowSeconds"/> it meets UIC 540: apply within
        /// 1.2 s of a 0.6 bar drop over 6 s, but not for 0.3 bar a minute.
        /// </summary>
        public double ApplyThreshold { get; set; } = 0.1 * Air.Bar;

        /// <summary>
        /// The time constant, s, with which the control reservoir follows the
        /// pipe through its charging choke while released (inferred, set by
        /// UIC 540's sensitivity and insensitivity as above).
        /// </summary>
        public double ControlFollowSeconds { get; set; } = 15;

        /// <summary>
        /// The most the control reservoir charges to, gauge Pa: the pipe's
        /// 5 bar working pressure. A distributor's overload limiter keeps a
        /// pipe held over it (a filling stroke, a release held long) from
        /// overcharging its reference, which "could, in subsequent braking,
        /// lead to over-application of the brakes and to subsequent
        /// difficulties in releasing" (Sab Wabco, EP0797521). Without it, a
        /// stroke held for 20 s, or a brake made while a stroke's overcharge
        /// was still coming down, left cars and engines dragging after the
        /// release (a player's report, 2026-10-28).
        /// </summary>
        public double ControlReservoirLimit { get; set; } = 5.0 * Air.Bar;

        /// <summary>The pipe must come back within this of the control reservoir for the valve to release fully, Pa (inferred).</summary>
        public double ReleaseThreshold { get; set; } = 0.05 * Air.Bar;

        /// <summary>The pipe reduction that gives full service, Pa: 1.5 bar (UIC; Melzi and Grasso 2018).</summary>
        public double FullServiceReduction { get; set; } = 1.5 * Air.Bar;

        /// <summary>Full-service cylinder pressure, gauge Pa: 3.8 bar (UIC; Pugi et al. give 4.85 bar absolute).</summary>
        public double MaximumCylinder { get; set; } = 3.8 * Air.Bar;

        /// <summary>
        /// The cylinder pressure of an emergency, gauge Pa: a UIC distributor's
        /// is its full service (NaN). An American coach's graduated-release
        /// equipment gives more, as its triple valves did: an emergency
        /// "equalize[s] at about 60 lb … instead of at 50 lb" (ICS p. 184).
        /// </summary>
        public double EmergencyCylinder { get; set; } = double.NaN;

        /// <summary>A reduction past this, Pa, is an emergency (well past full service's 1.5 bar).</summary>
        public double EmergencyReduction { get; set; } = 2.5 * Air.Bar;

        /// <summary>
        /// An American passenger coach's graduated-release equipment at the
        /// game's 5 bar pipe (the 70 lb pipe of the PM and LN equipment; ICS
        /// pp. 223–239): graduated on and off, quick to apply and release
        /// (Passenger), full service at the freight car's 3.8 bar, and an
        /// emergency about a sixth higher, 4.4 bar: as high as its auxiliary
        /// reservoir, levelled with the cylinder, reaches.
        /// </summary>
        public static UicDistributorSpec AmericanCoach() => new UicDistributorSpec { EmergencyCylinder = 4.4 * Air.Bar };

        /// <summary>
        /// The first stage's cylinder pressure, gauge Pa: the brake comes on
        /// at least this far at once, quickly, to take up the rigging (Pugi
        /// et al.: faster filling under 1.6 bar absolute; the figure is
        /// inferred).
        /// </summary>
        public double FirstStageCylinder { get; set; } = 0.6 * Air.Bar;

        /// <summary>The share of loaded cylinder pressure the empty setting gives (inferred: about 1.6 of 3.8 bar).</summary>
        public double EmptyRatio { get; set; } = 0.42;

        /// <summary>The first stage's port from the auxiliary reservoir to the cylinder.</summary>
        public Orifice FirstStage { get; set; } = new Orifice(4.0);

        /// <summary>The application choke in Goods (set to UIC 543's 18–30 s).</summary>
        public Orifice ApplyGoods { get; set; } = new Orifice(1.73);

        /// <summary>The application choke in Passenger (set to UIC 543's 3–5 s).</summary>
        public Orifice ApplyPassenger { get; set; } = new Orifice(4.4);

        /// <summary>The release choke in Goods (set to UIC 543's 45–60 s).</summary>
        public Orifice ReleaseGoods { get; set; } = new Orifice(1.75);

        /// <summary>The release choke in Passenger (set to UIC 543's 15–20 s).</summary>
        public Orifice ReleasePassenger { get; set; } = new Orifice(3.0);

        /// <summary>The quick-service port from the pipe into its chamber.</summary>
        public Orifice QuickService { get; set; } = new Orifice(4.0);

        /// <summary>The auxiliary reservoir's charging choke behind its check valve.</summary>
        public Orifice AuxiliaryCharging { get; set; } = new Orifice(1.6);

        /// <summary>
        /// The wider charging path once the pipe stands this far over the
        /// auxiliary reservoir, Pa: 0.4 bar (Pugi et al. 2004, the auxiliary
        /// reservoir filling device).
        /// </summary>
        public double FastChargingDifference { get; set; } = 0.4 * Air.Bar;

        /// <summary>The fast charging path's choke.</summary>
        public Orifice AuxiliaryFastCharging { get; set; } = new Orifice(2.5);

        /// <summary>The isolated car's cylinder vent.</summary>
        public Orifice IsolatedVent { get; set; } = new Orifice(2.0);

        /// <summary>
        /// The release valve's vent, which empties the control reservoir on a
        /// pull, and the auxiliary reservoir too while held (inferred). A car
        /// standing fully applied off air lets go about 6 s after a pull, then
        /// its cylinder exhausts at its G or P release time. Held, the
        /// auxiliary reservoir drains in about 50 s.
        /// </summary>
        public Orifice ReleaseValve { get; set; } = new Orifice(6.0);
    }

    /// <summary>
    /// A UIC car's three-pressure distributor with its reservoirs and brake
    /// cylinder. It compares the brake pipe with the control reservoir,
    /// which holds the pipe's charged pressure as its memory, and sets the
    /// cylinder by how far the pipe has fallen under it. So the brake
    /// applies and releases in steps (graduated release).
    ///
    /// The auxiliary reservoir charges from the pipe through a check valve,
    /// whenever the pipe stands above it: the pipe can fall while every
    /// reservoir stays full, the reservoirs never feed the pipe, and
    /// repeated applications can't use the brake up. Released, the control
    /// reservoir follows the pipe through a fine choke, so a slow leak
    /// doesn't apply the brake. An overcharged pipe (a filling stroke)
    /// overcharges it too, and brakes then drag unless the overcharge is
    /// let down slowly. Applying, the first stage vents a little pipe air
    /// into the quick-service chamber, which carries the drop on to the next
    /// car.
    /// </summary>
    public sealed class UicDistributor : ICarValve
    {
        /// <summary>How far the pipe must rise from its lowest for the valve to count it a release, Pa (inferred).</summary>
        private const double ReleasingMargin = 0.03 * Air.Bar;

        private readonly UicDistributorSpec spec;
        private readonly Restriction controlCharging;

        // The deepest reduction of this application, Pa.
        private double deepestReduction;

        public UicDistributor(UicDistributorSpec spec = null)
        {
            this.spec = spec ?? new UicDistributorSpec();
            ControlReservoir = new Chamber(this.spec.ControlReservoirVolume);
            AuxiliaryReservoir = new Chamber(this.spec.AuxiliaryReservoirVolume);
            Cylinder = new Chamber(this.spec.CylinderVolume);
            QuickServiceChamber = new Chamber(this.spec.QuickServiceVolume);
            controlCharging = Restriction.WithTimeConstant(this.spec.ControlReservoirVolume, this.spec.ControlFollowSeconds);
        }

        public UicDistributorSpec Spec => spec;

        public Chamber ControlReservoir { get; }

        public Chamber AuxiliaryReservoir { get; }

        public Chamber Cylinder { get; }

        public Chamber QuickServiceChamber { get; }

        /// <summary>The valve's chambers.</summary>
        public IReadOnlyList<Chamber> Chambers => new[] { ControlReservoir, AuxiliaryReservoir, Cylinder, QuickServiceChamber };

        public double FullServiceCylinder => spec.MaximumCylinder;

        /// <summary>Sets the empty setting's full-service cylinder pressure, gauge Pa.</summary>
        public void SetEmptyCylinder(double cylinder) => spec.EmptyRatio = Math.Max(0, Math.Min(1, cylinder / spec.MaximumCylinder));

        public double ChargeFlow { get; private set; }

        /// <summary>Whether the valve stands out of release: the control reservoir sealed and the cylinder set from it.</summary>
        public bool Applied { get; private set; }

        /// <summary>The G/P lever.</summary>
        public BrakingMode Mode { get; set; } = BrakingMode.Goods;

        /// <summary>The empty/loaded lever.</summary>
        public LoadSetting Load { get; set; } = LoadSetting.Loaded;

        /// <summary>Whether the isolating cock has cut the distributor out: the car runs unbraked and the pipe passes through.</summary>
        public bool Isolated { get; set; }

        /// <summary>The air taken from the pipe in the last step, kg/s.</summary>
        public double PipeDraw { get; private set; }

        /// <summary>The air put into the cylinder in the last step, kg/s, for the airflow's sound.</summary>
        public double FillFlow { get; private set; }

        /// <summary>The air let out of the cylinder in the last step, kg/s, for the exhaust's sound.</summary>
        public double ExhaustFlow { get; private set; }

        /// <summary>The cylinder pressure the valve is holding toward, gauge Pa.</summary>
        public double TargetCylinder { get; private set; }

        /// <summary>The deepest reduction of the application in hand, Pa: the first stage holds until the pipe rises from it.</summary>
        public double DeepestReduction => deepestReduction;

        /// <summary>
        /// Sets whether the valve stands applied and its application's deepest
        /// reduction, as saved: a car in the middle of a graduated release
        /// comes back exactly where it was.
        /// </summary>
        public void RestoreLatch(bool applied, double deepest)
        {
            Applied = applied;
            deepestReduction = applied ? Math.Max(0, deepest) : 0;
        }

        /// <summary>
        /// Charges every chamber to <paramref name="pressure"/> Pa, released:
        /// a car found standing charged, or a test's starting point.
        /// </summary>
        public void ChargeTo(double pressure)
        {
            ControlReservoir.Pressure = pressure;
            AuxiliaryReservoir.Pressure = pressure;
            Cylinder.Pressure = Air.Atmosphere;
            QuickServiceChamber.Pressure = Air.Atmosphere;
            Applied = false;
            TargetCylinder = 0;
        }

        /// <summary>
        /// Sets the chambers to pressures read from the game (a save, or the
        /// stock brake's state when this car first came under the model), Pa
        /// absolute. The valve stands applied if the pipe at
        /// <paramref name="pipe"/> Pa is under the control reservoir by its
        /// threshold.
        /// </summary>
        public void Restore(double control, double auxiliary, double cylinder, double pipe)
        {
            ControlReservoir.Pressure = control;
            AuxiliaryReservoir.Pressure = auxiliary;
            Cylinder.Pressure = Math.Max(Air.Atmosphere, cylinder);
            QuickServiceChamber.Pressure = Air.Atmosphere;
            Applied = control - pipe > spec.ApplyThreshold;
            deepestReduction = Applied ? control - pipe : 0;
            TargetCylinder = 0;
        }

        // The release valve, opened by a pull: it vents the control reservoir until the distributor lets go, then closes by itself.
        private bool releaseValveOpen;

        /// <summary>
        /// A short pull on the release valve, as on a UIC distributor's
        /// release device: it vents the control reservoir until the
        /// distributor lets go of the cylinder, then closes by itself. A car
        /// standing off air releases and stays released; its auxiliary
        /// reservoir keeps its air.
        /// </summary>
        public void PullRelease() => releaseValveOpen = true;

        /// <summary>Whether the release valve stands open from a pull, until the distributor lets go.</summary>
        public bool ReleaseValveOpen => releaseValveOpen;

        /// <summary>Restores the release valve, as saved (a review, 2026-10-27: a car saved mid-release stayed applied).</summary>
        public void RestoreReleaseValve(bool open) => releaseValveOpen = open;

        /// <summary>The release valve held open (a long pull): the auxiliary reservoir drains too, taking the car off air.</summary>
        public bool ReleaseHeld { get; set; }

        /// <summary>Advances the valve <paramref name="dt"/> s against its car's section of <paramref name="pipe"/>.</summary>
        public void Step(Chamber pipe, double dt)
        {
            var pipeBefore = pipe.Mass;
            var exhausted = 0.0;
            FillFlow = 0;
            ChargeFlow = 0;
            if (Isolated)
            {
                Applied = false;
                TargetCylinder = 0;
                exhausted += spec.IsolatedVent.Vent(Cylinder, dt);
                exhausted += spec.IsolatedVent.Vent(QuickServiceChamber, dt);
                PipeDraw = 0;
                ExhaustFlow = exhausted / dt;
                return;
            }

            ChargeAuxiliary(pipe, dt);

            if (releaseValveOpen || ReleaseHeld)
            {
                exhausted += spec.ReleaseValve.Vent(ControlReservoir, dt);
                if (ReleaseHeld)
                    exhausted += spec.ReleaseValve.Vent(AuxiliaryReservoir, dt);
            }

            var reduction = ControlReservoir.Pressure - pipe.Pressure;
            if (!Applied && reduction > spec.ApplyThreshold)
            {
                Applied = true;
                deepestReduction = 0;
            }
            else if (Applied && reduction < spec.ReleaseThreshold)
                Applied = false;
            // Released, the release valve closes by itself.
            if (!Applied)
                releaseValveOpen = false;

            if (Applied)
            {
                spec.QuickService.PassOneWay(pipe, QuickServiceChamber, dt);
                deepestReduction = Math.Max(deepestReduction, reduction);
                var target = spec.MaximumCylinder * reduction / spec.FullServiceReduction;
                var emergency = !double.IsNaN(spec.EmergencyCylinder) && reduction > spec.EmergencyReduction;
                // The first stage holds its minimum application until the driver starts to release.
                if (reduction > deepestReduction - ReleasingMargin)
                    target = Math.Max(target, spec.FirstStageCylinder);
                target = emergency ? spec.EmergencyCylinder : Math.Min(target, spec.MaximumCylinder);
                if (Load == LoadSetting.Empty)
                    target *= spec.EmptyRatio;
                TargetCylinder = target;
                exhausted += SetCylinder(Air.Atmosphere + target, dt);
            }
            else
            {
                ChargeFlow += Math.Max(0, controlCharging.Pass(pipe, ControlReservoir, dt, Air.Atmosphere + spec.ControlReservoirLimit)) / dt;
                TargetCylinder = 0;
                exhausted += SetCylinder(Air.Atmosphere, dt);
                exhausted += spec.QuickService.Vent(QuickServiceChamber, dt);
            }

            PipeDraw = (pipeBefore - pipe.Mass) / dt;
            ExhaustFlow = exhausted / dt;
        }

        private void ChargeAuxiliary(Chamber pipe, double dt)
        {
            var choke = pipe.Pressure - AuxiliaryReservoir.Pressure > spec.FastChargingDifference
                ? spec.AuxiliaryFastCharging
                : spec.AuxiliaryCharging;
            ChargeFlow = choke.PassOneWay(pipe, AuxiliaryReservoir, dt) / dt;
        }

        /// <summary>Fills or vents the cylinder toward <paramref name="target"/> Pa. Returns the air vented, kg.</summary>
        private double SetCylinder(double target, double dt)
        {
            var cylinder = Cylinder.Pressure;
            if (cylinder < target)
            {
                var firstStage = cylinder < Air.Atmosphere + spec.FirstStageCylinder;
                var choke = firstStage ? spec.FirstStage : Mode == BrakingMode.Goods ? spec.ApplyGoods : spec.ApplyPassenger;
                FillFlow += choke.PassOneWay(AuxiliaryReservoir, Cylinder, dt, target) / dt;
                return 0;
            }
            if (cylinder > target)
            {
                var choke = Mode == BrakingMode.Goods ? spec.ReleaseGoods : spec.ReleasePassenger;
                return choke.Vent(Cylinder, dt, target);
            }
            return 0;
        }
    }
}
