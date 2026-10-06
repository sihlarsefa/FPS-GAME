using System;

namespace Project.Infrastructure.Rendering
{
    /// <summary>Savaş ekran efektlerinin (bastırma, düşük can, sersemletme) saf matematiği; Unity bağımsız.</summary>
    public static class CombatScreenFxMath
    {
        public const float LowHealthThreshold = 0.35f;
        public const float ConcussionDecayPerSecond = 0.42f;

        /// <summary>Ekrana uygulanacak son efekt değerleri (hepsi 0..1, renk dışında).</summary>
        public struct FxState
        {
            public float Vignette;       // 0..1 vinyet yoğunluğu
            public float VignetteRed;    // 0..1 vinyetin kırmızı ağırlığı
            public float Saturation;     // -100..0 (ColorAdjustments)
            public float Chromatic;      // 0..1
            public float Blur;           // 0..1
            public float Flash;          // 0..1 beyaz parlama
            public bool Any => Vignette > 0.001f || Saturation < -0.5f || Chromatic > 0.001f || Blur > 0.001f || Flash > 0.001f;
        }

        public static float Clamp01(float v) => float.IsNaN(v) ? 0f : v < 0f ? 0f : v > 1f ? 1f : v;

        /// <summary>Düşük can şiddeti: eşik üstünde 0, 0 canda 1.</summary>
        public static float LowHealth01(float healthFraction, float threshold = LowHealthThreshold)
        {
            if (threshold <= 0f || healthFraction >= threshold) return 0f;
            return Clamp01(1f - healthFraction / threshold);
        }

        /// <summary>Kalp atışı nabzı (0..1): can azaldıkça hızlanır (1,1 .. 2,2 Hz), çift vuruşlu.</summary>
        public static float HeartPulse(float time, float lowHealth)
        {
            if (lowHealth <= 0f) return 0f;
            var hz = 1.1f + 1.1f * Clamp01(lowHealth);
            var phase = time * hz;
            phase -= (float)Math.Floor(phase);
            var a = Math.Max(0f, 1f - Math.Abs(phase - 0.08f) / 0.1f);
            var b = Math.Max(0f, 1f - Math.Abs(phase - 0.30f) / 0.12f) * 0.6f;
            return Math.Max(a, b);
        }

        /// <summary>Patlama sersemletme şiddeti: yakın = güçlü; dışarı 0.</summary>
        public static float ConcussionFor(float distance, float radius)
        {
            var reach = Math.Max(radius * 2.5f, 8f);
            if (float.IsNaN(distance) || distance < 0f || distance > reach) return 0f;
            var k = 1f - distance / reach;
            return Clamp01(k * k * 1.4f);
        }

        /// <summary>Sersemletme ölçeri: büyük olan kazanır, sonra söner.</summary>
        public static float AddConcussion(float meter, float impulse) => Math.Max(Clamp01(meter), Clamp01(impulse));

        public static float DecayConcussion(float meter, float dt) =>
            Clamp01(meter - Math.Max(0f, dt) * ConcussionDecayPerSecond);

        /// <summary>Gelen otomatik ateş (MG): atış başına küçük darbe; mesafe yakınsa daha büyük.</summary>
        public static float IncomingFireImpulse(float distance, float caliberFactor)
        {
            var d = ScreenEffectsMath.NearMissImpulse(distance, 6f);
            return d * 0.5f * Math.Max(0.4f, Math.Min(2f, caliberFactor));
        }

        /// <summary>Çap (mm) -> darbe çarpanı: 5,56 = 1; .50 cal ≈ 2.</summary>
        public static float CaliberFactor(float caliberMm)
        {
            if (float.IsNaN(caliberMm) || caliberMm <= 0f) return 1f;
            return Math.Max(0.5f, Math.Min(2f, caliberMm / 5.56f * 0.9f + 0.1f));
        }

        /// <summary>Nişan sallantı çarpanı (1 = normal). intensity ayar ölçeği.</summary>
        public static float SwayMultiplier(float suppression, float intensity = 1f)
        {
            var m = 1f + 2.2f * ScreenEffectsMath.SwayAmount(suppression);
            return 1f + (m - 1f) * Math.Max(0f, Math.Min(1.5f, intensity));
        }

        /// <summary>Küçük kamera sarsıntısı şiddeti (0..0.25) — bastırma yükseldikçe.</summary>
        public static float ShakeFor(float suppression) => 0.25f * ScreenEffectsMath.SwayAmount(suppression);

        public const float SplatterLifetime = 2f;
        public const float ArmorBreakDuration = 0.45f;

        /// <summary>Ekrana göre göreli yön (derece, 0 = önden, saat yönü +) -> bölge: 0 üst, 1 sağ, 2 alt, 3 sol.</summary>
        public static int QuadrantOf(float relativeDeg)
        {
            if (float.IsNaN(relativeDeg) || float.IsInfinity(relativeDeg)) return 0;
            var a = relativeDeg % 360f;
            if (a < 0f) a += 360f;
            return (int)((a + 45f) / 90f) % 4;
        }

