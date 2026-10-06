using System;
using Project.Core.Domain;

namespace Project.Application.Match.Flow
{
    /// <summary>
    /// Hedef maç süresine ve harita boyutuna göre faz planı üretir (saf). PUBG çemberleri her fazda alanı kabaca
    /// yarıya/üçte ikiye indirir (yarıçap oranı ≈ 0.6–0.75), bekleme süresi azalır, hasar yüzdesi geometrik artar.
    /// Üretilen plan ZoneRotationPlanner ile sınanabilir; MatchConfig.DefaultZonePhases() yerine harita başına
    /// özel plan gerektiğinde kullanılır.
    /// </summary>
    public static class ZonePhasePlanBuilder
    {
        public static ZonePhase[] Build(float startRadius, float finalRadius, int phaseCount, float totalSeconds,
            float firstDamagePercentPerSecond, float lastDamagePercentPerSecond, float maxHealth)
        {
            if (phaseCount < 1)
                phaseCount = 1;
            if (phaseCount > 12)
                phaseCount = 12;
            if (!(startRadius > 1f))
                startRadius = 1f;
            if (!(finalRadius >= 0f) || finalRadius >= startRadius)
                finalRadius = 0f;
            if (!(totalSeconds > 60f * phaseCount * 0.5f))
                totalSeconds = 60f * phaseCount * 0.5f;
            if (!(firstDamagePercentPerSecond > 0f))
                firstDamagePercentPerSecond = 0.5f;
            if (!(lastDamagePercentPerSecond >= firstDamagePercentPerSecond))
                lastDamagePercentPerSecond = firstDamagePercentPerSecond;

            var phases = new ZonePhase[phaseCount];

            // Hedef yarıçaplar: son çember finalRadius'a, geometrik oranla (son faz tamamen kapanır).
            var floorRadius = Math.Max(finalRadius, 1f);
            var ratio = phaseCount > 1 ? (float)Math.Pow(floorRadius / startRadius, 1.0 / phaseCount) : 0f;

            // Süre ağırlıkları: erken fazlar uzun (iniş + ganimet), geç fazlar kısa.
            var weights = new float[phaseCount];
            var weightSum = 0f;
            for (var i = 0; i < phaseCount; i++)
            {
                weights[i] = 1f - 0.55f * (phaseCount > 1 ? i / (float)(phaseCount - 1) : 0f);
                weightSum += weights[i];
            }

            var radius = startRadius;
            for (var i = 0; i < phaseCount; i++)
            {
                var target = i == phaseCount - 1 ? finalRadius : radius * ratio;
                var phaseSeconds = totalSeconds * weights[i] / weightSum;

                // İlk faz bekleme ağırlıklı (PUBG: 120 sn), sonrası bekleme ≈ %55 → %40.
                var t = phaseCount > 1 ? i / (float)(phaseCount - 1) : 1f;
                var waitShare = 0.62f - 0.22f * t;
                var wait = (float)Math.Round(phaseSeconds * waitShare);
                var shrink = (float)Math.Round(phaseSeconds - wait);

                var percent = firstDamagePercentPerSecond *
                              (float)Math.Pow(lastDamagePercentPerSecond / firstDamagePercentPerSecond, t);
                var dps = (float)Math.Round(ZoneDamageRules.PercentToDps(percent, maxHealth) * 10f) / 10f;
                phases[i] = new ZonePhase(wait, shrink, target, dps);
                radius = target;
            }

            return phases;
        }
    }
}
