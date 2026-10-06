using System;
using Project.Core.Domain;
using UnityEngine;

namespace Project.Presentation.UI
{
    /// <summary>Küçük yazılım tuvali (CPU piksel tamponu): gradyan, daire, çizgi, çokgen, silüet. Saf; Unity dokusuna gerek duymaz.</summary>
    public sealed class ArtCanvas
    {
        public readonly int Width;
        public readonly int Height;
        public readonly Color32[] Pixels;

        public ArtCanvas(int width, int height)
        {
            Width = Math.Max(4, width);
            Height = Math.Max(4, height);
            Pixels = new Color32[Width * Height];
        }

        public static Color32 ToColor32(Color c) => new Color32((byte)Mathf.Clamp(c.r * 255f, 0, 255), (byte)Mathf.Clamp(c.g * 255f, 0, 255),
            (byte)Mathf.Clamp(c.b * 255f, 0, 255), (byte)Mathf.Clamp(c.a * 255f, 0, 255));

        /// <summary>Alfa karışımlı piksel (y = 0 altta).</summary>
        public void Blend(int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height || c.a <= 0f)
                return;
            var i = y * Width + x;
            var d = Pixels[i];
            var a = Mathf.Clamp01(c.a);
            Pixels[i] = new Color32(
                (byte)(d.r + (c.r * 255f - d.r) * a),
                (byte)(d.g + (c.g * 255f - d.g) * a),
                (byte)(d.b + (c.b * 255f - d.b) * a),
                255);
        }

        public void VerticalGradient(Color top, Color bottom)
        {
            for (var y = 0; y < Height; y++)
            {
                var t = y / (float)(Height - 1);
                var c = Color.Lerp(bottom, top, t);
                var packed = ToColor32(c);
                for (var x = 0; x < Width; x++)
                    Pixels[y * Width + x] = packed;
            }
        }

        public void FillRect(int x0, int y0, int w, int h, Color c)
        {
            for (var y = y0; y < y0 + h; y++)
                for (var x = x0; x < x0 + w; x++)
                    Blend(x, y, c);
        }

        public void FillCircle(float cx, float cy, float r, Color c)
        {
            var x0 = (int)Math.Floor(cx - r - 1);
            var x1 = (int)Math.Ceiling(cx + r + 1);
            var y0 = (int)Math.Floor(cy - r - 1);
            var y1 = (int)Math.Ceiling(cy + r + 1);
            for (var y = y0; y <= y1; y++)
            {
                for (var x = x0; x <= x1; x++)
                {
                    var d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    var cov = Mathf.Clamp01(r - d + 0.5f);
                    if (cov > 0f)
                        Blend(x, y, new Color(c.r, c.g, c.b, c.a * cov));
                }
            }
        }

        public void Ring(float cx, float cy, float radius, float thickness, Color c)
        {
            var outer = radius + thickness * 0.5f;
            var x0 = (int)Math.Floor(cx - outer - 1);
            var x1 = (int)Math.Ceiling(cx + outer + 1);
            var y0 = (int)Math.Floor(cy - outer - 1);
            var y1 = (int)Math.Ceiling(cy + outer + 1);
            for (var y = y0; y <= y1; y++)
            {
                for (var x = x0; x <= x1; x++)
                {
                    var d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    var cov = Mathf.Clamp01(thickness * 0.5f - Mathf.Abs(d - radius) + 0.5f);
                    if (cov > 0f)
                        Blend(x, y, new Color(c.r, c.g, c.b, c.a * cov));
                }
            }
        }

        public void Line(float x0, float y0, float x1, float y1, float thickness, Color c)
        {
            var dx = x1 - x0;
            var dy = y1 - y0;
            var len = Mathf.Max(1f, Mathf.Sqrt(dx * dx + dy * dy));
            var steps = Mathf.CeilToInt(len * 1.5f);
            for (var i = 0; i <= steps; i++)
            {
                var t = i / (float)steps;
                FillCircle(x0 + dx * t, y0 + dy * t, thickness * 0.5f, c);
            }
        }

