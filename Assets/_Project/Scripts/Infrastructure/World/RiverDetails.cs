using System.Collections.Generic;
using Project.Infrastructure.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// Nehir kanyonu cilası: akıntı yönünde köpük şeritleri (hızlı kesitte yoğun), nehir içi taşlar + köpük halkaları, sığ kıyıda çakıl
    /// şeridi ve köprü ayaklarında akıntı izi. Tamamen deterministik; kurallar <see cref="RiverFlowRules"/>.
    /// Su sesi için <see cref="MaxSpeed01"/> / <see cref="SpeedAt"/> ENTEGRASYON girdisidir (AmbienceBeds henüz bağlamadı).
    /// </summary>
    public static class RiverDetails
    {
        public const string RootName = "NehirDetay";
        private const float Step = 6f;

        private sealed class Line
        {
            public Vector2[] P;
            public Vector2[] T;
            public float[] Speed;
            public float Length;
        }

        private static readonly List<Line> Lines = new();

        /// <summary>Haritadaki en hızlı nehir kesiti (0..1); nehir yoksa 0.</summary>
        public static float MaxSpeed01 { get; private set; }

        /// <summary>Dünya noktasının en yakın nehir kesitindeki akıntı hızı ve nehre uzaklığı (Ambience su sesi okuyabilir).</summary>
        public static float SpeedAt(Vector2 p, out float distance)
        {
            var best = float.MaxValue;
            var speed = 0f;
            foreach (var l in Lines)
                for (var i = 0; i < l.P.Length; i++)
                {
                    var d = (l.P[i] - p).sqrMagnitude;
                    if (d < best) { best = d; speed = l.Speed[i]; }
                }

            distance = best == float.MaxValue ? float.MaxValue : Mathf.Sqrt(best);
            return speed;
        }

        public static void Build(Transform parent, MapLayout layout, TerrainModel model, int seed)
        {
            Lines.Clear();
            MaxSpeed01 = 0f;
            if (layout == null || model == null || layout.Rivers.Count == 0)
                return;

            var level = layout.WaterLevel;
            var half = model.RiverWaterHalfWidth + 3f;
            var root = new GameObject(RootName) { layer = GameLayers.Water };
            root.transform.SetParent(parent, false);

            var foam = new Mesh_();
            var gravel = new Mesh_();
            var rockBuilder = new MeshBuilder(3);
            var rocks = 0;

            for (var r = 0; r < layout.Rivers.Count; r++)
            {
                var river = layout.Rivers[r];
                if (river?.Points == null || river.Points.Count < 2)
                    continue;
                var pts = WorldWater.ClipToSquare(river.Points, layout.HalfSize);
                var line = Resample(pts, model, river.Width);
                if (line == null)
                    continue;
                Lines.Add(line);
                var rs = seed * 131 + r * 17;

                for (var i = 0; i < line.P.Length; i++)
                {
                    MaxSpeed01 = Mathf.Max(MaxSpeed01, line.Speed[i]);
                    var right = new Vector2(line.T[i].y, -line.T[i].x);
                    var n = RiverFlowRules.StreakCount(line.Speed[i]);
                    var len = RiverFlowRules.StreakLength(line.Speed[i]);
                    for (var s = 0; s < n; s++)
                    {
                        var lat = (RiverFlowRules.Hash01(rs, i * 7 + s) - 0.5f) * 1.5f * half * 0.8f;
                        var p = line.P[i] + right * lat + line.T[i] * RiverFlowRules.Hash01(rs + 3, i * 7 + s) * Step;
                        foam.Quad(p, line.T[i], len, 0.35f + 0.3f * RiverFlowRules.Hash01(rs + 5, i * 7 + s), level + 0.05f,
                            0.55f * RiverFlowRules.FoamDensity(line.Speed[i]));
                    }

                    // Sığ kenar çakıl şeridi.
                    for (var side = -1; side <= 1; side += 2)
                        AddGravel(gravel, model, level, line.P[i], line.T[i], right * side, half, i == 0);
                }

                // Nehir içi taşlar.
                var count = RiverFlowRules.RockCount(line.Length);
                for (var k = 0; k < count; k++)
                {
                    var idx = Mathf.Clamp(Mathf.RoundToInt(RiverFlowRules.RockT(k, count, rs) * (line.P.Length - 1)), 0, line.P.Length - 1);
                    var right = new Vector2(line.T[idx].y, -line.T[idx].x);
                    var pos = line.P[idx] + right * (RiverFlowRules.RockLateral(k, rs) * model.RiverWaterHalfWidth);
                    var size = RiverFlowRules.RockSize(k, rs);
                    var scale = new Vector3(size * 1.2f, size * 0.9f, size);
                    var rot = Quaternion.Euler(0f, RiverFlowRules.Hash01(rs, k + 90) * 360f, 0f);
                    var y = Mathf.Max(model.SampleHeight(pos.x, pos.y), level - 0.5f) + 0.05f * scale.y;
                    var variant = (int)(RiverFlowRules.Hash01(rs, k + 40) * RockFactory.VariantCount) % RockFactory.VariantCount;
                    rockBuilder.AppendMapped(RockFactory.GetSplitVariant(variant), Matrix4x4.TRS(new Vector3(pos.x, y, pos.y), rot, scale),
                        new[] { RiverFlowRules.Hash01(rs, k + 60) < 0.4f ? 1 : 0, 2 });
                    rocks++;
                    foam.Ring(pos, RiverFlowRules.RockRingRadius(size, line.Speed[idx]), level + 0.06f, 0.75f);
                }
            }

            // Köprü ayağı akıntı izi.
            for (var b = 0; b < layout.Bridges.Count; b++)
            {
                var br = layout.Bridges[b];
                if (br == null)
                    continue;
                var speed = SpeedAt(br.Center, out var dist);
                if (dist > half + br.Length * 0.5f)
                    continue;
                var dir = br.Direction.sqrMagnitude < 1e-4f ? Vector2.up : br.Direction.normalized;
                var side = new Vector2(dir.y, -dir.x);
                var flow = FlowAt(br.Center);
                for (var s = -1; s <= 1; s += 2)
                {
                    var p = br.Center + side * (s * br.Width * 0.4f);
                    if (model.SampleHeight(p.x, p.y) > level)
                        continue;
                    foam.Quad(p + flow * RiverFlowRules.PierWakeLength(speed) * 0.5f, flow, RiverFlowRules.PierWakeLength(speed), 0.9f, level + 0.06f, 0.7f);
                    foam.Ring(p, 1.4f, level + 0.06f, 0.6f);
                }
            }

            Emit(root.transform, "AkıntıKöpüğü", foam, WaterSurfaceTextures.Foam, 3012);
            Emit(root.transform, "ÇakılŞerit", gravel, null, 3005);

            if (rocks > 0)
            {
                var mesh = rockBuilder.ToMesh("HK_RiverRocks");
                var go = new GameObject("NehirTaşları") { layer = GameLayers.Default };
                go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = new[]
                    { MaterialLibrary.Get(MaterialId.Rock), MaterialLibrary.Get(MaterialId.RockDark), VegetationMaterials.CreateMoss() };
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
        }

        private static Vector2 FlowAt(Vector2 p)
        {
            var best = float.MaxValue;
            var t = Vector2.up;
            foreach (var l in Lines)
                for (var i = 0; i < l.P.Length; i++)
                {
                    var d = (l.P[i] - p).sqrMagnitude;
                    if (d < best) { best = d; t = l.T[i]; }
                }

            return t;
        }

        private static Line Resample(List<Vector2> pts, TerrainModel model, float width)
        {
            if (pts.Count < 2)
                return null;
            var pos = new List<Vector2>();
            var carry = 0f;
            pos.Add(pts[0]);
            var total = 0f;
            for (var i = 1; i < pts.Count; i++)
            {
                var a = pts[i - 1];
                var b = pts[i];
                var seg = Vector2.Distance(a, b);
                total += seg;
                var d = Step - carry;
                while (d <= seg)
                {
                    pos.Add(Vector2.Lerp(a, b, d / seg));
                    d += Step;
                }

                carry = seg - (d - Step);
            }

            if (pos.Count < 3)
                return null;
            var n = pos.Count;
            var line = new Line { P = pos.ToArray(), T = new Vector2[n], Speed = new float[n], Length = total };
            var baseW = Mathf.Max(1f, width);
            for (var i = 0; i < n; i++)
            {
                var t = pos[Mathf.Min(n - 1, i + 1)] - pos[Mathf.Max(0, i - 1)];
                line.T[i] = t.sqrMagnitude < 1e-6f ? Vector2.up : t.normalized;
                var a = pos[Mathf.Max(0, i - 2)];
                var b = pos[Mathf.Min(n - 1, i + 2)];
                var dist = Vector2.Distance(a, b);
                var drop = dist > 0.1f ? (model.SampleHeight(a.x, a.y) - model.SampleHeight(b.x, b.y)) / dist : 0f;
                // Yerel daralma: kıyı çizgisine uzaklık (gerçek su genişliği) örneklenir.
                line.Speed[i] = RiverFlowRules.Speed01(drop, MeasureWidth(model, pos[i], line.T[i], baseW), baseW);
            }

            return line;
        }

        private static float MeasureWidth(TerrainModel model, Vector2 p, Vector2 t, float fallback)
        {
            var right = new Vector2(t.y, -t.x);
            var w = 0f;
            for (var side = -1; side <= 1; side += 2)
            {
                var d = 0f;
                while (d < fallback * 2f && model.IsWater(p.x + right.x * side * d, p.y + right.y * side * d))
                    d += 0.75f;
                w += d;
            }

            return w < 0.5f ? fallback : w;
        }

        private static void AddGravel(Mesh_ m, TerrainModel model, float level, Vector2 c, Vector2 t, Vector2 dir, float half, bool first)
        {
            var d = 0f;
            while (d < half + 6f && model.SampleHeight(c.x + dir.x * d, c.y + dir.y * d) < level - 0.05f)
                d += 0.5f;
            if (d >= half + 6f || d < 0.5f)
            {
                m.Break();
                return;
            }

            var d2 = d + 2f;
            var slope = Mathf.Clamp01((model.SampleHeight(c.x + dir.x * d2, c.y + dir.y * d2) - level) / 2f);
            var w = RiverFlowRules.GravelWidth(slope);
            m.GravelRow(c, t, dir, d - 0.9f, d + w, model, level, i0: first);
        }

        private static void Emit(Transform parent, string name, Mesh_ data, Texture tex, int queue)
        {
            var mesh = data.ToMesh(name);
            if (mesh == null)
                return;
            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
                return;
            var go = new GameObject(name) { layer = GameLayers.Water };
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = new Material(shader) { name = "HK_" + name, mainTexture = tex, color = Color.white, renderQueue = queue };
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        /// <summary>Basit renkli/UV'li mesh toplayıcı (köpük dörtgenleri, halkalar, çakıl şeridi).</summary>
        private sealed class Mesh_
        {
            private readonly List<Vector3> _v = new();
            private readonly List<Color> _c = new();
            private readonly List<Vector2> _uv = new();
            private readonly List<int> _t = new();
            private int _prev = -1; // önceki çakıl satırının ilk köşesi (-1 = kopuk)
            private int _prevSide;

            public void Break() => _prev = -1;

            public void Quad(Vector2 center, Vector2 dir, float length, float width, float y, float alpha)
            {
                var right = new Vector2(dir.y, -dir.x) * (width * 0.5f);
                var f = dir * (length * 0.5f);
                var b = _v.Count;
                Add(center - f - right, y, 0f, 0f, 0f); Add(center - f + right, y, 1f, 0f, 0f);
                Add(center + f + right, y, 1f, length * 0.25f, 0f); Add(center + f - right, y, 0f, length * 0.25f, 0f);
                // Uçlarda sön: orta iki köşe yerine uç alfa 0 (ilk iki ve son iki).
                SetAlpha(b, 0f); SetAlpha(b + 1, 0f); SetAlpha(b + 2, 0f); SetAlpha(b + 3, 0f);
                // Orta ekseni parlat: iki dörtgene bölmek yerine ortaya köşe ekle.
                var mid = _v.Count;
                Add(center - right * 0.0f, y, 0.5f, length * 0.125f, alpha);
                _t.AddRange(new[] { b, mid, b + 1, b + 1, mid, b + 2, b + 2, mid, b + 3, b + 3, mid, b });
            }

            public void Ring(Vector2 center, float radius, float y, float alpha)
            {
                const int seg = 14;
                var b = _v.Count;
                Add(new Vector2(center.x, center.y), y, 0.5f, 0.5f, 0f);
                for (var i = 0; i < seg; i++)
                {
                    var a = i / (float)seg * Mathf.PI * 2f;
                    var inner = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    Add(center + inner * (radius * 0.55f), y, 0.5f + inner.x * 0.5f, 0.5f + inner.y * 0.5f, alpha);
                    Add(center + inner * radius, y, 0.5f + inner.x * 0.5f, 0.5f + inner.y * 0.5f, 0f);
                }

                for (var i = 0; i < seg; i++)
                {
                    var n = (i + 1) % seg;
                    var i0 = b + 1 + i * 2; var o0 = i0 + 1; var i1 = b + 1 + n * 2; var o1 = i1 + 1;
                    _t.AddRange(new[] { b, i1, i0, i0, i1, o1, i0, o1, o0 });
                }
            }

            public void GravelRow(Vector2 c, Vector2 t, Vector2 dir, float d0, float d1, TerrainModel model, float level, bool i0)
            {
                var a = c + dir * d0;
                var b2 = c + dir * d1;
                var ya = Mathf.Max(model.SampleHeight(a.x, a.y), level) + 0.04f;
                var yb = Mathf.Max(model.SampleHeight(b2.x, b2.y), level) + 0.04f;
                var shade = 0.42f + 0.12f * RiverFlowRules.Hash01(Mathf.RoundToInt(c.x * 3f), Mathf.RoundToInt(c.y * 3f));
                var col = new Color(shade, shade * 0.95f, shade * 0.82f, 0.9f);
                var s = _v.Count;
                _v.Add(new Vector3(a.x, ya, a.y)); _c.Add(new Color(col.r, col.g, col.b, 0f)); _uv.Add(Vector2.zero);
                _v.Add(new Vector3(b2.x, yb, b2.y)); _c.Add(new Color(col.r, col.g, col.b, 0f)); _uv.Add(Vector2.zero);
                // Orta (kıyı çizgisi) tam opak: iki köşe arası ortada ek köşe.
                var m = Vector2.Lerp(a, b2, 0.55f);
                var ym = Mathf.Max(model.SampleHeight(m.x, m.y), level) + 0.04f;
                _v.Add(new Vector3(m.x, ym, m.y)); _c.Add(col); _uv.Add(Vector2.zero);
                // Üçlü: (a, m, b) şerit; bir önceki satırla bağla.
                if (_prev >= 0 && _prevSide == (int)Mathf.Sign(Vector2.Dot(dir, new Vector2(t.y, -t.x))))
                {
                    var p = _prev;
                    _t.AddRange(new[] { p, p + 2, s + 2, p, s + 2, s, p + 2, p + 1, s + 1, p + 2, s + 1, s + 2 });
                }

                _prev = s;
                _prevSide = (int)Mathf.Sign(Vector2.Dot(dir, new Vector2(t.y, -t.x)));
            }

            private void Add(Vector2 p, float y, float u, float v, float alpha)
            {
                _v.Add(new Vector3(p.x, y, p.y));
                _c.Add(new Color(1f, 1f, 1f, alpha));
                _uv.Add(new Vector2(u, v));
            }

            private void SetAlpha(int i, float a) { var c = _c[i]; c.a = a; _c[i] = c; }

            public Mesh ToMesh(string name)
            {
                if (_v.Count == 0 || _t.Count == 0)
                    return null;
                var mesh = new Mesh { name = "HK_" + name };
                if (_v.Count > 65000)
                    mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(_v);
                mesh.SetColors(_c);
                mesh.SetUVs(0, _uv);
                mesh.SetTriangles(_t, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
