using System;
using System.Collections.Generic;

namespace Project.Application.Match.Flow
{
    public enum EngagementBand
    {
        /// <summary>Bina içi / sokak: ≤ 30 m.</summary>
        CloseQuarters = 0,
        /// <summary>Köy / orman kenarı: 30–120 m.</summary>
        Mid = 1,
        /// <summary>Açık ova / tepe: 120 m +.</summary>
        Open = 2
    }

    public readonly struct CoverReport
    {
        public float MaxGap { get; }
        public float ExposedFraction { get; }
        public bool Passes { get; }

        public CoverReport(float maxGap, float exposedFraction, bool passes)
        {
            MaxGap = maxGap;
            ExposedFraction = exposedFraction;
            Passes = passes;
        }
    }

    /// <summary>
    /// Siper aralığı kuralları (saf). Warzone/Beenox tasarımcıları "siperler arası koşu süresi"ni açıkça
    /// dengeleme ölçüsü sayar: oyuncu ateş altında siperden siper koşabilmeli. Burada bir hat (transect)
    /// üzerindeki siper konumlarından en büyük boşluğu ve "açıkta kalan" oranı ölçülür; açıkta = en yakın siperden
    /// sprint × tepki süresi (varsayılan 2.5 sn × 5.5 m/sn ≈ 13.75 m) daha uzak.
    /// </summary>
    public static class CoverSpacingRules
    {
        public const float SprintSpeed = 5.5f;
        public const float ReachSeconds = 2.5f;

        public static float MaxGapFor(EngagementBand band)
        {
            switch (band)
            {
                case EngagementBand.CloseQuarters: return 8f;
                case EngagementBand.Mid: return 25f;
                default: return 55f;
            }
        }

        public static float MaxExposedFraction(EngagementBand band)
        {
            switch (band)
            {
                case EngagementBand.CloseQuarters: return 0.1f;
                case EngagementBand.Mid: return 0.35f;
                default: return 0.6f;
            }
        }

        public static EngagementBand BandForRange(float meters)
        {
            if (meters <= 30f) return EngagementBand.CloseQuarters;
            if (meters <= 120f) return EngagementBand.Mid;
            return EngagementBand.Open;
        }

        /// <summary>
        /// coverPositions: hat üzerindeki siper konumları (m, sırasız olabilir); lineLength hat uzunluğu.
        /// Hat başı ve sonu da boşluk hesabına dahildir.
        /// </summary>
        public static CoverReport Evaluate(IReadOnlyList<float> coverPositions, float lineLength, EngagementBand band)
        {
            if (!(lineLength > 0f) || float.IsInfinity(lineLength))
                return new CoverReport(0f, 0f, true);

            var sorted = new List<float>(coverPositions != null ? coverPositions.Count : 0);
            if (coverPositions != null)
            {
                for (var i = 0; i < coverPositions.Count; i++)
                {
                    var p = coverPositions[i];
                    if (!float.IsNaN(p) && p >= 0f && p <= lineLength)
                        sorted.Add(p);
                }
            }

            sorted.Sort();

            if (sorted.Count == 0)
                return new CoverReport(lineLength, 1f, false);

            var maxGap = Math.Max(sorted[0], lineLength - sorted[sorted.Count - 1]);
            for (var i = 1; i < sorted.Count; i++)
                maxGap = Math.Max(maxGap, sorted[i] - sorted[i - 1]);

            var reach = SprintSpeed * ReachSeconds;
            // Açıkta kalan uzunluk: her boşluğun ortasındaki, iki siperden de reach'ten uzak kısım.
            var exposed = 0f;
            exposed += Math.Max(0f, sorted[0] - reach);
            exposed += Math.Max(0f, lineLength - sorted[sorted.Count - 1] - reach);
            for (var i = 1; i < sorted.Count; i++)
                exposed += Math.Max(0f, sorted[i] - sorted[i - 1] - 2f * reach);

            var fraction = exposed / lineLength;
            var passes = maxGap <= MaxGapFor(band) * 1.5f && fraction <= MaxExposedFraction(band);
            return new CoverReport(maxGap, fraction, passes);
        }
    }
}
