using System;

namespace RealisticBrakes.Core
{
    /// <summary>
    /// A freight car's brake rigging: the force its cylinder puts on the
    /// wheels belongs to the car, not to its load. The game instead brakes
    /// every car at the same deceleration, loaded or empty (its force per kg
    /// of the car's current mass). Here the force is the game's at the car
    /// type's heaviest load, so a full car on its loaded setting stops exactly
    /// as it did, a lighter load stops shorter, and the empty setting brakes
    /// an empty car as the game did. An empty car left on loaded brakes about
    /// three times as hard as it should and slides its wheels, as the
    /// empty/loaded lever exists to prevent.
    /// </summary>
    public sealed class BrakeRigging
    {
        /// <summary>The cylinder pressure, gauge Pa, at which the game's braking factor starts: 0.5 bar (its 1.5 absolute).</summary>
        public const double StockTakeUp = 0.5 * Air.Bar;

        /// <summary>The cylinder pressure span, Pa, over which the game's braking factor rises from 0 to 1: 3.0 bar.</summary>
        public const double StockSpan = 3.0 * Air.Bar;

        /// <summary>A car of <paramref name="emptyMass"/> kg carrying at most <paramref name="loadedMass"/> kg in all.</summary>
        public BrakeRigging(double emptyMass, double loadedMass)
        {
            if (!(emptyMass > 0) || !(loadedMass >= emptyMass))
                throw new ArgumentOutOfRangeException(nameof(loadedMass), $"A rigging needs 0 < empty ({emptyMass}) ≤ loaded ({loadedMass}).");
            EmptyMass = emptyMass;
            LoadedMass = loadedMass;
        }

        /// <summary>The car's own mass, kg.</summary>
        public double EmptyMass { get; }

        /// <summary>The car's heaviest load with the car, kg: the mass its brake is sized for.</summary>
        public double LoadedMass { get; }

        /// <summary>
        /// The mass, kg, from which the empty/loaded lever belongs at loaded:
        /// halfway between empty and full (inferred; UIC cars carry the
        /// changeover mass stenciled).
        /// </summary>
        public double ChangeoverMass => EmptyMass + (LoadedMass - EmptyMass) / 2;

        /// <summary>The lever setting a car of <paramref name="mass"/> kg should have.</summary>
        public LoadSetting SettingFor(double mass) => mass >= ChangeoverMass ? LoadSetting.Loaded : LoadSetting.Empty;

        /// <summary>
        /// What to multiply the game's force per kg by, for a car now weighing
        /// <paramref name="mass"/> kg, so that its force is the rigging's.
        /// </summary>
        public double ForceShare(double mass) => LoadedMass / Math.Max(1, mass);

 public double ForceMultiplier(double air, double hand, double mass)
        {
            air = Math.Max(0, Math.Min(1, air));
            hand = Math.Max(0, Math.Min(1, hand));
            var share = ForceShare(mass);
            var factor = Math.Max(air, hand);
            return factor > 0.001 ? Math.Max(air * share, hand * Holding.HandBrakeShare) / factor : share;
        }

        /// <summary>
        /// The empty setting's full-service cylinder pressure, gauge Pa: enough
        /// for the game's braking factor of empty ÷ loaded mass, which brakes
        /// the empty car at the game's own deceleration.
        /// </summary>
        public double EmptyCylinder => StockTakeUp + StockSpan * EmptyMass / LoadedMass;

        /// <summary>The share of <paramref name="maximumCylinder"/> gauge Pa the empty setting gives.</summary>
        public double EmptyRatio(double maximumCylinder) => Math.Min(1, EmptyCylinder / maximumCylinder);

        /// <summary>The game's braking factor for <paramref name="cylinder"/> gauge Pa.</summary>
        public static double StockBrakingFactor(double cylinder) => Math.Max(0, Math.Min(1, (cylinder - StockTakeUp) / StockSpan));
    }
}
