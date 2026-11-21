using System;
using System.Collections.Generic;

namespace RealisticBrakes.Core
{
 public sealed class VacuumPump
    {
        private readonly double[] inches;
        private readonly double[] flows;

 public VacuumPump(IReadOnlyList<(double Inches, double Flow)> curve)
        {
            if (curve == null || curve.Count < 2)
                throw new ArgumentException("A pump's curve needs at least two points.", nameof(curve));
            var sorted = new List<(double Inches, double Flow)>(curve);
            sorted.Sort((a, b) => a.Inches.CompareTo(b.Inches));
            inches = new double[sorted.Count];
            flows = new double[sorted.Count];
            for (var i = 0; i < sorted.Count; i++)
            {
                inches[i] = sorted[i].Inches;
                flows[i] = Math.Max(0, sorted[i].Flow);
            }
        }

 public static VacuumPump HoldingAgainstHoles(params (double HoleInches, double InchesHeld)[] tests)
        {
            if (tests == null || tests.Length < 2)
                throw new ArgumentException("A pump needs at least two tests.", nameof(tests));
            var points = new List<(double Inches, double Flow)>();
            foreach (var (hole, held) in tests)
                points.Add((held, new Orifice(hole * 25.4, 0.6).MassFlow(Air.Atmosphere, Vacuum.FromInches(held))));
            points.Sort((a, b) => a.Inches.CompareTo(b.Inches));
            var (v0, q0) = points[0];
            var (v1, q1) = points[1];
            var open = q0 + (q0 - q1) / (v1 - v0) * v0;
            var (vn, qn) = points[points.Count - 1];
            var (vm, qm) = points[points.Count - 2];
            var ultimate = vn + qn * (vn - vm) / (qm - qn);
            points.Insert(0, (0, open));
            points.Add((ultimate, 0));
            return new VacuumPump(points);
        }

 public VacuumPump Scaled(double factor)
        {
            var curve = new (double, double)[inches.Length];
            for (var i = 0; i < inches.Length; i++)
                curve[i] = (inches[i], flows[i] * factor);
            return new VacuumPump(curve);
        }

 public double Ultimate => inches[inches.Length - 1];

        /// <summary>The air it draws from a pipe at <paramref name="pressure"/> Pa, kg/s.</summary>
        public double Capacity(double pressure)
        {
            var v = Vacuum.ToInches(pressure);
            if (v <= inches[0])
                return flows[0];
            for (var i = 1; i < inches.Length; i++)
                if (v <= inches[i])
                    return flows[i - 1] + (flows[i] - flows[i - 1]) * (v - inches[i - 1]) / (inches[i] - inches[i - 1]);
            return 0;
        }

        /// <summary>Draws air out of <paramref name="chamber"/> for <paramref name="dt"/> s, at <paramref name="share"/> of its capacity. Returns the mass drawn, kg.</summary>
        public double Draw(Chamber chamber, double dt, double share = 1)
        {
            var moved = Capacity(chamber.Pressure) * Math.Max(0, share) * dt;
            moved = Math.Min(moved, Math.Max(0, -chamber.MassToReach(Vacuum.FromInches(Ultimate))));
            chamber.Mass -= moved;
            return moved;
        }
    }

 public enum VacuumValvePosition
    {
 Release,

 Running,

 Lap,

        /// <summary>Air let into the pipe through the application port while it stands here.</summary>
        Apply,

 Service,

 Emergency,
    }

 public sealed class VacuumEngineSpec
    {
        /// <summary>Whether the engine brakes by steam (a steam engine) rather than air (a diesel).</summary>
        public bool SteamBrake { get; set; }

 public VacuumPump SmallEjector { get; set; } = VacuumPump.HoldingAgainstHoles((1 / 8.0, 23), (1 / 4.0, 22), (5 / 16.0, 19.4), (3 / 8.0, 16.4), (1 / 2.0, 8.7));

 public VacuumPump LargeEjector { get; set; }

 public double SmallEjectorSteam { get; set; } = 300 * 0.45359237 / 3600;

 public double LargeEjectorSteam { get; set; } = 750 * 0.45359237 / 3600;

