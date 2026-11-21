using DV.Simulation.Brake;
using DV.ThingTypes;
using HarmonyLib;
using LocoSim.Implementations;
using RealisticBrakes.Controls;
using RealisticBrakes.Core;
using UnityEngine;

namespace RealisticBrakes.Sim
{
 internal sealed partial class CarAir
    {
        /// <summary>The internal field holding the main reservoir's pressure before the readout's smoothing.</summary>
        internal static readonly AccessTools.FieldRef<BrakeSystem, float> MainReservoirUnsmoothed =
            AccessTools.FieldRefAccess<BrakeSystem, float>("mainReservoirPressureUnsmoothed");

        /// <summary>The game's brake pipe volume units per our bar·L, for the audio's flow curves (inferred: its pipe of 10 units a car stands for about 10 L).</summary>
        private const float StockFlowPerKgPerSecond = (float)(Air.RT / Air.Bar * 1000);

        /// <summary>The game's main reservoir ceiling, bar absolute (its compressor clamps there).</summary>
        private const float MainReservoirCeiling = 9f;

        private CarAir(BrakeSystem system, TrainCar car, CarBrake brake, int generation, BrakeRigging rigging)
        {
            System = system;
            Car = car;
            Brake = brake;
            Generation = generation;
            Rigging = rigging;
            if (rigging != null && brake.Valve != null)
            {
                brake.Valve.SetEmptyCylinder(rigging.EmptyCylinder);
                // A car arrives with its empty/loaded lever set for its load; a save's own lever is applied after this.
                var mass = car != null && car.massController != null ? car.massController.TotalMass : 0f;
                if (mass > 0f)
                    brake.Valve.Load = rigging.SettingFor(mass);
            }
            // A rigged car's bogies may hold an earlier model's force (a pooled car, a rebuilt model): the first restore writes the game's.
            forceOverridden = rigging != null;
        }

 public bool IsVacuum { get; private set; }

        /// <summary>The freight car's rigging, whose force is its own rather than its load's; null for cars the game's force stays on (engines, tenders, coaches).</summary>
        public BrakeRigging Rigging { get; }

        /// <summary>The model's run this air belongs to (<see cref="BrakeSets"/>).</summary>
        public int Generation { get; }

        public BrakeSystem System { get; }

        public TrainCar Car { get; }

        public CarBrake Brake { get; }

        /// <summary>The line the car last ran in, so a rebuilt one keeps the hoses' flows.</summary>
        public BrakeLine Line { get; set; }

        /// <summary>Whether the model ran this car this frame, so the stock air methods stand aside.</summary>
        public bool Handled { get; set; }

        /// <summary>Frame stamp written by <see cref="HandBrakeHold"/> while this car's freeze is scaled; 0 when not held.</summary>
        internal int HoldStamp;

        // The main reservoir pressure last written to the stock field, to notice another mod's direct writes.
        private float writtenMainReservoir = float.NaN;

        // The inputs as last read, and whether a stock setter wrote since, to wake a settled line.
        private float lastTrain = float.NaN;
        private float lastIndependent = float.NaN;
        private HandleZone lastZone = HandleZone.Travel;
        private bool lastCutout;
        private bool lastCompressing;
        private bool touched = true;

        /// <summary>Whether the car's bleed handle is held past a short pull, draining its reservoirs (<see cref="Controls.BleedHandle"/>).</summary>
        public bool ReleaseHeld => LocalReleaseHeld || RemoteReleaseHeld;

        /// <summary>Whether this game's player holds the car's bleed handle past a short pull.</summary>
        public bool LocalReleaseHeld { get; set; }

        /// <summary>Whether another player in the session holds it (<see cref="Net"/>).</summary>
        public bool RemoteReleaseHeld { get; set; }

        /// <summary>The flow into the car's pipe this frame, in the game's units, for the audio.</summary>
        public float PipeFlow { get; private set; }

        /// <summary>The air let out this frame, in the game's units, for the audio.</summary>
        public float ExhaustFlow { get; private set; }

        /// <summary>Whether the car has no brake of its own in the model: a tender or slug, whose cylinders the stock code copies from its engine.</summary>
        private bool CopiesItsEngine => Brake.Valve == null && Brake.Locomotive == null && Brake.VacuumEngine == null;

        /// <summary>The car's pipe pressure as the stock field holds it, bar absolute, for its gauge.</summary>
        public float PipeBar => Bar(Stock(Brake.PipeSection.Pressure));

 private double Stock(double pascals) => IsVacuum ? Core.Vacuum.AirEquivalent(pascals) : pascals;

