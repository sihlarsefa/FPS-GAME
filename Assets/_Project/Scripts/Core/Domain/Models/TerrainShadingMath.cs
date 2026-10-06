using System;

namespace Project.Core.Domain
{
    /// <summary>Arazi/zemin gölgelendirici kalite kademesi ayarları (S4). Kademe 0 Düşük ... 3 Ultra.</summary>
    public struct TerrainShadingTier
    {
        /// <summary>HAREKAT arazi gölgelendiricisine geçilsin mi (false: URP Terrain/Lit kalır).</summary>
        public bool UseHarekatTerrain;
        /// <summary>Yamaç triplanar gücü (0 = kapalı).</summary>
        public float CliffStrength;
        /// <summary>Yamaç eşiği: yüzey normali ile yukarı arasındaki açı (derece).</summary>
        public float CliffAngleDeg;
        /// <summary>Makro varyasyon gücü (0 = kapalı).</summary>
        public float MacroStrength;
        /// <summary>Detay normal gücü (0 = kapalı).</summary>
        public float DetailStrength;
        /// <summary>Detay normalin görüneceği mesafe (m).</summary>
        public float DetailDistance;
        /// <summary>Parallax Occlusion Mapping adım çarpanı (0 = kapalı).</summary>
        public float PomQuality;
        /// <summary>Küresel ıslaklık çarpanı (0 = ıslaklık yok).</summary>
        public float WetnessScale;
    }

    /// <summary>Zemin türü (PBR albedo parlaklık aralığı için).</summary>
    public enum PbrMaterialClass
    {
        Mud = 0,
        Soil = 1,
        Rock = 2,
        Grass = 3,
        Asphalt = 4,
        Snow = 5,
        Concrete = 6,
        Wood = 7
    }

    /// <summary>Çamur/zemin POM + ıslaklık ön ayarı.</summary>
    public struct SurfaceWetPreset
    {
        public float ParallaxDepth;
        public float WetResponse;
        public float BaseWet;
        public float PuddleLevel;
        public float WetDarken;
        public float WetSmooth;
        public float PuddleScale;
    }

    /// <summary>
    /// Saf arazi/zemin gölgelendirici matematiği (Unity bağımlılığı yok; EditMode testli). Shader tarafındaki formüllerin
    /// birebir aynası: HarekatTerrainExtras.hlsl ve HarekatParallaxLitPass.hlsl.
    /// </summary>
    public static class TerrainShadingMath
    {
        /// <summary>PBR standardı: doku yoğunluğu (piksel/metre).</summary>
        public const int TexelDensityPxPerMeter = 512;
        public const int MaxTextureSize = 4096;

        // Islaklık hızları (1/sn): yoğun yağmurda ~30 sn'de doyar, yağmursuz ~4 dk'da kurur.
        public const float WetRiseRate = 0.033f;
        public const float WetDryRate = 0.004f;

        public static TerrainShadingTier GetTier(int level)
        {
            switch (Clamp(level))
            {
                case 0:
                    return new TerrainShadingTier();
                case 1:
                    return new TerrainShadingTier
                    {
                        UseHarekatTerrain = true, MacroStrength = 0.12f, PomQuality = 0.5f, WetnessScale = 1f,
                        CliffAngleDeg = 35f, DetailDistance = 0f
                    };
                case 2:
                    return new TerrainShadingTier
                    {
                        UseHarekatTerrain = true, CliffStrength = 1f, CliffAngleDeg = 35f, MacroStrength = 0.2f,
                        DetailStrength = 0.6f, DetailDistance = 25f, PomQuality = 1f, WetnessScale = 1f
                    };
                default:
                    return new TerrainShadingTier
                    {
                        UseHarekatTerrain = true, CliffStrength = 1f, CliffAngleDeg = 32f, MacroStrength = 0.25f,
                        DetailStrength = 0.8f, DetailDistance = 45f, PomQuality = 1.5f, WetnessScale = 1f
                    };
            }
        }

        public static int Clamp(int level) => level < 0 ? 0 : (level > 3 ? 3 : level);

        /// <summary>Yamaç ağırlığı: normalY >= eşik -> 0, eşik-genişlik altı -> 1 (smoothstep).</summary>
        public static float SlopeWeight(float normalY, float threshold, float width)
        {
            var w = Math.Max(1e-4f, width);
            return 1f - SmoothStep(threshold - w, threshold, normalY);
        }

        /// <summary>Derece cinsinden yamaç eşiğini normal.y eşiğine çevirir (cos).</summary>
        public static float NormalYThreshold(float angleDeg)
            => (float)Math.Cos(Math.Max(1f, Math.Min(89f, angleDeg)) * Math.PI / 180.0);

        /// <summary>Islaklık entegrasyonu: yağmur 0-1 yükseltir, yağmursuzken kurur.</summary>
        public static float StepWetness(float wetness, float rain01, float dt)
        {
            var rain = Clamp01(rain01);
            var d = rain * WetRiseRate - (1f - rain) * WetDryRate;
            return Clamp01(wetness + d * Math.Max(0f, dt));
        }

        /// <summary>Birikinti maskesi: gürültü eşiği + düzlük (yalnız düz yüzeyde su durur).</summary>
        public static float PuddleMask(float noise01, float flatness, float coverage, float mud)
        {
            var cover = Clamp01(coverage + mud * 0.25f);
            var thr = 1f - cover;
            var p = SmoothStep(thr - 0.06f, thr + 0.06f, noise01);
            return p * SmoothStep(0.88f, 0.97f, flatness);
        }

        /// <summary>Birikinti seviyesi: ıslaklık 0.25 üstünde başlar.</summary>
        public static float PuddleLevel(float wetness) => Clamp01((wetness - 0.25f) / 0.75f);

