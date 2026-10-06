using System;

namespace Project.Presentation.UI.Play
{
    /// <summary>
    /// Tahmini bekleme süresi modeli (saf mantık). Çevrimdışı iken kuyruk simüle edilir; sunucu bağlanınca
    /// <see cref="Observe"/> gerçek bekleme sürelerini besler. Call of Duty eşleştirme yazılarındaki gibi
    /// "bağlantı > bekleme süresi" önceliği: tahmin aralık olarak gösterilir, kesin söz verilmez.
    /// </summary>
    public sealed class QueueEstimator
    {
        /// <summary>Gerçek ölçümlerin üstel ortalama ağırlığı.</summary>
        public const float EmaWeight = 0.3f;
        /// <summary>Alt/üst aralık çarpanları (tahmin ± belirsizlik).</summary>
        public const float LowFactor = 0.6f;
        public const float HighFactor = 1.5f;
        /// <summary>PUBG'de olduğu gibi bu süreden sonra daha dolu türe geçiş önerilir.</summary>
        public const float SuggestAfterSeconds = 60f;

        private readonly float[] _ema = new float[4];
        private readonly bool[] _seeded = new bool[4];

        /// <summary>Günün saatine göre talep çarpanı: gece seyrek (uzun), akşam yoğun (kısa).</summary>
        public static float DemandMultiplier(int hour)
        {
            hour = ((hour % 24) + 24) % 24;
            if (hour >= 18 && hour <= 23) return 0.8f;
            if (hour >= 12 && hour < 18) return 1.0f;
            if (hour >= 6 && hour < 12) return 1.25f;
            return 1.6f;
        }

        /// <summary>Gerçek ölçüm ekler (sn).</summary>
        public void Observe(PlayQueueKind kind, float seconds)
        {
            if (seconds < 0f || float.IsNaN(seconds)) return;
            var i = (int)kind;
            if (!_seeded[i]) { _ema[i] = seconds; _seeded[i] = true; }
            else _ema[i] = _ema[i] * (1f - EmaWeight) + seconds * EmaWeight;
        }

        /// <summary>Tür için beklenen süre (sn): ölçüm varsa ölçüm, yoksa taban × talep.</summary>
        public float Estimate(PlayQueueKind kind, int hour)
        {
            var info = PlayQueueModes.Get(kind);
            if (!info.UsesQueue) return 0f;
            var i = (int)kind;
            var demand = DemandMultiplier(hour);
            return _seeded[i] ? _ema[i] * demand : info.BaseWaitSeconds * demand;
        }

        public float Low(PlayQueueKind kind, int hour) => Estimate(kind, hour) * LowFactor;
        public float High(PlayQueueKind kind, int hour) => Estimate(kind, hour) * HighFactor;

        /// <summary>Eşleşmenin bulunacağı süreyi örnekler; u ∈ [0,1]. Aralığın içinde kalır.</summary>
        public float SampleWait(PlayQueueKind kind, int hour, float u)
        {
            u = Math.Max(0f, Math.Min(1f, u));
            var est = Estimate(kind, hour);
            return est * (0.62f + 0.85f * u);
        }

        /// <summary>"0:42" biçimi.</summary>
        public static string FormatClock(float seconds)
        {
            var s = Math.Max(0, (int)Math.Floor(seconds));
            return (s / 60) + ":" + (s % 60).ToString("00");
        }

        /// <summary>"~0:20 – 0:45" tahmin aralığı; kuyruksuz türde "ANINDA".</summary>
        public string FormatRange(PlayQueueKind kind, int hour)
        {
            if (!PlayQueueModes.Get(kind).UsesQueue) return "ANINDA";
            return "~" + FormatClock(Low(kind, hour)) + " – " + FormatClock(High(kind, hour));
        }

        /// <summary>
        /// Arama süresi ilerleme oranı (0..0.95). Tahmine yaklaştıkça yavaşlar, asla dolmaz; böylece
        /// "tahmini süre aşıldı" durumunda çubuk donmuş görünmez, hâlâ hafifçe ilerler.
        /// </summary>
        public static float Progress(float elapsed, float estimate)
        {
            if (estimate <= 0.01f) return 0.95f;
            var x = Math.Max(0f, elapsed) / estimate;
            return 0.95f * (1f - (float)Math.Exp(-1.6f * x));
        }

        /// <summary>Tahminin üst sınırı aşıldı mı (arayüzde "beklenenden uzun" uyarısı).</summary>
        public bool IsOverdue(PlayQueueKind kind, int hour, float elapsed) => elapsed > High(kind, hour);

        /// <summary>Uzun beklemede önerilecek tür; öneri yoksa null (Tim en dolu tür olduğundan öneri verilmez).</summary>
        public static PlayQueueKind? SuggestAlternative(PlayQueueKind kind, float elapsed)
        {
            if (elapsed < SuggestAfterSeconds) return null;
            if (kind == PlayQueueKind.Solo) return PlayQueueKind.Duo;
            if (kind == PlayQueueKind.Duo) return PlayQueueKind.TeamBr;
            return null;
        }
    }
}
