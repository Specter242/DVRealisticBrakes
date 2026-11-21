using DV.Indicators;
using UnityEngine;

namespace RealisticBrakes.Controls
{
    internal sealed partial class VacuumDial
    {
        private void Show(bool show)
        {
            shown = show;
            if (show)
            {
                stockMin = gauge.minValue;
                stockMax = gauge.maxValue;
                if (reading == Reading.TrainPipe)
                {
                    gauge.minValue = 1f;
                    gauge.maxValue = Bar(VacuumFull);
                    if (chamberHand != null)
                    {
                        chamberHand.minValue = gauge.minValue;
                        chamberHand.maxValue = gauge.maxValue;
                    }
                }
                else
                {
                    reader.enabled = false;
                    gauge.minValue = 0f;
                    gauge.maxValue = SteamFull;
                }
                if (face == null)
                    Build();
                gauge.needle.localPosition = handLifted;
                if (chamberHand != null)
                    chamberHand.needle.localPosition = handLifted;
                PaintHand(true);
            }
            else
            {
                PaintHand(false);
                gauge.minValue = stockMin;
                gauge.maxValue = stockMax;
                gauge.needle.localPosition = handRest;
                reader.enabled = true;
                ShowChamber(false);
            }
            if (face != null)
                face.SetActive(show);
            var legend = gauge.transform.Find(BrakeGauges.LegendName);
            if (legend != null)
                legend.gameObject.SetActive(!show);
            if (partner != null)
                foreach (var renderer in partner.needle.GetComponentsInChildren<Renderer>(true))
                    renderer.enabled = !show;
        }

        private void ShowChamber(bool show)
        {
            if (show == chamberShown || chamberHand == null)
                return;
            chamberShown = show;
            foreach (var renderer in chamberHand.needle.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = show;
        }

        // A custom cab's hand is painted to show on the face: a white one vanishes on parchment (the DE4-740A's, FEF-3's).
        private void PaintHand(bool on)
        {
            if (livery != null && dialColors.ContainsKey(livery))
                return;
            if (handRenderers == null)
            {
                handRenderers = gauge.needle.GetComponentsInChildren<Renderer>(true);
                handMaterials = new Material[handRenderers.Length][];
                for (var i = 0; i < handRenderers.Length; i++)
                    handMaterials[i] = handRenderers[i].sharedMaterials;
            }
            var paint = reading == Reading.SteamBrake ? BrakeGauges.RedHandMaterial() : Plain(DialColor.grayscale < 0.4f ? PaleInk : DarkInk);
            for (var i = 0; i < handRenderers.Length; i++)
            {
                if (handRenderers[i] == null)
                    continue;
                if (!on)
                {
                    handRenderers[i].sharedMaterials = handMaterials[i];
                    continue;
                }
                var painted = new Material[handMaterials[i].Length];
                for (var m = 0; m < painted.Length; m++)
                    painted[m] = paint;
                handRenderers[i].sharedMaterials = painted;
            }
        }

        // Whether more of the other hands on this dial turn about the opposite axis than about this one.
        private bool OpposesItsDial(Vector3 pivot, Vector3 direction, float reach)
        {
            if (cab == null)
                return false;
            var with = 0;
            var against = 0;
            foreach (var other in cab.GetComponentsInChildren<IndicatorGauge>(true))
            {
                // Not this mod's own hands, which copy another's axis.
                if (other == gauge || other == chamberHand || other.name == "I_VacuumChamber" || (other.GetComponent<EqualizingHand>() is EqualizingHand hand && hand.Added)
                    || other.needle == null || Vector3.Distance(other.needle.position, pivot) > reach * SameDialReach)
                    continue;
                var frame = other.needle.parent != null ? other.needle.parent : other.transform;
                var along = Vector3.Dot(frame.TransformDirection(other.rotationAxis).normalized, direction);
                if (along > 0.95f)
                    with++;
                else if (along < -0.95f)
                    against++;
            }
            return against > with;
        }

        // How far the hand's back lies toward the viewer from its pivot, m; negative behind it.
        private static float HandBack(Transform needle, Vector3 toward)
        {
            var back = float.PositiveInfinity;
            foreach (var filter in needle.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                    continue;
                var b = filter.sharedMesh.bounds;
                for (var i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    back = Mathf.Min(back, Vector3.Dot(filter.transform.TransformPoint(corner) - needle.position, toward));
                }
            }
            return float.IsInfinity(back) ? 0f : back;
        }
    }
}
