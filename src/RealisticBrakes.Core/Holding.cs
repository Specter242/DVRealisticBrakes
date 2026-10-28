using System;
using System.Collections.Generic;

namespace RealisticBrakes.Core
{
    /// <summary>One car of a standing cut, for <see cref="Holding"/>.</summary>
    public readonly struct HeldCar
    {
        public HeldCar(double mass, double brakeForce, double slope, double rollingCoefficient)
        {
            Mass = mass;
            BrakeForce = brakeForce;
            Slope = slope;
            RollingCoefficient = rollingCoefficient;
        }

        /// <summary>The car's mass, kg, cargo included.</summary>
        public double Mass { get; }

        /// <summary>The force its brakes give, air or hand, N.</summary>
        public double BrakeForce { get; }

        /// <summary>The sine of its grade, signed along the cut (each car's in the same sense).</summary>
        public double Slope { get; }

        /// <summary>Its rolling resistance, a share of its weight.</summary>
        public double RollingCoefficient { get; }
    }

 public static class Holding
    {
        /// <summary>
        /// A full hand brake's force as a share of a full air application
        /// (inferred). A loaded car's hand brake holds it alone on about 3 %;
        /// a cut needs one in about three on 1 %.
        /// </summary>
        public const double HandBrakeShare = 0.5;

        /// <summary>The grip a held, braked wheel has on dry rail, a share of its load (inferred).</summary>
        public const double Adhesion = 0.25;

        private const double Gravity = 9.81;

        /// <summary>The force the cut's brakes and rolling resistance can hold against, N.</summary>
        public static double HoldingForce(IEnumerable<HeldCar> cars)
        {
            var force = 0.0;
            foreach (var car in cars)
            {
                var weight = car.Mass * Gravity;
                force += Math.Min(Math.Max(0, car.BrakeForce), Adhesion * weight) + car.RollingCoefficient * weight;
            }
            return force;
        }

        /// <summary>Gravity's pull on the cut down its grade, N.</summary>
        public static double Pull(IEnumerable<HeldCar> cars)
        {
            var pull = 0.0;
            foreach (var car in cars)
                pull += car.Mass * Gravity * car.Slope;
            return Math.Abs(pull);
        }

        /// <summary>Whether the cut's brakes hold it where it stands.</summary>
        public static bool Holds(IReadOnlyCollection<HeldCar> cars) => HoldingForce(cars) >= Pull(cars);
    }
}