        /// <summary>Islak albedo çarpanı (1 = kuru).</summary>
        public static float WetAlbedoMultiplier(float wet, float darken, float puddle)
            => Lerp(1f, darken, Clamp01(wet)) * Lerp(1f, 0.78f, Clamp01(puddle));

        /// <summary>Islak pürüzsüzlük: kuru değerden ıslak hedefe.</summary>
        public static float WetSmoothness(float dry, float wetTarget, float wet, float puddle)
            => Lerp(dry, wetTarget, Clamp01(wet * 0.55f + puddle));

        /// <summary>Normalin xy bileşeni birikintide ne kadar korunur (düzleşme).</summary>
        public static float NormalFlatten(float puddle) => 1f - Clamp01(puddle) * 0.92f;

        /// <summary>Mesafe makro sönmesi: yakında 0.35, uzakta 1.</summary>
        public static float MacroFade(float distance, float start, float end)
            => Lerp(0.35f, 1f, SmoothStep(start, Math.Max(end, start + 0.01f), distance));

        /// <summary>POM adım sayısı: bakış dikken min, eğikken max; kalite ile çarpılır; 4-64 arası.</summary>
        public static int PomSteps(float viewZ, float minSteps, float maxSteps, float quality)
        {
            if (quality <= 0.001f)
                return 0;
            var t = Clamp01(Math.Abs(viewZ));
            var s = Lerp(maxSteps, minSteps, t) * quality;
            return (int)Math.Max(4f, Math.Min(64f, s));
        }

        /// <summary>POM mesafe sönmesi (1 yakın, 0 uzak).</summary>
        public static float PomFade(float distance, float start, float end)
            => 1f - SmoothStep(start, Math.Max(end, start + 0.01f), distance);

        public static SurfaceWetPreset MudPreset() => new SurfaceWetPreset
        {
            ParallaxDepth = 0.05f, WetResponse = 1f, BaseWet = 0.35f, PuddleLevel = 0.5f, WetDarken = 0.55f, WetSmooth = 0.93f, PuddleScale = 5f
        };

        public static SurfaceWetPreset GroundPreset() => new SurfaceWetPreset
        {
            ParallaxDepth = 0.035f, WetResponse = 0.6f, BaseWet = 0f, PuddleLevel = 0.35f, WetDarken = 0.65f, WetSmooth = 0.9f, PuddleScale = 6f
        };

        public static SurfaceWetPreset RockPreset() => new SurfaceWetPreset
        {
            ParallaxDepth = 0.03f, WetResponse = 0.35f, BaseWet = 0f, PuddleLevel = 0.15f, WetDarken = 0.7f, WetSmooth = 0.85f, PuddleScale = 8f
        };

        // ---------------- PBR standardı ----------------

        /// <summary>sRGB (gama) değerlerden 0-255 algısal parlaklık (Rec.709 ağırlıkları).</summary>
        public static float AlbedoLuminance(float r, float g, float b)
            => 255f * (0.2126f * Clamp01(r) + 0.7152f * Clamp01(g) + 0.0722f * Clamp01(b));

        /// <summary>Zemin türüne göre izinli albedo parlaklık aralığı (sRGB 0-255). Genel sınır: 30-240.</summary>
        public static void AlbedoRange(PbrMaterialClass cls, out float min, out float max)
        {
            switch (cls)
            {
                case PbrMaterialClass.Mud: min = 30f; max = 75f; break;
                case PbrMaterialClass.Soil: min = 45f; max = 115f; break;
                case PbrMaterialClass.Rock: min = 55f; max = 145f; break;
                case PbrMaterialClass.Grass: min = 35f; max = 100f; break;
                case PbrMaterialClass.Asphalt: min = 35f; max = 85f; break;
                case PbrMaterialClass.Snow: min = 200f; max = 240f; break;
                case PbrMaterialClass.Concrete: min = 100f; max = 195f; break;
                default: min = 40f; max = 130f; break; // Wood
            }
        }

        public static bool IsAlbedoInRange(PbrMaterialClass cls, float r, float g, float b)
        {
            AlbedoRange(cls, out var min, out var max);
            var l = AlbedoLuminance(r, g, b);
            return l >= min && l <= max;
        }

        /// <summary>Hedef 512 px/m için, tileMeters boyundaki bir karoya gereken doku boyu (2'nin kuvveti, 4096 sınırı).</summary>
        public static int TextureSizeForTile(float tileMeters)
        {
            var px = Math.Max(1f, tileMeters) * TexelDensityPxPerMeter;
            var size = 64;
            while (size < px && size < MaxTextureSize)
                size <<= 1;
            return size;
        }

        /// <summary>Doku boyundan 512 px/m ile karo dünya boyu (m).</summary>
        public static float TileMetersForTexture(int textureSize)
            => Math.Max(1, textureSize) / (float)TexelDensityPxPerMeter;

        /// <summary>Gerçek texel yoğunluğu (px/m) ve hedefe oranı (1 = tam isabet).</summary>
        public static float TexelDensityRatio(int textureSize, float tileMeters)
            => tileMeters <= 0f ? 0f : textureSize / tileMeters / TexelDensityPxPerMeter;

        // ORM(H) paketleme: kanal sırası dosya adı soneki "_ORMH". R=AO, G=Pürüzlülük, B=Metalik, A=Yükseklik (lineer, sRGB değil).
        public const string OrmSuffix = "_ORMH";

        /// <summary>Pürüzsüzlüğü pürüzlülüğe (G kanalı) çevirir.</summary>
        public static float RoughnessFromSmoothness(float smoothness) => 1f - Clamp01(smoothness);

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        private static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);

        private static float SmoothStep(float a, float b, float x)
        {
            if (b == a)
                return x < a ? 0f : 1f;
            var t = Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }
    }
}
