using System.Collections.Generic;
using System.Linq;
using DV;
using DV.Simulation.Brake;
using DV.Utils;
using RealisticBrakes.Sim;
using TMPro;
using UnityEngine;
using Lever = DV.CabControls.Spec.Lever;

namespace RealisticBrakes.Controls
{
    /// <summary>Which of a car's brake levers.</summary>
    internal enum CarLeverKind
    {
        /// <summary>A European freight car's G/P lever.</summary>
        BrakingMode,

        /// <summary>A European freight car's empty/loaded lever, when set by hand.</summary>
        EmptyLoaded,

        /// <summary>The isolating (cut-out) cock on the valve's branch pipe.</summary>
        IsolatingCock,

        /// <summary>An American car's retaining valve.</summary>
        Retainer,
    }

 internal static partial class CarLevers
    {
        private const string SourceLivery = "LocoS282A";
        private const string SourcePath = "Right/BrakeIsolationValveInterior";
        private const string SourceControl = "C_BrakeIsolationValve";
        private const string SourceLabel = "LocoS282A_Interior_Labels/Blower/Label_RM_1/Text";

        // The cab cock's bindings and its link to the S282's air, which a car doesn't have.
        private static readonly HashSet<string> Stripped = new HashSet<string>
        {
            "InteractablePortFeeder", "AnalogSetValueJoystickInput", "ToggleValueKeyboardInput", "MouseScrollKeyboardInput",
        };

        private const float FirstOffset = 0.2f;
        private const float Spacing = 0.28f;
        private const float Inset = 0.04f;
        private const float Scale = 1.8f;
        private const float LabelDrop = 0.1f;

        // A row runs toward the car's end only with this much room left before it, m.
        private const float EndRoom = 2f;

        // A label's height, m.
        private const float LabelHeight = 0.03f;

        private const string FrameName = "RealisticBrakes levers";

        // Each fitted car's row: a row takes its car off as it goes, if it is still the car's (a review, 2026-10-27: a static
        // list kept every car ever fitted, and its whole object graph, across play and sessions; then a refit's old row,
        // destroyed after the new one was fitted, took the car off).
        private static readonly Dictionary<TrainCar, Row> fitted = new Dictionary<TrainCar, Row>();

        /// <summary>The cars whose levers are fitted now.</summary>
        internal static IEnumerable<TrainCar> Fitted
        {
            get
            {
                foreach (var car in fitted.Where(p => p.Key == null || p.Value == null || p.Key.loadedExternalInteractables == null).Select(p => p.Key).ToList())
                    fitted.Remove(car);
                return fitted.Keys;
            }
        }

        /// <summary>
        /// Fits every loaded car's levers again, as the settings that choose
        /// them change (equipment, empty/loaded by hand, brake force from the
        /// car, here or taken from a session's host; the model turned on or
        /// off): a review, 2026-10-27, found rows left as they were.
        /// </summary>
        internal static void RefitAll()
        {
            if (Main.Mod == null)
                return;
            var spawner = SingletonBehaviour<CarSpawner>.Instance;
            if (spawner == null)
                return;
            foreach (var car in spawner.AllCars.ToList())
                if (car != null && car.loadedExternalInteractables != null)
                    Refit(car);
        }

        // On each row: takes its car off the list as the row goes, if it is still the car's.
        private sealed class Row : MonoBehaviour
        {
            // The interior is put back on the car when it's off by more than this, m or °.
            private const float Off = 0.0005f;
            private const float Turned = 0.01f;

            public TrainCar Car;

            private TrainCarInteriorPhysics follower;

 private void LateUpdate()
            {
                if (!apart)
                    KeepApart();
                if (Car == null || Car.interior == null || (follower == null && !Car.TryGetComponent(out follower)) || follower.enabled)
                    return;
                var car = Car.transform;
                var interior = Car.interior;
                if ((interior.position - car.position).sqrMagnitude > Off * Off || Quaternion.Angle(interior.rotation, car.rotation) > Turned)
                    follower.SyncPosition();
            }

