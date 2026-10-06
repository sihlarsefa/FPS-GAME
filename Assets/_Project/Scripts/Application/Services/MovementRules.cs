using System;

namespace Project.Application.Services
{
    /// <summary>
    /// Stamina modelinin ayarları (Unity'siz). Varsayılanlar Tarkov-lite/MW arası: koşu ~9 sn, nefes toparlama ~6 sn.
    /// Infrastructure'daki PlayerMovementConfig bu yapıyı doldurur.
    /// </summary>
    public struct StaminaTuning
    {
        public float Max;
        /// <summary>Koşarken saniyede harcanan stamina (yük çarpanıyla artar).</summary>
        public float SprintDrain;
        /// <summary>Harcama bittikten sonra toparlanma başlamadan önceki bekleme (s).</summary>
        public float RegenDelay;
        /// <summary>Tükenince (sıfır) bekleme ek süresi (s).</summary>
        public float ExhaustedExtraDelay;
        public float RegenRate;
        /// <summary>Tükenmiş durumdan çıkmak için gereken stamina.</summary>
        public float ExhaustedRecoverAt;
        /// <summary>Koşuya yeniden başlamak için gereken asgari stamina.</summary>
        public float SprintStartMin;

        public static StaminaTuning Default => new StaminaTuning
        {
            Max = 100f,
            SprintDrain = 11f,
            RegenDelay = 1.1f,
            ExhaustedExtraDelay = 0.9f,
            RegenRate = 15f,
            ExhaustedRecoverAt = 25f,
            SprintStartMin = 12f,
        };
    }

    /// <summary>
    /// Saf (Unity'siz, deterministik: yalnız dt ile ilerler) stamina durumu. Koşu harcar, zıplama/iniş/kayma/tırmanma tek seferlik
    /// maliyet ister; harcamadan sonra bekleme süresi geçince toparlanır. Tükenince (0) oyuncu "nefes nefese" kalır:
    /// <see cref="Exhausted"/> eşik aşılana dek koşamaz ve zıplayamaz. Ağ komutu şekli değişmez; durum her istemcide
    /// aynı girdi + dt ile yeniden üretilir.
    /// </summary>
    public sealed class StaminaModel
    {
        private StaminaTuning _t;
        private float _delay;

        public StaminaModel() : this(StaminaTuning.Default) { }

        public StaminaModel(StaminaTuning tuning)
        {
            Configure(tuning);
            Current = _t.Max;
        }

        public float Current { get; private set; }
        public float Max => _t.Max;
        public float Normalized => _t.Max > 0f ? Current / _t.Max : 0f;
        public bool Exhausted { get; private set; }
        public float RegenDelayRemaining => _delay;

        public void Configure(StaminaTuning tuning)
        {
            if (tuning.Max <= 0f)
                tuning = StaminaTuning.Default;
            _t = tuning;
            if (Current > _t.Max)
                Current = _t.Max;
        }

        public void Refill()
        {
            Current = _t.Max;
            Exhausted = false;
            _delay = 0f;
        }

        /// <summary>Koşuya başlanabilir/sürdürülebilir mi.</summary>
        public bool CanSprint(bool alreadySprinting)
        {
            if (Exhausted)
                return false;
            return alreadySprinting ? Current > 0.01f : Current >= _t.SprintStartMin;
        }

        public bool CanAfford(float cost) => !Exhausted && Current >= cost;

        /// <summary>Tek seferlik harcama (zıplama, iniş, kayma, tırmanma). Dönüş: tükenme eşiğine ilk kez düşüldü mü.</summary>
        public bool Spend(float cost)
        {
            if (cost <= 0f || float.IsNaN(cost))
                return false;
            Current = Math.Max(0f, Current - cost);
            _delay = Math.Max(_delay, _t.RegenDelay);
            return CheckExhaust();
        }

