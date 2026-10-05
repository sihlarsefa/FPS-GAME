using System;

namespace Project.Infrastructure.Audio
{
    /// <summary>
    /// Hızlı, iş parçacığı güvenli xorshift rastgele üreteci (UnityEngine.Random ana iş parçacığına bağlı olduğu
    /// için sentez bunu kullanır).
    /// </summary>
    internal struct SynthRng
    {
        private uint _state;

        public SynthRng(uint seed)
        {
            _state = seed == 0u ? 0x9E3779B9u : seed;
        }

        private uint NextUInt()
        {
            var x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        /// <summary>[-1, 1) aralığında beyaz gürültü örneği.</summary>
        public float Bipolar() => (int)NextUInt() * (1f / 2147483648f);

        /// <summary>[0, 1) aralığında değer.</summary>
        public float Unit() => (NextUInt() >> 8) * (1f / 16777216f);

        public float Range(float min, float max) => min + (max - min) * Unit();

        public int RangeInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
                return minInclusive;

            return minInclusive + (int)(NextUInt() % (uint)(maxExclusive - minInclusive));
        }

        public bool Chance(float probability) => Unit() < probability;
    }

    /// <summary>Tek kutuplu alçak/yüksek geçiren filtre.</summary>
    internal struct OnePole
    {
        private float _a;
        private float _y;

        public OnePole(float cutoffHz)
        {
            _a = Coefficient(cutoffHz);
            _y = 0f;
        }

        public static float Coefficient(float cutoffHz)
        {
            var fc = Math.Max(1f, Math.Min(cutoffHz, SynthDsp.SampleRate * 0.45f));
            return 1f - (float)Math.Exp(-2.0 * Math.PI * fc / SynthDsp.SampleRate);
        }

        public void SetCutoff(float cutoffHz) => _a = Coefficient(cutoffHz);

        public float Lowpass(float x)
        {
            _y += _a * (x - _y);
            return _y;
        }

        public float Highpass(float x)
        {
            _y += _a * (x - _y);
            return x - _y;
        }

        public void Reset() => _y = 0f;
    }

    internal enum FilterKind
    {
        None,
        Lowpass,
        Highpass,
        Bandpass
    }

    /// <summary>RBJ biquad (transpoze doğrudan form II).</summary>
    internal struct Biquad
    {
        private float _b0, _b1, _b2, _a1, _a2;
        private float _z1, _z2;

        public static Biquad Create(FilterKind kind, float frequency, float q)
        {
            var f = new Biquad();
            f.Set(kind, frequency, q);
            return f;
        }

        public void Set(FilterKind kind, float frequency, float q)
        {
            if (kind == FilterKind.None)
            {
                _b0 = 1f;
                _b1 = _b2 = _a1 = _a2 = 0f;
                return;
            }

            var fc = Math.Max(10f, Math.Min(frequency, SynthDsp.SampleRate * 0.45f));
            var w0 = 2.0 * Math.PI * fc / SynthDsp.SampleRate;
            var cos = Math.Cos(w0);
            var alpha = Math.Sin(w0) / (2.0 * Math.Max(0.05f, q));
            var a0 = 1.0 + alpha;
            double b0, b1, b2;

            switch (kind)
            {
                case FilterKind.Highpass:
                    b0 = (1.0 + cos) * 0.5;
                    b1 = -(1.0 + cos);
                    b2 = (1.0 + cos) * 0.5;
                    break;
                case FilterKind.Bandpass:
                    b0 = alpha;
                    b1 = 0.0;
                    b2 = -alpha;
                    break;
                default:
                    b0 = (1.0 - cos) * 0.5;
                    b1 = 1.0 - cos;
                    b2 = (1.0 - cos) * 0.5;
                    break;
            }

            _b0 = (float)(b0 / a0);
            _b1 = (float)(b1 / a0);
            _b2 = (float)(b2 / a0);
            _a1 = (float)(-2.0 * cos / a0);
            _a2 = (float)((1.0 - alpha) / a0);
        }

