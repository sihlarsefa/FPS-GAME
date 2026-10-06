using System;
using Project.Application.Services;
using Project.Core.Domain;

namespace Project.Application.Combat.Feel
{
    /// <summary>
    /// Silaha özgü tepme ayarı: tohum (ezberlenebilir desen), kuvvet çarpanları, toparlanma gecikmesi ve
    /// seri sıfırlama süresi. Tohum silah kimliğinden (FNV-1a) türetilir; aynı silah her oturumda aynı deseni verir.
    /// </summary>
    public readonly struct WeaponRecoilProfile
    {
        public readonly int Seed;
        public readonly float VerticalScale;
        public readonly float HorizontalScale;
        /// <summary>İlk atış dikey çarpanı (CoD/Tarkov: ilk atış belirgin vurur).</summary>
        public readonly float FirstShotScale;
        /// <summary>Son atıştan sonra toparlanmaya başlamadan önceki bekleme (sn).</summary>
        public readonly float RecoveryDelay;
        /// <summary>Seri sayacının sıfırlandığı sessizlik süresi (sn); desen baştan başlar.</summary>
        public readonly float BurstResetSeconds;
        /// <summary>ADS'de tepme çarpanı (&lt;1: nişanlıyken daha kontrollü).</summary>
        public readonly float AdsRecoilScale;
        /// <summary>Yatay sapmanın toparlanma hızı oranı (dikeye göre). 1 = aynı hızda.</summary>
        public readonly float HorizontalRecoveryRatio;
        /// <summary>Dikey birikim yumuşak tavanı (derece): üstünde ek tepme sönümlenir.</summary>
        public readonly float SoftPitchCeiling;

        public WeaponRecoilProfile(int seed, float verticalScale, float horizontalScale, float firstShotScale,
            float recoveryDelay, float burstResetSeconds, float adsRecoilScale, float horizontalRecoveryRatio, float softPitchCeiling)
        {
            Seed = seed;
            VerticalScale = verticalScale;
            HorizontalScale = horizontalScale;
            FirstShotScale = firstShotScale;
            RecoveryDelay = recoveryDelay;
            BurstResetSeconds = burstResetSeconds;
            AdsRecoilScale = adsRecoilScale;
            HorizontalRecoveryRatio = horizontalRecoveryRatio;
            SoftPitchCeiling = softPitchCeiling;
        }

        /// <summary>Silah kimliği + kategoriden profil. Kategori varsayılanı üzerine kimlik tohumu uygulanır.</summary>
        public static WeaponRecoilProfile For(string weaponId, WeaponCategory category)
        {
            var seed = RecoilPattern.SeedFor(weaponId);
            switch (category)
            {
                case WeaponCategory.Pistol: return new WeaponRecoilProfile(seed, 1.00f, 0.80f, 1.15f, 0.10f, 0.35f, 0.90f, 1.0f, 14f);
                case WeaponCategory.Smg: return new WeaponRecoilProfile(seed, 0.80f, 0.90f, 1.10f, 0.08f, 0.25f, 0.85f, 1.1f, 16f);
                case WeaponCategory.AssaultRifle: return new WeaponRecoilProfile(seed, 1.00f, 1.00f, 1.25f, 0.10f, 0.30f, 0.80f, 1.0f, 18f);
                case WeaponCategory.Dmr: return new WeaponRecoilProfile(seed, 1.35f, 0.75f, 1.30f, 0.20f, 0.60f, 0.75f, 0.9f, 14f);
                case WeaponCategory.Sniper: return new WeaponRecoilProfile(seed, 2.20f, 0.60f, 1.00f, 0.35f, 1.00f, 0.70f, 0.8f, 12f);
                case WeaponCategory.Shotgun: return new WeaponRecoilProfile(seed, 1.80f, 1.00f, 1.00f, 0.25f, 0.80f, 0.85f, 0.9f, 14f);
                case WeaponCategory.Lmg: return new WeaponRecoilProfile(seed, 0.85f, 1.20f, 1.35f, 0.15f, 0.40f, 0.75f, 0.8f, 22f);
                default: return new WeaponRecoilProfile(seed, 1f, 1f, 1.2f, 0.12f, 0.3f, 0.85f, 1f, 16f);
            }
        }

        /// <summary>Elle ayarlı çarpanlarla aynı tohumu koruyan kopya (silah verisinden gelen RecoilVertical/Horizontal için).</summary>
        public WeaponRecoilProfile WithScales(float vertical, float horizontal)
        {
            return new WeaponRecoilProfile(Seed, vertical, horizontal, FirstShotScale, RecoveryDelay,
                BurstResetSeconds, AdsRecoilScale, HorizontalRecoveryRatio, SoftPitchCeiling);
        }
    }

    /// <summary>
    /// Durumlu tepme oturumu: her atış için (pitch, yaw) darbe üretir, birikimi izler ve gecikmeli toparlanma verir.
    /// Desen ilk <see cref="RecoilPattern.PatternLength"/> atış deterministik (öğrenilebilir), üstü rastgele yataylı.
    /// </summary>
    public sealed class RecoilSession
    {
        private WeaponRecoilProfile _p;
        private float _sinceShot;

        public RecoilSession(WeaponRecoilProfile profile)
        {
            _p = profile;
            _sinceShot = 10f;
        }

