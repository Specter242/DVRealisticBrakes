using System.Collections.Generic;
using DV.Indicators;
using RealisticBrakes.Core;
using TMPro;
using UnityEngine;

namespace RealisticBrakes.Controls
{
    internal sealed partial class VacuumDial
    {
        // ---- The face ----

        private void Build()
        {
            var needle = gauge.needle;
            var frame = needle.parent != null ? needle.parent : gauge.transform;
            var rotation = needle.localRotation;
            needle.localRotation = Quaternion.identity;
            var tip = frame.InverseTransformPoint(BrakeGauges.NeedleTip(needle));
            needle.localRotation = rotation;

            var axis = gauge.rotationAxis.normalized;
            var pivot = needle.localPosition;
            var rest = Vector3.ProjectOnPlane(tip - pivot, axis);
            var length = rest.magnitude;
            if (length < 1e-5f)
                return;
            rest /= length;
            var worldLength = frame.TransformVector(rest * length).magnitude;
            // The axis points at the viewer, on every stock gauge and nearly every custom one; a gauge whose axis opposes the
            // others on its dial has it the other way (the 8F's reservoir hand, job 2702).
            var toward = frame.TransformDirection(axis).normalized;
            var facing = axis;
            if (OpposesItsDial(needle.position, toward, worldLength))
            {
                toward = -toward;
                facing = -axis;
            }
            // Heights are metres in the cab; the frame's own scale turns them into its units. Some hands sink behind their
            // pivot, even into the dial (the DM1U's, job 2658), so the pivot is the floor.
            var perMetre = 1f / frame.TransformVector(axis).magnitude;
            var back = HandBack(needle, toward);
            var floor = Mathf.Max(back, 0f);
            Vector3 Direction(float angle) => Quaternion.AngleAxis(angle, gauge.rotationAxis) * rest;
            var low = Mathf.Min(gauge.minAngle, gauge.maxAngle);
            var high = Mathf.Max(gauge.minAngle, gauge.maxAngle);
            var sector = high - low < RoundFrom;
            var flipped = frame.lossyScale.x * frame.lossyScale.y * frame.lossyScale.z < 0f;

            MeasureDial(frame, facing, toward, Direction, worldLength, length, floor, perMetre, low, high, sector, flipped);

            // Two gauges on one dial (the 8F's pipe and reservoir) share one face: the second's scale lies on the first's.
            var duplicate = false;
            var sharesFace = false;
            var discWorld = frame.TransformPoint(handRest + FitCenter);
            if (FitFound && cab != null)
                foreach (var other in cab.GetComponentsInChildren<VacuumDial>(true))
                {
                    if (other == this || other.face == null || !other.FitFound || !other.ownsDisc
                        || Vector3.Distance(other.discCenter, discWorld) > worldLength * 0.3f)
                        continue;
                    sharesFace = true;
                    duplicate = other.reading == reading;
                    FitLift = Mathf.Max(FitLift, Vector3.Dot(other.discCenter - needle.position, toward) - floor - FaceHeight + 0.00005f);
                    break;
                }
            ownsDisc = !sharesFace;
            Vector3 Above(float metres) => handRest + facing * ((floor + FitLift + metres) * perMetre);
            handLifted = handRest + facing * ((floor - back + FitLift + HandClearance) * perMetre);
            discCenter = frame.TransformPoint(Above(FaceHeight) + FitCenter);
            if (duplicate)
            {
                face = new GameObject("RealisticBrakes vacuum face");
                face.transform.SetParent(frame, false);
                return;
            }

            var full = reading == Reading.TrainPipe ? VacuumFull : SteamFull;
            var numbers = reading == Reading.TrainPipe ? VacuumNumbers : SteamNumbers;
            var marks = reading == Reading.TrainPipe ? VacuumMarks : SteamMarks;
            float AngleOf(float value) => Mathf.Lerp(gauge.minAngle, gauge.maxAngle, value / full);
            var mesh = new DialMesh(facing, flipped);

            // A dial much wider than its hand reaches: the scale stays at the hand's tip, its marks and numbers stand outside it, larger.
            var room = float.PositiveInfinity;
            var fromCenter = -FitCenter / length;
            for (var i = 0; i <= 12; i++)
            {
                var along = Direction(Mathf.Lerp(low, high, i / 12f));
                var b = Vector3.Dot(fromCenter, along);
                room = Mathf.Min(room, -b + Mathf.Sqrt(Mathf.Max(0f, b * b - fromCenter.sqrMagnitude + FitRadius * FitRadius)));
            }
            var unit = Mathf.Max(1f, room / FaceRadius);
            var outside = !sector && unit >= OutsideFrom;
            var faceCenter = Above(FaceHeight);
            var inkCenter = Above(InkHeight);
            var textCenter = Above(TextHeight);
            if (sharesFace)
            {
                // The first gauge's face is under this scale.
            }
            else if (sector && !FitFound)
                mesh.Sector(0, faceCenter, Direction, low - SectorMargin, high + SectorMargin, length * FitRadius);
            else
                mesh.Disc(0, faceCenter + FitCenter, rest, length * FitRadius);
            var width = length * 0.012f * Mathf.Min(unit, 1.4f);
            // A dial only printed on its panel has no rim of its own.
            if (printedRim)
                mesh.Arc(1, inkCenter + FitCenter, Direction, 0f, 360f, length * (FitRadius - 0.03f), length * 0.05f);
            mesh.Arc(1, inkCenter, Direction, gauge.minAngle, gauge.maxAngle, length * ArcRadius, width);
            for (var value = 0f; value <= full + 0.001f; value += marks)
            {
                var major = Mathf.Abs(Mathf.Repeat(value + numbers / 2f, numbers) - numbers / 2f) < 0.001f;
                var direction = Direction(AngleOf(value));
                var mark = (major ? MajorMark : MinorMark) * (outside ? unit : 1f);
                var from = outside ? ArcRadius : ArcRadius - mark;
                var to = outside ? ArcRadius + mark : ArcRadius;
                mesh.Bar(1, inkCenter + direction * (length * from), inkCenter + direction * (length * to), width * (major ? 1.6f : 0.8f));
            }
            if (reading == Reading.TrainPipe)
            {
                var direction = Direction(AngleOf((float)Vacuum.Working));
                var from = outside ? ArcRadius : ArcRadius - MajorMark;
                var to = outside ? ArcRadius + MajorMark * unit + 0.04f : ArcRadius + 0.08f;
                mesh.Bar(2, inkCenter + direction * (length * from), inkCenter + direction * (length * to), width * 2.4f);
            }

            face = new GameObject("RealisticBrakes vacuum face");
            face.transform.SetParent(frame, false);
            face.AddComponent<MeshFilter>().sharedMesh = mesh.Build();
            var dialColor = DialColor;
            var ink = dialColor.grayscale < 0.4f ? PaleInk : DarkInk;
            face.AddComponent<MeshRenderer>().sharedMaterials = new[] { Plain(dialColor), Plain(ink), Plain(MarkColor) };

            var font = cab != null ? BrakeGauges.Font(cab) : null;
            if (font == null)
                return;
            var middle = Direction((gauge.minAngle + gauge.maxAngle) / 2f);
            var up = frame.TransformDirection(middle).normalized;
            // A round face's text stands square to its scale. A part dial can sit any way round (the 8F's lie on their sides):
            // its text stands upright in the cab.
            if (sector)
            {
                var upright = Vector3.ProjectOnPlane(Vector3.up, toward);
                if (upright.sqrMagnitude > 0.01f)
                    up = upright.normalized;
            }
            void Text(string text, Vector3 local, float wide, float high2)
            {
                var go = new GameObject("text");
                go.transform.SetParent(face.transform, false);
                var tmp = go.AddComponent<TextMeshPro>();
                tmp.font = font.font;
                tmp.fontSharedMaterial = font.fontSharedMaterial;
                tmp.richText = true;
                tmp.text = text;
                tmp.color = ink;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.enableAutoSizing = false;
                tmp.fontSize = 10f;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.ForceMeshUpdate();
                var size = tmp.textBounds.size;
                if (!(size.x > 0f) || !(size.y > 0f))
                {
                    Destroy(go);
                    return;
                }
                var parentScale = face.transform.lossyScale.x;
                go.transform.localScale = Vector3.one * Mathf.Min(worldLength * wide / (size.x * parentScale), worldLength * high2 / (size.y * parentScale));
                // TextMeshPro reads from its +z side's back: its +z faces away from the viewer.
                go.transform.SetPositionAndRotation(frame.TransformPoint(local), Quaternion.LookRotation(-toward, up));
            }

            var numberRadius = outside ? ArcRadius + (MajorMark + 0.17f) * unit : NumberRadius;
            var numberScale = outside ? Mathf.Min(unit, 1.9f) : 1f;
            for (var value = 0f; value <= full + 0.001f; value += numbers)
                Text(value.ToString("0"), textCenter + Direction(AngleOf(value)) * (length * numberRadius), 0.32f * numberScale, 0.19f * numberScale);
            Vector3 titleAt, legendAt;
            var titleWide = 0.62f;
            var titleHigh = 0.14f;
            var legendWide = 0.36f;
            var legendHigh = 0.12f;
            if (sector)
            {
                var edge = reading == Reading.TrainPipe ? gauge.minAngle : gauge.maxAngle;
                var inward = Mathf.Sign((reading == Reading.TrainPipe ? gauge.maxAngle : gauge.minAngle) - edge);
                var broad = high - low >= 120f;
                var spot = Direction(edge + inward * (broad ? 30f : -26f)) * (length * (broad ? 0.6f : 0.72f));
                var below = Vector3.ProjectOnPlane(frame.InverseTransformDirection(-up), facing).normalized;
                titleAt = spot;
                legendAt = spot + below * (length * 0.15f);
                titleWide = 0.5f;
            }
            else if (outside)
            {
                titleAt = -middle * (length * 1.16f);
                legendAt = -middle * (length * 1.38f);
                titleWide = 0.8f;
                titleHigh = 0.18f;
                legendWide = 0.42f;
                legendHigh = 0.15f;
            }
            else
            {
                titleAt = -middle * (length * TitleDrop);
                legendAt = -middle * (length * LegendDrop);
            }
            Text(reading == Reading.TrainPipe ? "in vacuum" : "steam brake", textCenter + titleAt, titleWide, titleHigh);
            Text(reading == Reading.TrainPipe ? $"TP/<color={BrakeGauges.CylinderRed}>CH</color>" : "psi", textCenter + legendAt, legendWide, legendHigh);
        }

