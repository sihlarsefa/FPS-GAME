using System.Collections.Generic;
using UnityEngine;

namespace Project.Infrastructure.Rendering
{
    /// <summary>Prosedürel PBR yüzey türü (albedo + normal + maske üçlüsü üretir).</summary>
    public enum PbrSurface
    {
        None = 0,
        Stone,
        Plaster,
        Brick,
        Wood,
        Metal,
        Concrete,
        CamoFabric,
        GunMetal,
        Polymer,
        Sand
    }

    /// <summary>Bir yüzeyin ham pikselleri (Unity nesnesi yok; EditMode testleri için). Mask: R metalik, G AO, B 255, A pürüzsüzlük çarpanı.</summary>
    public sealed class PbrPixels
    {
        public int Size;
        public float[] Height;
        public Color32[] Albedo;
        public Color32[] Mask;
        public float NormalStrength;
    }

    /// <summary>Üretilmiş doku üçlüsü (önbellekli, paylaşılan; yok etmeyin).</summary>
    public sealed class PbrTextureSet
    {
        public Texture2D Albedo;
        public Texture2D Normal;
        public Texture2D Mask;
        public bool IsValid => Albedo != null && Normal != null && Mask != null;
    }

    /// <summary>
    /// Yüksek doğruluklu, döşenebilir prosedürel PBR dokuları. Yükseklik haritası çok oktavlı gürültüyle üretilir,
    /// normal harita Sobel ile (kenarlar sarmalı) türetilir, maske URP Lit sözleşmesindedir:
    /// R = metalik, G = oklüzyon, A = pürüzsüzlük (malzemenin _Smoothness değeriyle çarpılır).
    /// Üretim tembeldir ve önbelleklidir. Çözünürlük editörde 1024, oyunda 512 (Resolution ile değiştirilebilir).
    /// </summary>
    public static class ProceduralPbr
    {
        public const int HighSize = 1024;

#if UNITY_EDITOR
        private static int _resolution = HighSize;
#else
        private static int _resolution = 512;
#endif
        private static readonly Dictionary<long, PbrTextureSet> Cache = new Dictionary<long, PbrTextureSet>();

        /// <summary>Varsayılan doku çözünürlüğü (2'nin kuvvetine yuvarlanır, 64..2048).</summary>
        public static int Resolution
        {
            get => _resolution;
            set => _resolution = Mathf.ClosestPowerOfTwo(Mathf.Clamp(value, 64, 2048));
        }

        /// <summary>Malzeme kimliğine karşılık gelen PBR yüzeyi (None = klasik düz malzeme).</summary>
        public static PbrSurface SurfaceFor(MaterialId id)
        {
            switch (id)
            {
                case MaterialId.Rock:
                case MaterialId.RockDark:
                case MaterialId.Stone:
                case MaterialId.StoneDark:
                case MaterialId.Gravel:
                    return PbrSurface.Stone;
                case MaterialId.Sand: return PbrSurface.Sand;
                case MaterialId.Concrete:
                case MaterialId.ConcreteDark:
                case MaterialId.Asphalt:
                    return PbrSurface.Concrete;
                case MaterialId.Plaster:
                case MaterialId.PlasterWarm:
                    return PbrSurface.Plaster;
                case MaterialId.Brick:
                case MaterialId.RoofTile:
                    return PbrSurface.Brick;
                case MaterialId.Wood:
                case MaterialId.WoodDark:
                case MaterialId.DeadWood:
                case MaterialId.Bark:
                case MaterialId.GunWood:
                    return PbrSurface.Wood;
                case MaterialId.MetalPanel:
                case MaterialId.MetalDark:
                case MaterialId.RoofMetal:
                case MaterialId.Rust:
                case MaterialId.MosqueDome:
                case MaterialId.VehicleOlive:
                case MaterialId.VehicleTan:
                case MaterialId.VehicleDark:
                case MaterialId.HeliOlive:
                    return PbrSurface.Metal;
                case MaterialId.GunMetal: return PbrSurface.GunMetal;
                case MaterialId.GunPolymer:
                case MaterialId.GunTan:
                    return PbrSurface.Polymer;
                case MaterialId.Gear:
                case MaterialId.TentCanvas:
                case MaterialId.Sandbag:
                case MaterialId.Hesco:
                case MaterialId.Beret:
                case MaterialId.Parachute:
                case MaterialId.CamoWoodland:
                case MaterialId.CamoMountain:
                case MaterialId.CamoDesert:
                case MaterialId.CamoUrban:
                    return PbrSurface.CamoFabric;
                default: return PbrSurface.None;
            }
        }

