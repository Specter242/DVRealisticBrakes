using System.Globalization;
using System.Linq;
using UnityEngine;

namespace RealisticBrakes.Controls
{
 internal sealed class LeverBench : MonoBehaviour
    {
        private const int WindowId = 0x5242_4C42; // "RBLB"
        private const float Width = 560f;
        private const float LabelWidth = 250f;
        private const float ValueWidth = 60f;

        // The levers are refitted at most this often while a slider moves, s.
        private const float RefitEvery = 0.15f;

        // The nearest fitted car within this, m.
        private const float NearestWithin = 30f;

        private static readonly string[] Labels =
        {
            "Across, m (car frame, + right; its sign is the side)", "Up, m", "Along, m (+ forward)",
            "Spacing, m (to the next lever, + forward)", "Turn, ° (about the car's up)", "Roll, ° (about the car's length)",
            "Size (× the S282's cock)", "Label below, m (− above)", "Label out, m (where there's no face to snap to)",
        };

        // Each slider's reach either side of where it started.
        private static readonly float[] Spans = { 0.6f, 0.6f, 1.5f, 0.4f, 90f, 90f, 1.5f, 0.3f, 0.15f };

        private bool open;
        private bool snap = true;
        private TrainCar target;
        private string type;
        private float[] values;
        private float[] starts;
        private bool dirty;
        private float refitAt;
        private Rect window = new Rect(24f, 60f, Width, 420f);

        private void Update()
        {
            var key = Main.Settings?.DevBenchKey;
            if (key == null || key.keyCode == KeyCode.None || !key.Down())
                return;
            open = !open;
            if (open)
                Retarget();
        }

        private void Retarget()
        {
            var player = PlayerManager.PlayerTransform != null ? PlayerManager.PlayerTransform.position : (Vector3?)null;
            target = player.HasValue
                ? CarLevers.Fitted.Where(c => Vector3.Distance(c.transform.position, player.Value) < NearestWithin)
                    .OrderBy(c => Vector3.Distance(c.transform.position, player.Value)).FirstOrDefault()
                : null;
            type = CarLevers.TypeId(target);
            values = null;
        }

        private void OnGUI()
        {
            if (!open)
                return;
            if (dirty && Event.current.type == EventType.Repaint && Time.unscaledTime >= refitAt + RefitEvery)
            {
                dirty = false;
                refitAt = Time.unscaledTime;
                foreach (var car in CarLevers.Fitted.Where(c => CarLevers.TypeId(c) == type).ToList())
                    CarLevers.Refit(car);
            }
            window = GUILayout.Window(WindowId, window, Draw, "Lever bench (development; Alt frees the mouse)", GUILayout.Width(Width));
        }

        private void Draw(int id)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(target != null ? $"Seating '{type}' ({target.ID})" : "No car with levers here: stand by one", GUILayout.Width(Width - 230f));
            if (GUILayout.Button("Nearest"))
                Retarget();
            if (GUILayout.Button("Write to the log"))
                Write();
            GUILayout.EndHorizontal();
            if (target != null && type != null && GUILayout.Button("Put the first lever where the crosshair aims, on this car"))
                PutHere();
            GUILayout.Label("Starts from this type's seat (its table's, or beside the bleed handle) and moves live; nothing here is saved.");
            if (target != null && type != null && Load())
            {
                for (var i = 0; i < values.Length; i++)
                {
                    var before = values[i];
                    var value = Slider(Labels[i], before, starts[i] - Spans[i], starts[i] + Spans[i]);
                    if (!Mathf.Approximately(value, before))
                    {
                        values[i] = value;
                        CarLevers.DevSeats[type] = SeatOf(values, snap);
                        dirty = true;
                    }
                }
                var snapped = GUILayout.Toggle(snap, "Snap the labels to the car's face (off: \"Label out\" sets their depth)");
                if (snapped != snap)
                {
                    snap = snapped;
                    CarLevers.DevSeats[type] = SeatOf(values, snap);
                    dirty = true;
                }
                GUILayout.BeginHorizontal();
                GUILayout.Label($"CarLevers.Seats: {Line(type, values, snap)}");
                if (GUILayout.Button("Start over", GUILayout.Width(90f)))
                {
                    CarLevers.DevSeats.Remove(type);
                    values = null;
                    dirty = true;
                }
                GUILayout.EndHorizontal();
            }
            GUI.DragWindow(new Rect(0f, 0f, Width, 20f));
        }

