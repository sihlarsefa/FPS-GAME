using System;
using Project.Core.Domain;

namespace Project.Application.Combat.Feel
{
    /// <summary>Silah başına yayılım (bloom) ayarı.</summary>
    public readonly struct BloomTuning
    {
        /// <summary>Hareketsiz, ilk atış taban yayılımı (derece, yarı koni).</summary>
        public readonly float BaseSpreadDeg;
        /// <summary>Her atışta eklenen bloom (derece, ayakta/hip).</summary>
        public readonly float BloomPerShotDeg;
        /// <summary>Bloom tavanı (derece).</summary>
        public readonly float MaxBloomDeg;
        /// <summary>Son atıştan sonra bloom'un azalmaya başlamasına kadar bekleme (sn).</summary>
        public readonly float DecayDelay;
        /// <summary>Bekleme sonrası azalma hızı (derece/sn); büyük bloom ek olarak üstel azalır.</summary>
        public readonly float DecayDegPerSecond;
        /// <summary>İlk atış kesinliği: yeterince dinlenmişken yayılım bu çarpanla azalır (0..1).</summary>
        public readonly float FirstShotSpreadFactor;
        /// <summary>İlk atış bonusunun tam açılması için gereken dinlenme süresi (sn).</summary>
        public readonly float SettleSeconds;

        public BloomTuning(float baseSpreadDeg, float bloomPerShotDeg, float maxBloomDeg, float decayDelay,
            float decayDegPerSecond, float firstShotSpreadFactor, float settleSeconds)
        {
            BaseSpreadDeg = baseSpreadDeg;
            BloomPerShotDeg = bloomPerShotDeg;
            MaxBloomDeg = maxBloomDeg;
            DecayDelay = decayDelay;
            DecayDegPerSecond = decayDegPerSecond;
            FirstShotSpreadFactor = firstShotSpreadFactor;
            SettleSeconds = settleSeconds;
        }

        /// <summary>Kategori varsayılanları (CoD/Battlefield tarzı: otomatikler hızlı bloom, tek atışlılar sert ilk atış bonusu).</summary>
        public static BloomTuning ForCategory(WeaponCategory category)
        {
            switch (category)
            {
                case WeaponCategory.Pistol: return new BloomTuning(0.50f, 0.55f, 3.0f, 0.18f, 6.0f, 0.45f, 0.35f);
                case WeaponCategory.Smg: return new BloomTuning(0.70f, 0.28f, 3.5f, 0.10f, 5.0f, 0.60f, 0.30f);
                case WeaponCategory.AssaultRifle: return new BloomTuning(0.45f, 0.30f, 3.0f, 0.12f, 4.0f, 0.50f, 0.35f);
                case WeaponCategory.Dmr: return new BloomTuning(0.25f, 0.70f, 2.5f, 0.25f, 3.5f, 0.30f, 0.45f);
                case WeaponCategory.Sniper: return new BloomTuning(0.10f, 1.40f, 3.0f, 0.45f, 3.0f, 0.15f, 0.80f);
                case WeaponCategory.Shotgun: return new BloomTuning(2.20f, 0.60f, 4.0f, 0.30f, 4.0f, 1.00f, 0.40f);
                case WeaponCategory.Lmg: return new BloomTuning(0.65f, 0.22f, 4.5f, 0.15f, 2.5f, 0.70f, 0.50f);
                default: return new BloomTuning(0.5f, 0.3f, 3f, 0.15f, 4f, 0.6f, 0.3f);
            }
        }
    }

    /// <summary>
    /// Saf, durumlu yayılım modeli. Her atış bloom ekler (duruşa göre ölçekli); gecikmeden sonra bloom
    /// hem doğrusal hem üstel azalır (büyük bloom hızlı söner). Dinlenmiş silahın ilk atışı daha isabetlidir.
    /// </summary>
    public sealed class SpreadBloomModel
    {
        /// <summary>Bloom'un üstel azalma payı: kalan bloom'un saniyede bu oranı da silinir.</summary>
        public const float ExponentialDecayRate = 0.8f;