        public float Process(float x)
        {
            var y = _b0 * x + _z1;
            _z1 = _b1 * x - _a1 * y + _z2;
            _z2 = _b2 * x - _a2 * y;
            return y;
        }

        public void Reset()
        {
            _z1 = 0f;
            _z2 = 0f;
        }
    }

    /// <summary>
    /// Sentez yardımcıları. Tüm fonksiyonlar saf matematiktir (UnityEngine çağrısı yok) — iş parçacığı güvenlidir.
    /// "wrap" parametresi döngü seslerinde örnekleri tampon sonundan başa sarar (kesintisiz döngü).
    /// </summary>
    internal static class SynthDsp
    {
        public const int SampleRate = 44100;
        public const float Dt = 1f / SampleRate;

        /// <summary>
        /// Dairesel filtrelerde ısınma geçişinin uzunluğu (1 s). Kullanılan en yavaş filtrenin (18 Hz DC filtresi,
        /// τ ≈ 9 ms) durumu bu sürede tamamen oturur; tüm tamponu iki kez işlemekten çok daha ucuzdur.
        /// </summary>
        public const int CircularWarmupSamples = SampleRate;

        private const int TableBits = 12;
        private const int TableSize = 1 << TableBits;
        private static readonly float[] SineTable = BuildSineTable();
        private static readonly float[] SawTable = BuildSawTable();

        // ------------------------------------------------------------------ tablolar

        private static float[] BuildSineTable()
        {
            var t = new float[TableSize + 1];
            for (var i = 0; i <= TableSize; i++)
                t[i] = (float)Math.Sin(2.0 * Math.PI * i / TableSize);
            return t;
        }

        /// <summary>Bant sınırlı, yumuşatılmış testere dişi (pad/drone/motor için).</summary>
        private static float[] BuildSawTable()
        {
            var t = new float[TableSize + 1];
            const int harmonics = 40;
            var peak = 0f;
            for (var i = 0; i <= TableSize; i++)
            {
                var x = 2.0 * Math.PI * i / TableSize;
                var v = 0.0;
                for (var k = 1; k <= harmonics; k++)
                {
                    var rolloff = 1.0 / (1.0 + Math.Pow(k / 14.0, 2.0));
                    v += Math.Sin(k * x) / k * rolloff;
                }

                t[i] = (float)v;
                peak = Math.Max(peak, Math.Abs(t[i]));
            }

            if (peak > 0f)
            {
                for (var i = 0; i <= TableSize; i++)
                    t[i] /= peak;
            }

            return t;
        }

        /// <summary>Faz (döngü cinsinden) için tablo sinüsü.</summary>
        public static float Sin(double phaseCycles) => Lookup(SineTable, phaseCycles);

        /// <summary>Faz (döngü cinsinden) için yumuşak testere dişi.</summary>
        public static float Saw(double phaseCycles) => Lookup(SawTable, phaseCycles);

        private static float Lookup(float[] table, double phase)
        {
            // Math.Floor yerine kesme (negatif fazlar düzeltilir) — sıcak döngülerde belirgin hız kazancı.
            var p = phase - (long)phase;
            if (p < 0.0)
                p += 1.0;
            var f = (float)(p * TableSize);
            var i = (int)f;
            if (i >= TableSize)
                i = TableSize - 1;
            var frac = f - i;
            return table[i] + (table[i + 1] - table[i]) * frac;
        }

        /// <summary>[0, 1) aralığındaki faz için hızlı testere dişi (faz biriktiricili osilatörler).</summary>
        public static float SawUnit(float phase01)
        {
            var f = phase01 * TableSize;
            var i = (int)f;
            if (i >= TableSize)
                i = TableSize - 1;
            else if (i < 0)
                i = 0;
            var frac = f - i;
            return SawTable[i] + (SawTable[i + 1] - SawTable[i]) * frac;
        }

