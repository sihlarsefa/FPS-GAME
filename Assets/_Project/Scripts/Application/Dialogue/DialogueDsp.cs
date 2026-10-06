using System;
using System.Collections.Generic;

namespace Project.Application.Dialogue
{
    /// <summary>Küçük, bağımsız RBJ biquad (saf, Unity'siz).</summary>
    public sealed class DspBiquad
    {
        public enum Kind { Lowpass, Highpass, Peaking }

        private double _b0 = 1, _b1, _b2, _a1, _a2, _z1, _z2;

        public DspBiquad(Kind kind, double sampleRate, double frequency, double q, double gainDb = 0.0)
        {
            var fc = Math.Max(20.0, Math.Min(frequency, sampleRate * 0.45));
            var w0 = 2.0 * Math.PI * fc / sampleRate;
            var cos = Math.Cos(w0);
            var alpha = Math.Sin(w0) / (2.0 * Math.Max(0.05, q));
            double b0, b1, b2, a0, a1, a2;
            switch (kind)
            {
                case Kind.Highpass:
                    b0 = (1 + cos) / 2; b1 = -(1 + cos); b2 = (1 + cos) / 2;
                    a0 = 1 + alpha; a1 = -2 * cos; a2 = 1 - alpha;
                    break;
                case Kind.Peaking:
                    var a = Math.Pow(10.0, gainDb / 40.0);
                    b0 = 1 + alpha * a; b1 = -2 * cos; b2 = 1 - alpha * a;
                    a0 = 1 + alpha / a; a1 = -2 * cos; a2 = 1 - alpha / a;
                    break;
                default:
                    b0 = (1 - cos) / 2; b1 = 1 - cos; b2 = (1 - cos) / 2;
                    a0 = 1 + alpha; a1 = -2 * cos; a2 = 1 - alpha;
                    break;
            }

            _b0 = b0 / a0; _b1 = b1 / a0; _b2 = b2 / a0; _a1 = a1 / a0; _a2 = a2 / a0;
        }

        public float Process(float x)
        {
            var y = _b0 * x + _z1;
            _z1 = _b1 * x - _a1 * y + _z2;
            _z2 = _b2 * x - _a2 * y;
            return (float)y;
        }

        public void Run(float[] buf)
        {
            for (var i = 0; i < buf.Length; i++)
                buf[i] = Process(buf[i]);
        }
    }

    /// <summary>Hızlı xorshift rastgele (iş parçacığı bağımsız, tekrarlanabilir).</summary>
    public struct DspRng
    {
        private uint _s;

        public DspRng(uint seed) { _s = seed == 0 ? 0x9E3779B9u : seed; }

        private uint Next()
        {
            var x = _s;
            x ^= x << 13; x ^= x >> 17; x ^= x << 5;
            _s = x;
            return x;
        }

        public float Bipolar() => (int)Next() * (1f / 2147483648f);
        public float Unit() => (Next() >> 8) * (1f / 16777216f);
        public float Range(float a, float b) => a + (b - a) * Unit();
    }

    /// <summary>Telsiz işleme ayarları. <see cref="For"/> mesafe/stres/çatışmadan makul bir set üretir.</summary>
    public struct RadioDspSettings
    {
        public float LowCutHz;
        public float HighCutHz;
        /// <summary>Yumuşak doyum sürüşü (tanh); 1 = hafif, 3 = sert.</summary>
        public float Drive;
        public float CompThreshold;
        public float CompRatio;
        public float HissLevel;
        public float SquelchLeadSeconds;
        public float SquelchTailSeconds;
        public float SquelchLevel;
        /// <summary>Saniyede beklenen kopma sayısı (uzak mesafede > 0).</summary>
        public float DropoutRate;
        /// <summary>Konuşan çatışmadaysa arka plan çatışma sızıntısı 0..1.</summary>
        public float BattleBleed;
        /// <summary>Taşıyıcı ıslığı (1..1.4 kHz zayıf ton), 0 = yok.</summary>
        public float CarrierLevel;

        public static RadioDspSettings For(float signalQuality, DialogueStress stress, bool speakerInCombat)
        {
            var q = Math.Max(0f, Math.Min(1f, signalQuality));
            return new RadioDspSettings
            {
                LowCutHz = 300f,
                HighCutHz = 3400f - (1f - q) * 700f,
                Drive = 1.6f + (stress == DialogueStress.Panic ? 0.9f : stress == DialogueStress.Combat ? 0.4f : 0f),
                CompThreshold = 0.22f,
                CompRatio = 4f,
                HissLevel = 0.012f + (1f - q) * 0.05f,
                SquelchLeadSeconds = 0.09f,
                SquelchTailSeconds = 0.17f,
                SquelchLevel = 0.30f,
                DropoutRate = q > 0.55f ? 0f : (0.55f - q) * 2.2f,
                BattleBleed = speakerInCombat ? (stress == DialogueStress.Panic ? 0.9f : 0.6f) : 0f,
                CarrierLevel = 0.004f + (1f - q) * 0.01f
            };
        }
    }