        /// <summary>Dışbükey/içbükey çokgen (çift-tek kuralı, satır taraması).</summary>
        public void Polygon(Vector2[] pts, Color c)
        {
            if (pts == null || pts.Length < 3)
                return;
            float minY = float.MaxValue, maxY = float.MinValue;
            for (var i = 0; i < pts.Length; i++)
            {
                minY = Mathf.Min(minY, pts[i].y);
                maxY = Mathf.Max(maxY, pts[i].y);
            }

            var xs = new float[pts.Length];
            for (var y = Mathf.Max(0, Mathf.FloorToInt(minY)); y <= Mathf.Min(Height - 1, Mathf.CeilToInt(maxY)); y++)
            {
                var n = 0;
                var sy = y + 0.5f;
                for (var i = 0; i < pts.Length; i++)
                {
                    var a = pts[i];
                    var b = pts[(i + 1) % pts.Length];
                    if ((a.y <= sy && b.y > sy) || (b.y <= sy && a.y > sy))
                        xs[n++] = a.x + (sy - a.y) / (b.y - a.y) * (b.x - a.x);
                }

                Array.Sort(xs, 0, n);
                for (var k = 0; k + 1 < n; k += 2)
                    for (var x = Mathf.Max(0, Mathf.FloorToInt(xs[k])); x <= Mathf.Min(Width - 1, Mathf.CeilToInt(xs[k + 1]) - 1); x++)
                        Blend(x, y, c);
            }
        }

        /// <summary>Sütun başına yükseklik fonksiyonuyla alttan dolu silüet.</summary>
        public void Silhouette(Func<float, float> heightAtX01, Color c)
        {
            for (var x = 0; x < Width; x++)
            {
                var h = Mathf.RoundToInt(heightAtX01(x / (float)(Width - 1)) * Height);
                for (var y = 0; y < Mathf.Min(h, Height); y++)
                    Blend(x, y, c);
            }
        }