        /// <summary>
        /// Yazma aralığını hazırlar: başlangıç indeksi (dairesel ise sarılmış) ve yazılabilecek örnek sayısı.
        /// Dairesel olmayan tamponda başlangıç tampon dışındaysa false döner.
        /// </summary>
        public static bool PrepareSpan(int length, int s0, int requested, bool wrap, out int index, out int count)
        {
            index = 0;
            count = 0;
            if (length <= 0 || requested <= 0)
                return false;

            if (wrap)
            {
                index = s0 % length;
                if (index < 0)
                    index += length;
                count = Math.Min(requested, length);
                return true;
            }

            if (s0 < 0)
                s0 = 0;
            if (s0 >= length)
                return false;

            index = s0;
            count = Math.Min(requested, length - s0);
            return count > 0;
        }

        // ------------------------------------------------------------------ genel

        public static int Samples(float seconds) => Math.Max(1, (int)(seconds * SampleRate));

        public static float[] Buffer(float seconds) => new float[Samples(seconds)];

        /// <summary>Örnek başına çarpımsal sönüm katsayısı (tau saniye).</summary>
        public static float DecayFactor(float tau) => tau <= 0f ? 0f : (float)Math.Exp(-1.0 / (tau * SampleRate));

        /// <summary>
        /// Filtrelenmiş beyaz gürültünün seviyesini bant genişliğine göre dengeler; böylece "amp" parametreleri
        /// filtre frekansından bağımsız olarak benzer algılanır.
        /// </summary>
        public static float BandwidthGain(FilterKind kind, float frequency, float q)
        {
            float bandwidth;
            switch (kind)
            {
                case FilterKind.Lowpass:
                    bandwidth = frequency;
                    break;
                case FilterKind.Bandpass:
                    bandwidth = frequency / Math.Max(0.1f, q);
                    break;
                default:
                    return 1f;
            }

            var gain = (float)Math.Sqrt(SampleRate * 0.5 / Math.Max(10f, bandwidth)) * 0.5f;
            return Math.Max(1f, Math.Min(gain, 24f));
        }

        public static int Index(int i, int length, bool wrap)
        {
            if (!wrap)
                return i < length ? i : -1;

            i %= length;
            return i < 0 ? i + length : i;
        }

        // ------------------------------------------------------------------ katmanlar

        /// <summary>Filtrelenmiş gürültü patlaması: doğrusal atak + üstel sönüm.</summary>
        public static void NoiseBurst(float[] buf, ref SynthRng rng, float start, float attack, float tau, float amp,
            FilterKind kind, float frequency, float q = 0.707f, bool wrap = false)
        {
            if (amp == 0f || tau <= 0f)
                return;

            var s0 = (int)(start * SampleRate);
            var attackSamples = Math.Max(1, (int)(attack * SampleRate));
            if (!PrepareSpan(buf.Length, s0, attackSamples + (int)(tau * 7f * SampleRate), wrap, out var idx, out var total))
                return;

            var filter = Biquad.Create(kind, frequency, q);
            var gain = amp * BandwidthGain(kind, frequency, q);
            var k = DecayFactor(tau);
            var env = 1f;
            var len = buf.Length;
            var invAttack = 1f / attackSamples;
            for (var j = 0; j < total; j++)
            {
                float e;
                if (j < attackSamples)
                {
                    e = j * invAttack;
                }
                else
                {
                    env *= k;
                    e = env;
                }

                buf[idx] += filter.Process(rng.Bipolar()) * e * gain;
                if (++idx == len)
                    idx = 0;
            }
        }