    /// <summary>
    /// Telsiz işlemcisi (float örnekler): 300-3400 Hz bant geçiren (4. derece), yumuşak doyum, sıkıştırıcı,
    /// squelch açılış/kapanış kuyruğu, taşıyıcı tıslaması, uzak mesafede kopma ve konuşan çatışmadaysa arka plan
    /// çatışma sızıntısı. Çıktı = squelch önü + gövde + squelch kuyruğu. Saf mantık, tekrarlanabilir (tohum).
    /// </summary>
    public static class RadioDsp
    {
        public static float[] Process(float[] input, int sampleRate, RadioDspSettings s, uint seed)
        {
            if (input == null || input.Length == 0 || sampleRate <= 0)
                return new float[0];

            var lead = (int)(Math.Max(0f, s.SquelchLeadSeconds) * sampleRate);
            var tail = (int)(Math.Max(0f, s.SquelchTailSeconds) * sampleRate);
            var total = lead + input.Length + tail;
            var rng = new DspRng(seed);
            var buf = new float[total];
            Array.Copy(input, 0, buf, lead, input.Length);

            // 1) Mikrofona sızan çatışma (bant filtresinden önce: telsiz de onu işler).
            if (s.BattleBleed > 0.001f)
                AddBattleBleed(buf, sampleRate, s.BattleBleed, ref rng);

            // 2) Bant geçiren 300-3400 (2x HP + 2x LP, Q 0.707).
            var hp1 = new DspBiquad(DspBiquad.Kind.Highpass, sampleRate, s.LowCutHz, 0.707);
            var hp2 = new DspBiquad(DspBiquad.Kind.Highpass, sampleRate, s.LowCutHz, 0.707);
            var lp1 = new DspBiquad(DspBiquad.Kind.Lowpass, sampleRate, s.HighCutHz, 0.707);
            var lp2 = new DspBiquad(DspBiquad.Kind.Lowpass, sampleRate, s.HighCutHz, 0.707);
            for (var i = 0; i < total; i++)
                buf[i] = lp2.Process(lp1.Process(hp2.Process(hp1.Process(buf[i]))));

            // 3) Sıkıştırıcı + 4) yumuşak doyum
            Compress(buf, sampleRate, s.CompThreshold, s.CompRatio);
            var drive = Math.Max(0.1f, s.Drive);
            var norm = (float)(1.0 / Math.Tanh(drive));
            for (var i = 0; i < total; i++)
                buf[i] = (float)Math.Tanh(buf[i] * drive) * norm * 0.8f;

            // 5) Kopmalar (gövde bölgesinde)
            if (s.DropoutRate > 0.001f)
                ApplyDropouts(buf, lead, input.Length, sampleRate, s.DropoutRate, ref rng);

            // 6) Taşıyıcı tıslaması (bant sınırlı gürültü + zayıf ton) tüm süre boyunca
            AddHiss(buf, sampleRate, s.HissLevel, s.CarrierLevel, ref rng);

            // 7) Squelch açılış (tık + kısa gürültü) ve kapanış kuyruğu (azalan "kşş")
            AddSquelch(buf, 0, lead, sampleRate, s.SquelchLevel, false, ref rng);
            AddSquelch(buf, lead + input.Length, tail, sampleRate, s.SquelchLevel, true, ref rng);

            for (var i = 0; i < total; i++)
            {
                var v = buf[i];
                buf[i] = v > 0.98f ? 0.98f : v < -0.98f ? -0.98f : v;
            }

            return buf;
        }

        public static void Compress(float[] buf, int sampleRate, float threshold, float ratio)
        {
            if (ratio <= 1f || threshold <= 0f)
                return;

            var attack = 1f - (float)Math.Exp(-1.0 / (0.005 * sampleRate));
            var release = 1f - (float)Math.Exp(-1.0 / (0.09 * sampleRate));
            var env = 0f;
            var makeup = 1f + (1f - 1f / ratio) * 0.5f;
            for (var i = 0; i < buf.Length; i++)
            {
                var a = Math.Abs(buf[i]);
                env += (a > env ? attack : release) * (a - env);
                var gain = 1f;
                if (env > threshold)
                    gain = (float)(threshold * Math.Pow(env / threshold, 1.0 / ratio) / env);
                buf[i] *= gain * makeup;
            }
        }

