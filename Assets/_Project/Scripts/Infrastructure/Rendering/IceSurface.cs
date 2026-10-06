using System.Collections.Generic;
using Project.Core.Domain;
using Project.Infrastructure.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Infrastructure.Rendering
{
    /// <summary>
    /// Donmuş göl yüzeyi (Ayaz Geçidi): prosedürel buz malzemesi (mavimsi beyaz albedo, çatlak çizgileri, yüksek parlaklık,
    /// düşük metalik, hafif yüzey altı tonu) ve buz üstünde dağınık kar yamaları. Tohuma bağlı, deterministik; mesh'ler NaN korumalı.
    /// Su gibi çarpıştırıcısı yoktur (katman <see cref="GameLayers.Water"/>); davranış suyla aynı kalır.
    /// </summary>
    public static class IceSurface
    {
        private const int TexSize = 256;
        private static Texture2D _albedo;
        private static Material _ice, _snow;

        /// <summary>Haritada göller donuk mu (MapLayout.FrozenLakes).</summary>
        public static bool IsFrozen(MapLayout layout) => layout != null && layout.FrozenLakes;

        /// <summary>Buz malzemesi (URP Lit; bulunamazsa null).</summary>
        public static Material CreateMaterial()
        {
            if (_ice != null)
                return _ice;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                return null;
            var m = new Material(shader) { name = "HK_Ice" };
            var tex = AlbedoTexture();
            Set(m, "_BaseMap", tex, 1f / 14f);
            Set(m, "_MainTex", tex, 1f / 14f);
            SetColor(m, "_BaseColor", Color.white);
            SetColor(m, "_Color", Color.white);
            SetFloat(m, "_Smoothness", 0.9f);
            SetFloat(m, "_Glossiness", 0.9f);
            SetFloat(m, "_Metallic", 0.03f);
            if (m.HasProperty("_BumpMap"))
            {
                m.SetTexture("_BumpMap", WaterSurfaceTextures.NormalB);
                m.SetTextureScale("_BumpMap", new Vector2(1f / 22f, 1f / 22f));
                SetFloat(m, "_BumpScale", 0.25f);
                m.EnableKeyword("_NORMALMAP");
            }

            // Yüzey altı: buzun içinden gelen soluk mavi parıltı.
            if (m.HasProperty("_EmissionColor"))
            {
                m.SetColor("_EmissionColor", new Color(0.03f, 0.07f, 0.1f));
                m.EnableKeyword("_EMISSION");
            }

            _ice = m;
            return m;
        }

        /// <summary>Buz üstünde dağınık kar yamaları (tek birleşik mesh); göl yoksa null.</summary>
        public static GameObject BuildSnowPatches(Transform parent, MapLayout layout)
        {
            if (layout == null || layout.Lakes.Count == 0)
                return null;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                return null;
            if (_snow == null)
            {
                _snow = new Material(shader) { name = "HK_IceSnow" };
                SetColor(_snow, "_BaseColor", new Color(0.93f, 0.95f, 0.98f));
                SetColor(_snow, "_Color", new Color(0.93f, 0.95f, 0.98f));
                SetFloat(_snow, "_Smoothness", 0.12f);
                SetFloat(_snow, "_Glossiness", 0.12f);
                SetFloat(_snow, "_Metallic", 0f);
            }

            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var tris = new List<int>();
            for (var l = 0; l < layout.Lakes.Count; l++)
            {
                var lake = layout.Lakes[l];
                if (lake == null || lake.Radius <= 1f)
                    continue;
                var rng = new System.Random(layout.Seed * 7919 + l * 131 + 17);
                var count = Mathf.Clamp(Mathf.RoundToInt(lake.Radius * 0.35f), 6, 40);
                for (var i = 0; i < count; i++)
                {
                    var ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                    var dist = Mathf.Sqrt((float)rng.NextDouble()) * lake.Radius * 0.85f;
                    var size = 1.5f + (float)rng.NextDouble() * 5.5f;
                    var cx = lake.Center.x + Mathf.Cos(ang) * dist;
                    var cz = lake.Center.y + Mathf.Sin(ang) * dist;
                    AddBlob(verts, norms, tris, rng, new Vector3(cx, layout.WaterLevel + 0.04f, cz), size);
                }
            }

            if (verts.Count == 0)
                return null;
            var mesh = new Mesh { name = "HK_IceSnowPatches" };
            if (verts.Count > 65000)
                mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            var go = new GameObject("BuzKar");
            go.layer = GameLayers.Water;
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = _snow;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = true;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go;
        }

        private static void AddBlob(List<Vector3> verts, List<Vector3> norms, List<int> tris, System.Random rng, Vector3 c, float size)
        {
            const int seg = 10;
            var baseIdx = verts.Count;
            verts.Add(c);
            norms.Add(Vector3.up);
            var stretch = 0.6f + (float)rng.NextDouble() * 0.8f;
            var rot = (float)rng.NextDouble() * Mathf.PI;
            for (var i = 0; i < seg; i++)
            {
                var a = i / (float)seg * Mathf.PI * 2f;
                var rad = size * (0.65f + (float)rng.NextDouble() * 0.5f);
                var x = Mathf.Cos(a) * rad * stretch;
                var z = Mathf.Sin(a) * rad;
                var rx = x * Mathf.Cos(rot) - z * Mathf.Sin(rot);
                var rz = x * Mathf.Sin(rot) + z * Mathf.Cos(rot);
                if (float.IsNaN(rx) || float.IsNaN(rz))
                {
                    rx = 0f;
                    rz = 0f;
                }

                verts.Add(c + new Vector3(rx, 0f, rz));
                norms.Add(Vector3.up);
            }

            for (var i = 0; i < seg; i++)
            {
                tris.Add(baseIdx);
                tris.Add(baseIdx + 1 + (i + 1) % seg);
                tris.Add(baseIdx + 1 + i);
            }
        }

        /// <summary>Döngüsel buz dokusu: mavimsi beyaz taban, gürültü, Worley kenarlarından çatlak çizgileri.</summary>
        private static Texture2D AlbedoTexture()
        {
            if (_albedo != null)
                return _albedo;
            const int n = TexSize;
            const int cells = 7;
            var px = new Color32[n * n];
            var pts = new Vector2[cells * cells];
            var rng = new System.Random(4242);
            for (var i = 0; i < pts.Length; i++)
                pts[i] = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble());

            var baseCol = new Color(0.74f, 0.86f, 0.94f);
            var deepCol = new Color(0.48f, 0.68f, 0.82f);
            for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
            {
                var u = x / (float)n * cells;
                var v = y / (float)n * cells;
                var cx = Mathf.FloorToInt(u);
                var cy = Mathf.FloorToInt(v);
                float f1 = 9f, f2 = 9f;
                for (var dy = -1; dy <= 1; dy++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    var gx = cx + dx;
                    var gy = cy + dy;
                    var p = pts[((gy % cells + cells) % cells) * cells + ((gx % cells + cells) % cells)];
                    var d = Vector2.Distance(new Vector2(u, v), new Vector2(gx + p.x, gy + p.y));
                    if (d < f1) { f2 = f1; f1 = d; }
                    else if (d < f2) f2 = d;
                }

                var edge = Mathf.Clamp01(1f - (f2 - f1) * 14f);
                var crack = edge * edge;
                var noise = SkyWaterRules.TileableFbm(x / (float)n, y / (float)n, 5, 4, 77);
                var col = Color.Lerp(deepCol, baseCol, Mathf.Clamp01(0.35f + noise * 0.9f));
                col = Color.Lerp(col, new Color(0.93f, 0.97f, 1f), crack * 0.65f);
                col = Color.Lerp(col, new Color(0.38f, 0.58f, 0.74f), Mathf.Clamp01(edge - 0.7f) * 0.7f);
                px[y * n + x] = col;
            }

            _albedo = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "HK_IceAlbedo", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            _albedo.SetPixels32(px);
            _albedo.Apply(true, true);
            return _albedo;
        }

        private static void Set(Material m, string prop, Texture t, float scale)
        {
            if (!m.HasProperty(prop)) return;
            m.SetTexture(prop, t);
            m.SetTextureScale(prop, new Vector2(scale, scale));
        }

        private static void SetColor(Material m, string prop, Color c) { if (m.HasProperty(prop)) m.SetColor(prop, c); }
        private static void SetFloat(Material m, string prop, float v) { if (m.HasProperty(prop)) m.SetFloat(prop, v); }
    }
}
