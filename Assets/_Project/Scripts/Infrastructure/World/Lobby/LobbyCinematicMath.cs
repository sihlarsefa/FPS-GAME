using UnityEngine;

namespace Project.Infrastructure.World.Lobby
{
    /// <summary>Lobi sinematik sahnesinin saf matematiği (sahnesiz test edilir): titreşim, parallax, kalite kademesi bütçeleri.</summary>
    public static class LobbyCinematicMath
    {
        /// <summary>Kamp ateşi ışığı çarpanı (0.6..1.3 civarı, sürekli ve deterministik).</summary>
        public static float FireFlicker(float t, float seed)
        {
            var slow = Mathf.PerlinNoise(t * 3.2f, seed);
            var fast = Mathf.PerlinNoise(t * 14f, seed + 7.3f);
            return 0.7f + 0.45f * slow + 0.15f * fast;
        }

        /// <summary>Direk lambası: neredeyse sabit, nadiren hafif sönme.</summary>
        public static float LampFlicker(float t, float seed)
        {
            var n = Mathf.PerlinNoise(t * 1.4f, seed);
            var dip = n < 0.12f ? 0.82f + n : 1f;
            return Mathf.Clamp(dip * (0.97f + 0.03f * Mathf.PerlinNoise(t * 9f, seed + 3f)), 0.7f, 1.03f);
        }

        /// <summary>İmleç/zaman tabanlı yavaş parallax; (-1..1) girdi, metre cinsinden kamera kayması. Sınırlıdır.</summary>
        public static Vector3 ParallaxOffset(Vector2 pointer, float time, float maxMeters)
        {
            var px = Mathf.Clamp(pointer.x, -1f, 1f);
            var py = Mathf.Clamp(pointer.y, -1f, 1f);
            var drift = Mathf.Sin(time * 0.11f) * 0.25f;
            return new Vector3((px * 0.75f + drift) * maxMeters, py * 0.35f * maxMeters, 0f);
        }

        /// <summary>Yumuşak takip: hedefe doğru üstel yaklaşma (kare hızından bağımsız).</summary>
        public static Vector3 Damp(Vector3 current, Vector3 target, float dt, float rate)
            => Vector3.Lerp(current, target, 1f - Mathf.Exp(-Mathf.Max(0f, rate) * Mathf.Max(0f, dt)));

        /// <summary>Uzak katmanın parallax çarpanı: ufuk (derinlik büyük) neredeyse sabit kalır.</summary>
        public static float LayerFactor(float depthMeters) => 1f / (1f + Mathf.Max(0f, depthMeters) * 0.02f);

        /// <summary>Kalite kademesine (0 Düşük..3 Ultra) göre direk lambası sayısı.</summary>
        public static int LampCount(int tier) => tier <= 0 ? 2 : tier == 1 ? 3 : 4;

        /// <summary>Yer sisi parçacık üst sınırı (Düşük'te kapalı değil, çok az).</summary>
        public static int FogParticles(int tier) => tier <= 0 ? 12 : tier == 1 ? 24 : tier == 2 ? 40 : 64;

        /// <summary>Kıvılcım parçacık üst sınırı.</summary>
        public static int EmberParticles(int tier) => tier <= 0 ? 10 : tier == 1 ? 20 : tier == 2 ? 36 : 56;

        /// <summary>Direk lambası gölge verir mi (yalnız Ultra).</summary>
        public static bool LampShadows(int tier) => tier >= 3;
    }
}
