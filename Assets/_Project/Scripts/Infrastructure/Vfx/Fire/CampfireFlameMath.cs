using UnityEngine;

namespace Project.Infrastructure.Vfx.Fire
{
    /// <summary>Kamp ateşi için saf mantık: kalite kademesine göre parçacık sayıları, kor nabzı ve alev dokusu alfa/renk fonksiyonu.</summary>
    public static class CampfireFlameMath
    {
        /// <summary>Kalite kademesi (0..3) → alev, kıvılcım ve duman parçacık üst sınırları.</summary>
        public static void CountsForTier(int tier, out int flame, out int sparks, out int smoke)
        {
            tier = Mathf.Clamp(tier, 0, 3);
            flame = 12 + tier * 10;     // 12, 22, 32, 42
            sparks = 14 + tier * 14;    // 14, 28, 42, 56
            smoke = 10 + tier * 8;      // 10, 18, 26, 34
        }

        /// <summary>Yavaş kor nabzı 0..1 (iki sinüsün karışımı, iç yumuşak).</summary>
        public static float EmberPulse(float time, float phase)
        {
            var a = 0.5f + 0.5f * Mathf.Sin(time * 1.3f + phase);
            var b = 0.5f + 0.5f * Mathf.Sin(time * 2.9f + phase * 1.7f);
            return Mathf.Clamp01(a * 0.75f + b * 0.25f);
        }

        /// <summary>Radyal gradyan x gürültü ile yumuşak alev alfası (0..1); kenarlarda tam 0, keskin kenar yok.</summary>
        public static float FlameAlpha(float u, float v, float seed)
        {
            var dx = (u - 0.5f) * 2f;
            var dy = (v - 0.5f) * 2f;
            var r = Mathf.Sqrt(dx * dx + dy * dy);
            if (r >= 1f)
                return 0f;
            var radial = 1f - r;
            radial = radial * radial * (3f - 2f * radial);   // smoothstep
            var n = Mathf.PerlinNoise(u * 4f + seed, v * 4f + seed * 0.7f) * 0.65f
                    + Mathf.PerlinNoise(u * 9f + seed * 1.3f, v * 9f) * 0.35f;
            return Mathf.Clamp01(radial * (0.45f + 0.9f * n));
        }

        /// <summary>Alev rengi: sıcak çekirdekte sarımsı-beyaz, kenarda turuncu-kızıl.</summary>
        public static Color FlameColor(float alpha)
        {
            var heat = Mathf.Clamp01(alpha * 1.5f);
            var c = Color.Lerp(new Color(0.8f, 0.12f, 0.02f), new Color(1f, 0.55f, 0.1f), Mathf.Clamp01(heat * 1.4f));
            return Color.Lerp(c, new Color(1f, 0.9f, 0.6f), Mathf.Clamp01((heat - 0.6f) * 2f));
        }

        /// <summary>Alev dokusunu üretir (karesel, yumuşak alfa).</summary>
        public static Texture2D BuildFlameTexture(int size, float seed)
        {
            size = Mathf.Clamp(size, 16, 256);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "HK_CampfireFlameSoft",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var a = FlameAlpha((x + 0.5f) / size, (y + 0.5f) / size, seed);
                    Color32 c = FlameColor(a);
                    c.a = (byte)Mathf.RoundToInt(a * 255f);
                    px[y * size + x] = c;
                }
            }

            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }
    }
}