        /// <summary>
        /// Kare adımı. <paramref name="sprinting"/> koşu harcar; <paramref name="regenMultiplier"/> duruşa/harekete göre
        /// toparlanma çarpanı (bkz. <see cref="MovementRules.RegenMultiplier"/>); <paramref name="drainMultiplier"/> yük çarpanı.
        /// Dönüş: bu karede tükendi mi.
        /// </summary>
        public bool Tick(float dt, bool sprinting, float regenMultiplier, float drainMultiplier)
        {
            if (dt <= 0f || float.IsNaN(dt))
                return false;

            var justExhausted = false;
            if (sprinting)
            {
                Current = Math.Max(0f, Current - _t.SprintDrain * Math.Max(0.1f, drainMultiplier) * dt);
                _delay = Math.Max(_delay, _t.RegenDelay);
                justExhausted = CheckExhaust();
            }
            else if (_delay > 0f)
            {
                _delay = Math.Max(0f, _delay - dt);
            }
            else if (Current < _t.Max)
            {
                // Tükenmişken toparlanma biraz yavaş (nefes), ilk bekleme zaten uzatıldı.
                var rate = _t.RegenRate * Math.Max(0f, regenMultiplier) * (Exhausted ? 0.8f : 1f);
                Current = Math.Min(_t.Max, Current + rate * dt);
            }

            if (Exhausted && Current >= _t.ExhaustedRecoverAt)
                Exhausted = false;
            return justExhausted;
        }

        private bool CheckExhaust()
        {
            if (!Exhausted && Current <= 0.0001f)
            {
                Exhausted = true;
                _delay = Math.Max(_delay, _t.RegenDelay + _t.ExhaustedExtraDelay);
                return true;
            }

            return false;
        }
    }

    /// <summary>Hareket hissi için saf formüller (yük, eğim, stamina, duruş geçişi, adım ritmi). Testlenebilir, Unity'siz.</summary>
    public static class MovementRules
    {
        public const float JumpStaminaCost = 8f;
        public const float MantleStaminaCost = 12f;
        public const float SlideStaminaCost = 10f;
        public const float SlideCooldownSeconds = 2.2f;
        /// <summary>Kayma başlangıç hızı en çok koşu hızının bu katı olur (eski 1.05 + sınır).</summary>
        public const float SlideMaxSpeedFactor = 1.08f;

        public const float MaxLoadSpeedPenalty = 0.16f;
        public const float MaxLoadAccelPenalty = 0.35f;

        private static float Clamp01(float v) => float.IsNaN(v) ? 0f : v < 0f ? 0f : v > 1f ? 1f : v;

        /// <summary>
        /// Yük etkisi 0..1: envanter doluluk oranı (yarısına kadar hafif, ağırlaştıkça artar) + zırh seviyesi
        /// (yelek seviyesi + kask seviyesi/2; her seviye ~%4).
        /// </summary>
        public static float LoadBurden(float loadFraction, float armorScore)
        {
            var l = Clamp01(float.IsNaN(loadFraction) ? 0f : loadFraction);
            // 0.4'e kadar neredeyse hissedilmez, sonra eğri.
            var inv = Clamp01((l - 0.25f) / 0.75f);
            inv *= inv;
            var armor = Clamp01((armorScore < 0f ? 0f : armorScore) * 0.04f);
            return Clamp01(inv * 0.7f + armor * 0.5f + inv * armor * 0.2f);
        }

        /// <summary>Yürüme/koşu hız çarpanı (1 = yüksüz).</summary>
        public static float LoadSpeedFactor(float burden) => 1f - MaxLoadSpeedPenalty * Clamp01(burden);

        /// <summary>İvmelenme/yavaşlama çarpanı: ağır yük geç hızlanır, geç durur (atalet).</summary>
        public static float LoadAccelFactor(float burden) => 1f - MaxLoadAccelPenalty * Clamp01(burden);

        /// <summary>Zıplama başlangıç hızı çarpanı.</summary>
        public static float LoadJumpFactor(float burden) => 1f - 0.18f * Clamp01(burden);

        /// <summary>Koşu stamina harcama çarpanı (1..1.7).</summary>
        public static float LoadDrainMultiplier(float burden) => 1f + 0.7f * Clamp01(burden);

        /// <summary>
        /// Eğim hız çarpanı. <paramref name="slopeDegrees"/> zemin eğimi; <paramref name="downhillDot"/> hareket yönünün
        /// yokuş aşağı yöne iç çarpımı (-1 tam yokuş yukarı, +1 tam yokuş aşağı). Yukarı: her derece ~%1,1 yavaş (alt sınır 0,6);
        /// aşağı: hafif hız (en çok +%6), çok dikte frenleme yok.
        /// </summary>
        public static float SlopeSpeedFactor(float slopeDegrees, float downhillDot)
        {
            var deg = float.IsNaN(slopeDegrees) ? 0f : Math.Max(0f, Math.Min(60f, slopeDegrees));
            var d = float.IsNaN(downhillDot) ? 0f : Math.Max(-1f, Math.Min(1f, downhillDot));
            if (deg < 3f)
                return 1f;
            if (d < 0f)
                return Math.Max(0.6f, 1f - (-d) * 0.011f * (deg - 3f));
            return Math.Min(1.06f, 1f + d * 0.004f * (deg - 3f));
        }