        /// <summary>Builds a car's air from its brake setup and the pressures the game holds for it now.</summary>
        public static CarAir Create(BrakeSystem system, float pipePressure, int generation)
        {
            var car = TrainCar.Resolve(system.gameObject);
            var length = car != null && car.InterCouplerDistance > 1f ? car.InterCouplerDistance : 12f;
            var geometry = BrakeSets.Geometry;
            var pipe = Pa(pipePressure);
            CarBrake brake;
            var american = Main.Settings.Equipment == Equipment.American;
            if (Main.Settings.Equipment == Equipment.Vacuum)
                return CreateVacuum(system, car, length, geometry, pipe, generation);
            if (system.hasCompressor)
            {
                // An engine carries its own prototype's standard (EngineStandards; the cars keep the setting). American
                // manual-lap engines (the steam engines and the DM3) have the E-T equipment: the H-6 automatic and the No. 6
                // distributing valve. The self-lapping diesels have the 26-L.
                var americanEngine = IsAmericanEngine(car);
                var et = americanEngine && system.hasTrainBrake && !system.selfLappingController;
                var spec = et ? LocomotiveBrakeSpec.EtSteam() : americanEngine ? LocomotiveBrakeSpec.American() : new LocomotiveBrakeSpec();
                if (system.mainResVolume > 0)
                    spec.MainReservoirVolume = system.mainResVolume / 1000.0;
                var valveSpec = et ? DriverBrakeValveSpec.H6() : americanEngine ? DriverBrakeValveSpec.American() : DriverBrakeValveSpec.Uic();
                var engine = new LocomotiveBrake(spec, system.hasTrainBrake ? new DriverBrakeValve(valveSpec) : null);
                brake = new CarBrake(length, geometry, locomotive: engine);
                engine.MainReservoir.Pressure = Pa(MainReservoirUnsmoothed(system));
                // The distributor's cylinder is the relay's pilot: it starts where the engine's cylinders stand, so they hold as they were.
                engine.Cylinder.Pressure = Pa(Mathf.Max(1f, system.brakeCylinderPressureUnsmoothed));
                engine.Distributor.Restore(Pa(system.controlReservoirPressure), Mathf.Max(system.auxReservoirPressure, pipePressure) * Air.Bar, engine.Cylinder.Pressure, pipe);
                // The valve takes the pipe over where it stands, an overcharge kept to let down slowly (DriverBrakeValve.TakeOver).
                engine.DriverValve?.TakeOver(pipe);
            }
            else if (system.brakeCylinderPressureCalculation == BrakeSystem.BrakeCylinderPressureCalculation.Regular)
            {
                if (american && !IsCoach(car))
                {
                    // The stock brake keeps no emergency reservoir: it starts as charged as the auxiliary (a save's own key restores it).
                    var valve = new AarControlValve(new AarControlValveSpec { CylinderVolume = AarControlValveSpec.CylinderVolumeAt(PistonTravel(car)) });
                    var aux = Pa(system.auxReservoirPressure);
                    valve.Restore(aux, aux, Pa(system.brakeCylinderPressureUnsmoothed), pipe);
                    brake = new CarBrake(length, geometry, valve);
                }
                else
                {
                    // An American coach has graduated-release passenger equipment, which the distributor in P stands for.
                    var valve = american ? new UicDistributor(UicDistributorSpec.AmericanCoach()) { Mode = BrakingMode.Passenger } : new UicDistributor { Mode = ModeFor(car) };
                    valve.Restore(Pa(system.controlReservoirPressure), Pa(system.auxReservoirPressure), Pa(system.brakeCylinderPressureUnsmoothed), pipe);
                    brake = new CarBrake(length, geometry, valve);
                }
            }
            else
            {
                // A tender or slug copying its engine's cylinders: the stock code still does the copying.
                brake = new CarBrake(length, geometry);
            }
            brake.PipeSection.Pressure = pipe;
            return new CarAir(system, car, brake, generation, RiggingFor(car));
        }

