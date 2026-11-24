using System;
using System.Collections.Generic;

namespace RealisticBrakes.Core
{
    /// <summary>
    /// One car's air brake: its length of brake pipe and what hangs off it.
    /// That is a valve of the car's equipment with its reservoirs and
    /// cylinder on a car, an engine's brake on a locomotive,
    /// or nothing on a car that only passes the pipe through (a tender
    /// copying its engine).
    /// It outlives any one train: cars couple and uncouple, and their pipes
    /// are joined anew, but each car keeps its own air.
    /// </summary>
    public sealed class CarBrake
    {
 public CarBrake(double length, PipeGeometry geometry, ICarValve valve = null, LocomotiveBrake locomotive = null, VacuumEngine vacuumEngine = null)
        {
            if (!(length > 0))
                throw new ArgumentOutOfRangeException(nameof(length), length, "A car needs a positive length.");
            if ((valve != null ? 1 : 0) + (locomotive != null ? 1 : 0) + (vacuumEngine != null ? 1 : 0) > 1)
                throw new ArgumentException("A car has a valve or an engine's brake, not two.");
            Length = length;
            PipeSection = new Chamber((geometry ?? PipeGeometry.Uic()).SectionVolume(length));
            Valve = valve;
            Locomotive = locomotive;
            VacuumEngine = vacuumEngine;
            var chambers = new List<Chamber> { PipeSection };
            if (valve != null)
                chambers.AddRange(valve.Chambers);
            if (vacuumEngine != null)
                chambers.AddRange(vacuumEngine.Chambers);
            if (locomotive != null)
            {
                chambers.Add(locomotive.MainReservoir);
                chambers.Add(locomotive.Cylinder);
                chambers.AddRange(locomotive.Distributor.Chambers);
                if (locomotive.DriverValve != null)
                    chambers.Add(locomotive.DriverValve.EqualizingReservoir);
            }
            Chambers = chambers.ToArray();
        }

        /// <summary>Every chamber of the car's air, its pipe section first.</summary>
        public IReadOnlyList<Chamber> Chambers { get; }

        /// <summary>The car's length over couplers, m.</summary>
        public double Length { get; }

        /// <summary>The car's own length of brake pipe.</summary>
        public Chamber PipeSection { get; }

        /// <summary>The car's valve, of either family, or null.</summary>
        public ICarValve Valve { get; }

        /// <summary>The car's European distributor, or null.</summary>
        public UicDistributor Distributor => Valve as UicDistributor;

        /// <summary>The car's American control valve, or null.</summary>
        public AarControlValve ControlValve => Valve as AarControlValve;

        /// <summary>The engine's air brake, or null.</summary>
        public LocomotiveBrake Locomotive { get; }

 public VacuumCylinder VacuumCylinder => Valve as VacuumCylinder;

 public VacuumEngine VacuumEngine { get; }

        /// <summary>A leak between the car's pipe and the air (whichever way the air runs), or a closed orifice for none.</summary>
        public Orifice PipeLeak { get; set; }

        /// <summary>The outside vent's bore: a brake valve's emergency port (<see cref="DriverBrakeValveSpec.EmergencyPort"/>).</summary>
        public static readonly Orifice OutsideVentPort = new Orifice(25.0);

        /// <summary>
        /// Whether another mod vents the pipe here (<see cref="OutsideVentWatch"/>): opened at the engine working the train
        /// while that mod vents the game's pipe, through <see cref="OutsideVentPort"/>.
        /// </summary>
        public bool OutsideVent { get; set; }

        /// <summary>The air the outside vent let out in the last step, kg/s.</summary>
        public double OutsideVentFlow { get; private set; }

        /// <summary>
        /// The caboose valve's bore, wide open: a ¾ in pipe, as the Westinghouse B-3-A conductor's valve is tapped (ICS
        /// Westinghouse air brake handbook, 1918, p. 401).
        /// </summary>
        public static readonly Orifice CabooseValvePort = new Orifice(19.0);

 public double CabooseValve { get; set; }

        /// <summary>The share of <see cref="CabooseValvePort"/>'s area open at <paramref name="travel"/>: the square, so the first part of the travel makes a gentle reduction and the last an emergency.</summary>
        public static double CabooseValveArea(double travel)
        {
            var t = double.IsNaN(travel) ? 0 : Math.Max(0, Math.Min(1, travel));
            return t * t;
        }

 public double CabooseValveFlow { get; private set; }