        /// <summary>The relief valve's setting, Pa: 21 in (LPC 1921 p.80).</summary>
        public double ReliefSetting { get; set; } = Vacuum.FromInches(Vacuum.Working);

        /// <summary>How far past its setting the relief valve opens fully, Pa: a diaphragm valve holds within ¼ in (Hunter 1962).</summary>
        public double ReliefBand { get; set; } = 0.25 * Vacuum.Inch;

 public Orifice ReliefPort { get; set; } = new Orifice(25);

        /// <summary>The brake valve's application port (inferred).</summary>
        public Orifice ApplicationPort { get; set; } = new Orifice(0.75 * 25.4);

        /// <summary>The brake valve's emergency port: 2 in (Marsh 1952 p.101).</summary>
        public Orifice EmergencyPort { get; set; } = new Orifice(2 * 25.4);

        /// <summary>How far off its target a self-lapping valve opens fully, Pa (inferred: ¼ in, as the relief valve).</summary>
        public double LapBand { get; set; } = 0.25 * Vacuum.Inch;

 public double OwnBrakeOffAt { get; set; } = Vacuum.FromInches(Vacuum.Working);

 public double SteamBrakeMaximum { get; set; } = 180 * 6894.757;

        /// <summary>
        /// The steam brake's cylinders on the engine and tender, m³ (inferred:
        /// two 10 in cylinders of 12 in stroke and the tender's): their steam,
        /// taken as filled, is about half a kilogram a cubic metre a bar.
        /// </summary>
        public double SteamBrakeVolume { get; set; } = 0.03;

        /// <summary>The steam brake's times to follow its valve, s (inferred).</summary>
        public double SteamBrakeApplyTime { get; set; } = 1.0;

        /// <inheritdoc cref="SteamBrakeApplyTime"/>
        public double SteamBrakeReleaseTime { get; set; } = 2.0;

        /// <summary>A diesel's main reservoir, m³.</summary>
        public double MainReservoirVolume { get; set; } = 1.0;

        /// <summary>A diesel's brake cylinders, m³, and their relay's ports, as the air engines' (<see cref="LocomotiveBrakeSpec"/>).</summary>
        public double CylinderVolume { get; set; } = 0.03;

        /// <inheritdoc cref="CylinderVolume"/>
        public Orifice CylinderSupply { get; set; } = new Orifice(5.5);

        /// <inheritdoc cref="CylinderVolume"/>
        public Orifice CylinderExhaust { get; set; } = new Orifice(8.5);

        /// <summary>A diesel's full cylinder, from its proportional valve or its independent, gauge Pa.</summary>
        public double AirBrakeMaximum { get; set; } = 3.8 * Air.Bar;

        public VacuumEngineSpec()
        {
            LargeEjector = SmallEjector.Scaled(1.75);
        }

 public static VacuumEngineSpec Steam() => new VacuumEngineSpec
        {
            SteamBrake = true,
            OwnBrakeOffAt = Vacuum.FromInches(20.5),
        };

 public static VacuumEngineSpec Diesel() => new VacuumEngineSpec();
    }

 public sealed class VacuumEngine
    {
        private readonly VacuumEngineSpec spec;
        private readonly Chamber cylinder;
        private readonly Chamber[] chambers;

        public VacuumEngine(VacuumEngineSpec spec = null)
        {
            this.spec = spec ?? VacuumEngineSpec.Diesel();
            MainReservoir = new Chamber(this.spec.MainReservoirVolume);
            cylinder = new Chamber(this.spec.SteamBrake ? 0.001 : this.spec.CylinderVolume);
            // The cylinder too on a steam engine, where it only mirrors the steam brake: the line then stays awake until that has settled.
            chambers = new[] { MainReservoir, cylinder };
        }

        public VacuumEngineSpec Spec => spec;

        /// <summary>The main reservoir: a diesel's own brake works from it; a steam engine's stands unused.</summary>
        public Chamber MainReservoir { get; }

        /// <summary>A diesel's brake cylinders; a steam engine's air equivalent of its steam brake, for the game.</summary>
        public Chamber Cylinder => cylinder;

        public IReadOnlyList<Chamber> Chambers => chambers;

        /// <summary>Where the brake valve stands.</summary>
        public VacuumValvePosition Position { get; set; } = VacuumValvePosition.Running;

