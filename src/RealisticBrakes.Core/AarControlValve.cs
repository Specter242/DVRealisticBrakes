using System;
using System.Collections.Generic;

namespace RealisticBrakes.Core
{
    /// <summary>The retaining valve on an American car's cylinder exhaust.</summary>
    public enum Retainer
    {
        /// <summary>Exhaust: the cylinder exhausts fully on a release.</summary>
        Exhaust,

        /// <summary>Low pressure: holds about 10 psi (0.7 bar) in the cylinder through a release.</summary>
        LowPressure,

        /// <summary>High pressure: holds about 20 psi (1.4 bar).</summary>
        HighPressure,

        /// <summary>Slow direct: exhausts fully, slowly.</summary>
        SlowDirect,
    }

    /// <summary>
    /// The sizes and settings of an American freight car's AB-type control
    /// valve. Volumes are the AB equipment's for a 10-inch cylinder; the
    /// thresholds and bores are inferred and set by the tests, at the game's
    /// 5 bar pipe (close to the steam era's 70 psi freight pipe).
    /// </summary>
    public sealed class AarControlValveSpec
    {
        /// <summary>The auxiliary reservoir, m³: 2,500 cu in.</summary>
        public double AuxiliaryReservoirVolume { get; set; } = 0.041;

        /// <summary>The emergency reservoir, m³: 3,500 cu in.</summary>
        public double EmergencyReservoirVolume { get; set; } = 0.0574;

        /// <summary>
        /// The cylinder at service travel with its pipe, m³: 16.7 L gives the
        /// AB's 2.5 bar of cylinder per bar of reduction, and full-service
        /// equalization at 64 psi from a 90 psi pipe.
        /// </summary>
        public double CylinderVolume { get; set; } = 0.0167;

        /// <summary>The piston travel <see cref="CylinderVolume"/> is at, inches: 8, as the equipment is proportioned (ICS p. 367).</summary>
        public const double NominalTravel = 8;

        /// <summary>The 10-inch cylinder's swept volume per inch of piston travel, m³ (78.5 sq in).</summary>
        public const double VolumePerInch = 78.54 * 0.0254 * 0.0254 * 0.0254;

        /// <summary>
        /// The cylinder at <paramref name="travel"/> inches of piston travel,
        /// m³: the same clearance and pipe, and the piston's sweep. Travel
        /// matters a great deal: a 12 lb reduction gives "18 lb" at 12 in,
        /// "35 lb" at 8 in, and "65 lb" at 4 in (ICS p. 200).
        /// </summary>
        public static double CylinderVolumeAt(double travel) =>
            0.0167 + (Math.Max(1, travel) - NominalTravel) * VolumePerInch;

        /// <summary>The quick-action chamber, which senses an emergency rate of fall, m³ (inferred).</summary>
        public double QuickActionVolume { get; set; } = 0.0015;

        /// <summary>How far the pipe must fall under the auxiliary reservoir to apply, Pa (about 1.5 psi).</summary>
        public double ApplyThreshold { get; set; } = 0.1 * Air.Bar;

        /// <summary>How far the pipe must rise over the auxiliary reservoir to release, Pa (about 1.5 psi).</summary>
        public double ReleaseThreshold { get; set; } = 0.1 * Air.Bar;

        /// <summary>The auxiliary reservoir level with the pipe within this, Pa, the valve laps.</summary>
        public double LapBand { get; set; } = 0.01 * Air.Bar;

        /// <summary>The quick-service limiting valve's cylinder pressure, gauge Pa: the first application takes pipe air into the cylinder to here (8–10 psi).</summary>
        public double QuickServiceCylinder { get; set; } = 0.6 * Air.Bar;

        /// <summary>The quick-action chamber's lead over the pipe that makes an emergency, Pa (inferred).</summary>
        public double EmergencyThreshold { get; set; } = 0.45 * Air.Bar;

        /// <summary>The time constant, s, with which the quick-action chamber follows the pipe (inferred: a service rate stays under the threshold).</summary>
        public double QuickActionSeconds { get; set; } = 1.0;

