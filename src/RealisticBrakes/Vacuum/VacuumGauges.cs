using System.Collections.Generic;
using DV.Indicators;
using DV.Simulation.Brake;
using RealisticBrakes.Core;
using RealisticBrakes.Sim;
using UnityEngine;

namespace RealisticBrakes.Controls
{
 internal static class VacuumGauges
    {
        /// <summary>Fits the pipe and main reservoir gauges under <paramref name="cab"/>, after <see cref="BrakeGauges.Fit"/>.</summary>
        public static void Fit(Component cab, TrainCar car)
        {
            var system = car != null ? car.brakeSystem : null;
            if (system == null)
                return;
            var cylinders = cab.GetComponentsInChildren<IndicatorBrakeCylinderReader>(true);
            foreach (var reader in cab.GetComponentsInChildren<IndicatorBrakePipeReader>(true))
            {
                var gauge = reader.GetComponent<IndicatorGauge>();
                if (!Usable(gauge) || gauge.GetComponent<VacuumDial>() != null)
                    continue;
                IndicatorGauge partner = null;
                foreach (var other in cylinders)
                {
                    var hand = other.GetComponent<IndicatorGauge>();
                    if (Usable(hand) && Vector3.Distance(hand.needle.position, gauge.needle.position) < SameDial(gauge))
                        partner = hand;
                }
                var chamber = CopyHand(gauge, cab);
                var dial = gauge.gameObject.AddComponent<VacuumDial>();
                dial.Init(gauge, reader, system, VacuumDial.Reading.TrainPipe, partner, chamber, cab, car.carLivery != null ? car.carLivery.id : null);
            }
            foreach (var reader in cab.GetComponentsInChildren<IndicatorMainResReader>(true))
            {
                var gauge = reader.GetComponent<IndicatorGauge>();
                if (Usable(gauge) && gauge.GetComponent<VacuumDial>() == null)
                    gauge.gameObject.AddComponent<VacuumDial>().Init(gauge, reader, system, VacuumDial.Reading.SteamBrake, null, null, cab, car.carLivery != null ? car.carLivery.id : null);
            }
        }

        // A gauge with a hand to see. Some engines carry readers on empty objects for other mods to read (the DE4-740B's
        // DummyReaders, job 2702), which have no dial to cover.
        private static bool Usable(IndicatorGauge gauge)
        {
            if (gauge == null || gauge.needle == null)
                return false;
            foreach (var filter in gauge.needle.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null)
                    return true;
            return false;
        }

        // How near another hand's pivot stands to share this gauge's dial: a custom cab's can be some millimetres apart (the 8F's).
        private static float SameDial(IndicatorGauge gauge) =>
            Mathf.Max(BrakeGauges.SameDial, 0.12f * Vector3.Distance(BrakeGauges.NeedleTip(gauge.needle), gauge.needle.position));

        // The chamber's hand: the pipe hand's copy, red, and thinner, so the pipe's lies over it where they cross.
        private static IndicatorGauge CopyHand(IndicatorGauge gauge, Component cab)
        {
            var copy = Object.Instantiate(gauge.gameObject, gauge.transform.parent, false);
            copy.name = "I_VacuumChamber";
            foreach (var reader in copy.GetComponents<AIndicatorBrakePressureReader>())
                Object.DestroyImmediate(reader);
            // A dial's own equalizing reservoir hand carries its driver (the FPD-4's, job 2956), which the copy mustn't.
            foreach (var driver in copy.GetComponents<EqualizingHand>())
                Object.DestroyImmediate(driver);
            var legend = copy.transform.Find(BrakeGauges.LegendName);
            if (legend != null)
                Object.DestroyImmediate(legend.gameObject);
            var hand = copy.GetComponent<IndicatorGauge>();
            var axis = BrakeGauges.Toward(hand);
            hand.needle.localScale = Vector3.Scale(hand.needle.localScale, new Vector3(
                Mathf.Abs(axis.x) > 0.5f ? 0.7f : 1f, Mathf.Abs(axis.y) > 0.5f ? 0.7f : 1f, Mathf.Abs(axis.z) > 0.5f ? 0.7f : 1f));
            BrakeGauges.Paint(hand, BrakeGauges.RedHandMaterial());
            foreach (var renderer in hand.needle.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = false;
            return hand;
        }
    }

