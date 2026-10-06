using System;
using Project.Core.Domain;

namespace Project.Application.Services
{
    /// <summary>Kalibre/sınıf bazlı ateş hissi satırı: mevcut recoil üstüne çarpan katmanı.</summary>
    public readonly struct FireFeel
    {
        public readonly float KickPitch;
        public readonly float KickYaw;
        public readonly float FlashScale;
        public readonly float SmokeRate;
        /// <summary>Atış başına birikecek süreğen titreşim (derece, MG).</summary>
        public readonly float ShakePerShot;
        /// <summary>Pompalı pump gecikmesi (sn); 0 = yok.</summary>
        public readonly float PumpDelay;

        public FireFeel(float kickPitch, float kickYaw, float flashScale, float smokeRate, float shakePerShot = 0f, float pumpDelay = 0f)
        {
            KickPitch = kickPitch;
            KickYaw = kickYaw;
            FlashScale = flashScale;
            SmokeRate = smokeRate;
            ShakePerShot = shakePerShot;
            PumpDelay = pumpDelay;
        }
    }

    /// <summary>
    /// Saf ateş hissi tablosu (Unity yok). Mevcut RecoilVertical/Horizontal değerlerine dokunmaz;
    /// yalnızca kamera vuruşu/flaş/duman katmanını çarpar.
    /// </summary>
    public static class FireFeelRules
    {
        public const float FirstShotKick762 = 1.3f;
        public const float FirstShotFlash762 = 1.4f;
        public const float SuppressedKick = 0.6f;
        public const float MaxShake = 0.35f;
        public const float ShakeDecayPerSecond = 1.5f;
        public const float PumpRebound = 0.35f;

        // Tablo: kickPitch / kickYaw / flashScale / smokeRate / shake / pumpDelay
        private static readonly FireFeel Pistol = new FireFeel(1.00f, 1.00f, 0.90f, 0.5f);
        private static readonly FireFeel Smg9 = new FireFeel(0.90f, 0.90f, 0.90f, 0.7f);
        private static readonly FireFeel Rifle556 = new FireFeel(0.85f, 0.80f, 1.00f, 1.0f);
        private static readonly FireFeel Rifle762 = new FireFeel(1.15f, 1.10f, 1.25f, 1.2f);
        private static readonly FireFeel Dmr = new FireFeel(1.20f, 1.10f, 1.30f, 1.1f);
        private static readonly FireFeel Sniper = new FireFeel(1.30f, 1.00f, 1.40f, 1.0f);
        private static readonly FireFeel Shotgun = new FireFeel(1.35f, 1.20f, 1.35f, 1.3f, 0f, 0.35f);
        private static readonly FireFeel Lmg = new FireFeel(1.00f, 1.00f, 1.15f, 1.3f, 0.02f);
        private static readonly FireFeel Neutral = new FireFeel(1f, 1f, 1f, 1f);

        public static FireFeel For(WeaponCategory category, AmmoType ammo)
        {
            switch (category)
            {
                case WeaponCategory.Pistol: return Pistol;
                case WeaponCategory.Smg: return Smg9;
                case WeaponCategory.AssaultRifle: return ammo == AmmoType.Mm762 ? Rifle762 : Rifle556;
                case WeaponCategory.Dmr: return Dmr;
                case WeaponCategory.Sniper: return Sniper;
                case WeaponCategory.Shotgun: return Shotgun;
                case WeaponCategory.Lmg: return Lmg;
                default: return Neutral;
            }
        }

        public static bool IsHeavyRifle(WeaponCategory category, AmmoType ammo)
        {
            return ammo == AmmoType.Mm762 && (category == WeaponCategory.AssaultRifle || category == WeaponCategory.Dmr);
        }

        /// <summary>İlk atış (sprayShots &lt;= 1) 7.62 tüfek/DMR için ekstra çarpan; diğerleri 1.</summary>
        public static float FirstShotKick(WeaponCategory category, AmmoType ammo, int sprayShots)
        {
            return sprayShots <= 1 && IsHeavyRifle(category, ammo) ? FirstShotKick762 : 1f;
        }

        public static float FirstShotFlash(WeaponCategory category, AmmoType ammo, int sprayShots)
        {
            return sprayShots <= 1 && IsHeavyRifle(category, ammo) ? FirstShotFlash762 : 1f;
        }

        public static float KickPitchMultiplier(WeaponCategory category, AmmoType ammo, int sprayShots, bool suppressed)
        {
            var m = For(category, ammo).KickPitch * FirstShotKick(category, ammo, sprayShots);
            return suppressed ? m * SuppressedKick : m;
        }

        public static float KickYawMultiplier(WeaponCategory category, AmmoType ammo, int sprayShots, bool suppressed)
        {
            var m = For(category, ammo).KickYaw * FirstShotKick(category, ammo, sprayShots);
            return suppressed ? m * SuppressedKick : m;
        }

        public static float FlashScale(WeaponCategory category, AmmoType ammo, int sprayShots, bool suppressed)
        {
            if (suppressed)
                return 1f;
            return For(category, ammo).FlashScale * FirstShotFlash(category, ammo, sprayShots);
        }

        /// <summary>
        /// Duman yoğunluğu: seri atış arttıkça (sprayShots) ve tempo yükseldikçe artar; susturucuda azalır.
        /// 0.1-3 aralığında.
        /// </summary>
        public static float SmokeRate(WeaponCategory category, AmmoType ammo, int sprayShots, float fireInterval, bool suppressed)
        {
            var baseRate = For(category, ammo).SmokeRate;
            var heat = Clamp01(Math.Max(0, sprayShots) / 12f);
            var interval = float.IsNaN(fireInterval) || fireInterval <= 0f ? 0.1f : fireInterval;
            var tempo = Clamp(0.08f / interval, 0.7f, 1.5f);
            var rate = baseRate * (1f + heat * 1.5f) * tempo;
            if (suppressed)
                rate *= 0.5f;
            return Clamp(rate, 0.1f, 3f);
        }

        /// <summary>MG süreğen titreşim: atış başına birikir, tavanlı. Seri bitince azalır.</summary>
        public static float ShakeAccumulate(float current, WeaponCategory category, AmmoType ammo)
        {
            var add = For(category, ammo).ShakePerShot;
            if (add <= 0f)
                return Math.Max(0f, current);
            return Clamp((float.IsNaN(current) ? 0f : current) + add, 0f, MaxShake);
        }

        public static float ShakeDecay(float current, float dt)
        {
            if (float.IsNaN(current) || current <= 0f)
                return 0f;
            return Math.Max(0f, current - ShakeDecayPerSecond * Math.Max(0f, dt) * MaxShake);
        }

        /// <summary>Pompalı pump gecikmesi sonrası geri dönüş darbesi (pitch, derece) — tek vuruşun ardından.</summary>
        public static float PumpReboundPitch(float shotPitch)
        {
            return -Math.Abs(shotPitch) * PumpRebound;
        }

        private static float Clamp01(float v) { return v < 0f ? 0f : v > 1f ? 1f : v; }
        private static float Clamp(float v, float lo, float hi) { return float.IsNaN(v) ? lo : v < lo ? lo : v > hi ? hi : v; }
    }
}
