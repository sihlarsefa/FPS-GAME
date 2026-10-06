using System.Collections.Generic;
using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// Askerî pafta stilinde mini harita dokusu: yükselti/eğim renkleri, kabartma gölgesi (KB ışık), 20 m eş yükselti
    /// eğrileri (100 m'de kalın), ağaç örtüsü, dere/göl (mavi + kıyı çizgisi), yapı ayak izleri (koyu), yollar (asfalt turuncu
    /// kenarlı, toprak yol kahverengi, patika kesikli), pafta ızgarası (harita 10 × 10 — arayüz paftalarıyla hizalı, ≈100 m)
    /// ve çerçeve. Doku (0,0) = güneybatı köşe, v kuzeye artar (<see cref="WorldMetadata.WorldToMapUV"/> ile aynı).
    /// </summary>
    public static class MinimapTextureGenerator
    {
        public const int ContourInterval = 20;
        public const int IndexContourInterval = 100;
        public const int GridDivisions = 10;

        private static readonly Color LowLand = new Color(0.66f, 0.7f, 0.52f);
        private static readonly Color MidLand = new Color(0.74f, 0.71f, 0.55f);
        private static readonly Color HighLand = new Color(0.71f, 0.67f, 0.59f);
        private static readonly Color RockColor = new Color(0.63f, 0.61f, 0.58f);
        private static readonly Color SnowColor = new Color(0.94f, 0.95f, 0.96f);
        private static readonly Color ForestColor = new Color(0.45f, 0.56f, 0.38f);
        private static readonly Color ContourColor = new Color(0.5f, 0.36f, 0.22f);
        private static readonly Color WaterShallow = new Color(0.45f, 0.63f, 0.74f);
        private static readonly Color WaterDeep = new Color(0.3f, 0.48f, 0.63f);
        private static readonly Color ShoreColor = new Color(0.22f, 0.38f, 0.54f);
        private static readonly Color BuildingColor = new Color(0.17f, 0.17f, 0.17f);
        private static readonly Color BuildingEdge = new Color(0.08f, 0.08f, 0.08f);
        private static readonly Color AsphaltCasing = new Color(0.15f, 0.12f, 0.1f);
        private static readonly Color AsphaltFill = new Color(0.9f, 0.58f, 0.26f);
        private static readonly Color DirtRoad = new Color(0.47f, 0.33f, 0.2f);
        private static readonly Color GridColor = new Color(0.08f, 0.08f, 0.1f);
        private static readonly Color BorderColor = new Color(0.1f, 0.1f, 0.1f);

        /// <summary>Arazi katmanlarının pafta renkleri (TerrainLayerKind sırası) — dokuların soluk/okunaklı karşılığı.</summary>
        private static readonly Color[] LayerMapColors =
        {
            new Color(0.64f, 0.7f, 0.5f),    // çim
            new Color(0.78f, 0.74f, 0.55f),  // kuru çim
            new Color(0.75f, 0.67f, 0.53f),  // toprak
            new Color(0.64f, 0.62f, 0.59f),  // kaya
            new Color(0.71f, 0.69f, 0.65f),  // çakıl
            new Color(0.56f, 0.51f, 0.43f),  // çamur
            new Color(0.94f, 0.95f, 0.96f),  // kar
            new Color(0.45f, 0.45f, 0.46f)   // asfalt
        };

        /// <summary>Ağaç örtüsü için hafif ağaç kaydı (dünya XZ, tür, genişlik ölçeği).</summary>
        public struct TreeMark
        {
            public float X;
            public float Z;
            public TreeKind Kind;
            public float WidthScale;
        }

        /// <summary>Sözleşme imzası. terrain/layout/structures null olabilir; size 64..4096'ya sınırlandırılır.</summary>
        public static Texture2D Generate(Terrain terrain, MapLayout layout, IReadOnlyList<Bounds> structures, int size)
        {
            size = Mathf.Clamp(size, 64, 4096);
            ResolveFrame(terrain, layout, out var minX, out var minZ, out var extent);
            var mpp = extent / size;
            var heights = SampleHeights(terrain, layout, size, minX, minZ, mpp);
            var trees = CollectTrees(terrain);
            var ground = SampleGroundColors(terrain, size, minX, minZ, mpp);
            var colors = RenderPixels(heights, size, minX, minZ, extent, layout, structures, trees, ground);

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, false)
            {
                name = "HK_Minimap",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 2
            };
            texture.SetPixels32(colors);
            texture.Apply(true, false);
            return texture;
        }

        /// <summary>
        /// Çekirdek çizim (Unity nesnesi gerekmez): piksel merkezlerindeki yükseklikler (size², satır = güney→kuzey), çerçeve,
        /// yerleşim, yapı sınırları ve ağaçlar → renkler.
        /// </summary>
        public static Color32[] RenderPixels(float[] heights, int size, float minX, float minZ, float extent, MapLayout layout,
            IReadOnlyList<Bounds> structures, IReadOnlyList<TreeMark> trees, Color[] groundColors = null)
        {
            var waterLevel = layout != null ? layout.WaterLevel : float.NegativeInfinity;
            var mpp = extent / size; // metre / piksel
            var pixels = new Color[size * size];

            if (groundColors != null && groundColors.Length == pixels.Length)
                System.Array.Copy(groundColors, pixels, pixels.Length);
            else
                PaintRelief(pixels, heights, size, mpp, waterLevel, layout != null ? layout.SnowLine : TerrainPainter.SnowLine);
            PaintTrees(pixels, trees, size, minX, minZ, mpp);
            ApplyHillshade(pixels, heights, size, mpp, waterLevel);
            PaintContours(pixels, heights, size, waterLevel);
            PaintWater(pixels, heights, size, waterLevel, layout, minX, minZ, mpp);
            PaintStructures(pixels, structures, size, minX, minZ, mpp);
            PaintRoads(pixels, layout, size, minX, minZ, mpp);
            PaintGrid(pixels, size);

            var colors = new Color32[pixels.Length];
            for (var i = 0; i < pixels.Length; i++)
            {
                var c = pixels[i];
                colors[i] = new Color32(ToByte(c.r), ToByte(c.g), ToByte(c.b), 255);
            }

            return colors;
        }

        /// <summary>
        /// Katman ağırlıklarından pafta zemin rengi: weights [z, x, katman] (alphamap düzeni, texel i ↔ i/(res-1)), çerçeve.
        /// Katman sayısı <see cref="TerrainTextureFactory.LayerCount"/> değilse null.
        /// </summary>
        public static Color[] GroundColorsFromWeights(float[,,] weights, float originX, float originZ, float terrainSizeX, float terrainSizeZ,
            int size, float minX, float minZ, float mpp)
        {
            if (weights == null)
                return null;
            var resZ = weights.GetLength(0);
            var resX = weights.GetLength(1);
            var layers = weights.GetLength(2);
            if (layers != TerrainTextureFactory.LayerCount || resX < 2 || resZ < 2)
                return null;

            var result = new Color[size * size];
            for (var py = 0; py < size; py++)
            {
                var wz = minZ + (py + 0.5f) * mpp;
                var iz = Mathf.Clamp(Mathf.RoundToInt((wz - originZ) / Mathf.Max(0.001f, terrainSizeZ) * (resZ - 1)), 0, resZ - 1);
                for (var px = 0; px < size; px++)
                {
                    var wx = minX + (px + 0.5f) * mpp;
                    var ix = Mathf.Clamp(Mathf.RoundToInt((wx - originX) / Mathf.Max(0.001f, terrainSizeX) * (resX - 1)), 0, resX - 1);
                    float r = 0f, g = 0f, b = 0f;
                    for (var l = 0; l < layers; l++)
                    {
                        var w = weights[iz, ix, l];
                        if (w <= 0f)
                            continue;
                        var c = LayerMapColors[l];
                        r += c.r * w;
                        g += c.g * w;
                        b += c.b * w;
                    }

                    result[py * size + px] = new Color(r, g, b, 1f);
                }
            }

            return result;
        }

        private static Color[] SampleGroundColors(Terrain terrain, int size, float minX, float minZ, float mpp)
        {
            if (terrain == null || terrain.terrainData == null)
                return null;
            var data = terrain.terrainData;
            if (data.alphamapLayers != TerrainTextureFactory.LayerCount)
                return null;

            var res = data.alphamapResolution;
            var weights = data.GetAlphamaps(0, 0, res, res);
            var origin = terrain.transform.position;
            return GroundColorsFromWeights(weights, origin.x, origin.z, data.size.x, data.size.z, size, minX, minZ, mpp);
        }

        /// <summary>Modelden piksel yükseklikleri (araziden bağımsız önizleme/test için).</summary>
        public static float[] SampleHeights(TerrainModel model, int size, float minX, float minZ, float extent)
        {
            var mpp = extent / size;
            var result = new float[size * size];
            for (var py = 0; py < size; py++)
            {
                var wz = minZ + (py + 0.5f) * mpp;
                for (var px = 0; px < size; px++)
                    result[py * size + px] = model.SampleHeight(minX + (px + 0.5f) * mpp, wz);
            }

            return result;
        }

        // ================================================================== Çerçeve ve yükseklik

        private static void ResolveFrame(Terrain terrain, MapLayout layout, out float minX, out float minZ, out float extent)
        {
            if (layout != null)
            {
                minX = -layout.HalfSize;
                minZ = -layout.HalfSize;
                extent = layout.HalfSize * 2f;
                return;
            }

            if (terrain != null && terrain.terrainData != null)
            {
                var size = terrain.terrainData.size;
                var p = terrain.transform.position;
                extent = Mathf.Max(size.x, size.z);
                minX = p.x + size.x * 0.5f - extent * 0.5f;
                minZ = p.z + size.z * 0.5f - extent * 0.5f;
                return;
            }

            minX = -512f;
            minZ = -512f;
            extent = 1024f;
        }

        /// <summary>Piksel merkezlerindeki dünya yükseklikleri (arazi → son model → 0).</summary>
        private static float[] SampleHeights(Terrain terrain, MapLayout layout, int size, float minX, float minZ, float mpp)
        {
            var result = new float[size * size];
            if (terrain != null && terrain.terrainData != null)
            {
                var data = terrain.terrainData;
                var res = data.heightmapResolution;
                var raw = data.GetHeights(0, 0, res, res);
                var tSize = data.size;
                var origin = terrain.transform.position;
                var sx = (res - 1) / Mathf.Max(0.001f, tSize.x);
                var sz = (res - 1) / Mathf.Max(0.001f, tSize.z);
                for (var py = 0; py < size; py++)
                {
                    var wz = minZ + (py + 0.5f) * mpp;
                    var fz = Mathf.Clamp((wz - origin.z) * sz, 0f, res - 1.001f);
                    var iz = (int)fz;
                    var tz = fz - iz;
                    for (var px = 0; px < size; px++)
                    {
                        var wx = minX + (px + 0.5f) * mpp;
                        var fx = Mathf.Clamp((wx - origin.x) * sx, 0f, res - 1.001f);
                        var ix = (int)fx;
                        var tx = fx - ix;
                        var a = Mathf.Lerp(raw[iz, ix], raw[iz, ix + 1], tx);
                        var b = Mathf.Lerp(raw[iz + 1, ix], raw[iz + 1, ix + 1], tx);
                        result[py * size + px] = Mathf.Lerp(a, b, tz) * tSize.y + origin.y;
                    }
                }

                return result;
            }

            var model = TerrainGenerator.LastModel;
            if (model != null && (layout == null || model.Layout == layout))
                return SampleHeights(model, size, minX, minZ, mpp * size);

            return result;
        }

        // ================================================================== Katmanlar

        private static void PaintRelief(Color[] pixels, float[] heights, int size, float mpp, float waterLevel, float snowLine)
        {
            for (var py = 0; py < size; py++)
            {
                for (var px = 0; px < size; px++)
                {
                    var i = py * size + px;
                    var h = heights[i];
                    Gradient(heights, size, px, py, mpp, out var gx, out var gz);
                    var slope = Mathf.Atan(Mathf.Sqrt(gx * gx + gz * gz)) * Mathf.Rad2Deg;

                    Color c;
                    if (h < 40f)
                        c = LowLand;
                    else if (h < 90f)
                        c = Color.Lerp(LowLand, MidLand, (h - 40f) / 50f);
                    else
                        c = Color.Lerp(MidLand, HighLand, Mathf.Clamp01((h - 90f) / 40f));

                    c = Color.Lerp(c, RockColor, TerrainNoise.SmoothStep(26f, 38f, slope));
                    c = Color.Lerp(c, SnowColor, TerrainNoise.SmoothStep(snowLine - 5f, snowLine + 4f, h)
                                                 * (1f - TerrainNoise.SmoothStep(42f, 55f, slope)));
                    pixels[i] = c;
                }
            }
        }

        private static List<TreeMark> CollectTrees(Terrain terrain)
        {
            var result = new List<TreeMark>();
            if (terrain == null || terrain.terrainData == null)
                return result;

            var data = terrain.terrainData;
            var instances = data.treeInstances;
            if (instances == null)
                return result;

            var tSize = data.size;
            var origin = terrain.transform.position;
            result.Capacity = instances.Length;
            for (var t = 0; t < instances.Length; t++)
            {
                var tree = instances[t];
                result.Add(new TreeMark
                {
                    X = origin.x + tree.position.x * tSize.x,
                    Z = origin.z + tree.position.z * tSize.z,
                    Kind = (TreeKind)Mathf.Clamp(tree.prototypeIndex, 0, TreeFactory.KindCount - 1),
                    WidthScale = tree.widthScale
                });
            }

            return result;
        }

        private static void PaintTrees(Color[] pixels, IReadOnlyList<TreeMark> trees, int size, float minX, float minZ, float mpp)
        {
            if (trees == null || trees.Count == 0)
                return;

            var cover = new float[size * size];
            for (var t = 0; t < trees.Count; t++)
            {
                var tree = trees[t];
                var kind = tree.Kind;
                var crown = CrownRadius(kind) * Mathf.Max(0.3f, tree.WidthScale);
                var strength = kind == TreeKind.Bush ? 0.45f : (kind == TreeKind.Dead ? 0.3f : 1f);
                var r = Mathf.Max(0.75f, crown / mpp);
                var cx = (tree.X - minX) / mpp;
                var cz = (tree.Z - minZ) / mpp;
                var x0 = Mathf.Max(0, Mathf.FloorToInt(cx - r));
                var x1 = Mathf.Min(size - 1, Mathf.CeilToInt(cx + r));
                var z0 = Mathf.Max(0, Mathf.FloorToInt(cz - r));
                var z1 = Mathf.Min(size - 1, Mathf.CeilToInt(cz + r));
                for (var py = z0; py <= z1; py++)
                {
                    for (var px = x0; px <= x1; px++)
                    {
                        var dx = px + 0.5f - cx;
                        var dz = py + 0.5f - cz;
                        var d = Mathf.Sqrt(dx * dx + dz * dz);
                        var v = Mathf.Clamp01(r + 0.5f - d) * strength;
                        var i = py * size + px;
                        if (v > cover[i])
                            cover[i] = v;
                    }
                }
            }

            for (var i = 0; i < pixels.Length; i++)
            {
                if (cover[i] > 0f)
                    pixels[i] = Color.Lerp(pixels[i], ForestColor, cover[i] * 0.85f);
            }
        }

        private static float CrownRadius(TreeKind kind)
        {
            switch (kind)
            {
                case TreeKind.PineA: return 2.8f;
                case TreeKind.PineB: return 3.4f;
                case TreeKind.Oak: return 3.6f;
                case TreeKind.Dead: return 1.6f;
                default: return 1.4f;
            }
        }

        private static void ApplyHillshade(Color[] pixels, float[] heights, int size, float mpp, float waterLevel)
        {
            var light = new Vector3(-1f, 1.5f, 1f).normalized; // kuzeybatıdan
            var flat = light.y;
            const float exaggeration = 1.8f;
            for (var py = 0; py < size; py++)
            {
                for (var px = 0; px < size; px++)
                {
                    var i = py * size + px;
                    if (heights[i] < waterLevel - 0.05f)
                        continue;
                    Gradient(heights, size, px, py, mpp, out var gx, out var gz);
                    var n = new Vector3(-gx * exaggeration, 1f, -gz * exaggeration).normalized;
                    var shade = Vector3.Dot(n, light) / flat;
                    var factor = Mathf.Clamp(Mathf.Lerp(1f, shade, 0.5f), 0.62f, 1.15f);
                    var c = pixels[i];
                    pixels[i] = new Color(c.r * factor, c.g * factor, c.b * factor, 1f);
                }
            }
        }

        private static void PaintContours(Color[] pixels, float[] heights, int size, float waterLevel)
        {
            var isIndex = new bool[size * size];
            var isLine = new bool[size * size];
            for (var py = 0; py < size - 1; py++)
            {
                for (var px = 0; px < size - 1; px++)
                {
                    var i = py * size + px;
                    var h = heights[i];
                    if (h < waterLevel)
                        continue;
                    var c = Mathf.FloorToInt(h / ContourInterval);
                    var hr = heights[i + 1];
                    var hu = heights[i + size];
                    var cr = Mathf.FloorToInt(hr / ContourInterval);
                    var cu = Mathf.FloorToInt(hu / ContourInterval);
                    if (c == cr && c == cu)
                        continue;

                    isLine[i] = true;
                    var top = Mathf.Max(c, Mathf.Max(cr, cu)) * ContourInterval;
                    if (top % IndexContourInterval == 0)
                    {
                        isIndex[i] = true;
                        isIndex[i + 1] = true;
                    }
                }
            }

            for (var i = 0; i < pixels.Length; i++)
            {
                if (isIndex[i])
                    pixels[i] = Color.Lerp(pixels[i], ContourColor, 0.85f);
                else if (isLine[i])
                    pixels[i] = Color.Lerp(pixels[i], ContourColor, 0.55f);
            }
        }

        private static void PaintWater(Color[] pixels, float[] heights, int size, float waterLevel, MapLayout layout, float minX, float minZ,
            float mpp)
        {
            if (float.IsNegativeInfinity(waterLevel))
                return;

            var threshold = waterLevel - 0.05f;
            var water = new bool[size * size];
            for (var i = 0; i < heights.Length; i++)
                water[i] = heights[i] < threshold;

            // Dar dere küçük dokularda kaybolmasın: merkez çizgisini en az ~1.5 px genişlikte işaretle.
            if (layout != null)
            {
                for (var r = 0; r < layout.Rivers.Count; r++)
                {
                    var river = layout.Rivers[r];
                    if (river?.Points == null)
                        continue;
                    var halfWidth = Mathf.Max(river.Width * 0.25f, 0.75f * mpp);
                    ForEachPolylinePixel(river.Points, halfWidth, size, minX, minZ, mpp, (i, d, s) =>
                    {
                        if (d <= halfWidth)
                            water[i] = true;
                    });
                }
            }

            for (var py = 0; py < size; py++)
            {
                for (var px = 0; px < size; px++)
                {
                    var i = py * size + px;
                    if (!water[i])
                        continue;

                    var shore = (px > 0 && !water[i - 1]) || (px < size - 1 && !water[i + 1])
                                || (py > 0 && !water[i - size]) || (py < size - 1 && !water[i + size]);
                    if (shore)
                    {
                        pixels[i] = ShoreColor;
                        continue;
                    }

                    var depth = Mathf.Clamp01((waterLevel - heights[i]) / 2.5f);
                    pixels[i] = Color.Lerp(WaterShallow, WaterDeep, depth);
                }
            }
        }

        private static void PaintStructures(Color[] pixels, IReadOnlyList<Bounds> structures, int size, float minX, float minZ, float mpp)
        {
            if (structures == null)
                return;

            for (var s = 0; s < structures.Count; s++)
            {
                var b = structures[s];
                if (b.size.x < 0.5f || b.size.z < 0.5f || b.size.x > 120f || b.size.z > 120f)
                    continue;

                var x0 = Mathf.Max(0, Mathf.FloorToInt((b.min.x - minX) / mpp));
                var x1 = Mathf.Min(size - 1, Mathf.CeilToInt((b.max.x - minX) / mpp) - 1);
                var z0 = Mathf.Max(0, Mathf.FloorToInt((b.min.z - minZ) / mpp));
                var z1 = Mathf.Min(size - 1, Mathf.CeilToInt((b.max.z - minZ) / mpp) - 1);
                if (x1 < x0)
                    x1 = x0;
                if (z1 < z0)
                    z1 = z0;
                for (var py = z0; py <= z1; py++)
                {
                    for (var px = x0; px <= x1; px++)
                    {
                        var edge = px == x0 || px == x1 || py == z0 || py == z1;
                        pixels[py * size + px] = edge ? BuildingEdge : BuildingColor;
                    }
                }
            }
        }

        private static void PaintRoads(Color[] pixels, MapLayout layout, int size, float minX, float minZ, float mpp)
        {
            if (layout == null)
                return;

            // Önce toprak yollar, sonra asfalt (kavşaklarda asfalt üstte).
            for (var pass = 0; pass < 2; pass++)
            {
                for (var r = 0; r < layout.Roads.Count; r++)
                {
                    var road = layout.Roads[r];
                    if (road?.Points == null || road.Points.Count < 2)
                        continue;

                    var asphalt = road.Kind == RoadKind.Asphalt;
                    if (asphalt != (pass == 1))
                        continue;

                    if (asphalt)
                    {
                        var fillHalf = Mathf.Max(road.Width * 0.5f, 1.25f * mpp);
                        var casingHalf = fillHalf + Mathf.Max(1.2f, mpp);
                        ForEachPolylinePixel(road.Points, casingHalf + mpp, size, minX, minZ, mpp, (i, d, s) =>
                        {
                            var casing = Mathf.Clamp01((casingHalf - d) / mpp + 0.5f);
                            if (casing <= 0f)
                                return;
                            var fill = Mathf.Clamp01((fillHalf - d) / mpp + 0.5f);
                            var c = Color.Lerp(pixels[i], AsphaltCasing, casing);
                            pixels[i] = Color.Lerp(c, AsphaltFill, fill);
                        });
                    }
                    else
                    {
                        var trail = road.Width <= 4.5f;
                        var half = Mathf.Max(road.Width * 0.4f, 0.8f * mpp);
                        ForEachPolylinePixel(road.Points, half + mpp, size, minX, minZ, mpp, (i, d, s) =>
                        {
                            if (trail && Mathf.Repeat(s, 16f) > 10f)
                                return;
                            var a = Mathf.Clamp01((half - d) / mpp + 0.5f);
                            if (a > 0f)
                                pixels[i] = Color.Lerp(pixels[i], DirtRoad, a * 0.95f);
                        });
                    }
                }
            }

            // Köprüler: yolun iki yanında koyu korkuluk çizgileri.
            for (var b = 0; b < layout.Bridges.Count; b++)
            {
                var bridge = layout.Bridges[b];
                if (bridge == null)
                    continue;
                var dir = bridge.Direction.sqrMagnitude > 1e-4f ? bridge.Direction.normalized : Vector2.up;
                var right = new Vector2(dir.y, -dir.x);
                var halfLength = bridge.Length * 0.5f;
                var offset = bridge.Width * 0.5f + Mathf.Max(0.6f, mpp);
                for (var side = -1; side <= 1; side += 2)
                {
                    var a = bridge.Center - dir * halfLength + right * offset * side;
                    var c = bridge.Center + dir * halfLength + right * offset * side;
                    var line = new List<Vector2> { a, c };
                    var lineHalf = Mathf.Max(0.5f, 0.6f * mpp);
                    ForEachPolylinePixel(line, lineHalf + mpp, size, minX, minZ, mpp, (i, d, s) =>
                    {
                        var alpha = Mathf.Clamp01((lineHalf - d) / mpp + 0.5f);
                        if (alpha > 0f)
                            pixels[i] = Color.Lerp(pixels[i], BuildingEdge, alpha);
                    });
                }
            }
        }

        private static void PaintGrid(Color[] pixels, int size)
        {
            for (var k = 1; k < GridDivisions; k++)
            {
                var line = Mathf.Clamp(Mathf.RoundToInt(k * size / (float)GridDivisions), 0, size - 1);
                for (var t = 0; t < size; t++)
                {
                    var v = line * size + t;   // yatay çizgi (sabit z)
                    var h = t * size + line;   // dikey çizgi (sabit x)
                    pixels[v] = Color.Lerp(pixels[v], GridColor, 0.32f);
                    pixels[h] = Color.Lerp(pixels[h], GridColor, 0.32f);
                }
            }

            var border = Mathf.Max(1, size / 512);
            for (var b = 0; b < border; b++)
            {
                for (var t = 0; t < size; t++)
                {
                    pixels[b * size + t] = BorderColor;
                    pixels[(size - 1 - b) * size + t] = BorderColor;
                    pixels[t * size + b] = BorderColor;
                    pixels[t * size + size - 1 - b] = BorderColor;
                }
            }
        }

        // ================================================================== Yardımcılar

        private delegate void PolylinePixel(int index, float distanceMeters, float alongMeters);

        /// <summary>Çoklu çizgiye reach (m) mesafesindeki her piksel için en yakın segment uzaklığı ve çizgi boyu konumu.</summary>
        private static void ForEachPolylinePixel(List<Vector2> points, float reach, int size, float minX, float minZ, float mpp,
            PolylinePixel action)
        {
            if (points == null || points.Count < 2)
                return;

            // Piksel başına en yakın segmentin uzaklığını tutmak için yerel tampon (yalnızca etkilenen pikseller).
            var best = new Dictionary<int, Vector2>();
            var along = 0f;
            for (var s = 0; s + 1 < points.Count; s++)
            {
                var a = points[s];
                var b = points[s + 1];
                var segLength = Vector2.Distance(a, b);
                var x0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, b.x) - reach - minX) / mpp));
                var x1 = Mathf.Min(size - 1, Mathf.CeilToInt((Mathf.Max(a.x, b.x) + reach - minX) / mpp));
                var z0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.y, b.y) - reach - minZ) / mpp));
                var z1 = Mathf.Min(size - 1, Mathf.CeilToInt((Mathf.Max(a.y, b.y) + reach - minZ) / mpp));
                for (var py = z0; py <= z1; py++)
                {
                    var wz = minZ + (py + 0.5f) * mpp;
                    for (var px = x0; px <= x1; px++)
                    {
                        var wx = minX + (px + 0.5f) * mpp;
                        var d = TerrainNoise.SegmentDistance(wx, wz, a.x, a.y, b.x, b.y, out var t);
                        if (d > reach)
                            continue;
                        var index = py * size + px;
                        if (!best.TryGetValue(index, out var current) || d < current.x)
                            best[index] = new Vector2(d, along + t * segLength);
                    }
                }

                along += segLength;
            }

            foreach (var pair in best)
                action(pair.Key, pair.Value.x, pair.Value.y);
        }

        private static void Gradient(float[] heights, int size, int px, int py, float mpp, out float gx, out float gz)
        {
            var xl = px > 0 ? px - 1 : px;
            var xr = px < size - 1 ? px + 1 : px;
            var zd = py > 0 ? py - 1 : py;
            var zu = py < size - 1 ? py + 1 : py;
            var dxm = Mathf.Max(1, xr - xl) * mpp;
            var dzm = Mathf.Max(1, zu - zd) * mpp;
            gx = (heights[py * size + xr] - heights[py * size + xl]) / dxm;
            gz = (heights[zu * size + px] - heights[zd * size + px]) / dzm;
        }

        private static byte ToByte(float v)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);
        }
    }
}