        /// <summary>
        /// The quick-action chamber's blowdown choke in an emergency: the vent
        /// valve stays open until the chamber has blown down, about a minute,
        /// so the train can't be recharged at once (inferred bore).
        /// </summary>
        public Orifice QuickActionBlowdown { get; set; } = new Orifice(0.55);

        /// <summary>The quick-action chamber pressure over the atmosphere that holds the vent valve open, Pa (inferred).</summary>
        public double VentHold { get; set; } = 0.3 * Air.Bar;

        /// <summary>
        /// The time constant, s, with which the auxiliary reservoir follows a
        /// slowly falling pipe back through the charging path while released,
        /// so a leak doesn't apply the brake (inferred, as the European
        /// control reservoir's: 0.3 bar a minute stays under the threshold).
        /// </summary>
        public double BackflowSeconds { get; set; } = 15;

        /// <summary>The service port from the auxiliary reservoir to the cylinder (set to fill in about 7 s).</summary>
        public Orifice ServicePort { get; set; } = new Orifice(2.6);

        /// <summary>
        /// The quick-service port from the pipe into the cylinder: small
        /// enough that a train of short cars' dips don't add up to an
        /// emergency (a second review, 2026-10-24: a 3 mm port tripped 40
        /// 9.1 m flatcars on a 0.5 bar service).
        /// </summary>
        public Orifice QuickServicePort { get; set; } = new Orifice(1.5);

        /// <summary>The emergency port from both reservoirs into the cylinder.</summary>
        public Orifice EmergencyPort { get; set; } = new Orifice(6.0);

        /// <summary>The vent valve opening the pipe to the air in an emergency.</summary>
        public Orifice VentValve { get; set; } = new Orifice(20.0);

        /// <summary>The exhaust from the cylinder on a release (set to about 15 s to 0.4 bar).</summary>
        public Orifice ReleasePort { get; set; } = new Orifice(3.0);

        /// <summary>The slow-direct retainer's exhaust.</summary>
        public Orifice SlowDirectPort { get; set; } = new Orifice(0.9);

        /// <summary>The auxiliary reservoir's charging choke, in release only.</summary>
        public Orifice AuxiliaryCharging { get; set; } = new Orifice(1.2);

        /// <summary>The emergency reservoir's charging choke, in release only.</summary>
        public Orifice EmergencyCharging { get; set; } = new Orifice(1.3);

        /// <summary>The accelerated release: emergency reservoir air into the pipe as a service release begins.</summary>
        public Orifice AcceleratedRelease { get; set; } = new Orifice(2.0);

        /// <summary>
        /// The bleed rod's drain, which empties the auxiliary and emergency
        /// reservoirs while the rod is held: both in about 50 s (inferred).
        /// The cylinder vents through <see cref="ReleasePort"/>.
        /// </summary>
        public Orifice DrainPort { get; set; } = new Orifice(4.0);

        /// <summary>
        /// The retaining valve's vent while it holds (HP or LP): "1/16-in.",
        /// which takes "about 20 to 25 sec." from 50 to 15 lb, "thus sufficient
        /// time is given for the auxiliaries to recharge" (ICS p. 378). Sized
        /// to that time on this larger cylinder: the 1/16 in. bore took 41 s.
        /// </summary>
        public Orifice RetainerVent { get; set; } = new Orifice(2.15);

        /// <summary>
        /// Whether the valve has retarded release and uniform recharge, as the
        /// K triple brought in (ICS pp. 194–195): a car that sees the pipe rise
        /// well over its auxiliary reservoir, near the engine, releases through
        /// a small port and recharges slowly, so a long train lets go "at
        /// practically the same time" (p. 209) and the head end can't rob the
        /// rear of air.
        /// </summary>
        public bool RetardedRelease { get; set; } = true;

        /// <summary>How far the pipe must stand over the auxiliary reservoir for a retarded release, Pa: "about 3 lb." (ICS p. 195).</summary>
        public double RetardThreshold { get; set; } = 0.2 * Air.Bar;

