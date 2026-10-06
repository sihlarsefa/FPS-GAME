using System;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// AmbientLife için saf (UnityEngine'siz) matematik: gün evresine göre seviyeler, kademe sayıları, rüzgâr esinti bandı,
    /// sürü yolu, duman/kuş/kelebek doku alfaları. Deterministik; birim testlenir.
    /// </summary>
    public static class AmbientLifeMath
    {
        public const float TwoPi = 6.2831853f;

        public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        public static float Smooth(float a, float b, float v)
        {
            if (Math.Abs(b - a) < 1e-6f) return v >= b ? 1f : 0f;
            var t = Clamp01((v - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Tamsayı karması -> [0,1). Aynı girdi her zaman aynı sonucu verir.</summary>
        public static float Hash01(int a, int b = 0, int c = 0)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)a) * 16777619u;
                h = (h ^ (uint)b) * 16777619u;
                h = (h ^ (uint)c) * 16777619u;
                h ^= h >> 15; h *= 2246822519u; h ^= h >> 13; h *= 3266489917u; h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        // ---- Kademe sınırları (0 Düşük .. 3 Ultra) ----
        public static int FlockCount(int tier) => tier <= 0 ? 0 : (tier == 1 ? 1 : (tier == 2 ? 2 : 3));
        public static int BirdsPerFlock(int flock, int seed) => 5 + (int)(Hash01(seed, flock, 11) * 5f) % 5; // 5..9
        public static int SmokeColumnCount(int tier) => tier <= 0 ? 0 : (tier == 1 ? 2 : (tier == 2 ? 5 : 9));
        public const int PuffsPerColumn = 7;
        public static int ButterflyCount(int tier) => tier <= 1 ? 0 : (tier == 2 ? 10 : 22);

        // ---- Gün evresi seviyeleri (güneş yüksekliği derece) ----
        /// <summary>Baca dumanı: şafak/alacakaranlık/gece güçlü, öğlen kapalı; soğuk haritada gün boyu.</summary>
        public static float SmokeLevel(float sunElevDeg, bool cold)
        {
            var b = 1f - Smooth(10f, 28f, sunElevDeg);
            return cold ? Math.Max(b, 0.85f) : b;
        }

        /// <summary>Kelebek/böcek: yüksek güneş, sıcak harita, yağmursuz.</summary>
        public static float ButterflyLevel(float sunElevDeg, bool cold, float rain)
        {
            if (cold) return 0f;
            return Smooth(28f, 45f, sunElevDeg) * (1f - Clamp01(rain * 3f));
        }

        /// <summary>Cırcır böceği katmanı (ENTEGRASYON Audio): gece, sıcak harita, yağmursuz.</summary>
        public static float CricketLevel(float sunElevDeg, bool cold, float rain)
        {
            if (cold) return 0f;
            return (1f - Smooth(-8f, 3f, sunElevDeg)) * (1f - Clamp01(rain * 2f));
        }

        /// <summary>Kuş sürüleri: gündüz ve alacakaranlıkta uçar, geceleyin ve yağmurda yok.</summary>
        public static float BirdLevel(float sunElevDeg, float rain)
        {
            return Smooth(-2f, 8f, sunElevDeg) * (1f - Clamp01(rain * 2.5f));
        }

        // ---- Rüzgâr esinti bandı ----
        /// <summary>
        /// Rüzgâr yönünde ilerleyen yavaş esinti dalgaları. along: rüzgâr ekseni boyunca konum (m). 0..1.
        /// </summary>
        public static float GustBand(float along, float t, float wavelength, float speed)
        {
            if (wavelength < 1f) wavelength = 1f;
            var ph = (along - speed * t) / wavelength;
            var w = 0.5f + 0.5f * (float)Math.Sin(TwoPi * ph);
            var slow = 0.5f + 0.5f * (float)Math.Sin(TwoPi * (ph * 0.37f + 0.21f));
            var g = w * w * (0.55f + 0.45f * slow);
            return Clamp01(g * 1.35f);
        }

        // ---- Sürü yolu ----
        public static void FlockPoint(float cx, float cz, float rx, float rz, float phase, float speedRad, float t,
            out float x, out float z)
        {
            var a = phase + speedRad * t;
            x = cx + rx * (float)Math.Cos(a);
            z = cz + rz * (float)Math.Sin(a * 1.0f) * (1f + 0.15f * (float)Math.Sin(a * 3f));
        }

        /// <summary>Ateş sonrası dağılma katsayısı s (0..1) -> zamanla sönüm (saniyede ~0.14).</summary>
        public static float ScatterDecay(float s, float dt) => Math.Max(0f, s - 0.14f * dt);

        /// <summary>Atış bu sürüyü ürkütür mü (mesafe m).</summary>
        public static bool ShotScatters(float distance, float radius) => distance <= radius;

        // ---- Duman parçacığı yaşam döngüsü: age01 0..1 ----
        public static void PuffState(float age01, out float size, out float alpha, out float rise)
        {
            age01 = Clamp01(age01);
            size = 0.7f + 3.6f * age01;
            alpha = Smooth(0f, 0.12f, age01) * (1f - Smooth(0.45f, 1f, age01)) * 0.55f;
            rise = 14f * age01 * (1f - 0.25f * age01);
        }

        // ---- Doku alfaları (u,v 0..1) ----
        public static float BirdAlpha(float u, float v)
        {
            // V biçimli kanat: merkezden dışa yukarı eğik ince çizgi.
            var dx = Math.Abs(u - 0.5f) * 2f;      // 0..1
            var line = 0.5f - dx * 0.30f;           // kanat ucu yukarı
            var d = Math.Abs(v - line);
            var thick = 0.07f * (1f - dx * 0.6f);
            var a = 1f - Smooth(thick * 0.5f, thick, d);
            return dx > 0.98f ? 0f : a;
        }

        public static float PuffAlpha(float u, float v)
        {
            var dx = u - 0.5f; var dy = v - 0.5f;
            var r = (float)Math.Sqrt(dx * dx + dy * dy) * 2f;
            return 1f - Smooth(0.25f, 1f, r);
        }

        public static float WingAlpha(float u, float v)
        {
            var dx = Math.Abs(u - 0.5f) * 2f; var dy = (v - 0.5f) * 2f;
            var body = dx < 0.12f && Math.Abs(dy) < 0.6f ? 1f : 0f;
            var wx = dx - 0.55f; var wy = dy * 0.9f;
            var wing = 1f - Smooth(0.25f, 0.45f, (float)Math.Sqrt(wx * wx + wy * wy));
            return Clamp01(Math.Max(body, wing * (dx > 0.1f ? 1f : 0f)));
        }
    }
}
