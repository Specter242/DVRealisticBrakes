using RealisticBrakes.Sim;

namespace RealisticBrakes
{
    /// <summary>
    /// For other mods (by reflection, when this mod is loaded and
    /// <see cref="Active"/>). This mod's own click on the independent lever
    /// bails off through <see cref="BailOff"/>; another mod's control can too.
    /// </summary>
    public static class Api
    {
        /// <summary>Whether this mod is running the brake now.</summary>
        public static bool Active => Main.Active;

        /// <summary>
        /// Bails off <paramref name="car"/>'s independent: the engine lets go of
        /// the automatic application then on, the train's brakes staying
        /// applied, until a release. Returns the cylinder pressure bailed, gauge
        /// bar; 0 when there was nothing to bail or the car isn't an engine
        /// under the model. On an American steam engine's E-T, in an emergency
        /// from its own handle, the valve puts the pressure back once the
        /// independent leaves release, so a single call lets off only for a
        /// moment (Wood p. 120).
        /// </summary>
        public static float BailOff(TrainCar car)
        {
            if (!Main.Active || car == null || car.brakeSystem == null || !BrakeSets.TryGet(car.brakeSystem, out var air))
                return 0f;
            var engine = air.Brake.Locomotive;
            if (engine == null)
                return 0f;
            air.Touch();
            return (float)(engine.BailOff() / Core.Air.Bar);
        }

        /// <summary>
        /// The pressure of <paramref name="part"/> on <paramref name="car"/> now,
        /// in gauge bar (0 is atmospheric), as this mod's brake has it. The
        /// parts: <c>BrakePipe</c> (the car's own section of pipe),
        /// <c>AuxiliaryReservoir</c>, <c>ControlReservoir</c> (European cars and
        /// engines), <c>EmergencyReservoir</c> (American cars),
        /// <c>BrakeCylinder</c>, <c>MainReservoir</c> and
        /// <c>EqualizingReservoir</c> (engines), named without regard to case.
        /// NaN where the car has no such part, the name is none of these, or
        /// this mod isn't running the car's brake. These names, units, and
        /// meanings stay as they are from version to version; this mod's
        /// internals don't (Fuggschen, 2026-11-07).
        /// </summary>
        public static float Pressure(TrainCar car, string part)
        {
            if (!Main.Active || car == null || car.brakeSystem == null || !Core.BrakeParts.TryParse(part, out var which)
                || !BrakeSets.TryGet(car.brakeSystem, out var air) || air.IsVacuum)
                return float.NaN;
            return (float)(air.Brake.GaugePressure(which) / Core.Air.Bar);
        }

        /// <summary>
        /// The equipment <paramref name="car"/>'s brake runs on now:
        /// <c>"European"</c> (UIC) or <c>"American"</c> (AAR). Each engine has
        /// its own prototype's, the cars the setting's. Null when this mod isn't
        /// running the car's brake.
        /// </summary>
        public static string Equipment(TrainCar car)
        {
            if (!Main.Active || car == null || car.brakeSystem == null || !BrakeSets.TryGet(car.brakeSystem, out var air))
                return null;
            if (air.IsVacuum)
                return "Vacuum";
            if (air.Brake.Locomotive != null)
                return CarAir.IsAmericanEngine(car) ? "American" : "European";
            return Main.Settings.Equipment == RealisticBrakes.Equipment.American ? "American" : "European";
        }

        /// <summary>
        /// The G/P mode a European car's distributor runs in now: <c>"Goods"</c>
        /// (slow to apply and release, for a long train) or <c>"Passenger"</c>
        /// (quick); engines and coaches run in Passenger. Null on American
        /// equipment, which has no changeover, on a car with no brake valve, and
        /// when this mod isn't running the car's brake.
        /// </summary>
        public static string BrakeMode(TrainCar car)
        {
            if (Equipment(car) != "European" || !BrakeSets.TryGet(car.brakeSystem, out var air))
                return null;
            var mode = air.Brake.BrakingMode;
            return mode == null ? null : mode == Core.BrakingMode.Goods ? "Goods" : "Passenger";
        }

        /// <summary>
        /// Whether <paramref name="car"/>'s own brake is cut out by its
        /// isolating cock (the cut-out lever by its bleed handle): it runs
        /// unbraked, the pipe passing through. Null for a car with no brake
        /// valve, and when this mod isn't running the car's brake.
        /// </summary>
        public static bool? CutOut(TrainCar car) =>
            Main.Active && car != null && car.brakeSystem != null && BrakeSets.TryGet(car.brakeSystem, out var air) ? air.Brake.CutOut : null;

        /// <summary>
        /// Whether an engine's brake valve is cut out, so its train brake handle
        /// doesn't work the train's pipe (as on a trailing engine). Apart from
        /// <see cref="CutOut"/>, which is the engine's own brake. Null for a car
        /// with no brake valve, and when this mod isn't running the car's brake.
        /// </summary>
        public static bool? BrakeValveCutOut(TrainCar car) =>
            Main.Active && car != null && car.brakeSystem != null && BrakeSets.TryGet(car.brakeSystem, out var air) ? air.Brake.BrakeValveCutOut : null;
    }
}
