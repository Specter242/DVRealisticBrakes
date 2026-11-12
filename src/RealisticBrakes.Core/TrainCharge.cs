using System;
using System.Collections.Generic;

namespace RealisticBrakes.Core
{
 public static class TrainCharge
    {
        /// <summary>
        /// A car is short of air when what it brakes with stands this far
        /// under the brake valve's running pressure, Pa. A charged European
        /// car's auxiliary reservoir gives up about half a bar in a full
        /// service, so one braked again straight after a release isn't short;
        /// an American car's gives up more, and is, as its books warn.
        /// </summary>
        public const double ShortBy = 1.0 * Air.Bar;

        /// <summary>The share of a train's braked cars short of air from which the driver is told.</summary>
        public const double WorthTelling = 0.25;

        /// <summary>The notice isn't repeated for this long, s: a driver working the handle hears it once.</summary>
        public const double RepeatAfter = 60;

        /// <summary>
        /// The pressure <paramref name="car"/> brakes with, Pa absolute: its
        /// auxiliary reservoir, and for a European distributor its control
        /// reservoir too, as the cylinder follows how far the pipe stands
        /// under that. NaN for an engine, a car without a valve of those two
        /// kinds, or one cut out, none of which count.
        /// </summary>
        public static double Supply(CarBrake car)
        {
            if (car == null || car.Locomotive != null || car.Valve == null || car.Valve.Isolated)
                return double.NaN;
            if (car.Distributor != null)
                return Math.Min(car.Distributor.AuxiliaryReservoir.Pressure, car.Distributor.ControlReservoir.Pressure);
            return car.ControlValve != null ? car.ControlValve.AuxiliaryReservoir.Pressure : double.NaN;
        }

        /// <summary>
        /// Counts <paramref name="cars"/>' braked cars and those short of air
        /// against <paramref name="running"/>, the brake valve's running
        /// pressure, gauge Pa.
        /// </summary>
        public static void Count(IReadOnlyList<CarBrake> cars, double running, out int braked, out int shortOfAir)
        {
            braked = 0;
            shortOfAir = 0;
            var least = Air.Atmosphere + running - ShortBy;
            for (var i = 0; i < cars.Count; i++)
            {
                var supply = Supply(cars[i]);
                if (double.IsNaN(supply))
                    continue;
                braked++;
                if (supply < least)
                    shortOfAir++;
            }
        }

        /// <summary>Whether <paramref name="valve"/> stands in a position that applies the train's brakes.</summary>
        public static bool Applying(DriverBrakeValve valve)
        {
            if (valve == null || !valve.CutIn)
                return false;
            switch (valve.Position)
            {
                case DriverValvePosition.Apply:
                case DriverValvePosition.Emergency:
                    return true;
                case DriverValvePosition.Service:
                    return valve.ServiceReduction > 0 || valve.Spec.MinimumReduction > 0;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Whether to tell the driver: the brake has just gone from off to
        /// applying, enough of the cars are short, and the notice wasn't
        /// given within <see cref="RepeatAfter"/>.
        /// </summary>
        public static bool Tell(bool wasApplying, bool applying, int braked, int shortOfAir, double sinceLast) =>
            applying && !wasApplying && braked > 0 && shortOfAir >= Math.Max(1.0, WorthTelling * braked) && sinceLast >= RepeatAfter;

        /// <summary>The notice's text.</summary>
        public static string Message(int braked, int shortOfAir) =>
            shortOfAir >= braked
                ? braked == 1
                    ? "The car is short of air and will brake weakly: give the train time to charge"
                    : $"All {braked} cars are short of air and will brake weakly: give the train time to charge"
                : $"{shortOfAir} of {braked} cars are short of air and will brake weakly: give the train time to charge";
    }
}