        /// <summary>Stamina toparlanma çarpanı: durarak daha hızlı, çömelik/yüzüstü bonus, yürürken yavaş.</summary>
        public static float RegenMultiplier(bool moving, bool crouchOrProne, bool prone)
        {
            var m = moving ? 0.55f : 1.35f;
            if (prone)
                m *= 1.3f;
            else if (crouchOrProne)
                m *= 1.15f;
            return m;
        }

        /// <summary>Tükenmiş oyuncunun yürüme hızı çarpanı (nefes nefese).</summary>
        public static float ExhaustedSpeedFactor(bool exhausted) => exhausted ? 0.88f : 1f;

        /// <summary>Nefes sesi şiddeti 0..1 (PlayerBreathing okur). Düşük stamina + tükenmiş durumda yükselir.</summary>
        public static float BreathingIntensity(float staminaNormalized, bool exhausted)
        {
            var s = Clamp01(staminaNormalized);
            var b = Clamp01((0.55f - s) / 0.55f);
            if (exhausted)
                b = Math.Max(b, 0.75f);
            return b;
        }

        /// <summary>İniş stamina maliyeti: sert iniş (>4 m/s) nefes keser.</summary>
        public static float LandingStaminaCost(float impactSpeed)
        {
            var v = float.IsNaN(impactSpeed) ? 0f : impactSpeed;
            return v <= 4f ? 0f : Math.Min(35f, (v - 4f) * 2.6f);
        }

        /// <summary>Sert inişte yatay hızın korunan oranı (1 = hiç kayıp). 4 m/s altı kayıpsız, 12+ m/s'de %45.</summary>
        public static float LandingVelocityKeep(float impactSpeed)
        {
            var v = float.IsNaN(impactSpeed) ? 0f : impactSpeed;
            if (v <= 4f)
                return 1f;
            return Math.Max(0.45f, 1f - (v - 4f) / 8f * 0.55f);
        }

        /// <summary>
        /// Duruş geçiş süresi (s): boy farkıyla orantılı; çömelme ~0,30 s, yüzüstü ~0,48 s. <paramref name="transitionK"/> config
        /// katsayısı (10 = varsayılan hız).
        /// </summary>
        public static float StanceDuration(float fromHeight, float toHeight, float transitionK)
        {
            var delta = Math.Abs(fromHeight - toHeight);
            var k = Math.Max(0.1f, transitionK);
            var d = (0.12f + delta * 0.3f) * (10f / k);
            return Math.Max(0.05f, Math.Min(1.5f, d));
        }

        /// <summary>Yumuşak (ease-in-out) duruş eğrisi, t 0..1.</summary>
        public static float StanceEase(float t)
        {
            var x = Clamp01(t);
            return x * x * (3f - 2f * x);
        }

        /// <summary>Duruş değişirken hız çarpanı: boy hâlâ değişiyorsa en çok %35 yavaşlar.</summary>
        public static float StanceTransitionSpeedFactor(float currentHeight, float targetHeight)
        {
            var remaining = Math.Abs(currentHeight - targetHeight);
            return 1f - 0.35f * Clamp01(remaining / 0.6f);
        }

        /// <summary>Tek adım uzunluğu (m) — duruş/koşu; FootstepEmitter ile aynı değerler.</summary>
        public static float StrideLength(bool crouching, bool prone, bool sprinting)
        {
            if (prone)
                return 1.2f;
            if (crouching)
                return 1.7f;
            return sprinting ? 2.8f : 2.2f;
        }

        /// <summary>Adım ritmi (adım/sn) = yatay hız / adım uzunluğu.</summary>
        public static float StepsPerSecond(float horizontalSpeed, float stride)
        {
            if (stride <= 0.01f || float.IsNaN(horizontalSpeed) || horizontalSpeed < 0f)
                return 0f;
            return horizontalSpeed / stride;
        }

        /// <summary>Kayma başlayabilir mi (stamina + bekleme).</summary>
        public static bool CanSlide(float stamina, float cooldownRemaining)
        {
            return cooldownRemaining <= 0f && stamina >= SlideStaminaCost;
        }

        /// <summary>Eğilmede (Q/E) boşluk oranından izin verilen eğilme: 0..1.</summary>
        public static float LeanAllowed(float clearanceDistance, float neededDistance)
        {
            if (neededDistance <= 0.001f)
                return 1f;
            return Clamp01(clearanceDistance / neededDistance);
        }
    }
}