        // This gauge's face color: its stock dial's where that is known, else the steam engines' parchment.
        private Color DialColor
        {
            get
            {
                if (livery == null)
                    return Parchment;
                if (dialColors.TryGetValue(livery, out var known))
                    return known;
                foreach (var (start, color) in customDialColors)
                    if (livery.StartsWith(start, System.StringComparison.Ordinal))
                        return color;
                return Parchment;
            }
        }

        // ---- Fitting the face to the dial that is there ----

        // Looks at the dial with a small camera, the hands and glass out of the way. A flat tile tried over it finds the height
        // that clears the printing, however the dial is built. Then one wide tile at that height shows everything standing
        // above it: the dial's rim, whose inner edge gives the circle the face must fill, wherever the hand's pivot is on it.
        // A custom cab's dial can stand in front of the pivot and be far wider than the hand or off center from it (the
        // survey of 2026-11-18), and its meshes can't be read.
        private void MeasureDial(Transform frame, Vector3 facing, Vector3 toward, System.Func<float, Vector3> direction, float worldLength, float length,
            float floor, float perMetre, float low, float high, bool sector, bool flipped)
        {
            FitLift = 0f;
            FitRadius = FaceRadius;
            FitFound = false;
            FitCenter = Vector3.zero;
            printedRim = false;
            var shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null || cab == null)
                return;
            var hidden = new List<Renderer>();
            void Hide(Transform root)
            {
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                    if (renderer.enabled)
                    {
                        renderer.enabled = false;
                        hidden.Add(renderer);
                    }
            }
            foreach (var other in cab.GetComponentsInChildren<IndicatorGauge>(true))
            {
                if (other.needle != null)
                    Hide(other.needle);
                var legend = other.transform.Find(BrakeGauges.LegendName);
                if (legend != null)
                    Hide(legend);
            }
            // The cover glass and glare over the dial.
            var near = new Bounds(gauge.needle.position, Vector3.one * (worldLength * ProbeReach * 2f));
            foreach (var renderer in cab.GetComponentsInChildren<Renderer>(true))
                if (renderer.enabled && renderer.sharedMaterial != null && renderer.bounds.Intersects(near)
                    && (renderer.sharedMaterial.renderQueue >= (int)UnityEngine.Rendering.RenderQueue.AlphaTest + 50 || IsGlass(renderer)))
                {
                    renderer.enabled = false;
                    hidden.Add(renderer);
                }
            var faces = new List<GameObject>();
            foreach (var dial in cab.GetComponentsInChildren<VacuumDial>(true))
                if (dial.face != null && dial.face.activeSelf)
                {
                    dial.face.SetActive(false);
                    faces.Add(dial.face);
                }