        private BloomTuning _t;

        public SpreadBloomModel(BloomTuning tuning)
        {
            _t = tuning;
            SinceLastShot = tuning.SettleSeconds;
        }

        public float BloomDeg { get; private set; }
        public float SinceLastShot { get; private set; }
        public int ShotsInBurst { get; private set; }

        public BloomTuning Tuning => _t;

        public void SetTuning(BloomTuning tuning) { _t = tuning; BloomDeg = Math.Min(BloomDeg, tuning.MaxBloomDeg); }

        public void Reset()
        {
            BloomDeg = 0f;
            ShotsInBurst = 0;
            SinceLastShot = _t.SettleSeconds;
        }

        /// <summary>0..1: silahın ne kadar dinlendiği (ilk atış bonus gücü).</summary>
        public float Rested01 => _t.SettleSeconds <= 0f ? 1f : Clamp01(SinceLastShot / _t.SettleSeconds);

        /// <summary>İlk atış bonusu: dinlenmiş ve bloom'suzken yayılım FirstShotSpreadFactor'e iner.</summary>
        public float FirstShotFactor()
        {
            var rested = Rested01 * (BloomDeg <= 0.01f ? 1f : 0f);
            return 1f + (_t.FirstShotSpreadFactor - 1f) * rested;
        }

        /// <summary>Anlık yarı-koni yayılımı (derece). stanceMul = duruş+ADS birleşik çarpan.</summary>
        public float CurrentSpreadDeg(float stanceMul, float adsRatio)
        {
            var sm = float.IsNaN(stanceMul) || stanceMul < 0f ? 1f : stanceMul;
            var ar = float.IsNaN(adsRatio) ? 1f : Clamp(adsRatio, 0.02f, 2f);
            return (_t.BaseSpreadDeg * FirstShotFactor() + BloomDeg) * sm * ar;
        }

        /// <summary>Atış kaydı: bloom artar. bloomStanceMul iyi duruşta (<1) bloom ekini azaltır.</summary>
        public void OnShot(float bloomStanceMul = 1f)
        {
            var m = float.IsNaN(bloomStanceMul) || bloomStanceMul < 0f ? 1f : Math.Min(bloomStanceMul, 3f);
            // Seri ilerledikçe ek bloom hafif azalır (tavana yaklaşınca yumuşar).
            var headroom = _t.MaxBloomDeg <= 0f ? 0f : 1f - BloomDeg / _t.MaxBloomDeg;
            var add = _t.BloomPerShotDeg * m * (0.35f + 0.65f * Clamp01(headroom));
            BloomDeg = Math.Min(_t.MaxBloomDeg, BloomDeg + add);
            SinceLastShot = 0f;
            ShotsInBurst++;
        }

        /// <summary>Zaman adımı. recoveryMul duruş toparlanma çarpanıdır.</summary>
        public void Step(float dt, float recoveryMul = 1f)
        {
            if (float.IsNaN(dt) || dt <= 0f) return;
            SinceLastShot += dt;
            if (SinceLastShot <= _t.DecayDelay || BloomDeg <= 0f) return;

            // Gecikmenin bu adıma düşen kısmı hariç efektif süre.
            var eff = Math.Min(dt, SinceLastShot - _t.DecayDelay);
            var rm = float.IsNaN(recoveryMul) || recoveryMul < 0.1f ? 1f : recoveryMul;
            var linear = _t.DecayDegPerSecond * rm * eff;
            var expo = BloomDeg * (1f - (float)Math.Exp(-ExponentialDecayRate * rm * eff));
            BloomDeg = Math.Max(0f, BloomDeg - linear - expo);
            if (BloomDeg < 0.005f) BloomDeg = 0f;
            if (BloomDeg <= 0f) ShotsInBurst = 0;
        }

        private static float Clamp01(float v) { return float.IsNaN(v) ? 0f : v < 0f ? 0f : v > 1f ? 1f : v; }
        private static float Clamp(float v, float lo, float hi) { return float.IsNaN(v) ? lo : v < lo ? lo : v > hi ? hi : v; }
    }
}
