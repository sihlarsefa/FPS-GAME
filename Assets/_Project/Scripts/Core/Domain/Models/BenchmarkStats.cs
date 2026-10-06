using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Project.Core.Domain
{
    /// <summary>Benchmark özeti: ortalama, %1 low (en kötü %1 karenin ortalaması), en kötü kare.</summary>
    public readonly struct BenchmarkSummary
    {
        public readonly int Frames;
        public readonly float AvgMs, AvgFps, OnePercentLowMs, OnePercentLowFps, WorstMs, WorstFps;

        public BenchmarkSummary(int frames, float avgMs, float lowMs, float worstMs)
        {
            Frames = frames;
            AvgMs = avgMs;
            OnePercentLowMs = lowMs;
            WorstMs = worstMs;
            AvgFps = avgMs > 0.0001f ? 1000f / avgMs : 0f;
            OnePercentLowFps = lowMs > 0.0001f ? 1000f / lowMs : 0f;
            WorstFps = worstMs > 0.0001f ? 1000f / worstMs : 0f;
        }
    }

    /// <summary>Saf mantık: kare süresi (ms) listesinden özet ve CSV üretir.</summary>
    public static class BenchmarkStats
    {
        public static BenchmarkSummary Summarize(IReadOnlyList<float> frameMs)
        {
            if (frameMs == null || frameMs.Count == 0)
                return new BenchmarkSummary(0, 0f, 0f, 0f);

            var n = frameMs.Count;
            var sorted = new float[n];
            double sum = 0;
            for (var i = 0; i < n; i++) { sorted[i] = frameMs[i]; sum += frameMs[i]; }
            Array.Sort(sorted);

            var take = Math.Max(1, n / 100);
            double lowSum = 0;
            for (var i = 0; i < take; i++) lowSum += sorted[n - 1 - i];

            return new BenchmarkSummary(n, (float)(sum / n), (float)(lowSum / take), sorted[n - 1]);
        }

        public const string CsvHeader = "map,tier,resolution,frames,duration_s,avg_ms,avg_fps,low1_ms,low1_fps,worst_ms,worst_fps";

        public static string ToCsv(string map, string tier, string resolution, float durationSeconds, BenchmarkSummary s)
        {
            var c = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine(CsvHeader);
            sb.Append(map).Append(',').Append(tier).Append(',').Append(resolution).Append(',')
              .Append(s.Frames.ToString(c)).Append(',')
              .Append(durationSeconds.ToString("0.0", c)).Append(',')
              .Append(s.AvgMs.ToString("0.000", c)).Append(',').Append(s.AvgFps.ToString("0.0", c)).Append(',')
              .Append(s.OnePercentLowMs.ToString("0.000", c)).Append(',').Append(s.OnePercentLowFps.ToString("0.0", c)).Append(',')
              .Append(s.WorstMs.ToString("0.000", c)).Append(',').Append(s.WorstFps.ToString("0.0", c))
              .AppendLine();
            return sb.ToString();
        }

        /// <summary>
        /// Sabit güzergâh: t∈[0,1] → (konum x,z, bakış x,z) harita yarı boyuna göre normalize.
        /// Dış halka (r=0.55) tam tur, ardından merkezden geçen çapraz; deterministik.
        /// </summary>
        public static void EvaluatePath(float t, float cx, float cz, float half,
            out float px, out float pz, out float lx, out float lz)
        {
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            const double twoPi = Math.PI * 2.0;
            if (t < 0.75f)
            {
                var a = t / 0.75f * twoPi;
                var r = half * 0.55;
                px = cx + (float)(Math.Cos(a) * r);
                pz = cz + (float)(Math.Sin(a) * r);
                // bakış: merkeze doğru hafif yana
                lx = cx + (float)(Math.Cos(a + 0.9) * half * 0.25);
                lz = cz + (float)(Math.Sin(a + 0.9) * half * 0.25);
            }
            else
            {
                var u = (t - 0.75f) / 0.25f;
                px = cx + (float)(-half * 0.5 + u * half);
                pz = cz + (float)(-half * 0.5 + u * half);
                lx = px + (float)(half * 0.2);
                lz = pz - (float)(half * 0.2);
            }
        }
    }
}
