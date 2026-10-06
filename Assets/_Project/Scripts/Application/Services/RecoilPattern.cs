using System;

namespace Project.Application.Services
{
    /// <summary>
    /// Silaha özgü, deterministik sekme deseni: ilk PatternLength atış her seferinde aynıdır (ezberlenebilir),
    /// sonrasında çağıran rastgele sekmeye döner. Saf mantık; Unity bağımlılığı yok.
    /// </summary>
    public static class RecoilPattern
    {
        /// <summary>Ezberlenebilir desen uzunluğu (ilk 10 atış tam öğrenilebilir, sonraki atışlar yatışır).</summary>
        public const int PatternLength = 20;

        /// <summary>Desen üstüne eklenen rastgele bileşen: dikeyde ±%6, yatayda ±%18 (yatay yarı çapa oranla).</summary>
        public const float RandomVerticalFraction = 0.06f;

        public const float RandomHorizontalFraction = 0.18f;

        /// <summary>Kamera toparlanma tabanı (derece/sn, RecoilRecovery = 1 için).</summary>
        public const float BaseRecoveryDegPerSecond = 3f;

        /// <summary>Birikmiş tepmenin her derecesi için ek toparlanma (derece/sn) — büyük sapma daha hızlı geri gelir.</summary>
        public const float RecoveryPerAccumulatedDegree = 0.6f;

        /// <summary>
        /// Kameranın hedef çizgisine geri dönme hızı (derece/sn). <paramref name="recoilRecovery"/> silahın RecoilRecovery
        /// değeri; <paramref name="accumulatedPitch"/> atış serisinde biriken yukarı sapma (derece).
        /// </summary>
        public static float RecoveryDegPerSecond(float recoilRecovery, float accumulatedPitch)
        {
            var r = float.IsNaN(recoilRecovery) || recoilRecovery < 0.05f ? 1f : recoilRecovery;
            var acc = float.IsNaN(accumulatedPitch) || accumulatedPitch < 0f ? 0f : accumulatedPitch;
            return r * (BaseRecoveryDegPerSecond + RecoveryPerAccumulatedDegree * acc);
        }

        /// <summary>Kararlı (platformdan bağımsız) tohum: FNV-1a.</summary>
        public static int SeedFor(string weaponId)
        {
            unchecked
            {
                var h = (uint)2166136261;
                if (weaponId != null)
                {
                    for (var i = 0; i < weaponId.Length; i++)
                        h = (h ^ weaponId[i]) * 16777619u;
                }

                return (int)h;
            }
        }

        public static bool IsPatterned(int shotIndex) => shotIndex >= 0 && shotIndex < PatternLength;

        /// <summary>
        /// shotIndex: 0 = sıkılan ilk atış. vertical çarpanı ~[0.8,1.3], horizontal ∈ [-1,1] (yarı çap çarpanı).
        /// Silaha özgü yan eğilim (sola/sağa kayma) ve sinüs salınımı vardır; ilk atış güçlü yukarı, sonra yatışır.
        /// Pattern dışındaki indeksler için (1, 0) döner; çağıran rastgele yatay kullanmalı.
        /// </summary>
        public static void GetStep(int seed, int shotIndex, out float vertical, out float horizontal)
        {
            if (!IsPatterned(shotIndex))
            {
                vertical = 1f;
                horizontal = 0f;
                return;
            }

            // İlk atışlar güçlü yukarı tepme, sonra yatışır.
            var ramp = shotIndex == 0 ? 1.25f : shotIndex < 4 ? 1.1f : shotIndex < 9 ? 1f : shotIndex < 15 ? 0.92f : 0.85f;
            vertical = ramp * (0.92f + 0.16f * Hash01(seed, shotIndex * 2));

            var phase = Hash01(seed, 977) * 6.2831853f;
            var freq = 0.55f + 0.5f * Hash01(seed, 978);
            var drift = (float)Math.Sin(phase + shotIndex * freq);
            var amp = 0.45f + 0.5f * Hash01(seed, shotIndex * 2 + 1);

            // Silaha özgü yan eğilim: 4. atışa doğru oturur (her silahın "ezberlenecek" yönü).
            var side = Hash01(seed, 979) < 0.5f ? -1f : 1f;
            var bias = side * (0.2f + 0.25f * Hash01(seed, 980)) * Math.Min(1f, shotIndex / 4f);

            horizontal = drift * amp * 0.65f + bias;
            if (shotIndex == 0)
                horizontal *= 0.3f;
            horizontal = Math.Max(-1f, Math.Min(1f, horizontal));
        }

