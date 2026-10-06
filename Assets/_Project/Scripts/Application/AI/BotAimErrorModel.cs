using System;

namespace Project.Application.AI
{
    /// <summary>
    /// İnsansı nişan hatası (saf mantık). Insurgency'de botlar yana hızlı hareketli hedefe yetişemez, yavaş/duran hedefe ölümcüldür;
    /// Tarkov'da nişan yavaşlatıldı. Hata konisi (derece, yarı-açı): taban + mesafe + hedef açısal hızı (takip gecikmesi)
    /// + botun kendi hareketi + baskı; hedefte kalma süresiyle ve beceriyle küçülür.
    /// Ayrıca ilk atışlar bilerek "yakın ıska" yönüne (BotSkill.NearMiss ile uyumlu) kayar.
    /// </summary>
    public static class BotAimErrorModel
    {
        public const float MinConeDegrees = 0.15f;
        public const float MaxConeDegrees = 14f;

        /// <summary>Beceriye göre taban hata (derece).</summary>
        public static float BaseCone(float skill01)
        {
            return Lerp(3.2f, 0.7f, skill01);
        }

        /// <summary>
        /// Hedefin bota göre enine hızı (m/sn) ve mesafe → açısal hız (derece/sn). Takip gecikmesi bununla orantılı.
        /// </summary>
        public static float AngularSpeedDegrees(float lateralSpeed, float distance)
        {
            var d = MathF.Max(2f, distance);
            return MathF.Abs(lateralSpeed) / d * 57.29578f;
        }

        /// <summary>
        /// Takip gecikmesinden kaynaklı ek hata (derece): açısal hız * tepki süresi * (1 - takip yeteneği).
        /// Sert yan hareket (ör. strafe) botu şaşırtır.
        /// </summary>
        public static float TrackingLagCone(float angularSpeedDegrees, float skill01)
        {
            var lag = Lerp(0.30f, 0.12f, skill01);          // sn: botun hedef tahmin gecikmesi
            return angularSpeedDegrees * lag * 0.55f;
        }

        /// <summary>Mesafe terimi (derece): uzakta küçük açı hatası bile metre cinsinden büyür; kısmen telafi edilir.</summary>
        public static float DistanceCone(float distance)
        {
            return MathF.Min(2.2f, MathF.Max(0f, distance) / 120f * 1.6f);
        }

        /// <summary>Botun kendi hareket çarpanı: duran 1.0, yürüyen 1.35, koşan 1.9.</summary>
        public static float SelfMotionMultiplier(float selfSpeed, bool crouched)
        {
            var m = 1f + MathF.Min(selfSpeed, 6.5f) / 6.5f * 0.95f;
            if (crouched && selfSpeed < 0.5f) m *= 0.85f;
            return m;
        }

        /// <summary>Hedefte kalma süresine göre toparlanma (BotSkill.AimSettle ile aynı biçim: 0.65 + extra*exp(-t/tau)).</summary>
        public static float SettleMultiplier(float secondsOnTarget, float skill01)
        {
            var extra = Lerp(1.55f, 0.7f, skill01);
            var tau = Lerp(1.15f, 0.5f, skill01);
            return 0.65f + extra * MathF.Exp(-MathF.Max(0f, secondsOnTarget) / tau);
        }

        /// <summary>Toplam hata konisi (derece, yarı-açı).</summary>
        public static float ConeDegrees(float skill01, float distance, float lateralTargetSpeed, float selfSpeed,
            bool selfCrouched, float secondsOnTarget, float suppression01, bool targetPartiallyHidden)
        {
            var angular = AngularSpeedDegrees(lateralTargetSpeed, distance);
            var cone = BaseCone(skill01) + DistanceCone(distance) + TrackingLagCone(angular, skill01);
            cone *= SelfMotionMultiplier(selfSpeed, selfCrouched);
            cone *= SettleMultiplier(secondsOnTarget, skill01);
            cone *= 1f + Clamp01(suppression01) * 1.3f;
            if (targetPartiallyHidden)
                cone *= 1.25f; // sadece kafa/omuz görünür: belirsiz nişan noktası
            return Clamp(cone, MinConeDegrees, MaxConeDegrees);
        }

        /// <summary>
        /// Koni içinde bir örnek: (u, v) ∈ [0,1) iki rastgele sayı → (yawOffset, pitchOffset) derece.
        /// Dairesel düzgün dağılım yerine merkeze yığılan (r = cone * u^0.65) ve dikey hatası %70 olan dağılım.
        /// </summary>
        public static void SampleOffset(float coneDegrees, float u, float v, out float yawDeg, out float pitchDeg)
        {
            var r = coneDegrees * MathF.Pow(Clamp01(u), 0.65f);
            var theta = Clamp01(v) * 6.2831855f;
            yawDeg = r * MathF.Cos(theta);
            pitchDeg = r * MathF.Sin(theta) * 0.7f;
        }

        /// <summary>
        /// Hedef önde giderken ("lead") bot tahmin payı: tam öncü değil, beceriyle %50..%95'ine yakın.
        /// Dönüş: hedef hızına çarpılacak öngörü süresi (sn).
        /// </summary>
        public static float LeadSeconds(float distance, float bulletSpeed, float skill01)
        {
            var flight = MathF.Max(0f, distance) / MathF.Max(50f, bulletSpeed);
            return flight * Lerp(0.5f, 0.95f, skill01);
        }

        /// <summary>
        /// Atış için "bilerek ıska" payı (0..1): hedefe henüz yeni kilitlenen bot ilk atışları yakın-ıska yönünde kaçırır,
        /// oyuncuya tepki vermesi için süre tanır (Tarkov/Squad hissi). 1 = bu atışı ıskala.
        /// </summary>
        public static float DeliberateMissWeight(int shotIndexSinceAcquire, float skill01)
        {
            var n = Math.Max(0, shotIndexSinceAcquire);
            var w = n == 0 ? 0.55f : (n == 1 ? 0.3f : (n == 2 ? 0.12f : 0f));
            return w * Lerp(1.2f, 0.5f, skill01);
        }

        /// <summary>
        /// Yakın-ıska sapması (derece): hedefin kafasının/gövdesinin yanından geçecek şekilde en az minDegrees dışı koni.
        /// </summary>
        public static float NearMissOffsetDegrees(float distance, float rng01)
        {
            // 0.4..1.1 m sapma → derece
            var meters = Lerp(0.4f, 1.1f, rng01);
            return MathF.Atan2(meters, MathF.Max(3f, distance)) * 57.29578f;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        private static float Clamp(float v, float a, float b) => v < a ? a : (v > b ? b : v);
        private static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
    }
}
