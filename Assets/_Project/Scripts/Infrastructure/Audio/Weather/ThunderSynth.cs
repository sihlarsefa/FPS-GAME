using System;
using static Project.Infrastructure.Audio.SynthDsp;

namespace Project.Infrastructure.Audio.Weather
{
    /// <summary>
    /// Prosedürel gök gürültüsü (saf C#, iş parçacığı güvenli): yakın şimşekte keskin "crack" + uzun yuvarlanan rumble.
    /// Mesafe arttıkça crack kaybolur, rumble uzar ve alçak geçiren kesim frekansı düşer.
    /// </summary>
    public static class ThunderSynth
    {
        public const float NearMeters = 300f;
        public const float FarMeters = 3000f;

        /// <summary>Mesafeye göre alçak geçiren kesim (Hz): yakın ~9 kHz, uzak ~350 Hz (üstel).</summary>
        public static float CutoffHz(float distanceM)
        {
            var t = Norm(distanceM);
            return 9000f * (float)Math.Pow(350f / 9000f, t);
        }

        /// <summary>Crack seviyesi (0..1): yalnız yakında; 0.35 normalleşmiş mesafeden sonra yok.</summary>
        public static float CrackAmount(float distanceM) => Clamp01(1f - Norm(distanceM) / 0.35f);

        /// <summary>Klip süresi (sn): yakın ~2.6, uzak ~7.</summary>
        public static float DurationSeconds(float distanceM) => 2.6f + 4.4f * Norm(distanceM);

        public static float[] Render(float distanceM, int seed = 1)
        {
            var rng = new SynthRng(0x7B0D3A11u ^ (uint)(seed * 2654435761u));
            var dur = DurationSeconds(distanceM);
            var b = Buffer(dur);
            var t = Norm(distanceM);

            // Crack: kısa geniş bantlı çatlama + alçak "thump".
            var crack = CrackAmount(distanceM);
            if (crack > 0f)
            {
                NoiseBurst(b, ref rng, 0f, 0.0008f, 0.02f, 1.0f * crack, FilterKind.Highpass, 1200f, 0.707f);
                NoiseBurst(b, ref rng, 0.004f, 0.002f, 0.09f, 0.8f * crack, FilterKind.Lowpass, 1800f, 0.707f);
                Tone(b, 0f, 90f, 40f, 0.05f, 0.002f, 0.18f, 0.7f * crack);
            }

            // Rumble: art arda gelen alçak gürültü dalgaları (uzun kuyruk).
            var start = 0.02f + 0.25f * t;
            var pieces = 4 + rng.RangeInt(0, 3);
            for (var i = 0; i < pieces; i++)
            {
                var at = start + rng.Range(0f, dur * 0.35f);
                var len = Math.Min(dur - at - 0.05f, rng.Range(0.9f, 1.8f) + 1.6f * t);
                if (len <= 0.1f)
                    continue;
                NoiseSwell(b, ref rng, at, len, rng.Range(0.45f, 0.9f), FilterKind.Lowpass,
                    rng.Range(260f, 420f), rng.Range(90f, 180f), 0.707f, 0.15f);
            }
            NoiseSwell(b, ref rng, start, dur - start - 0.05f, 0.7f, FilterKind.Lowpass, 160f, 70f, 0.707f, 0.1f);

            // Mesafe alçak geçirmesi (iki kutup) + normalleştirme.
            var lp1 = new OnePole(CutoffHz(distanceM));
            var lp2 = new OnePole(CutoffHz(distanceM));
            var peak = 0f;
            for (var i = 0; i < b.Length; i++)
            {
                var y = lp2.Lowpass(lp1.Lowpass(b[i]));
                b[i] = y;
                var a = Math.Abs(y);
                if (a > peak) peak = a;
            }
            var g = peak > 1e-6f ? 0.9f / peak : 0f;
            var fade = Math.Max(1, (int)(0.15f * SampleRate));
            for (var i = 0; i < b.Length; i++)
            {
                b[i] *= g;
                var rem = b.Length - 1 - i;
                if (rem < fade) b[i] *= rem / (float)fade;
            }
            return b;
        }

        private static float Norm(float d)
        {
            if (float.IsNaN(d)) return 1f;
            return Clamp01((d - NearMeters) / (FarMeters - NearMeters));
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