            // Whether the levers' collisions with each other are still to be switched off.
            private bool apart;

            // The physics forgets ignored pairs when a collider is switched off: each time the row comes on, again.
            private void OnEnable() => apart = false;

            /// <summary>
            /// Keeps the row's levers from striking each other. Each handle is
            /// a sprung hinge with a rigidbody; swung against its neighbor it
            /// was pushed back to its stop (TheIronWolf2003, 2026-10-29). Done
            /// once the handles have woken, as a rigidbody added then would
            /// renew their colliders.
            /// </summary>
            private void KeepApart()
            {
                apart = true;
                var levers = new List<Collider[]>();
                foreach (Transform holder in transform)
                    levers.Add(holder.GetComponentsInChildren<Collider>(true));
                for (var a = 0; a < levers.Count; a++)
                    for (var b = a + 1; b < levers.Count; b++)
                        foreach (var mine in levers[a])
                            foreach (var theirs in levers[b])
                                Physics.IgnoreCollision(mine, theirs);
            }

            private void OnDestroy()
            {
                if (Car != null && fitted.TryGetValue(Car, out var row) && row == this)
                    fitted.Remove(Car);
            }
        }

        /// <summary>A car type's key in <see cref="Seats"/>.</summary>
        internal static string TypeId(TrainCar car) => car != null && car.carLivery != null && car.carLivery.parentType != null ? car.carLivery.parentType.id : null;

        private static readonly Color Ink = new Color(0.92f, 0.9f, 0.84f);

        /// <summary>Fits <paramref name="car"/>'s brake levers among its outside controls, <paramref name="controls"/>.</summary>
        public static void Fit(TrainCar car, Transform controls)
        {
            var system = car != null ? car.brakeSystem : null;
            if (system == null || controls == null || system.hasCompressor
                || system.brakeCylinderPressureCalculation != BrakeSystem.BrakeCylinderPressureCalculation.Regular
                || controls.GetComponentInChildren<CarLever>(true) != null)
                return;
            var bleed = controls.GetComponentsInChildren<BrakeCylinderReleaseButtonFeeder>(true).FirstOrDefault();
            if (bleed == null)
                return;
            var kinds = KindsFor(car).ToList();
            if (kinds.Count == 0 || !Sources(out var source, out var font))
                return;
            var seat = SeatFor(car, bleed.transform);

            // Outward on the seat's side; turned about the car's up, then rolled about its length.
            var body = Frame(car);
            var outward = body.right * (seat.First.x < 0f ? -1f : 1f);
            var rotation = Quaternion.AngleAxis(seat.Turn, body.up) * Quaternion.AngleAxis(seat.Roll, body.forward) * Quaternion.LookRotation(outward, body.up);

            var frame = new GameObject(FrameName);
            frame.SetActive(false);
            frame.transform.SetParent(controls, false);
            var row = frame.AddComponent<Row>();
            row.Car = car;
            var placed = new List<(Transform Holder, CarLeverKind Kind, Vector3 At)>();
            for (var i = 0; i < kinds.Count; i++)
            {
                var at = body.TransformPoint(seat.First + Vector3.forward * (seat.Spacing * i));
                placed.Add((Place(source, frame.transform, car, kinds[i], at, rotation, seat), kinds[i], at));
            }
            frame.SetActive(true);
            // TextMeshPro lays its text out only once active: the labels go on after (as Realistic Firing writes its gauges' scales).
            if (font != null)
                foreach (var (holder, kind, at) in placed)
                {
                    var spot = at - body.up * seat.LabelDrop;
                    var facing = rotation;
                    if (seat.SnapLabels && Surface(car, spot, outward, out var surface, out var normal))
                    {
                        spot = surface + normal * LabelProud;
                        facing = Quaternion.LookRotation(normal, Vector3.ProjectOnPlane(body.up, normal));
                    }
                    else
                        spot += outward * seat.LabelOut;
                    Label(holder, font, LabelFor(kind), spot, facing);
                    PositionLabels(car, holder, kind, font, seat, outward, rotation);
                }
            fitted[car] = row;
        }

