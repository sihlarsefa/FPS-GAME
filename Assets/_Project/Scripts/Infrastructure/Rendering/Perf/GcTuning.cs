using System;
using UnityEngine;
using UnityEngine.Scripting;

namespace Project.Infrastructure.Rendering.Perf
{
    /// <summary>GC ayarlarının saf hesapları (testlenebilir).</summary>
    public static class GcTuningMath
    {
        public const ulong MinSliceNs = 500_000UL;     // 0.5 ms
        public const ulong MaxSliceNs = 4_000_000UL;   // 4 ms

        /// <summary>
        /// Artımlı GC zaman dilimi: kare süresinin ~%8'i (60 FPS'te ~1.3 ms). Küçük dilim = takılma yok ama
        /// toplama daha çok kareye yayılır; ayırma yüksekse dilim büyütülür ki GC yetişsin.
        /// </summary>
        public static ulong SliceNanoseconds(int targetFps, bool heavyAllocation)
        {
            var frameNs = 1_000_000_000.0 / Math.Max(15, Math.Min(240, targetFps));
            var frac = heavyAllocation ? 0.16 : 0.08;
            var ns = (ulong)(frameNs * frac);
            return ns < MinSliceNs ? MinSliceNs : ns > MaxSliceNs ? MaxSliceNs : ns;
        }

        /// <summary>Elle GC yalnız güvenli anda: yükleme/menü, son toplamadan beri yeterli süre geçmiş ve yığın büyümüş.</summary>
        public static bool ShouldCollectAtSafePoint(bool safePoint, float secondsSinceLast, long heapGrowthBytes)
            => safePoint && secondsSinceLast >= 30f && heapGrowthBytes >= 16L * 1024 * 1024;
    }

    /// <summary>Artımlı GC dilimini hedef FPS'e göre ayarlar.</summary>
    public static class GcTuning
    {
        public static bool Supported => GarbageCollector.isIncremental;

        /// <summary>Artımlı GC açıksa dilimi ayarlar; false = bu platformda uygulanamadı.</summary>
        public static bool Apply(int targetFps, bool heavyAllocation)
        {
            try
            {
                if (!GarbageCollector.isIncremental) return false;
                GarbageCollector.incrementalTimeSliceNanoseconds = GcTuningMath.SliceNanoseconds(targetFps, heavyAllocation);
                return true;
            }
            catch (Exception) { return false; }
        }
    }
}
