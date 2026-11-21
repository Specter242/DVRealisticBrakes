using System.Linq;
using DV.Indicators;
using DV.Simulation.Brake;
using RealisticBrakes.Core;
using RealisticBrakes.Sim;
using TMPro;
using UnityEngine;

namespace RealisticBrakes.Controls
{
 internal static class BrakeGauges
    {
        // Realistic Firing's front-jet hands (its StokerGauges), and its legend colors.
        private static readonly Color RedHand = new Color(0.75f, 0.08f, 0.05f);
        private static readonly Color BlueHand = new Color(0.1f, 0.35f, 0.85f);
        private const string Red = "#BF140D";
        private const string Blue = "#1A59D9";
        // The game's red cylinder hand (the S282's NeedleRed material).
        internal const string CylinderRed = "#B53D32";
        private static readonly Color Ink = new Color(0.1f, 0.09f, 0.08f);

        private const float LegendDrop = 0.72f;
        private const float LegendHeight = 0.15f;
        private const float LegendWidth = 0.7f;

        // The legend sits this far behind the hands' pivot, m: over the dial, under the hands (seen in game, 2026-10-27).
        private const float LegendDepth = 0.0005f;

        // Two gauges whose pivots are closer than this, m, share a dial.
        internal const float SameDial = 0.002f;

        /// <summary>The name of a dial's legend, under its gauge.</summary>
        internal const string LegendName = "RealisticBrakes legend";

        private static Material redMaterial;
        private static Material blueMaterial;

        /// <summary>Fits <paramref name="car"/>'s cab, under <paramref name="cab"/>, if its train brake is a manual-lap valve.</summary>
        public static void Fit(Component cab, TrainCar car)
        {
            var system = car != null ? car.brakeSystem : null;
            if (system == null || system.selfLappingController || !system.hasTrainBrake || !system.hasCompressor)
                return;
            var mainReservoir = Gauge<IndicatorMainResReader>(cab);
            if (mainReservoir == null || mainReservoir.transform.parent == null || cab.GetComponentInChildren<EqualizingHand>(true) != null)
                return;

            var own = OwnEqualizingHand(cab, mainReservoir);
            if (own != null)
            {
                var drives = own.gameObject.AddComponent<EqualizingHand>();
                drives.Gauge = own;
                drives.System = system;
                drives.Reader = own.GetComponent<IndicatorBrakePipeReader>();
                return;
            }

            var copy = Object.Instantiate(mainReservoir.gameObject, mainReservoir.transform.parent, false);
            copy.name = "I_EqualizingReservoir";
            foreach (var reader in copy.GetComponents<AIndicatorBrakePressureReader>())
                Object.DestroyImmediate(reader);
            var hand = copy.GetComponent<IndicatorGauge>();
            // It sits GaugeHand.Gap behind the stock hand, as the game's own second hand does (the cylinder's, behind the
            // pipe's): metres in the cab, in the units of the frame the hand moves in, as some dials are modeled at another scale.
            var axis = Toward(hand);
            var metresPerUnit = hand.needle.parent != null ? hand.needle.parent.TransformVector(axis).magnitude : 1f;
            hand.needle.localPosition -= axis * (float)GaugeHand.LocalGap(metresPerUnit);
            Paint(mainReservoir, RedHandMaterial());
            Paint(hand, BlueHandMaterial());
            var driver = copy.AddComponent<EqualizingHand>();
            driver.Gauge = hand;
            driver.System = system;

            var font = Font(cab);
            if (font == null || !GameOwn(car))
                return;
            Legend(mainReservoir, font, $"<color={Red}>MR</color>/<color={Blue}>ER</color>");
            var pipe = Gauge<IndicatorBrakePipeReader>(cab);
            var cylinder = Gauge<IndicatorBrakeCylinderReader>(cab);
            if (pipe != null && cylinder != null && Vector3.Distance(pipe.needle.position, cylinder.needle.position) < SameDial)
                Legend(pipe, font, $"BP/<color={CylinderRed}>BC</color>");
        }

        /// <summary>
        /// The equalizing reservoir hand <paramref name="mainReservoir"/>'s dial came with, if any: a second hand on its pivot,
        /// read from the brake pipe (the game has no reader for the equalizing reservoir), while another dial shows the pipe.
        /// A dial with the main reservoir and the pipe and nothing else to show the pipe keeps its hands as they are. Readers
        /// on hands with no mesh, kept for other mods to read (the DE4-740B's, job 2702), show nothing and don't count.
        /// </summary>
        private static IndicatorGauge OwnEqualizingHand(Component cab, IndicatorGauge mainReservoir)
        {
            var pivot = mainReservoir.needle.position;
            var pipes = cab.GetComponentsInChildren<IndicatorBrakePipeReader>(true)
                .Select(r => r.GetComponent<IndicatorGauge>())
                .Where(g => g != null && g.needle != null && g.needle.GetComponentsInChildren<MeshFilter>(true).Any(f => f.sharedMesh != null))
                .ToList();
            var own = pipes.FirstOrDefault(g => Vector3.Distance(g.needle.position, pivot) < SameDial);
            return own != null && pipes.Any(g => Vector3.Distance(g.needle.position, pivot) >= SameDial) ? own : null;
        }

        /// <summary>Whether <paramref name="car"/> is one of the game's own engines, not a Custom Car Loader engine (its own livery type).</summary>
        private static bool GameOwn(TrainCar car) => car.carLivery != null && car.carLivery.GetType() == typeof(DV.ThingTypes.TrainCarLivery);