        private static void AddHiss(float[] buf, int sr, float level, float carrier, ref DspRng rng)
        {
            if (level <= 0f && carrier <= 0f)
                return;

            var hp = new DspBiquad(DspBiquad.Kind.Highpass, sr, 600.0, 0.707);
            var lp = new DspBiquad(DspBiquad.Kind.Lowpass, sr, 3600.0, 0.707);
            var phase = 0.0;
            var inc = 2.0 * Math.PI * 1150.0 / sr;
            for (var i = 0; i < buf.Length; i++)
            {
                var n = lp.Process(hp.Process(rng.Bipolar()));
                var am = 0.75f + 0.25f * (float)Math.Sin(i * 2.0 * Math.PI * 7.0 / sr);
                buf[i] += n * level * 3f * am;
                if (carrier > 0f)
                {
                    phase += inc;
                    buf[i] += (float)Math.Sin(phase) * carrier;
                }
            }
        }

        private static void AddSquelch(float[] buf, int start, int length, int sr, float level, bool closing, ref DspRng rng)
        {
            if (length <= 0 || level <= 0f)
                return;

            var hp = new DspBiquad(DspBiquad.Kind.Highpass, sr, 700.0, 0.707);
            var lp = new DspBiquad(DspBiquad.Kind.Lowpass, sr, 3200.0, 0.707);
            for (var i = 0; i < length && start + i < buf.Length; i++)
            {
                var t = (float)i / length;
                // açılış: ilk anda tık sonra hızla kısılan gürültü; kapanış: üstel azalan "kşş"
                var env = closing ? (float)Math.Exp(-4.0 * t) : (float)Math.Exp(-5.0 * t) * (t < 0.06f ? 1f : 0.7f);
                var click = i < 24 ? (closing ? 0.5f : 1f) : 0f;
                var n = lp.Process(hp.Process(rng.Bipolar()));
                buf[start + i] += (n * 1.6f * env + click * rng.Bipolar()) * level;
            }
        }

        private static void ApplyDropouts(float[] buf, int bodyStart, int bodyLength, int sr, float ratePerSecond, ref DspRng rng)
        {
            var seconds = bodyLength / (float)sr;
            var expected = ratePerSecond * seconds;
            var count = (int)expected + (rng.Unit() < expected - (int)expected ? 1 : 0);
            for (var d = 0; d < count; d++)
            {
                var len = (int)(rng.Range(0.04f, 0.14f) * sr);
                var at = bodyStart + (int)(rng.Unit() * Math.Max(1, bodyLength - len));
                var fade = Math.Max(1, (int)(0.004f * sr));
                for (var i = 0; i < len && at + i < buf.Length; i++)
                {
                    var edge = Math.Min(Math.Min(i, len - i), fade) / (float)fade;
                    var g = 1f - edge; // ortada tam kopma
                    buf[at + i] = buf[at + i] * g + rng.Bipolar() * 0.05f * edge;
                }
            }
        }

        private static void AddBattleBleed(float[] buf, int sr, float amount, ref DspRng rng)
        {
            // Ayrı tampon: uzak gümbürtü + kısa kısa tüfek patlaması benzeri üstel gürültü atımları (sesi kirletmesin).
            var bleed = new float[buf.Length];
            var seconds = buf.Length / (float)sr;
            var bursts = (int)(seconds * (3f + 9f * amount));
            for (var b = 0; b < bursts; b++)
            {
                var at = (int)(rng.Unit() * buf.Length);
                var tau = rng.Range(0.025f, 0.08f) * sr;
                var amp = amount * rng.Range(0.08f, 0.22f);
                var len = (int)(tau * 5f);
                for (var i = 0; i < len && at + i < bleed.Length; i++)
                    bleed[at + i] += rng.Bipolar() * amp * (float)Math.Exp(-i / tau);
            }

            var rumble = new DspBiquad(DspBiquad.Kind.Lowpass, sr, 220.0, 0.707);
            var mid = new DspBiquad(DspBiquad.Kind.Lowpass, sr, 2200.0, 0.707);
            for (var i = 0; i < buf.Length; i++)
                buf[i] += mid.Process(bleed[i]) + rumble.Process(rng.Bipolar()) * amount * 0.12f;
        }
    }
}
