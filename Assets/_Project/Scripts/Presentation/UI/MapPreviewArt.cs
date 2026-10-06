using System;
using System.Collections.Generic;
using System.IO;
using Project.Core.Domain;
using Project.Infrastructure.World;
using UnityEngine;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Harita seçim kartları ve yükleme ekranı için üst-görünüm "PUBG tarzı" stilize harita resmi: yükselti gölgeli arazi,
    /// koyu yeşil orman yamaları, krem yollar, mavi sular, adlı yerleşim noktaları, çerçeve ve harita adı (5x7 piksel yazı,
    /// Türkçe harfli). Her harita için çalışma anında bir kez üretilir (sabit tohum → deterministik), bellekte ve
    /// persistentDataPath altında PNG olarak önbelleklenir. Çekirdek çizim (<see cref="Render"/>) Unity nesnesi
    /// gerektirmez; test edilebilir. Kuzey yukarıda, doku (0,0) = güneybatı.
    /// </summary>
    public static partial class MapPreviewArt
    {
        /// <summary>Önbellek sürümü; çizim değişince artırılır (eski PNG'ler yok sayılır).</summary>
        public const int CacheVersion = 1;

        public const int DefaultSize = 512;
        public const int PreviewSeed = 1;
        private const int HeightResolution = 257;

        private static readonly Color Frame = new Color(0.07f, 0.08f, 0.08f);
        private static readonly Color FrameLine = new Color(0.82f, 0.78f, 0.62f);
        private static readonly Color Road = new Color(0.93f, 0.88f, 0.7f);
        private static readonly Color RoadCasing = new Color(0.32f, 0.28f, 0.2f);
        private static readonly Color WaterShallow = new Color(0.36f, 0.62f, 0.78f);
        private static readonly Color WaterDeep = new Color(0.16f, 0.36f, 0.58f);
        private static readonly Color ForestDark = new Color(0.16f, 0.32f, 0.17f);
        private static readonly Color Low = new Color(0.56f, 0.66f, 0.4f);
        private static readonly Color Mid = new Color(0.68f, 0.68f, 0.46f);
        private static readonly Color High = new Color(0.66f, 0.58f, 0.46f);
        private static readonly Color Snow = new Color(0.94f, 0.96f, 0.98f);
        private static readonly Color Sand = new Color(0.84f, 0.79f, 0.6f);
        private static readonly Color Dot = new Color(0.97f, 0.97f, 0.94f);
        private static readonly Color DotMinor = new Color(0.9f, 0.82f, 0.5f);
        private static readonly Color Ink = new Color(0.05f, 0.05f, 0.05f);

        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        // ================================================================== Genel API

        /// <summary>Harita kimliği/adı → önizleme dokusu (bellek → disk → üretim). Hata olursa null.</summary>
        public static Texture2D Get(string mapId, int size = DefaultSize)
        {
            var id = MapCatalog.Normalize(mapId);
            size = Mathf.Clamp(size, 128, 2048);
            var key = id + "_" + size;
            if (Cache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var tex = TryLoadFromDisk(key) ?? Generate(id, size);
            if (tex != null)
                Cache[key] = tex;
            return tex;
        }

        /// <summary>Bellek önbelleğini bırakır (diskteki PNG'ler kalır).</summary>
        public static void ReleaseMemory()
        {
            ReleaseModeCards();
            foreach (var kv in Cache)
                if (kv.Value != null)
                    UnityEngine.Object.Destroy(kv.Value);
            Cache.Clear();
        }

        /// <summary>Diskte önbelleklenen dosya yolu.</summary>
        public static string CachePath(string key) =>
            Path.Combine(UnityEngine.Application.persistentDataPath, "map_preview_v" + CacheVersion + "_" + key + ".png");

        private static Texture2D TryLoadFromDisk(string key)
        {
            try
            {
                var path = CachePath(key);
                if (!File.Exists(path))
                    return null;
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "HK_MapPreview_" + key };
                if (!tex.LoadImage(File.ReadAllBytes(path), false))
                {
                    UnityEngine.Object.Destroy(tex);
                    return null;
                }

                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                return tex;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static Texture2D Generate(string id, int size)
        {
            try
            {
                var layout = MapLayout.Create(id, PreviewSeed);
                var model = TerrainModel.Build(layout, PreviewSeed, HeightResolution);
                var mapPx = size - 2 * Margin(size);
                var heights = SampleHeights(model, layout, mapPx);
                var pixels = Render(layout, heights, size, MapCatalog.DisplayName(id));

                var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, false)
                {
                    name = "HK_MapPreview_" + id,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Trilinear,
                    anisoLevel = 2
                };
                tex.SetPixels32(pixels);
                tex.Apply(true, false);
                TrySaveToDisk(id + "_" + size, pixels, size);
                return tex;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return null;
            }
        }

        private static void TrySaveToDisk(string key, Color32[] pixels, int size)
        {
            try
            {
                var tmp = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tmp.SetPixels32(pixels);
                var png = tmp.EncodeToPNG();
                UnityEngine.Object.Destroy(tmp);
                if (png != null)
                    File.WriteAllBytes(CachePath(key), png);
            }
            catch (Exception)
            {
                // Disk önbelleği isteğe bağlı.
            }
        }

        private static float[] SampleHeights(TerrainModel model, MapLayout layout, int mapPx)
        {
            var extent = layout.HalfSize * 2f;
            var h = new float[mapPx * mapPx];
            var mpp = extent / mapPx;
            for (var y = 0; y < mapPx; y++)
            for (var x = 0; x < mapPx; x++)
                h[y * mapPx + x] = model.SampleHeight(-layout.HalfSize + (x + 0.5f) * mpp, -layout.HalfSize + (y + 0.5f) * mpp);
            return h;
        }

        // ================================================================== Çekirdek çizim (saf)

        /// <summary>Kenar boşluğu (px): çerçeve + harita adı için.</summary>
        public static int Margin(int size) => Mathf.Max(8, size / 32);

        /// <summary>
        /// Resmi çizer. <paramref name="heights"/>: (size - 2*Margin)² piksel merkezi yükseklikleri (satır = güney→kuzey).
        /// Yanlış uzunlukta ise düz arazi varsayılır.
        /// </summary>
        public static Color32[] Render(MapLayout layout, float[] heights, int size, string title)
        {
            var margin = Margin(size);
            var mapPx = size - 2 * margin;
            var count = mapPx * mapPx;
            if (heights == null || heights.Length != count)
                heights = new float[count];

            var half = layout.HalfSize;
            var mpp = half * 2f / mapPx;
            var pix = new Color[count];

            PaintTerrain(pix, heights, mapPx, mpp, layout);
            PaintForest(pix, heights, mapPx, mpp, layout);
            PaintWater(pix, heights, mapPx, mpp, layout);
            PaintRoads(pix, mapPx, half, layout);
            PaintSettlements(pix, mapPx, half, layout);

            var full = new Color[size * size];
            for (var i = 0; i < full.Length; i++)
                full[i] = Frame;
            for (var y = 0; y < mapPx; y++)
                Array.Copy(pix, y * mapPx, full, (y + margin) * size + margin, mapPx);

            DrawFrame(full, size, margin);
            DrawTitle(full, size, margin, title);

            var result = new Color32[full.Length];
            for (var i = 0; i < full.Length; i++)
                result[i] = new Color32(B(full[i].r), B(full[i].g), B(full[i].b), 255);
            return result;
        }

        private static byte B(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);

        private static void PaintTerrain(Color[] pix, float[] h, int n, float mpp, MapLayout layout)
        {
            var span = Mathf.Max(1f, layout.MaxHeight - layout.WaterLevel);
            var light = new Vector3(-0.6f, 0.9f, 0.6f).normalized;
            for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
            {
                var i = y * n + x;
                var ht = h[i];
                var t = Mathf.Clamp01((ht - layout.WaterLevel) / span);
                Color c = t < 0.5f ? Color.Lerp(Low, Mid, t * 2f) : Color.Lerp(Mid, High, (t - 0.5f) * 2f);
                if (layout.BeachHeight > 0f && ht > layout.WaterLevel && ht < layout.WaterLevel + layout.BeachHeight)
                    c = Color.Lerp(Sand, c, (ht - layout.WaterLevel) / layout.BeachHeight);

                var xl = h[y * n + Mathf.Max(0, x - 1)];
                var xr = h[y * n + Mathf.Min(n - 1, x + 1)];
                var zl = h[Mathf.Max(0, y - 1) * n + x];
                var zr = h[Mathf.Min(n - 1, y + 1) * n + x];
                var ex = 2.2f;
                var nrm = new Vector3(-(xr - xl) * ex / (2f * mpp), 1f, -(zr - zl) * ex / (2f * mpp)).normalized;
                var d = Mathf.Clamp01(Vector3.Dot(nrm, light));
                var slope = 1f - nrm.y;

                if (ht >= layout.SnowLine && slope < 0.35f)
                    c = Color.Lerp(c, Snow, Mathf.Clamp01((ht - layout.SnowLine) / 12f + 0.4f));
                if (slope > 0.3f)
                    c = Color.Lerp(c, new Color(0.55f, 0.52f, 0.5f), Mathf.Clamp01((slope - 0.3f) * 2f));

                var f = 0.55f + 0.75f * d;
                pix[i] = new Color(c.r * f, c.g * f, c.b * f, 1f);
            }
        }

        private static void PaintForest(Color[] pix, float[] h, int n, float mpp, MapLayout layout)
        {
            var seed = layout.Seed * 31 + 7;
            var half = layout.HalfSize;
            for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
            {
                var i = y * n + x;
                var ht = h[i];
                if (ht <= layout.WaterLevel + (layout.BeachHeight > 0f ? layout.BeachHeight : 1f) || ht >= layout.SnowLine - 8f)
                    continue;
                var xl = h[y * n + Mathf.Max(0, x - 1)];
                var xr = h[y * n + Mathf.Min(n - 1, x + 1)];
                var zl = h[Mathf.Max(0, y - 1) * n + x];
                var zr = h[Mathf.Min(n - 1, y + 1) * n + x];
                var grad = Mathf.Sqrt((xr - xl) * (xr - xl) + (zr - zl) * (zr - zl)) / (2f * mpp);
                if (grad > 0.7f)
                    continue;

                var wx = -half + (x + 0.5f) * mpp;
                var wz = -half + (y + 0.5f) * mpp;
                var nz = Fbm(wx * 0.006f, wz * 0.006f, seed);
                // Orman düşük-orta rakımda, gürültü eşiğinin üstünde.
                var band = 1f - Mathf.Clamp01(Mathf.Abs((ht - layout.WaterLevel) / Mathf.Max(1f, layout.MaxHeight - layout.WaterLevel) - 0.3f) * 2.4f);
                var v = nz * 0.7f + band * 0.5f;
                if (v < 0.62f || InsideSettlement(layout, wx, wz, 1.15f))
                    continue;
                var a = Mathf.Clamp01((v - 0.62f) * 9f);
                var tex = 0.9f + 0.2f * Hash01(x / 2, y / 2, seed);
                var fc = new Color(ForestDark.r * tex, ForestDark.g * tex, ForestDark.b * tex, 1f);
                var baseC = pix[i];
                var shade = 0.7f + 0.5f * (baseC.g / Mathf.Max(0.01f, Low.g));
                pix[i] = Color.Lerp(baseC, new Color(fc.r * shade, fc.g * shade, fc.b * shade, 1f), a * 0.85f);
            }
        }

        private static bool InsideSettlement(MapLayout layout, float wx, float wz, float scale)
        {
            for (var i = 0; i < layout.Locations.Count; i++)
            {
                var l = layout.Locations[i];
                if (l.Kind == LocationKind.Forest)
                    continue;
                var r = (l.ClearRadius > 0f ? l.ClearRadius : l.Radius) * scale;
                var dx = wx - l.Center.x;
                var dz = wz - l.Center.y;
                if (dx * dx + dz * dz < r * r)
                    return true;
            }

            return false;
        }

        private static void PaintWater(Color[] pix, float[] h, int n, float mpp, MapLayout layout)
        {
            var half = layout.HalfSize;
            for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
            {
                var i = y * n + x;
                var depth = layout.WaterLevel - h[i];
                if (depth <= 0f)
                    continue;
                var c = Color.Lerp(WaterShallow, WaterDeep, Mathf.Clamp01(depth / 8f));
                pix[i] = new Color(c.r, c.g, c.b, 1f);
            }

            // Göller (zemin oyuğu zayıf kalırsa görsel garanti) ve dereler.
            for (var i = 0; i < layout.Lakes.Count; i++)
            {
                var l = layout.Lakes[i];
                FillDisc(pix, n, ToPx(l.Center, half, n), l.Radius / mpp * 0.98f, layout.FrozenLakes ? new Color(0.78f, 0.88f, 0.94f) : WaterShallow);
            }

            for (var i = 0; i < layout.Rivers.Count; i++)
                Polyline(pix, n, layout.Rivers[i].Points, half, Mathf.Max(1.5f, layout.Rivers[i].Width / mpp), layout.FrozenLakes ? new Color(0.78f, 0.88f, 0.94f) : WaterShallow);
        }

        private static void PaintRoads(Color[] pix, int n, float half, MapLayout layout)
        {
            var mpp = half * 2f / n;
            for (var pass = 0; pass < 2; pass++)
            for (var i = 0; i < layout.Roads.Count; i++)
            {
                var r = layout.Roads[i];
                var w = Mathf.Max(1.6f, r.Width / mpp * (r.Kind == RoadKind.Asphalt ? 1.5f : 1.1f));
                if (pass == 0)
                    Polyline(pix, n, r.Points, half, w + 1.6f, RoadCasing);
                else
                    Polyline(pix, n, r.Points, half, w, r.Kind == RoadKind.Asphalt ? Road : Color.Lerp(Road, Sand, 0.45f));
            }
        }

        private static void PaintSettlements(Color[] pix, int n, float half, MapLayout layout)
        {
            var scale = Mathf.Max(1, n / 256);
            for (var i = 0; i < layout.Locations.Count; i++)
            {
                var l = layout.Locations[i];
                var p = ToPx(l.Center, half, n);
                var r = (l.IsMajor ? 3.2f : 2.2f) * scale * 0.9f;
                FillDisc(pix, n, p, r + 1.4f, Ink);
                FillDisc(pix, n, p, r, l.IsMajor ? Dot : DotMinor);
                if (!l.IsMajor || string.IsNullOrEmpty(l.Name))
                    continue;
                var tw = TextWidth(l.Name, scale);
                var tx = Mathf.RoundToInt(p.x - tw * 0.5f);
                var ty = Mathf.RoundToInt(p.y + r + 3f * scale);
                tx = Mathf.Clamp(tx, 2, Mathf.Max(2, n - tw - 2));
                if (ty + 9 * scale >= n)
                    ty = Mathf.RoundToInt(p.y - r - 12f * scale);
                DrawText(pix, n, n, tx + 1, ty - 1, l.Name, scale, Ink);
                DrawText(pix, n, n, tx, ty, l.Name, scale, Dot);
            }
        }

        private static void DrawFrame(Color[] full, int size, int margin)
        {
            Rect(full, size, margin - 2, margin - 2, size - margin + 2, size - margin + 2, FrameLine, 2);
            Rect(full, size, 2, 2, size - 2, size - 2, new Color(FrameLine.r * 0.6f, FrameLine.g * 0.6f, FrameLine.b * 0.6f), 1);
            // Pafta çentikleri: 10 bölme.
            var mapPx = size - 2 * margin;
            for (var k = 0; k <= 10; k++)
            {
                var p = margin + Mathf.RoundToInt(mapPx * k / 10f);
                for (var t = 3; t < margin - 3; t++)
                {
                    Put(full, size, p, t, FrameLine);
                    Put(full, size, p, size - 1 - t, FrameLine);
                    Put(full, size, t, p, FrameLine);
                    Put(full, size, size - 1 - t, p, FrameLine);
                }
            }
        }

        private static void DrawTitle(Color[] full, int size, int margin, string title)
        {
            if (string.IsNullOrEmpty(title))
                return;
            var scale = Mathf.Max(2, size / 96);
            var tw = TextWidth(title, scale);
            var th = 7 * scale;
            var padX = 6 * scale;
            var padY = 3 * scale;
            var x0 = margin + 8;
            var y0 = margin + 8;
            for (var y = y0; y < y0 + th + padY * 2; y++)
            for (var x = x0; x < x0 + tw + padX * 2; x++)
            {
                var i = y * size + x;
                if (i < full.Length)
                    full[i] = Color.Lerp(full[i], Frame, 0.78f);
            }

            Rect(full, size, x0, y0, x0 + tw + padX * 2, y0 + th + padY * 2, FrameLine, 1);
            DrawText(full, size, size, x0 + padX, y0 + padY, title, scale, Dot);
        }

        // ================================================================== Çizim yardımcıları

        private static Vector2 ToPx(Vector2 world, float half, int n) =>
            new Vector2((world.x + half) / (half * 2f) * n, (world.y + half) / (half * 2f) * n);

        private static void Put(Color[] buf, int size, int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= size || y >= size)
                return;
            buf[y * size + x] = c;
        }

        private static void Rect(Color[] buf, int size, int x0, int y0, int x1, int y1, Color c, int thick)
        {
            for (var t = 0; t < thick; t++)
            for (var x = x0; x <= x1; x++)
            {
                Put(buf, size, x, y0 + t, c);
                Put(buf, size, x, y1 - t, c);
            }

            for (var t = 0; t < thick; t++)
            for (var y = y0; y <= y1; y++)
            {
                Put(buf, size, x0 + t, y, c);
                Put(buf, size, x1 - t, y, c);
            }
        }

        private static void FillDisc(Color[] buf, int n, Vector2 c, float r, Color col)
        {
            var x0 = Mathf.Max(0, Mathf.FloorToInt(c.x - r));
            var x1 = Mathf.Min(n - 1, Mathf.CeilToInt(c.x + r));
            var y0 = Mathf.Max(0, Mathf.FloorToInt(c.y - r));
            var y1 = Mathf.Min(n - 1, Mathf.CeilToInt(c.y + r));
            var r2 = r * r;
            for (var y = y0; y <= y1; y++)
            for (var x = x0; x <= x1; x++)
            {
                var dx = x + 0.5f - c.x;
                var dy = y + 0.5f - c.y;
                if (dx * dx + dy * dy <= r2)
                    buf[y * n + x] = col;
            }
        }

        private static void Polyline(Color[] buf, int n, List<Vector2> pts, float half, float widthPx, Color col)
        {
            if (pts == null || pts.Count < 2)
                return;
            var r = widthPx * 0.5f;
            for (var i = 0; i < pts.Count - 1; i++)
            {
                var a = ToPx(pts[i], half, n);
                var b = ToPx(pts[i + 1], half, n);
                var len = Vector2.Distance(a, b);
                var steps = Mathf.Max(1, Mathf.CeilToInt(len / Mathf.Max(0.5f, r * 0.5f)));
                for (var s = 0; s <= steps; s++)
                    FillDisc(buf, n, Vector2.Lerp(a, b, s / (float)steps), r, col);
            }
        }

        // ================================================================== Gürültü

        private static float Hash01(int x, int y, int seed)
        {
            unchecked
            {
                var h = (uint)(x * 374761393 + y * 668265263 + seed * 1274126177);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        private static float ValueNoise(float x, float y, int seed)
        {
            var xi = Mathf.FloorToInt(x);
            var yi = Mathf.FloorToInt(y);
            var fx = x - xi;
            var fy = y - yi;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            var a = Mathf.Lerp(Hash01(xi, yi, seed), Hash01(xi + 1, yi, seed), fx);
            var b = Mathf.Lerp(Hash01(xi, yi + 1, seed), Hash01(xi + 1, yi + 1, seed), fx);
            return Mathf.Lerp(a, b, fy);
        }

        private static float Fbm(float x, float y, int seed)
        {
            var sum = 0f;
            var amp = 0.5f;
            var norm = 0f;
            for (var o = 0; o < 4; o++)
            {
                sum += ValueNoise(x, y, seed + o * 101) * amp;
                norm += amp;
                x *= 2.03f;
                y *= 2.03f;
                amp *= 0.5f;
            }

            return sum / norm;
        }

        // ================================================================== 5x7 yazı tipi

        private static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
        {
            ['A'] = new[] { "01110", "10001", "10001", "11111", "10001", "10001", "10001" },
            ['B'] = new[] { "11110", "10001", "10001", "11110", "10001", "10001", "11110" },
            ['C'] = new[] { "01110", "10001", "10000", "10000", "10000", "10001", "01110" },
            ['D'] = new[] { "11110", "10001", "10001", "10001", "10001", "10001", "11110" },
            ['E'] = new[] { "11111", "10000", "10000", "11110", "10000", "10000", "11111" },
            ['F'] = new[] { "11111", "10000", "10000", "11110", "10000", "10000", "10000" },
            ['G'] = new[] { "01110", "10001", "10000", "10111", "10001", "10001", "01111" },
            ['H'] = new[] { "10001", "10001", "10001", "11111", "10001", "10001", "10001" },
            ['I'] = new[] { "01110", "00100", "00100", "00100", "00100", "00100", "01110" },
            ['J'] = new[] { "00111", "00010", "00010", "00010", "00010", "10010", "01100" },
            ['K'] = new[] { "10001", "10010", "10100", "11000", "10100", "10010", "10001" },
            ['L'] = new[] { "10000", "10000", "10000", "10000", "10000", "10000", "11111" },
            ['M'] = new[] { "10001", "11011", "10101", "10101", "10001", "10001", "10001" },
            ['N'] = new[] { "10001", "11001", "10101", "10011", "10001", "10001", "10001" },
            ['O'] = new[] { "01110", "10001", "10001", "10001", "10001", "10001", "01110" },
            ['P'] = new[] { "11110", "10001", "10001", "11110", "10000", "10000", "10000" },
            ['Q'] = new[] { "01110", "10001", "10001", "10001", "10101", "10010", "01101" },
            ['R'] = new[] { "11110", "10001", "10001", "11110", "10100", "10010", "10001" },
            ['S'] = new[] { "01111", "10000", "10000", "01110", "00001", "00001", "11110" },
            ['T'] = new[] { "11111", "00100", "00100", "00100", "00100", "00100", "00100" },
            ['U'] = new[] { "10001", "10001", "10001", "10001", "10001", "10001", "01110" },
            ['V'] = new[] { "10001", "10001", "10001", "10001", "10001", "01010", "00100" },
            ['W'] = new[] { "10001", "10001", "10001", "10101", "10101", "11011", "10001" },
            ['X'] = new[] { "10001", "10001", "01010", "00100", "01010", "10001", "10001" },
            ['Y'] = new[] { "10001", "10001", "01010", "00100", "00100", "00100", "00100" },
            ['Z'] = new[] { "11111", "00001", "00010", "00100", "01000", "10000", "11111" },
            ['-'] = new[] { "00000", "00000", "00000", "11111", "00000", "00000", "00000" },
            ['0'] = new[] { "01110", "10011", "10101", "10101", "10101", "11001", "01110" },
            ['1'] = new[] { "00100", "01100", "00100", "00100", "00100", "00100", "01110" },
            ['2'] = new[] { "01110", "10001", "00001", "00110", "01000", "10000", "11111" },
            ['3'] = new[] { "11110", "00001", "00001", "01110", "00001", "00001", "11110" },
            ['4'] = new[] { "00010", "00110", "01010", "10010", "11111", "00010", "00010" },
            ['5'] = new[] { "11111", "10000", "11110", "00001", "00001", "10001", "01110" },
            ['6'] = new[] { "00110", "01000", "10000", "11110", "10001", "10001", "01110" },
            ['7'] = new[] { "11111", "00001", "00010", "00100", "01000", "01000", "01000" },
            ['8'] = new[] { "01110", "10001", "10001", "01110", "10001", "10001", "01110" },
            ['9'] = new[] { "01110", "10001", "10001", "01111", "00001", "00010", "01100" }
        };

        private enum Accent { None, Cedilla, Breve, Dot, Diaeresis }

        /// <summary>Türkçe büyük harfe çevirir (i→İ, ı→I) ve taban harf + aksan döndürür.</summary>
        public static void Decompose(char ch, out char baseChar, out int accent)
        {
            accent = (int)Accent.None;
            switch (ch)
            {
                case 'Ç': case 'ç': baseChar = 'C'; accent = (int)Accent.Cedilla; return;
                case 'Ş': case 'ş': baseChar = 'S'; accent = (int)Accent.Cedilla; return;
                case 'Ğ': case 'ğ': baseChar = 'G'; accent = (int)Accent.Breve; return;
                case 'İ': case 'i': baseChar = 'I'; accent = (int)Accent.Dot; return;
                case 'ı': baseChar = 'I'; return;
                case 'Ö': case 'ö': baseChar = 'O'; accent = (int)Accent.Diaeresis; return;
                case 'Ü': case 'ü': baseChar = 'U'; accent = (int)Accent.Diaeresis; return;
                default: baseChar = char.ToUpperInvariant(ch); return;
            }
        }

        /// <summary>Metin genişliği (px): harf başına 6 ölçek birimi.</summary>
        public static int TextWidth(string text, int scale) => string.IsNullOrEmpty(text) ? 0 : text.Length * 6 * scale - scale;

        /// <summary>(x, y) = metnin sol-alt köşesi değil sol-üst "gövde" başlangıcı; y yukarı doğru artar (doku koordinatı).</summary>
        private static void DrawText(Color[] buf, int w, int h, int x, int y, string text, int scale, Color col)
        {
            var cx = x;
            foreach (var ch in text)
            {
                Decompose(ch, out var b, out var acc);
                if (Glyphs.TryGetValue(b, out var rows))
                {
                    for (var r = 0; r < 7; r++)
                    for (var c = 0; c < 5; c++)
                        if (rows[r][c] == '1')
                            Block(buf, w, h, cx + c * scale, y + (6 - r) * scale, scale, col);
                    DrawAccent(buf, w, h, cx, y, scale, (Accent)acc, col);
                }

                cx += 6 * scale;
            }
        }

        private static void DrawAccent(Color[] buf, int w, int h, int cx, int y, int scale, Accent a, Color col)
        {
            switch (a)
            {
                case Accent.Cedilla:
                    Block(buf, w, h, cx + 2 * scale, y - scale, scale, col);
                    Block(buf, w, h, cx + 1 * scale, y - 2 * scale, scale, col);
                    break;
                case Accent.Breve:
                    Block(buf, w, h, cx + 1 * scale, y + 8 * scale, scale, col);
                    Block(buf, w, h, cx + 2 * scale, y + 7 * scale, scale, col);
                    Block(buf, w, h, cx + 3 * scale, y + 8 * scale, scale, col);
                    break;
                case Accent.Dot:
                    Block(buf, w, h, cx + 2 * scale, y + 8 * scale, scale, col);
                    break;
                case Accent.Diaeresis:
                    Block(buf, w, h, cx + 1 * scale, y + 8 * scale, scale, col);
                    Block(buf, w, h, cx + 3 * scale, y + 8 * scale, scale, col);
                    break;
            }
        }

        private static void Block(Color[] buf, int w, int h, int x, int y, int s, Color col)
        {
            for (var yy = 0; yy < s; yy++)
            for (var xx = 0; xx < s; xx++)
            {
                var px = x + xx;
                var py = y + yy;
                if (px >= 0 && py >= 0 && px < w && py < h)
                    buf[py * w + px] = col;
            }
        }
    }
}
