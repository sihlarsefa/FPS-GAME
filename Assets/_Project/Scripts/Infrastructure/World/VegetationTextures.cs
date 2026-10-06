using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// Bitki dokuları (alfa kesmeli): çam iğne dalı, yaprak kümesi, çimen tutamı, çiçek. Piksel üretimi Unity nesnesi gerektirmez
    /// (testlenebilir); <c>Create*</c> yöntemleri Texture2D sarar. Aynı tohum aynı dokuyu verir.
    /// Kart dokularında u = genişlik, v = uzunluk (0 gövde tarafı, 1 uç).
    /// </summary>
    public static class VegetationTextures
    {
        public const int CardSize = 128;
        public const int GrassSize = 128;

        // ------------------------------------------------------------------ Piksel üretimi

        /// <summary>Çam dalı kartı: ortada ince dal, iki yana ileri süpürülmüş iğne demetleri; uçta daralır. Alfa 0/255 (+ yumuşak kenar).</summary>
        /// <summary>Çam iğnesi albedo parlaklık çarpanı (gün ışığında siyaha çökmesin).</summary>
        public const float NeedleBrightness = 1.6f;

        /// <summary>Güneş alan uç iğnelerde albedo artışı (t = dal boyunca 0..1; kökte 1.0, uçta 1+NeedleTipBoost).</summary>
        public const float NeedleTipBoost = 0.22f;

        public static float NeedleTipFactor(float t) => 1f + NeedleTipBoost * Mathf.Clamp01(t) * Mathf.Clamp01(t);

        /// <summary>Çam malzeme tonu: varyant ton kayması (±%6): sıcak (+) kırmızı yönde, serin (-) mavi yönde.</summary>
        public static Color PineTint(int variant)
        {
            var h = TreeMeshes.GetPineVariant(variant).HueShift;
            return new Color(1f + h, 1f, 1f - h * 0.9f);
        }

        /// <summary>Meşe malzeme tonu: iki varyant (0 zeytin, 1 hafif sarımsı); uzakta açık leke olmaması için üst sınır 1.</summary>
        public static Color OakTint(int variant) => variant % 2 == 1 ? new Color(0.98f, 0.98f, 0.84f) : new Color(0.94f, 0.97f, 0.88f);

        public static Color32[] NeedleCard(int size, int seed)
        {
            var px = new Color32[size * size];
            var rng = new System.Random(seed * 131 + 5);
            var twig = new Color32(78, 56, 38, 255);

            // Dal (v boyunca, hafif dalgalı).
            for (var s = 0; s <= 64; s++)
            {
                var t = s / 64f;
                var x = 0.5f + Mathf.Sin(t * 5f + seed) * 0.012f;
                Disk(px, size, x, t, 0.011f * (1f - t * 0.5f), twig);
            }

            const int perSide = 34;
            for (var side = -1; side <= 1; side += 2)
            {
                for (var i = 0; i < perSide; i++)
                {
                    var t = 0.02f + 0.94f * (i + (float)rng.NextDouble() * 0.6f) / perSide;
                    var reach = 0.46f * (1f - t * 0.72f) * Range(rng, 0.8f, 1.1f);
                    var originX = 0.5f;
                    // Her dal noktasından 3 iğne yelpazesi.
                    for (var f = 0; f < 3; f++)
                    {
                        var sweep = Range(rng, 0.25f, 0.6f) + f * 0.12f; // ileri (v+) eğim
                        var dx = side * (1f - f * 0.15f) * reach;
                        var dy = sweep * reach * 0.85f;
                        var shade = Range(rng, 0.75f, 1.15f) * Mathf.Lerp(0.8f, 1.1f, t) * NeedleTipFactor(t);
                        // Soluk, gri-zeytin yeşili (koyu/doygun değil): ışık altında siyaha çökmez.
                        var c = new Color32(
                            (byte)Mathf.Clamp((62f * shade + 8f) * NeedleBrightness, 0f, 255f),
                            (byte)Mathf.Clamp((92f * shade + 14f) * NeedleBrightness, 0f, 255f),
                            (byte)Mathf.Clamp(54f * shade * NeedleBrightness, 0f, 255f), 255);
                        Segment(px, size, originX, t, originX + dx, t + dy, 0.0085f, c);
                    }
                }
            }

            // Uç demeti.
            for (var f = 0; f < 5; f++)
            {
                var a = (f - 2) * 0.22f;
                Segment(px, size, 0.5f, 0.9f, 0.5f + Mathf.Sin(a) * 0.15f, 0.9f + Mathf.Cos(a) * 0.1f, 0.009f, new Color32(138, 189, 112, 255));
            }

            return px;
        }

        /// <summary>Meşe yaprak kümesi: dal üzerinde rastgele dönmüş eliptik yapraklar (yeşil tonları, damarlı). Alfa 0/255.</summary>
        public static Color32[] LeafCluster(int size, int seed)
        {
            var px = new Color32[size * size];
            var rng = new System.Random(seed * 151 + 9);
            var twig = new Color32(70, 52, 36, 255);
            Segment(px, size, 0.5f, 0f, 0.5f, 0.55f, 0.012f, twig);

            const int leaves = 46;
            for (var i = 0; i < leaves; i++)
            {
                // Yaprak merkezi: kümenin içinde, merkeze doğru yoğun.
                var angle = Range(rng, 0f, Mathf.PI * 2f);
                var radius = Mathf.Sqrt((float)rng.NextDouble()) * 0.4f;
                var cx = 0.5f + Mathf.Cos(angle) * radius * 0.95f;
                var cy = 0.5f + Mathf.Sin(angle) * radius;
                var rot = Range(rng, 0f, Mathf.PI);
                var len = Range(rng, 0.11f, 0.17f);
                var wid = len * Range(rng, 0.45f, 0.62f);
                var shade = Range(rng, 0.7f, 1.2f);
                var yellow = Range(rng, 0f, 0.25f);
                // Soluk zeytin yeşili; bazı yapraklar sarıya kayar (doğal çeşitlilik).
                var c = new Color32(
                    (byte)Mathf.Clamp((84f + 70f * yellow) * shade, 0f, 255f),
                    (byte)Mathf.Clamp(112f * shade + 6f, 0f, 255f),
                    (byte)Mathf.Clamp(56f * shade, 0f, 255f), 255);
                Ellipse(px, size, cx, cy, len, wid, rot, c);
            }

            return px;
        }

        /// <summary>Çimen tutamı: 9 eğik, uca doğru incelen yaprak. Gri-yeşil (arazi healthy/dry rengiyle çarpılır), tabanda koyu.</summary>
        public static Color32[] GrassTuft(int size, int seed)
        {
            var px = new Color32[size * size];
            var rng = new System.Random(seed * 173 + 3);
            const int blades = 9;
            for (var i = 0; i < blades; i++)
            {
                var baseX = 0.5f + Range(rng, -0.2f, 0.2f);
                var lean = Range(rng, -0.28f, 0.28f) + (baseX - 0.5f) * 0.6f;
                var height = Range(rng, 0.55f, 0.97f);
                var width = Range(rng, 0.028f, 0.04f);
                var bright = Range(rng, 0.8f, 1.05f);
                var steps = 48;
                for (var s = 0; s <= steps; s++)
                {
                    var t = s / (float)steps;
                    var x = baseX + lean * t * t;
                    var y = t * height;
                    var w = width * (1f - Mathf.Pow(t, 1.6f)) + 0.002f;
                    var shade = bright * Mathf.Lerp(0.55f, 1f, t);
                    var v = (byte)Mathf.Clamp(255f * shade, 0f, 255f);
                    Disk(px, size, x, y, w, new Color32(v, v, v, 255));
                }
            }

            return px;
        }

        /// <summary>Çiçek: ince sap + 6 taç yaprak + sarı merkez. Beyazımsı (arazi rengiyle boyanır).</summary>
        public static Color32[] Flower(int size, int seed)
        {
            var px = new Color32[size * size];
            var rng = new System.Random(seed * 191 + 1);
            var stem = new Color32(150, 190, 120, 255);
            const float headY = 0.62f;
            Segment(px, size, 0.5f, 0f, 0.5f, headY, 0.012f, stem);
            Ellipse(px, size, 0.4f, 0.2f, 0.12f, 0.035f, 0.5f, stem);
            for (var p = 0; p < 6; p++)
            {
                var a = p / 6f * Mathf.PI * 2f + Range(rng, -0.1f, 0.1f);
                var cx = 0.5f + Mathf.Cos(a) * 0.09f;
                var cy = headY + Mathf.Sin(a) * 0.09f;
                Ellipse(px, size, cx, cy, 0.1f, 0.05f, a, new Color32(250, 250, 250, 255));
            }

            Disk(px, size, 0.5f, headY, 0.05f, new Color32(255, 214, 80, 255));
            return px;
        }

        /// <summary>Verilen alfa ≥ eşik olan piksel oranı (0..1) — testler ve kesme ayarı için.</summary>
        public static float Coverage(Color32[] pixels, byte threshold = 128)
        {
            if (pixels == null || pixels.Length == 0)
                return 0f;
            var n = 0;
            for (var i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a >= threshold)
                    n++;
            }

            return n / (float)pixels.Length;
        }

        // ------------------------------------------------------------------ Unity dokuları

        public static Texture2D CreateNeedleCard(int seed) => MakeCutout("HK_Veg_Needles", CardSize, NeedleCard(CardSize, seed), true);

        public static Texture2D CreateLeafCluster(int seed) => MakeCutout("HK_Veg_Leaves", CardSize, LeafCluster(CardSize, seed), true);

        public static Texture2D CreateGrassTuft(int seed) => MakeCutout("HK_Veg_GrassTuft", GrassSize, GrassTuft(GrassSize, seed), false);

        public static Texture2D CreateFlower(int seed) => MakeCutout("HK_Veg_Flower", GrassSize, Flower(GrassSize, seed), false);

        private static Texture2D MakeCutout(string name, int size, Color32[] pixels, bool mips)
        {
            // Kenar sızıntısını önlemek için boş piksellerin rengini en yakın dolu renge yayar (alfa 0 kalır).
            Dilate(pixels, size, 3);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, mips, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 2
            };
            tex.SetPixels32(pixels);
            tex.Apply(mips, false);
            return tex;
        }

        // ------------------------------------------------------------------ Çizim yardımcıları (uv 0..1; y yukarı)

        private static void Disk(Color32[] px, int size, float u, float v, float radius, Color32 color)
        {
            var cx = u * (size - 1);
            var cy = v * (size - 1);
            var r = Mathf.Max(0.8f, radius * size);
            var x0 = Mathf.Max(0, Mathf.FloorToInt(cx - r - 1));
            var x1 = Mathf.Min(size - 1, Mathf.CeilToInt(cx + r + 1));
            var y0 = Mathf.Max(0, Mathf.FloorToInt(cy - r - 1));
            var y1 = Mathf.Min(size - 1, Mathf.CeilToInt(cy + r + 1));
            var r2 = r * r;
            for (var y = y0; y <= y1; y++)
            {
                for (var x = x0; x <= x1; x++)
                {
                    var dx = x - cx;
                    var dy = y - cy;
                    if (dx * dx + dy * dy <= r2)
                        px[y * size + x] = color;
                }
            }
        }

        private static void Segment(Color32[] px, int size, float u0, float v0, float u1, float v1, float radius, Color32 color)
        {
            var len = Mathf.Max(Mathf.Abs(u1 - u0), Mathf.Abs(v1 - v0)) * size;
            var steps = Mathf.Max(2, Mathf.CeilToInt(len * 1.5f));
            for (var s = 0; s <= steps; s++)
            {
                var t = s / (float)steps;
                // Uca doğru incelir.
                Disk(px, size, Mathf.Lerp(u0, u1, t), Mathf.Lerp(v0, v1, t), radius * (1f - t * 0.55f), color);
            }
        }

        private static void Ellipse(Color32[] px, int size, float u, float v, float halfLen, float halfWid, float rot, Color32 color)
        {
            var cx = u * (size - 1);
            var cy = v * (size - 1);
            var a = halfLen * size;
            var b = Mathf.Max(0.8f, halfWid * size);
            var m = Mathf.CeilToInt(Mathf.Max(a, b)) + 1;
            var cos = Mathf.Cos(rot);
            var sin = Mathf.Sin(rot);
            for (var y = Mathf.Max(0, Mathf.FloorToInt(cy) - m); y <= Mathf.Min(size - 1, Mathf.FloorToInt(cy) + m); y++)
            {
                for (var x = Mathf.Max(0, Mathf.FloorToInt(cx) - m); x <= Mathf.Min(size - 1, Mathf.FloorToInt(cx) + m); x++)
                {
                    var dx = x - cx;
                    var dy = y - cy;
                    var lx = dx * cos + dy * sin;
                    var ly = -dx * sin + dy * cos;
                    var q = (lx * lx) / (a * a) + (ly * ly) / (b * b);
                    if (q > 1f)
                        continue;
                    // Orta damar: biraz koyu.
                    var vein = Mathf.Abs(ly) < 0.7f ? 0.82f : 1f;
                    px[y * size + x] = new Color32((byte)(color.r * vein), (byte)(color.g * vein), (byte)(color.b * vein), 255);
                }
            }
        }

        /// <summary>Alfa 0 piksellerin RGB'sini komşu dolu piksellerden doldurur (mip/bilinear sızıntısı için). Alfa değişmez.</summary>
        private static void Dilate(Color32[] px, int size, int passes)
        {
            for (var p = 0; p < passes; p++)
            {
                var copy = (Color32[])px.Clone();
                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        var i = y * size + x;
                        if (copy[i].a != 0 || copy[i].r + copy[i].g + copy[i].b != 0)
                            continue;
                        for (var k = 0; k < 4; k++)
                        {
                            var nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0);
                            var ny = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                            if (nx < 0 || ny < 0 || nx >= size || ny >= size)
                                continue;
                            var n = copy[ny * size + nx];
                            if (n.a == 0 && n.r + n.g + n.b == 0)
                                continue;
                            px[i] = new Color32(n.r, n.g, n.b, 0);
                            break;
                        }
                    }
                }
            }
        }

        private static float Range(System.Random rng, float min, float max)
        {
            return min + (float)rng.NextDouble() * (max - min);
        }
    }
}