 internal sealed partial class VacuumDial : MonoBehaviour
    {
        internal enum Reading
        {
            TrainPipe,
            SteamBrake,
        }

        private const double PsiPerPascal = 1 / 6894.757;

        // The scales: their full value, the step between numbers, and the step between marks.
        private const float VacuumFull = 30f, VacuumNumbers = 5f, VacuumMarks = 1f;
        private const float SteamFull = 200f, SteamNumbers = 50f, SteamMarks = 10f;

        // In shares of the hand's length: the face's edge, which covers all that is printed (photographs of the eight stock
        // cabs, job 2654), the scale's arc and marks, the numbers, the title, and the legend.
        private const float FaceRadius = 1.2f;
        private const float ArcRadius = 1.02f;
        private const float MajorMark = 0.14f;
        private const float MinorMark = 0.07f;
        private const float NumberRadius = 0.76f;
        // Low on the dial, under the reach of the hands' tails.
        private const float TitleDrop = 0.6f;
        private const float LegendDrop = 0.76f;

        // A gauge sweeping less than this, degrees, gets a face of its own sweep, not a round one: the 8F's two half dials
        // share one round dial, and the Jk3's are narrow sectors (job 2702). Its face runs this far past each end of the
        // scale.
        private const float RoundFrom = 200f;
        private const float SectorMargin = 6f;

        // From this many times the usual face's width, the scale's marks and numbers stand outside the hand's reach.
        private const float OutsideFrom = 1.3f;

        // The look at a dial: the image's size, how far over the pivot the camera stands, m, how far out it sees, in hand
        // lengths, and the heights tried over the usual one, m.
        private const int ProbeSize = 256;
        private const float ProbeAbove = 0.004f;
        private const float BezelAbove = 0.03f;
        private const float ProbeReach = 3.3f;
        private const float RimTolerance = 3f;
        private static readonly float[] ProbeLifts = { 0f, 0.0003f, 0.0007f, 0.0012f, 0.002f, 0.003f };

        // Hands whose pivots stand within this many hand lengths are on one dial, as the 8F's three are.
        private const float SameDialReach = 1.5f;

        // Over the hand's pivot, m, or over its back where that stands higher: the printed dials all lie behind the pivot
        // (the probes' photographs, job 2654). The face, its marks and text, and how far the hands then clear them.
        private const float FaceHeight = 0.0001f;
        private const float InkHeight = 0.00022f;
        private const float TextHeight = 0.0003f;
        private const float HandClearance = 0.0005f;

        private static readonly Color Parchment = new Color(0.84f, 0.79f, 0.65f);
        private static readonly Color DarkInk = new Color(0.09f, 0.08f, 0.07f);
        private static readonly Color PaleInk = new Color(0.86f, 0.86f, 0.8f);
        private static readonly Color MarkColor = new Color(0.72f, 0.08f, 0.05f);
        private static readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();

        // Each stock cab's dial, so the face passes for it; any other engine gets the steam engines' parchment.
        private static readonly Dictionary<string, Color> dialColors = new Dictionary<string, Color>
        {
            ["LocoS282A"] = Parchment,
            ["LocoS060"] = new Color(0.72f, 0.65f, 0.5f),
            ["LocoDM3"] = new Color(0.56f, 0.51f, 0.47f),
            ["LocoDE6"] = new Color(0.9f, 0.89f, 0.86f),
            ["LocoDE2"] = new Color(0.9f, 0.88f, 0.84f),
            ["LocoMicroshunter"] = new Color(0.9f, 0.76f, 0.64f),
            ["LocoDH4"] = new Color(0.2f, 0.2f, 0.2f),
            ["LocoDM1U"] = new Color(0.18f, 0.18f, 0.17f),
            ["CabooseRed"] = Parchment,
        };

        // Custom engines whose dials are known, by the start of the livery's id, so the new face passes for the old one.
        private static readonly (string Livery, Color Color)[] customDialColors =
        {
            ("FEF-3", new Color(0.07f, 0.07f, 0.08f)),
            ("S484-84", new Color(0.9f, 0.89f, 0.86f)),
            ("em_lms_8f", new Color(0.87f, 0.84f, 0.75f)),
            ("Jk3", new Color(0.72f, 0.74f, 0.72f)),
        };