 internal static Transform Frame(TrainCar car) => car.interior != null ? car.interior : car.transform;

        /// <summary>Takes <paramref name="car"/>'s levers off and fits them again (none while the model is off), as the lever bench moves them.</summary>
        internal static void Refit(TrainCar car)
        {
            var controls = car != null && car.loadedExternalInteractables != null ? car.loadedExternalInteractables.transform : null;
            if (controls == null)
                return;
            var frame = controls.Find(FrameName);
            if (frame != null)
            {
                frame.SetParent(null, false);
                Object.Destroy(frame.gameObject);
            }
            if (Main.Active)
                Fit(car, controls);
        }

        /// <summary>
        /// Where <paramref name="car"/>'s levers sit: the bench's seat or its
        /// type's; else the seat of a type built on the same body (the same
        /// outside controls, as the gas and chemical tankers share the oil
        /// tanker's); else beside its bleed handle.
        /// </summary>
        internal static Seat SeatFor(TrainCar car, Transform bleed)
        {
            var type = TypeId(car);
            if (type != null && (DevSeats.TryGetValue(type, out var seat) || Seats.TryGetValue(type, out seat)))
                return seat;
            var body = car.carLivery != null ? car.carLivery.externalInteractablesPrefab : null;
            if (body != null)
                foreach (var seated in DevSeats.Concat(Seats))
                    if (Globals.G.Types.TryGetCarType(seated.Key, out var other)
                        && other.liveries.Any(l => l != null && l.externalInteractablesPrefab == body))
                        return seated.Value;
            return DefaultSeat(car, bleed);
        }

 internal static Seat DefaultSeat(TrainCar car, Transform bleed)
        {
            var body = Frame(car);
            var local = body.InverseTransformPoint(bleed.position);
            var outward = body.right * (local.x < 0f ? -1f : 1f);
            var toEnd = local.z < 0f ? -1f : 1f;
            var room = car.InterCouplerDistance / 2f - Mathf.Abs(local.z);
            var along = room > EndRoom ? toEnd : -toEnd;
            // Level with the handle's outer face (its pivot sits well inside the ring you pull).
            var start = bleed.position + outward * (OuterFace(bleed, outward) - Vector3.Dot(bleed.position, outward) - Inset);
            var first = body.InverseTransformPoint(start) + Vector3.forward * (along * FirstOffset);
            return new Seat(first, along * Spacing, 0f, 0f, Scale, LabelDrop, Seat.DefaultLabelOut);
        }

        /// <summary>The car's bleed handle, under its outside controls, if loaded.</summary>
        internal static Transform BleedOf(TrainCar car)
        {
            var controls = car != null ? car.loadedExternalInteractables : null;
            var feeder = controls != null ? controls.GetComponentsInChildren<BrakeCylinderReleaseButtonFeeder>(true).FirstOrDefault() : null;
            return feeder != null ? feeder.transform : null;
        }

        // How far out the handle's drawn parts reach along outward, as a dot product with it.
        private static float OuterFace(Transform handle, Vector3 outward)
        {
            var face = Vector3.Dot(handle.position, outward);
            foreach (var renderer in handle.GetComponentsInChildren<Renderer>(true))
            {
                var b = renderer.bounds;
                var reach = Mathf.Abs(b.extents.x * outward.x) + Mathf.Abs(b.extents.y * outward.y) + Mathf.Abs(b.extents.z * outward.z);
                face = Mathf.Max(face, Vector3.Dot(b.center, outward) + reach);
            }
            return face;
        }