        /// <summary>
        /// The retarded release's exhaust: set so the head end of a long train
        /// lets go with the rear, about 24 s after a full service on 50 cars.
        /// The accelerated release already carries a release down the train
        /// quickly, so the head end only waits a few seconds more.
        /// </summary>
        public Orifice RetardedReleasePort { get; set; } = new Orifice(RetardedReleaseBore);

        /// <summary>The retarded release's exhaust bore, mm (set by the tests).</summary>
        public const double RetardedReleaseBore = 2.4;

        /// <summary>The share of the charging the auxiliary gets in retarded release: the feed groove "about half as large" (ICS p. 195).</summary>
        public double RetardedChargeShare { get; set; } = 0.5;

        /// <summary>The high-pressure retainer's hold, gauge Pa: 20 psi.</summary>
        public double HighRetained { get; set; } = 1.4 * Air.Bar;

        /// <summary>The low-pressure retainer's hold, gauge Pa: 10 psi.</summary>
        public double LowRetained { get; set; } = 0.7 * Air.Bar;
    }

    /// <summary>
    /// An American freight car's AB-type control valve with its auxiliary
    /// and emergency reservoirs and cylinder. Unlike the European
    /// distributor, it is a two-pressure valve: it compares the pipe with its
    /// own auxiliary reservoir.
    ///
    /// In service it lets the auxiliary reservoir into the cylinder until the
    /// reservoir has fallen to the pipe, then laps; the volumes give the
    /// cylinder about 2.5 times the reduction. Any rise of the pipe over the
    /// reservoir releases the cylinder in full (direct release), unless the
    /// retaining valve holds some. The reservoirs recharge only in release,
    /// so applications made before the train has recharged come up short,
    /// and repeated ones run the brake down.
    ///
    /// Its emergency portion watches how fast the pipe falls. Faster than a
    /// service rate, it vents the pipe at the car, carrying the emergency
    /// down the train, and adds the emergency reservoir's air: about 20 %
    /// more in the cylinder. A service release begins with emergency
    /// reservoir air fed back into the pipe, speeding the release along the
    /// train.
    /// </summary>
    public sealed class AarControlValve : ICarValve
    {
        private readonly AarControlValveSpec spec;
        private readonly Restriction quickActionCharging;
        private readonly Restriction auxiliaryBackflow;
        private readonly Chamber[] chambers;
        private bool quickServiceDone;
        private bool acceleratingRelease;

        public AarControlValve(AarControlValveSpec spec = null)
        {
            this.spec = spec ?? new AarControlValveSpec();
            AuxiliaryReservoir = new Chamber(this.spec.AuxiliaryReservoirVolume);
            EmergencyReservoir = new Chamber(this.spec.EmergencyReservoirVolume);
            Cylinder = new Chamber(this.spec.CylinderVolume);
            QuickActionChamber = new Chamber(this.spec.QuickActionVolume);
            quickActionCharging = Restriction.WithTimeConstant(this.spec.QuickActionVolume, this.spec.QuickActionSeconds);
            auxiliaryBackflow = Restriction.WithTimeConstant(this.spec.AuxiliaryReservoirVolume, this.spec.BackflowSeconds);
            chambers = new[] { AuxiliaryReservoir, EmergencyReservoir, Cylinder, QuickActionChamber };
        }

        public AarControlValveSpec Spec => spec;

        public Chamber AuxiliaryReservoir { get; }

        public Chamber EmergencyReservoir { get; }

        public Chamber Cylinder { get; }

        public Chamber QuickActionChamber { get; }

        public IReadOnlyList<Chamber> Chambers => chambers;

        public bool Applied { get; private set; }

        /// <summary>Whether the valve went to emergency in this application.</summary>
        public bool Emergency { get; private set; }

        public bool Isolated { get; set; }

        public LoadSetting Load
        {
            get => load;
            set
            {
                if (load == value)
                    return;
                load = value;
                Cylinder.Resize(spec.CylinderVolume + (value == LoadSetting.Empty ? emptyVolume : 0));
            }
        }

        private LoadSetting load = LoadSetting.Loaded;

        // The empty/load equipment's added cylinder volume, m³, which proportions the empty setting's pressure.
        private double emptyVolume;

        public Retainer Retainer { get; set; } = Retainer.Exhaust;

