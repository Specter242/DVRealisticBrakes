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
        public static double HoldingForce(IReadOnlyList<HeldCar> cars)
        {
            // By index: this runs every frame for each standing cut, and enumerating through the interface allocates.
            var force = 0.0;
            for (var i = 0; i < cars.Count; i++)
            {
                var car = cars[i];
                var weight = car.Mass * Gravity;
                force += Math.Min(Math.Max(0, car.BrakeForce), Adhesion * weight) + car.RollingCoefficient * weight;
            }
            return force;
        }

        /// <summary>Gravity's pull on the cut down its grade, N.</summary>
        public static double Pull(IReadOnlyList<HeldCar> cars)
        {
            var pull = 0.0;
            for (var i = 0; i < cars.Count; i++)
                pull += cars[i].Mass * Gravity * cars[i].Slope;
            return Math.Abs(pull);
        }

        /// <summary>Whether the cut's brakes hold it where it stands.</summary>
        public static bool Holds(IReadOnlyList<HeldCar> cars) => HoldingForce(cars) >= Pull(cars);

        /// <summary>
        /// A standing cut's last weighing, kept while nothing it was weighed
        /// from changes. Weighing reads every car's mass, force, and grade,
        /// and a parked cut's don't change from frame to frame: with 31 cuts
        /// of 414 cars standing, weighing each every frame was most of the
        /// 0.84 ms a frame the hold took (measured in game, 2026-11-09). A
        /// cut is weighed again when its inputs change (each car's braking
        /// and mass, as the caller sums them up), and every
        /// <see cref="RefreshSeconds"/> whatever they are, for what the sum
        /// doesn't hold: the grade under a cut that creeps, a setting that
        /// changes its cars' force.
        /// </summary>
        public sealed class Weighed
        {
            /// <summary>The longest a weighing stands with its inputs unchanged, s.</summary>
            public const double RefreshSeconds = 0.5;

            private readonly double offset;
            private bool weighed;
            private int inputs;
            private double at;

            /// <summary>
            /// <paramref name="offset"/>, s, under <see cref="RefreshSeconds"/>,
            /// brings this cut's first refresh forward by that much, so cuts
            /// first weighed in one frame aren't all weighed again in another.
            /// </summary>
            public Weighed(double offset = 0)
            {
                this.offset = Math.Max(0, Math.Min(RefreshSeconds, offset));
            }

            /// <summary>Whether the cut's brakes held it when it was last weighed.</summary>
            public bool Holds { get; private set; }

            /// <summary>Whether the cut is to be weighed again at <paramref name="now"/>, s, its inputs summed up as <paramref name="inputs"/>.</summary>
            public bool Due(int inputs, double now) =>
                !weighed || inputs != this.inputs || now - at >= RefreshSeconds || now < at - RefreshSeconds;

            /// <summary>Keeps a weighing made at <paramref name="now"/> from <paramref name="inputs"/>.</summary>
            public void Record(int inputs, double now, bool holds)
            {
                // The first weighing counts as made a little earlier, by the cut's offset.
                at = weighed ? now : now - offset;
                weighed = true;
                this.inputs = inputs;
                Holds = holds;
            }
        }

        /// <summary>The Wardale 5AT's Kss brake plugin's mod ID (JimTaplin's Wardale_Brakes, an optional file of Nexus mod 1770).</summary>
        public const string WardaleBrakesId = "Wardale_Brakes";

        private static readonly string[] wardaleBrakesCars = { "PROTO5AT", "PROTO5AT_tender" };

 public static bool ForceKeptByOtherMod(string carTypeId, bool wardaleBrakesActive) =>
            wardaleBrakesActive && carTypeId != null && Array.IndexOf(wardaleBrakesCars, carTypeId) >= 0;
    }
}
