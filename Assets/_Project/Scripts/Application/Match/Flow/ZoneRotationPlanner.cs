using System;
using System.Collections.Generic;
using Project.Core.Domain;

namespace Project.Application.Match.Flow
{
    /// <summary>Tek bir faz için rotasyon / tempo raporu.</summary>
    public readonly struct ZonePhaseReport
    {
        public int Index { get; }
        public float StartRadius { get; }
        public float EndRadius { get; }

        /// <summary>Yeni alan / eski alan (PUBG çemberlerinde yaklaşık 0.25–0.5 arası).</summary>
        public float AreaRatio { get; }

        /// <summary>Çember kenarının daralma hızı (m/sn), yalnızca yarıçap farkından.</summary>
        public float EdgeSpeed { get; }

        /// <summary>Eski çemberin en uzak noktasındaki oyuncunun bekleme+daralma içinde gerekli koşu hızı (m/sn).</summary>
        public float WorstRotationSpeed { get; }

        public float DamagePerSecond { get; }
        public bool TooFast { get; }
        public bool TooSlow { get; }

        public ZonePhaseReport(int index, float startRadius, float endRadius, float areaRatio, float edgeSpeed,
            float worstRotationSpeed, float damagePerSecond, bool tooFast, bool tooSlow)
        {
            Index = index;
            StartRadius = startRadius;
            EndRadius = endRadius;
            AreaRatio = areaRatio;
            EdgeSpeed = edgeSpeed;
            WorstRotationSpeed = worstRotationSpeed;
            DamagePerSecond = damagePerSecond;
            TooFast = tooFast;
            TooSlow = tooSlow;
        }
    }

    /// <summary>
    /// Alan daralması tempo analizi (saf). Battle royale çemberlerinin iki klasik hatası: (1) en uzaktaki takımın
    /// yürüyerek yetişemeyeceği kadar hızlı kapanan çember, (2) hiçbir şey olmayan, tempoyu öldüren yavaş çember.
    /// PUBG "Blue Zone Revamp" yazısı da aynı yönde: uyarı süresi kısalır, daralma süresi uzar → geç oyunda çatışma
    /// alanı korunur. Bu sınıf mevcut faz planını sprint hızına göre sınar ve ayar önerir.
    /// </summary>
    public static class ZoneRotationPlanner
    {
        /// <summary>Takımın dayanabileceği ortalama koşu hızı (m/sn): sprint ≈ 5.5, yük/durma payıyla 4.5.</summary>
        public const float DefaultSustainedSpeed = 4.5f;

        /// <summary>Kenarın bundan hızlı ilerlemesi (m/sn) yürüyen oyuncuyu çember içinde sıkıştırır.</summary>
        public const float EdgeSpeedLimit = 3.2f;

        /// <summary>Alan oranı bu değerin altına inerse çember "kıskaç" sayılır, üstüne çıkarsa "uyuşuk".</summary>
        public const float MinAreaRatio = 0.18f;
        public const float MaxAreaRatio = 0.62f;

        public static IReadOnlyList<ZonePhaseReport> Analyze(IReadOnlyList<ZonePhase> phases, float startRadius,
            float expectedCenterShift, float sustainedSpeed)
        {
            var list = new List<ZonePhaseReport>(phases != null ? phases.Count : 0);
            if (phases == null)
                return list;

            if (!(sustainedSpeed > 0f) || float.IsInfinity(sustainedSpeed))
                sustainedSpeed = DefaultSustainedSpeed;

            var radius = Math.Max(0f, startRadius);
            for (var i = 0; i < phases.Count; i++)
            {
                var p = phases[i];
                var target = Math.Max(0f, Math.Min(p.TargetRadius, radius));
                var allowedShift = radius - target;
                var shift = Math.Max(0f, Math.Min(expectedCenterShift, allowedShift));

                var shrinkSeconds = Math.Max(0.01f, p.ShrinkSeconds);
                var edge = (radius - target) / shrinkSeconds;
                var areaRatio = radius > 0.001f ? (target * target) / (radius * radius) : 0f;
                var worst = WorstRotationSpeed(radius, target, shift, Math.Max(0f, p.WaitSeconds) + shrinkSeconds);

                // Son faz (hedef 0) alan oranı anlamsız: yalnızca hız sınanır.
                var finalCollapse = target <= 0.001f;
                var tooFast = edge > EdgeSpeedLimit || worst > sustainedSpeed ||
                              (!finalCollapse && areaRatio < MinAreaRatio);
                var tooSlow = !finalCollapse && areaRatio > MaxAreaRatio;
                list.Add(new ZonePhaseReport(i, radius, target, areaRatio, edge, worst, p.DamagePerSecond, tooFast, tooSlow));
                radius = target;
            }

            return list;
        }

        /// <summary>
        /// Eski çemberin kenarındaki, yeni merkezden en uzak oyuncunun yeni çembere ulaşması için mesafe / süre.
        /// En kötü nokta: yeni merkezden (kayma + eski yarıçap) uzaklıkta; kalan yol = bu − yeni yarıçap.
        /// </summary>
        public static float WorstRotationSpeed(float oldRadius, float newRadius, float centerShift, float availableSeconds)
        {
            if (!(availableSeconds > 0f))
                return float.PositiveInfinity;

            var distance = centerShift + oldRadius - newRadius;
            return distance > 0f ? distance / availableSeconds : 0f;
        }

        /// <summary>Verilen noktadaki oyuncunun yeni çembere varması için gereken koşu hızı (m/sn).</summary>
        public static float RequiredSpeedFor(float x, float z, float nextCenterX, float nextCenterZ, float nextRadius,
            float availableSeconds)
        {
            if (!(availableSeconds > 0f))
                return float.PositiveInfinity;

            var dx = x - nextCenterX;
            var dz = z - nextCenterZ;
            var distance = (float)Math.Sqrt(dx * dx + dz * dz) - nextRadius;
            return distance > 0f ? distance / availableSeconds : 0f;
        }

        /// <summary>
        /// Bu fazda bu oyuncu yürüyerek yetişir mi? Araç/helikopter yoksa sustainedSpeed kullanılır;
        /// yetişemiyorsa kaç saniye eksik kaldığı (bölge dışında geçireceği süre) döndürülür.
        /// </summary>
        public static float SecondsOutsideIfWalking(float x, float z, float nextCenterX, float nextCenterZ,
            float nextRadius, float availableSeconds, float speed)
        {
            if (!(speed > 0f))
                return float.PositiveInfinity;

            var dx = x - nextCenterX;
            var dz = z - nextCenterZ;
            var distance = (float)Math.Sqrt(dx * dx + dz * dz) - nextRadius;
            if (distance <= 0f)
                return 0f;

            var needed = distance / speed;
            return needed > availableSeconds ? needed - availableSeconds : 0f;
        }

        /// <summary>Toplam bölge süresi (bekleme + daralma, saniye).</summary>
        public static float TotalSeconds(IReadOnlyList<ZonePhase> phases)
        {
            var total = 0f;
            if (phases == null)
                return total;
            for (var i = 0; i < phases.Count; i++)
                total += Math.Max(0f, phases[i].WaitSeconds) + Math.Max(0f, phases[i].ShrinkSeconds);
            return total;
        }
    }
}