        public double PipeDraw { get; private set; }

        public double FillFlow { get; private set; }

        public double ExhaustFlow { get; private set; }

        /// <summary>
        /// Sizes the empty/load equipment so that full service on empty gives
        /// <paramref name="cylinder"/> gauge Pa. The equipment proportions the
        /// quick-service jump too, and adds cylinder volume: in service the
        /// cylinder rises by the reservoir's fall, 1.5 bar at full service,
        /// times the reservoir's volume over the cylinder's. The quick-service
        /// air comes in as the reservoir's does, and adds about half its jump
        /// (set by the tests). An emergency on empty is held to the same
        /// share of the loaded emergency.
        /// </summary>
        public void SetEmptyCylinder(double cylinder)
        {
            emptyRatio = Math.Max(0.05, Math.Min(1, cylinder / FullServiceCylinder));
            var quick = spec.QuickServiceCylinder * emptyRatio;
            var risen = Math.Max(0.05 * Air.Bar, cylinder - quick / 2);
            var total = FullServiceReduction * spec.AuxiliaryReservoirVolume / risen;
            emptyVolume = Math.Max(0, total - spec.CylinderVolume);
            if (load == LoadSetting.Empty)
                Cylinder.Resize(spec.CylinderVolume + emptyVolume);
        }

        /// <summary>The loaded emergency's cylinder pressure, gauge Pa: both reservoirs and the cylinder levelled (about 4.3 bar).</summary>
        public double EmergencyCylinder =>
            5.0 * Air.Bar * (spec.AuxiliaryReservoirVolume + spec.EmergencyReservoirVolume)
            / (spec.AuxiliaryReservoirVolume + spec.EmergencyReservoirVolume + spec.CylinderVolume);

        public double ChargeFlow { get; private set; }

        /// <summary>
        /// Full service's cylinder pressure on loaded, from a 5 bar pipe,
        /// gauge Pa: the charged reservoir and the cylinder after its
        /// quick-service jump, levelled (about 3.7 bar).
        /// </summary>
        public double FullServiceCylinder =>
            (5.0 * Air.Bar * spec.AuxiliaryReservoirVolume + spec.QuickServiceCylinder * spec.CylinderVolume)
            / (spec.AuxiliaryReservoirVolume + spec.CylinderVolume);

        /// <summary>The pipe reduction of full service, Pa: 1.5 bar.</summary>
        private const double FullServiceReduction = 1.5 * Air.Bar;

        // The empty setting's share of loaded cylinder pressure.
        private double emptyRatio = 1;

        public void ChargeTo(double pressure)
        {
            AuxiliaryReservoir.Pressure = pressure;
            EmergencyReservoir.Pressure = pressure;
            QuickActionChamber.Pressure = pressure;
            Cylinder.Pressure = Air.Atmosphere;
            Applied = false;
            Emergency = false;
            quickServiceDone = false;
            acceleratingRelease = false;
        }

        /// <summary>
        /// Sets the chambers to pressures read from the game or a save, Pa
        /// absolute. The valve stands applied if the cylinder holds pressure.
        /// </summary>
        public void Restore(double auxiliary, double emergency, double cylinder, double pipe)
        {
            AuxiliaryReservoir.Pressure = auxiliary;
            EmergencyReservoir.Pressure = emergency;
            Cylinder.Pressure = Math.Max(Air.Atmosphere, cylinder);
            QuickActionChamber.Pressure = pipe;
            Applied = cylinder > Air.FromGaugeBar(0.1);
            Emergency = false;
            quickServiceDone = Applied;
            acceleratingRelease = false;
            CylinderReleaseOpen = false;
        }

        /// <summary>Sets the latch as saved.</summary>
        public void RestoreLatch(bool applied, bool emergency)
        {
            Applied = applied;
            Emergency = applied && emergency;
            quickServiceDone = applied;
            CylinderReleaseOpen = false;
        }

        /// <summary>
        /// Whether the brake cylinder release valve stands open: a short pull
        /// of the bleed rod opened it on an applied car, and it vents the
        /// cylinder until the pipe is recharged and the valve releases.
        /// </summary>
        public bool CylinderReleaseOpen { get; private set; }

