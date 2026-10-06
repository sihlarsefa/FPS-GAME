using System.Collections.Generic;
using Project.Core.Domain;
using Project.Infrastructure.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Infrastructure.Rendering
{
    /// <summary>
    /// Kıyı şeridi: göl/deniz/dere kıyı çizgisini arazi yüksekliğinden bulur; su tarafına doğru sığ (turkuaz) renk geçişi + köpük
    /// çizgisi veren, köşe renkli tek bir şerit mesh üretir (derinlik rengi taklidi).
    /// </summary>
    public static class WaterSurfaceShore
    {
        // Kıyıdan su yönüne mesafe (m) ve köşe rengi: derin tarafı saydam, kıyıya doğru turkuaz sonra köpük.
        private static readonly float[] Offsets = { 4.5f, 1.6f, 0.35f, -0.35f, -0.9f, -3.2f };
        private static readonly Color[] Colors =
        {
            new Color(0.25f, 0.62f, 0.62f, 0f),
            new Color(0.35f, 0.75f, 0.7f, 0.32f),
            new Color(0.95f, 1f, 1f, 0.85f),
            new Color(0.95f, 1f, 1f, 0f),
            // Kara tarafında ıslak koyulaşma bandı (köpük dokusu çarpılmaz; koyu, yarı saydam).
            new Color(0.07f, 0.06f, 0.045f, 0.34f),
            new Color(0.07f, 0.06f, 0.045f, 0f)
        };

        /// <summary>Şeritleri üretir; ortak köpük malzemesini döner (nabız için).</summary>
        public static Material Build(Transform parent, MapLayout layout, TerrainModel model) => Build(parent, layout, model, out _);

        /// <summary>İkinci (yarım periyot kaydırmalı) köpük katmanı malzemesini de döner.</summary>
        public static Material Build(Transform parent, MapLayout layout, TerrainModel model, out Material secondFoam)
        {
            secondFoam = null;
            if (layout == null || model == null)
                return null;

            var loops = new List<(List<Vector2> pts, List<Vector2> dirs, bool closed)>();
            var level = layout.WaterLevel;

            for (var l = 0; l < layout.Lakes.Count; l++)
            {
                var lake = layout.Lakes[l];
                if (lake == null || lake.Radius <= 1f)
                    continue;
                if (model.SampleHeight(lake.Center.x, lake.Center.y) >= level)
                    continue;
                var maxR = lake.Radius * TerrainModel.MaxLakeShoreScale + 5f;
                RadialShore(model, level, lake.Center, 0f, maxR, 72, false, loops);
            }

            if (layout.SeaPlane)
                RadialShore(model, level, Vector2.zero, layout.HalfSize * 1.3f, 0f, 120, true, loops);

            for (var r = 0; r < layout.Rivers.Count; r++)
            {
                var river = layout.Rivers[r];
                if (river?.Points == null || river.Points.Count < 2)
                    continue;
                var pts = WorldWater.ClipToSquare(river.Points, layout.HalfSize);
                RiverShore(model, level, pts, model.RiverWaterHalfWidth + 3f, loops);
            }

            if (loops.Count == 0)
                return null;

            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (var i = 0; i < loops.Count; i++)
                AppendStrip(loops[i].pts, loops[i].dirs, loops[i].closed, level + 0.04f, verts, cols, uvs, tris);

            if (verts.Count == 0)
                return null;

            var mesh = new Mesh { name = "HK_ShoreFoam" };
            if (verts.Count > 65000)
                mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
                return null;
            var mat = new Material(shader) { name = "HK_ShoreFoamMat", mainTexture = WaterSurfaceTextures.Foam, color = Color.white };
            mat.renderQueue = 3010;

            var go = new GameObject("KıyıKöpüğü");
            go.layer = GameLayers.Water;
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;

            // İkinci katman: aynı mesh, ayrı malzeme (kendi kaydırma fazı); alfa WaterSurface'te çapraz solar.
            var mat2 = new Material(mat) { name = "HK_ShoreFoamMat2", renderQueue = 3011 };
            var go2 = new GameObject("KıyıKöpüğü2");
            go2.layer = GameLayers.Water;
            go2.transform.SetParent(parent, false);
            go2.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            go2.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr2 = go2.AddComponent<MeshRenderer>();
            mr2.sharedMaterial = mat2;
            mr2.shadowCastingMode = ShadowCastingMode.Off;
            mr2.receiveShadows = false;
            mr2.lightProbeUsage = LightProbeUsage.Off;
            mr2.reflectionProbeUsage = ReflectionProbeUsage.Off;
            secondFoam = mat2;
            return mat;
        }

        /// <summary>
        /// Merkezden ışınlar: from→to yarıçap aralığında (from su tarafı) ilk su→kara geçişi. Göl: 0→maxR (su tarafı merkez).
        /// Deniz: dışarıdan içeri (su tarafı dış).
        /// </summary>
        private static void RadialShore(TerrainModel model, float level, Vector2 center, float from, float to, int rays, bool sea,
            List<(List<Vector2>, List<Vector2>, bool)> loops)
        {
            const float step = 2f;
            var count = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(to - from) / step));
            var samples = new float[count + 1];
            var pts = new List<Vector2>();
            var dirs = new List<Vector2>();
            var sign = to >= from ? 1f : -1f;
            for (var k = 0; k < rays; k++)
            {
                var a = k / (float)rays * Mathf.PI * 2f;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                for (var i = 0; i <= count; i++)
                {
                    var rad = from + sign * i * step;
                    var p = center + d * rad;
                    samples[i] = model.SampleHeight(p.x, p.y);
                }

                var dist = SkyWaterRules.FindCrossing(samples, step, level);
                if (dist < 0f)
                    continue;
                var shoreR = from + sign * dist;
                pts.Add(center + d * shoreR);
                dirs.Add(sea ? d : -d); // su tarafı: deniz dışarıda, göl merkezde
            }

            if (pts.Count >= 3)
                loops.Add((pts, dirs, true));
        }

        private static void RiverShore(TerrainModel model, float level, List<Vector2> line, float reach,
            List<(List<Vector2>, List<Vector2>, bool)> loops)
        {
            if (line == null || line.Count < 2)
                return;
            for (var side = -1; side <= 1; side += 2)
            {
                var pts = new List<Vector2>();
                var dirs = new List<Vector2>();
                for (var i = 0; i < line.Count; i++)
                {
                    var prev = line[Mathf.Max(0, i - 1)];
                    var next = line[Mathf.Min(line.Count - 1, i + 1)];
                    var tan = next - prev;
                    if (tan.sqrMagnitude < 1e-6f)
                    {
                        Flush(pts, dirs, loops);
                        continue;
                    }

                    tan.Normalize();
                    var normal = new Vector2(tan.y, -tan.x) * side; // kıyıya doğru
                    const float step = 1f;
                    var count = Mathf.CeilToInt(reach / step);
                    var samples = new float[count + 1];
                    for (var s = 0; s <= count; s++)
                    {
                        var p = line[i] + normal * (s * step);
                        samples[s] = model.SampleHeight(p.x, p.y);
                    }

                    var dist = SkyWaterRules.FindCrossing(samples, step, level);
                    if (dist < 0f || samples[0] >= level)
                    {
                        Flush(pts, dirs, loops);
                        continue;
                    }

                    pts.Add(line[i] + normal * dist);
                    dirs.Add(-normal);
                }

                Flush(pts, dirs, loops);
            }
        }

        private static void Flush(List<Vector2> pts, List<Vector2> dirs, List<(List<Vector2>, List<Vector2>, bool)> loops)
        {
            if (pts.Count >= 2)
                loops.Add((new List<Vector2>(pts), new List<Vector2>(dirs), false));
            pts.Clear();
            dirs.Clear();
        }

        private static void AppendStrip(List<Vector2> pts, List<Vector2> dirs, bool closed, float y, List<Vector3> verts, List<Color> cols,
            List<Vector2> uvs, List<int> tris)
        {
            var n = pts.Count;
            if (n < 2)
                return;
            var baseIndex = verts.Count;
            var along = 0f;
            for (var i = 0; i < n; i++)
            {
                if (i > 0)
                    along += Vector2.Distance(pts[i - 1], pts[i]);
                for (var j = 0; j < Offsets.Length; j++)
                {
                    var p = pts[i] + dirs[i] * Offsets[j];
                    verts.Add(new Vector3(p.x, y, p.y));
                    cols.Add(Colors[j]);
                    uvs.Add(new Vector2(j / (float)(Offsets.Length - 1), along / 6f));
                }
            }

            var segs = closed ? n : n - 1;
            var w = Offsets.Length;
            for (var i = 0; i < segs; i++)
            {
                var a = baseIndex + i * w;
                var b = baseIndex + ((i + 1) % n) * w;
                for (var j = 0; j < w - 1; j++)
                {
                    // Çift yönlü (Sprites/Default Cull Off) olduğundan sarım önemsiz.
                    tris.Add(a + j); tris.Add(b + j); tris.Add(b + j + 1);
                    tris.Add(a + j); tris.Add(b + j + 1); tris.Add(a + j + 1);
                }
            }
        }
    }
}
