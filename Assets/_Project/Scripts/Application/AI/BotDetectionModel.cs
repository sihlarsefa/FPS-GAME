using System;

namespace Project.Application.AI
{
    /// <summary>
    /// İnsansı algı birikimi (saf mantık). Tarkov 12.7 "daha uzun tespit, dar görüş açısı" ve CoD/BF tarzı
    /// "görüş yavaşça dolar" yaklaşımı: düşman bir anda %100 fark edilmez; farkındalık (0..1) görüş
    /// merkezine yakınlık, mesafe, hedef hareketi, kısmi örtü ve duman ile hızlanıp yavaşlar.
    /// Tespit eşiği 1'dir; ilk görüşten sonra tepki gecikmesi ayrıca <see cref="FirstSightDelay"/> ile eklenir.
    /// </summary>
    public static class BotDetectionModel
    {
        /// <summary>Tam hızda algılanan merkezi koni yarı açısı (derece).</summary>
        public const float FoveaHalfAngle = 12f;
        /// <summary>Çevresel görüşte kazanç çarpanı tabanı (görüş sınırında).</summary>
        public const float PeripheralFloor = 0.18f;
        /// <summary>Bu mesafede (m) kazanç yarıya düşer.</summary>
        public const float HalfGainDistance = 55f;
        /// <summary>Farkındalık, kazanç yokken saniyede bu kadar söner.</summary>
        public const float DecayPerSecond = 0.22f;
        /// <summary>Tespit sonrası "yarı farkında" (şüpheli) eşiği.</summary>
        public const float SuspicionThreshold = 0.45f;

        /// <summary>Görüş ekseninden sapma (derece) için kazanç çarpanı 0..1 (fovea içinde 1, kenara doğru PeripheralFloor).</summary>
        public static float AngleGain(float offAxisDegrees, float fovDegrees)
        {
            var a = MathF.Abs(offAxisDegrees);
            var half = MathF.Max(20f, fovDegrees * 0.5f);
            if (a >= half)
                return 0f;
            if (a <= FoveaHalfAngle)
                return 1f;
            var t = (a - FoveaHalfAngle) / (half - FoveaHalfAngle);
            // Yumuşak düşüş (smoothstep).
            var s = t * t * (3f - 2f * t);
            return 1f + (PeripheralFloor - 1f) * s;
        }

        /// <summary>Mesafe için kazanç çarpanı: 1 / (1 + (d/half)^2), 0.05 altına inmez.</summary>
        public static float DistanceGain(float distance)
        {
            var r = MathF.Max(0f, distance) / HalfGainDistance;
            return MathF.Max(0.05f, 1f / (1f + r * r));
        }

        /// <summary>Hedef hareket çarpanı: hareketsiz/çömelik gizli, sprint belirgin.</summary>
        public static float MotionGain(float targetSpeed, bool crouched, bool prone)
        {
            var g = 0.55f + MathF.Min(targetSpeed, 7f) / 7f * 0.9f; // 0.55 .. 1.45
            if (crouched) g *= 0.75f;
            if (prone) g *= 0.55f;
            return g;
        }

        /// <summary>Görünürlük 0..1: ışın örneklerinin görünen oranı (kısmi örtü), duman yoğunluğu ve kamuflaj düşürür.</summary>
        public static float Visibility(float visibleFraction01, float smokeDensity01, float camouflage01)
        {
            var v = Clamp01(visibleFraction01) * (1f - 0.9f * Clamp01(smokeDensity01));
            v *= 1f - 0.5f * Clamp01(camouflage01);
            return Clamp01(v);
        }

        /// <summary>Saniyedeki farkındalık kazancı (1.0 = bir saniyede tam tespit).</summary>
        public static float GainPerSecond(float offAxisDegrees, float fovDegrees, float distance, float targetSpeed,
            bool crouched, bool prone, float visibility01, float skill01, bool firedRecently)
        {
            var g = AngleGain(offAxisDegrees, fovDegrees) * DistanceGain(distance) * MotionGain(targetSpeed, crouched, prone);
            g *= Clamp01(visibility01);
            g *= 0.8f + 0.7f * Clamp01(skill01);
            if (firedRecently)
                g *= 3.0f; // namlu parlaması/ses: neredeyse anında fark edilir
            // Taban: iyi görünen yakın düşman ~0.45 sn'de dolar.
            return g * 2.4f;
        }

        /// <summary>Bir adım: yeni farkındalık değeri.</summary>
        public static float Step(float awareness01, float gainPerSecond, float dt)
        {
            var a = Clamp01(awareness01);
            if (gainPerSecond > 0.0001f)
                a += gainPerSecond * MathF.Max(0f, dt);
            else
                a -= DecayPerSecond * MathF.Max(0f, dt);
            return Clamp01(a);
        }

        public static bool IsSuspicious(float awareness01) => awareness01 >= SuspicionThreshold && awareness01 < 1f;
        public static bool IsDetected(float awareness01) => awareness01 >= 0.999f;

        /// <summary>
        /// İlk tespitten sonra ateş açmadan önceki ek tepki gecikmesi (sn).
        /// İnsan 0.2–0.35 sn; bot beceriyle kısalır; sürpriz (arkadan/yan) ve mesafe uzatır. rng01 [0,1].
        /// </summary>
        public static float FirstSightDelay(float skill01, float offAxisDegrees, float distance, float rng01)
        {
            var s = Clamp01(skill01);
            var baseDelay = Lerp(0.42f, 0.2f, s);
            baseDelay += Clamp01(MathF.Abs(offAxisDegrees) / 90f) * 0.18f;      // yandan gelen daha geç fark edilir
            baseDelay += Clamp01(distance / 150f) * 0.08f;
            baseDelay *= Lerp(0.85f, 1.25f, Clamp01(rng01));
            return MathF.Max(0.15f, baseDelay);
        }

        /// <summary>Hedef kaybolduktan sonra "hala biliyorum" hafıza süresi (sn): kıdemli daha uzun hatırlar.</summary>
        public static float MemorySeconds(float skill01, bool lastSeenMoving)
        {
            var m = Lerp(4f, 9f, Clamp01(skill01));
            if (lastSeenMoving) m *= 1.25f;
            return m;
        }

        /// <summary>Aranan son bilinen konuma eklenecek hata yarıçapı (m): zaman geçtikçe büyür (hedef kaçmış olabilir).</summary>
        public static float SearchUncertainty(float secondsSinceSeen, float targetSpeedWhenLost)
        {
            var radius = 1.5f + MathF.Max(0f, secondsSinceSeen) * MathF.Max(1f, targetSpeedWhenLost) * 0.6f;
            return MathF.Min(radius, 30f);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        private static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
    }
}