        /// <summary>The car's brake cylinder, or null when it has none of its own.</summary>
        public Chamber Cylinder => Locomotive?.Cylinder ?? VacuumEngine?.Cylinder ?? Valve?.Cylinder;

        /// <summary>The car's brake cylinder pressure, gauge Pa (0 without one).</summary>
        public double CylinderGauge => Cylinder == null ? 0 : Math.Max(0, Cylinder.Pressure - Air.Atmosphere);

        /// <summary>
        /// The European distributor's G/P mode, for the public API: the car's, or on an engine its own distributor's;
        /// null on an American (AB) car, which has no changeover, or a car with no valve.
        /// </summary>
        public BrakingMode? BrakingMode => Distributor?.Mode ?? (Valve == null ? Locomotive?.Distributor.Mode : null);

        /// <summary>
        /// Whether the car's own brake is cut out by its isolating cock (it runs unbraked, the pipe passing through), for
        /// the public API; null for a car with no valve.
        /// </summary>
        public bool? CutOut => Valve?.Isolated ?? Locomotive?.Distributor.Isolated;

        /// <summary>
        /// Whether an engine's brake valve is cut out, so it doesn't work the train's pipe (as on a trailing engine), for
        /// the public API; null for a car with no brake valve. Apart from <see cref="CutOut"/>: the engine's own brake.
        /// </summary>
        public bool? BrakeValveCutOut => Locomotive?.DriverValve == null ? (bool?)null : !Locomotive.DriverValve.CutIn;

        /// <summary>Whether this car is an engine whose own brake can be bailed off.</summary>
        public bool CanBail => Locomotive != null || (VacuumEngine != null && VacuumEngine.CanBailOff);

        /// <summary>
        /// <paramref name="part"/>'s pressure now, gauge Pa, for the public API; NaN where the car has no such part.
        /// </summary>
        public double GaugePressure(BrakePart part)
        {
            if (VacuumCylinder != null || VacuumEngine != null)
                return double.NaN;
            Chamber chamber;
            switch (part)
            {
                case BrakePart.BrakePipe:
                    chamber = PipeSection;
                    break;
                case BrakePart.AuxiliaryReservoir:
                    chamber = Valve?.AuxiliaryReservoir ?? Locomotive?.Distributor.AuxiliaryReservoir;
                    break;
                case BrakePart.ControlReservoir:
                    chamber = Distributor?.ControlReservoir ?? (Valve == null ? Locomotive?.Distributor.ControlReservoir : null);
                    break;
                case BrakePart.EmergencyReservoir:
                    chamber = ControlValve?.EmergencyReservoir;
                    break;
                case BrakePart.BrakeCylinder:
                    chamber = Cylinder;
                    break;
                case BrakePart.MainReservoir:
                    chamber = Locomotive?.MainReservoir;
                    break;
                case BrakePart.EqualizingReservoir:
                    chamber = Locomotive?.DriverValve?.EqualizingReservoir;
                    break;
                default:
                    chamber = null;
                    break;
            }
            return chamber == null ? double.NaN : Math.Max(0, chamber.Pressure - Air.Atmosphere);
        }

        /// <summary>Charges the car to <paramref name="pressure"/> Pa, released: found standing charged.</summary>
        public void ChargeTo(double pressure)
        {
            PipeSection.Pressure = pressure;
            Valve?.ChargeTo(pressure);
            VacuumEngine?.ChargeTo(pressure);
            if (Locomotive != null)
            {
                Locomotive.Distributor.ChargeTo(pressure);
                Locomotive.DriverValve?.ChargeTo(pressure);
                Locomotive.HeldAutomatic = 0;
                if (Locomotive.MainReservoir.Pressure < pressure)
                    Locomotive.MainReservoir.Pressure = pressure;
            }
        }

        /// <summary>Advances the car's equipment <paramref name="dt"/> s against its section of pipe.</summary>
        public void Step(double dt)
        {
            Valve?.Step(PipeSection, dt);
            Locomotive?.Step(PipeSection, dt);
            VacuumEngine?.Step(PipeSection, dt);
            if (PipeLeak.IsOpen)
                PipeLeak.OpenToAtmosphere(PipeSection, dt);
            OutsideVentFlow = OutsideVent && dt > 0 ? Math.Abs(OutsideVentPort.OpenToAtmosphere(PipeSection, dt)) / dt : 0;
            var open = CabooseValveArea(CabooseValve);
            CabooseValveFlow = open > 0 && dt > 0 ? Math.Abs(CabooseValvePort.Opened(open).OpenToAtmosphere(PipeSection, dt)) / dt : 0;
        }
    }
}