        /// <summary>
        /// A short pull of the bleed rod, as on the ABDX and its successors:
        /// the brake cylinder release valve opens and vents the cylinder
        /// straight to the air. It stays open, the car released, until the
        /// pipe is recharged; a further reduction meanwhile doesn't apply it.
        /// The reservoirs keep their air for the next application.
        /// </summary>
        public void PullRelease()
        {
            if (Applied)
                CylinderReleaseOpen = true;
        }

        /// <summary>The bleed rod held (a long pull): the auxiliary and emergency reservoirs drain too, taking the car off air.</summary>
        public bool ReleaseHeld { get; set; }

        /// <summary>Restores the brake cylinder release valve's latch, as saved.</summary>
        public void RestoreCylinderRelease(bool open) => CylinderReleaseOpen = open && Applied;

        public void Step(Chamber pipe, double dt)
        {
            var pipeBefore = pipe.Mass;
            var exhausted = 0.0;
            FillFlow = 0;
            ChargeFlow = 0;
            Retarded = false;
            if (Isolated)
            {
                Applied = false;
                Emergency = false;
                exhausted += spec.ReleasePort.Vent(Cylinder, dt);
                PipeDraw = 0;
                ExhaustFlow = exhausted / dt;
                return;
            }

            // The emergency portion: the quick-action chamber follows the pipe; a fall it can't keep up with is an emergency.
            // In an emergency it blows down instead, holding the vent valve open meanwhile.
            if (Emergency)
                spec.QuickActionBlowdown.Vent(QuickActionChamber, dt);
            else
                quickActionCharging.Pass(pipe, QuickActionChamber, dt);
            if (!Emergency && QuickActionChamber.Pressure - pipe.Pressure > spec.EmergencyThreshold)
            {
                Emergency = true;
                Applied = true;
                acceleratingRelease = false;
            }

            var lead = AuxiliaryReservoir.Pressure - pipe.Pressure;
            if (Applied && !Emergency && -lead > spec.ReleaseThreshold)
                Release();
            else if (Emergency && pipe.Pressure - AuxiliaryReservoir.Pressure > spec.ReleaseThreshold)
                Release();
            else if (!Applied && lead > spec.ApplyThreshold)
            {
                Applied = true;
                quickServiceDone = false;
                acceleratingRelease = false;
            }

            // Released, the brake cylinder release valve resets.
            if (!Applied)
                CylinderReleaseOpen = false;
            if (ReleaseHeld)
            {
                exhausted += spec.DrainPort.Vent(AuxiliaryReservoir, dt);
                exhausted += spec.DrainPort.Vent(EmergencyReservoir, dt);
            }

            if (CylinderReleaseOpen)
            {
                // The cylinder vents straight to the air; the vent valve still works the pipe in an emergency.
                if (Emergency && QuickActionChamber.Pressure > Air.Atmosphere + spec.VentHold)
                    exhausted += spec.VentValve.Vent(pipe, dt);
                exhausted += spec.ReleasePort.Vent(Cylinder, dt);
            }
            else if (Emergency)
            {
                if (QuickActionChamber.Pressure > Air.Atmosphere + spec.VentHold)
                    exhausted += spec.VentValve.Vent(pipe, dt);
                if (load == LoadSetting.Empty)
                {
                    // The empty/load equipment holds the emergency to the empty share.
                    var ceiling = Air.Atmosphere + emptyRatio * EmergencyCylinder;
                    FillFlow += spec.EmergencyPort.PassOneWay(AuxiliaryReservoir, Cylinder, dt, ceiling) / dt;
                    FillFlow += spec.EmergencyPort.PassOneWay(EmergencyReservoir, Cylinder, dt, ceiling) / dt;
                }
                else
                {
                    // Both reservoirs open to the cylinder: all three come level.
                    FillFlow += spec.EmergencyPort.Pass(AuxiliaryReservoir, Cylinder, dt) / dt;
                    FillFlow += spec.EmergencyPort.PassOneWay(EmergencyReservoir, Cylinder, dt) / dt;
                }
            }
            else if (Applied)
            {
                if (!quickServiceDone)
                {
                    // Quick service: pipe air into the cylinder up to the limiting valve's pressure, a sharp local drop that carries the application on.
                    var limit = spec.QuickServiceCylinder * (load == LoadSetting.Empty ? emptyRatio : 1);
                    FillFlow += spec.QuickServicePort.PassOneWay(pipe, Cylinder, dt, Air.Atmosphere + limit) / dt;
                    if (Cylinder.Pressure >= Air.Atmosphere + limit - 1)
                        quickServiceDone = true;
                }
                // Service: the auxiliary reservoir into the cylinder until it has fallen to the pipe (lap).
                if (lead > spec.LapBand)
                {
                    var moved = spec.ServicePort.MassFlow(AuxiliaryReservoir.Pressure, Cylinder.Pressure) * dt;
                    // No further than the reservoir falling to the pipe.
                    moved = Math.Min(moved, Math.Max(0, (lead - spec.LapBand / 2) * AuxiliaryReservoir.Volume / Air.RT));
                    moved = Math.Min(moved, AuxiliaryReservoir.MassToEqualize(Cylinder));
                    if (moved > 0)
                    {
                        AuxiliaryReservoir.Mass -= moved;
                        Cylinder.Mass += moved;
                        FillFlow += moved / dt;
                    }
                }
            }
            else if (acceleratingRelease)
            {
                exhausted += Exhaust(dt, false);
                if (spec.AcceleratedRelease.PassOneWay(EmergencyReservoir, pipe, dt) <= 0)
                    acceleratingRelease = false;
            }
            else
            {
                // Near the engine, the pipe stands well over the reservoir: a retarded release, and a slow recharge. Further
                // back the pipe rises no faster than the reservoir charges, and the car releases in full. (The emergency
                // reservoir's own air raises the pipe at every car, so it doesn't count while it flows.)
                Retarded = spec.RetardedRelease && pipe.Pressure - AuxiliaryReservoir.Pressure > spec.RetardThreshold;
                exhausted += Exhaust(dt, Retarded);
                var charging = Retarded ? dt * spec.RetardedChargeShare : dt;
                var charged = spec.AuxiliaryCharging.PassOneWay(pipe, AuxiliaryReservoir, charging);
                charged += auxiliaryBackflow.Pass(pipe, AuxiliaryReservoir, charging);
                charged += spec.EmergencyCharging.PassOneWay(pipe, EmergencyReservoir, dt);
                ChargeFlow = Math.Max(0, charged) / dt;
            }

            PipeDraw = (pipeBefore - pipe.Mass) / dt;
            ExhaustFlow = exhausted / dt;
        }