        /// <summary>
        /// Desene eklenen rastgele bileşen için çarpanlar: <paramref name="random01"/> ∈ [0,1].
        /// Dikey çarpan 1 ± RandomVerticalFraction, yatay ek ∈ ±RandomHorizontalFraction (yarı çap çarpanı).
        /// </summary>
        public static void GetRandomComponent(float random01, out float verticalScale, out float horizontalAdd)
        {
            var r = float.IsNaN(random01) ? 0.5f : (random01 < 0f ? 0f : random01 > 1f ? 1f : random01);
            horizontalAdd = (r * 2f - 1f) * RandomHorizontalFraction;
            // Aynı rastgele değerden bağımsız görünen ikinci değer.
            var r2 = (r * 7.31f) % 1f;
            verticalScale = 1f + (r2 * 2f - 1f) * RandomVerticalFraction;
        }

        private static float Hash01(int seed, int n)
        {
            unchecked
            {
                var x = (uint)seed ^ ((uint)n * 0x9E3779B1u);
                x ^= x >> 16;
                x *= 0x85EBCA6Bu;
                x ^= x >> 13;
                x *= 0xC2B2AE35u;
                x ^= x >> 16;
                return (x & 0xFFFFFF) / 16777216f;
            }
        }
    }

    /// <summary>Nişan geçişi: blend, AdsTime süresinde 0..1 ilerler; yayılım ve zoom bu eğriyle harmanlanır.</summary>
    public static class AdsBlend
    {
        public const float MinAdsTime = 0.05f;

        public static float Step(float current, bool wantsAim, float dt, float adsTime)
        {
            var t = adsTime < MinAdsTime || float.IsNaN(adsTime) ? MinAdsTime : adsTime;
            if (float.IsNaN(dt) || dt <= 0f)
                return current;
            var target = wantsAim ? 1f : 0f;
            var step = dt / t;
            if (current < target)
                return Math.Min(target, current + step);
            return Math.Max(target, current - step);
        }

        public static float Smooth(float blend)
        {
            var b = blend < 0f ? 0f : blend > 1f ? 1f : blend;
            return b * b * (3f - 2f * b);
        }

        public static float Lerp(float hip, float ads, float blend)
        {
            return hip + (ads - hip) * Smooth(blend);
        }
    }

    /// <summary>Dürbünlü silahta nefes tutma: Shift ile 4 sn sabit, bitince sallanma cezası, bırakınca toparlanır.</summary>
    public sealed class HoldBreathState
    {
        public const float MaxSeconds = 4f;
        public const float SteadySway = 0.25f;
        public const float NormalSway = 1f;
        public const float PenaltySway = 1.8f;
        public const float RecoverPerSecond = 0.8f;

        private bool _exhausted;

        public float Remaining { get; private set; } = MaxSeconds;
        public bool IsHolding { get; private set; }

        /// <summary>Atış yayılımına uygulanan sallanma çarpanı.</summary>
        public float SwayMultiplier { get; private set; } = NormalSway;

        public void Reset()
        {
            Remaining = MaxSeconds;
            _exhausted = false;
            IsHolding = false;
            SwayMultiplier = NormalSway;
        }

        public void Update(float dt, bool wantsHold, bool scoped)
        {
            if (float.IsNaN(dt) || dt <= 0f)
                return;

            var holding = wantsHold && scoped && !_exhausted && Remaining > 0f;
            IsHolding = holding;
            if (holding)
            {
                Remaining -= dt;
                if (Remaining <= 0f)
                {
                    Remaining = 0f;
                    _exhausted = true;
                    IsHolding = false;
                }
            }
            else
            {
                Remaining = Math.Min(MaxSeconds, Remaining + RecoverPerSecond * dt);
                if (_exhausted && !wantsHold && Remaining >= MaxSeconds * 0.5f)
                    _exhausted = false;
            }

            if (IsHolding)
                SwayMultiplier = SteadySway;
            else if (_exhausted)
                SwayMultiplier = PenaltySway;
            else
            {
                // Toparlanırken ceza yumuşakça normale iner.
                var recovered = Remaining / MaxSeconds;
                SwayMultiplier = Remaining >= MaxSeconds ? NormalSway : NormalSway + (PenaltySway - NormalSway) * (1f - recovered) * 0.5f;
            }
        }
    }
}
