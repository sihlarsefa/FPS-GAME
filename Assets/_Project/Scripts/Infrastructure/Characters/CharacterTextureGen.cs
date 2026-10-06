using System.Collections.Generic;
using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Infrastructure.Characters
{
    /// <summary>
    /// Karakter detay dokuları: yükseklik haritaları saf matematik (<see cref="Height"/>, EditMode testli), normal haritalar
    /// ProceduralPbr.NormalFromHeight ile türetilir ve bir kez üretilip önbelleğe alınır (paylaşılan; yok etmeyin).
    /// </summary>
    public static class CharacterTextureGen
    {
        public const int DefaultSize = 128;

        private static readonly Dictionary<long, Texture2D> NormalCache = new Dictionary<long, Texture2D>();
        private static Texture2D _macro;

        /// <summary>Tür için döşenebilir yükseklik haritası (size×size, 0..1). Aynı (tür, boyut, tohum) her zaman aynı sonucu verir.</summary>
        public static float[] Height(CharacterMaterialKind kind, int size, int seed)
        {
            size = Mathf.Clamp(size, 16, 512);
            var h = new float[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    float v;
                    switch (kind)
                    {
                        case CharacterMaterialKind.Skin:
                            v = Pores(x, y, size, seed);
                            break;
                        case CharacterMaterialKind.Cordura:
                            v = Basket(x, y, size, 8, seed);
                            break;
                        case CharacterMaterialKind.Rubber:
                        case CharacterMaterialKind.Leather:
                            v = Pebble(x, y, size, kind == CharacterMaterialKind.Leather ? 24 : 40, seed);
                            break;
                        case CharacterMaterialKind.HelmetPaint:
                            v = 0.5f + 0.5f * Tile(x, y, size, 32, seed) * 0.3f + 0.35f * Tile(x, y, size, 8, seed + 3);
                            break;
                        case CharacterMaterialKind.NvgLens:
                            v = 0.5f;
                            break;
                        default:
                            v = Ripstop(x, y, size, seed);
                            break;
                    }

                    h[y * size + x] = Mathf.Clamp01(v);
                }
            }

            return h;
        }

        /// <summary>Tür için önbellekli normal haritası (RG kodlu; linear).</summary>
        public static Texture2D Normal(CharacterMaterialKind kind, int seed = 17, int size = DefaultSize)
        {
            size = Mathf.ClosestPowerOfTwo(Mathf.Clamp(size, 16, 512));
            var key = ((long)(int)kind << 48) ^ ((long)size << 32) ^ (uint)seed;
            if (NormalCache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var strength = kind == CharacterMaterialKind.Skin ? 3.5f : kind == CharacterMaterialKind.NvgLens ? 0.1f : 5f;
            var px = ProceduralPbr.NormalFromHeight(Height(kind, size, seed), size, strength);
            var tex = ProceduralPbr.MakeTexture("HK_Char_" + kind + "_N", size, px, true);
            NormalCache[key] = tex;
            return tex;
        }

        /// <summary>Makro kir/leke dokusu (R, tembel; ProceduralTextures.Noise önbelleğini paylaşır).</summary>
        public static Texture2D Macro => _macro != null ? _macro : (_macro = ProceduralTextures.Noise(128, 5051));

        // ------------------------------------------------------------------ Yükseklik desenleri (hepsi döşenebilir)

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                var h = (uint)(x * 374761393 + y * 668265263 + seed * 1274126177);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
            }
        }

        /// <summary>Döngüsel (period hücre) değer gürültüsü, 0..1.</summary>
        private static float Tile(int x, int y, int size, int period, int seed)
        {
            var fx = (float)x / size * period;
            var fy = (float)y / size * period;
            var x0 = Mathf.FloorToInt(fx);
            var y0 = Mathf.FloorToInt(fy);
            var tx = fx - x0;
            var ty = fy - y0;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            int a = x0 % period, b = (x0 + 1) % period, c = y0 % period, d = (y0 + 1) % period;
            var v00 = Hash(a, c, seed);
            var v10 = Hash(b, c, seed);
            var v01 = Hash(a, d, seed);
            var v11 = Hash(b, d, seed);
            return Mathf.Lerp(Mathf.Lerp(v00, v10, tx), Mathf.Lerp(v01, v11, tx), ty);
        }

        /// <summary>Çapraz dokuma (twill) + her 16 pikselde kalın ripstop ızgara + hafif lif gürültüsü.</summary>
        private static float Ripstop(int x, int y, int size, int seed)
        {
            const float tau = 6.2831853f;
            var cells = 8f; // iri dokuma: ince taneli (moire/çizgili) parazit yok
            var u = (float)x / size * cells;
            var v = (float)y / size * cells;
            var twill = 0.5f + 0.5f * Mathf.Sin((u + v) * tau);
            var weaveCell = 0.5f + 0.25f * Mathf.Sin(u * tau) * Mathf.Sin(v * tau);
            var grid = (x % (size / 4) < 2 || y % (size / 4) < 2) ? 0.25f : 0f;
            var fiber = Tile(x, y, size, 64, seed) * 0.15f;
            return 0.25f + twill * 0.22f + weaveCell * 0.22f + grid + fiber;
        }

        /// <summary>Cordura: kaba sepet dokuma (tek-alt/tek-üst) + lif gürültüsü.</summary>
        private static float Basket(int x, int y, int size, int cells, int seed)
        {
            var cs = size / cells;
            var cx = x / cs;
            var cy = y / cs;
            var horiz = ((cx + cy) & 1) == 0;
            var local = horiz ? (float)(y % cs) / cs : (float)(x % cs) / cs;
            var ridge = 0.5f + 0.5f * Mathf.Sin(local * 6.2831853f * 2f);
            // MOLLE ızgarası: her 16 pikselde yatay şerit oluğu (koyu çizgi), ortada kısa dikiş kesikleri.
            var molle = (y % 16 < 2 && (x % 32) > 5) ? -0.22f : 0f;
            return 0.3f + ridge * 0.45f + Tile(x, y, size, 48, seed) * 0.2f + molle;
        }

        /// <summary>Gözenek: ince gürültü + seyrek çukurlar.</summary>
        private static float Pores(int x, int y, int size, int seed)
        {
            // Yalnızca çok düşük frekanslı, yumuşak dalga: pikselli gözenek/gürültü yok (yakın planda TV statiği yapıyordu).
            return 0.55f + Tile(x, y, size, 3, seed) * 0.25f + Tile(x, y, size, 5, seed + 9) * 0.1f;
        }

        /// <summary>Kauçuk/deri çakıl dokusu.</summary>
        private static float Pebble(int x, int y, int size, int period, int seed)
        {
            return Tile(x, y, size, period, seed) * 0.6f + Tile(x, y, size, period * 2, seed + 5) * 0.4f;
        }

        // ------------------------------------------------------------------ Savaş yıpranması (bakılmış albedo bindirmeleri)
        // Hepsi saf matematik (EditMode testli); doku üreticileri (Face/Gear/Uniform) seviye+tohum başına bir kez üretilip önbelleğe alınır.

        /// <summary>Yüz yıpranma dokusu çözünürlüğü.</summary>
        public const int FaceWearSize = 256;
        /// <summary>Yüz yıpranma dokusunun baş UV'sine eşlemesi: _BaseMap tiling (2,4), offset (0,0.1). Baş UV'si = (açı*0.078, y).</summary>
        public const float FaceUScale = 2f, FaceVScale = 4f, FaceVOffset = 0.1f;
        /// <summary>Gear kazıma tabanı: çizikler bu tabana göre açıktır (çarpan; malzeme rengi bu kadar açılarak telafi edilir).</summary>
        public const float GearScratchBase = 0.88f;

        /// <summary>Üniforma kir yamalarının karıştığı kir rengi (sRGB). Yamalar bu renge doğru KARIŞIR; global karartma çarpanı yoktur.</summary>
        public static readonly Color DirtColor = new Color(0.32f, 0.26f, 0.18f);
        /// <summary>Toz yamalarının karıştığı açık, doymamış ton (koyu kumaşın solması; kirin parlaklık kaybını dengeler).</summary>
        public static readonly Color DustColor = new Color(0.52f, 0.47f, 0.37f);
        /// <summary>Kir yaması en yüksek karışımı (tam güçte %35) ve yüzeyin en çok ne kadarını kaplayabileceği (%35).</summary>
        public const float UniformDirtMaxBlend = 0.35f, UniformDirtMaxCoverage = 0.35f;
        /// <summary>Toz yaması en yüksek karışımı ve en çok kapsama.</summary>
        public const float UniformDustMaxBlend = 0.18f, UniformDustMaxCoverage = 0.30f;
        /// <summary>Genel soluk (luma korunur: yalnız doygunluk azalır, parlaklık değişmez).</summary>
        public const float UniformFade = 0.12f;
        /// <summary>Çamur sıçraması küme yoğunluğu (tam güçte) ve beneğin çamur rengine karışımı.</summary>
        public const float UniformSplatterDensity = 0.025f, SplatterBlend = 0.6f;
        /// <summary>Yüz: barut isi en yüksek gücü (≤0.5), yarıçap ölçeği ve kan izi en yüksek gücü (ince çizgi, opak değil).</summary>
        public const float SootMaxStrength = 0.5f, SootRadiusScale = 0.9f, BloodMaxStrength = 0.8f;
        /// <summary>Yüz çarpan dokusu tabanının tam güçte düşüşü (ten parlaklığı en çok %4 azalır).</summary>
        public const float FaceBaseDrop = 0.04f;

        private static readonly Color32 BloodColor = new Color32(0x5A, 0x14, 0x10, 255);
        private static readonly Color SootColor = new Color(0.07f, 0.07f, 0.065f);
        private static readonly Color MudSpeck = new Color(0.21f, 0.15f, 0.10f);

        private static readonly Dictionary<string, Texture2D> WearCache = new Dictionary<string, Texture2D>();

        // sakal-yanak-çene barut blob'ları: x(|x|), y, yarıçap, ağırlık (metre, baş yerel)
        private static readonly float[] SootBlobs =
        {
            0.034f, 0.080f, 0.020f, 1.0f,  0.046f, 0.062f, 0.022f, 0.8f,  0.000f, 0.082f, 0.014f, 0.6f,
            0.030f, 0.034f, 0.020f, 0.7f,  0.000f, 0.016f, 0.018f, 0.6f,  0.020f, 0.052f, 0.016f, 0.5f
        };

        private static float Sm(float e0, float e1, float v)
        {
            var t = Mathf.Clamp01((v - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Tile olmayan düzgün değer gürültüsü (0..1).</summary>
        private static float Noise2(float x, float y, int seed)
        {
            var x0 = Mathf.FloorToInt(x);
            var y0 = Mathf.FloorToInt(y);
            var tx = x - x0;
            var ty = y - y0;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            return Mathf.Lerp(Mathf.Lerp(Hash(x0, y0, seed), Hash(x0 + 1, y0, seed), tx),
                              Mathf.Lerp(Hash(x0, y0 + 1, seed), Hash(x0 + 1, y0 + 1, seed), tx), ty);
        }

        /// <summary>Yüz yıpranma dokusu pikselini (tu,tv 0..1) baş yerel (x yanal, y yükseklik metre) koordinatına çevirir; front 0..1 = yüzün önü.</summary>
        public static void FaceCoords(float tu, float tv, out float x, out float y, out float front)
        {
            var u = tu / FaceUScale;
            y = (tv - FaceVOffset) / FaceVScale;
            var a = u / 0.078f;
            x = Mathf.Cos(a) * 0.07f;
            front = Sm(0.15f, 0.55f, Mathf.Sin(a));
        }

        /// <summary>Alın ter parlaması bandı (y metre, baş yerel): 0..1.</summary>
        public static float SweatSheen(float y) => Sm(0.135f, 0.145f, y) * (1f - Sm(0.165f, 0.178f, y));

        /// <summary>
        /// Yüz yıpranması maskeleri (0..1). level: 0, .33, .66, 1 (nicemlenmiş); variant 0..4 belirleyici.
        /// Sınırlar: kan izi ince çizgi ve opak değil (≤<see cref="BloodMaxStrength"/>); barut isi yumuşak lekeler, gücü ≤<see cref="SootMaxStrength"/>,
        /// yüzün ≤%30'unu kaplar (eşik altı halo sıfırlanır). Ten rengi baskın kalır (bkz. <see cref="WearRules.FaceLuminanceFloor"/>).
        /// </summary>
        public static void FaceWearSample(float x, float y, float level, int variant, out float blood, out float soot, out float sheen)
        {
            blood = 0f; soot = 0f; sheen = 0f;
            if (level <= 0f)
                return;

            var v = WearRules.VisualStrength(level);
            var count = Mathf.Clamp(Mathf.RoundToInt(level * 3f), 0, 3);
            for (var i = 0; i < count; i++)
            {
                var side = Hash(i, variant, 17) > 0.5f ? 1f : -1f;
                var kind = (variant + i) % 3;
                float ox = kind == 0 ? 0.062f : kind == 1 ? 0.030f : 0.050f;
                float oy = kind == 0 ? 0.124f : kind == 1 ? 0.117f : 0.092f;
                ox = side * (ox + (Hash(i, variant, 41) - 0.5f) * 0.008f);
                oy += (Hash(i, variant, 43) - 0.5f) * 0.008f;
                var len = (0.026f + 0.034f * Hash(i, variant, 47)) * (0.6f + 0.4f * level);
                var drift = side * (Hash(i, variant, 53) - 0.4f) * 0.010f;
                var phase = Hash(i, variant, 59) * 6.2832f;
                var t = (oy - y) / len;
                if (t < 0f || t > 1f)
                    continue;
                var cx = ox + drift * t + 0.0025f * Mathf.Sin(t * 6f + phase);
                var w = 0.0014f * (1f - 0.45f * t) + 0.0003f; // ince çizgi (~3 mm)
                var m = 1f - Sm(w * 0.55f, w, Mathf.Abs(x - cx));
                var alpha = (0.55f + 0.4f * Hash(i, variant, 31)) * (1f - 0.55f * t * t) * (0.85f + 0.3f * Noise2(y * 400f, i, variant));
                blood = Mathf.Max(blood, Mathf.Min(BloodMaxStrength, Mathf.Clamp01(m * alpha)));
            }

            var ax = Mathf.Abs(x);
            var sideBit = x >= 0f ? 1 : 0;
            for (var b = 0; b < SootBlobs.Length; b += 4)
            {
                var bi = b / 4;
                var strength = 0.55f + 0.9f * Hash(bi, variant * 2 + sideBit, 67);
                var bx = SootBlobs[b] + (Hash(bi, variant, 71) - 0.5f) * 0.008f;
                var by = SootBlobs[b + 1] + (Hash(bi, variant, 73) - 0.5f) * 0.008f;
                var r = SootBlobs[b + 2] * SootRadiusScale;
                var dx = (ax - bx) / r;
                var dy = (y - by) / r;
                var fall = Mathf.Exp(-(dx * dx + dy * dy) * 1.6f) * (0.55f + 0.9f * Noise2(x * 320f, y * 320f, variant + 5));
                soot = Mathf.Max(soot, Mathf.Clamp01(fall * SootBlobs[b + 3] * strength));
            }

            // Yumuşak eşik: 0.10 altındaki halo sıfırlanır (kapsama sınırlı), tepe SootMaxStrength × güç ile kısılır.
            soot = SootMaxStrength * v * Sm(0.10f, 0.80f, soot);
            sheen = SweatSheen(y) * (1f - Sm(0.05f, 0.07f, ax)) * Mathf.Lerp(0.5f, 1f, Noise2(x * 500f, y * 500f, variant + 9)) * v;
        }

        /// <summary>
        /// Yüz yıpranma çarpan dokusu pikselleri: beyaz = ten aynen; kan/barut ten rengine bölünerek (albedo × ten) hedef renge oturur.
        /// Taban çarpan tam güçte en çok <see cref="FaceBaseDrop"/> (%4) düşer: ten ten rengi kalır, yalnız ince kan izi + sınırlı is lekesi eklenir.
        /// </summary>
        public static Color32[] FaceWearPixels(int size, float level, int variant, Color skin)
        {
            size = Mathf.Clamp(size, 16, 512);
            var px = new Color32[size * size];
            var sk = new Color(Mathf.Max(skin.r, 0.05f), Mathf.Max(skin.g, 0.05f), Mathf.Max(skin.b, 0.05f));
            var baseMul = level > 0f ? 1f - FaceBaseDrop * WearRules.VisualStrength(level) : 1f; // parlama için pay (sheen 1.0'a açar)
            var bloodC = new Color(BloodColor.r / 255f / sk.r, BloodColor.g / 255f / sk.g, BloodColor.b / 255f / sk.b);
            var sootC = new Color(SootColor.r / sk.r, SootColor.g / sk.g, SootColor.b / sk.b);
            for (var py = 0; py < size; py++)
            {
                for (var pxl = 0; pxl < size; pxl++)
                {
                    FaceCoords((pxl + 0.5f) / size, (py + 0.5f) / size, out var x, out var y, out var front);
                    var r = baseMul; var g = baseMul; var b = baseMul;
                    if (level > 0f && front > 0.01f)
                    {
                        FaceWearSample(x, y, level, variant, out var blood, out var soot, out var sheen);
                        sheen *= front; soot *= front; blood *= front;
                        r = Mathf.Lerp(r, 1f, sheen * 0.8f); g = Mathf.Lerp(g, 1f, sheen * 0.8f); b = Mathf.Lerp(b, 1f, sheen * 0.8f);
                        r = Mathf.Lerp(r, Mathf.Clamp01(sootC.r), soot); g = Mathf.Lerp(g, Mathf.Clamp01(sootC.g), soot); b = Mathf.Lerp(b, Mathf.Clamp01(sootC.b), soot);
                        r = Mathf.Lerp(r, Mathf.Clamp01(bloodC.r), blood); g = Mathf.Lerp(g, Mathf.Clamp01(bloodC.g), blood); b = Mathf.Lerp(b, Mathf.Clamp01(bloodC.b), blood);
                    }

                    px[py * size + pxl] = new Color32(To8(r), To8(g), To8(b), 255);
                }
            }

            return px;
        }

        private static byte To8(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);

        /// <summary>
        /// Üniforma yıpranması (SINIRLI): global karartma çarpanı YOKTUR; kir/toz yama ve ton kaymasıdır. Katmanlar: (1) genel soluk (luma korunur),
        /// (2) açık toz yamaları (≤%18 karışım, ≤%30 kapsama), (3) kir yamaları kir rengine (<see cref="DirtColor"/>) ≤%35 karışım ve yüzeyin ≤%35'inde,
        /// (4) seyrek çamur sıçraması benekleri. Yama kapsaması alan eşiğiyle her tohumda zorlanır; ortalama sRGB parlaklık oranı ayrıca
        /// <see cref="WearRules.UniformLuminanceFloor"/> altına düşemez (koruma geçişi: kaynağa doğru geri karışım). Kamuflaj deseni okunur kalır. level 0 → kopya.
        /// </summary>
        public static Color32[] UniformWearPixels(Color32[] src, float level, int seed)
        {
            var size = Mathf.RoundToInt(Mathf.Sqrt(src.Length));
            var px = new Color32[src.Length];
            var vis = WearRules.VisualStrength(level);
            if (vis <= 0f || size * size != src.Length)
            {
                System.Array.Copy(src, px, src.Length);
                return px;
            }

            // Düşük frekanslı (döşenebilir) alanlar: kir ve toz yaması maskeleri. Eşik, alanın en çok CoverageMax'ını kaplayacak şekilde seçilir.
            var dirtField = new float[src.Length];
            var dustField = new float[src.Length];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    dirtField[y * size + x] = 0.6f * Tile(x, y, size, 4, seed + 101) + 0.4f * Tile(x, y, size, 11, seed + 103);
                    dustField[y * size + x] = Tile(x, y, size, 7, seed + 131);
                }
            }

            const float edge = 0.10f; // yumuşak yama kenarı (alan birimi)
            var dirtLo = FieldThreshold(dirtField, UniformDirtMaxCoverage);
            var dustLo = FieldThreshold(dustField, UniformDustMaxCoverage);
            var dirtBlend = UniformDirtMaxBlend * vis;
            var dustBlend = UniformDustMaxBlend * vis;
            var fade = UniformFade * vis;
            var density = UniformSplatterDensity * vis;
            long lumSrc = 0, lumOut = 0;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var i = y * size + x;
                    var c = src[i];
                    var r = c.r / 255f; var g = c.g / 255f; var b = c.b / 255f;

                    // (1) genel soluk: doygunluk azalır, parlaklık (luma) korunur
                    var gray = 0.299f * r + 0.587f * g + 0.114f * b;
                    r = Mathf.Lerp(r, gray, fade); g = Mathf.Lerp(g, gray, fade); b = Mathf.Lerp(b, gray, fade);

                    // (2) toz yaması: açık, doymamış tona (koyu kumaş solar; kirin parlaklık kaybını dengeler)
                    var dm = Sm(dustLo, dustLo + edge, dustField[i]) * dustBlend;
                    r = Mathf.Lerp(r, DustColor.r, dm); g = Mathf.Lerp(g, DustColor.g, dm); b = Mathf.Lerp(b, DustColor.b, dm);

                    // (3) kir yaması: kir rengine karışım (çarpan yok)
                    var km = Sm(dirtLo, dirtLo + edge, dirtField[i]) * dirtBlend;
                    r = Mathf.Lerp(r, DirtColor.r, km); g = Mathf.Lerp(g, DirtColor.g, km); b = Mathf.Lerp(b, DirtColor.b, km);

                    // (4) sıçrama: 2x2 kümeler, kirli bölgede yoğun, çamur rengine kısmi karışım (siyah değil)
                    var cluster = Hash(x >> 1, y >> 1, seed + 211);
                    if (cluster < density * (km > 0.02f ? 1.8f : 0.5f) && Hash(x, y, seed + 223) > 0.3f)
                    {
                        r = Mathf.Lerp(r, MudSpeck.r, SplatterBlend); g = Mathf.Lerp(g, MudSpeck.g, SplatterBlend); b = Mathf.Lerp(b, MudSpeck.b, SplatterBlend);
                    }

                    var o = new Color32(To8(r), To8(g), To8(b), c.a);
                    px[i] = o;
                    lumSrc += 299 * c.r + 587 * c.g + 114 * c.b;
                    lumOut += 299 * o.r + 587 * o.g + 114 * o.b;
                }
            }

            // Koruma geçişi: ortalama parlaklık oranı tabanın altındaysa kaynağa doğru geri karıştır (parlaklık karışımda doğrusaldır).
            var floor = WearRules.LuminanceFloor(vis, WearRules.UniformLuminanceFloor);
            if (lumSrc > 0 && lumOut < floor * lumSrc)
            {
                var ratio = (float)lumOut / lumSrc;
                var t = Mathf.Clamp01((floor - ratio) / Mathf.Max(1f - ratio, 1e-4f));
                for (var i = 0; i < px.Length; i++)
                {
                    var o = px[i];
                    var c = src[i];
                    px[i] = new Color32(
                        (byte)Mathf.RoundToInt(Mathf.Lerp(o.r, c.r, t)),
                        (byte)Mathf.RoundToInt(Mathf.Lerp(o.g, c.g, t)),
                        (byte)Mathf.RoundToInt(Mathf.Lerp(o.b, c.b, t)), o.a);
                }
            }

            return px;
        }

        /// <summary>Alan değerlerinin (0..1) en çok <paramref name="coverage"/> oranı eşiğin üstünde kalacak eşik (1024 kutulu histogram; belirleyici). Yama kapsama sınırını zorlar.</summary>
        public static float FieldThreshold(float[] field, float coverage)
        {
            const int bins = 1024;
            var hist = new int[bins];
            for (var i = 0; i < field.Length; i++)
                hist[Mathf.Clamp((int)(field[i] * bins), 0, bins - 1)]++;
            var limit = (int)(Mathf.Clamp01(coverage) * field.Length);
            var acc = 0;
            for (var k = bins - 1; k >= 0; k--)
            {
                acc += hist[k];
                if (acc > limit)
                    return (k + 1f) / bins;
            }

            return 0f;
        }

        /// <summary>
        /// Gear yıpranma çarpan dokusu: scratches=kenar çizikleri (tabana göre açık, ≤12 ince çizgi), mud=kurumuş çamur beneği (seyrek, kısmi karışım). Tileable.
        /// İnce tutulur: çizikler tabandan en çok %13.6 açık, benekler yüzeyin birkaç %'i.
        /// </summary>
        public static Color32[] GearWearPixels(int size, float level, int seed, bool scratches, bool mud)
        {
            size = Mathf.Clamp(size, 16, 512);
            var buf = new float[size * size];
            var vis = WearRules.VisualStrength(level); // 0..1 doyan güç: kenar aşınması/çizik ince kalır
            if (scratches && vis > 0f)
            {
                var n = 4 + Mathf.RoundToInt(vis * 8f);
                for (var i = 0; i < n; i++)
                {
                    var x0 = Hash(i, 1, seed) * size; var y0 = Hash(i, 2, seed) * size;
                    var ang = Hash(i, 3, seed) * 3.1416f;
                    var len = (0.08f + 0.22f * Hash(i, 4, seed)) * size;
                    var bright = 0.55f + 0.35f * Hash(i, 5, seed);
                    var dx = Mathf.Cos(ang); var dy = Mathf.Sin(ang);
                    for (var s = 0f; s < len; s += 0.5f)
                    {
                        var ix = ((int)(x0 + dx * s) % size + size) % size;
                        var iy = ((int)(y0 + dy * s) % size + size) % size;
                        var fade = 1f - s / len * 0.5f;
                        buf[iy * size + ix] = Mathf.Max(buf[iy * size + ix], bright * fade);
                    }
                }
            }

            var px = new Color32[size * size];
            var density = 0.02f * vis;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var v = scratches ? GearScratchBase + (1f - GearScratchBase) * buf[y * size + x] : 1f;
                    if (scratches && vis > 0f && Hash(x, y, seed + 7) < 0.003f * vis)
                        v = 1f; // boya sıyrığı noktaları
                    float r = v, g = v, b = v;
                    if (mud && vis > 0f)
                    {
                        var nn = Tile(x, y, size, 5, seed + 301);
                        if (Hash(x >> 1, y >> 1, seed + 311) < density * (nn > 0.5f ? 2f : 0.6f))
                        {
                            r = Mathf.Lerp(r, MudSpeck.r, SplatterBlend); g = Mathf.Lerp(g, MudSpeck.g, SplatterBlend); b = Mathf.Lerp(b, MudSpeck.b, SplatterBlend);
                        }
                    }

                    px[y * size + x] = new Color32(To8(r), To8(g), To8(b), 255);
                }
            }

            return px;
        }

        /// <summary>Önbellekli yüz yıpranma albedo'su (paylaşılan; yok etmeyin).</summary>
        public static Texture2D FaceWearAlbedo(Color skin, float level, int variant)
        {
            var key = "face|" + ColorUtility.ToHtmlStringRGB(skin) + "|" + level.ToString("0.00") + "|" + variant;
            if (WearCache.TryGetValue(key, out var t) && t != null)
                return t;
            t = ProceduralPbr.MakeTexture("HK_Wear_Face", FaceWearSize, FaceWearPixels(FaceWearSize, level, variant, skin), false);
            t.wrapMode = TextureWrapMode.Clamp;
            WearCache[key] = t;
            return t;
        }

        /// <summary>Önbellekli gear yıpranma albedo'su; null → bu tür için doku gerekmez.</summary>
        public static Texture2D GearWearAlbedo(CharacterMaterialKind kind, float level, int seed = 29)
        {
            if (level <= 0f)
                return null;
            var scratches = kind == CharacterMaterialKind.HelmetPaint || kind == CharacterMaterialKind.Cordura;
            var mud = kind == CharacterMaterialKind.Leather || kind == CharacterMaterialKind.Rubber;
            if (!scratches && !mud)
                return null;
            var key = "gear|" + kind + "|" + level.ToString("0.00") + "|" + seed;
            if (WearCache.TryGetValue(key, out var t) && t != null)
                return t;
            t = ProceduralPbr.MakeTexture("HK_Wear_" + kind, DefaultSize * 2, GearWearPixels(DefaultSize * 2, level, seed, scratches, mud), false);
            WearCache[key] = t;
            return t;
        }

        /// <summary>Kamuflaj albedo'suna yıpranma uygular (önbellekli). Okunamazsa kaynak doku döner.</summary>
        public static Texture2D UniformWearAlbedo(Texture2D source, string sourceKey, float level, int seed)
        {
            if (source == null || level <= 0f)
                return source;
            var key = "uni|" + sourceKey + "|" + level.ToString("0.00");
            if (WearCache.TryGetValue(key, out var t) && t != null)
                return t;
            try
            {
                var src = source.GetPixels32();
                t = ProceduralPbr.MakeTexture("HK_Wear_Uniform", source.width, UniformWearPixels(src, level, seed), false);
                WearCache[key] = t;
                return t;
            }
            catch (System.Exception)
            {
                return source;
            }
        }
    }
}