        private static IEnumerable<CarLeverKind> KindsFor(TrainCar car)
        {
            if (Main.Settings.Equipment == Equipment.Vacuum)
                yield break;
            if (Main.Settings.Equipment == Equipment.American)
            {
                // A coach's graduated-release equipment has no retainer here: it runs on the level of the passenger lines.
                if (!CarAir.IsCoach(car))
                    yield return CarLeverKind.Retainer;
                yield return CarLeverKind.IsolatingCock;
                yield break;
            }
            var freight = CarAir.RiggingFor(car) != null;
            if (freight)
                yield return CarLeverKind.BrakingMode;
            if (freight && Main.Settings.ManualEmptyLoaded && Main.Settings.BrakeForceFromCar)
                yield return CarLeverKind.EmptyLoaded;
            yield return CarLeverKind.IsolatingCock;
        }

        private static bool Sources(out Transform source, out TextMeshPro font)
        {
            source = null;
            font = null;
            if (!Globals.G.Types.TryGetLivery(SourceLivery, out var livery) || livery.interiorPrefab == null)
                return false;
            var cab = livery.interiorPrefab.transform;
            source = cab.Find(SourcePath);
            var label = cab.Find(SourceLabel);
            font = label != null ? label.GetComponent<TextMeshPro>() : null;
            return source != null && source.Find(SourceControl) != null;
        }

        private static Transform Place(Transform source, Transform frame, TrainCar car, CarLeverKind kind, Vector3 at, Quaternion rotation, Seat seat)
        {
            var holder = Object.Instantiate(source.gameObject, frame, false).transform;
            holder.name = "RealisticBrakes " + kind;
            holder.SetPositionAndRotation(at, rotation);
            holder.localScale = Vector3.one * seat.Scale;
            var control = holder.Find(SourceControl);
            foreach (var component in control.GetComponents<MonoBehaviour>().Where(c => Stripped.Contains(c.GetType().Name)).ToList())
                Object.DestroyImmediate(component);
            var spec = control.GetComponent<Lever>();
            var notches = kind == CarLeverKind.Retainer ? 4 : 2;
            if (spec != null)
            {
                spec.notches = notches;
                spec.jointLimitMin = 0f;
                spec.jointLimitMax = 45f * (notches - 1);
                spec.useSteppedJoint = true;
                spec.steppedValueUpdate = true;
            }
            var lever = control.gameObject.AddComponent<CarLever>();
            lever.Kind = kind;
            lever.Car = car;
            lever.Notches = notches;
            // Under the same setting as the cab's (cj187, 2026-10-31: with the text off, the cut-out and G/P text still showed).
            control.gameObject.AddComponent<HoverText>().Source = () => Main.Settings.ShowValveText ? lever.Describe() : null;
            return holder;
        }

        // The valve's name, under its lever; its positions are round the handle (PositionLabels).
        private static string LabelFor(CarLeverKind kind)
        {
            switch (kind)
            {
                case CarLeverKind.BrakingMode: return "MODE";
                case CarLeverKind.EmptyLoaded: return "LOAD";
                case CarLeverKind.Retainer: return "RETAINER";
                default: return Main.Settings.Equipment == Equipment.American ? "CUT-OUT" : "ISOLATE";
            }
        }

        // Each notch's name, from the first (the lever's value 0) on: the valve's settings in its order (NotchOf).
        private static string[] PositionNames(CarLeverKind kind)
        {
            switch (kind)
            {
                case CarLeverKind.BrakingMode: return new[] { "G", "P" };
                case CarLeverKind.EmptyLoaded: return new[] { "EMPTY", "LOADED" };
                case CarLeverKind.Retainer: return new[] { "EX", "LP", "HP", "SD" };
                default: return new[] { "IN", "OUT" };
            }
        }

        // The position names' letters, m, and how far beyond the handle's tip they stand.
        private const float PositionHeight = 0.02f;
        private const float PositionBeyond = 0.03f;