        // Moves the first lever to the point of the car under the crosshair, a little out from it, and the sliders' ranges with it.
        private void PutHere()
        {
            var camera = PlayerManager.ActiveCamera;
            if (camera == null || !Load())
                return;
            var ray = new Ray(camera.transform.position, camera.transform.forward);
            var hits = Physics.RaycastAll(ray, AimReach, ~0, QueryTriggerInteraction.Ignore)
                .Where(h => TrainCar.Resolve(h.collider.transform) == target)
                .OrderBy(h => h.distance)
                .ToList();
            if (hits.Count == 0)
            {
                Main.Mod.Logger.Log("Lever bench: aim at the car itself, within 10 m");
                return;
            }
            var hit = hits[0];
            var local = CarLevers.Frame(target).InverseTransformPoint(hit.point + hit.normal * AimStandOff);
            values[0] = local.x;
            values[1] = local.y;
            values[2] = local.z;
            starts = (float[])values.Clone();
            CarLevers.DevSeats[type] = SeatOf(values, snap);
            dirty = true;
        }

        // The crosshair's reach, and how far out from the surface it hits the first lever's pivot goes, m.
        private const float AimReach = 10f;
        private const float AimStandOff = 0.03f;

        // Reads the target type's seat into the sliders, once.
        private bool Load()
        {
            if (values != null)
                return true;
            var bleed = CarLevers.BleedOf(target);
            if (bleed == null)
            {
                GUILayout.Label("Its outside isn't loaded: walk nearer.");
                return false;
            }
            var seat = CarLevers.SeatFor(target, bleed);
            values = new[] { seat.First.x, seat.First.y, seat.First.z, seat.Spacing, seat.Turn, seat.Roll, seat.Scale, seat.LabelDrop, seat.LabelOut };
            snap = seat.SnapLabels;
            starts = (float[])values.Clone();
            return true;
        }

        private static Seat SeatOf(float[] v, bool snap) => new Seat(new Vector3(v[0], v[1], v[2]), v[3], v[4], v[5], v[6], v[7], v[8], snap);

        private void Write()
        {
            if (target == null || type == null || !Load())
            {
                Main.Mod.Logger.Log("Lever bench: no car with levers to seat");
                return;
            }
            Main.Mod.Logger.Log($"Lever bench, CarLevers.Seats: {Line(type, values, snap)}");
        }

        private static string Line(string type, float[] v, bool snap) =>
            $"[\"{type}\"] = new Seat(new Vector3({M(v[0])}, {M(v[1])}, {M(v[2])}), {M(v[3])}, {Deg(v[4])}, {Deg(v[5])}, {M(v[6])}, {M(v[7])}, {M(v[8])}, {(snap ? "true" : "false")}),";

        // Numbers as the table writes them: invariant, no "-0", with the float suffix.
        private static string Number(float value, string format)
        {
            var text = value.ToString(format, CultureInfo.InvariantCulture);
            if (text.StartsWith("-") && float.Parse(text, CultureInfo.InvariantCulture) == 0f)
                text = text.Substring(1);
            return text + "f";
        }

        private static string M(float value) => Number(value, "0.###");
        private static string Deg(float value) => Number(value, "0");

        private static float Slider(string label, float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(LabelWidth));
            value = GUILayout.HorizontalSlider(value, min, max);
            GUILayout.Label(value.ToString("0.###", CultureInfo.InvariantCulture), GUILayout.Width(ValueWidth));
            GUILayout.EndHorizontal();
            return value;
        }
    }
}
