using System;
using System.Collections.Generic;

namespace Project.Application.Replay
{
    /// <summary>Tekrar zaman çizelgesi örnekleme: kare arama, doğrusal/açısal aradeğer. Saf mantık.</summary>
    public static class ReplayTimeline
    {
        /// <summary>time'dan küçük veya eşit son karenin indeksi (kare yoksa -1, zaman başın öncesiyse 0).</summary>
        public static int FindFrame(IList<ReplayFrame> frames, float time)
        {
            if (frames == null || frames.Count == 0)
                return -1;
            int lo = 0, hi = frames.Count - 1;
            if (time <= frames[0].Time) return 0;
            if (time >= frames[hi].Time) return hi;
            while (lo < hi)
            {
                var mid = (lo + hi + 1) >> 1;
                if (frames[mid].Time <= time) lo = mid; else hi = mid - 1;
            }
            return lo;
        }

        /// <summary>Derece cinsinden en kısa yoldan aradeğer.</summary>
        public static float LerpAngle(float a, float b, float t)
        {
            var d = ((b - a) % 360f + 540f) % 360f - 180f;
            var r = a + d * t;
            r %= 360f;
            return r < 0f ? r + 360f : r;
        }

        /// <summary>Kimliğe göre karedeki örneği bulur.</summary>
        public static bool TryGetSample(ReplayFrame frame, int id, out ReplayActorSample sample)
        {
            if (frame != null && frame.Actors != null)
                for (var i = 0; i < frame.Actors.Length; i++)
                    if (frame.Actors[i].Id == id)
                    {
                        sample = frame.Actors[i];
                        return true;
                    }
            sample = default;
            return false;
        }

        /// <summary>
        /// Verilen zamanda bir askerin aradeğerlenmiş durumunu verir. Bir kareden diğerine ölü/diriliş değişimi ve
        /// duruş/silah ayrık alanlar ilk karenin değerini kullanır. Asker her iki karede yoksa false.
        /// </summary>
        public static bool Sample(ReplayData data, int id, float time, out ReplayActorSample result)
        {
            result = default;
            if (data == null || data.Frames.Count == 0)
                return false;

            var i = FindFrame(data.Frames, time);
            var a = data.Frames[i];
            var hasA = TryGetSample(a, id, out var sa);
            if (i + 1 >= data.Frames.Count)
            {
                result = sa;
                return hasA;
            }

            var b = data.Frames[i + 1];
            var hasB = TryGetSample(b, id, out var sb);
            if (!hasA && !hasB)
                return false;
            if (!hasA) { result = sb; return true; }
            if (!hasB) { result = sa; return true; }

            var span = b.Time - a.Time;
            var t = span > 1e-5f ? Clamp01((time - a.Time) / span) : 0f;
            result = sa;
            result.X = sa.X + (sb.X - sa.X) * t;
            result.Y = sa.Y + (sb.Y - sa.Y) * t;
            result.Z = sa.Z + (sb.Z - sa.Z) * t;
            result.Yaw = LerpAngle(sa.Yaw, sb.Yaw, t);
            result.Pitch = sa.Pitch + (sb.Pitch - sa.Pitch) * t;
            result.Alive = t < 0.5f ? sa.Alive : sb.Alive;
            return true;
        }

        /// <summary>[from, to) aralığındaki olayların indeks aralığı başlangıcı (olaylar zaman sıralı).</summary>
        public static int FirstEventAtOrAfter(IList<ReplayEvent> events, float time)
        {
            if (events == null) return 0;
            int lo = 0, hi = events.Count;
            while (lo < hi)
            {
                var mid = (lo + hi) >> 1;
                if (events[mid].Time < time) lo = mid + 1; else hi = mid;
            }
            return lo;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