        /// <summary>Filtrelenmiş gürültü bölümü: hann zarfı, isteğe bağlı merkez frekans kayması.</summary>
        public static void NoiseSwell(float[] buf, ref SynthRng rng, float start, float duration, float amp,
            FilterKind kind, float freqStart, float freqEnd, float q = 0.707f, float grain = 0f, bool wrap = false)
        {
            if (amp == 0f || duration <= 0f)
                return;

            var s0 = (int)(start * SampleRate);
            var n = Samples(duration);
            if (!PrepareSpan(buf.Length, s0, n, wrap, out var idx, out var total))
                return;

            var filter = Biquad.Create(kind, freqStart, q);
            var gainStart = BandwidthGain(kind, freqStart, q);
            var gainEnd = BandwidthGain(kind, freqEnd, q);
            var grainEnv = 1f;
            var grainTarget = 1f;
            var len = buf.Length;
            var invN = 1f / n;
            for (var j = 0; j < total; j++)
            {
                var u = j * invN;
                if ((j & 31) == 0 && freqStart != freqEnd)
                    filter.Set(kind, freqStart * (float)Math.Pow(freqEnd / freqStart, u), q);

                if (grain > 0f && (j & 127) == 0)
                    grainTarget = rng.Unit() < grain ? rng.Range(0.6f, 1.4f) : rng.Range(0.05f, 0.4f);
                grainEnv += (grainTarget - grainEnv) * 0.02f;

                // Hann = sin²(πu) (tablo).
                var sh = Sin(u * 0.5);
                var hann = sh * sh;
                var g = gainStart + (gainEnd - gainStart) * u;
                buf[idx] += filter.Process(rng.Bipolar()) * hann * amp * g * (grain > 0f ? grainEnv : 1f);
                if (++idx == len)
                    idx = 0;
            }
        }

        /// <summary>Çok kısa, yüksek geçirilmiş tık (mekanik parçalar).</summary>
        public static void Click(float[] buf, ref SynthRng rng, float start, float amp, float highpass = 2500f,
            float tau = 0.0015f, bool wrap = false)
        {
            NoiseBurst(buf, ref rng, start, 0.0002f, tau, amp, FilterKind.Highpass, highpass, 0.7f, wrap);
        }

        /// <summary>
        /// Sönümlenen sinüs (frekans üstel olarak f0'dan f1'e kayar). waveform: 0 sinüs, 1 testere.
        /// </summary>
        public static void Tone(float[] buf, float start, float f0, float f1, float sweepTau, float attack, float tau,
            float amp, int waveform = 0, bool wrap = false, float maxDuration = -1f)
        {
            if (amp == 0f || tau <= 0f)
                return;

            var s0 = (int)(start * SampleRate);
            var attackSamples = Math.Max(1, (int)(attack * SampleRate));
            var requested = attackSamples + (int)(tau * 7f * SampleRate);
            if (maxDuration > 0f)
                requested = Math.Min(requested, Samples(maxDuration));
            if (!PrepareSpan(buf.Length, s0, requested, wrap, out var idx, out var total))
                return;

            var len = buf.Length;
            var k = DecayFactor(tau);
            var sweepK = sweepTau > 0f ? DecayFactor(sweepTau) : 0f;
            var sweep = 1f;
            var env = 1f;
            var phase = 0.0;
            for (var j = 0; j < total; j++)
            {
                var f = f1 + (f0 - f1) * sweep;
                sweep *= sweepK;
                phase += f * Dt;

                float e;
                if (j < attackSamples)
                {
                    e = (float)j / attackSamples;
                }
                else
                {
                    env *= k;
                    e = env;
                }

                var osc = waveform == 1 ? Saw(phase) : Sin(phase);
                buf[idx] += osc * e * amp;
                if (++idx == len)
                    idx = 0;
            }
        }

