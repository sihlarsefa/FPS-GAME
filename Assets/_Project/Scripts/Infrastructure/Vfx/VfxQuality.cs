using UnityEngine;

namespace Project.Infrastructure.Vfx
{
    /// <summary>Efekt kalite kademesi (Düşük/Orta/Yüksek).</summary>
    public enum VfxTier
    {
        Low = 0,
        Medium = 1,
        High = 2
    }

    /// <summary>
    /// Kalite kademesine göre parçacık sayısı / havuz üst sınırı ölçekleri. Saf yardımcılar (FromLevel, CountFactor,
    /// Scale, ...) Unity gerektirmez ve test edilebilir; <see cref="Tier"/> QualitySettings'ten okur.
    /// </summary>
    public static class VfxQuality
    {
        private static VfxTier _tier = VfxTier.High;
        private static float _nextRead;

        /// <summary>Geçerli kademe (yaklaşık saniyede bir yenilenir; hata olursa Yüksek).</summary>
        public static VfxTier Tier
        {
            get
            {
                float now;
                try { now = Time.realtimeSinceStartup; }
                catch { return _tier; }

                if (now >= _nextRead)
                {
                    _nextRead = now + 1f;
                    try { _tier = FromLevel(QualitySettings.GetQualityLevel(), QualitySettings.names.Length); }
                    catch { _tier = VfxTier.High; }
                }

                return _tier;
            }
        }

        /// <summary>Kademeyi zorla ayarlar (test / ayar menüsü).</summary>
        public static void Override(VfxTier tier)
        {
            _tier = tier;
            _nextRead = float.MaxValue;
        }

        /// <summary>Zorlamayı kaldırır.</summary>
        public static void ClearOverride()
        {
            _nextRead = 0f;
        }

        public static float CountFactor => CountFactorFor(Tier);

        public static VfxTier FromLevel(int level, int levelCount)
        {
            if (levelCount <= 1)
                return VfxTier.High;

            var t = Mathf.Clamp01(level / (float)(levelCount - 1));
            if (t < 0.34f) return VfxTier.Low;
            if (t < 0.67f) return VfxTier.Medium;
            return VfxTier.High;
        }

        public static float CountFactorFor(VfxTier tier)
        {
            switch (tier)
            {
                case VfxTier.Low: return 0.4f;
                case VfxTier.Medium: return 0.7f;
                default: return 1f;
            }
        }

        /// <summary>Pozitif sayıyı kademeye göre ölçekler; en az 1 kalır, 0 → 0.</summary>
        public static int Scale(int count)
        {
            return ScaleFor(count, Tier);
        }

        public static int ScaleFor(int count, VfxTier tier)
        {
            if (count <= 0)
                return 0;
            return Mathf.Max(1, Mathf.RoundToInt(count * CountFactorFor(tier)));
        }

        /// <summary>Pul (kovan) havuzu üst sınırı.</summary>
        public static int CasingCap(VfxTier tier)
        {
            switch (tier)
            {
                case VfxTier.Low: return 8;
                case VfxTier.Medium: return 16;
                default: return 24;
            }
        }

        /// <summary>Mermi deliği havuzu üst sınırı (GameVfx.MaxDecals içinde).</summary>
        public static int DecalCap(VfxTier tier, int max)
        {
            switch (tier)
            {
                case VfxTier.Low: return Mathf.Max(8, Mathf.RoundToInt(max * 0.4f));
                case VfxTier.Medium: return Mathf.Max(8, Mathf.RoundToInt(max * 0.7f));
                default: return max;
            }
        }

        /// <summary>Patlama yanık izi havuzu üst sınırı (kademe başına).</summary>
        public static int ScorchCap(VfxTier tier, int max)
        {
            switch (tier)
            {
                case VfxTier.Low: return Mathf.Max(4, Mathf.RoundToInt(max * 0.35f));
                case VfxTier.Medium: return Mathf.Max(4, Mathf.RoundToInt(max * 0.65f));
                default: return max;
            }
        }

        /// <summary>Rotor tozunun çıkabileceği en yüksek zemin mesafesi (m).</summary>
        public const float RotorDustMaxHeight = 15f;

        /// <summary>Rotor tozu yalnızca zemine 15 m'den yakınken çıkar (yükseklik zeminden ölçülür).</summary>
        public static bool RotorDustAllowed(float heightAboveGround)
        {
            return !float.IsNaN(heightAboveGround) && heightAboveGround >= 0f && heightAboveGround < RotorDustMaxHeight;
        }

        /// <summary>Namlu dumanı yalnızca Orta/Yüksek kademede; en az bu aralıkla (sn) tekrarlanır.</summary>
        public static float MuzzleSmokeInterval(VfxTier tier)
        {
            return tier == VfxTier.High ? 0.12f : tier == VfxTier.Medium ? 0.3f : float.PositiveInfinity;
        }

        /// <summary>Düşük kademede kapatılan isteğe bağlı katmanlar (kalıcı duman sütunu, rotor halkası...) için.</summary>
        public static bool AllowOptional(VfxTier tier)
        {
            return tier != VfxTier.Low;
        }
    }
}
