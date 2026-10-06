using System.Collections.Generic;
using UnityEngine;

namespace Project.Infrastructure.Rendering
{
    /// <summary>
    /// Çalışma zamanında üretilen dokular (harici varlık yok). Her doku bir kez üretilir ve önbelleğe alınır.
    /// Dönen dokular paylaşımlıdır — değiştirmeyin / yok etmeyin.
    /// Dokular okunabilir bırakılır (editör kurulumu bunları varlık olarak kaydedebilsin).
    /// </summary>
    public static class ProceduralTextures
    {
        /// <summary>Türk bayrağı kırmızısı (#E30A17).</summary>
        public static readonly Color32 FlagRed = new Color32(0xE3, 0x0A, 0x17, 0xFF);

        private static Texture2D _softCircle;
        private static Texture2D _circle;
        private static Texture2D _ring;
        private static Texture2D _triangle;
        private static Texture2D _whitePixel;
        private static Texture2D _turkishFlag;
        private static Texture2D _softLine;
        private static Texture2D _smokePuff;
        private static Texture2D _bulletHole;
        private static Texture2D _starburst;

        private static readonly Dictionary<CamoKey, Texture2D> CamoCache = new Dictionary<CamoKey, Texture2D>();
        private static readonly Dictionary<long, Texture2D> NoiseCache = new Dictionary<long, Texture2D>();
        private static readonly Dictionary<int, Texture2D> SurfaceCache = new Dictionary<int, Texture2D>();
        private static readonly Dictionary<Texture2D, Sprite> SpriteCache = new Dictionary<Texture2D, Sprite>();

        // ------------------------------------------------------------------ Public API (contract)

        /// <summary>Yumuşak kenarlı beyaz daire (parçacıklar için). 64×64, alfa merkezden dışa azalır.</summary>
        public static Texture2D SoftCircle => Valid(_softCircle) ? _softCircle : (_softCircle = BuildSoftCircle(64));

        /// <summary>Kenar yumuşatmalı (AA) dolu beyaz daire. 128×128.</summary>
        public static Texture2D Circle => Valid(_circle) ? _circle : (_circle = BuildCircle(128));

        /// <summary>Kenar yumuşatmalı beyaz halka. 128×128.</summary>
        public static Texture2D Ring => Valid(_ring) ? _ring : (_ring = BuildRing(128, 0.92f, 0.14f));

        /// <summary>Yukarıyı gösteren kenar yumuşatmalı beyaz üçgen (işaretçi/pusula). 128×128.</summary>
        public static Texture2D Triangle => Valid(_triangle) ? _triangle : (_triangle = BuildTriangle(128));

        /// <summary>Düz beyaz doku (4×4).</summary>
        public static Texture2D WhitePixel => Valid(_whitePixel) ? _whitePixel : (_whitePixel = BuildWhite());

        /// <summary>
        /// Türk bayrağı (3:2, 768×512). Kırmızı #E30A17; ay ve yıldız Türk Bayrağı Kanunu oranlarıyla:
        /// dış daire merkezi gönderden 1/2 G, çapı 1/2 G; iç daire merkezi +1/16 G, çapı 2/5 G;
        /// iç daire ile yıldız çemberi arası 1/3 G, yıldız çemberi çapı 1/4 G, bir ucu ayın ağzına bakar.
        /// u=0 gönder (sol) tarafıdır.
        /// </summary>
        public static Texture2D TurkishFlag => Valid(_turkishFlag) ? _turkishFlag : (_turkishFlag = BuildTurkishFlag(768, 512));

        /// <summary>Uzunlamasına sabit, enine yumuşak bant (mermi izi / ışın çizgileri için). 16×64.</summary>
        public static Texture2D SoftLine => Valid(_softLine) ? _softLine : (_softLine = BuildSoftLine());

        /// <summary>Gürültülü yumuşak duman bulutu (alfa). 128×128.</summary>
        public static Texture2D SmokePuff => Valid(_smokePuff) ? _smokePuff : (_smokePuff = BuildSmokePuff(128, 1337));

        /// <summary>Mermi deliği izi: koyu merkez, yumuşak kenar (alfa). 64×64.</summary>
        public static Texture2D BulletHole => Valid(_bulletHole) ? _bulletHole : (_bulletHole = BuildBulletHole(64, 4242));

