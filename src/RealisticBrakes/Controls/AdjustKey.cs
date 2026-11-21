using System.Collections.Generic;
using RealisticBrakes.Sim;
using UnityEngine;

namespace RealisticBrakes.Controls
{
 internal sealed class AdjustKey : MonoBehaviour
    {
        private static readonly Dictionary<TrainCar, HashSet<byte>> holders = new Dictionary<TrainCar, HashSet<byte>>();

        private TrainCar heldOn;

        /// <summary>Whether anyone holds the key on <paramref name="car"/>.</summary>
        public static bool Held(TrainCar car) => car != null && holders.ContainsKey(car);

        /// <summary>A player's hold on <paramref name="car"/> taken or let go: this game's, or one from the network.</summary>
        public static void SetHold(TrainCar car, byte player, bool held)
        {
            if (car == null)
                return;
            if (!holders.TryGetValue(car, out var set))
            {
                if (!held)
                    return;
                holders[car] = set = new HashSet<byte>();
            }
            if (held)
                set.Add(player);
            else
                set.Remove(player);
            if (set.Count == 0)
                holders.Remove(car);
            if (car.brakeSystem != null && BrakeSets.TryGet(car.brakeSystem, out var air))
                air.Touch();
        }

        /// <summary>Who holds the key on each car (including this game's own hand, as <see cref="TrainBrakeHandle.LocalHand"/>), for a joining guest.</summary>
        public static IEnumerable<(TrainCar Car, byte Player)> Holds()
        {
            foreach (var pair in holders)
                foreach (var player in pair.Value)
                    yield return (pair.Key, player);
        }

        /// <summary>Who holds the key on <paramref name="car"/> from elsewhere.</summary>
        public static IEnumerable<byte> HoldersOf(TrainCar car)
        {
            var list = new List<byte>();
            if (car != null && holders.TryGetValue(car, out var set))
                foreach (var holder in set)
                    if (holder != TrainBrakeHandle.LocalHand)
                        list.Add(holder);
            return list;
        }

        /// <summary>
        /// A player leaves the session (<paramref name="player"/>; every remote
        /// player, when null): their Angleichen holds end. Returns the cars
        /// whose hold ended, for the host to tell its guests.
        /// </summary>
        public static List<TrainCar> ReleaseHands(byte? player)
        {
            var ended = new List<TrainCar>();
            foreach (var pair in new List<KeyValuePair<TrainCar, HashSet<byte>>>(holders))
                foreach (var holder in new List<byte>(pair.Value))
                    if (holder != TrainBrakeHandle.LocalHand && (player == null || holder == player))
                    {
                        SetHold(pair.Key, holder, false);
                        ended.Add(pair.Key);
                    }
            return ended;
        }

        /// <summary>The game resets <paramref name="car"/> for reuse: any holds on it go.</summary>
        public static void Forget(TrainCar car)
        {
            if (car != null)
                holders.Remove(car);
        }

        private void Update()
        {
            Net.Tick();
            var key = Main.Settings?.AdjustKey;
            var car = PlayerManager.Car;
            var down = key != null && key.keyCode != KeyCode.None && Main.Active && key.Pressed()
                && car != null && car.brakeSystem != null && BrakeSets.TryGet(car.brakeSystem, out var air) && Adjustable(air);
            if (down && heldOn != car)
            {
                LetGo();
                heldOn = car;
                SetHold(car, TrainBrakeHandle.LocalHand, true);
                Net.Report(new NetAction(car, NetActionKind.Adjust, 0, 1));
                Notice.Show("Angleichen: the brake pipe rises to at most 5.5 bar while you hold it");
            }
            else if (!down && heldOn != null)
                LetGo();
            else if (key != null && key.keyCode != KeyCode.None && key.Down() && car != null && !down)
                Notice.Show("No Angleichen on this engine's brake valve");
        }

        private void OnDisable() => LetGo();

        private void LetGo()
        {
            if (heldOn == null)
                return;
            SetHold(heldOn, TrainBrakeHandle.LocalHand, false);
            Net.Report(new NetAction(heldOn, NetActionKind.Adjust, 0, 0));
            heldOn = null;
        }

        // An engine whose brake valve has Angleichen: a European one, cut in.
        private static bool Adjustable(CarAir air)
        {
            var valve = air.Brake.Locomotive?.DriverValve;
            return valve != null && valve.CutIn && valve.Spec.AdjustRate > 0;
        }
    }
}