        /// <summary>Sabit zarflı ton: atak, düz bölüm, salınım (müzik notaları, alarm).</summary>
        public static void Note(float[] buf, float start, float duration, float frequency, float attack, float release,
            float amp, int waveform = 0, float detune = 0f, float vibratoHz = 0f, float vibratoDepth = 0f, bool wrap = false)
        {
            if (amp == 0f || duration <= 0f)
                return;

            var s0 = (int)(start * SampleRate);
            if (!PrepareSpan(buf.Length, s0, Samples(duration + release), wrap, out var idx, out var n))
                return;

            var len = buf.Length;
            var holdEnd = Samples(duration);
            var attackSamples = Math.Max(1, (int)(attack * SampleRate));
            var releaseSamples = Math.Max(1, (int)(release * SampleRate));
            var phaseA = 0.0;
            var phaseB = 0.37;
            var vibPhase = 0.0;
            for (var j = 0; j < n; j++)
            {
                float e;
                if (j < attackSamples)
                    e = (float)j / attackSamples;
                else if (j < holdEnd)
                    e = 1f;
                else
                    e = Math.Max(0f, 1f - (float)(j - holdEnd) / releaseSamples);

                // Yumuşak eğri (doğrusal zarftan daha müzikal).
                e *= e * (3f - 2f * e);

                var f = frequency;
                if (vibratoDepth > 0f)
                {
                    vibPhase += vibratoHz * Dt;
                    f *= 1f + vibratoDepth * Sin(vibPhase);
                }

                phaseA += f * Dt;
                float osc;
                if (waveform == 1)
                {
                    osc = Saw(phaseA);
                    if (detune != 0f)
                    {
                        phaseB += f * (1f + detune) * Dt;
                        osc = (osc + Saw(phaseB)) * 0.5f;
                    }
                }
                else if (waveform == 2)
                {
                    // Yumuşak kare (tek harmonikler) — alarm ve bip sesleri.
                    osc = Sin(phaseA) + Sin(phaseA * 3.0) * 0.28f + Sin(phaseA * 5.0) * 0.12f;
                }
                else
                {
                    osc = Sin(phaseA);
                    if (detune != 0f)
                    {
                        phaseB += f * (1f + detune) * Dt;
                        osc = (osc + Sin(phaseB)) * 0.5f;
                    }
                }

                buf[idx] += osc * e * amp;
                if (++idx == len)
                    idx = 0;
            }
        }

        /// <summary>Metalik çınlama: uyumsuz kısmi tonların toplamı.</summary>
        public static void Ring(float[] buf, float start, float amp, float tau, bool wrap, params float[] partialsHz)
        {
            if (partialsHz == null)
                return;

            for (var p = 0; p < partialsHz.Length; p++)
            {
                var partialAmp = amp / (1f + p * 0.7f);
                var partialTau = tau / (1f + p * 0.45f);
                Tone(buf, start, partialsHz[p], partialsHz[p], 0f, 0.0005f, partialTau, partialAmp, 0, wrap);
            }
        }

        /// <summary>Metal çarpma: tık + çınlama + gövde.</summary>
        public static void Clack(float[] buf, ref SynthRng rng, float start, float amp, float ringHz, bool wrap = false)
        {
            Click(buf, ref rng, start, amp, 2200f, 0.0018f, wrap);
            Ring(buf, start, amp * 0.35f, 0.022f, wrap, ringHz, ringHz * 1.47f, ringHz * 2.09f);
            NoiseBurst(buf, ref rng, start, 0.0008f, 0.012f, amp * 0.35f, FilterKind.Lowpass, 900f, 0.7f, wrap);
        }

        /// <summary>Ses hızını aşan merminin N-dalgası (keskin "çat").</summary>
        public static void NWave(float[] buf, float start, float amp, float widthSeconds = 0.0004f)
        {
            var s0 = (int)(start * SampleRate);
            var half = Math.Max(2, (int)(widthSeconds * SampleRate * 0.5f));
            for (var j = 0; j < half * 2 && s0 + j < buf.Length; j++)
            {
                var u = (float)j / (half * 2);
                buf[s0 + j] += amp * (1f - 2f * u);
            }
        }