        /// <summary>Namlu alevi için ışınsal yıldız patlaması (alfa). 128×128.</summary>
        public static Texture2D Starburst => Valid(_starburst) ? _starburst : (_starburst = BuildStarburst(128, 7));

        /// <summary>
        /// Dört renkli, döşenebilir dijital (pikselli) kamuflaj. a ana zemin (~%45), b ve c lekeler, d küçük koyu benekler.
        /// 128×128, 4 piksellik hücreler. Aynı parametreler aynı dokuyu döndürür.
        /// </summary>
        public static Texture2D DigitalCamo(Color a, Color b, Color c, Color d, int seed)
        {
            var key = new CamoKey(a, b, c, d, seed);
            if (CamoCache.TryGetValue(key, out var cached) && Valid(cached))
                return cached;

            var tex = BuildDigitalCamo(key, 128, 4);
            CamoCache[key] = tex;
            return tex;
        }

        private static readonly Dictionary<CamoKey, Texture2D> FabricCamoCache = new Dictionary<CamoKey, Texture2D>();

        /// <summary>
        /// Asker üniforması kamuflajı: 512x512 = 1 m (metre ölçekli UV), 10-25 cm iri lekeler, ~2,5 cm'lik iri piksel basamakları
        /// (piksel başı gürültü yok), bilinear/trilinear filtre ve çok düşük güçte kumaş dokuma bindirmesi.
        /// </summary>
        public static Texture2D DigitalCamoFabric(Color a, Color b, Color c, Color d, int seed)
        {
            var key = new CamoKey(a, b, c, d, seed);
            if (FabricCamoCache.TryGetValue(key, out var cached) && Valid(cached))
                return cached;

            const int size = 512;
            const int cells = 40;
            var palette = new[] { key.A, key.B, key.C, key.D };
            var cellColor = new Color32[cells * cells];
            for (var cy = 0; cy < cells; cy++)
            {
                for (var cx = 0; cx < cells; cx++)
                {
                    var fx = cx / (float)cells * 5f;
                    var fy = cy / (float)cells * 5f;
                    var n1 = Fbm(fx, fy, 5, 2, seed);
                    var n2 = Fbm(fx * 1.6f, fy * 1.6f, 8, 1, seed * 31 + 7);
                    var v = n1 + (Hash01(cx, cy, seed ^ 0x5bd1e995) - 0.5f) * 0.05f;
                    int index;
                    if (n2 > 0.66f) index = 3;
                    else if (v < 0.42f) index = 1;
                    else if (v > 0.58f) index = 2;
                    else index = 0;
                    cellColor[cy * cells + cx] = palette[index];
                }
            }

            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                var row = (y * cells / size) * cells;
                for (var x = 0; x < size; x++)
                {
                    var col = cellColor[row + x * cells / size];
                    // Çok hafif dokuma: 2x2 piksellik çapraz damalı ±3% parlaklık.
                    var w = (((x >> 1) + (y >> 1)) & 1) == 0 ? 1.03f : 0.97f;
                    pixels[y * size + x] = new Color32(ToByte(col.r / 255f * w), ToByte(col.g / 255f * w), ToByte(col.b / 255f * w), 255);
                }
            }

            var tex = Create("HK_FabricCamo_" + seed, size, size, pixels, true, TextureWrapMode.Repeat);
            tex.anisoLevel = 4;
            FabricCamoCache[key] = tex;
            return tex;
        }

        /// <summary>Döşenebilir gri tonlu fBm gürültüsü (0..1). size 8..1024 arasına sıkıştırılır (2'nin kuvveti önerilir).</summary>
        public static Texture2D Noise(int size, int seed)
        {
            size = Mathf.Clamp(size, 8, 1024);
            var key = ((long)size << 32) ^ (uint)seed;
            if (NoiseCache.TryGetValue(key, out var cached) && Valid(cached))
                return cached;

            var tex = BuildNoise(size, seed);
            NoiseCache[key] = tex;
            return tex;
        }

        /// <summary>Yüzey ayrıntısı: 0,72..1,0 aralığına sıkıştırılmış döşenebilir gürültü (malzeme rengini fazla karartmaz). 128×128.</summary>
        public static Texture2D SurfaceDetail(int seed)
        {
            if (SurfaceCache.TryGetValue(seed, out var cached) && Valid(cached))
                return cached;

            var tex = BuildSurfaceDetail(128, seed);
            SurfaceCache[seed] = tex;
            return tex;
        }