 private static CarAir CreateVacuum(BrakeSystem system, TrainCar car, double length, PipeGeometry geometry, double stockPipe, int generation)
        {
            var pipe = Core.Vacuum.FromAirEquivalent(stockPipe);
            CarBrake brake;
            if (system.hasCompressor)
            {
                var spec = IsSteam(car) ? VacuumEngineSpec.Steam() : VacuumEngineSpec.Diesel();
                if (system.mainResVolume > 0)
                    spec.MainReservoirVolume = system.mainResVolume / 1000.0;
                var engine = new VacuumEngine(spec);
                brake = new CarBrake(length, geometry, vacuumEngine: engine);
                engine.MainReservoir.Pressure = Pa(MainReservoirUnsmoothed(system));
                engine.RestoreOwnBrake(spec.SteamBrake
                    ? BrakeRigging.StockBrakingFactor(Pa(system.brakeCylinderPressureUnsmoothed) - Air.Atmosphere)
                    : (Pa(Mathf.Max(1f, system.brakeCylinderPressureUnsmoothed)) - Air.Atmosphere) / spec.AirBrakeMaximum);
            }
            else if (system.brakeCylinderPressureCalculation == BrakeSystem.BrakeCylinderPressureCalculation.Regular)
            {
                var valve = new VacuumCylinder(IsCoach(car) ? VacuumCylinderSpec.Coach() : VacuumCylinderSpec.Bogie());
                valve.Spec.DirectAdmission = Main.Settings.DirectAdmission;
                // The chamber from the stock auxiliary reservoir, and the underside level with the pipe.
                valve.Restore(Core.Vacuum.FromAirEquivalent(Pa(system.auxReservoirPressure)), pipe);
                brake = new CarBrake(length, geometry, valve);
            }
            else
            {
                // A tender or slug copying its engine's cylinders: the stock code still does the copying.
                brake = new CarBrake(length, geometry);
            }
            brake.PipeSection.Pressure = pipe;
            var air = new CarAir(system, car, brake, generation, RiggingFor(car)) { IsVacuum = true };
            if (brake.VacuumEngine != null && brake.VacuumEngine.Spec.SteamBrake)
            {
                air.steamPump = SteamPumpOf(car);
                if (air.steamPump != null)
                {
                    ejectors.Remove(air.steamPump);
                    ejectors.Add(air.steamPump, air);
                }
                Controls.EjectorSound.Attach(car);
            }
            return air;
        }

        private SteamCompressor steamPump;
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<SteamCompressor, CarAir> ejectors =
            new System.Runtime.CompilerServices.ConditionalWeakTable<SteamCompressor, CarAir>();

 internal static bool TryGetEjectors(SteamCompressor pump, out VacuumEngine engine)
        {
            engine = null;
            if (!ejectors.TryGetValue(pump, out var air) || !BrakeSets.TryGet(air.System, out var current) || current != air)
                return false;
            engine = air.Brake.VacuumEngine;
            return engine != null;
        }

        private static SteamCompressor SteamPumpOf(TrainCar car)
        {
            var flow = car != null && car.SimController != null ? car.SimController.SimulationFlow : null;
            if (flow != null)
                foreach (var component in flow.OrderedSimComps)
                    if (component is SteamCompressor pump)
                        return pump;
            return null;
        }


        /// <summary>Whether <paramref name="car"/> is a steam engine: one with a boiler, stock or Custom Car Loader.</summary>
        internal static bool IsSteam(TrainCar car)
        {
            var flow = car != null && car.SimController != null ? car.SimController.SimulationFlow : null;
            if (flow != null)
                foreach (var component in flow.OrderedSimComps)
                    if (component is Boiler)
                        return true;
            return car != null && car.carLivery != null && CarTypes.IsSteamLocomotive(car.carLivery);
        }

        internal static bool IsAmericanEngine(TrainCar car)
        {
            var settings = Main.Settings;
            if (settings.Equipment == Equipment.Vacuum)
                return false;
            var setting = settings.Equipment == Equipment.American ? AirStandard.American : AirStandard.European;
            var livery = car != null ? car.carLivery : null;
            return EngineStandards.For(settings.EveryEngineUsesSetting, setting, livery?.id, livery?.parentType?.id) == AirStandard.American;
        }

        private static BrakingMode ModeFor(TrainCar car) => IsCoach(car) ? BrakingMode.Passenger : Main.Settings.FreightMode;

        /// <summary>Whether <paramref name="car"/> is a passenger coach.</summary>
        internal static bool IsCoach(TrainCar car)
        {
            var type = car != null && car.carLivery != null ? car.carLivery.parentType : null;
            return type != null && type.id == "Passenger";
        }

        /// <summary>
        /// An American car's piston travel, inches: 7 to 9, as car inspectors
        /// left it (ICS p. 200: "as near 8 in. as possible"), the same for a
        /// car every run and on every player's game.
        /// </summary>
        private static double PistonTravel(TrainCar car)
        {
            var id = car != null && car.logicCar != null ? car.logicCar.carGuid : null;
            if (string.IsNullOrEmpty(id))
                return AarControlValveSpec.NominalTravel;
            // FNV-1a: string hashes differ between runtimes.
            var hash = 2166136261u;
            foreach (var c in id)
                hash = (hash ^ c) * 16777619u;
            return 7 + 2 * (hash % 1001) / 1000.0;
        }

        /// <summary>Notes a value set from outside (a stock setter), which wakes the car's line.</summary>
        public void Touch() => touched = true;

        /// <summary>The car's line is settled and skipped this frame: no air moves, so the audio's flows are nothing.</summary>
        public void Rest()
        {
            PipeFlow = 0;
            ExhaustFlow = 0;
        }

        internal static float Bar(double pascals) => (float)Air.ToAbsoluteBar(pascals);

        internal static double Pa(float absoluteBar) => Air.FromAbsoluteBar(Mathf.Max(0f, absoluteBar));
    }
}