 private static void PositionLabels(TrainCar car, Transform holder, CarLeverKind kind, TextMeshPro font, Seat seat, Vector3 outward, Quaternion facing)
        {
            var control = holder.Find(SourceControl);
            var spec = control != null ? control.GetComponent<Lever>() : null;
            var names = PositionNames(kind);
            if (spec == null || spec.jointAxis.sqrMagnitude < 1e-8f)
                return;
            var axis = spec.jointAxis.normalized;
            var tip = HandleTip(control, axis);
            if (tip.sqrMagnitude < 1e-10f)
                return;
            var scale = Mathf.Max(control.lossyScale.x, 1e-4f);
            var reach = tip.magnitude + PositionBeyond / scale;
            var step = names.Length > 1 ? (spec.jointLimitMax - spec.jointLimitMin) / (names.Length - 1) : 0f;
            var up = Frame(car).up;
            for (var k = 0; k < names.Length; k++)
            {
                var angle = spec.jointLimitMin + step * (spec.invertDirection ? names.Length - 1 - k : k);
                var spot = control.TransformPoint(Quaternion.AngleAxis(angle, axis) * tip.normalized * reach);
                var face = facing;
                if (seat.SnapLabels && Surface(car, spot, outward, out var surface, out var normal))
                {
                    spot = surface + normal * LabelProud;
                    face = Quaternion.LookRotation(normal, Vector3.ProjectOnPlane(up, normal));
                }
                Label(holder, font, names[k], spot, face, PositionHeight);
            }
        }

        // The handle's tip at rest, in its control's frame, across its joint's axis: as far as its drawn parts reach from the
        // pivot, the way they lean (or their farthest corner, for a handle centred on its pivot). Mesh bounds, as the game's
        // meshes can't be read.
        private static Vector3 HandleTip(Transform control, Vector3 axis)
        {
            var corners = new List<Vector3>();
            foreach (var filter in control.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                    continue;
                var b = filter.sharedMesh.bounds;
                for (var i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    corners.Add(Vector3.ProjectOnPlane(control.InverseTransformPoint(filter.transform.TransformPoint(corner)), axis));
                }
            }
            if (corners.Count == 0)
                return Vector3.zero;
            var mean = Vector3.zero;
            foreach (var c in corners)
                mean += c;
            mean /= corners.Count;
            var farthest = corners[0];
            foreach (var c in corners)
                if (c.sqrMagnitude > farthest.sqrMagnitude)
                    farthest = c;
            // A handle off its pivot leans its way; one centred on it (a T or a wheel), its farthest corner.
            var lean = mean.magnitude > 0.2f * farthest.magnitude ? mean.normalized : farthest.normalized;
            var length = 0f;
            foreach (var c in corners)
                length = Mathf.Max(length, Vector3.Dot(c, lean));
            return lean * length;
        }

        // A label facing outward, upright, sized by its height.
        private static void Label(Transform parent, TextMeshPro font, string text, Vector3 position, Quaternion facing) =>
            Label(parent, font, text, position, facing, LabelHeight);

        private static void Label(Transform parent, TextMeshPro font, string text, Vector3 position, Quaternion facing, float labelHeight)
        {
            var go = new GameObject("label " + text);
            go.transform.SetParent(parent, true);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = font.font;
            tmp.fontSharedMaterial = font.fontSharedMaterial;
            tmp.text = text;
            tmp.color = Ink;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = false;
            tmp.fontSize = 10f;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.ForceMeshUpdate();
            var height = tmp.textBounds.size.y;
            if (!(height > 0f))
            {
                Object.Destroy(go);
                return;
            }
            go.transform.localScale = Vector3.one * (labelHeight / (height * go.transform.parent.lossyScale.x));
            // TextMeshPro reads from its +z side's back: its +z faces into the car, away from whoever reads it.
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(-(facing * Vector3.forward), facing * Vector3.up));
        }
    }
}