        /// <summary>Dokudan tam boy, merkez pivotlu sprite (önbellekli). null → null.</summary>
        public static Sprite ToSprite(Texture2D texture)
        {
            if (texture == null)
                return null;

            if (SpriteCache.TryGetValue(texture, out var cached) && cached != null)
                return cached;

            var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect);
            sprite.name = texture.name + "_Sprite";
            SpriteCache[texture] = sprite;
            return sprite;
        }

        /// <summary>Bir malzeme tarifindeki doku anahtarını dokuya çevirir (editör kurulumu da kullanır).</summary>
        public static Texture2D ForKey(MaterialTextureKey key, MaterialSpec spec)
        {
            switch (key)
            {
                case MaterialTextureKey.None: return null;
                case MaterialTextureKey.Noise: return Noise(128, spec != null ? spec.TextureSeed : 0);
                case MaterialTextureKey.SurfaceDetail: return SurfaceDetail(spec != null ? spec.TextureSeed : 0);
                case MaterialTextureKey.DigitalCamo:
                    return spec != null
                        ? DigitalCamo(spec.CamoA, spec.CamoB, spec.CamoC, spec.CamoD, spec.TextureSeed)
                        : DigitalCamo(Color.gray, Color.gray, Color.gray, Color.gray, 0);
                case MaterialTextureKey.TurkishFlag: return TurkishFlag;
                case MaterialTextureKey.SoftCircle: return SoftCircle;
                case MaterialTextureKey.Circle: return Circle;
                case MaterialTextureKey.Ring: return Ring;
                case MaterialTextureKey.SoftLine: return SoftLine;
                case MaterialTextureKey.SmokePuff: return SmokePuff;
                case MaterialTextureKey.BulletHole: return BulletHole;
                case MaterialTextureKey.Starburst: return Starburst;
                case MaterialTextureKey.WhitePixel: return WhitePixel;
                case MaterialTextureKey.PbrAlbedo:
                {
                    var set = spec != null ? ProceduralPbr.Get(spec.Pbr, spec.TextureSeed) : null;
                    return set != null ? set.Albedo : null;
                }
                default: return null;
            }
        }

        // ------------------------------------------------------------------ Builders

        private static Texture2D BuildSoftCircle(int size)
        {
            var pixels = new Color32[size * size];
            var half = size * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x + 0.5f - half) / half;
                    var dy = (y + 0.5f - half) / half;
                    var r = Mathf.Sqrt(dx * dx + dy * dy);
                    var t = Mathf.Clamp01(1f - r);
                    var a = t * t * (3f - 2f * t); // smoothstep
                    a = Mathf.Pow(a, 1.25f);
                    pixels[y * size + x] = new Color32(255, 255, 255, ToByte(a));
                }
            }

            return Create("HK_SoftCircle", size, size, pixels, true, TextureWrapMode.Clamp);
        }

        private static Texture2D BuildCircle(int size)
        {
            var pixels = new Color32[size * size];
            var half = size * 0.5f;
            var radius = half - 1.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x + 0.5f - half;
                    var dy = y + 0.5f - half;
                    var dist = Mathf.Sqrt(dx * dx + dy * dy) - radius; // piksel cinsinden SDF
                    pixels[y * size + x] = new Color32(255, 255, 255, ToByte(Coverage(dist, 1f)));
                }
            }

            return Create("HK_Circle", size, size, pixels, true, TextureWrapMode.Clamp);
        }

        private static Texture2D BuildRing(int size, float outer01, float thickness01)
        {
            var pixels = new Color32[size * size];
            var half = size * 0.5f;
            var outer = half * outer01;
            var inner = outer - half * thickness01;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x + 0.5f - half;
                    var dy = y + 0.5f - half;
                    var r = Mathf.Sqrt(dx * dx + dy * dy);
                    var a = Coverage(r - outer, 1f) * (1f - Coverage(r - inner, 1f));
                    pixels[y * size + x] = new Color32(255, 255, 255, ToByte(a));
                }
            }

            return Create("HK_Ring", size, size, pixels, true, TextureWrapMode.Clamp);
        }

        private static Texture2D BuildTriangle(int size)
        {
            var pixels = new Color32[size * size];
            // Yukarı bakan eşkenar üçgen (piksel uzayında, y yukarı).
            var margin = size * 0.08f;
            var top = new Vector2(size * 0.5f, size - margin);
            var left = new Vector2(margin, margin + size * 0.06f);
            var right = new Vector2(size - margin, margin + size * 0.06f);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    // Konveks çokgen için yaklaşık SDF: kenar yarı düzlem mesafelerinin en büyüğü (saat yönü: top → right → left).
                    var d = Mathf.Max(EdgeDistance(p, top, right), Mathf.Max(EdgeDistance(p, right, left), EdgeDistance(p, left, top)));
                    pixels[y * size + x] = new Color32(255, 255, 255, ToByte(Coverage(d, 1f)));
                }
            }

            return Create("HK_Triangle", size, size, pixels, true, TextureWrapMode.Clamp);
        }

        private static Texture2D BuildWhite()
        {
            var pixels = new Color32[16];
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(255, 255, 255, 255);
            return Create("HK_WhitePixel", 4, 4, pixels, false, TextureWrapMode.Clamp);
        }

        private static Texture2D BuildSoftLine()
        {
            const int w = 16, h = 64;
            var pixels = new Color32[w * h];
            for (var y = 0; y < h; y++)
            {
                var v = (y + 0.5f) / h * 2f - 1f; // -1..1 enine
                var t = Mathf.Clamp01(1f - Mathf.Abs(v));
                var a = t * t * (3f - 2f * t);
                for (var x = 0; x < w; x++)
                    pixels[y * w + x] = new Color32(255, 255, 255, ToByte(a));
            }

            return Create("HK_SoftLine", w, h, pixels, true, TextureWrapMode.Clamp);
        }

        private static Texture2D BuildSmokePuff(int size, int seed)
        {
            var pixels = new Color32[size * size];
            var half = size * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x + 0.5f - half) / half;
                    var dy = (y + 0.5f - half) / half;
                    var r = Mathf.Sqrt(dx * dx + dy * dy);
                    var n = Fbm(x / (float)size * 4f, y / (float)size * 4f, 4, 4, seed);
                    var edge = Mathf.Clamp01(1f - r * (0.85f + 0.45f * n));
                    var a = edge * edge * (3f - 2f * edge);
                    var shade = (byte)Mathf.Clamp(200 + (int)(55f * n), 0, 255);
                    pixels[y * size + x] = new Color32(shade, shade, shade, ToByte(a * (0.65f + 0.35f * n)));
                }
            }

            return Create("HK_SmokePuff", size, size, pixels, true, TextureWrapMode.Clamp);
        }

        private static Texture2D BuildBulletHole(int size, int seed)
        {
            var pixels = new Color32[size * size];
            var half = size * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x + 0.5f - half) / half;
                    var dy = (y + 0.5f - half) / half;
                    var r = Mathf.Sqrt(dx * dx + dy * dy);
                    var angle = Mathf.Atan2(dy, dx);
                    var jag = 0.06f * Mathf.Sin(angle * 7f + seed) + 0.04f * Mathf.Sin(angle * 13f + seed * 0.37f);
                    // Delik çekirdeği (çok koyu) + kavrulmuş/çatlamış çevre halkası.
                    var core = Coverage((r - (0.22f + jag * 0.5f)) * half, 1f);
                    var scorch = Mathf.Clamp01(1f - (r - 0.2f) / (0.62f + jag));
                    scorch = scorch * scorch;
                    var a = Mathf.Max(core, scorch * 0.75f);
                    var shade = (byte)Mathf.Lerp(60f, 8f, core);
                    pixels[y * size + x] = new Color32(shade, (byte)(shade * 0.95f), (byte)(shade * 0.9f), ToByte(a));
                }
            }

            return Create("HK_BulletHole", size, size, pixels, true, TextureWrapMode.Clamp);
        }

        private static Texture2D BuildStarburst(int size, int seed)
        {
            var pixels = new Color32[size * size];
            var half = size * 0.5f;
            var rng = new System.Random(seed);
            const int spikes = 7;
            var spikePhase = new float[spikes];
            var spikeLen = new float[spikes];
            for (var i = 0; i < spikes; i++)
            {
                spikePhase[i] = (i + (float)rng.NextDouble() * 0.5f) / spikes * Mathf.PI * 2f;
                spikeLen[i] = 0.7f + (float)rng.NextDouble() * 0.3f;
            }

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x + 0.5f - half) / half;
                    var dy = (y + 0.5f - half) / half;
                    var r = Mathf.Sqrt(dx * dx + dy * dy);
                    var angle = Mathf.Atan2(dy, dx);
                    var spike = 0f;
                    for (var i = 0; i < spikes; i++)
                    {
                        var da = Mathf.DeltaAngle(angle * Mathf.Rad2Deg, spikePhase[i] * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                        var width = 0.16f * (1f - r);
                        var s = Mathf.Clamp01(1f - Mathf.Abs(da) / Mathf.Max(0.02f, width));
                        s *= Mathf.Clamp01(1f - r / spikeLen[i]);
                        spike = Mathf.Max(spike, s);
                    }

                    var coreT = Mathf.Clamp01(1f - r / 0.45f);
                    var core = coreT * coreT * (3f - 2f * coreT);
                    var a = Mathf.Clamp01(Mathf.Max(core, spike));
                    pixels[y * size + x] = new Color32(255, 255, 255, ToByte(a));
                }
            }

            return Create("HK_Starburst", size, size, pixels, true, TextureWrapMode.Clamp);
        }

        private static Texture2D BuildTurkishFlag(int width, int height)
        {
            var pixels = new Color32[width * height];
            var g = (float)height;                // G = bayrak eni
            var px = 1f / g;                      // bir pikselin G cinsinden boyu (AA için)
            const float cy = 0.5f;

            // Ay: dış daire merkez (0.5G), yarıçap 0.25G; iç daire merkez (0.5625G), yarıçap 0.2G.
            const float outerX = 0.5f, outerR = 0.25f;
            const float innerX = 0.5f + 1f / 16f, innerR = 0.2f;

            // Yıldız: çevrel daire çapı F = 1/4 G (R = 0.125G). E = 1/3 G: iç ay dairesinin gönder tarafı kenarı ile
            // yıldız çevrel dairesinin gönder tarafı kenarı arası → yıldız merkezi 0.5625 - 0.2 + 1/3 + 0.125 = 0.8208 G
            // (resmî çizimle aynı: sol ucu 0.6958 G'de, iç dairenin ağzına 1/15 G girer). Bir uç gönderi (sol) gösterir.
            const float starR = 0.125f;
            var starX = innerX - innerR + 1f / 3f + starR; // = 0.820833 G
            var starInner = starR * Mathf.Cos(72f * Mathf.Deg2Rad) / Mathf.Cos(36f * Mathf.Deg2Rad);
            var star = new Vector2[10];
            for (var i = 0; i < 10; i++)
            {
                var ang = (180f + i * 36f) * Mathf.Deg2Rad;
                var rad = (i % 2 == 0) ? starR : starInner;
                star[i] = new Vector2(starX + Mathf.Cos(ang) * rad, cy + Mathf.Sin(ang) * rad);
            }

            var red = FlagRed;
            var white = new Color32(255, 255, 255, 255);
            var starMinX = starX - starR - px * 2f;
            var starMaxX = starX + starR + px * 2f;

            for (var y = 0; y < height; y++)
            {
                var v = (y + 0.5f) * px;
                for (var x = 0; x < width; x++)
                {
                    var u = (x + 0.5f) * px;

                    // Hilal kapsamı: dış daire içinde VE iç dairenin dışında.
                    var dOuter = Hypot(u - outerX, v - cy) - outerR;
                    var dInner = Hypot(u - innerX, v - cy) - innerR;
                    var crescent = Coverage(dOuter, px) * (1f - Coverage(dInner, px));

                    var starCov = 0f;
                    if (u >= starMinX && u <= starMaxX && Mathf.Abs(v - cy) <= starR + px * 2f)
                        starCov = Coverage(PolygonSignedDistance(new Vector2(u, v), star), px);

                    var w = Mathf.Clamp01(Mathf.Max(crescent, starCov));
                    pixels[y * width + x] = Color32.Lerp(red, white, w);
                }
            }

            return Create("HK_TurkishFlag", width, height, pixels, true, TextureWrapMode.Clamp);
        }

        private static Texture2D BuildDigitalCamo(CamoKey key, int size, int cellSize)
        {
            var cells = Mathf.Max(4, size / cellSize);
            size = cells * cellSize;
            var pixels = new Color32[size * size];
            var palette = new[] { key.A, key.B, key.C, key.D };
            var cellColor = new Color32[cells * cells];

            for (var cyCell = 0; cyCell < cells; cyCell++)
            {
                for (var cxCell = 0; cxCell < cells; cxCell++)
                {
                    // Hücre koordinatı → döşenebilir fBm (periyot = hücre sayısı → kenarlar sarılır).
                    var fx = cxCell / (float)cells * 4f;
                    var fy = cyCell / (float)cells * 4f;
                    var n1 = Fbm(fx, fy, 4, 3, key.Seed);
                    var n2 = Fbm(fx * 2f, fy * 2f, 8, 2, key.Seed * 31 + 7);
                    var jitter = (Hash01(cxCell, cyCell, key.Seed ^ 0x5bd1e995) - 0.5f) * 0.12f;
                    var v = n1 + jitter;

                    int index;
                    if (n2 + jitter * 0.5f > 0.70f)
                        index = 3;          // küçük koyu benekler
                    else if (v < 0.40f)
                        index = 1;          // b lekeleri
                    else if (v > 0.60f)
                        index = 2;          // c lekeleri
                    else
                        index = 0;          // ana zemin

                    cellColor[cyCell * cells + cxCell] = palette[index];
                }
            }

            for (var y = 0; y < size; y++)
            {
                var row = (y / cellSize) * cells;
                for (var x = 0; x < size; x++)
                    pixels[y * size + x] = cellColor[row + x / cellSize];
            }

            var tex = Create("HK_DigitalCamo_" + key.Seed, size, size, pixels, true, TextureWrapMode.Repeat);
            return tex;
        }

        private static Texture2D BuildNoise(int size, int seed)
        {
            var pixels = new Color32[size * size];
            var octaves = Mathf.Clamp(Mathf.RoundToInt(Mathf.Log(size, 2f)) - 2, 1, 6);
            var min = float.MaxValue;
            var max = float.MinValue;
            var values = new float[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var n = Fbm(x / (float)size * 4f, y / (float)size * 4f, 4, octaves, seed);
                    values[y * size + x] = n;
                    if (n < min) min = n;
                    if (n > max) max = n;
                }
            }

            var range = Mathf.Max(0.0001f, max - min);
            for (var i = 0; i < values.Length; i++)
            {
                var b = ToByte((values[i] - min) / range);
                pixels[i] = new Color32(b, b, b, 255);
            }

            return Create("HK_Noise_" + size + "_" + seed, size, size, pixels, true, TextureWrapMode.Repeat);
        }

        private static Texture2D BuildSurfaceDetail(int size, int seed)
        {
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var broad = Fbm(x / (float)size * 4f, y / (float)size * 4f, 4, 4, seed);
                    var grain = Hash01(x, y, seed * 7 + 3);
                    var v = 0.72f + 0.24f * broad + 0.04f * grain;
                    var b = ToByte(v);
                    pixels[y * size + x] = new Color32(b, b, b, 255);
                }
            }

            return Create("HK_SurfaceDetail_" + seed, size, size, pixels, true, TextureWrapMode.Repeat);
        }

        // ------------------------------------------------------------------ Helpers

        private static Texture2D Create(string name, int width, int height, Color32[] pixels, bool mipmaps, TextureWrapMode wrap)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, mipmaps, false)
            {
                name = name,
                wrapMode = wrap,
                filterMode = FilterMode.Bilinear,
                anisoLevel = mipmaps ? 2 : 0
            };
            tex.SetPixels32(pixels);
            tex.Apply(mipmaps, false);
            return tex;
        }

        private static bool Valid(Texture2D tex) => tex != null;

        private static byte ToByte(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);

        private static float Hypot(float x, float y) => Mathf.Sqrt(x * x + y * y);

        /// <summary>İşaretli mesafeden (negatif = içeride) kenar yumuşatmalı kapsam. pixel = bir pikselin aynı birimdeki boyu.</summary>
        private static float Coverage(float signedDistance, float pixel) => Mathf.Clamp01(0.5f - signedDistance / pixel);

        /// <summary>a→b kenarına göre işaretli mesafe; saat yönündeki konveks çokgende içerisi negatiftir.</summary>
        private static float EdgeDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            var e = b - a;
            var n = new Vector2(e.y, -e.x).normalized; // saat yönünde sağ taraf = iç normal
            return -Vector2.Dot(p - a, n);
        }

        /// <summary>Basit (konkav olabilir) çokgen için işaretli mesafe: içeride negatif.</summary>
        private static float PolygonSignedDistance(Vector2 p, Vector2[] poly)
        {
            var minSq = float.MaxValue;
            var inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                var a = poly[j];
                var b = poly[i];
                var e = b - a;
                var w = p - a;
                var t = Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                var d = w - e * t;
                var sq = d.sqrMagnitude;
                if (sq < minSq)
                    minSq = sq;

                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }

            var dist = Mathf.Sqrt(minSq);
            return inside ? -dist : dist;
        }

        /// <summary>Tamsayı karması → [0,1).</summary>
        private static float Hash01(int x, int y, int seed)
        {
            unchecked
            {
                var h = (uint)seed * 0x9E3779B1u;
                h ^= (uint)x * 0x85EBCA77u;
                h = (h << 13) | (h >> 19);
                h ^= (uint)y * 0xC2B2AE3Du;
                h ^= h >> 16;
                h *= 0x7FEB352Du;
                h ^= h >> 15;
                h *= 0x846CA68Bu;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        /// <summary>Periyodik (döşenebilir) değer gürültüsü. x,y kafes biriminde; period kafes hücresi sayısı.</summary>
        private static float ValueNoise(float x, float y, int period, int seed)
        {
            var x0 = Mathf.FloorToInt(x);
            var y0 = Mathf.FloorToInt(y);
            var tx = x - x0;
            var ty = y - y0;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);

            var xa = Mod(x0, period);
            var xb = Mod(x0 + 1, period);
            var ya = Mod(y0, period);
            var yb = Mod(y0 + 1, period);

            var a = Hash01(xa, ya, seed);
            var b = Hash01(xb, ya, seed);
            var c = Hash01(xa, yb, seed);
            var d = Hash01(xb, yb, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        /// <summary>Döşenebilir fBm: (x,y) temel periyot biriminde [0, basePeriod) aralığında; her oktavda periyot ikiye katlanır.</summary>
        private static float Fbm(float x, float y, int basePeriod, int octaves, int seed)
        {
            var sum = 0f;
            var amp = 0.5f;
            var norm = 0f;
            var period = basePeriod;
            var fx = x;
            var fy = y;
            for (var o = 0; o < octaves; o++)
            {
                sum += ValueNoise(fx, fy, period, seed + o * 1013) * amp;
                norm += amp;
                amp *= 0.5f;
                period *= 2;
                fx *= 2f;
                fy *= 2f;
            }

            return sum / norm;
        }

        private static int Mod(int a, int m)
        {
            var r = a % m;
            return r < 0 ? r + m : r;
        }

        private readonly struct CamoKey : System.IEquatable<CamoKey>
        {
            public readonly Color32 A, B, C, D;
            public readonly int Seed;

            public CamoKey(Color a, Color b, Color c, Color d, int seed)
            {
                A = a; B = b; C = c; D = d; Seed = seed;
            }

            public bool Equals(CamoKey other) =>
                Pack(A) == Pack(other.A) && Pack(B) == Pack(other.B) && Pack(C) == Pack(other.C) && Pack(D) == Pack(other.D) && Seed == other.Seed;

            public override bool Equals(object obj) => obj is CamoKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    var h = (int)Pack(A);
                    h = h * 397 ^ (int)Pack(B);
                    h = h * 397 ^ (int)Pack(C);
                    h = h * 397 ^ (int)Pack(D);
                    return h * 397 ^ Seed;
                }
            }

            private static uint Pack(Color32 c) => (uint)(c.r | (c.g << 8) | (c.b << 16) | (c.a << 24));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Etki alanı yeniden yüklenmeden oynatma: yok edilmiş dokular Valid() ile zaten yeniden üretilir.
            SpriteCache.Clear();
        }
    }
}