        /// <summary>
        /// Kuru (dry) kaynağın gecikmeli, alçak geçirilmiş ve hafifçe yayılmış kopyasını tampona ekler
        /// (vadi yankısı). Kaynak ayrı bir dizi olduğu için yankılar birbirini beslemez.
        /// </summary>
        public static void Echo(float[] buf, float[] dry, float delay, float amp, float lowpassHz)
        {
            var d = (int)(delay * SampleRate);
            if (dry == null || d <= 0 || d >= buf.Length)
                return;

            var lp1 = new OnePole(lowpassHz);
            var lp2 = new OnePole(lowpassHz * 1.3f);
            var smear = Math.Max(1, (int)(0.004f * SampleRate));
            var n = Math.Min(dry.Length, buf.Length - d);
            for (var i = 0; i < n; i++)
            {
                var x = dry[i];
                if (i >= smear)
                    x = (x + dry[i - smear] * 0.6f) * 0.625f;
                buf[i + d] += lp2.Lowpass(lp1.Lowpass(x)) * amp;
            }
        }

        /// <summary>Döngü tamponunu dairesel filtreler (iki geçiş: ısınma + yazma) — döngü noktasında kesinti olmaz.</summary>
        public static void FilterCircular(float[] buf, FilterKind kind, float frequency, float q = 0.707f)
        {
            var f = Biquad.Create(kind, frequency, q);
            for (var i = Math.Max(0, buf.Length - CircularWarmupSamples); i < buf.Length; i++)
                f.Process(buf[i]);
            for (var i = 0; i < buf.Length; i++)
                buf[i] = f.Process(buf[i]);
        }

        /// <summary>Tek seferlik sesler için doğrusal filtre.</summary>
        public static void Filter(float[] buf, FilterKind kind, float frequency, float q = 0.707f)
        {
            var f = Biquad.Create(kind, frequency, q);
            for (var i = 0; i < buf.Length; i++)
                buf[i] = f.Process(buf[i]);
        }

        public static float[] WhiteNoise(int length, ref SynthRng rng)
        {
            var b = new float[length];
            for (var i = 0; i < length; i++)
                b[i] = rng.Bipolar();
            return b;
        }

        public static void Mix(float[] dst, float[] src, float gain)
        {
            var n = Math.Min(dst.Length, src.Length);
            for (var i = 0; i < n; i++)
                dst[i] += src[i] * gain;
        }

        /// <summary>Yumuşak doyum (tanh yaklaşımı).</summary>
        public static void SoftClip(float[] buf, float drive)
        {
            if (drive <= 0f)
                return;

            var norm = 1f / Saturate(drive);
            for (var i = 0; i < buf.Length; i++)
                buf[i] = Saturate(buf[i] * drive) * norm;
        }

        public static float Saturate(float x)
        {
            if (x > 3f)
                return 1f;
            if (x < -3f)
                return -1f;
            var x2 = x * x;
            return x * (27f + x2) / (27f + 9f * x2);
        }

        public static void Normalize(float[] buf, float peak)
        {
            var max = 0f;
            for (var i = 0; i < buf.Length; i++)
            {
                var a = Math.Abs(buf[i]);
                if (a > max)
                    max = a;
            }

            if (max < 1e-6f)
                return;

            var g = peak / max;
            for (var i = 0; i < buf.Length; i++)
                buf[i] *= g;
        }

        /// <summary>Tıklamayı önlemek için başa/sona kısa geçiş.</summary>
        public static void FadeEdges(float[] buf, float fadeIn, float fadeOut)
        {
            var nIn = Math.Min(buf.Length, (int)(fadeIn * SampleRate));
            for (var i = 0; i < nIn; i++)
                buf[i] *= (float)i / nIn;

            var nOut = Math.Min(buf.Length, (int)(fadeOut * SampleRate));
            for (var i = 0; i < nOut; i++)
                buf[buf.Length - 1 - i] *= (float)i / nOut;
        }

        /// <summary>DC kaymasını giderir (çok düşük kesimli yüksek geçiren).</summary>
        public static void RemoveDc(float[] buf, bool circular)
        {
            var hp = new OnePole(18f);
            if (circular)
            {
                for (var i = Math.Max(0, buf.Length - CircularWarmupSamples); i < buf.Length; i++)
                    hp.Highpass(buf[i]);
            }

            for (var i = 0; i < buf.Length; i++)
                buf[i] = hp.Highpass(buf[i]);
        }
    }
}
