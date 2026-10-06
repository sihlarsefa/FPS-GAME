using System;
using Project.Application.Services;

namespace Project.Infrastructure.Weapons.Reload
{
    /// <summary>Silah çekme/koyma süreleri (saf): tabanca hızlı, ağır silah yavaş; ateş hazır eşiği (CoD: çekme ~%85'inde ateş edilebilir).</summary>
    public static class WeaponSwapTiming
    {
        public const float FireReadyFraction = 0.85f;

        public static float DrawSeconds(ReloadKind kind)
        {
            switch (kind)
            {
                case ReloadKind.Pistol: return 0.38f;
                case ReloadKind.Shotgun: return 0.62f;
                case ReloadKind.BoltAction: return 0.70f;
                case ReloadKind.Machinegun: return 0.85f;
                default: return 0.52f;
            }
        }

        /// <summary>Koyma çekmeden ~%65 kısadır (silah indirilirken yeni silah hemen çıkabilir).</summary>
        public static float HolsterSeconds(ReloadKind kind) => DrawSeconds(kind) * 0.65f;

        public static float FireReadySeconds(ReloadKind kind) => DrawSeconds(kind) * FireReadyFraction;

        /// <summary>Hızlı değişim toplamı: eski silahı indir + yenisini ateşe hazır çek.</summary>
        public static float SwapSeconds(ReloadKind from, ReloadKind to) => HolsterSeconds(from) + FireReadySeconds(to);

        /// <summary>Çekme sırasında şarjör yeni silahta tam yerindedir; boş kurma yok. Doldurma sonrası çekme iptali için kalan oran.</summary>
        public static float RemainingFraction(float elapsed, ReloadKind kind)
        {
            var d = DrawSeconds(kind);
            return d <= 1e-4f ? 0f : Math.Max(0f, Math.Min(1f, 1f - elapsed / d));
        }
    }
}
