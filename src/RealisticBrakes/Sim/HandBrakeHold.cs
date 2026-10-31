using System.Collections.Generic;
using System.Linq;
using DV.Simulation.Brake;
using DV.Utils;
using HarmonyLib;
using RealisticBrakes.Core;
using UnityEngine;
using UnityModManagerNet;

namespace RealisticBrakes.Sim
{
 internal sealed class HandBrakeHold : MonoBehaviour
    {
        // A cut every car of which is slower than this, m/s, stands (the freeze acts under 0.01–0.1).
        private const float StandingSpeed = 0.15f;

        // The braking factors either side of the game's freeze at a half.
        private const float Frozen = 0.55f;
        private const float Free = 0.45f;

        // A car whose brakes give less than this target factor is unbraked here.
        private const float Braked = 0.01f;

        // A spawned or loaded car's cut is weighed this long after, s, once its couplings have formed.
        private const float SecureAfter = 3f;

        private readonly Dictionary<TrainCar, float> fresh = new Dictionary<TrainCar, float>();
        private readonly List<TrainCar> due = new List<TrainCar>();
        private readonly List<HeldCar> held = new List<HeldCar>();
        private readonly HashSet<TrainCar> scaled = new HashSet<TrainCar>();
        private readonly HashSet<TrainCar> scaledNow = new HashSet<TrainCar>();
        private CarSpawner spawner;

        // The game's easing rate for a car's braking factor (BrakeSystem.brakingFactorRef, internal).
        private static readonly AccessTools.FieldRef<BrakeSystem, float> easingRate = AccessTools.FieldRefAccess<BrakeSystem, float>("brakingFactorRef");

        private void LateUpdate()
        {
            Watch();
            scaledNow.Clear();
            if (Main.Active)
            {
                SecureFresh();
                foreach (var set in Trainset.allSets)
                    if (set != null && Standing(set))
                        Hold(set);
            }
            // A car no longer held gets back its force per kg and its factor at once: the rigging's or the game's (a review,
            // 2026-10-27: with the brake force from the load, the rigging stops rewriting it, and the scaled force stayed).
            foreach (var car in scaled)
                if (!scaledNow.Contains(car) && car != null)
                    Restore(car);
            scaled.Clear();
            scaled.UnionWith(scaledNow);
        }

        // Follows the game's spawner, which isn't there until a game loads (it isn't made on demand: no AllowAutoCreate).
        private void Watch()
        {
            var current = SingletonBehaviour<CarSpawner>.Instance;
            if (current == spawner)
                return;
            if (spawner != null)
                spawner.CarSpawned -= OnSpawned;
            spawner = current;
            fresh.Clear();
            if (spawner != null)
                spawner.CarSpawned += OnSpawned;
        }

        private void OnSpawned(TrainCar car)
        {
            if (car != null)
                fresh[car] = Time.time;
        }

        private static bool Standing(Trainset set)
        {
            foreach (var car in set.cars)
                if (car == null || car.derailed || Mathf.Abs(car.GetForwardSpeed()) > StandingSpeed)
                    return false;
            return true;
        }

        // Sets the freeze for a standing cut from whether its brakes hold it. A cut just spawned or loaded is frozen outright,
        // its unbraked cars too, until it's weighed and secured (SecureFresh), so it can't roll off before then (a review,
        // 2026-10-27: a fresh cut with no brake on had nothing to freeze).
        private void Hold(Trainset set)
        {
            var unweighed = Fresh(set);
            var holds = unweighed || Weigh(set);
            var frozen = false;
            foreach (var car in set.cars)
            {
                var brakes = car.brakeSystem;
                if (brakes == null || !BrakeSets.Handles(brakes))
                    continue;
                var target = brakes.unaffectedTargetBrakingFactor * brakes.heatController.overheatReductionFactor;
                if (target < Braked && !unweighed)
                    continue;
                var factor = holds ? Mathf.Max(target, Frozen) : Mathf.Min(target, Free);
                // The same force at the new factor; none for an unbraked car frozen. Not where another mod keeps the force.
                if (!ForceKeptByOtherMod(car))
                {
                    var basePerKg = BasePerKg(car);
                    foreach (var bogie in car.Bogies)
                        if (bogie != null)
                            bogie.maxBrakingForcePerKg = basePerKg * Mathf.Max(0f, target) / factor;
                }
                SetFactor(brakes, factor);
                scaledNow.Add(car);
                frozen |= holds;
            }
            if (frozen && !Net.IsGuest)
                StopCreep(set);
        }

