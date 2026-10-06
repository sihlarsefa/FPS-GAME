using System;
using Project.Core.Domain;

namespace Project.Application.Combat.Ballistics
{
    /// <summary>
    /// Namlu çıkış hızı varyansı: atış başına küçük, deterministik (tohumlu) sapma. Dağılım kırpılmış normaldir
    /// (±3 sigma). Rastgele girdiler dışarıdan verilir; sunucu ve istemci aynı sonucu üretir.
    /// </summary>
    public static class MuzzleVelocityVariance
    {
        public const float MaxSigmas = 3f;

        /// <summary>Kalibre başına standart sapma (nominal hızın oranı).</summary>
        public static float SigmaFraction(AmmoType ammo)
        {
            switch (ammo)
            {
                case AmmoType.Mm762: return 0.008f;
                case AmmoType.Mm556: return 0.009f;
                case AmmoType.Mm9: return 0.012f;
                case AmmoType.Gauge12: return 0.020f;
                default: return 0.010f;
            }
        }

        /// <summary>Namlu ısınması (0..1) varyansı artırır: tam ısınmada x1.5.</summary>
        public static float HeatMultiplier(float heat01)
        {
            var h = heat01 < 0f ? 0f : (heat01 > 1f ? 1f : heat01);
            return 1f + 0.5f * h;
        }

        /// <summary>Box-Muller: iki (0,1) tekdüze sayıdan standart normal değer (±3'e kırpılır).</summary>
        public static float StandardNormal(float u1, float u2)
        {
            var a = u1 < 1e-6f ? 1e-6f : (u1 > 1f ? 1f : u1);
            var z = (float)(Math.Sqrt(-2.0 * Math.Log(a)) * Math.Cos(2.0 * Math.PI * u2));
            return z < -MaxSigmas ? -MaxSigmas : (z > MaxSigmas ? MaxSigmas : z);
        }

        /// <summary>Varyanslı namlu hızı (m/sn). Nominal &lt;= 0 ise 0 döner.</summary>
        public static float Sample(float nominal, AmmoType ammo, float u1, float u2, float heat01 = 0f)
        {
            if (nominal <= 0f) return 0f;
            var sigma = SigmaFraction(ammo) * HeatMultiplier(heat01);
            return nominal * (1f + sigma * StandardNormal(u1, u2));
        }

        /// <summary>Tohumdan [0,1) değeri (xorshift/FNV karışımı; kanal başına bağımsız).</summary>
        public static float Roll01(uint seed, int channel)
        {
            unchecked
            {
                var h = seed ^ ((uint)channel * 0x9E3779B1u);
                h ^= h >> 16; h *= 0x85EBCA6Bu;
                h ^= h >> 13; h *= 0xC2B2AE35u;
                h ^= h >> 16;
                return (h >> 8) * (1f / 16777216f);
            }
        }

        public static float SampleSeeded(float nominal, AmmoType ammo, uint seed, float heat01 = 0f) =>
            Sample(nominal, ammo, Roll01(seed, 21), Roll01(seed, 22), heat01);
    }
}