        public int ShotIndex { get; private set; }
        /// <summary>Birikmiş yukarı sapma (derece, ≥ 0).</summary>
        public float AccumulatedPitch { get; private set; }
        /// <summary>Birikmiş yatay sapma (derece, işaretli).</summary>
        public float AccumulatedYaw { get; private set; }

        public WeaponRecoilProfile Profile => _p;

        public void SetProfile(WeaponRecoilProfile profile) { _p = profile; }

        public void Reset()
        {
            ShotIndex = 0;
            AccumulatedPitch = 0f;
            AccumulatedYaw = 0f;
            _sinceShot = 10f;
        }

        /// <summary>
        /// Atış: pitchDeg (yukarı +) ve yawDeg (sağ +) darbesi. baseVerticalDeg/baseHorizontalDeg silahın
        /// RecoilVertical/Horizontal değerleri; random01 ∈ [0,1] çağıranın rastgelesi (desen dışı + küçük gürültü).
        /// </summary>
        public void OnShot(float baseVerticalDeg, float baseHorizontalDeg, float random01, ShooterStance stance,
            float adsBlend, out float pitchDeg, out float yawDeg)
        {
            if (_sinceShot > _p.BurstResetSeconds) ShotIndex = 0;

            RecoilPattern.GetStep(_p.Seed, ShotIndex, out var v, out var h);
            RecoilPattern.GetRandomComponent(random01, out var vRand, out var hAdd);
            if (!RecoilPattern.IsPatterned(ShotIndex))
                h = (random01 * 2f - 1f) * 0.8f;

            var first = ShotIndex == 0 ? _p.FirstShotScale : 1f;
            var ads = Clamp01(adsBlend);
            var adsMul = 1f + (_p.AdsRecoilScale - 1f) * ads;
            var stanceMul = StanceSpreadRules.RecoilMultiplier(stance);

            var pitch = baseVerticalDeg * _p.VerticalScale * v * vRand * first * adsMul * stanceMul;
            var yaw = baseHorizontalDeg * _p.HorizontalScale * (h + hAdd) * adsMul * stanceMul;

            // Yumuşak tavan: birikim tavana yaklaştıkça yeni dikey darbe söner.
            var ceil = Math.Max(1f, _p.SoftPitchCeiling);
            pitch *= 1f / (1f + (AccumulatedPitch / ceil) * (AccumulatedPitch / ceil));

            pitchDeg = pitch;
            yawDeg = yaw;
            AccumulatedPitch = Math.Max(0f, AccumulatedPitch + pitch);
            AccumulatedYaw += yaw;
            ShotIndex++;
            _sinceShot = 0f;
        }

        /// <summary>
        /// Toparlanma adımı: kameradan geri alınacak (pitchDeg aşağı, yawDeg merkeze) miktar. Bekleme dolmadan 0.
        /// recoilRecovery silahın RecoilRecovery değeri.
        /// </summary>
        public void Step(float dt, float recoilRecovery, ShooterStance stance, out float pitchBack, out float yawBack)
        {
            pitchBack = 0f;
            yawBack = 0f;
            if (float.IsNaN(dt) || dt <= 0f) return;
            _sinceShot += dt;
            if (_sinceShot <= _p.RecoveryDelay) return;
            if (AccumulatedPitch <= 0f && Math.Abs(AccumulatedYaw) <= 0f) return;

            var mul = StanceSpreadRules.RecoveryMultiplier(stance);
            var rate = RecoilPattern.RecoveryDegPerSecond(recoilRecovery, AccumulatedPitch) * mul;
            pitchBack = Math.Min(AccumulatedPitch, rate * dt);
            AccumulatedPitch -= pitchBack;

            var yawRate = rate * _p.HorizontalRecoveryRatio;
            var yawMag = Math.Min(Math.Abs(AccumulatedYaw), yawRate * dt);
            yawBack = AccumulatedYaw > 0f ? yawMag : -yawMag;
            AccumulatedYaw -= yawBack;
        }

        /// <summary>Verilen tohumla ilk n atışın kümülatif ham yolunu (pitch, yaw) üretir — öğrenme/ghost çizgisi için.</summary>
        public static void PredictedPath(WeaponRecoilProfile p, float baseV, float baseH, int shots, float[] pitchOut, float[] yawOut)
        {
            var s = new RecoilSession(p);
            var n = Math.Min(shots, Math.Min(pitchOut.Length, yawOut.Length));
            for (var i = 0; i < n; i++)
            {
                s.OnShot(baseV, baseH, 0.5f, ShooterStance.Standing, 0f, out _, out _);
                pitchOut[i] = s.AccumulatedPitch;
                yawOut[i] = s.AccumulatedYaw;
            }
        }

        /// <summary>
        /// Öğrenme skoru 0..1: oyuncunun kamerayı çektiği toplam (pull) ile birikmiş sapma ne kadar örtüşüyor.
        /// 1 = tam telafi, 0 = hiç/aşırı telafi.
        /// </summary>
        public static float CompensationScore(float accumulatedDeg, float playerPullDeg)
        {
            var a = Math.Abs(accumulatedDeg);
            if (a < 0.01f) return 1f;
            var err = Math.Abs(a - Math.Abs(playerPullDeg)) / a;
            return Clamp01(1f - err);
        }

        private static float Clamp01(float v) { return float.IsNaN(v) ? 0f : v < 0f ? 0f : v > 1f ? 1f : v; }
    }
}
