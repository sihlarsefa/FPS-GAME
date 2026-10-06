using UnityEngine;

namespace Project.Infrastructure.Weapons
{
    /// <summary>Procedural dürbün dokuları: retikül, vinyet, göz kutusu halkası, lens kiri. Harici asset gerekmez.</summary>
    public static class ScopeTextures
    {
        private static Texture2D _vignette;
        private static Texture2D _eyeBox;
        private static Texture2D _dirt;
        private static readonly Texture2D[] Reticles = new Texture2D[4];

        public static Texture2D Reticle(ReticleKind kind)
        {
            var i = (int)kind;
            if (i < 0 || i >= Reticles.Length || kind == ReticleKind.None)
                return null;
            if (Reticles[i] == null)
                Reticles[i] = Make(RenderReticle(kind, 256), 256);
            return Reticles[i];
        }

        public static Texture2D Vignette() => _vignette != null ? _vignette : _vignette = Make(RenderVignette(256, 0.44f, 0.03f), 256);

        public static Texture2D EyeBox() => _eyeBox != null ? _eyeBox : _eyeBox = Make(RenderVignette(256, 0.30f, 0.30f), 256);

        public static Texture2D Dirt() => _dirt != null ? _dirt : _dirt = Make(RenderDirt(128), 128);

        private static Texture2D Make(Color32[] px, int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "ScopeTex",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            t.SetPixels32(px);
            t.Apply(false, true);
            return t;
        }

        /// <summary>Dışı karanlık, içi şeffaf daire. radius = ekran yarıçapı oranı (0..0.5), soft = kenar yumuşaklığı.</summary>
        public static Color32[] RenderVignette(int size, float radius, float soft)
        {
            var px = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x + 0.5f) / size - 0.5f;
                    var dy = (y + 0.5f) / size - 0.5f;
                    var d = Mathf.Sqrt(dx * dx + dy * dy);
                    var a = Mathf.Clamp01((d - (radius - soft)) / Mathf.Max(1e-4f, soft * 2f));
                    px[y * size + x] = new Color32(0, 0, 0, (byte)Mathf.RoundToInt(a * 255f));
                }
            }

            return px;
        }

        public static Color32[] RenderDirt(int size)
        {
            var px = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var n = Mathf.PerlinNoise(x * 0.11f + 7.3f, y * 0.11f + 1.9f) * 0.6f
                            + Mathf.PerlinNoise(x * 0.37f, y * 0.37f) * 0.4f;
                    var a = Mathf.Clamp01((n - 0.55f) * 2.2f) * 0.35f;
                    // Lens kenarında daha fazla kir/parıltı.
                    var dx = (x + 0.5f) / size - 0.5f;
                    var dy = (y + 0.5f) / size - 0.5f;
                    a += Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) * 2f - 0.6f) * 0.15f;
                    px[y * size + x] = new Color32(200, 205, 210, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            }

            return px;
        }

        /// <summary>Retikül pikselleri (şeffaf zemin). Merkez her zaman çizilir.</summary>
        public static Color32[] RenderReticle(ReticleKind kind, int size)
        {
            var px = new Color32[size * size];
            var red = new Color32(255, 40, 30, 255);
            var black = new Color32(8, 8, 8, 255);
            var c = size / 2;
            switch (kind)
            {
                case ReticleKind.RedDot:
                    Disc(px, size, c, c, size * 0.018f, red);
                    break;
                case ReticleKind.AcogChevron:
                    // Ters V (chevron) + kısa iç çizgi.
                    for (var i = 0; i < size * 0.10f; i++)
                    {
                        Dot(px, size, c - i, c - (int)(size * 0.02f) + i, 1.4f, red);
                        Dot(px, size, c + i, c - (int)(size * 0.02f) + i, 1.4f, red);
                    }

                    Disc(px, size, c, c + (int)(size * 0.02f), 2f, red);
                    // Alt menzil işaretleri.
                    for (var m = 1; m <= 3; m++)
                        Line(px, size, c - 6, c - (int)(size * 0.13f) - m * 14, c + 6, c - (int)(size * 0.13f) - m * 14, 1f, red);
                    break;
                case ReticleKind.MilDot:
                    // İnce artı + mil noktaları + kalın dış çubuklar.
                    Line(px, size, 4, c, size - 5, c, 0.8f, black);
                    Line(px, size, c, 4, c, size - 5, 0.8f, black);
                    Line(px, size, 4, c, c - (int)(size * 0.20f), c, 2.2f, black);
                    Line(px, size, c + (int)(size * 0.20f), c, size - 5, c, 2.2f, black);
                    Line(px, size, c, 4, c, c - (int)(size * 0.20f), 2.2f, black);
                    Line(px, size, c, c + (int)(size * 0.20f), c, size - 5, 2.2f, black);
                    for (var m = -4; m <= 4; m++)
                    {
                        if (m == 0)
                            continue;
                        var off = (int)(m * size * 0.045f);
                        Disc(px, size, c + off, c, 2.2f, black);
                        Disc(px, size, c, c + off, 2.2f, black);
                    }

                    break;
            }

            return px;
        }

        private static void Disc(Color32[] px, int size, int cx, int cy, float r, Color32 col)
        {
            var ir = Mathf.CeilToInt(r) + 1;
            for (var y = cy - ir; y <= cy + ir; y++)
            {
                for (var x = cx - ir; x <= cx + ir; x++)
                {
                    if (x < 0 || y < 0 || x >= size || y >= size)
                        continue;
                    var d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    if (d <= r)
                        px[y * size + x] = col;
                }
            }
        }

        private static void Dot(Color32[] px, int size, int x, int y, float r, Color32 col) => Disc(px, size, x, y, r, col);

        private static void Line(Color32[] px, int size, int x0, int y0, int x1, int y1, float thickness, Color32 col)
        {
            var steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0), 1);
            for (var i = 0; i <= steps; i++)
            {
                var t = i / (float)steps;
                Disc(px, size, Mathf.RoundToInt(Mathf.Lerp(x0, x1, t)), Mathf.RoundToInt(Mathf.Lerp(y0, y1, t)), thickness, col);
            }
        }
    }
}
