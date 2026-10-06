using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Mod anahtar-görsel kartları v2: katmanlı siluet kompozisyonları (BR: paraşütçüler + vadi; Çatışma: karşılıklı siperler;
    /// Konvoy: Kirpi kolonu; Rehine: gece ev baskını; Poligon: hedef şeritleri). Koyu antrasit zemin, kırmızı #D43A2E kenar ışığı,
    /// köşe ayraçları. Deterministik prosedürel çizim (<see cref="RenderModeCard"/> saf), doku olarak bellekte önbelleklenir.
    /// Dizin sırası <c>MainMenuKeyArt.Mode</c> ile aynıdır (0 BR, 1 Çatışma, 2 Konvoy, 3 Rehine, 4 Poligon).
    /// </summary>
    public static partial class MapPreviewArt
    {
        public const int ModeCardCount = 5;
        public const int ModeCardWidth = 640;
        public const int ModeCardHeight = 400;

        private static readonly Dictionary<string, Texture2D> ModeCardCache = new Dictionary<string, Texture2D>();

        private static readonly Color McRed = new Color(0.831f, 0.227f, 0.180f, 1f);
        private static readonly Color McRedLight = new Color(1f, 0.42f, 0.34f, 1f);
        private static readonly Color McInk = new Color(0.035f, 0.037f, 0.043f, 1f);

        // ================================================================== Genel API

        /// <summary>Mod kartı dokusu (bellek önbellekli). index 0..4.</summary>
        public static Texture2D GetModeCard(int index, int width = ModeCardWidth, int height = ModeCardHeight)
        {
            index = Mathf.Clamp(index, 0, ModeCardCount - 1);
            width = Mathf.Clamp(width, 160, 1920);
            height = Mathf.Clamp(height, 100, 1200);
            var key = index + "_" + width + "x" + height;
            if (ModeCardCache.TryGetValue(key, out var t) && t != null)
                return t;
            try
            {
                var tex = new Texture2D(width, height, TextureFormat.RGBA32, true, false)
                {
                    name = "HK_ModeCardV2_" + key,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Trilinear,
                    anisoLevel = 2
                };
                tex.SetPixels32(RenderModeCard(index, width, height));
                tex.Apply(true, false);
                ModeCardCache[key] = tex;
                return tex;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return null;
            }
        }

        private static void ReleaseModeCards()
        {
            foreach (var kv in ModeCardCache)
                if (kv.Value != null)
                    UnityEngine.Object.Destroy(kv.Value);
            ModeCardCache.Clear();
        }

        /// <summary>Saf çizim: satır 0 = alt (Unity doku sırası), tam opak.</summary>
        public static Color32[] RenderModeCard(int index, int width = ModeCardWidth, int height = ModeCardHeight)
        {
            index = Mathf.Clamp(index, 0, ModeCardCount - 1);
            var s = new ArtSurface(width, height);
            s.Clear(new Color(McInk.r, McInk.g, McInk.b, 1f));
            float W = width, H = height;
            switch (index)
            {
                case 0: McBattleRoyale(s, W, H); break;
                case 1: McSkirmish(s, W, H); break;
                case 2: McConvoy(s, W, H); break;
                case 3: McHostage(s, W, H); break;
                default: McRange(s, W, H); break;
            }

            McFinish(s, W, H, index);
            return s.ToColor32();
        }

        // ================================================================== Ortak yardımcılar

        private static float McHash(int n)
        {
            unchecked
            {
                n = (n << 13) ^ n;
                n = n * (n * n * 15731 + 789221) + 1376312589;
                return (n & 0x7fffffff) / 2147483648f;
            }
        }

        private static float McNoise(float x, int seed)
        {
            var i = Mathf.FloorToInt(x);
            var f = x - i;
            var u = f * f * (3f - 2f * f);
            return Mathf.Lerp(McHash(i + seed * 131), McHash(i + 1 + seed * 131), u);
        }

        private static float McFbm(float x, int seed)
            => McNoise(x, seed) * 0.55f + McNoise(x * 2.1f, seed + 7) * 0.3f + McNoise(x * 4.3f, seed + 13) * 0.15f;

        private static Color McA(Color c, float a) => new Color(c.r, c.g, c.b, a);

        /// <summary>Üstten yatay zirve çizgisiyle doldurulan siluet katmanı + kırmızı kenar ışığı bandı.</summary>
        private static void McRidge(ArtSurface s, float W, float H, Func<float, float> topY01, Color top, Color bottom, float rimPx, float rimAlpha)
        {
            const int step = 3;
            var n = Mathf.CeilToInt(W / step) + 1;
            var ridge = new Vector2[n];
            var yMin = H;
            for (var i = 0; i < n; i++)
            {
                var x = Mathf.Min(W, i * step);
                var y = topY01(x / W) * H;
                ridge[i] = new Vector2(x, y);
                if (y < yMin) yMin = y;
            }

            var poly = new Vector2[n + 2];
            Array.Copy(ridge, poly, n);
            poly[n] = new Vector2(W + 1f, H + 2f);
            poly[n + 1] = new Vector2(-1f, H + 2f);
            var span = Mathf.Max(1f, H - yMin);
            s.Fill(poly, (x, y) => Color.Lerp(top, bottom, Mathf.Clamp01((y - yMin) / span)));

            if (rimPx > 0f && rimAlpha > 0f)
            {
                var band = new Vector2[n * 2];
                for (var i = 0; i < n; i++)
                    band[i] = ridge[i];
                for (var i = 0; i < n; i++)
                    band[n + i] = ridge[n - 1 - i] + new Vector2(0f, rimPx);
                s.Fill(band, McA(McRed, rimAlpha));
            }
        }

        private static void McTree(ArtSurface s, float x, float gy, float h, Color c, float rimAlpha)
        {
            var layers = 5;
            var w = h * 0.34f;
            s.Fill(new[] { new Vector2(x - h * 0.02f, gy), new Vector2(x + h * 0.02f, gy), new Vector2(x + h * 0.02f, gy - h * 0.2f), new Vector2(x - h * 0.02f, gy - h * 0.2f) }, c);
            for (var i = 0; i < layers; i++)
            {
                var t = i / (float)layers;
                var ly = gy - h * (0.12f + t * 0.8f);
                var lw = w * (1f - t * 0.82f);
                var tri = new[] { new Vector2(x - lw, ly), new Vector2(x + lw, ly), new Vector2(x, ly - h * 0.3f) };
                if (rimAlpha > 0f)
                    s.Fill(new[] { tri[0] + new Vector2(-1.2f, -1.2f), tri[1] + new Vector2(-1.2f, -1.2f), tri[2] + new Vector2(-1.2f, -1.6f) }, McA(McRed, rimAlpha));
                s.Fill(tri, c);
            }
        }

        /// <summary>Askeri siluet (yan görünüm, tek renk). Namlu ucu konumunu döndürür.</summary>
        private static Vector2 McOperator(ArtSurface s, float x, float gy, float h, float facing, Color col, bool nvg, float aimLift)
        {
            var r = h * 0.05f;
            void Cap(float ax, float ay, float bx, float by, float rr)
                => s.Fill(ArtSurface.CapsulePoly(x + ax * h * facing, gy + ay * h, x + bx * h * facing, gy + by * h, rr), col);

            Cap(0.02f, -0.47f, 0.07f, 0f, r);
            Cap(-0.02f, -0.47f, -0.09f, 0f, r);
            s.Fill(ArtSurface.CapsulePoly(x, gy - 0.50f * h, x, gy - 0.72f * h, h * 0.11f), col);
            s.FillCircle(x + facing * 0.012f * h, gy - 0.82f * h, h * 0.062f, col);
            s.Fill(ArtSurface.EllipsePoly(x, gy - 0.845f * h, h * 0.088f, h * 0.058f), col);
            if (nvg)
                s.Fill(ArtSurface.CapsulePoly(x + facing * 0.06f * h, gy - 0.865f * h, x + facing * 0.115f * h, gy - 0.865f * h, h * 0.02f), col);
            Cap(0.03f, -0.70f, 0.09f, -0.58f, h * 0.036f);
            Cap(0.09f, -0.58f, 0.24f, -0.64f - aimLift, h * 0.034f);
            Cap(-0.06f, -0.60f, 0.40f, -0.66f - aimLift, h * 0.019f);
            Cap(0.14f, -0.60f, 0.15f, -0.52f, h * 0.014f);
            return new Vector2(x + facing * 0.42f * h, gy + (-0.66f - aimLift) * h);
        }

        private static Vector2 McOperatorLit(ArtSurface s, float x, float gy, float h, float facing, Color col, bool nvg, float aimLift, float rimAlpha)
        {
            if (rimAlpha > 0f)
                McOperator(s, x - 1.7f * h / 150f, gy - 1.7f * h / 150f, h, facing, McA(McRed, rimAlpha), nvg, aimLift);
            return McOperator(s, x, gy, h, facing, col, nvg, aimLift);
        }

        private static void McMuzzleFlash(ArtSurface s, Vector2 p, float size, float facing)
        {
            s.Glow(p.x, p.y, size * 3f, McRed, 0.9f);
            s.Glow(p.x, p.y, size * 1.3f, new Color(1f, 0.85f, 0.7f), 0.9f);
            s.Fill(new[]
            {
                new Vector2(p.x, p.y - size * 0.35f), new Vector2(p.x + facing * size * 1.8f, p.y), new Vector2(p.x, p.y + size * 0.35f)
            }, new Color(1f, 0.92f, 0.8f, 0.95f));
        }

        private static void McSandbags(ArtSurface s, float x0, float x1, float baseY, int rows, float bagW, float bagH, Color col, float rimAlpha, int seed)
        {
            for (var r = 0; r < rows; r++)
            {
                var y = baseY - (r + 1) * bagH * 0.86f;
                var off = (r & 1) == 0 ? 0f : bagW * 0.5f;
                for (var x = x0 - off; x < x1; x += bagW * 0.96f)
                {
                    var j = McHash(seed + r * 977 + (int)(x / bagW)) * 0.12f;
                    var bag = ArtSurface.RoundRectPoly(x, y, bagW, bagH, bagH * 0.45f);
                    var shade = 0.82f + j;
                    var c = new Color(col.r * shade, col.g * shade, col.b * shade, 1f);
                    if (r == rows - 1 && rimAlpha > 0f)
                    {
                        var up = new Vector2[bag.Length];
                        for (var i = 0; i < bag.Length; i++)
                            up[i] = bag[i] + new Vector2(0f, -1.6f);
                        s.Fill(up, McA(McRed, rimAlpha));
                    }

                    s.Fill(bag, c);
                    s.Fill(bag, (px, py) => new Color(0f, 0f, 0f, Mathf.Clamp01((py - y) / bagH) * 0.28f));
                }
            }
        }

        private static void McStars(ArtSurface s, float W, float H, float maxY01, int count, int seed, float alpha)
        {
            for (var i = 0; i < count; i++)
            {
                var x = McHash(seed + i * 7) * W;
                var y = McHash(seed + i * 13 + 5) * H * maxY01;
                var b = 0.35f + McHash(seed + i * 29) * 0.65f;
                var r = 0.5f + McHash(seed + i * 41) * 0.9f;
                s.FillCircle(x, y, r, new Color(0.85f, 0.88f, 1f, alpha * b));
            }
        }

        private static void McFinish(ArtSurface s, float W, float H, int index)
        {
            // Alt kararma (metin okunurluğu).
            var y0 = Mathf.RoundToInt(H * 0.55f);
            for (var y = y0; y < s.Height; y++)
            {
                var t = (y - y0) / (float)(s.Height - y0);
                var a = Mathf.SmoothStep(0f, 0.72f, t);
                for (var x = 0; x < s.Width; x++)
                    s.Over(x, y, new Color(0.02f, 0.02f, 0.025f, 1f), a);
            }

            // Vinyet + deterministik tane.
            for (var y = 0; y < s.Height; y++)
            for (var x = 0; x < s.Width; x++)
            {
                var nx = x / (W - 1f) * 2f - 1f;
                var ny = y / (H - 1f) * 2f - 1f;
                var v = Mathf.Clamp01(nx * nx * 0.65f + ny * ny * 0.75f - 0.32f) * 0.62f;
                var g = (McHash(x * 7919 + y * 104729 + index * 31) - 0.5f) * 0.035f;
                var i = y * s.Width + x;
                var c = s.Pixels[i];
                var k = 1f - v;
                s.Pixels[i] = new Color(Mathf.Clamp01(c.r * k + g), Mathf.Clamp01(c.g * k + g), Mathf.Clamp01(c.b * k + g), 1f);
            }

            // Kırmızı çerçeve: ince iç hat + köşe ayraçları + alt kenar ışığı.
            var m = Mathf.Max(6f, H * 0.03f);
            var thin = Mathf.Max(1.2f, H * 0.004f);
            var frame = McA(McRed, 0.55f);
            s.FillPaths(new[]
            {
                new[] { new Vector2(m, m), new Vector2(W - m, m), new Vector2(W - m, H - m), new Vector2(m, H - m) },
                new[] { new Vector2(m + thin, m + thin), new Vector2(W - m - thin, m + thin), new Vector2(W - m - thin, H - m - thin), new Vector2(m + thin, H - m - thin) }
            }, (x, y) => frame);

            var len = H * 0.1f;
            var th = Mathf.Max(2.5f, H * 0.009f);
            var bracket = McRed;
            void Corner(float cx, float cy, float dx, float dy)
            {
                s.Fill(new[] { new Vector2(cx, cy), new Vector2(cx + dx * len, cy), new Vector2(cx + dx * len, cy + dy * th), new Vector2(cx, cy + dy * th) }, bracket);
                s.Fill(new[] { new Vector2(cx, cy), new Vector2(cx + dx * th, cy), new Vector2(cx + dx * th, cy + dy * len), new Vector2(cx, cy + dy * len) }, bracket);
            }

            Corner(m, m, 1f, 1f);
            Corner(W - m, m, -1f, 1f);
            Corner(m, H - m, 1f, -1f);
            Corner(W - m, H - m, -1f, -1f);

            var edge = Mathf.Max(2f, H * 0.008f);
            for (var x = 0; x < s.Width; x++)
            {
                var a = Mathf.Sin(x / (W - 1f) * Mathf.PI);
                for (var k = 0; k < 3; k++)
                    s.Over(x, s.Height - 1 - k, McRed, a * (1f - k / 3f) * 0.9f);
            }

            _ = edge;
        }

        // ================================================================== 0 — Battle Royale: paraşütçüler + vadi

        private static void McBattleRoyale(ArtSurface s, float W, float H)
        {
            s.VerticalGradient(new Color(0.02f, 0.022f, 0.032f), new Color(0.40f, 0.11f, 0.085f), 0, (int)(H * 0.62f));
            McStars(s, W, H, 0.4f, 70, 11, 0.7f);
            // Kırmızı güneş + ışıma.
            s.Glow(W * 0.5f, H * 0.56f, W * 0.55f, McRed, 0.55f, 0.7f);
            s.FillCircle(W * 0.5f, H * 0.5f, H * 0.075f, (x, y) => Color.Lerp(new Color(1f, 0.62f, 0.42f), McRed, Mathf.Clamp01((y - H * 0.43f) / (H * 0.15f))));
            s.Glow(W * 0.5f, H * 0.5f, H * 0.28f, McRedLight, 0.5f);

            // Vadi: V biçimli katmanlar (uzaktan yakına).
            var haze = new Color(0.32f, 0.10f, 0.09f);
            for (var layer = 0; layer < 4; layer++)
            {
                var l = layer;
                var baseY = 0.52f + l * 0.085f;
                var depth = l / 3f;
                var top = Color.Lerp(new Color(0.16f, 0.07f, 0.07f), new Color(0.03f, 0.033f, 0.04f), depth);
                var bot = Color.Lerp(top, McInk, 0.5f);
                McRidge(s, W, H, u =>
                {
                    var v = Mathf.Pow(Mathf.Abs(u * 2f - 1f), 1.15f);
                    return baseY - (0.04f + 0.22f * (1f - l * 0.18f)) * v - 0.05f * McFbm(u * (5f + l * 2f), 3 + l) + 0.02f * (1f - v);
                }, top, bot, 2.2f - l * 0.4f, 0.75f - l * 0.12f);
                // Katman arası pus.
                s.Glow(W * 0.5f, H * (baseY + 0.02f), W * 0.5f, haze, 0.10f * (1f - depth), 0.25f);
            }

            // Paraşütçüler (x01, y01, ölçek).
            var jumpers = new[]
            {
                new Vector3(0.50f, 0.07f, 0.55f), new Vector3(0.16f, 0.17f, 0.62f), new Vector3(0.86f, 0.15f, 0.55f), new Vector3(0.76f, 0.40f, 0.78f),
                new Vector3(0.62f, 0.20f, 1.05f), new Vector3(0.30f, 0.34f, 1.45f), new Vector3(0.46f, 0.36f, 0.62f)
            };
            for (var i = 0; i < jumpers.Length; i++)
                McParachutist(s, jumpers[i].x * W, jumpers[i].y * H, jumpers[i].z * H / 400f, i);

            // Ön plan çam ormanı (kenarlarda çerçeve).
            for (var i = 0; i < 16; i++)
            {
                var side = i < 8 ? 0 : 1;
                var k = i % 8;
                var x = side == 0 ? W * (0.0f + k * 0.045f) + McHash(i * 5) * 14f : W * (1f - k * 0.045f) - McHash(i * 5) * 14f;
                var h = H * (0.17f + 0.2f * McHash(i * 17 + 3)) * (1f - k * 0.045f);
                McTree(s, x, H * (0.98f + 0.02f * McHash(i)), h, McInk, 0.55f);
            }

            McRidge(s, W, H, u => 0.95f + 0.02f * McNoise(u * 20f, 4), McInk, McInk, 0f, 0f);
        }

        private static void McParachutist(ArtSurface s, float cx, float cy, float sc, int seed)
        {
            var rx = 68f * sc;
            var ry = 34f * sc;
            const int segs = 5;
            const int arcN = 14;
            var charcoal = new Color(0.07f, 0.075f, 0.085f, 1f);
            var redPanel = new Color(0.46f, 0.12f, 0.10f, 1f);

            Vector2 Arc(float theta, float sag = 0f) => new Vector2(cx + Mathf.Cos(theta) * rx, cy - Mathf.Sin(theta) * ry + sag);

            // Dış gölge + panel dolguları.
            var outline = new List<Vector2>();
            for (var i = 0; i <= arcN * segs; i++)
                outline.Add(Arc(Mathf.PI - Mathf.PI * i / (arcN * segs)));
            // Alt kenar: kıvrımlı (segs adet çukur).
            for (var i = segs - 1; i >= 0; i--)
            {
                var xa = cx + rx - (rx * 2f) * (segs - 1 - i) / segs;
                var xb = cx + rx - (rx * 2f) * (segs - i) / segs;
                outline.Add(new Vector2((xa + xb) * 0.5f, cy + 7f * sc));
                outline.Add(new Vector2(xb, cy + 1f * sc));
            }

            var poly = outline.ToArray();
            var rim = new Vector2[poly.Length];
            for (var i = 0; i < poly.Length; i++)
                rim[i] = poly[i] + new Vector2(-1.4f, -1.8f);
            s.Fill(rim, McA(McRed, 0.9f));
            s.Fill(poly, charcoal);

            for (var p = 0; p < segs; p += 2)
            {
                var t0 = Mathf.PI - Mathf.PI * p / segs;
                var t1 = Mathf.PI - Mathf.PI * (p + 1) / segs;
                var panel = new List<Vector2>();
                for (var i = 0; i <= arcN; i++)
                    panel.Add(Arc(Mathf.Lerp(t0, t1, i / (float)arcN)));
                panel.Add(new Vector2(cx + Mathf.Cos(t1) * rx, cy + 1f * sc));
                panel.Add(new Vector2(cx + Mathf.Cos(t0) * rx, cy + 1f * sc));
                s.Fill(panel.ToArray(), (x, y) => Color.Lerp(redPanel, charcoal, Mathf.Clamp01((y - (cy - ry)) / (ry * 1.1f)) * 0.55f));
            }

            // Askı hatları ve asılı asker.
            var sx = cx + (McHash(seed * 3) - 0.5f) * 6f * sc;
            var sy = cy + 78f * sc;
            var line = new Color(0.8f, 0.8f, 0.82f, 0.45f);
            var lw = Mathf.Max(0.8f, 1.1f * sc);
            for (var i = 0; i <= segs; i++)
            {
                var th = Mathf.PI - Mathf.PI * i / segs;
                var ax = cx + Mathf.Cos(th) * rx;
                s.Line(ax, cy + 2f * sc, sx + (ax - cx) * 0.06f, sy - 14f * sc, lw, line);
            }

            var body = McInk;
            s.Fill(ArtSurface.CapsulePoly(sx, sy - 13f * sc, sx, sy + 2f * sc, 6f * sc), body);
            s.FillCircle(sx, sy - 19f * sc, 4.6f * sc, body);
            s.Fill(ArtSurface.EllipsePoly(sx, sy - 21f * sc, 6f * sc, 4f * sc), body);
            s.Fill(ArtSurface.CapsulePoly(sx - 2.5f * sc, sy + 2f * sc, sx - 3f * sc, sy + 26f * sc, 3.2f * sc), body);
            s.Fill(ArtSurface.CapsulePoly(sx + 2.5f * sc, sy + 2f * sc, sx + 3.5f * sc, sy + 25f * sc, 3.2f * sc), body);
            s.Fill(ArtSurface.CapsulePoly(sx - 5f * sc, sy - 12f * sc, sx - 5.5f * sc, sy - 29f * sc, 1.6f * sc), body);
            s.Fill(ArtSurface.CapsulePoly(sx + 5f * sc, sy - 12f * sc, sx + 5.5f * sc, sy - 29f * sc, 1.6f * sc), body);
            // Kırmızı kenar ışığı (omuz).
            s.Line(sx - 5.8f * sc, sy - 14f * sc, sx - 5.8f * sc, sy + 0f, Mathf.Max(0.8f, 1.2f * sc), McA(McRed, 0.85f));
        }

        // ================================================================== 1 — Çatışma: karşılıklı siperler

        private static void McSkirmish(ArtSurface s, float W, float H)
        {
            s.VerticalGradient(new Color(0.03f, 0.03f, 0.036f), new Color(0.36f, 0.10f, 0.07f), 0, (int)(H * 0.62f));
            // Duman sütunları + patlama ışıması.
            for (var i = 0; i < 9; i++)
            {
                var x = W * (0.12f + i * 0.095f) + McHash(i * 3) * 20f;
                var rr = H * (0.07f + 0.08f * McHash(i * 7));
                for (var k = 0; k < 6; k++)
                    s.FillCircle(x + McHash(i * 11 + k) * 22f - 11f, H * (0.5f - k * 0.055f * (0.7f + McHash(i))), rr * (1f - k * 0.08f), new Color(0.06f, 0.05f, 0.055f, 0.22f));
            }

            s.Glow(W * 0.5f, H * 0.56f, W * 0.55f, McRed, 0.85f, 0.55f);
            s.Glow(W * 0.5f, H * 0.58f, W * 0.2f, new Color(1f, 0.6f, 0.35f), 0.55f, 0.5f);

            // Uzak ufuk çizgisi + yıkık siluetler.
            McRidge(s, W, H, u => 0.56f - 0.025f * McFbm(u * 9f, 21) - (Mathf.Abs(u - 0.5f) < 0.04f ? 0.02f : 0f), new Color(0.07f, 0.04f, 0.04f), McInk, 2f, 0.7f);
            McRuin(s, W * 0.31f, H * 0.57f, H * 0.11f, 1f);
            McRuin(s, W * 0.7f, H * 0.57f, H * 0.14f, -1f);
            McRuin(s, W * 0.57f, H * 0.575f, H * 0.07f, 1f);

            // Kimsesiz arazi.
            McRidge(s, W, H, u => 0.62f + 0.01f * McNoise(u * 14f, 8), new Color(0.07f, 0.05f, 0.05f), McInk, 1.8f, 0.6f);
            for (var i = 0; i < 5; i++)
            {
                var x = W * (0.36f + 0.07f * i);
                var y = H * (0.67f + 0.012f * (i % 3));
                s.Fill(ArtSurface.EllipsePoly(x, y, 30f * (0.8f + McHash(i) * 0.5f), 5f), new Color(0.02f, 0.02f, 0.025f, 0.9f));
            }

            // Dikenli tel.
            var wire = new Color(0.02f, 0.02f, 0.024f, 1f);
            for (var i = 0; i < 9; i++)
            {
                var x = W * (0.355f + i * 0.036f);
                var y = H * 0.665f;
                s.Line(x, y, x, y + H * 0.05f, 2f, wire);
                s.Line(x, y + H * 0.01f, x + W * 0.036f, y + H * 0.02f, 1.1f, wire);
                s.Line(x, y + H * 0.02f, x + W * 0.036f, y + H * 0.01f, 1.1f, wire);
            }

            // İz mermileri (kırmızı).
            for (var i = 0; i < 7; i++)
            {
                var fromLeft = (i & 1) == 0;
                var y0 = H * (0.66f + 0.05f * McHash(i * 5));
                var y1 = H * (0.60f + 0.10f * McHash(i * 9 + 1));
                var xa = fromLeft ? W * 0.30f : W * 0.70f;
                var xb = fromLeft ? W * 0.62f : W * 0.38f;
                var dir = fromLeft ? 1f : -1f;
                s.Beam(xa, y0, xb, y1, 1.0f, 2.8f, McA(McRedLight, 0.8f));
                s.Glow(xb, y1, 7f, McRed, 0.7f);
                _ = dir;
            }

            // Siperler: sol (yakın/büyük), sağ (biraz uzak).
            McTrench(s, W, H, true, 0.0f, 0.43f, 0.84f, 1.0f);
            McTrench(s, W, H, false, 0.57f, 1.0f, 0.80f, 0.86f);

            // Ön alt şerit: toprak/sandık.
            McRidge(s, W, H, u => 0.965f, McInk, McInk, 0f, 0f);
        }

        private static void McRuin(ArtSurface s, float x, float gy, float h, float lean)
        {
            var c = new Color(0.025f, 0.022f, 0.026f, 1f);
            var pts = new[]
            {
                new Vector2(x - h * 0.45f, gy), new Vector2(x - h * 0.45f, gy - h * 0.55f), new Vector2(x - h * 0.28f, gy - h * 0.8f), new Vector2(x - h * 0.1f, gy - h * 0.62f),
                new Vector2(x + h * 0.05f, gy - h), new Vector2(x + h * 0.22f, gy - h * 0.7f * lean), new Vector2(x + h * 0.45f, gy - h * 0.35f), new Vector2(x + h * 0.45f, gy)
            };
            var rim = new Vector2[pts.Length];
            for (var i = 0; i < pts.Length; i++)
                rim[i] = pts[i] + new Vector2(-1.2f, -1.8f);
            s.Fill(rim, McA(McRed, 0.6f));
            s.Fill(pts, c);
        }

        private static void McTrench(ArtSurface s, float W, float H, bool left, float x0n, float x1n, float baseN, float scaleN)
        {
            var x0 = x0n * W;
            var x1 = x1n * W;
            var baseY = baseN * H;
            var facing = left ? 1f : -1f;
            var figH = H * 0.30f * scaleN;
            var earth = new Color(0.045f, 0.035f, 0.035f, 1f);

            // Siper gerisi toprak yığını.
            var span = x1 - x0;
            McRidge(s, W, H, u =>
            {
                var rel = (u * W - x0) / span;
                if (rel < 0f || rel > 1f)
                    return 2f;
                var e = left ? rel : 1f - rel;
                return (baseY - H * 0.1f * scaleN) / H - 0.03f * Mathf.Sin(e * Mathf.PI * 0.9f) * 0f;
            }, earth, earth, 0f, 0f);

            // Askerler (siper arkasından görünür).
            var count = 3;
            for (var i = 0; i < count; i++)
            {
                var t = (i + 0.5f) / count;
                var x = left ? Mathf.Lerp(x0 + span * 0.38f, x1 - span * 0.05f, t) : Mathf.Lerp(x0 + span * 0.05f, x1 - span * 0.38f, t);
                var gy = baseY + H * 0.01f;
                var muzzle = McOperatorLit(s, x, gy, figH * (0.92f + 0.1f * McHash(i * 5 + (left ? 0 : 99))), facing, McInk, i == 1, 0.02f * i, 0.8f);
                if (i != 1)
                    McMuzzleFlash(s, muzzle, H * 0.016f, facing);
            }

            // Kum torbası parapeti (soldiyer gövdesini kısmen örter).
            var bagH = H * 0.045f * scaleN;
            McSandbags(s, x0 - 20f, x1 + 20f, baseY + bagH * 1.3f, 3, bagH * 2.2f, bagH, new Color(0.10f, 0.085f, 0.08f), 0.85f, left ? 5 : 77);
        }

        // ================================================================== 2 — Konvoy: Kirpi kolonu

        private static void McConvoy(ArtSurface s, float W, float H)
        {
            s.VerticalGradient(new Color(0.025f, 0.03f, 0.04f), new Color(0.46f, 0.13f, 0.09f), 0, (int)(H * 0.6f));
            McStars(s, W, H, 0.25f, 40, 31, 0.55f);
            var sunX = W * 0.80f;
            var sunY = H * 0.5f;
            s.Glow(sunX, sunY + H * 0.04f, W * 0.5f, McRed, 0.65f, 0.6f);
            s.FillCircle(sunX, sunY, H * 0.09f, (x, y) => Color.Lerp(new Color(1f, 0.7f, 0.5f), McRed, Mathf.Clamp01((y - (sunY - H * 0.09f)) / (H * 0.18f))));
            s.Glow(sunX, sunY, H * 0.3f, McRedLight, 0.45f);

            // Dağ katmanları.
            McRidge(s, W, H, u => 0.46f - 0.17f * McFbm(u * 4f, 41) - 0.08f * Mathf.Exp(-Mathf.Pow((u - 0.35f) * 4f, 2f)), new Color(0.16f, 0.07f, 0.07f), new Color(0.06f, 0.035f, 0.04f), 2.2f, 0.7f);
            McRidge(s, W, H, u => 0.55f - 0.10f * McFbm(u * 6f + 3f, 52), new Color(0.08f, 0.05f, 0.05f), McInk, 2f, 0.6f);

            // Zemin + yol.
            var vx = W * 0.80f;
            var vy = H * 0.575f;
            McRidge(s, W, H, u => 0.575f + 0.004f * McNoise(u * 30f, 5), new Color(0.05f, 0.04f, 0.042f), McInk, 0f, 0f);
            var road = new[]
            {
                new Vector2(vx - 3f, vy), new Vector2(vx + 3f, vy), new Vector2(W * 0.50f, H + 2f), new Vector2(-W * 0.30f, H + 2f)
            };
            s.Fill(road, (x, y) => Color.Lerp(new Color(0.11f, 0.075f, 0.075f), new Color(0.035f, 0.032f, 0.036f), Mathf.Clamp01((y - vy) / (H - vy))));
            // Yol kenarı kırmızı ışıltı + orta kesik çizgi.
            s.Beam(vx, vy, -W * 0.30f, H + 2f, 1.2f, 6f, McA(McRed, 0.35f));
            s.Beam(vx, vy, W * 0.50f, H + 2f, 1.2f, 6f, McA(McRed, 0.35f));
            for (var i = 0; i < 9; i++)
            {
                var t0 = i / 9f;
                var t1 = t0 + 0.05f;
                var p0 = new Vector2(Mathf.Lerp(vx, W * 0.10f, t0 * t0), Mathf.Lerp(vy, H + 2f, t0 * t0));
                var p1 = new Vector2(Mathf.Lerp(vx, W * 0.10f, t1 * t1), Mathf.Lerp(vy, H + 2f, t1 * t1));
                s.Beam(p0.x, p0.y, p1.x, p1.y, 1f + t0 * 5f, 1f + t1 * 5f, McA(McRedLight, 0.22f + t0 * 0.2f));
            }

            // Kolon: uzaktan yakına (t = 0 yakın).
            var cols = new[] { 0.80f, 0.60f, 0.43f, 0.29f, 0.17f };
            for (var i = 0; i < cols.Length; i++)
            {
                var t = cols[i];
                var k = 1f - t;                         // 0 uzak, 1 yakın
                var px = Mathf.Lerp(vx, W * 0.16f, Mathf.Pow(k, 1.0f));
                var gy = Mathf.Lerp(vy + 4f, H * 0.93f, Mathf.Pow(k, 1.55f));
                var sc = Mathf.Lerp(0.14f, 1.12f, Mathf.Pow(k, 1.55f)) * H / 400f * 1.15f;
                var hz = Mathf.Clamp01(1f - k) * 0.6f;
                McKirpi(s, px - 100f * sc, gy, sc, Color.Lerp(McInk, new Color(0.22f, 0.09f, 0.08f), hz), hz);
            }

            McRidge(s, W, H, u => 0.975f, McInk, McInk, 0f, 0f);
        }

        /// <summary>Kirpi (MRAP) yan görünüm silüeti; yerel birim: ~210 px uzunluk, sağa gider.</summary>
        private static void McKirpi(ArtSurface s, float x, float gy, float sc, Color body, float haze)
        {
            Vector2 L(float lx, float ly) => new Vector2(x + lx * sc, gy + ly * sc);
            Vector2[] Poly(params float[] xy)
            {
                var p = new Vector2[xy.Length / 2];
                for (var i = 0; i < p.Length; i++)
                    p[i] = L(xy[i * 2], xy[i * 2 + 1]);
                return p;
            }

            // Toz bulutu (arkada).
            for (var i = 0; i < 5; i++)
                s.FillCircle(x - (12f + i * 30f) * sc, gy - (14f + i * 7f) * sc, (22f + i * 9f) * sc, new Color(0.35f, 0.14f, 0.11f, 0.11f - i * 0.015f));

            var hull = Poly(8, -30, 8, -80, 36, -96, 112, -102, 158, -96, 190, -74, 206, -54, 208, -32, 190, -28, 20, -28);
            // Kırmızı kenar ışığı (üst/arka ofset).
            var rim = new Vector2[hull.Length];
            for (var i = 0; i < hull.Length; i++)
                rim[i] = hull[i] + new Vector2(-1.5f * Mathf.Max(0.8f, sc), -1.8f * Mathf.Max(0.8f, sc));
            s.Fill(rim, McA(McRed, 0.9f - haze * 0.5f));
            s.Fill(hull, body);

            // Tekerlekler.
            foreach (var wx in new[] { 52f, 164f })
            {
                var c = L(wx, -24);
                s.FillCircle(c.x - 1.4f, c.y - 1.4f, 26f * sc, McA(McRed, 0.75f - haze * 0.4f));
                s.FillCircle(c.x, c.y, 26f * sc, new Color(0.012f, 0.012f, 0.015f, 1f));
                s.FillCircle(c.x, c.y, 11f * sc, body);
                s.Ring(c.x, c.y, 15f * sc, 13f * sc, new Color(0.3f, 0.12f, 0.1f, 0.6f));
            }

            // Çamurluk kemerleri (gövdeyi tekerlek üstünde kesen koyu).
            // Pencereler (hafif kırmızı yansıma).
            var win = Color.Lerp(new Color(0.15f, 0.04f, 0.04f), new Color(0.5f, 0.15f, 0.1f), 0.5f - haze * 0.3f);
            s.Fill(Poly(124, -90, 152, -90, 178, -68, 124, -68), win);
            s.Fill(Poly(86, -88, 116, -88, 116, -68, 86, -68), win);
            // Zırh panelleri (ince çizgiler).
            s.Line(L(20, -52).x, L(20, -52).y, L(120, -52).x, L(120, -52).y, Mathf.Max(0.7f, 1.4f * sc), new Color(0f, 0f, 0f, 0.5f));
            s.Line(L(80, -96).x, L(80, -96).y, L(80, -34).x, L(80, -34).y, Mathf.Max(0.7f, 1.2f * sc), new Color(0f, 0f, 0f, 0.45f));

            // Tareta + makineli tüfek.
            var tur = Poly(60, -102, 96, -102, 96, -122, 60, -122);
            s.Fill(tur, body);
            s.Fill(new[] { tur[3] + new Vector2(-1.4f, -1.6f), tur[2] + new Vector2(-1.4f, -1.6f), tur[2], tur[3] }, McA(McRed, 0.85f - haze * 0.4f));
            s.Fill(ArtSurface.CapsulePoly(L(96, -114).x, L(96, -114).y, L(150, -118).x, L(150, -118).y, 3.2f * sc), body);
            // Anten.
            s.Line(L(24, -90).x, L(24, -90).y, L(14, -170).x, L(14, -170).y, Mathf.Max(0.8f, 1.6f * sc), body);
            // Farlar (ön) ve stop (arka).
            var hl = L(207, -50);
            s.Glow(hl.x, hl.y, 22f * sc, new Color(1f, 0.82f, 0.65f), 0.85f);
            s.Glow(hl.x + 40f * sc, hl.y + 6f * sc, 70f * sc, McRedLight, 0.28f, 0.35f);
            var tl = L(8, -52);
            s.Glow(tl.x, tl.y, 16f * sc, McRed, 0.95f);
        }

        // ================================================================== 3 — Rehine: gece ev baskını

        private static void McHostage(ArtSurface s, float W, float H)
        {
            s.VerticalGradient(new Color(0.012f, 0.014f, 0.024f), new Color(0.07f, 0.045f, 0.06f), 0, (int)(H * 0.7f));
            McStars(s, W, H, 0.45f, 90, 71, 0.8f);
            // Ay (bone) + soğuk ışıma.
            s.Glow(W * 0.17f, H * 0.17f, H * 0.35f, new Color(0.45f, 0.5f, 0.75f), 0.35f);
            s.FillCircle(W * 0.17f, H * 0.17f, H * 0.045f, new Color(0.92f, 0.92f, 0.88f));
            s.FillCircle(W * 0.17f + H * 0.014f, H * 0.17f - H * 0.008f, H * 0.04f, new Color(0.04f, 0.045f, 0.07f));

            // Uzak ağaç hattı.
            McRidge(s, W, H, u => 0.58f - 0.05f * McFbm(u * 12f, 61) - 0.02f, new Color(0.04f, 0.035f, 0.05f), McInk, 1.6f, 0.45f);
            McRidge(s, W, H, u => 0.66f + 0.012f * McNoise(u * 16f, 12), new Color(0.03f, 0.03f, 0.036f), McInk, 0f, 0f);

            // Ev.
            var hx0 = W * 0.42f;
            var hx1 = W * 0.90f;
            var hyTop = H * 0.34f;
            var hyBot = H * 0.70f;
            var wall = new Color(0.025f, 0.025f, 0.032f, 1f);
            var roof = new[]
            {
                new Vector2(hx0 - W * 0.025f, hyTop + H * 0.01f), new Vector2((hx0 + hx1) * 0.5f, hyTop - H * 0.15f), new Vector2(hx1 + W * 0.025f, hyTop + H * 0.01f)
            };
            var body = new[] { new Vector2(hx0, hyTop), new Vector2(hx1, hyTop), new Vector2(hx1, hyBot), new Vector2(hx0, hyBot) };
            s.Fill(new[] { roof[0] + new Vector2(-1.5f, -1.8f), roof[1] + new Vector2(-1.5f, -2.2f), roof[2] + new Vector2(1.5f, -1.8f) }, McA(McRed, 0.85f));
            s.Fill(roof, new Color(0.02f, 0.02f, 0.026f, 1f));
            s.Fill(body, (x, y) => Color.Lerp(new Color(0.045f, 0.04f, 0.05f), wall, Mathf.Clamp01((y - hyTop) / (hyBot - hyTop))));
            s.Fill(new[] { new Vector2(hx0 + W * 0.14f, hyTop - H * 0.1f), new Vector2(hx0 + W * 0.17f, hyTop - H * 0.1f), new Vector2(hx0 + W * 0.17f, hyTop - H * 0.03f), new Vector2(hx0 + W * 0.14f, hyTop - H * 0.03f) }, wall);
            s.Line(hx0, hyTop, hx1, hyTop, 2f, McA(McRed, 0.45f));

            // Pencereler: sıcak ışık; biri rehine silueti.
            var winW = W * 0.07f;
            var winH = H * 0.1f;
            var wins = new[]
            {
                new Vector2(hx0 + W * 0.12f, hyTop + H * 0.05f), new Vector2(hx0 + W * 0.26f, hyTop + H * 0.05f), new Vector2(hx0 + W * 0.38f, hyTop + H * 0.05f),
                new Vector2(hx0 + W * 0.26f, hyTop + H * 0.20f), new Vector2(hx0 + W * 0.38f, hyTop + H * 0.20f)
            };
            var lit = new[] { true, false, true, true, false };
            for (var i = 0; i < wins.Length; i++)
            {
                var w = wins[i];
                var on = lit[i];
                var p0 = w;
                s.Fill(new[] { p0, p0 + new Vector2(winW, 0), p0 + new Vector2(winW, winH), p0 + new Vector2(0, winH) },
                    (x, y) => on ? Color.Lerp(new Color(1f, 0.74f, 0.38f), new Color(0.78f, 0.34f, 0.16f), Mathf.Clamp01((y - w.y) / winH)) : new Color(0.06f, 0.07f, 0.1f));
                if (on)
                    s.Glow(w.x + winW * 0.5f, w.y + winH * 0.5f, winW * 2.4f, new Color(1f, 0.55f, 0.25f), 0.38f);
                // Çerçeve çubukları
                s.Line(w.x + winW * 0.5f, w.y, w.x + winW * 0.5f, w.y + winH, 1.6f, wall);
                s.Line(w.x, w.y + winH * 0.5f, w.x + winW, w.y + winH * 0.5f, 1.6f, wall);
            }

            // Rehine (sandalyede oturan siluet, ikinci pencere solu).
            var hw = wins[3];
            s.FillCircle(hw.x + winW * 0.5f, hw.y + winH * 0.46f, winH * 0.16f, wall);
            s.Fill(ArtSurface.CapsulePoly(hw.x + winW * 0.5f, hw.y + winH * 0.62f, hw.x + winW * 0.5f, hw.y + winH * 0.95f, winH * 0.2f), wall);

            // Kapı (açık, içerisi kırmızı loş).
            var dx = hx0 + W * 0.035f;
            var dw = W * 0.065f;
            var dTop = hyBot - H * 0.19f;
            s.Fill(new[] { new Vector2(dx, dTop), new Vector2(dx + dw, dTop), new Vector2(dx + dw, hyBot), new Vector2(dx, hyBot) },
                (x, y) => Color.Lerp(new Color(0.42f, 0.09f, 0.06f), new Color(0.12f, 0.025f, 0.02f), Mathf.Clamp01((y - dTop) / (hyBot - dTop))));
            s.Glow(dx + dw * 0.5f, hyBot - H * 0.07f, dw * 2.4f, McRed, 0.5f);

            // Ön zemin + patika.
            McRidge(s, W, H, u => 0.70f, new Color(0.035f, 0.03f, 0.04f), McInk, 0f, 0f);
            s.Fill(new[] { new Vector2(dx - 4f, hyBot), new Vector2(dx + dw + 4f, hyBot), new Vector2(W * 0.30f, H + 2f), new Vector2(-W * 0.10f, H + 2f) },
                (x, y) => Color.Lerp(new Color(0.09f, 0.05f, 0.055f), new Color(0.03f, 0.028f, 0.034f), Mathf.Clamp01((y - hyBot) / (H - hyBot))));

            // Ağaçlar (sağ ön).
            McTree(s, W * 0.96f, H * 0.80f, H * 0.42f, McInk, 0.5f);
            McTree(s, W * 0.03f, H * 0.78f, H * 0.34f, McInk, 0.5f);

            // Operatörler: kapıya doğru dizilmiş; yakından uzağa küçülür.
            var ops = new[] { new Vector3(0.09f, 0.97f, 0.62f), new Vector3(0.20f, 0.90f, 0.52f), new Vector3(0.30f, 0.84f, 0.43f), new Vector3(0.375f, 0.795f, 0.36f) };
            var doorLaser = new Vector2(dx + dw * 0.5f, hyBot - H * 0.07f);
            for (var i = ops.Length - 1; i >= 0; i--)
            {
                var o = ops[i];
                var figH = H * o.z * 0.62f;
                var mz = McOperatorLit(s, o.x * W, o.y * H, figH, 1f, McInk, true, 0.01f, 0.85f);
                if (i != 3)
                {
                    // Lazer: namludan kapıya/pencereye.
                    var target = i == 0 ? new Vector2(wins[0].x + winW * 0.5f, wins[0].y + winH * 0.55f) : doorLaser;
                    s.Beam(mz.x, mz.y, target.x, target.y, 0.9f, 0.9f, McA(McRedLight, 0.5f));
                    s.Glow(target.x, target.y, 8f, McRedLight, 0.85f);
                }
            }

            // Alçak sis.
            s.Glow(W * 0.5f, H * 0.78f, W * 0.7f, new Color(0.3f, 0.12f, 0.12f), 0.09f, 0.18f);
        }

        // ================================================================== 4 — Poligon: hedef şeritleri

        private static void McRange(ArtSurface s, float W, float H)
        {
            s.VerticalGradient(new Color(0.02f, 0.022f, 0.028f), new Color(0.22f, 0.07f, 0.06f), 0, (int)(H * 0.58f));
            s.Glow(W * 0.5f, H * 0.52f, W * 0.55f, McRed, 0.55f, 0.55f);
            McRidge(s, W, H, u => 0.50f - 0.06f * McFbm(u * 5f, 91), new Color(0.07f, 0.04f, 0.045f), McInk, 2f, 0.65f);
            McRidge(s, W, H, u => 0.58f, new Color(0.06f, 0.05f, 0.05f), McInk, 0f, 0f);

            var vx = W * 0.5f;
            var vy = H * 0.58f;
            // Şerit çizgileri (perspektif).
            for (var i = -3; i <= 3; i++)
            {
                var bx = vx + i * W * 0.32f;
                s.Beam(vx + i * 4f, vy, bx, H + 2f, 1f, 4f + Mathf.Abs(i) * 0.4f, McA(McRedLight, i == 0 ? 0.0f : 0.28f));
            }

            // Hedefler: (x01, ölçek).
            var targets = new[] { new Vector2(0.26f, 0.65f), new Vector2(0.74f, 0.65f), new Vector2(0.5f, 1.0f) };
            for (var i = 0; i < targets.Length; i++)
            {
                var x = targets[i].x * W;
                var sc = targets[i].y;
                var gy = Mathf.Lerp(vy + 2f, H * 0.80f, sc);
                var r = H * 0.17f * sc;
                var cy = gy - r * 1.55f;
                s.Fill(new[] { new Vector2(x - r * 0.07f, gy), new Vector2(x + r * 0.07f, gy), new Vector2(x + r * 0.07f, cy), new Vector2(x - r * 0.07f, cy) }, McInk);
                s.FillCircle(x - 1.5f, cy - 1.8f, r * 1.02f, McA(McRed, 0.9f));
                s.FillCircle(x, cy, r, new Color(0.84f, 0.83f, 0.8f));
                s.Ring(x, cy, r * 0.82f, r * 0.74f, McInk);
                s.Ring(x, cy, r * 0.56f, r * 0.49f, McInk);
                s.FillCircle(x, cy, r * 0.42f, McRed);
                s.FillCircle(x, cy, r * 0.17f, McInk);
                for (var k = 0; k < 5; k++)
                {
                    var a = McHash(i * 31 + k) * Mathf.PI * 2f;
                    var d = r * 0.35f * McHash(i * 17 + k * 5);
                    s.FillCircle(x + Mathf.Cos(a) * d, cy + Mathf.Sin(a) * d, Mathf.Max(1.2f, r * 0.03f), new Color(0.03f, 0.03f, 0.035f));
                }
            }

            // Nişangâh halkası (hafif).
            s.Ring(W * 0.5f, H * 0.5f, H * 0.34f, H * 0.335f, McA(McRed, 0.55f));
            s.Beam(W * 0.5f - H * 0.44f, H * 0.5f, W * 0.5f - H * 0.2f, H * 0.5f, 1.4f, 1.4f, McA(McRed, 0.6f));
            s.Beam(W * 0.5f + H * 0.2f, H * 0.5f, W * 0.5f + H * 0.44f, H * 0.5f, 1.4f, 1.4f, McA(McRed, 0.6f));

            // Ön tezgâh + çanta.
            McRidge(s, W, H, u => 0.915f - 0.01f * Mathf.Sin(u * 12f), new Color(0.05f, 0.05f, 0.055f), McInk, 2f, 0.7f);
        }
    }
}
