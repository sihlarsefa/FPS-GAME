using System;
using System.Collections.Generic;

namespace Project.Application.Dialogue
{
    /// <summary>
    /// Ses kimliği işleme (float örnekler): perde (yeniden örnekleme), formant rengi (iki tepe EQ), şiddet (kazanç +
    /// doyum), parça birleştirme (boşluk + çapraz geçiş) ve prosedürel yedek konuşma. Saf mantık.
    /// </summary>
    public static class VoiceDsp
    {
        /// <summary>Doğrusal yeniden örnekleme: ratio &gt; 1 daha kısa ve tiz.</summary>
        public static float[] Resample(float[] input, float ratio)
        {
            if (input == null || input.Length == 0)
                return new float[0];
            ratio = Math.Max(0.5f, Math.Min(2f, ratio));
            if (Math.Abs(ratio - 1f) < 0.001f)
                return (float[])input.Clone();

            var n = Math.Max(1, (int)(input.Length / ratio));
            var output = new float[n];
            for (var i = 0; i < n; i++)
            {
                var pos = i * (double)ratio;
                var i0 = (int)pos;
                var frac = (float)(pos - i0);
                var a = input[Math.Min(i0, input.Length - 1)];
                var b = input[Math.Min(i0 + 1, input.Length - 1)];
                output[i] = a + (b - a) * frac;
            }

            return output;
        }

        /// <summary>Formant rengi: shift &lt; 1 gövdeli/derin (F1/F2 aşağı), &gt; 1 ince/parlak.</summary>
        public static void ApplyFormant(float[] buf, int sampleRate, float formantShift)
        {
            if (buf == null || buf.Length == 0 || Math.Abs(formantShift - 1f) < 0.005f)
                return;

            var shift = Math.Max(0.8f, Math.Min(1.25f, formantShift));
            var gain = (shift - 1f) * 40f; // 0.9 -> -4 dB / +4 dB
            // Orijinal F1/F2 bölgesi azaltılır, kaymış bölgeye yaklaşılır: iki tepe EQ çifti.
            var f1a = new DspBiquad(DspBiquad.Kind.Peaking, sampleRate, 550.0, 1.2, -gain * 0.6);
            var f1b = new DspBiquad(DspBiquad.Kind.Peaking, sampleRate, 550.0 * shift, 1.2, gain);
            var f2a = new DspBiquad(DspBiquad.Kind.Peaking, sampleRate, 1600.0, 1.0, -gain * 0.6);
            var f2b = new DspBiquad(DspBiquad.Kind.Peaking, sampleRate, 1600.0 * shift, 1.0, gain);
            for (var i = 0; i < buf.Length; i++)
                buf[i] = f2b.Process(f2a.Process(f1b.Process(f1a.Process(buf[i]))));
        }

        /// <summary>Şiddet: tepe normalize + 0.5..1.5 aralığında doyum (zorlanan/bağıran ses).</summary>
        public static void ApplyIntensity(float[] buf, float intensity, float targetPeak = 0.85f)
        {
            if (buf == null || buf.Length == 0)
                return;

            var peak = 0f;
            for (var i = 0; i < buf.Length; i++)
                peak = Math.Max(peak, Math.Abs(buf[i]));
            if (peak < 1e-5f)
                return;

            var drive = 1f + Math.Max(0f, intensity - 0.8f) * 1.6f;
            var gain = targetPeak / peak;
            var norm = (float)(1.0 / Math.Tanh(drive));
            for (var i = 0; i < buf.Length; i++)
                buf[i] = (float)Math.Tanh(buf[i] * gain * drive) * norm * targetPeak;
        }

        /// <summary>Kimliği uygular: perde*hız^0.35 örnekleme, formant, şiddet.</summary>
        public static float[] ApplyIdentity(float[] clip, int sampleRate, VoiceIdentity id)
        {
            var ratio = id.Pitch * (float)Math.Pow(Math.Max(0.5f, id.Speed), 0.35);
            var buf = Resample(clip, ratio);
            ApplyFormant(buf, sampleRate, id.Formant);
            ApplyIntensity(buf, id.Intensity);
            return buf;
        }

        /// <summary>Parçaları boşluk (hızla kısalır) ve kısa çapraz geçişle birleştirir. gapSeconds = temel boşluk.</summary>
        public static float[] Stitch(IList<float[]> parts, int sampleRate, float gapSeconds, float speed)
        {
            if (parts == null || parts.Count == 0)
                return new float[0];

            var gap = (int)(Math.Max(0f, gapSeconds) / Math.Max(0.5f, speed) * sampleRate);
            var fade = Math.Max(1, (int)(0.004f * sampleRate));
            var total = 0;
            var count = 0;
            for (var i = 0; i < parts.Count; i++)
            {
                if (parts[i] == null || parts[i].Length == 0)
                    continue;
                total += parts[i].Length + (count > 0 ? gap : 0);
                count++;
            }

            var output = new float[total];
            var pos = 0;
            var first = true;
            for (var i = 0; i < parts.Count; i++)
            {
                var p = parts[i];
                if (p == null || p.Length == 0)
                    continue;
                if (!first)
                    pos += gap;
                first = false;
                for (var s = 0; s < p.Length; s++)
                {
                    var w = 1f;
                    if (s < fade) w = s / (float)fade;
                    else if (p.Length - s <= fade) w = (p.Length - s) / (float)fade;
                    output[pos + s] += p[s] * w;
                }

                pos += p.Length;
            }

            return output;
        }