        private void Release()
        {
            // A service release starts with the emergency reservoir's air into the pipe; after an emergency that air is in the cylinder.
            acceleratingRelease = !Emergency;
            Applied = false;
            Emergency = false;
        }

        /// <summary>Whether the valve stands in retarded release, its exhaust restricted.</summary>
        public bool Retarded { get; private set; }

        /// <summary>
        /// Exhausts the cylinder through the valve's release port, or its
        /// retarded one, and the retaining valve: holding (HP or LP), the
        /// retainer vents through its own small port down to its pressure.
        /// Returns the air let out, kg.
        /// </summary>
        private double Exhaust(double dt, bool retarded)
        {
            var port = retarded ? spec.RetardedReleasePort : spec.ReleasePort;
            switch (Retainer)
            {
                case Retainer.HighPressure:
                    return Smaller(port, spec.RetainerVent).Vent(Cylinder, dt, Air.Atmosphere + spec.HighRetained);
                case Retainer.LowPressure:
                    return Smaller(port, spec.RetainerVent).Vent(Cylinder, dt, Air.Atmosphere + spec.LowRetained);
                case Retainer.SlowDirect:
                    return Smaller(port, spec.SlowDirectPort).Vent(Cylinder, dt);
                default:
                    return port.Vent(Cylinder, dt);
            }
        }

        private static Orifice Smaller(Orifice a, Orifice b) => a.EffectiveArea <= b.EffectiveArea ? a : b;
    }
}