        /// <summary>Hasar -> kan sıçraması şiddeti (0.35..1).</summary>
        public static float SplatterStrength(float damage) =>
            float.IsNaN(damage) ? 0.35f : Math.Max(0.35f, Math.Min(1f, 0.35f + damage / 60f));

        /// <summary>Kan lekesi opaklığı: yaş 0..2 sn; ilk 0,25 sn tam, sonra yumuşak sönüm.</summary>
        public static float SplatterAlpha(float age, float strength, float lifetime = SplatterLifetime)
        {
            if (float.IsNaN(age) || age < 0f || age >= lifetime || lifetime <= 0f) return 0f;
            var hold = 0.25f;
            if (age <= hold) return Clamp01(strength);
            var t = (age - hold) / (lifetime - hold);
            return Clamp01(strength * (1f - t) * (1f - t));
        }

        /// <summary>İyileşmede temiz silme: kalan lekeler bu çarpanla hızla (0,35 sn) söner. t = silme yaşı.</summary>
        public static float HealWipe(float t)
        {
            if (float.IsNaN(t) || t < 0f) return 1f;
            return Clamp01(1f - t / 0.35f);
        }

        /// <summary>Yaralı (düşük) ekran vinyeti: kenar kararması + yavaş nabız.</summary>
        public static float DownedVignette(float downed, float pulse) =>
            downed <= 0f ? 0f : Clamp01(downed * (0.8f + 0.2f * Clamp01(pulse)));

        /// <summary>Zırh kırılma parlaması: ani tepe, hızlı sönüm. age sn.</summary>
        public static float ArmorBreakFlash(float age)
        {
            if (float.IsNaN(age) || age < 0f || age >= ArmorBreakDuration) return 0f;
            var t = age / ArmorBreakDuration;
            return Clamp01((1f - t) * (1f - t));
        }

        /// <summary>Zırh kırılma halkası: 0..1 yarıçap ilerlemesi (hızlı çıkış).</summary>
        public static float ArmorBreakRing(float age)
        {
            if (float.IsNaN(age) || age < 0f) return 0f;
            return Clamp01(1f - (float)Math.Pow(1f - Clamp01(age / ArmorBreakDuration), 3));
        }

        /// <summary>Kalp atışı ses kancası: nabız aşağıdan eşiği geçerse (tepe kenarı) true.</summary>
        public static bool HeartbeatEdge(float previousPulse, float pulse, float threshold = 0.85f) =>
            previousPulse < threshold && pulse >= threshold;

        /// <summary>Vuruş başına kamera sarsıntı şiddeti (0..0.5), ayar ölçeğiyle (0 = kapalı).</summary>
        public static float HitShake(float damage, float shakeScale)
        {
            if (float.IsNaN(damage) || damage <= 0f) return 0f;
            return Clamp01(0.15f + 0.35f * Math.Min(1f, damage / 60f)) * Math.Max(0f, Math.Min(1.5f, shakeScale));
        }

        /// <summary>
        /// Tüm girdilerden efekt durumu. tier: kalite kademesi (0..3): 0 yalnız vinyet+solma, 1 + renk sapması,
        /// 2+ + bulanıklık.
        /// </summary>
        public static FxState Compute(float suppression, float lowHealth, float pulse, float concussion,
            float tinnitus, float intensity, int tier, float downed = 0f, float armorFlash = 0f)
        {
            downed = Clamp01(downed);
            armorFlash = Clamp01(armorFlash);
            intensity = Math.Max(0f, Math.Min(1.5f, intensity));
            suppression = Clamp01(suppression);
            lowHealth = Clamp01(lowHealth);
            concussion = Clamp01(concussion);
            tinnitus = Clamp01(tinnitus);
            tier = tier < 0 ? 0 : tier > 3 ? 3 : tier;

            var supV = ScreenEffectsMath.VignetteAlpha(suppression);
            var lowV = 0.55f * lowHealth * (0.65f + 0.35f * pulse);
            var concV = 0.4f * concussion;
            var s = new FxState
            {
                Vignette = Clamp01((float)Math.Max(Math.Max(supV, Math.Max(lowV, concV)), DownedVignette(downed, pulse)) * intensity),
                VignetteRed = Math.Max(lowHealth <= 0f ? 0f : Clamp01(lowHealth * 1.2f * (1f - Clamp01(supV))), 0.7f * downed),
                Saturation = -100f * Clamp01((ScreenEffectsMath.DesaturationAlpha(suppression) + 0.55f * lowHealth + 0.3f * concussion + 0.7f * downed) * intensity),
                Chromatic = tier >= 1 ? Clamp01((0.6f * suppression + 0.5f * concussion + 0.25f * lowHealth * pulse) * intensity) : 0f,
                Blur = tier >= 2 ? Clamp01((0.35f * suppression * suppression + 0.85f * Math.Max(concussion, Math.Max(0.6f * tinnitus, 0.55f * downed))) * intensity) : 0f,
                Flash = Clamp01((Math.Max((concussion - 0.7f) / 0.3f, 0.6f * armorFlash)) * intensity)
            };
            return s;
        }
    }
}
