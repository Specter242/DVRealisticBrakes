using System.Runtime.CompilerServices;
using DV.Simulation.Brake;
using Newtonsoft.Json.Linq;
using RealisticBrakes.Controls;
using RealisticBrakes.Core;

namespace RealisticBrakes.Sim
{
    /// <summary>
    /// The air the game's own save keys don't hold, kept under
    /// <see cref="Key"/> in each car's save entry: the levers, the
    /// distributor's latch and quick-service chamber, and an engine's
    /// equalizing reservoir, overcharge, and bail-off. The stock keys (pipe,
    /// auxiliary, control, main reservoir, cylinder) keep being written, so a
    /// save loads sanely without the mod. Pressures are absolute bar, as the
    /// game's; differences (deepest reduction, overcharge, bail) are bar.
    /// </summary>
    internal static class SavedAir
    {
        public const string Key = "realisticBrakes";

        private const int Version = 1;

        // A loaded car's entry, held until the model first builds the car's air.
        private static readonly ConditionalWeakTable<BrakeSystem, JObject> pending = new ConditionalWeakTable<BrakeSystem, JObject>();

        /// <summary>
        /// The car's entry to save, or null when the model hasn't taken the car
        /// on. An engine's handle left in an added notch that stays (holding,
        /// a steam engine's release, emergency) is saved with it, as the game
        /// saves only the handle's stock value.
        /// </summary>
        public static JObject Write(BrakeSystem system)
        {
            if (!Main.Active || system == null || !BrakeSets.TryGet(system, out var air))
                return null;
            var entry = Write(air.Brake);
            var zone = TrainBrakeHandle.ZoneOf(system);
            if (air.Brake.Locomotive != null && TrainBrakeHandle.Stays(zone, system))
                entry["zone"] = (int)zone;
            return entry;
        }

        /// <summary>The entry for a car's air.</summary>
        internal static JObject Write(CarBrake brake)
        {
            var entry = new JObject { ["v"] = Version };
            if (brake.Distributor != null)
                WriteValve(entry, brake.Distributor, "");
            if (brake.ControlValve != null)
                WriteAmerican(entry, brake.ControlValve);
            var engine = brake.Locomotive;
            if (engine != null)
            {
                WriteValve(entry, engine.Distributor, "loco");
                entry["bailed"] = CarAir.Bar(engine.BailedOff);
                if (engine.HeldAutomatic > 0)
                    entry["held"] = CarAir.Bar(engine.HeldAutomatic);
                // The S-6's air held in the application chamber, as a lapped independent leaves it.
                if (engine.Spec.ManualLapIndependent && engine.IndependentChamber.Pressure > Air.Atmosphere + 1000)
                    entry["independent"] = CarAir.Bar(engine.IndependentChamber.Pressure);
                if (engine.DriverValve != null)
                {
                    entry["er"] = CarAir.Bar(engine.DriverValve.EqualizingReservoir.Pressure);
                    entry["overcharge"] = CarAir.Bar(engine.DriverValve.OverchargeLeft);
                }
            }
            return entry;
        }

        private static void WriteAmerican(JObject entry, AarControlValve valve)
        {
            entry["family"] = "AAR";
            entry["load"] = valve.Load == LoadSetting.Loaded ? "loaded" : "empty";
            entry["isolated"] = valve.Isolated;
            entry["applied"] = valve.Applied;
            entry["emergency"] = valve.Emergency;
            entry["cylinderRelease"] = valve.CylinderReleaseOpen;
            entry["retainer"] = valve.Retainer.ToString();
            entry["emergencyReservoir"] = CarAir.Bar(valve.EmergencyReservoir.Pressure);
            entry["quickAction"] = CarAir.Bar(valve.QuickActionChamber.Pressure);
        }

        private static void ReadAmerican(JObject entry, AarControlValve valve)
        {
            // A save made with European equipment has nothing for an American valve.
            if ((string)entry["family"] != "AAR")
                return;
            var load = (string)entry["load"];
            if (load != null)
                valve.Load = load == "empty" ? LoadSetting.Empty : LoadSetting.Loaded;
            valve.Isolated = (bool?)entry["isolated"] ?? false;
            var applied = (bool?)entry["applied"];
            if (applied.HasValue)
            {
                valve.RestoreLatch(applied.Value, (bool?)entry["emergency"] ?? false);
                // A bled car stays released until its pipe is recharged.
                valve.RestoreCylinderRelease((bool?)entry["cylinderRelease"] ?? false);
            }
            if (System.Enum.TryParse<Retainer>((string)entry["retainer"] ?? "", out var retainer))
                valve.Retainer = retainer;
            var emergency = (float?)entry["emergencyReservoir"];
            if (emergency.HasValue)
                valve.EmergencyReservoir.Pressure = CarAir.Pa(emergency.Value);
            var quick = (float?)entry["quickAction"];
            if (quick.HasValue)
                valve.QuickActionChamber.Pressure = CarAir.Pa(quick.Value);
        }

