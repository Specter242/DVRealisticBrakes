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
        /// under the model.
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
    }
}