 public double Target { get; set; } = Vacuum.FromInches(Vacuum.Working);

        /// <summary>Whether the brake valve works the pipe; a trailing engine's is cut out.</summary>
        public bool CutIn { get; set; } = true;

 public bool PumpOn { get; set; } = true;

 public double Supply { get; set; } = 1;

        /// <summary>The independent's demand, 0 to 1 of the engine's full brake.</summary>
        public double Independent { get; set; }

        /// <summary>A diesel's compressor feeding its main reservoir, kg/s.</summary>
        public double CompressorFlow { get; set; }

        /// <summary>The steam brake's pressure, gauge Pa.</summary>
        public double SteamBrakePressure { get; private set; }

        /// <summary>The share of its full brake the engine has on now.</summary>
        public double BrakeShare { get; private set; }

 public double AutomaticShare { get; private set; }

        /// <summary>The share of <see cref="AutomaticShare"/> bailed off.</summary>
        public double BailedOff { get; set; }

 public bool CanBailOff => !spec.SteamBrake;

 public double SteamFlow { get; private set; }

 public double PumpFlow { get; private set; }

        /// <summary>The air let into the pipe in the last step (the brake valve and the relief valve), kg/s.</summary>
        public double AdmitFlow { get; private set; }

        /// <summary>The air the brake valve's own ports let into the pipe in the last step, kg/s: an application, not the relief valve's steady bleed.</summary>
        public double ValveAdmitFlow { get; private set; }

        /// <summary>A diesel's cylinders' exhaust in the last step, kg/s.</summary>
        public double ExhaustFlow { get; private set; }

        /// <summary>
        /// Puts the engine's own brake as it stood, at <paramref name="share"/>
        /// of its full (a save, or the game's stock cylinder): a steam engine's
        /// steam brake; a diesel's cylinders come from the stock field itself.
        /// </summary>
        public void RestoreOwnBrake(double share)
        {
            share = Math.Max(0, Math.Min(1, share));
            if (spec.SteamBrake)
            {
                SteamBrakePressure = share * spec.SteamBrakeMaximum;
                BrakeShare = share;
                cylinder.Pressure = Air.Atmosphere + (share > 1e-4 ? BrakeRigging.StockTakeUp + share * BrakeRigging.StockSpan : 0);
            }
            else
            {
                cylinder.Pressure = Air.Atmosphere + share * spec.AirBrakeMaximum;
                BrakeShare = share;
            }
        }

        /// <summary>Charges the engine to the pipe's <paramref name="pressure"/> Pa, its own brake off.</summary>
        public void ChargeTo(double pressure)
        {
            cylinder.Pressure = Air.Atmosphere;
            SteamBrakePressure = 0;
            BrakeShare = 0;
            AutomaticShare = 0;
            BailedOff = 0;
        }

        /// <summary>
        /// Lets the engine's automatic brake off, the train's staying on, by
        /// the rules of the air engines' bail: it stays off while the
        /// application holds, a further application puts it back by that much,
        /// and a release clears it. Returns the share bailed (0: nothing to
        /// bail, or a steam brake, which can't be).
        /// </summary>
        public double BailOff()
        {
            if (!CanBailOff)
                return 0;
            var bailed = Math.Max(0, AutomaticShare - BailedOff);
            BailedOff = AutomaticShare;
            return bailed;
        }