            var meshes = new List<Mesh>();
            var material = new Material(shader) { color = Color.magenta, hideFlags = HideFlags.HideAndDontSave };
            material.SetInt("_SrcBlend", 1);
            material.SetInt("_DstBlend", 0);
            material.SetInt("_Cull", 0);
            material.SetInt("_ZWrite", 0);
            var holder = new GameObject("RealisticBrakes dial probe");
            var tile = new GameObject("RealisticBrakes dial tile") { layer = gauge.gameObject.layer };
            tile.transform.SetParent(frame, false);
            var filter = tile.AddComponent<MeshFilter>();
            var tileRenderer = tile.AddComponent<MeshRenderer>();
            tileRenderer.sharedMaterial = material;
            tileRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tileRenderer.receiveShadows = false;
            var target = RenderTexture.GetTemporary(ProbeSize, ProbeSize, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(ProbeSize, ProbeSize, TextureFormat.RGBA32, false);
            var active = RenderTexture.active;
            try
            {
                holder.transform.SetPositionAndRotation(gauge.needle.position + toward * (floor + ProbeAbove),
                    Quaternion.LookRotation(-toward, frame.TransformDirection(direction((low + high) / 2f))));
                var camera = holder.AddComponent<Camera>();
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = worldLength * ProbeReach;
                camera.aspect = 1f;
                camera.nearClipPlane = 0.00005f;
                camera.farClipPlane = floor + ProbeAbove + 0.03f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                camera.useOcclusionCulling = false;
                camera.targetTexture = target;

                Color32[] Look()
                {
                    camera.Render();
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, ProbeSize, ProbeSize), 0, 0, false);
                    return image.GetPixels32();
                }
                bool Tile(Color32 pixel) => pixel.r > 110 && pixel.b > 110 && pixel.g < 90;
                int Count()
                {
                    var count = 0;
                    foreach (var pixel in Look())
                        if (Tile(pixel))
                            count++;
                    return count;
                }
                void Place(Mesh ring, float height)
                {
                    filter.sharedMesh = ring;
                    tile.transform.localPosition = handRest + facing * ((floor + height) * perMetre);
                }
                float Seen(Mesh ring, float height)
                {
                    Place(ring, height);
                    material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
                    var whole = Count();
                    material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual);
                    return whole > 0 ? Count() / (float)whole : 0f;
                }
                Mesh Ring(float inner, float outer, float from, float to)
                {
                    var built = new DialMesh(facing, flipped);
                    built.Ring(0, Vector3.zero, direction, from, to, length * inner, length * outer);
                    var made = built.Build();
                    meshes.Add(made);
                    return made;
                }

