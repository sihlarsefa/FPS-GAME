using System;
using UnityEngine;

namespace Project.Presentation.UI
{
    /// <summary>Mermi sayacı uyarı kademesi.</summary>
    public enum AmmoTier
    {
        Normal = 0,
        Low = 1,
        Critical = 2,
        Empty = 3
    }

    /// <summary>
    /// Şarjör "pip" çubuğu ve mermi uyarısı saf hesapları (EditMode testli). Pip sayısı üst sınırlıdır (okunaklılık):
    /// büyük şarjörlerde her pip birden çok mermiyi temsil eder. Düşük mermi eşiği şarjör boyutunun %33'ü (en az 3),
    /// kritik eşik %15'idir (en az 1) — CoD/Battlefield tarzı kademeli uyarı.
    /// </summary>
    public static class AmmoPipLayout
    {
        public const int MaxPips = 30;
        public const float LowFraction = 0.33f;
        public const float CriticalFraction = 0.15f;

        public struct Pips
        {
            public int Count;
            public int Filled;
            public int RoundsPerPip;
            /// <summary>Her 5 pipte bir ayraç boşluğu (okunurluk için gruplama).</summary>
            public int GroupSize;
        }

        public static AmmoTier Tier(int ammo, int magazineSize)
        {
            if (ammo <= 0)
                return AmmoTier.Empty;
            if (magazineSize <= 0)
                return AmmoTier.Normal;
            var critical = Mathf.Max(1, Mathf.FloorToInt(magazineSize * CriticalFraction));
            var low = Mathf.Max(3, Mathf.FloorToInt(magazineSize * LowFraction));
            if (ammo <= critical)
                return AmmoTier.Critical;
            return ammo <= low ? AmmoTier.Low : AmmoTier.Normal;
        }

        public static Pips Compute(int ammo, int magazineSize)
        {
            magazineSize = Mathf.Max(0, magazineSize);
            ammo = Mathf.Clamp(ammo, 0, Mathf.Max(magazineSize, ammo));
            var per = Mathf.Max(1, Mathf.CeilToInt(magazineSize / (float)MaxPips));
            var count = per <= 1 ? magazineSize : Mathf.CeilToInt(magazineSize / (float)per);
            var filled = per <= 1 ? ammo : Mathf.CeilToInt(Mathf.Min(ammo, magazineSize) / (float)per);
            return new Pips { Count = count, Filled = Mathf.Min(filled, count), RoundsPerPip = per, GroupSize = 5 };
        }

        /// <summary>i. pipin X konumu (soldan, px): pipler arası boşluk + her grup sonunda ek ayraç.</summary>
        public static float PipX(int index, float pipWidth, float gap, float groupGap, int groupSize)
        {
            var groups = groupSize > 0 ? index / groupSize : 0;
            return index * (pipWidth + gap) + groups * groupGap;
        }

        /// <summary>Toplam şerit genişliği (px).</summary>
        public static float TotalWidth(int count, float pipWidth, float gap, float groupGap, int groupSize)
        {
            if (count <= 0)
                return 0f;
            return PipX(count - 1, pipWidth, gap, groupGap, groupSize) + pipWidth;
        }

        /// <summary>Kademe rengi: normal beyaz, düşük kehribar, kritik/boş imza kırmızısı.</summary>
        public static Color TierColor(AmmoTier tier)
        {
            switch (tier)
            {
                case AmmoTier.Low: return UiTheme.Amber;
                case AmmoTier.Critical:
                case AmmoTier.Empty: return HudRules.SignatureRed;
                default: return Color.white;
            }
        }

        /// <summary>Kritik/boş kademede sayı nabzı (0..1); diğerlerinde 0. Frekans 3 Hz.</summary>
        public static float WarnPulse(AmmoTier tier, float time)
        {
            if (tier < AmmoTier.Critical)
                return 0f;
            return 0.5f + 0.5f * Mathf.Sin(time * 2f * Mathf.PI * 3f);
        }

        /// <summary>Yedek şarjör pipleri (en çok 6): şarjör sayısı = ceil(yedek / şarjör boyutu).</summary>
        public static int ReserveMagazines(int reserveRounds, int magazineSize)
        {
            if (reserveRounds <= 0 || magazineSize <= 0)
                return 0;
            return Mathf.Min(6, Mathf.CeilToInt(reserveRounds / (float)magazineSize));
        }

        /// <summary>"Yeniden doldur" ipucu: şarjör düşük/boşken ve yedek varken görünür.</summary>
        public static bool ShowReloadHint(AmmoTier tier, int reserveRounds, bool reloading) =>
            !reloading && reserveRounds > 0 && tier >= AmmoTier.Critical;
    }
}
