using System;
using DV.Simulation.Brake;
using RealisticBrakes.Controls;
using RealisticBrakes.Core;
using UnityEngine;

namespace RealisticBrakes.Sim
{
    internal sealed partial class CarAir
    {
        /// <summary>The game's independent over its travel, gauge Pa: 1 to 4.5 bar absolute (<c>BrakeSystem.SimulateLocoControlValve</c>).</summary>
        internal const double CarIndependentRange = 3.5 * Air.Bar;

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
            var vacuum = Brake.VacuumCylinder;
            if (vacuum != null)
                vacuum.Spec.DirectAdmission = Main.Settings.DirectAdmission;
            // Not on a tender or slug copying its engine's cylinders: the stock code that does the copying takes the pull
            // too, and lets a detached one go. Taken here, the pull was lost and a tender uncoupled with the engine brake
            // on stayed locked.
            if (bs.controlReservoirPressureReleased && !CopiesItsEngine)
            {
                Brake.Valve?.PullRelease();
                engine?.PullRelease();
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
            // The caboose valve (CabooseValve), set by a hand here or another player's: the line stays awake while it blows.
            var caboose = CabooseValve.AnyOpen ? CabooseValve.TravelOf(Car) : 0.0;
            if (Brake.CabooseValve != caboose)
            {
                Brake.CabooseValve = caboose;
                changed = true;
            }
            changed |= caboose > 0;
            // Another mod's independent on an ordinary car (Caboose Brake Handle's on the caboose, Nexus 1774): the game's
            // independent travel, 1 to 4.5 bar absolute, fills its cylinder from its own auxiliary reservoir. Not a tender
            // following its engine, nor an engine, whose own independent this is.
            if (engine == null && Brake.Valve != null && bs.brakeCylinderPressureCalculation == BrakeSystem.BrakeCylinderPressureCalculation.Regular)
            {
                var position = Mathf.Clamp01(bs.independentBrakePosition);
                var carIndependent = (bs.indBrakeCurve != null ? Mathf.Clamp01(bs.indBrakeCurve.Evaluate(position)) : position) * CarIndependentRange;
                if (Math.Abs(Brake.Valve.IndependentCylinder - carIndependent) > 1)
                {
                    Brake.Valve.IndependentCylinder = carIndependent;
                    changed = true;
                }
            }

            var vacuumEngine = Brake.VacuumEngine;
            var mainReservoir = engine?.MainReservoir ?? vacuumEngine?.MainReservoir;
            if (mainReservoir == null)
                return changed;
            if (bs.mainResVolume > 0 && Mathf.Abs(bs.mainResVolume / 1000f - (float)mainReservoir.Volume) > 1e-4f)
            {
                mainReservoir.Resize(bs.mainResVolume / 1000.0);
                changed = true;
            }
            // Another mod writing the stock field directly, past the setters.
            var stock = MainReservoirUnsmoothed(bs);
            if (!float.IsNaN(writtenMainReservoir) && Mathf.Abs(stock - writtenMainReservoir) > 1e-4f)
            {
                mainReservoir.Pressure = Pa(stock);
                changed = true;
            }
            var compressing = bs.compressorProductionRate > 0.001f;
            // A widened handle's new notches read as their stock neighbors: only the handle knows them.
            var zone = TrainBrakeHandle.ZoneOf(bs);
            // A value set from outside (the remote, a cable, a driver mod) that moves the handle off what its added notch
            // reads as takes it out of that notch (the review, 2026-10-28: an apply and release by remote left it in holding).
            if (zone != HandleZone.Travel && bs.trainBrakePosition != lastTrain && !TrainBrakeHandle.Stands(zone, bs.trainBrakePosition))
            {
                TrainBrakeHandle.ClearZone(bs);
                zone = HandleZone.Travel;
            }
            if (bs.trainBrakePosition != lastTrain || bs.independentBrakePosition != lastIndependent
                || bs.trainBrakeCutout != lastCutout || compressing || compressing != lastCompressing || zone != lastZone
                || (engine != null && engine.HeldInRelease))
                changed = true;
            lastZone = zone;
            lastTrain = bs.trainBrakePosition;
            lastIndependent = bs.independentBrakePosition;
            lastCutout = bs.trainBrakeCutout;
            lastCompressing = compressing;

            if (vacuumEngine != null)
            {
                ReadVacuumHandles(vacuumEngine, zone, compressing, pumpSpeedUp);
                changed |= supplyChanged;
                supplyChanged = false;
                return changed;
            }
            var valve = engine.DriverValve;
            if (valve != null)
            {
                valve.CutIn = bs.trainBrakeCutout;
                valve.Adjusting = Controls.AdjustKey.Held(Car);
                var handle = bs.trainBrakePosition;
                // A widened handle's added notches: a diesel's filling stroke or a steam engine's release (the H-6's), main reservoir air straight into the pipe; a diesel's emergency.
                // A diesel's handle widened before a switch to American equipment, or another player's, keeps its stroke's notch: the 26-L has none, so it reads as release.
                if (zone == HandleZone.Fill && bs.selfLappingController && !valve.Spec.FillingStroke)
                    zone = HandleZone.Travel;
                // The No. 394: I before the travel, then II to VI with VA along it, on any engine's handle.
                if (valve.Spec.Matrosov394)
                    valve.Position = zone == HandleZone.Fill ? DriverValvePosition.Fill
                        : zone == HandleZone.Emergency ? DriverValvePosition.Emergency
                        : DriverBrakeValveSpec.Matrosov394At(handle);
                else if (zone == HandleZone.Fill)
                    valve.Position = DriverValvePosition.Fill;
                else if (zone == HandleZone.Emergency)
                    valve.Position = DriverValvePosition.Emergency;
                // The H-6's holding reads as running; a value set from outside since (the remote, a cable) moves it on.
                else if (zone == HandleZone.Holding && !bs.selfLappingController && handle <= 0.1f)
                    valve.Position = DriverValvePosition.Holding;
                else if (bs.selfLappingController)
                {
                    if (!TryReadSelfLapping(bs, handle, out var demand))
                        valve.Position = DriverValvePosition.Running;
                    else
                    {
                        valve.Position = DriverValvePosition.Service;
                        valve.ServiceReduction = demand * valve.Spec.FullService;
                    }
                }
                else
                {
                    valve.Position = MapManualLapZone(handle, DriverValvePosition.Running, DriverValvePosition.Lap, DriverValvePosition.Apply, DriverValvePosition.Emergency);
                }
            }
            var independent = bs.independentBrakePosition;
            // The S-6's handle stands in positions, read from its own travel; a self-lapping one's through the game's curve.
            engine.Independent = !bs.hasIndependentBrake ? 0
                // The No. 254's six notches, I (bail-off) and II reading as released.
                : IsRussianEngine(Car) ? LocomotiveBrakeSpec.Matrosov254Travel(independent)
                : engine.Spec.ManualLapIndependent || bs.indBrakeCurve == null ? independent
                : bs.indBrakeCurve.Evaluate(independent);

            // The pump's own rate times the game's Main Reservoir Fill Time, as the stock compressor runs.
            var rate = bs.compressorProductionRate * pumpSpeedUp;
            engine.CompressorFlow = compressing ? rate * Air.Bar * 1e-3 / Air.RT : 0;
            return changed;
        }

