using System;
using System.Collections.Generic;
using DV;
using DV.Simulation.Brake;
using DV.ThingTypes;
using HarmonyLib;
using RealisticBrakes.Controls;
using RealisticBrakes.Core;
using UnityEngine;

namespace RealisticBrakes.Sim
{
 internal sealed class CarAir
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

        /// <summary>The car's pipe pressure, bar absolute, for its gauge.</summary>
        public float PipeBar => Bar(Brake.PipeSection.Pressure);

        /// <summary>Builds a car's air from its brake setup and the pressures the game holds for it now.</summary>
        public static CarAir Create(BrakeSystem system, float pipePressure, int generation)
        {
            var car = TrainCar.Resolve(system.gameObject);
            var length = car != null && car.InterCouplerDistance > 1f ? car.InterCouplerDistance : 12f;
            var geometry = BrakeSets.Geometry;
            var pipe = Pa(pipePressure);
            CarBrake brake;
            if (system.hasCompressor)
            {
                var spec = new LocomotiveBrakeSpec();
                if (system.mainResVolume > 0)
                    spec.MainReservoirVolume = system.mainResVolume / 1000.0;
                var valveSpec = Main.Settings.Equipment == Equipment.American ? DriverBrakeValveSpec.American() : DriverBrakeValveSpec.Uic();
                var engine = new LocomotiveBrake(spec, system.hasTrainBrake ? new DriverBrakeValve(valveSpec) : null);
                brake = new CarBrake(length, geometry, locomotive: engine);
                engine.MainReservoir.Pressure = Pa(MainReservoirUnsmoothed(system));
                engine.Distributor.Restore(Pa(system.controlReservoirPressure), Mathf.Max(system.auxReservoirPressure, pipePressure) * Air.Bar, Air.Atmosphere, pipe);
                engine.Cylinder.Pressure = Pa(Mathf.Max(1f, system.brakeCylinderPressureUnsmoothed));
                engine.DriverValve?.ChargeTo(pipe);
            }
            else if (system.brakeCylinderPressureCalculation == BrakeSystem.BrakeCylinderPressureCalculation.Regular)
            {
                if (Main.Settings.Equipment == Equipment.American)
                {
                    // The stock brake keeps no emergency reservoir: it starts as charged as the auxiliary (a save's own key restores it).
                    var valve = new AarControlValve();
                    var aux = Pa(system.auxReservoirPressure);
                    valve.Restore(aux, aux, Pa(system.brakeCylinderPressureUnsmoothed), pipe);
                    brake = new CarBrake(length, geometry, valve);
                }
                else
                {
                    var valve = new UicDistributor { Mode = ModeFor(car) };
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

        // Each car type's heaviest cargo, kg, once worked out.
        private static readonly Dictionary<TrainCarType_v2, float> heaviestCargo = new Dictionary<TrainCarType_v2, float>();

        /// <summary>A freight car's rigging, sized for its type's heaviest cargo; null for a car that carries none.</summary>
        internal static BrakeRigging RiggingFor(TrainCar car)
        {
            var type = car != null && car.carLivery != null ? car.carLivery.parentType : null;
            if (type == null || type.mass <= 0 || car.brakeSystem == null || car.brakeSystem.hasCompressor)
                return null;
            if (!heaviestCargo.TryGetValue(type, out var cargo))
            {
                cargo = 0f;
                var cargos = Globals.G?.Types?.cargos;
                if (cargos != null)
                    foreach (var c in cargos)
                        if (c != null && c.loadableCarTypes != null)
                            foreach (var loadable in c.loadableCarTypes)
                                if (loadable != null && loadable.carType == type)
                                    cargo = Mathf.Max(cargo, c.massPerUnit);
                heaviestCargo[type] = cargo;
            }
            return cargo > 0f ? new BrakeRigging(type.mass, type.mass + cargo * car.cargoCapacity) : null;
        }

        // Whether the bogies' force per kg is the model's rather than the game's.
        private bool forceOverridden;

        /// <summary>The freight car's force per kg as its rigging set it this frame, before the hold's scaling (<see cref="HandBrakeHold"/>).</summary>
        public float RiggedPerKg { get; private set; }

 private bool ApplyRigging()
        {
            var valve = Brake.Valve;
            if (Rigging == null || valve == null || Car == null)
                return false;
            if (!Main.Settings.BrakeForceFromCar)
            {
                RestoreForce();
                RiggedPerKg = Car.carLivery.parentType.brakes.MaxBrakingForcePerBogie * StockForcePerKg;
                var was = valve.Load;
                valve.Load = LoadSetting.Loaded;
                return was != valve.Load;
            }
            var mass = Car.massController != null ? Car.massController.TotalMass : 0f;
            if (mass <= 0f)
                return false;
            var before = valve.Load;
            // European cars' lever by hand when the setting says so; American empty/load equipment is always automatic.
            if (!(Main.Settings.ManualEmptyLoaded && Brake.Distributor != null))
                valve.Load = Rigging.SettingFor(mass);

            var multiplier = (float)Rigging.ForceMultiplier(BrakeRigging.StockBrakingFactor(Brake.CylinderGauge), System.handbrakePosition, mass);
            var perKg = Car.carLivery.parentType.brakes.MaxBrakingForcePerBogie * StockForcePerKg * multiplier;
            RiggedPerKg = perKg;
            foreach (var bogie in Car.Bogies)
                if (bogie != null)
                    bogie.maxBrakingForcePerKg = perKg;
            forceOverridden = true;
            return before != valve.Load;
        }

        /// <summary>Gives the bogies back the game's own force per kg.</summary>
        public void RestoreForce()
        {
            if (!forceOverridden || Car == null)
                return;
            var stockPerKg = Car.carLivery.parentType.brakes.MaxBrakingForcePerBogie * StockForcePerKg;
            foreach (var bogie in Car.Bogies)
                if (bogie != null)
                    bogie.maxBrakingForcePerKg = stockPerKg;
            forceOverridden = false;
        }

        /// <summary>The game's per-kg factor on a car type's force per bogie (<c>Bogie.Awake</c>: 1/11,500).</summary>
        internal const float StockForcePerKg = 8.695652E-05f;

        private static BrakingMode ModeFor(TrainCar car)
        {
            var type = car != null && car.carLivery != null ? car.carLivery.parentType : null;
            if (type != null && type.id == "Passenger")
                return BrakingMode.Passenger;
            return Main.Settings.FreightMode;
        }

        /// <summary>
        /// Reads the handles, the release valve, the compressor, and the
        /// reservoir's size before the line runs. Returns whether any of them,
        /// or a value set from outside, changed since the last frame, which
        /// wakes a settled line.
        /// </summary>
        public bool BeforeStep(float pumpSpeedUp)
        {
            var bs = System;
            var changed = touched | ApplyRigging();
            touched = false;
            // The game's bleed handle: a press is a short pull; held past a few seconds (BleedHandle), it drains the reservoirs.
            var engine = Brake.Locomotive;
            if (bs.controlReservoirPressureReleased)
            {
                Brake.Valve?.PullRelease();
                engine?.Distributor.PullRelease();
                bs.controlReservoirPressureReleased = false;
                changed = true;
            }
            if (Brake.Valve != null && Brake.Valve.ReleaseHeld != ReleaseHeld)
            {
                Brake.Valve.ReleaseHeld = ReleaseHeld;
                changed = true;
            }
            if (engine != null && engine.Distributor.ReleaseHeld != ReleaseHeld)
            {
                engine.Distributor.ReleaseHeld = ReleaseHeld;
                changed = true;
            }

            if (engine == null)
                return changed;
            if (bs.mainResVolume > 0 && Mathf.Abs(bs.mainResVolume / 1000f - (float)engine.MainReservoir.Volume) > 1e-4f)
            {
                engine.MainReservoir.Resize(bs.mainResVolume / 1000.0);
                changed = true;
            }
            // Another mod writing the stock field directly, past the setters.
            var stock = MainReservoirUnsmoothed(bs);
            if (!float.IsNaN(writtenMainReservoir) && Mathf.Abs(stock - writtenMainReservoir) > 1e-4f)
            {
                engine.MainReservoir.Pressure = Pa(stock);
                changed = true;
            }
            var compressing = bs.compressorProductionRate > 0.001f;
            // A widened handle's new notches read as their stock neighbors: only the handle knows them.
            var zone = TrainBrakeHandle.ZoneOf(bs);
            if (bs.trainBrakePosition != lastTrain || bs.independentBrakePosition != lastIndependent
                || bs.trainBrakeCutout != lastCutout || compressing || compressing != lastCompressing || zone != lastZone)
                changed = true;
            lastZone = zone;
            lastTrain = bs.trainBrakePosition;
            lastIndependent = bs.independentBrakePosition;
            lastCutout = bs.trainBrakeCutout;
            lastCompressing = compressing;

            var valve = engine.DriverValve;
            if (valve != null)
            {
                valve.CutIn = bs.trainBrakeCutout;
                var handle = bs.trainBrakePosition;
                // A widened handle's added notches: a diesel's filling stroke or a steam engine's release (the H-6's), main reservoir air straight into the pipe; a diesel's emergency.
                if (zone == HandleZone.Fill)
                    valve.Position = DriverValvePosition.Fill;
                else if (zone == HandleZone.Emergency)
                    valve.Position = DriverValvePosition.Emergency;
                else if (bs.selfLappingController)
                {
                    var t = bs.trainBrakeCurve != null ? bs.trainBrakeCurve.Evaluate(handle) : handle;
                    if (t <= 0.001f)
                        valve.Position = DriverValvePosition.Running;
                    else
                    {
                        valve.Position = DriverValvePosition.Service;
                        valve.ServiceReduction = Mathf.Clamp01(t) * valve.Spec.FullService;
                    }
                }
                else
                {
                    // The game's manual-lap zones: running, lap, apply, emergency.
                    valve.Position = handle > 0.9f ? DriverValvePosition.Emergency
                        : handle > 0.5f ? DriverValvePosition.Apply
                        : handle > 0.1f ? DriverValvePosition.Lap
                        : DriverValvePosition.Running;
                }
            }
            var independent = bs.independentBrakePosition;
            engine.Independent = bs.hasIndependentBrake ? (bs.indBrakeCurve != null ? bs.indBrakeCurve.Evaluate(independent) : independent) : 0;

            // The pump's own rate times the game's Main Reservoir Fill Time, as the stock compressor runs.
            var rate = bs.compressorProductionRate * pumpSpeedUp;
            engine.CompressorFlow = compressing ? rate * Air.Bar * 1e-3 / Air.RT : 0;
            return changed;
        }

        /// <summary>Notes a value set from outside (a stock setter), which wakes the car's line.</summary>
        public void Touch() => touched = true;

        /// <summary>The car's line is settled and skipped this frame: no air moves, so the audio's flows are nothing.</summary>
        public void Rest()
        {
            PipeFlow = 0;
            ExhaustFlow = 0;
        }

        /// <summary>Writes the model's pressures into the stock fields after the line ran.</summary>
        public void AfterStep()
        {
            var bs = System;
            var valve = Brake.Valve;
            var engine = Brake.Locomotive;
            var pipeIn = 0.0;
            var exhaust = 0.0;
            if (valve != null)
            {
                bs.auxReservoirPressure = Bar(valve.AuxiliaryReservoir.Pressure);
                // An American valve has no control reservoir; its auxiliary stands in, so the stock brake, if it takes over, sees no reduction.
                bs.controlReservoirPressure = Bar(Brake.Distributor?.ControlReservoir.Pressure ?? valve.AuxiliaryReservoir.Pressure);
                bs.brakeCylinderPressureUnsmoothed = Bar(valve.Cylinder.Pressure);
                // As the stock brake counts it: the reservoirs' charging and the cylinder's filling.
                pipeIn += valve.ChargeFlow + valve.FillFlow;
                exhaust += valve.ExhaustFlow;
            }
            if (engine != null)
            {
                if (Air.ToAbsoluteBar(engine.MainReservoir.Pressure) > MainReservoirCeiling)
                    engine.MainReservoir.Pressure = Air.FromAbsoluteBar(MainReservoirCeiling);
                bs.auxReservoirPressure = Bar(engine.Distributor.AuxiliaryReservoir.Pressure);
                bs.controlReservoirPressure = Bar(engine.Distributor.ControlReservoir.Pressure);
                bs.brakeCylinderPressureUnsmoothed = Bar(engine.Cylinder.Pressure);
                exhaust += engine.ExhaustFlow;
                pipeIn += engine.FillFlow;
                if (engine.DriverValve != null)
                {
                    pipeIn += engine.DriverValve.FeedFlow;
                    exhaust += engine.DriverValve.ExhaustFlow;
                }

                ref var mr = ref MainReservoirUnsmoothed(bs);
                var before = mr;
                mr = Bar(engine.MainReservoir.Pressure);
                writtenMainReservoir = mr;
                if (Mathf.Abs(bs.mainReservoirPressure - mr) > 0.0001f || before != mr)
                    bs.Fire_MainReservoirPressureChanged();
                // The game's governor.
                if (!bs.compressorActivationSignal && mr < bs.compressorActivationPressure)
                    bs.ToggleCompressorActivationSignal();
                else if (bs.compressorActivationSignal && mr >= MainReservoirCeiling)
                    bs.ToggleCompressorActivationSignal();
            }
            PipeFlow = (float)pipeIn * StockFlowPerKgPerSecond;
            ExhaustFlow = (float)exhaust * StockFlowPerKgPerSecond;
        }

        internal static float Bar(double pascals) => (float)Air.ToAbsoluteBar(pascals);

        internal static double Pa(float absoluteBar) => Air.FromAbsoluteBar(Mathf.Max(0f, absoluteBar));
    }
}