        public void Step(Chamber pipe, double dt)
        {
            var pumped = 0.0;
            var admitted = 0.0;
            var large = false;
            if (CutIn)
            {
                switch (Position)
                {
                    case VacuumValvePosition.Release:
                        large = true;
                        pumped += Pump(pipe, dt, 1);
                        break;
                    case VacuumValvePosition.Running:
                        pumped += Pump(pipe, dt, 1);
                        break;
                    case VacuumValvePosition.Apply:
                        admitted -= spec.ApplicationPort.OpenToAtmosphere(pipe, dt);
                        break;
                    case VacuumValvePosition.Service:
                        var excess = Target - pipe.Pressure;
                        if (excess > 0)
                            admitted -= spec.ApplicationPort.Opened(excess / spec.LapBand).OpenToAtmosphere(pipe, dt);
                        else
                            pumped += Pump(pipe, dt, -excess / spec.LapBand);
                        break;
                    case VacuumValvePosition.Emergency:
                        admitted -= spec.EmergencyPort.OpenToAtmosphere(pipe, dt);
                        break;
                }
            }
            var byValve = admitted;
            var over = spec.ReliefSetting - pipe.Pressure;
            if (over > 0)
                admitted -= spec.ReliefPort.Opened(over / spec.ReliefBand).OpenToAtmosphere(pipe, dt);

            var supply = spec.SteamBrake ? Math.Max(0, Math.Min(1, Supply)) : 1;
            SteamFlow = spec.SteamBrake ? supply * ((PumpOn ? spec.SmallEjectorSteam : 0) + (large && CutIn ? spec.LargeEjectorSteam : 0)) : 0;
            PumpFlow = pumped / dt;
            AdmitFlow = Math.Max(0, admitted) / dt;
            ValveAdmitFlow = Math.Max(0, byValve) / dt;

            if (CompressorFlow > 0)
                MainReservoir.Mass += CompressorFlow * dt;
            AutomaticShare = Math.Max(0, Math.Min(1, (pipe.Pressure - spec.OwnBrakeOffAt) / (Air.Atmosphere - spec.OwnBrakeOffAt)));
            // A release shrinks the bail with the application, so what's bailed never outlasts it.
            BailedOff = CanBailOff ? Math.Max(0, Math.Min(BailedOff, AutomaticShare)) : 0;
            var demand = Math.Max(AutomaticShare - BailedOff, Math.Max(0, Math.Min(1, Independent)));
            ExhaustFlow = 0;
            if (spec.SteamBrake)
                StepSteamBrake(demand, dt);
            else
                StepAirBrake(demand, dt);
        }

        private double Pump(Chamber pipe, double dt, double share)
        {
            if (share <= 0 || (!PumpOn && !spec.SteamBrake))
                return 0;
            share = Math.Min(1, share) * (spec.SteamBrake ? Math.Max(0, Math.Min(1, Supply)) : 1);
            var drawn = PumpOn ? spec.SmallEjector.Draw(pipe, dt, share) : 0;
            if (Position == VacuumValvePosition.Release)
                drawn += spec.LargeEjector.Draw(pipe, dt, share);
            return drawn;
        }

        /// <summary>Saturated steam's density per pascal near 180 psi, kg/(m³·Pa): about 6.9 kg/m³ at 13.4 bar absolute.</summary>
        private const double SteamDensityPerPascal = 6.9 / 13.4e5;

        private void StepSteamBrake(double demand, double dt)
        {
            var target = demand * spec.SteamBrakeMaximum;
            var time = target > SteamBrakePressure ? spec.SteamBrakeApplyTime : spec.SteamBrakeReleaseTime;
            var rise = (target - SteamBrakePressure) * Math.Min(1, dt / time);
            SteamBrakePressure += rise;
            // Filling the cylinders takes boiler steam; letting them off blows it away.
            if (rise > 0)
                SteamFlow += spec.SteamBrakeVolume * SteamDensityPerPascal * rise / dt;
            BrakeShare = SteamBrakePressure / spec.SteamBrakeMaximum;
            cylinder.Pressure = Air.Atmosphere + (BrakeShare > 1e-4 ? BrakeRigging.StockTakeUp + BrakeShare * BrakeRigging.StockSpan : 0);
        }

        // The relay fills or exhausts the cylinders toward the demand, opening in proportion over 0.1 bar.
        private void StepAirBrake(double demand, double dt)
        {
            var target = Air.Atmosphere + demand * spec.AirBrakeMaximum;
            var error = target - cylinder.Pressure;
            const double band = 0.1 * Air.Bar;
            if (error > 0)
                spec.CylinderSupply.Opened(error / band).PassOneWay(MainReservoir, cylinder, dt, target);
            else if (error < 0)
                ExhaustFlow = spec.CylinderExhaust.Opened(-error / band).Vent(cylinder, dt, target) / dt;
            BrakeShare = Math.Max(0, cylinder.Pressure - Air.Atmosphere) / spec.AirBrakeMaximum;
        }
    }
}