        // Pipe dials only printed on a flat panel, with no rim to find: their printed scale's outer edge, in hand lengths
        // from the pivot (measured from the survey of 2026-11-18).
        private static readonly (string Livery, float Radius)[] printedDials =
        {
            ("FEF-3", 2.15f),
            ("S484-841", 2.03f),
        };

        private IndicatorGauge gauge;
        private AIndicatorBrakePressureReader reader;
        private BrakeSystem system;
        private Reading reading;
        private IndicatorGauge partner;
        private IndicatorGauge chamberHand;
        private Component cab;
        private string livery;
        private GameObject face;
        private Vector3 handRest;
        private Vector3 handLifted;
        private bool shown;
        private bool chamberShown;
        private float stockMin, stockMax;
        private Renderer[] handRenderers;
        private Material[][] handMaterials;

        /// <summary>How far past the usual height the printed dial made the face stand, m.</summary>
        internal float FitLift { get; private set; }

        /// <summary>The face's radius, in hand lengths.</summary>
        internal float FitRadius { get; private set; } = FaceRadius;

        /// <summary>Whether the dial's own edge was found, by its rim or from <see cref="printedDials"/>.</summary>
        internal bool FitFound { get; private set; }

        // The face's center from the hand's pivot, in the frame's units: a dial needn't be centered on its hand.
        private Vector3 FitCenter;
        private bool printedRim;
        private bool ownsDisc;
        private Vector3 discCenter;

        public void Init(IndicatorGauge gauge, AIndicatorBrakePressureReader reader, BrakeSystem system, Reading reading, IndicatorGauge partner, IndicatorGauge chamberHand, Component cab, string livery)
        {
            this.livery = livery;
            handRest = handLifted = gauge.needle.localPosition;
            this.gauge = gauge;
            this.reader = reader;
            this.system = system;
            this.reading = reading;
            this.partner = partner;
            this.chamberHand = chamberHand;
            this.cab = cab;
        }

        private void Update()
        {
            if (gauge == null || system == null)
                return;
            if (!shown && Main.Settings?.Equipment != Equipment.Vacuum)
                return;
            CarAir air = null;
            var wanted = Main.Active && Main.Settings.Equipment == Equipment.Vacuum && BrakeSets.TryGet(system, out air) && air.IsVacuum
                         && (reading == Reading.TrainPipe || air.Brake.VacuumEngine?.Spec.SteamBrake == true);
            if (wanted != shown)
                Show(wanted);
            if (!wanted)
                return;
            if (reading == Reading.SteamBrake)
                gauge.Value = (float)(air.Brake.VacuumEngine.SteamBrakePressure * PsiPerPascal);
            else if (chamberHand != null)
            {
                var chamber = NearestChamber(air);
                ShowChamber(chamber != null);
                if (chamber != null)
                    chamberHand.Value = (float)Air.ToAbsoluteBar(Vacuum.AirEquivalent(chamber.Pressure));
            }
        }

        // The chamber of the nearest braked car along the train; a light engine has none to show.
        private static Chamber NearestChamber(CarAir air)
        {
            // A caboose's gauge reads its own.
            if (air.Brake.VacuumCylinder != null)
                return air.Brake.VacuumCylinder.Chamber;
            var cars = air.Line?.Cars;
            if (cars == null)
                return null;
            var own = -1;
            for (var i = 0; i < cars.Count && own < 0; i++)
                if (cars[i] == air.Brake)
                    own = i;
            if (own < 0)
                return null;
            for (var step = 1; step < cars.Count; step++)
            {
                var ahead = own - step >= 0 ? cars[own - step].VacuumCylinder : null;
                var behind = own + step < cars.Count ? cars[own + step].VacuumCylinder : null;
                if (behind != null)
                    return behind.Chamber;
                if (ahead != null)
                    return ahead.Chamber;
            }
            return null;
        }

        private static float Bar(float inches) => (float)Air.ToAbsoluteBar(Vacuum.AirEquivalent(Vacuum.FromInches(inches)));
    }
}