        /// <summary>Alt ve kenar koyulaştırma (vinyet).</summary>
        public void Vignette(float strength)
        {
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    var nx = x / (float)(Width - 1) * 2f - 1f;
                    var ny = y / (float)(Height - 1) * 2f - 1f;
                    var v = Mathf.Clamp01((nx * nx * 0.7f + ny * ny * 0.9f) - 0.35f) * strength;
                    Blend(x, y, new Color(0f, 0f, 0f, v));
                }
            }
        }

        public Texture2D ToTexture(string name)
        {
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            tex.SetPixels32(Pixels);
            tex.Apply(false, true);
            return tex;
        }
    }

    /// <summary>
    /// Ana menü prosedürel kapak görselleri: mod kartları (BR, Çatışma, Konvoy, Rehine, Poligon) ve harita silüetleri
    /// (Kuzgun Vadisi, Ayaz Geçidi, Mavi Liman, Kartal Yaylası). Deterministik; hiçbir dosya gerektirmez.
    /// </summary>
    public static class MainMenuKeyArt
    {
        public const int ModeCount = 5;

        private static float Hash(int n)
        {
            unchecked
            {
                n = (n << 13) ^ n;
                return 1f - ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 1073741824f;
            }
        }

        private static float Noise(float x, int seed)
        {
            var i = Mathf.FloorToInt(x);
            var f = x - i;
            var u = f * f * (3f - 2f * f);
            return Mathf.Lerp(Hash(i + seed * 131), Hash(i + 1 + seed * 131), u) * 0.5f + 0.5f;
        }

        private static float Fbm(float x, int seed)
            => Noise(x, seed) * 0.55f + Noise(x * 2.1f, seed + 7) * 0.3f + Noise(x * 4.3f, seed + 13) * 0.15f;

        /// <summary>Mod kapak tuvali (0 BR, 1 Çatışma, 2 Konvoy, 3 Rehine, 4 Poligon).</summary>
        public static ArtCanvas Mode(int index, int width = 320, int height = 200)
        {
            index = Mathf.Clamp(index, 0, ModeCount - 1);
            var c = new ArtCanvas(width, height);
            var w = (float)c.Width;
            var h = (float)c.Height;

            Color top, bottom, accent;
            switch (index)
            {
                case 0: top = new Color(0.10f, 0.14f, 0.22f); bottom = new Color(0.86f, 0.52f, 0.28f); accent = new Color(0.31f, 0.62f, 1f); break;
                case 1: top = new Color(0.16f, 0.07f, 0.07f); bottom = new Color(0.78f, 0.30f, 0.16f); accent = new Color(0.95f, 0.22f, 0.2f); break;
                case 2: top = new Color(0.08f, 0.13f, 0.12f); bottom = new Color(0.78f, 0.62f, 0.34f); accent = new Color(0.95f, 0.72f, 0.2f); break;
                case 3: top = new Color(0.07f, 0.08f, 0.16f); bottom = new Color(0.5f, 0.28f, 0.34f); accent = new Color(0.95f, 0.85f, 0.4f); break;
                default: top = new Color(0.1f, 0.14f, 0.09f); bottom = new Color(0.62f, 0.64f, 0.42f); accent = new Color(0.9f, 0.2f, 0.18f); break;
            }

            c.VerticalGradient(top, bottom);
            c.FillCircle(w * 0.74f, h * 0.62f, h * 0.2f, new Color(1f, 0.82f, 0.55f, 0.35f));
            c.FillCircle(w * 0.74f, h * 0.62f, h * 0.09f, new Color(1f, 0.9f, 0.7f, 0.55f));

            var ridge = new Color(0.04f, 0.06f, 0.05f, 0.92f);
            c.Silhouette(x => 0.22f + 0.2f * Fbm(x * 5f, 3 + index), new Color(ridge.r + 0.05f, ridge.g + 0.06f, ridge.b + 0.06f, 0.55f));
            c.Silhouette(x => 0.12f + 0.14f * Fbm(x * 7f + 4f, 9 + index), ridge);

            switch (index)
            {
                case 0: DrawBattleRoyale(c, accent); break;
                case 1: DrawSkirmish(c, accent); break;
                case 2: DrawConvoy(c, accent); break;
                case 3: DrawHostage(c, accent); break;
                default: DrawRange(c, accent); break;
            }

            c.Vignette(0.7f);
            return c;
        }

        private static void DrawBattleRoyale(ArtCanvas c, Color accent)
        {
            float w = c.Width, h = c.Height;
            var cx = w * 0.5f;
            var cy = h * 0.5f;
            for (var i = 0; i < 4; i++)
                c.Ring(cx, cy, h * (0.18f + i * 0.1f), i == 3 ? 3f : 1.6f, new Color(accent.r, accent.g, accent.b, i == 3 ? 0.9f : 0.45f));
            c.Ring(cx, cy, h * 0.05f, 2f, Color.white);
            // T-70 siluet: gövde, kuyruk, rotor.
            var dark = new Color(0.02f, 0.03f, 0.03f, 0.95f);
            c.Polygon(new[] { new Vector2(cx - 34, cy + 24), new Vector2(cx + 20, cy + 28), new Vector2(cx + 34, cy + 20), new Vector2(cx + 22, cy + 10), new Vector2(cx - 20, cy + 10) }, dark);
            c.Polygon(new[] { new Vector2(cx - 20, cy + 24), new Vector2(cx - 60, cy + 30), new Vector2(cx - 62, cy + 36), new Vector2(cx - 18, cy + 30) }, dark);
            c.Line(cx - 44, cy + 36, cx + 48, cy + 34, 1.6f, new Color(0f, 0f, 0f, 0.8f));
            c.Line(cx - 2, cy + 28, cx - 2, cy + 36, 2f, dark);
            for (var i = 0; i < 6; i++)
            {
                var px = w * (0.12f + i * 0.14f) + Hash(i) * 6f;
                var py = h * (0.72f + Hash(i + 20) * 0.12f);
                c.FillCircle(px, py, 3.2f, new Color(0.9f, 0.9f, 0.85f, 0.85f));
                c.Line(px, py - 3, px, py - 12, 1f, new Color(1f, 1f, 1f, 0.5f));
            }
        }

        private static void DrawSkirmish(ArtCanvas c, Color accent)
        {
            float w = c.Width, h = c.Height;
            var blue = new Color(0.31f, 0.62f, 1f, 0.9f);
            for (var i = 0; i < 9; i++)
            {
                var y = h * (0.3f + Hash(i + 4) * 0.2f + i * 0.04f);
                c.Line(w * 0.06f, y, w * (0.45f + Hash(i) * 0.1f), y + Hash(i + 8) * 14f, 1.8f, new Color(accent.r, accent.g, accent.b, 0.8f));
                c.Line(w * 0.94f, y + 6f, w * (0.55f - Hash(i + 3) * 0.1f), y - Hash(i + 9) * 14f, 1.8f, blue);
            }

            c.FillCircle(w * 0.5f, h * 0.44f, 14f, new Color(1f, 0.85f, 0.4f, 0.8f));
            c.FillCircle(w * 0.5f, h * 0.44f, 26f, new Color(1f, 0.5f, 0.2f, 0.25f));
            for (var i = 0; i < 2; i++)
            {
                var sx = i == 0 ? w * 0.18f : w * 0.82f;
                DrawSoldier(c, sx, h * 0.13f, 1f, new Color(0.02f, 0.03f, 0.03f, 1f));
            }
        }

        private static void DrawSoldier(ArtCanvas c, float x, float y, float scale, Color color)
        {
            c.FillCircle(x, y + 36 * scale, 6f * scale, color);
            c.Polygon(new[] { new Vector2(x - 9 * scale, y + 30 * scale), new Vector2(x + 9 * scale, y + 30 * scale), new Vector2(x + 7 * scale, y + 8 * scale), new Vector2(x - 7 * scale, y + 8 * scale) }, color);
            c.Polygon(new[] { new Vector2(x - 7 * scale, y + 8 * scale), new Vector2(x - 2 * scale, y + 8 * scale), new Vector2(x - 4 * scale, y), new Vector2(x - 9 * scale, y) }, color);
            c.Polygon(new[] { new Vector2(x + 2 * scale, y + 8 * scale), new Vector2(x + 7 * scale, y + 8 * scale), new Vector2(x + 9 * scale, y), new Vector2(x + 4 * scale, y) }, color);
            c.Line(x + 4 * scale, y + 22 * scale, x + 22 * scale, y + 26 * scale, 2.4f * scale, color);
        }

        private static void DrawConvoy(ArtCanvas c, Color accent)
        {
            float w = c.Width, h = c.Height;
            c.Polygon(new[] { new Vector2(0, h * 0.2f), new Vector2(w, h * 0.26f), new Vector2(w, h * 0.12f), new Vector2(0, h * 0.08f) }, new Color(0.06f, 0.06f, 0.06f, 0.95f));
            for (var i = 0; i < 9; i++)
                c.FillRect(Mathf.RoundToInt(w * (0.04f + i * 0.11f)), Mathf.RoundToInt(h * (0.14f + i * 0.0067f)), 12, 2, new Color(0.9f, 0.85f, 0.5f, 0.6f));
            var dark = new Color(0.02f, 0.03f, 0.03f, 0.97f);
            var offsets = new[] { 0.1f, 0.38f, 0.66f };
            for (var i = 0; i < 3; i++)
            {
                var x = w * offsets[i];
                var y = h * (0.2f + i * 0.015f);
                var len = i == 1 ? 62f : 48f;
                c.FillRect(Mathf.RoundToInt(x), Mathf.RoundToInt(y + 8), Mathf.RoundToInt(len), 22, dark);
                c.FillRect(Mathf.RoundToInt(x + len * 0.55f), Mathf.RoundToInt(y + 28), Mathf.RoundToInt(len * 0.4f), 8, dark);
                c.FillCircle(x + 10, y + 8, 8f, dark);
                c.FillCircle(x + len - 10, y + 8, 8f, dark);
                c.FillCircle(x + 10, y + 8, 3f, new Color(accent.r, accent.g, accent.b, 0.8f));
            }

            c.Line(w * 0.2f, h * 0.55f, w * 0.8f, h * 0.55f, 2f, new Color(accent.r, accent.g, accent.b, 0.7f));
            c.Polygon(new[] { new Vector2(w * 0.8f, h * 0.55f + 8), new Vector2(w * 0.8f + 14, h * 0.55f), new Vector2(w * 0.8f, h * 0.55f - 8) }, new Color(accent.r, accent.g, accent.b, 0.85f));
        }

        private static void DrawHostage(ArtCanvas c, Color accent)
        {
            float w = c.Width, h = c.Height;
            var cx = w * 0.5f;
            var cy = h * 0.42f;
            c.Ring(cx, cy, h * 0.3f, 2.4f, new Color(accent.r, accent.g, accent.b, 0.8f));
            c.Ring(cx, cy, h * 0.36f, 1.2f, new Color(accent.r, accent.g, accent.b, 0.4f));
            DrawSoldier(c, cx, cy - 34f, 1.3f, new Color(0.02f, 0.03f, 0.03f, 1f));
            c.Line(cx - 14, cy + 4, cx + 14, cy + 4, 2f, new Color(accent.r, accent.g, accent.b, 0.9f));
            // Kilit
            c.FillRect(Mathf.RoundToInt(cx + 44), Mathf.RoundToInt(cy - 8), 18, 14, new Color(accent.r, accent.g, accent.b, 0.95f));
            c.Ring(cx + 53, cy + 8, 6f, 2.2f, new Color(accent.r, accent.g, accent.b, 0.95f));
            for (var i = 0; i < 3; i++)
                c.FillCircle(w * (0.2f + i * 0.3f), h * 0.1f, 3f, new Color(1f, 0.4f, 0.3f, 0.5f));
        }

        private static void DrawRange(ArtCanvas c, Color accent)
        {
            float w = c.Width, h = c.Height;
            var cx = w * 0.5f;
            var cy = h * 0.5f;
            for (var i = 5; i >= 1; i--)
                c.FillCircle(cx, cy, h * 0.07f * i, i % 2 == 0 ? new Color(0.94f, 0.94f, 0.9f, 0.92f) : new Color(accent.r, accent.g, accent.b, 0.9f));
            c.FillCircle(cx, cy, h * 0.03f, new Color(0.1f, 0.1f, 0.1f, 0.95f));
            for (var i = 0; i < 7; i++)
                c.FillCircle(cx + Hash(i) * h * 0.16f, cy + Hash(i + 11) * h * 0.16f, 2.4f, new Color(0f, 0f, 0f, 0.85f));
            c.Line(cx, 0, cx, cy - h * 0.37f, 3f, new Color(0.04f, 0.04f, 0.04f, 0.95f));
            c.Line(cx - 14, cy - h * 0.37f, cx + 14, cy - h * 0.37f, 3f, new Color(0.04f, 0.04f, 0.04f, 0.95f));
            c.Line(w * 0.08f, h * 0.1f, w * 0.92f, h * 0.1f, 1.2f, new Color(1f, 1f, 1f, 0.35f));
        }

        /// <summary>Harita silüet kapağı (kimlik: <see cref="MapCatalog"/>).</summary>
        public static ArtCanvas Map(string mapId, int width = 320, int height = 200)
        {
            var id = MapCatalog.Normalize(mapId);
            var c = new ArtCanvas(width, height);
            float w = c.Width, h = c.Height;
            if (id == MapCatalog.AyazGecidi)
            {
                c.VerticalGradient(new Color(0.18f, 0.24f, 0.36f), new Color(0.78f, 0.84f, 0.92f));
                c.FillCircle(w * 0.3f, h * 0.7f, h * 0.12f, new Color(1f, 1f, 1f, 0.5f));
                c.Silhouette(x => 0.38f + 0.4f * Mathf.Pow(Fbm(x * 3.2f, 21), 1.6f), new Color(0.52f, 0.6f, 0.72f, 1f));
                c.Silhouette(x => 0.26f + 0.34f * Mathf.Pow(Fbm(x * 4.1f + 3f, 22), 1.5f) * (0.35f + Mathf.Abs(x - 0.5f) * 1.3f), new Color(0.78f, 0.84f, 0.92f, 1f));
                c.Silhouette(x => 0.1f + 0.16f * Fbm(x * 6f, 23), new Color(0.12f, 0.16f, 0.2f, 1f));
                c.Line(w * 0.5f, 0, w * 0.46f, h * 0.2f, 6f, new Color(0.9f, 0.93f, 0.97f, 0.9f));
            }
            else if (id == MapCatalog.MaviLiman)
            {
                c.VerticalGradient(new Color(0.16f, 0.3f, 0.5f), new Color(0.98f, 0.7f, 0.46f));
                c.FillCircle(w * 0.7f, h * 0.5f, h * 0.14f, new Color(1f, 0.9f, 0.7f, 0.6f));
                c.FillRect(0, 0, c.Width, Mathf.RoundToInt(h * 0.4f), new Color(0.08f, 0.22f, 0.36f, 1f));
                for (var i = 0; i < 18; i++)
                    c.FillRect(Mathf.RoundToInt(w * (0.58f + Hash(i) * 0.12f)), Mathf.RoundToInt(h * (0.05f + i * 0.018f)), Mathf.RoundToInt(w * 0.14f), 1, new Color(1f, 0.85f, 0.6f, 0.35f));
                var dark = new Color(0.03f, 0.05f, 0.07f, 1f);
                c.FillRect(0, 0, Mathf.RoundToInt(w * 0.55f), Mathf.RoundToInt(h * 0.28f), dark);
                for (var i = 0; i < 7; i++)
                    c.FillRect(Mathf.RoundToInt(w * (0.02f + i * 0.075f)), Mathf.RoundToInt(h * 0.28f), Mathf.RoundToInt(w * 0.06f), Mathf.RoundToInt(h * (0.05f + (i % 3) * 0.035f)), dark);
                for (var i = 0; i < 2; i++)
                {
                    var x = w * (0.14f + i * 0.28f);
                    c.Line(x, h * 0.28f, x, h * 0.62f, 3f, dark);
                    c.Line(x - 24, h * 0.58f, x + 36, h * 0.62f, 3f, dark);
                    c.Line(x + 36, h * 0.62f, x + 36, h * 0.46f, 1.4f, dark);
                }

                c.Line(w * 0.88f, h * 0.4f, w * 0.88f, h * 0.7f, 5f, dark);
                c.FillCircle(w * 0.88f, h * 0.72f, 6f, new Color(1f, 0.9f, 0.5f, 0.95f));
            }
            else if (id == MapCatalog.KartalYaylasi)
            {
                c.VerticalGradient(new Color(0.22f, 0.3f, 0.5f), new Color(0.96f, 0.78f, 0.52f));
                c.FillCircle(w * 0.2f, h * 0.62f, h * 0.12f, new Color(1f, 0.92f, 0.72f, 0.65f));
                c.Silhouette(x => 0.34f + 0.12f * Fbm(x * 3f, 31), new Color(0.38f, 0.38f, 0.42f, 1f));
                c.Silhouette(x => 0.22f + 0.1f * Fbm(x * 4f + 2f, 32) + (x > 0.62f ? 0.16f * Mathf.Clamp01((x - 0.62f) * 6f) : 0f), new Color(0.16f, 0.2f, 0.14f, 1f));
                c.Silhouette(x => 0.1f + 0.05f * Fbm(x * 6f, 33), new Color(0.07f, 0.1f, 0.07f, 1f));
                var dark = new Color(0.03f, 0.04f, 0.03f, 1f);
                c.FillRect(Mathf.RoundToInt(w * 0.3f), Mathf.RoundToInt(h * 0.2f), 36, 16, dark);
                c.Polygon(new[] { new Vector2(w * 0.3f - 4, h * 0.2f + 16), new Vector2(w * 0.3f + 18, h * 0.2f + 30), new Vector2(w * 0.3f + 40, h * 0.2f + 16) }, dark);
                // Kartal
                var ex = w * 0.62f;
                var ey = h * 0.72f;
                c.Line(ex - 24, ey + 6, ex, ey, 2.4f, dark);
                c.Line(ex, ey, ex + 24, ey + 6, 2.4f, dark);
                c.FillCircle(ex, ey - 1, 3f, dark);
            }
            else
            {
                c.VerticalGradient(new Color(0.14f, 0.18f, 0.3f), new Color(0.88f, 0.54f, 0.32f));
                c.FillCircle(w * 0.78f, h * 0.55f, h * 0.15f, new Color(1f, 0.84f, 0.58f, 0.55f));
                c.Silhouette(x => 0.42f + 0.3f * Fbm(x * 3.6f, 41), new Color(0.2f, 0.24f, 0.3f, 1f));
                c.Silhouette(x => 0.26f + 0.2f * Fbm(x * 5f + 1f, 42), new Color(0.1f, 0.15f, 0.12f, 1f));
                c.Silhouette(x => 0.12f + 0.08f * Fbm(x * 8f, 43), new Color(0.04f, 0.07f, 0.05f, 1f));
                var pine = new Color(0.02f, 0.05f, 0.03f, 1f);
                for (var i = 0; i < 26; i++)
                {
                    var x = w * (i / 26f + Hash(i) * 0.015f);
                    var ph = h * (0.1f + Hash(i + 5) * 0.03f + 0.04f);
                    var s = 7f + Hash(i + 9) * 3f;
                    c.Polygon(new[] { new Vector2(x - s, ph), new Vector2(x + s, ph), new Vector2(x, ph + s * 3.2f) }, pine);
                }

                c.Line(w * 0.36f, 0, w * 0.5f, h * 0.2f, 5f, new Color(0.3f, 0.5f, 0.6f, 0.65f));
                for (var i = 0; i < 3; i++)
                    c.FillRect(0, Mathf.RoundToInt(h * (0.12f + i * 0.05f)), c.Width, Mathf.RoundToInt(h * 0.03f), new Color(1f, 0.8f, 0.6f, 0.06f));
            }

            c.Vignette(0.55f);
            return c;
        }
    }
}