        /// <summary>Yüzeyin dokularını (albedo/normal/maske) üretir ya da önbellekten verir. size &lt;= 0 → Resolution.</summary>
        public static PbrTextureSet Get(PbrSurface surface, int seed, int size = 0)
        {
            if (surface == PbrSurface.None)
                return null;

            size = Mathf.ClosestPowerOfTwo(Mathf.Clamp(size <= 0 ? _resolution : size, 16, 2048));
            var key = ((long)(int)surface << 48) ^ ((long)size << 32) ^ (uint)seed;
            if (Cache.TryGetValue(key, out var cached) && cached != null && cached.IsValid)
                return cached;

            var px = Generate(surface, size, seed);
            var set = new PbrTextureSet
            {
                Albedo = MakeTexture("HK_PBR_" + surface + "_Albedo", size, px.Albedo, false),
                Normal = MakeTexture("HK_PBR_" + surface + "_Normal", size, NormalFromHeight(px.Height, size, px.NormalStrength), true),
                Mask = MakeTexture("HK_PBR_" + surface + "_Mask", size, px.Mask, true)
            };
            Cache[key] = set;
            return set;
        }

        /// <summary>Mevcut piksellerden doku üretir (editör PNG kaydı ve terrain katmanları için de kullanılır).</summary>
        public static Texture2D MakeTexture(string name, int size, Color32[] pixels, bool linear)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, linear)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4
            };
            tex.SetPixels32(pixels);
            tex.Apply(true, false);
            return tex;
        }

        // ================================================================== Normal / maske yardımcıları

        /// <summary>
        /// Yükseklikten (0..1, döşenebilir) Sobel ile teğet uzayı normal harita. Kenarlar sarmalıdır; RG kodlu, B = z, A = 255
        /// (UnpackNormal hem RG hem AG düzeninde çalışır). strength: bayağı kabartma gücü (~2..10).
        /// </summary>
        public static Color32[] NormalFromHeight(float[] h, int size, float strength)
        {
            var result = new Color32[size * size];
            var mask = size - 1; // size 2'nin kuvveti
            var scale = strength * 0.02f * size / 8f;
            for (var y = 0; y < size; y++)
            {
                var ym = ((y - 1) & mask) * size;
                var y0 = y * size;
                var yp = ((y + 1) & mask) * size;
                for (var x = 0; x < size; x++)
                {
                    var xm = (x - 1) & mask;
                    var xp = (x + 1) & mask;
                    var dx = (h[yp + xp] + 2f * h[y0 + xp] + h[ym + xp]) - (h[yp + xm] + 2f * h[y0 + xm] + h[ym + xm]);
                    var dy = (h[yp + xm] + 2f * h[yp + x] + h[yp + xp]) - (h[ym + xm] + 2f * h[ym + x] + h[ym + xp]);
                    var nx = -dx * scale;
                    var ny = -dy * scale;
                    var inv = 1f / Mathf.Sqrt(nx * nx + ny * ny + 1f);
                    result[y0 + x] = new Color32(Byte(nx * inv * 0.5f + 0.5f), Byte(ny * inv * 0.5f + 0.5f), Byte(inv * 0.5f + 0.5f), 255);
                }
            }

            return result;
        }

        /// <summary>Renk piksellerinden (ör. arazi dokusu) hafifçe yumuşatılmış parlaklık yükseklik haritası (0..1).</summary>
        public static float[] HeightFromLuminance(Color32[] pixels, int size)
        {
            var raw = new float[pixels.Length];
            for (var i = 0; i < raw.Length; i++)
                raw[i] = (pixels[i].r * 0.299f + pixels[i].g * 0.587f + pixels[i].b * 0.114f) / 255f;

            var mask = size - 1;
            var blur = new float[raw.Length];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var xm = (x - 1) & mask;
                    var xp = (x + 1) & mask;
                    var ym = (y - 1) & mask;
                    var yp = (y + 1) & mask;
                    blur[y * size + x] = (raw[y * size + x] * 4f + raw[y * size + xm] + raw[y * size + xp] + raw[ym * size + x] + raw[yp * size + x]) * 0.125f;
                }
            }

            return blur;
        }

        /// <summary>Yükseklikten maske (R metalik sabit, G oklüzyon: çukurlar koyu, A pürüzsüzlük çarpanı: tepeler biraz parlak).</summary>
        public static Color32[] MaskFromHeight(float[] h, int size, float metallic, float aoStrength, float smoothBase, float smoothVariation)
        {
            var mask = new Color32[h.Length];
            var min = float.MaxValue;
            var max = float.MinValue;
            for (var i = 0; i < h.Length; i++)
            {
                if (h[i] < min) min = h[i];
                if (h[i] > max) max = h[i];
            }

            var inv = 1f / Mathf.Max(0.0001f, max - min);
            for (var i = 0; i < h.Length; i++)
            {
                var t = (h[i] - min) * inv;
                mask[i] = new Color32(Byte(metallic), Byte(1f - aoStrength * (1f - t)), 255,
                    Byte(smoothBase + smoothVariation * (t - 0.5f)));
            }

            return mask;
        }

        // ================================================================== Yüzey üretimi

        private struct Sample
        {
            public float H;      // yükseklik 0..1
            public float Albedo; // gri ton çarpanı (malzeme rengiyle çarpılır)
            public float Ao;     // 0..1
            public float Smooth; // pürüzsüzlük çarpanı 0..1
            public float Metal;  // 0..1
        }

        /// <summary>Ham pikselleri üretir (Unity nesnesi gerektirmez). size 2'nin kuvveti olmalıdır.</summary>
        public static PbrPixels Generate(PbrSurface surface, int size, int seed)
        {
            size = Mathf.ClosestPowerOfTwo(Mathf.Clamp(size, 16, 2048));
            var result = new PbrPixels
            {
                Size = size,
                Height = new float[size * size],
                Albedo = new Color32[size * size],
                Mask = new Color32[size * size],
                NormalStrength = Strength(surface)
            };

            var inv = 1f / size;
            for (var py = 0; py < size; py++)
            {
                var v = py * inv;
                for (var px = 0; px < size; px++)
                {
                    var s = Shade(surface, px * inv, v, px, py, seed);
                    var i = py * size + px;
                    result.Height[i] = Mathf.Clamp01(s.H);
                    var a = Byte(s.Albedo);
                    result.Albedo[i] = new Color32(a, a, a, 255);
                    result.Mask[i] = new Color32(Byte(s.Metal), Byte(s.Ao), 255, Byte(s.Smooth));
                }
            }

            return result;
        }

        private static float Strength(PbrSurface s)
        {
            switch (s)
            {
                case PbrSurface.Stone: return 7f;
                case PbrSurface.Brick: return 8f;
                case PbrSurface.Wood: return 5f;
                case PbrSurface.Concrete: return 4f;
                case PbrSurface.Plaster: return 3f;
                case PbrSurface.Metal: return 3f;
                case PbrSurface.GunMetal: return 2.5f;
                case PbrSurface.CamoFabric: return 6f;
                case PbrSurface.Polymer: return 3f;
                case PbrSurface.Sand: return 4f;
                default: return 4f;
            }
        }

        private static Sample Shade(PbrSurface surface, float u, float v, int px, int py, int seed)
        {
            switch (surface)
            {
                case PbrSurface.Stone: return Stone(u, v, px, py, seed);
                case PbrSurface.Plaster: return Plaster(u, v, px, py, seed);
                case PbrSurface.Brick: return Brick(u, v, px, py, seed);
                case PbrSurface.Wood: return Wood(u, v, px, py, seed);
                case PbrSurface.Metal: return Metal(u, v, px, py, seed);
                case PbrSurface.Concrete: return Concrete(u, v, px, py, seed);
                case PbrSurface.CamoFabric: return Fabric(u, v, px, py, seed);
                case PbrSurface.GunMetal: return GunMetal(u, v, px, py, seed);
                case PbrSurface.Polymer: return Polymer(u, v, px, py, seed);
                case PbrSurface.Sand: return Sand(u, v, px, py, seed);
                default: return new Sample { H = 0.5f, Albedo = 1f, Ao = 1f, Smooth = 1f };
            }
        }

        private static Sample Stone(float u, float v, int px, int py, int seed)
        {
            var low = Fbm(u, v, 3, 6, seed);
            var mid = Fbm(u + 0.31f, v + 0.17f, 8, 4, seed + 7);
            // Çatlaklar: ridged gürültünün ince hattı.
            var ridge = 1f - Mathf.Abs(Fbm(u, v, 5, 4, seed + 31) * 2f - 1f);
            var crack = Mathf.Pow(Mathf.Clamp01(ridge), 14f);
            var grain = Hash01(px, py, seed + 3);
            var h = low * 0.55f + mid * 0.35f + grain * 0.06f - crack * 0.45f;
            return new Sample
            {
                H = h + 0.2f,
                Albedo = Mathf.Clamp01(0.66f + 0.24f * low + 0.04f * grain - crack * 0.3f),
                Ao = 1f - crack * 0.7f - Mathf.Clamp01(0.5f - low) * 0.25f,
                Smooth = 0.6f + 0.3f * mid,
                Metal = 0f
            };
        }

        private static Sample Plaster(float u, float v, int px, int py, int seed)
        {
            var low = Fbm(u, v, 3, 5, seed);
            var trowel = Fbm(u * 1f, v * 1f, 6, 4, seed + 9);
            var grain = Hash01(px, py, seed + 5);
            var speck = Hash01(px >> 1, py >> 1, seed + 21) > 0.992f ? 1f : 0f;
            var h = low * 0.5f + trowel * 0.3f + grain * 0.12f - speck * 0.25f;
            return new Sample
            {
                H = h + 0.25f,
                Albedo = Mathf.Clamp01(0.8f + 0.1f * low + 0.03f * grain - speck * 0.15f),
                Ao = 1f - speck * 0.4f - Mathf.Clamp01(0.4f - low) * 0.2f,
                Smooth = 0.45f + 0.15f * trowel,
                Metal = 0f
            };
        }

        private static Sample Brick(float u, float v, int px, int py, int seed)
        {
            const int rows = 8;
            const int cols = 4;
            var row = Mathf.FloorToInt(v * rows);
            var fv = v * rows - row;
            var uu = u * cols + ((row & 1) == 1 ? 0.5f : 0f);
            var col = Mathf.FloorToInt(uu);
            var fu = uu - col;
            var idx = ((col % cols) + cols) % cols;
            // Harç: tuğla kenarına uzaklık.
            var edge = Mathf.Min(Mathf.Min(fu, 1f - fu) * 0.5f, Mathf.Min(fv, 1f - fv));
            var mortarW = 0.09f;
            var inBrick = Mathf.Clamp01((edge - mortarW * 0.5f) / (mortarW * 0.6f));
            var tint = Hash01(idx, row, seed + 41);
            var wear = Fbm(u, v, 6, 5, seed + 3);
            var grain = Hash01(px, py, seed + 8);
            var h = inBrick * (0.62f + 0.22f * wear + 0.06f * tint) + (1f - inBrick) * 0.12f + grain * 0.05f * inBrick;
            var albedo = inBrick > 0.5f ? 0.62f + 0.3f * tint + 0.1f * wear : 0.95f - 0.08f * grain;
            return new Sample
            {
                H = h,
                Albedo = Mathf.Clamp01(albedo + 0.03f * grain),
                Ao = 0.35f + 0.65f * inBrick,
                Smooth = inBrick > 0.5f ? 0.55f + 0.25f * wear : 0.35f,
                Metal = 0f
            };
        }

        private static Sample Wood(float u, float v, int px, int py, int seed)
        {
            const int planks = 4;
            var plank = Mathf.FloorToInt(u * planks);
            var fu = u * planks - plank;
            var seam = Mathf.Clamp01(1f - Mathf.Min(fu, 1f - fu) * planks * 9f);
            var ph = Hash01(plank, 0, seed + 55);
            // Yıllık halkalar: uzun eksende gerilmiş, bükülmüş sinüs.
            var warp = Fbm(u, v, 3, 4, seed) * 2f - 1f;
            var longWarp = ValueNoise(u * planks * 2f, v * 2f + ph * 8f, planks * 2, 2, seed + 61);
            var rings = Mathf.Sin((fu * 7f + warp * 1.6f + longWarp * 1.2f + ph * 6f) * Mathf.PI * 2f) * 0.5f + 0.5f;
            var fiber = ValueNoise(u * 96f, v * 6f, 96, 6, seed + 71);
            var h = 0.55f + 0.2f * rings + 0.18f * fiber - seam * 0.5f;
            var pore = Hash01(px, py >> 3, seed + 9) > 0.93f ? 0.25f : 0f;
            return new Sample
            {
                H = h - pore * 0.2f,
                Albedo = Mathf.Clamp01(0.7f + 0.2f * rings + 0.1f * fiber + (ph - 0.5f) * 0.12f - seam * 0.4f - pore * 0.2f),
                Ao = 1f - seam * 0.7f - pore * 0.4f,
                Smooth = 0.5f + 0.25f * (1f - rings),
                Metal = 0f
            };
        }

        private static Sample Metal(float u, float v, int px, int py, int seed)
        {
            // Boyalı levha: 2x2 panel derzleri, perçinler, aşınmış (çıplak metal) kenar/çizikler.
            var cu = u * 2f;
            var cv = v * 2f;
            var fu = cu - Mathf.Floor(cu);
            var fv = cv - Mathf.Floor(cv);
            var edge = Mathf.Min(Mathf.Min(fu, 1f - fu), Mathf.Min(fv, 1f - fv));
            var groove = Mathf.Clamp01(1f - edge * 70f);
            var du = Mathf.Min(fu, 1f - fu) - 0.07f;
            var dv = fv - Mathf.Round(fv * 4f) * 0.25f;
            var rivet = Mathf.Clamp01(1f - Mathf.Sqrt(du * du + dv * dv) * 120f) * 0.5f;
            var brushed = ValueNoise(u * 4f, v * 128f, 4, 128, seed);
            var dent = Fbm(u, v, 3, 5, seed + 13);
            var scratch = Mathf.Pow(Mathf.Clamp01(ValueNoise(u * 64f, v * 3f, 64, 3, seed + 29) - 0.55f) * 2.2f, 2f);
            var chip = Mathf.Clamp01((Fbm(u, v, 6, 5, seed + 47) - 0.62f) * 5f);
            var bare = Mathf.Clamp01(chip + scratch * 0.7f);
            var h = 0.55f + 0.12f * dent + 0.05f * brushed - groove * 0.4f + rivet * 0.2f - bare * 0.06f;
            return new Sample
            {
                H = h,
                Albedo = Mathf.Clamp01(0.82f + 0.12f * dent + 0.06f * brushed - bare * 0.22f - groove * 0.35f),
                Ao = 1f - groove * 0.8f,
                Smooth = Mathf.Clamp01(0.7f + 0.15f * dent + bare * 0.2f - 0.1f * brushed),
                Metal = Mathf.Clamp01(0.3f + 0.7f * bare)
            };
        }

        private static Sample Concrete(float u, float v, int px, int py, int seed)
        {
            var low = Fbm(u, v, 3, 6, seed);
            var mid = Fbm(u, v, 12, 3, seed + 5);
            var grain = Hash01(px, py, seed + 3);
            var pit = Hash01(px >> 1, py >> 1, seed + 17) > 0.985f ? 1f : 0f;
            // Kalıp izi: ince yatay derz.
            var form = Mathf.Clamp01(1f - Mathf.Abs(v * 2f - Mathf.Round(v * 2f)) * 160f) * 0.5f;
            var h = low * 0.5f + mid * 0.3f + grain * 0.1f - pit * 0.3f - form * 0.3f;
            return new Sample
            {
                H = h + 0.25f,
                Albedo = Mathf.Clamp01(0.78f + 0.16f * low + 0.04f * grain - pit * 0.2f - form * 0.15f),
                Ao = 1f - pit * 0.6f - form * 0.4f - Mathf.Clamp01(0.4f - low) * 0.2f,
                Smooth = 0.4f + 0.2f * mid,
                Metal = 0f
            };
        }

        private static Sample Fabric(float u, float v, int px, int py, int seed)
        {
            // Dokuma: periyot tam sayı olduğundan döşenebilir.
            const float threads = 96f;
            var warp = Mathf.Sin(u * threads * Mathf.PI * 2f) * 0.5f + 0.5f;
            var weft = Mathf.Sin(v * threads * Mathf.PI * 2f) * 0.5f + 0.5f;
            var over = (((int)(u * threads) + (int)(v * threads)) & 1) == 0;
            var weave = over ? warp : weft;
            var lint = Hash01(px, py, seed + 6);
            var blotch = Fbm(u, v, 4, 5, seed);
            var h = 0.45f + 0.4f * weave + 0.06f * lint;
            return new Sample
            {
                H = h,
                Albedo = Mathf.Clamp01(0.7f + 0.15f * weave + 0.12f * blotch + 0.05f * lint),
                Ao = 0.6f + 0.4f * weave,
                Smooth = 0.25f + 0.1f * blotch,
                Metal = 0f
            };
        }

        private static Sample GunMetal(float u, float v, int px, int py, int seed)
        {
            var brushed = ValueNoise(u * 8f, v * 256f, 8, 256, seed);
            var fine = Hash01(px, py, seed + 4);
            var wear = Mathf.Clamp01((Fbm(u, v, 5, 5, seed + 19) - 0.6f) * 4f);
            var scratch = Mathf.Pow(Mathf.Clamp01(ValueNoise(u * 32f, v * 4f, 32, 4, seed + 37) - 0.6f) * 2.5f, 2f);
            var h = 0.55f + 0.14f * brushed + 0.06f * fine - scratch * 0.15f;
            return new Sample
            {
                H = h,
                Albedo = Mathf.Clamp01(0.8f + 0.1f * brushed + wear * 0.12f + scratch * 0.1f),
                Ao = 1f - 0.1f * (1f - brushed),
                Smooth = Mathf.Clamp01(0.75f - 0.2f * brushed - scratch * 0.2f + wear * 0.15f),
                Metal = Mathf.Clamp01(0.9f + 0.1f * wear)
            };
        }

        private static Sample Polymer(float u, float v, int px, int py, int seed)
        {
            // Tutuş dokusu: ince nokta (stipple) + yumuşak dalga.
            var cell = Hash01(px >> 1, py >> 1, seed + 2);
            var stipple = cell > 0.55f ? 1f : 0f;
            var soft = Fbm(u, v, 4, 5, seed);
            var h = 0.5f + 0.25f * stipple + 0.12f * soft;
            return new Sample
            {
                H = h,
                Albedo = Mathf.Clamp01(0.86f + 0.1f * soft - 0.08f * stipple),
                Ao = 1f - 0.25f * (1f - stipple) * 0.4f,
                Smooth = 0.5f - 0.2f * stipple + 0.1f * soft,
                Metal = 0f
            };
        }

        private static Sample Sand(float u, float v, int px, int py, int seed)
        {
            var warp = Fbm(u, v, 3, 4, seed) * 2f - 1f;
            var ripple = Mathf.Sin((v * 14f + warp * 1.4f) * Mathf.PI * 2f) * 0.5f + 0.5f;
            var dune = Fbm(u, v, 2, 5, seed + 11);
            var grain = Hash01(px, py, seed + 5);
            var h = 0.4f + 0.25f * ripple * (0.4f + dune) + 0.3f * dune + 0.06f * grain;
            return new Sample
            {
                H = h,
                Albedo = Mathf.Clamp01(0.82f + 0.12f * dune + 0.06f * grain - 0.05f * (1f - ripple)),
                Ao = 0.8f + 0.2f * ripple,
                Smooth = 0.3f + 0.1f * grain,
                Metal = 0f
            };
        }

        // ================================================================== Gürültü (döşenebilir)

        private static byte Byte(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);

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

        private static int Mod(int a, int m)
        {
            var r = a % m;
            return r < 0 ? r + m : r;
        }

        /// <summary>Anizotropik periyodik değer gürültüsü; x periyodu px, y periyodu py kafes hücresi.</summary>
        private static float ValueNoise(float x, float y, int periodX, int periodY, int seed)
        {
            var x0 = Mathf.FloorToInt(x);
            var y0 = Mathf.FloorToInt(y);
            var tx = x - x0;
            var ty = y - y0;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            var xa = Mod(x0, periodX);
            var xb = Mod(x0 + 1, periodX);
            var ya = Mod(y0, periodY);
            var yb = Mod(y0 + 1, periodY);
            var a = Hash01(xa, ya, seed);
            var b = Hash01(xb, ya, seed);
            var c = Hash01(xa, yb, seed);
            var d = Hash01(xb, yb, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        /// <summary>Döşenebilir fBm; u,v 0..1 (sarmal), basePeriod ilk oktavdaki hücre sayısı, her oktavda ikiye katlanır.</summary>
        private static float Fbm(float u, float v, int basePeriod, int octaves, int seed)
        {
            var sum = 0f;
            var amp = 0.5f;
            var norm = 0f;
            var period = basePeriod;
            for (var o = 0; o < octaves; o++)
            {
                sum += ValueNoise(u * period, v * period, period, period, seed + o * 1013) * amp;
                norm += amp;
                amp *= 0.5f;
                period *= 2;
            }

            return sum / norm;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Cache.Clear();
        }
    }
}