        // The game freezes a slow car only under a hair of speed, 0.01 m/s on the level to 0.1 at 5°, and below 0.1 m/s fades
        // its brake force with its speed (Bogie.FixedUpdate). A cut its brakes hold, once something set it moving past the
        // freeze, came back to rest only if its brakes were several times gravity's pull (about 5 times on 1 %): one that
        // only just held crept on for good, at the speed where its faded force met the pull, about 0.1 m/s (Torun,
        // 2026-10-31, after a cut slid: "the slightest amount of motion which then adds up over time"; and "some sort of
        // small impulse ... as a result of client-host interaction" to set it going). Its brakes hold it, as a real car's
        // do at a standstill, so it's stopped and the game's freeze takes it. The host's, which owns movement. Only a cut
        // without an engine: a train starting before its rear has released is pulled by its engine, not creeping.
        private static void StopCreep(Trainset set)
        {
            var creeping = false;
            foreach (var car in set.cars)
            {
                if (car == null)
                    continue;
                if (car.IsLoco)
                    return;
                var x = Mathf.Abs(car.transform.localEulerAngles.x);
                var degrees = x > 180f ? Mathf.Abs(x - 360f) : x;
                // On 5° and steeper the game doesn't freeze, and neither does this.
                if (degrees >= 5f)
                    return;
                creeping |= Mathf.Abs(car.GetForwardSpeed()) > Mathf.Lerp(0.01f, 0.1f, Mathf.InverseLerp(0f, 5f, degrees));
            }
            if (!creeping)
                return;
            foreach (var car in set.cars)
            {
                if (car == null)
                    continue;
                Stop(car.rb);
                foreach (var bogie in car.Bogies)
                    if (bogie != null)
                        Stop(bogie.rb);
            }
        }

