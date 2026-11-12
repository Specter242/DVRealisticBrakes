using RealisticBrakes.Core;
using RealisticBrakes.Sim;
using UnityEngine;

namespace RealisticBrakes.Controls
{
 internal sealed class ChargeNotice : MonoBehaviour
    {
        private const float Shown = 8f;

        private DriverBrakeValve watched;
        private bool wasApplying;
        private float toldAt = float.NegativeInfinity;

        private void Update()
        {
            var car = Main.Active ? PlayerManager.Car : null;
            var set = car != null && car.brakeSystem != null ? car.brakeSystem.brakeset : null;
            if (set == null || !BrakeSets.TryGetLine(set, out var line))
            {
                watched = null;
                return;
            }
            var valve = Working(line);
            var applying = TrainCharge.Applying(valve);
            // A valve first seen applying (the player stepping onto a braked train) is no application just made.
            if (valve != watched)
            {
                watched = valve;
                wasApplying = applying;
                return;
            }
            if (applying && !wasApplying)
            {
                TrainCharge.Count(line.Cars, valve.Spec.Running, out var braked, out var shortOfAir);
                if (TrainCharge.Tell(wasApplying, applying, braked, shortOfAir, Time.time - toldAt))
                {
                    toldAt = Time.time;
                    Notice.Show(TrainCharge.Message(braked, shortOfAir), Shown);
                }
            }
            wasApplying = applying;
        }

        // The brake valve working the train: the first one cut in.
        private static DriverBrakeValve Working(BrakeLine line)
        {
            for (var i = 0; i < line.Cars.Count; i++)
            {
                var valve = line.Cars[i].Locomotive?.DriverValve;
                if (valve != null && valve.CutIn)
                    return valve;
            }
            return null;
        }
    }
}