        private static IndicatorGauge Gauge<TReader>(Component cab) where TReader : AIndicatorBrakePressureReader =>
            cab.GetComponentsInChildren<TReader>(true)
                .Select(r => r.GetComponent<IndicatorGauge>())
                .FirstOrDefault(g => g != null && g.needle != null);

        // Toward the viewer, in the gauge's frame: the hands turn clockwise to them, positively about the rotation axis,
        // which in Unity's left-handed frame is clockwise seen from the axis's tip (the mirrored legend, 2026-10-27).
        internal static Vector3 Toward(IndicatorGauge gauge) => gauge.rotationAxis.normalized;

        internal static void Paint(IndicatorGauge gauge, Material material)
        {
            foreach (var renderer in gauge.needle.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterial = material;
        }

        // The stock hands use the shared gauge atlas and its occlusion map: a plain material keeps the color from darkening, as Realistic Firing's do.
        internal static Material RedHandMaterial() => redMaterial != null ? redMaterial : redMaterial = HandMaterial("RealisticBrakes main reservoir hand", RedHand);

        private static Material BlueHandMaterial() => blueMaterial != null ? blueMaterial : blueMaterial = HandMaterial("RealisticBrakes equalizing reservoir hand", BlueHand);

        private static Material HandMaterial(string name, Color color)
        {
            var material = new Material(Shader.Find("Standard")) { name = name, color = color };
            material.SetFloat("_Glossiness", 0.2f);
            return material;
        }

        // The cab's own label font, or TextMeshPro's default.
        internal static TextMeshPro Font(Component cab) =>
            cab.GetComponentsInChildren<TextMeshPro>(true).FirstOrDefault(t => t.font != null);

        // A legend below the pivot, upright on the dial: the hand at mid-sweep points to the dial's top.
        private static void Legend(IndicatorGauge gauge, TextMeshPro font, string text)
        {
            var needle = gauge.needle;
            var frame = needle.parent != null ? needle.parent : gauge.transform;
            var rotation = needle.localRotation;
            needle.localRotation = Quaternion.AngleAxis((gauge.minAngle + gauge.maxAngle) / 2f, gauge.rotationAxis);
            var tip = NeedleTip(needle);
            needle.localRotation = rotation;

            var toward = frame.TransformDirection(Toward(gauge)).normalized;
            var up = Vector3.ProjectOnPlane(tip - needle.position, toward);
            var length = up.magnitude;
            if (length < 1e-4f)
                return;
            up /= length;

            var go = new GameObject(LegendName);
            go.transform.SetParent(gauge.transform, false);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = font.font;
            tmp.fontSharedMaterial = font.fontSharedMaterial;
            tmp.richText = true;
            tmp.text = text;
            tmp.color = Ink;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = false;
            tmp.fontSize = 10f;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.ForceMeshUpdate();
            var size = tmp.textBounds.size;
            if (!(size.x > 0f) || !(size.y > 0f))
            {
                Object.Destroy(go);
                return;
            }
            var parentScale = gauge.transform.lossyScale.x;
            go.transform.localScale = Vector3.one * Mathf.Min(length * LegendWidth / (size.x * parentScale), length * LegendHeight / (size.y * parentScale));
            // TextMeshPro reads from its +z side's back: its +z faces away from the viewer.
            go.transform.SetPositionAndRotation(needle.position - up * (length * LegendDrop) - toward * LegendDepth, Quaternion.LookRotation(-toward, up));
        }

        // The hand's tip: the far end of its mesh along its longest axis (Realistic Firing's StokerGauges).
        internal static Vector3 NeedleTip(Transform pivot)
        {
            var filter = pivot.GetComponentInChildren<MeshFilter>(true);
            if (filter == null || filter.sharedMesh == null)
                return pivot.position + pivot.up;
            var b = filter.sharedMesh.bounds;
            var axis = b.extents.x >= b.extents.y && b.extents.x >= b.extents.z ? 0 : b.extents.y >= b.extents.z ? 1 : 2;
            var tip = b.center;
            tip[axis] += (b.center[axis] < 0f ? -1f : 1f) * b.extents[axis];
            return filter.transform.TransformPoint(tip);
        }
    }

    /// <summary>
    /// Moves the equalizing reservoir's hand, absolute bar as the stock gauges read. While the model is off, an added hand
    /// is hidden, and a dial's own hand goes back to its reader.
    /// </summary>
    internal sealed class EqualizingHand : MonoBehaviour
    {
        public IndicatorGauge Gauge { get; set; }

        public BrakeSystem System { get; set; }

        /// <summary>The reader of a hand the dial came with, stood down while the model drives it; null for a hand this mod added.</summary>
        public Behaviour Reader { get; set; }

        /// <summary>Whether this mod added the hand, a copy of the main reservoir's.</summary>
        public bool Added => Reader == null;

        private Renderer[] renderers;
        private bool shown = true;

        private void Update()
        {
            if (Gauge == null)
                return;
            var valve = Main.Active && BrakeSets.TryGet(System, out var air) ? air.Brake.Locomotive?.DriverValve : null;
            if (Reader != null)
            {
                if (Reader.enabled != (valve == null))
                    Reader.enabled = valve == null;
            }
            else
                Show(valve != null);
            if (valve != null)
                Gauge.Value = (float)Air.ToAbsoluteBar(valve.EqualizingReservoir.Pressure);
        }

        private void OnDestroy()
        {
            if (Reader != null)
                Reader.enabled = true;
        }

        private void Show(bool show)
        {
            if (show == shown)
                return;
            shown = show;
            renderers = renderers ?? Gauge.needle.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
                renderer.enabled = show;
        }
    }
}