        private static void Stop(Rigidbody body)
        {
            if (body == null || body.isKinematic)
                return;
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        // The Wardale 5AT's Kss brake plugin, when it runs: it keeps the 5AT's force per kg (Holding.ForceKeptByOtherMod).
        private static UnityModManager.ModEntry wardaleBrakes;
        private static bool lookedForWardaleBrakes;

        private static bool ForceKeptByOtherMod(TrainCar car)
        {
            if (!lookedForWardaleBrakes)
            {
                wardaleBrakes = UnityModManager.FindMod(Holding.WardaleBrakesId);
                lookedForWardaleBrakes = true;
            }
            var type = car.carLivery != null ? car.carLivery.parentType : null;
            return Holding.ForceKeptByOtherMod(type != null ? type.id : null, wardaleBrakes != null && wardaleBrakes.Active);
        }

        private static void SetFactor(BrakeSystem brakes, float factor)
        {
            brakes.brakingFactor = factor;
            easingRate(brakes) = 0f;
        }

        private bool Fresh(Trainset set)
        {
            if (fresh.Count == 0 || Net.IsGuest)
                return false;
            foreach (var car in set.cars)
                if (car != null && fresh.ContainsKey(car))
                    return true;
            return false;
        }

        // Whether the cut's brakes hold it: each car's force as the game will give it, its grade along the cut.
        private bool Weigh(Trainset set)
        {
            held.Clear();
            var cars = set.cars;
            for (var i = 0; i < cars.Count; i++)
            {
                var car = cars[i];
                var brakes = car.brakeSystem;
                var mass = car.massController != null ? car.massController.TotalMass : 0f;
                var force = 0f;
                if (brakes != null)
                {
                    var target = brakes.unaffectedTargetBrakingFactor * brakes.heatController.overheatReductionFactor;
                    var perKg = BasePerKg(car);
                    foreach (var bogie in car.Bogies)
                        if (bogie != null && bogie.rb != null)
                            force += target * perKg * bogie.rb.mass;
                }
                var forward = car.transform.forward;
                var sense = Sense(cars, i);
                var rolling = car.carLivery != null && car.carLivery.parentType != null ? car.carLivery.parentType.RollingResistanceCoef : 0.002f;
                held.Add(new HeldCar(mass, force, forward.y * sense, rolling));
            }
            return Holding.Holds(held);
        }

        // Whether car i faces along the cut, from its first car to its last, by its couplings rather than its heading (a review,
        // 2026-10-27: around a hairpin, a car's heading against the first car's turns it back). +1 along, -1 against.
        private static float Sense(List<TrainCar> cars, int i)
        {
            var car = cars[i];
            if (cars.Count < 2)
                return 1f;
            if (i == 0)
                return Joins(car.rearCoupler, cars[1]) ? -1f : 1f;
            if (Joins(car.frontCoupler, cars[i - 1]))
                return -1f;
            if (Joins(car.rearCoupler, cars[i - 1]))
                return 1f;
            // Not in coupling order: by heading against the car before it, as that one faces.
            return Vector3.Dot(car.transform.forward, cars[i - 1].transform.forward) < 0f ? -Sense(cars, i - 1) : Sense(cars, i - 1);
        }

        private static bool Joins(Coupler coupler, TrainCar other) =>
            coupler != null && coupler.coupledTo != null && coupler.coupledTo.train == other;

        // The car's force per kg before the freeze's scaling: the rigging's as set this frame, or the game's own.
        private static float BasePerKg(TrainCar car)
        {
            if (BrakeSets.TryGet(car.brakeSystem, out var air) && air.Rigging != null)
                return air.RiggedPerKg;
            return StockPerKg(car);
        }

        private static float StockPerKg(TrainCar car) =>
            car.carLivery != null && car.carLivery.parentType != null
                ? car.carLivery.parentType.brakes.MaxBrakingForcePerBogie * CarAir.StockForcePerKg
                : 0f;

        private static void Restore(TrainCar car)
        {
            // With the model off, the game's force: the rigging's isn't kept up then.
            var perKg = Main.Active ? BasePerKg(car) : StockPerKg(car);
            if (!ForceKeptByOtherMod(car))
                foreach (var bogie in car.Bogies)
                    if (bogie != null)
                        bogie.maxBrakingForcePerKg = perKg;
            var brakes = car.brakeSystem;
            if (brakes != null)
                SetFactor(brakes, brakes.unaffectedTargetBrakingFactor * brakes.heatController.overheatReductionFactor);
        }

        // A cut the game spawned or loaded, weighed once its couplings have formed: if its brakes can't hold it, its hand brakes go on.
        private void SecureFresh()
        {
            if (fresh.Count == 0 || Net.IsGuest)
                return;
            due.Clear();
            foreach (var pair in fresh)
                if (pair.Key == null || Time.time - pair.Value > SecureAfter)
                    due.Add(pair.Key);
            if (due.Count == 0)
                return;
            var sets = new HashSet<Trainset>();
            foreach (var car in due)
            {
                fresh.Remove(car);
                if (car != null && car.trainset != null)
                    sets.Add(car.trainset);
            }
            foreach (var set in sets)
            {
                if (!Standing(set) || Weigh(set))
                    continue;
                var set_ = 0;
                foreach (var car in set.cars)
                    if (car.brakeSystem != null && car.brakeSystem.hasHandbrake && car.brakeSystem.handbrakePosition < 1f)
                    {
                        car.brakeSystem.SetHandbrakePosition(1f);
                        set_++;
                    }
                if (set_ > 0)
                    Main.Mod?.Logger.Log($"Set {set_} hand brakes on a standing cut of {set.cars.Count} cars that its brakes couldn't hold (from {set.firstCar?.ID})");
            }
        }
    }
}
