using System;

namespace Project.Infrastructure.Audio.Ui
{
    /// <summary>
    /// Uzak topçu sesi (prosedürel): önce alçak "thump" ve başlangıç çatlağı, sonra uzaklıkla artan, alçak geçirenle
    /// yumuşatılmış yankı/gürleme kuyruğu. Uzaklık km cinsinden: kesim frekansı ve düzey UiAudioRules ile belirlenir.
    /// Çıktı mono, tepe 0.8; ses düzeyi çalma tarafında katman payıyla verilir.
    /// </summary>
    public static class DistantArtillerySynth
    {
        public const int Rate = 22050;

        public static float DurationSeconds(float km) => 2.4f + 0.5f * Math.Min(8f, Math.Max(0.5f, km));

        public static float[] Render(float km, uint seed)
        {
            km = Math.Min(8f, Math.Max(0.5f, km));
            var dur = DurationSeconds(km);
            var n = (int)(dur * Rate);
            var d = new float[n];
            var rng = seed | 1u;
            float Noise() { rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5; return (rng & 0xFFFFFF) / (float)0x800000 - 1f; }

            var cut = UiAudioRules.DistanceCutoffHz(km);
            var k = 1f - (float)Math.Exp(-2.0 * Math.PI * cut / Rate);
            var kLow = 1f - (float)Math.Exp(-2.0 * Math.PI * 70.0 / Rate);
            float a = 0f, b = 0f, lo = 0f;
            double ph = 0;
            var rumbleTau = 0.9f + 0.28f * km;
            for (var i = 0; i < n; i++)
            {
                var t = i / (float)Rate;
                a += k * (Noise() - a);
                b += k * (a - b);        // 2 kutuplu alçak geçiren
                lo += kLow * (Noise() - lo);
                ph += 2.0 * Math.PI * (34.0 + 18.0 * Math.Exp(-t * 6.0)) / Rate;
                var thump = (float)Math.Sin(ph) * UiSfxSynth.Envelope(t, 0.012f, 0.22f);
                var crack = b * 2.5f * UiSfxSynth.Envelope(t, 0.006f, 0.12f);
                var rumble = lo * 5f * UiSfxSynth.Envelope(t, 0.15f, rumbleTau);
                d[i] = thump * 0.9f + crack + rumble * 0.6f;
            }
            // Dağınık yankı: üç gecikmeli, kısılmış kopya (dağlardan dönen yankı).
            var echoes = new[] { (int)(0.37f * Rate), (int)(0.81f * Rate), (int)(1.43f * Rate) };
            var gains = new[] { 0.35f, 0.22f, 0.12f };
            var src = (float[])d.Clone();
            for (var e = 0; e < echoes.Length; e++)
                for (var i = echoes[e]; i < n; i++)
                    d[i] += src[i - echoes[e]] * gains[e];
            // Sona yumuşak kuyruk
            var fade = Math.Min(n, Rate / 2);
            for (var i = 0; i < fade; i++) d[n - 1 - i] *= i / (float)fade;
            var peak = LoudnessMath.PeakLinear(d);
            var g = peak > 1e-6f ? 0.8f / peak : 1f;
            for (var i = 0; i < n; i++) d[i] *= g;
            return d;
        }
    }
}