        // Evaluates a self-lapping handle through the game's brake curve; returns false in running, or true with the clamped service demand.
        private static bool TryReadSelfLapping(BrakeSystem bs, float handle, out float demand)
        {
            var t = bs.trainBrakeCurve != null ? bs.trainBrakeCurve.Evaluate(handle) : handle;
            if (t <= 0.001f)
            {
                demand = 0f;
                return false;
            }
            demand = Mathf.Clamp01(t);
            return true;
        }

        // The game's manual-lap zones: running, lap, apply, emergency.
        private static T MapManualLapZone<T>(float handle, T running, T lap, T apply, T emergency) =>
            handle > 0.9f ? emergency
                : handle > 0.5f ? apply
                : handle > 0.1f ? lap
                : running;

        private float lastSupply = float.NaN;
        private bool supplyChanged;

        private void ReadVacuumHandles(VacuumEngine engine, HandleZone zone, bool pumping, float pumpSpeedUp)
        {
            var bs = System;
            engine.CutIn = bs.hasTrainBrake && bs.trainBrakeCutout;
            if (engine.Spec.SteamBrake)
            {
                var pump = steamPump;
                var open = pump == null || !pump.compressorControl.IsConnected || pump.compressorControl.Value > 0f;
                if (open != engine.PumpOn)
                {
                    engine.PumpOn = open;
                    supplyChanged = true;
                }
                engine.Supply = pump == null ? 1 : Mathf.InverseLerp(1f, pump.pressureForMaxProduction, pump.steamPressure.Value);
                if (Mathf.Abs((float)engine.Supply - lastSupply) > 0.01f)
                {
                    lastSupply = (float)engine.Supply;
                    supplyChanged = true;
                }
            }
            else
            {
                engine.PumpOn = pumping;
            }
            var handle = bs.trainBrakePosition;
            if (zone == HandleZone.Fill)
                engine.Position = VacuumValvePosition.Release;
            else if (zone == HandleZone.Emergency)
                engine.Position = VacuumValvePosition.Emergency;
            else if (bs.selfLappingController)
            {
                if (!TryReadSelfLapping(bs, handle, out var demand))
                    engine.Position = VacuumValvePosition.Running;
                else
                {
                    engine.Position = VacuumValvePosition.Service;
                    engine.Target = Core.Vacuum.FromInches(Core.Vacuum.Working * (1 - demand));
                }
            }
            else
            {
                engine.Position = MapManualLapZone(handle, VacuumValvePosition.Running, VacuumValvePosition.Lap, VacuumValvePosition.Apply, VacuumValvePosition.Emergency);
            }
            var independent = bs.independentBrakePosition;
            engine.Independent = !bs.hasIndependentBrake ? 0 : bs.indBrakeCurve == null ? independent : bs.indBrakeCurve.Evaluate(independent);
            engine.CompressorFlow = pumping && !engine.Spec.SteamBrake ? bs.compressorProductionRate * pumpSpeedUp * Air.Bar * 1e-3 / Air.RT : 0;
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
                bs.auxReservoirPressure = Bar(Stock(valve.AuxiliaryReservoir.Pressure));
                bs.controlReservoirPressure = Bar(Stock(Brake.Distributor?.ControlReservoir.Pressure ?? valve.AuxiliaryReservoir.Pressure));
                bs.brakeCylinderPressureUnsmoothed = Bar(valve.Cylinder.Pressure);
                // As the stock brake counts it: the reservoirs' charging and the cylinder's filling.
                pipeIn += valve.ChargeFlow + valve.FillFlow;
                exhaust += valve.ExhaustFlow;
            }
            var vacuumEngine = Brake.VacuumEngine;
            if (vacuumEngine != null)
            {
                if (Air.ToAbsoluteBar(vacuumEngine.MainReservoir.Pressure) > MainReservoirCeiling)
                    vacuumEngine.MainReservoir.Pressure = Air.FromAbsoluteBar(MainReservoirCeiling);
                // The stock brake, if it takes over, finds the engine's reservoirs charged as the pipe is.
                bs.auxReservoirPressure = PipeBar;
                bs.controlReservoirPressure = PipeBar;
                bs.brakeCylinderPressureUnsmoothed = Bar(vacuumEngine.Cylinder.Pressure);
                exhaust += vacuumEngine.ValveAdmitFlow + vacuumEngine.ExhaustFlow;
                WriteMainReservoir(vacuumEngine.MainReservoir);
                var pumpWanted = !vacuumEngine.Spec.SteamBrake;
                if (bs.compressorActivationSignal != pumpWanted)
                    bs.ToggleCompressorActivationSignal();
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

                var mr = WriteMainReservoir(engine.MainReservoir);
                // The E-T's duplex governor, set by the handle; otherwise the game's.
                var setting = engine.GovernorSetting(Brake.PipeSection);
                var on = double.IsNaN(setting) ? bs.compressorActivationPressure : Bar(Air.Atmosphere + setting - engine.Spec.GovernorDifferential);
                var off = double.IsNaN(setting) ? MainReservoirCeiling : Mathf.Min(MainReservoirCeiling, Bar(Air.Atmosphere + setting));
                if (!bs.compressorActivationSignal && mr < on)
                    bs.ToggleCompressorActivationSignal();
                else if (bs.compressorActivationSignal && mr >= off)
                    bs.ToggleCompressorActivationSignal();
            }
            // Another mod venting the pipe at this engine (BrakeSets.VentFromOutside) blows as the brake valve's exhaust does.
            exhaust += Brake.OutsideVentFlow;
            // The caboose valve blows at its car, heard as that car's brake exhaust.
            exhaust += Brake.CabooseValveFlow;
            PipeFlow = (float)pipeIn * StockFlowPerKgPerSecond;
            ExhaustFlow = (float)exhaust * StockFlowPerKgPerSecond;
        }

        /// <summary>Writes the model's main reservoir to the stock field, telling its readers when it moved; returns it, bar absolute.</summary>
        private float WriteMainReservoir(Chamber reservoir)
        {
            var bs = System;
            ref var mr = ref MainReservoirUnsmoothed(bs);
            var before = mr;
            mr = Bar(reservoir.Pressure);
            writtenMainReservoir = mr;
            if (Mathf.Abs(bs.mainReservoirPressure - mr) > 0.0001f || before != mr)
                bs.Fire_MainReservoirPressureChanged();
            return mr;
        }
    }
}
