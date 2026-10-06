using System;

namespace Project.Infrastructure.Rendering.Perf
{
    public struct AllocationStats
    {
        public int Frames;
        /// <summary>Penceredeki ortalama kare başı ayırma (bayt).</summary>
        public float AvgBytesPerFrame;
        public long PeakBytesInFrame;
        /// <summary>Penceredeki toplam bayt / pencere süresi.</summary>
        public float BytesPerSecond;
        /// <summary>Penceredeki GC koleksiyon sayısı (gen0 artışı).</summary>
        public int Collections;
        /// <summary>Ayırma yapan (eşik üstü) kare sayısı.</summary>
        public int AllocatingFrames;
    }

    /// <summary>
    /// Kare başı yönetilen bellek ayırma izleyicisi (saf mantık). Girdi: kare başı ayrılan bayt
    /// (Profiler "GC Allocated In Frame" ya da toplam bellek farkı) ve GC sayacı.
    /// Hedef sıcak yol bütçesi: oyun içinde ~0 B/kare; 1 KB/kare üstü her karede GC baskısı yaratır
    /// (60 FPS'de ~60 KB/s -> birkaç saniyede bir toplama).
    /// </summary>
    public sealed class AllocationTracker
    {
        private readonly long[] _bytes;
        private readonly float[] _dt;
        private readonly int[] _gc;
        private int _head, _count;
        private long _lastTotal = -1;
        private int _lastGcCount = -1;

        public AllocationTracker(int window = 300)
        {
            if (window < 4) window = 4;
            _bytes = new long[window];
            _dt = new float[window];
            _gc = new int[window];
        }

        public int Count => _count;

        /// <summary>Doğrudan kare başı bayt verildiğinde (ProfilerRecorder).</summary>
        public void PushFrame(long bytesAllocated, int collectionsThisFrame, float deltaSeconds)
        {
            if (bytesAllocated < 0) bytesAllocated = 0;
            _bytes[_head] = bytesAllocated;
            _gc[_head] = collectionsThisFrame < 0 ? 0 : collectionsThisFrame;
            _dt[_head] = deltaSeconds < 0f ? 0f : deltaSeconds;
            _head = (_head + 1) % _bytes.Length;
            if (_count < _bytes.Length) _count++;
        }

        /// <summary>
        /// Toplam yönetilen bellek örneğinden türetir (yedek yöntem). Toplam düştüyse GC çalışmıştır:
        /// o karenin ayırması bilinemez, 0 yazılır. Mono yığını bloklar halinde büyüdüğü için kaba bir tahmindir.
        /// </summary>
        public void PushTotals(long totalMemory, int gcCount, float deltaSeconds)
        {
            long alloc = 0;
            var collections = 0;
            if (_lastTotal >= 0)
            {
                alloc = totalMemory - _lastTotal;
                if (alloc < 0) alloc = 0;
                collections = Math.Max(0, gcCount - _lastGcCount);
            }
            _lastTotal = totalMemory;
            _lastGcCount = gcCount;
            PushFrame(alloc, collections, deltaSeconds);
        }

        public void Clear()
        {
            _head = 0; _count = 0; _lastTotal = -1; _lastGcCount = -1;
        }

        public AllocationStats Compute(long perFrameThresholdBytes = 256)
        {
            var s = new AllocationStats { Frames = _count };
            if (_count == 0) return s;
            var start = _count == _bytes.Length ? _head : 0;
            long sum = 0;
            double time = 0;
            for (var i = 0; i < _count; i++)
            {
                var idx = (start + i) % _bytes.Length;
                var b = _bytes[idx];
                sum += b;
                time += _dt[idx];
                s.Collections += _gc[idx];
                if (b > s.PeakBytesInFrame) s.PeakBytesInFrame = b;
                if (b > perFrameThresholdBytes) s.AllocatingFrames++;
            }
            s.AvgBytesPerFrame = (float)sum / _count;
            s.BytesPerSecond = time > 0.0001 ? (float)(sum / time) : 0f;
            return s;
        }

        /// <summary>Sürekli ayırma var mı: pencere dolmuşken kare başı ortalama eşiği aşıyor mu.</summary>
        public static bool IsHot(in AllocationStats s, float avgBytesThreshold = 1024f)
            => s.Frames >= 60 && s.AvgBytesPerFrame > avgBytesThreshold;
    }
}
