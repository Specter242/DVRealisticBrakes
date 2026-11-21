using System.Collections.Generic;
using UnityEngine;

namespace RealisticBrakes.Controls
{
    internal static partial class CarLevers
    {
        /// <summary>
        /// Where a car type's levers sit (its <c>TrainCarType_v2.id</c>): set
        /// with the lever bench in game, its log lines pasted in here.
        /// </summary>
        internal static readonly Dictionary<string, Seat> Seats = new Dictionary<string, Seat>
        {
            ["Flatbed"] = new Seat(new Vector3(1.397f, 0.92f, 0.306f), 0.306f, 0f, 0f, 1.8f, -0.137f),
            ["Hopper"] = new Seat(new Vector3(-1.398f, 1.024f, -6.685f), 0.351f, 0f, 0f, 1.8f, -0.091f),
            ["Gondola"] = new Seat(new Vector3(1.401f, 1.107f, 0.489f), 0.279f, 0f, 0f, 1.8f, 0.1f),
            ["Boxcar"] = new Seat(new Vector3(1.222f, 1.061f, 1.566f), 0.378f, 0f, 0f, 1.8f, 0.1f),
            ["TankOil"] = new Seat(new Vector3(-1.19f, 1.061f, -4.814f), -0.317f, 0f, 0f, 1.8f, 0.054f, 0.05f),
            ["Autorack"] = new Seat(new Vector3(1.392f, 0.795f, -0.206f), -0.344f, 0f, 0f, 1.8f, 0.1f),
            ["TankShortFood"] = new Seat(new Vector3(-1.19f, 1.068f, -3.038f), -0.29f, 0f, 0f, 1.8f, 0.049f),
            ["TankGas"] = new Seat(new Vector3(-1.213f, 1.054f, -4.845f), -0.321f, 0f, 0f, 1.8f, 0.04f),
            ["TankChem"] = new Seat(new Vector3(-1.19f, 1.065f, -4.83f), -0.294f, 0f, 0f, 1.8f, 0.063f),
            ["Stock"] = new Seat(new Vector3(1.193f, 0.992f, 1.508f), 0.317f, 0f, 0f, 1.8f, -0.094f),
            ["Refrigerator"] = new Seat(new Vector3(1.199f, 0.986f, 1.551f), 0.298f, 0f, 0f, 1.8f, 0.051f),
            ["Passenger"] = new Seat(new Vector3(1.406f, 1.085f, 1.932f), 0.329f, 0f, 0f, 1.8f, 0.066f),
            ["NuclearFlask"] = new Seat(new Vector3(1.344f, 1.277f, -4.343f), 0.332f, 0f, 0f, 1.8f, 0.1f),
            ["HopperCovered"] = new Seat(new Vector3(1.39f, 1.085f, 0.378f), 0.397f, 0f, 0f, 1.8f, 0.069f),
            ["Caboose"] = new Seat(new Vector3(1.304f, 1.043f, -0.024f), 0.18f, 0f, 0f, 1.8f, 0.1f, 0.05f),
        };

        /// <summary>The lever bench's seats this session, over <see cref="Seats"/>; never saved.</summary>
        internal static readonly Dictionary<string, Seat> DevSeats = new Dictionary<string, Seat>();

        // A label stands this far proud of the face it's snapped to, m; the face is looked for from this far out, and this far in.
        private const float LabelProud = 0.004f;
        private const float SurfaceFrom = 0.5f;
        private const float SurfaceDepth = 0.4f;

        private const float SnapReach = 0.08f;

        /// <summary>
        /// The car's face at <paramref name="spot"/>, looking in from outside
        /// along <paramref name="outward"/>: the first of its walking and item
        /// collision, which trace its parts. Its coarse car-to-car box, its
        /// bogies, and its outside controls (these levers among them) don't count.
        /// </summary>
        private static bool Surface(TrainCar car, Vector3 spot, Vector3 outward, out Vector3 point, out Vector3 normal)
        {
            point = spot;
            normal = outward;
            var controls = car.loadedExternalInteractables != null ? car.loadedExternalInteractables.transform : null;
            var ray = new Ray(spot + outward * SurfaceFrom, -outward);
            var best = float.PositiveInfinity;
            foreach (var hit in Physics.RaycastAll(ray, SurfaceFrom + SurfaceDepth, ~0, QueryTriggerInteraction.Ignore))
            {
                var collider = hit.collider.transform;
                if (hit.distance >= best || (controls != null && collider.IsChildOf(controls)) || TrainCar.Resolve(collider) != car || !IsFace(collider)
                    || Vector3.Dot(hit.point - spot, outward) > SnapReach)
                    continue;
                best = hit.distance;
                point = hit.point;
                normal = hit.normal;
            }
            return !float.IsPositiveInfinity(best);
        }

        // Under the car's walking or item collision.
        private static bool IsFace(Transform collider)
        {
            for (var t = collider; t != null; t = t.parent)
                if (t.name == "[items]" || t.name == "[walkable]")
                    return true;
            return false;
        }
    }

    /// <summary>Where a car type's levers sit, in the car's frame.</summary>
    internal readonly struct Seat
    {
        /// <summary>The labels' depth before it was set per type: at the handles, 5 cm out from the pivots.</summary>
        public const float DefaultLabelOut = 0.05f;

        public Seat(Vector3 first, float spacing, float turn, float roll, float scale, float labelDrop, float labelOut = DefaultLabelOut, bool snapLabels = true)
        {
            SnapLabels = snapLabels;
            First = first;
            Spacing = spacing;
            Turn = turn;
            Roll = roll;
            Scale = scale;
            LabelDrop = labelDrop;
            LabelOut = labelOut;
        }

        /// <summary>The first lever's pivot, m, car frame (x across, + right; y up; z along, + forward). Its side is x's sign.</summary>
        public Vector3 First { get; }

        /// <summary>From one lever to the next along the car, m (+ forward).</summary>
        public float Spacing { get; }

        /// <summary>The levers turned about the car's up, °.</summary>
        public float Turn { get; }

        /// <summary>The levers rolled about the car's length, °.</summary>
        public float Roll { get; }

        /// <summary>The levers' size, times the S282's cock.</summary>
        public float Scale { get; }

        /// <summary>Each label this far below its lever, m (negative: above).</summary>
        public float LabelDrop { get; }

 public bool SnapLabels { get; }

 public float LabelOut { get; }
    }
}