                var dialRing = sector ? Ring(0.35f, 0.98f, low, high) : Ring(0.35f, 0.98f, 0f, 360f);
                var best = -1f;
                foreach (var lift in ProbeLifts)
                {
                    var seen = Seen(dialRing, lift + FaceHeight);
                    if (seen > best)
                    {
                        best = seen;
                        FitLift = lift;
                    }
                    if (seen >= 0.97f)
                        break;
                }

                // Everything standing over the face's height, seen from over any rim.
                holder.transform.position = gauge.needle.position + toward * (floor + BezelAbove);
                camera.farClipPlane = floor + BezelAbove + 0.03f;
                Place(Ring(0.01f, ProbeReach * 1.5f, 0f, 360f), FitLift + FaceHeight + 0.0001f);
                material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual);
                var pixels = Look();
                var perHand = ProbeSize / (2f * ProbeReach);
                if (FindRim(pixels, Tile, perHand, out var center, out var radius))
                {
                    var world = holder.transform.right * (center.x / perHand * worldLength) + holder.transform.up * (center.y / perHand * worldLength);
                    FitCenter = Vector3.ProjectOnPlane(frame.InverseTransformVector(world), facing);
                    FitRadius = radius / perHand + 0.05f;
                    FitFound = true;
                }
                else if (reading == Reading.TrainPipe && livery != null)
                    foreach (var (start, printed) in printedDials)
                        if (livery.StartsWith(start, System.StringComparison.Ordinal))
                        {
                            FitRadius = printed;
                            FitFound = true;
                            printedRim = true;
                        }
            }
            finally
            {
                RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(target);
                Destroy(image);
                DestroyImmediate(tile);
                DestroyImmediate(holder);
                foreach (var made in meshes)
                    Destroy(made);
                DestroyImmediate(material);
                foreach (var renderer in hidden)
                    if (renderer != null)
                        renderer.enabled = true;
                foreach (var other in faces)
                    if (other != null)
                        other.SetActive(true);
            }
        }

        // The rim around the pivot: from the middle of the image, rays out to whatever first stands over the tile, and the
        // circle most of those points lie on. A straight-sided panel fits none. Center and radius in pixels, from the pivot.
        private static bool FindRim(Color32[] pixels, System.Func<Color32, bool> tile, float perHand, out Vector2 center, out float radius)
        {
            center = Vector2.zero;
            radius = 0f;
            const int rays = 120;
            var points = new List<Vector2>(rays);
            var middle = (ProbeSize - 1) / 2f;
            for (var i = 0; i < rays; i++)
            {
                var angle = i * Mathf.PI * 2f / rays;
                var way = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                for (var r = perHand * 0.22f; r < ProbeSize; r += 0.5f)
                {
                    var x = Mathf.RoundToInt(middle + way.x * r);
                    var y = Mathf.RoundToInt(middle + way.y * r);
                    if (x < 0 || y < 0 || x >= ProbeSize || y >= ProbeSize)
                        break;
                    if (!tile(pixels[y * ProbeSize + x]))
                    {
                        points.Add(way * r);
                        break;
                    }
                }
            }
            if (points.Count < rays / 2)
                return false;
            var random = new System.Random(1);
            var most = 0;
            for (var attempt = 0; attempt < 400; attempt++)
            {
                var a = points[random.Next(points.Count)];
                var b = points[random.Next(points.Count)];
                var c = points[random.Next(points.Count)];
                var d = 2f * (a.x * (b.y - c.y) + b.x * (c.y - a.y) + c.x * (a.y - b.y));
                if (Mathf.Abs(d) < 1f)
                    continue;
                var ux = (a.sqrMagnitude * (b.y - c.y) + b.sqrMagnitude * (c.y - a.y) + c.sqrMagnitude * (a.y - b.y)) / d;
                var uy = (a.sqrMagnitude * (c.x - b.x) + b.sqrMagnitude * (a.x - c.x) + c.sqrMagnitude * (b.x - a.x)) / d;
                var trial = new Vector2(ux, uy);
                var size = (a - trial).magnitude;
                if (size < perHand * 0.9f || size > ProbeSize * 0.5f || trial.magnitude >= size)
                    continue;
                var count = 0;
                foreach (var point in points)
                    if (Mathf.Abs((point - trial).magnitude - size) < RimTolerance)
                        count++;
                if (count > most)
                {
                    most = count;
                    center = trial;
                    radius = size;
                }
            }
            if (most < rays * 0.55f)
                return false;
            // The face runs to the farthest of the rim's points, so a many-sided rim leaves no gap at its corners.
            var farthest = 0f;
            foreach (var point in points)
                if (Mathf.Abs((point - center).magnitude - radius) < RimTolerance)
                    farthest = Mathf.Max(farthest, (point - center).magnitude);
            radius = farthest;
            return true;
        }

        private static bool IsGlass(Renderer renderer) =>
            renderer.name.IndexOf("glass", System.StringComparison.OrdinalIgnoreCase) >= 0
            || renderer.sharedMaterial.name.IndexOf("glass", System.StringComparison.OrdinalIgnoreCase) >= 0
            || (renderer.transform.parent != null && renderer.transform.parent.name.IndexOf("glass", System.StringComparison.OrdinalIgnoreCase) >= 0);

        private static Material Plain(Color color)
        {
            if (materials.TryGetValue(color, out var material) && material != null)
                return material;
            material = new Material(Shader.Find("Standard")) { name = "RealisticBrakes dial", color = color };
            material.SetFloat("_Glossiness", 0.15f);
            return materials[color] = material;
        }

        /// <summary>A flat mesh facing along an axis, in three submeshes: the face, the ink, and the red mark.</summary>
        private sealed class DialMesh
        {
            private readonly Vector3 axis;
            private readonly bool flipped;
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<int>[] triangles = { new List<int>(), new List<int>(), new List<int>() };

            public DialMesh(Vector3 axis, bool flipped)
            {
                this.axis = axis;
                this.flipped = flipped;
            }

            // Wound so its front faces along the axis: Unity's cross product of a front face's edges points at the viewer.
            private void Triangle(int sub, Vector3 a, Vector3 b, Vector3 c)
            {
                var along = Vector3.Dot(Vector3.Cross(b - a, c - a), axis) > 0f;
                if (along == flipped)
                {
                    var swap = b;
                    b = c;
                    c = swap;
                }
                var first = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
                triangles[sub].Add(first);
                triangles[sub].Add(first + 1);
                triangles[sub].Add(first + 2);
            }

            private void Quad(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                Triangle(sub, a, b, c);
                Triangle(sub, a, c, d);
            }

            public void Disc(int sub, Vector3 center, Vector3 from, float radius)
            {
                const int sides = 72;
                var last = center + from * radius;
                for (var i = 1; i <= sides; i++)
                {
                    var next = center + Quaternion.AngleAxis(360f * i / sides, axis) * from * radius;
                    Triangle(sub, center, last, next);
                    last = next;
                }
            }

            public void Sector(int sub, Vector3 center, System.Func<float, Vector3> direction, float from, float to, float radius)
            {
                const int steps = 60;
                var last = center + direction(from) * radius;
                for (var i = 1; i <= steps; i++)
                {
                    var next = center + direction(Mathf.Lerp(from, to, (float)i / steps)) * radius;
                    Triangle(sub, center, last, next);
                    last = next;
                }
            }

            public void Ring(int sub, Vector3 center, System.Func<float, Vector3> direction, float from, float to, float inner, float outer)
            {
                const int steps = 60;
                var last = direction(from);
                for (var i = 1; i <= steps; i++)
                {
                    var next = direction(Mathf.Lerp(from, to, (float)i / steps));
                    Quad(sub, center + last * inner, center + last * outer, center + next * outer, center + next * inner);
                    last = next;
                }
            }

            public void Bar(int sub, Vector3 from, Vector3 to, float width)
            {
                var side = Vector3.Cross(axis, (to - from).normalized) * (width / 2f);
                Quad(sub, from - side, from + side, to + side, to - side);
            }

            public void Arc(int sub, Vector3 center, System.Func<float, Vector3> direction, float from, float to, float radius, float width)
            {
                const int steps = 60;
                var last = direction(from);
                for (var i = 1; i <= steps; i++)
                {
                    var next = direction(Mathf.Lerp(from, to, (float)i / steps));
                    Quad(sub, center + last * (radius - width / 2f), center + last * (radius + width / 2f), center + next * (radius + width / 2f), center + next * (radius - width / 2f));
                    last = next;
                }
            }

            public Mesh Build()
            {
                var mesh = new Mesh { name = "RealisticBrakes dial" };
                mesh.SetVertices(vertices);
                mesh.subMeshCount = triangles.Length;
                for (var i = 0; i < triangles.Length; i++)
                    mesh.SetTriangles(triangles[i], i);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
