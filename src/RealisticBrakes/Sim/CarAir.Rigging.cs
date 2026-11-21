using System.Collections.Generic;
using DV;
using DV.ThingTypes;
using RealisticBrakes.Core;
using UnityEngine;

namespace RealisticBrakes.Sim
{
    internal sealed partial class CarAir
    {
        /// <summary>The game's per-kg factor on a car type's force per bogie (<c>Bogie.Awake</c>: 1/11,500).</summary>
        internal const float StockForcePerKg = 8.695652E-05f;

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

        // Cached inputs and output of ApplyRigging so unchanged or sleeping cars skip recomputing and writing bogie force per kg.
        private bool riggingDirty = true;
        private bool lastBrakeForceFromCar;
        private LoadBraking lastLoadBraking;
        private bool lastManualEmptyLoaded;
        private float lastRiggingStockPerKg = float.NaN;
        private float lastRiggingMass = float.NaN;
        private float lastRiggingHandbrake = float.NaN;
        private double lastRiggingCylinder = double.NaN;
        private LoadSetting lastRiggingLoad;
        private float lastWrittenPerKg = float.NaN;

        /// <summary>The freight car's force per kg as its rigging set it this frame, before the hold's scaling (<see cref="HandBrakeHold"/>).</summary>
        public float RiggedPerKg { get; private set; }

        /// <summary>Notes that something outside <see cref="ApplyRigging"/> (such as <see cref="HandBrakeHold"/>) wrote the bogies' force per kg, so the next tick writes it back.</summary>
        public void MarkRiggingDirty() => riggingDirty = true;

 public float HeatShare
        {
            get
            {
                if (Car == null || Car.Bogies == null || Car.Bogies.Length == 0 || Car.Bogies[0] == null)
                    return 1f;
                var stockPerKg = Car.carLivery.parentType.brakes.MaxBrakingForcePerBogie * StockForcePerKg;
                return stockPerKg > 0f ? Car.Bogies[0].maxBrakingForcePerKg / stockPerKg : 1f;
            }
        }

 private bool ApplyRigging()
        {
            var valve = Brake.Valve;
            if (Rigging == null || valve == null || Car == null)
                return false;
            var stockPerKg = Car.carLivery.parentType.brakes.MaxBrakingForcePerBogie * StockForcePerKg;
            var brakeForceFromCar = Main.Settings.BrakeForceFromCar;
            if (!brakeForceFromCar)
            {
                if (!riggingDirty && !forceOverridden && !lastBrakeForceFromCar
                    && lastRiggingStockPerKg == stockPerKg
                    && valve.Load == LoadSetting.Loaded)
                    return false;
                RestoreForce();
                RiggedPerKg = stockPerKg;
                lastRiggingStockPerKg = stockPerKg;
                lastWrittenPerKg = stockPerKg;
                var was = valve.Load;
                valve.Load = LoadSetting.Loaded;
                lastBrakeForceFromCar = false;
                lastRiggingLoad = valve.Load;
                riggingDirty = false;
                return was != valve.Load;
            }
            var mass = Car.massController != null ? Car.massController.TotalMass : 0f;
            if (mass <= 0f)
                return false;
            var loadBraking = Main.Settings.LoadBraking;
            var manualEmptyLoaded = Main.Settings.ManualEmptyLoaded;
            var handbrake = System.handbrakePosition;
            var cylinder = Brake.CylinderGauge;
            var before = valve.Load;
            if (!riggingDirty && forceOverridden
                && lastBrakeForceFromCar
                && lastRiggingStockPerKg == stockPerKg
                && lastLoadBraking == loadBraking
                && lastManualEmptyLoaded == manualEmptyLoaded
                && lastRiggingMass == mass
                && lastRiggingHandbrake == handbrake
                && lastRiggingCylinder == cylinder
                && lastRiggingLoad == before)
            {
                var bogiesMatch = true;
                foreach (var bogie in Car.Bogies)
                    if (bogie != null && bogie.maxBrakingForcePerKg != lastWrittenPerKg)
                    {
                        bogiesMatch = false;
                        break;
                    }
                if (bogiesMatch)
                    return false;
                // A bogie was replaced or modified externally: rewrite the cached force per kg without recomputing rigging.
                RiggedPerKg = lastWrittenPerKg;
                foreach (var bogie in Car.Bogies)
                    if (bogie != null)
                        bogie.maxBrakingForcePerKg = lastWrittenPerKg;
                return false;
            }

            // European cars' lever by hand when the setting says so; American empty/load equipment is always automatic. An
            // American car of the period has none: its cylinder takes full pressure, and its rigging's leverage gives the
            // force for its empty weight (the review, 2026-10-28: run on the empty setting, every cylinder read 1.4 bar and
            // the cars' piston travel stopped mattering).
            var period = (Brake.ControlValve != null || Brake.VacuumCylinder != null) && loadBraking == LoadBraking.Period;
            if (period)
                valve.Load = LoadSetting.Loaded;
            else if (!(manualEmptyLoaded && Brake.Distributor != null))
                valve.Load = Rigging.SettingFor(mass);

            var multiplier = (float)Rigging.ForceMultiplier(BrakeRigging.StockBrakingFactor(cylinder), handbrake, mass,
                period ? Rigging.PeriodLeverage : 1);
            var perKg = stockPerKg * multiplier;
            RiggedPerKg = perKg;
            foreach (var bogie in Car.Bogies)
                if (bogie != null)
                    bogie.maxBrakingForcePerKg = perKg;
            forceOverridden = true;
            lastBrakeForceFromCar = true;
            lastRiggingStockPerKg = stockPerKg;
            lastLoadBraking = loadBraking;
            lastManualEmptyLoaded = manualEmptyLoaded;
            lastRiggingMass = mass;
            lastRiggingHandbrake = handbrake;
            lastRiggingCylinder = cylinder;
            lastRiggingLoad = valve.Load;
            lastWrittenPerKg = perKg;
            riggingDirty = false;
            return before != valve.Load;
        }

        /// <summary>Gives the bogies back the game's own force per kg.</summary>
        public void RestoreForce()
        {
            riggingDirty = true;
            if (!forceOverridden || Car == null)
                return;
            var stockPerKg = Car.carLivery.parentType.brakes.MaxBrakingForcePerBogie * StockForcePerKg;
            foreach (var bogie in Car.Bogies)
                if (bogie != null)
                    bogie.maxBrakingForcePerKg = stockPerKg;
            forceOverridden = false;
        }
    }
}