        private static void WriteValve(JObject entry, UicDistributor valve, string prefix)
        {
            entry[prefix + "mode"] = valve.Mode == BrakingMode.Goods ? "G" : "P";
            entry[prefix + "load"] = valve.Load == LoadSetting.Loaded ? "loaded" : "empty";
            entry[prefix + "isolated"] = valve.Isolated;
            entry[prefix + "applied"] = valve.Applied;
            entry[prefix + "deepest"] = CarAir.Bar(valve.DeepestReduction);
            entry[prefix + "quickService"] = CarAir.Bar(valve.QuickServiceChamber.Pressure);
            entry[prefix + "releaseValve"] = valve.ReleaseValveOpen;
        }

        /// <summary>Holds a loaded car's entry until its air is built.</summary>
        public static void Hold(BrakeSystem system, JObject entry)
        {
            // Loaded with the model off, the car starts from the stock keys when it comes on.
            if (!Main.Active || system == null || entry == null || (int?)entry["v"] != Version)
                return;
            pending.Remove(system);
            pending.Add(system, entry);
        }

        /// <summary>Drops an entry held for a car the game resets for reuse.</summary>
        public static void Discard(BrakeSystem system) => pending.Remove(system);

        /// <summary>Applies the entry held for <paramref name="air"/>'s car, if any, after it was restored from the stock keys.</summary>
        public static void Apply(CarAir air)
        {
            if (!pending.TryGetValue(air.System, out var entry))
                return;
            pending.Remove(air.System);
            Read(entry, air.Brake);
            var zone = (int?)entry["zone"];
            if (zone.HasValue && air.Brake.Locomotive != null && air.Car != null && TrainBrakeHandle.Stays((HandleZone)zone.Value, air.System))
                TrainBrakeHandle.SetRemoteZone(air.Car, (HandleZone)zone.Value, TrainBrakeHandle.LocalHand);
        }

        /// <summary>Sets a car's air from its saved entry, over what the stock keys restored.</summary>
        internal static void Read(JObject entry, CarBrake brake)
        {
            if ((int?)entry["v"] != Version)
                return;
            if (brake.Distributor != null && (string)entry["family"] != "AAR")
                ReadValve(entry, brake.Distributor, "");
            if (brake.ControlValve != null)
                ReadAmerican(entry, brake.ControlValve);
            var engine = brake.Locomotive;
            if (engine != null)
            {
                ReadValve(entry, engine.Distributor, "loco");
                engine.BailedOff = ((float?)entry["bailed"] ?? 0f) * Air.Bar;
                // An E-T engine's brake held through release (none on other engines).
                if (engine.Spec.HoldsAutomaticBrake)
                    engine.HeldAutomatic = ((float?)entry["held"] ?? 0f) * Air.Bar;
                var independent = (float?)entry["independent"];
                if (engine.Spec.ManualLapIndependent)
                    engine.IndependentChamber.Pressure = independent.HasValue ? CarAir.Pa(independent.Value) : Air.Atmosphere;
                if (engine.DriverValve != null)
                {
                    var er = (float?)entry["er"];
                    if (er.HasValue)
                        engine.DriverValve.EqualizingReservoir.Pressure = CarAir.Pa(er.Value);
                    var overcharge = (float?)entry["overcharge"];
                    if (overcharge.HasValue)
                        engine.DriverValve.RestoreOvercharge(overcharge.Value * Air.Bar);
                }
            }
        }

        private static void ReadValve(JObject entry, UicDistributor valve, string prefix)
        {
            var mode = (string)entry[prefix + "mode"];
            if (mode != null)
                valve.Mode = mode == "P" ? BrakingMode.Passenger : BrakingMode.Goods;
            var load = (string)entry[prefix + "load"];
            if (load != null)
                valve.Load = load == "empty" ? LoadSetting.Empty : LoadSetting.Loaded;
            valve.Isolated = (bool?)entry[prefix + "isolated"] ?? false;
            var applied = (bool?)entry[prefix + "applied"];
            if (applied.HasValue)
                valve.RestoreLatch(applied.Value, ((float?)entry[prefix + "deepest"] ?? 0f) * Air.Bar);
            var quick = (float?)entry[prefix + "quickService"];
            if (quick.HasValue)
                valve.QuickServiceChamber.Pressure = CarAir.Pa(quick.Value);
            valve.RestoreReleaseValve((bool?)entry[prefix + "releaseValve"] ?? false);
        }
    }
}
