using System;

namespace Project.Infrastructure.Rendering
{
    /// <summary>Su cilası için saf matematik (Unity bağımlılığı yok): köpük genişliği/fazı, rüzgâr dalga yönü, parıltı ve ışık huzmesi parametreleri.</summary>
    public static class WaterSurfaceMath
    {
        public const float FoamMinWidth = 1.2f;
        public const float FoamMaxWidth = 2.0f;
        public const float WindBlendSeconds = 30f;
        public const float MaxWaveRotationDeg = 60f;
        public const float ReferenceWindDeg = 65f;
        public const int MaxShafts = 3;

        /// <summary>Köpük bandının ekranda ~targetPx piksel kalması için dünya genişliği (m), [1,2 - 2] aralığına kısılır.</summary>
        public static float FoamWidth(float cameraDistance, float fovDeg, float screenHeightPx, float targetPx = 14f)
        {
            if (screenHeightPx < 1f) return FoamMinWidth;
            var d = Math.Max(0.5f, cameraDistance);
            var fov = Math.Min(170f, Math.Max(5f, fovDeg)) * (float)Math.PI / 180f;
            var perPixel = 2f * d * (float)Math.Tan(fov * 0.5f) / screenHeightPx;
            return Clamp(perPixel * Math.Max(1f, targetPx), FoamMinWidth, FoamMaxWidth);
        }

        /// <summary>Köpük dokusunun enine ölçeği: bant genişliği büyüdükçe doku gerilir (görünür genişlik sabit kalır).</summary>
        public static float FoamScaleX(float width) => Clamp(1.6f / Clamp(width, FoamMinWidth, FoamMaxWidth), 0.8f, 1.34f);

        /// <summary>Kaydırma fazı 0..1: period saniyede bir döner; layer 0/1 yarım periyot arayla.</summary>
        public static float FoamPhase(float time, float period, int layer)
        {
            var p = Math.Max(0.1f, period);
            var f = time / p + (layer == 0 ? 0f : 0.5f);
            return f - (float)Math.Floor(f);
        }

        /// <summary>Faz kenarlarında sönen üçgen/sinüs ağırlığı 0..1 (iki katman toplamı yaklaşık sabit).</summary>
        public static float FoamWeight(float phase) => (float)Math.Sin(Clamp(phase, 0f, 1f) * Math.PI);

        /// <summary>İki açı arasındaki en kısa fark (-180..180).</summary>
        public static float DeltaAngle(float from, float to)
        {
            var d = (to - from) % 360f;
            if (d > 180f) d -= 360f;
            if (d < -180f) d += 360f;
            return d;
        }

        /// <summary>Açıyı hedefe üstel yaklaştırır (tau ~ 30 sn).</summary>
        public static float ApproachAngle(float current, float target, float dt, float tau = WindBlendSeconds)
        {
            var k = 1f - (float)Math.Exp(-Math.Max(0f, dt) / Math.Max(0.01f, tau));
            return current + DeltaAngle(current, target) * k;
        }

        /// <summary>Rüzgâr yönünden (x,z) derece (0 = +z, saat yönü).</summary>
        public static float WindAngleDeg(float dirX, float dirZ)
        {
            if (dirX * dirX + dirZ * dirZ < 1e-6f) return ReferenceWindDeg;
            return (float)(Math.Atan2(dirX, dirZ) * 180.0 / Math.PI);
        }

        /// <summary>Shader _WaveRot (radyan): rüzgâr açısının referanstan farkı, ±60 derece ile sınırlı.</summary>
        public static float WaveRotationRad(float windAngleDeg)
        {
            var d = Clamp(DeltaAngle(ReferenceWindDeg, windAngleDeg), -MaxWaveRotationDeg, MaxWaveRotationDeg);
            return -d * (float)Math.PI / 180f; // yön dönüşü: saat yönü açı artışı = shader'da -z yönüne dönüş
        }

        /// <summary>Kademe başına güneş parıltısı gücü (0 = kapalı); shader aralığı 0..2.</summary>
        public static float GlitterStrength(int tier)
        {
            switch (tier)
            {
                case 0: return 0f;
                case 1: return 0.45f;
                case 2: return 0.9f;
                default: return 1.3f;
            }
        }

        /// <summary>Alçak güneş çarpanı 0..1 (CPU tarafı test/telemetri; shader ile aynı eğri): güneş yüksekliği (sin elevasyon).</summary>
        public static float LowSunFactor(float sunDirY)
        {
            var a = Clamp((0.45f - sunDirY) / 0.4f, 0f, 1f);
            var b = Clamp(sunDirY * 12f + 0.6f, 0f, 1f);
            return a * b;
        }

        /// <summary>Su altı huzme sayısı: yalnız kademe >=2 (2 → 2 adet, 3 → 3 adet).</summary>
        public static int ShaftCount(int tier) => tier >= 3 ? 3 : tier == 2 ? 2 : 0;

        /// <summary>Huzme genel alfa 0..0.22: su altı karışımı, güneş görünürlüğü ve kademeye bağlı.</summary>
        public static float ShaftAlpha(float underwaterBlend, bool sunVisible, float sunDirY, int tier)
        {
            if (!sunVisible || ShaftCount(tier) == 0) return 0f;
            var sunUp = Clamp(-sunDirY * 3f, 0f, 1f); // ışık aşağı bakar (dir.y < 0 = güneş yukarıda)
            return Clamp(underwaterBlend, 0f, 1f) * sunUp * 0.22f;
        }

        /// <summary>Huzme i'nin yavaş dönüş açısı (derece).</summary>
        public static float ShaftAngle(float time, int index) => index * 120f + time * (4f + index * 1.5f);

        /// <summary>Huzme yatay yarıçap ofseti (m): kamera çevresinde 3-7 m.</summary>
        public static float ShaftRadius(int index) => 3f + index * 2f;

        /// <summary>Kıyı köpüğü iki katman alfa tabanı (0..1): kaydırma ağırlığı ile çarpılır.</summary>
        public static float FoamAlpha(float weight, float pulse) => Clamp(weight * (0.82f + 0.18f * pulse), 0f, 1f);

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