        /// <summary>Başı/sonu sessizlikten kırpar (TTS klipleri için; parça birleştirme sıkı olur).</summary>
        public static float[] TrimSilence(float[] input, float threshold = 0.01f, int keepSamples = 64)
        {
            if (input == null || input.Length == 0)
                return new float[0];
            var a = 0;
            var b = input.Length - 1;
            while (a < b && Math.Abs(input[a]) < threshold) a++;
            while (b > a && Math.Abs(input[b]) < threshold) b--;
            a = Math.Max(0, a - keepSamples);
            b = Math.Min(input.Length - 1, b + keepSamples);
            var n = b - a + 1;
            var output = new float[n];
            Array.Copy(input, a, output, 0, n);
            return output;
        }
    }

    /// <summary>
    /// Prosedürel yedek konuşma (klip yoksa): metinden hece benzeri formant vızıltısı. Sesli harfler F1/F2 ile
    /// tonlanır (kimliğin perdesi ve formantı), ünsüzler kısa gürültü patlaması; anlaşılır değil ama ritim, uzunluk ve
    /// stres uyumludur. Süre metin uzunluğuyla orantılı.
    /// </summary>
    public static class ProceduralVoice
    {
        // Türkçe ünlüler: (F1, F2) Hz
        private static readonly Dictionary<char, float[]> Vowels = new Dictionary<char, float[]>
        {
            { 'a', new[] { 800f, 1250f } }, { 'e', new[] { 550f, 1800f } }, { 'ı', new[] { 380f, 1450f } },
            { 'i', new[] { 300f, 2200f } }, { 'o', new[] { 500f, 900f } }, { 'ö', new[] { 450f, 1500f } },
            { 'u', new[] { 330f, 800f } }, { 'ü', new[] { 300f, 1650f } }
        };

        public static float[] Synthesize(string text, VoiceIdentity id, DialogueStress stress, int sampleRate, uint seed)
        {
            if (string.IsNullOrEmpty(text) || sampleRate <= 0)
                return new float[0];

            var rng = new DspRng(seed);
            var speed = Math.Max(0.6f, id.Speed);
            var f0 = 112f * id.Pitch * (stress == DialogueStress.Panic ? 1.12f : 1f);
            var list = new List<float[]>(text.Length);
            var phase = 0.0;
            var lower = text.ToLowerInvariant();

            for (var i = 0; i < lower.Length; i++)
            {
                var ch = lower[i];
                var contour = 1f + 0.08f * (float)Math.Sin(i * 0.7); // doğal tonlama dalgası
                if (Vowels.TryGetValue(ch, out var f))
                {
                    var dur = 0.085f / speed;
                    var n = Math.Max(8, (int)(dur * sampleRate));
                    var seg = new float[n];
                    var r1 = new DspBiquad(DspBiquad.Kind.Peaking, sampleRate, f[0] * id.Formant, 4.0, 14.0);
                    var r2 = new DspBiquad(DspBiquad.Kind.Peaking, sampleRate, f[1] * id.Formant, 5.0, 11.0);
                    var inc = 2.0 * Math.PI * f0 * contour / sampleRate;
                    for (var k = 0; k < n; k++)
                    {
                        phase += inc;
                        if (phase > 2.0 * Math.PI) phase -= 2.0 * Math.PI;
                        // testere + üçgen karışımı: zengin harmonikli glottal pulse
                        var saw = (float)(phase / Math.PI - 1.0);
                        var env = (float)Math.Sin(Math.PI * k / n);
                        seg[k] = r2.Process(r1.Process(saw * 0.35f)) * env * 0.5f;
                    }

                    list.Add(seg);
                }
                else if (char.IsLetter(ch))
                {
                    var n = Math.Max(8, (int)(0.028f / speed * sampleRate));
                    var seg = new float[n];
                    var hp = new DspBiquad(DspBiquad.Kind.Highpass, sampleRate, "sşçzjfhs".IndexOf(ch) >= 0 ? 3500.0 : 900.0, 0.707);
                    for (var k = 0; k < n; k++)
                        seg[k] = hp.Process(rng.Bipolar()) * (float)Math.Exp(-4.0 * k / n) * 0.35f;
                    list.Add(seg);
                }
                else if (ch == ' ' || ch == ',')
                {
                    list.Add(new float[Math.Max(1, (int)(0.04f / speed * sampleRate))]);
                }
                else if (ch == '.' || ch == '!' || ch == '?')
                {
                    list.Add(new float[Math.Max(1, (int)(0.07f / speed * sampleRate))]);
                }
            }

            var joined = VoiceDsp.Stitch(list, sampleRate, 0f, 1f);
            VoiceDsp.ApplyIntensity(joined, id.Intensity, 0.7f);
            return joined;
        }
    }
}
