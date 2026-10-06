using System;
using static Project.Infrastructure.Audio.SynthDsp;

namespace Project.Infrastructure.Audio.Ambience
{
    /// <summary>
    /// Ortam yatakları için prosedürel yedek sentez (Resources/Audio/Ambience/&lt;biyom&gt;/ altında klip yoksa kullanılır).
    /// Saf C#, iş parçacığı güvenli, sabit tohum. Döngüler 44.1 kHz mono ve kesintisizdir.
    /// </summary>
    public static class AmbienceSynth
    {
        public const float LoopSeconds = 10f;
        public const float WavesLoopSeconds = 12f;

        public static bool IsLoopLength(AmbienceLoop l) => true;

        public static float[] RenderLoop(AmbienceLoop loop)
        {
            var rng = new SynthRng(0xA11B1E00u ^ ((uint)loop * 2654435761u));
            switch (loop)
            {
                case AmbienceLoop.WindGust: return WindGust(ref rng, 0.55f, 380f, 950f);
                case AmbienceLoop.SnowWind: return WindGust(ref rng, 0.75f, 700f, 1900f);
                case AmbienceLoop.ForestRustle: return ForestRustle(ref rng);
                case AmbienceLoop.Insects: return Insects(ref rng);
                case AmbienceLoop.RainOutdoor: return Rain(ref rng, false);
                case AmbienceLoop.RainRoof: return Rain(ref rng, true);
                case AmbienceLoop.Waves: return Waves(ref rng);
                case AmbienceLoop.WindStrong: return WindGust(ref rng, 1f, 260f, 620f);
                case AmbienceLoop.Grasshopper: return Grasshopper(ref rng);
                case AmbienceLoop.RadioHiss: return RadioHiss(ref rng);
                default: return Buffer(1f);
            }
        }

        public static float[] RenderOneShot(AmbienceOneShot kind, int variant)
        {
            var rng = new SynthRng(0xA5D07000u ^ ((uint)kind * 2654435761u) ^ ((uint)variant * 40503u));
            float[] b;
            switch (kind)
            {
                case AmbienceOneShot.Bird: b = Bird(ref rng, variant); break;
                case AmbienceOneShot.Owl: b = Owl(ref rng, variant); break;
                case AmbienceOneShot.DogBark: b = DogBark(ref rng, variant); break;
                case AmbienceOneShot.DogHowl: b = DogHowl(ref rng); break;
                case AmbienceOneShot.Rooster: b = Rooster(ref rng); break;
                case AmbienceOneShot.AxeChop: b = AxeChop(ref rng, variant); break;
                case AmbienceOneShot.SheepBell: b = SheepBell(ref rng, variant); break;
                case AmbienceOneShot.Gull: b = Gull(ref rng, variant); break;
                default: return null;
            }

            Normalize(b, 0.8f);
            FadeEdges(b, 0.003f, 0.05f);
            return b;
        }

        // ------------------------------------------------------------------ döngüler

        private static float[] Finish(float[] b, float peak)
        {
            RemoveDc(b, true);
            Normalize(b, peak);
            return b;
        }

        /// <summary>Frekansı döngüde tam sayı çevrim olacak şekilde yuvarlar (kesintisiz faz).</summary>
        private static double Cyclic(float hz, float seconds) => Math.Round(hz * seconds) / seconds;

        private static float[] WindGust(ref SynthRng rng, float intensity, float lowHz, float whistleHz)
        {
            var b = Buffer(LoopSeconds);
            var n = b.Length;
            var low = WhiteNoise(n, ref rng);
            FilterCircular(low, FilterKind.Lowpass, lowHz, 0.6f);
            var mid = WhiteNoise(n, ref rng);
            FilterCircular(mid, FilterKind.Bandpass, whistleHz, 5f);
            var lg = BandwidthGain(FilterKind.Lowpass, lowHz, 0.6f);
            var mg = BandwidthGain(FilterKind.Bandpass, whistleHz, 5f);
            var p1 = rng.Unit();
            var p2 = rng.Unit();
            var p3 = rng.Unit();
            for (var i = 0; i < n; i++)
            {
                var u = (double)i / n;
                // Esintiler: yavaş (1-2 tepe) + orta (5) dalgalanma; kare alma tepeleri keskinleştirir.
                var g = 0.35f + 0.35f * Sin(u * 2.0 + p1) + 0.2f * Sin(u * 5.0 + p2) + 0.1f * Sin(u * 9.0 + p3);
                g = Math.Max(0.05f, g);
                b[i] = low[i] * lg * 0.6f * g * intensity + mid[i] * mg * 0.22f * g * g * g * intensity;
            }

            return Finish(b, 0.7f);
        }

        private static float[] Grasshopper(ref SynthRng rng)
        {
            var b = Buffer(LoopSeconds);
            var n = b.Length;
            var carriers = new[] { 6200f, 7100f };
            var rates = new[] { 7f, 9f };
            for (var c = 0; c < carriers.Length; c++)
            {
                var f = Cyclic(carriers[c], LoopSeconds);
                var rate = Cyclic(rates[c], LoopSeconds);
                var ph = rng.Unit();
                for (var i = 0; i < n; i++)
                {
                    var t = (double)i / SampleRate;
                    var g = (t * rate + ph) % 1.0;
                    var env = g < 0.5 ? Sin(g * 1.0) : 0.0;
                    env *= env;
                    b[i] += Sin(t * f) * (float)env * (c == 0 ? 0.5f : 0.3f);
                }
            }

            return Finish(b, 0.35f);
        }

        private static float[] RadioHiss(ref SynthRng rng)
        {
            var b = Buffer(LoopSeconds);
            var n = b.Length;
            var hiss = WhiteNoise(n, ref rng);
            FilterCircular(hiss, FilterKind.Bandpass, 2400f, 1.2f);
            var g = BandwidthGain(FilterKind.Bandpass, 2400f, 1.2f);
            var p1 = rng.Unit();
            var p2 = rng.Unit();
            for (var i = 0; i < n; i++)
            {
                var u = (double)i / n;
                // Fısıltı: yavaş yükselip alçalan zarf, ara ara kısa kesilmeler.
                var m = Math.Max(0.05f, 0.4f + 0.35f * Sin(u * 3.0 + p1) + 0.25f * Sin(u * 11.0 + p2));
                b[i] = hiss[i] * g * m;
            }

            return Finish(b, 0.3f);
        }

        private static float[] ForestRustle(ref SynthRng rng)
        {
            var b = Buffer(LoopSeconds);
            var n = b.Length;
            var leaves = WhiteNoise(n, ref rng);
            FilterCircular(leaves, FilterKind.Bandpass, 3600f, 0.8f);
            var soft = WhiteNoise(n, ref rng);
            FilterCircular(soft, FilterKind.Bandpass, 1500f, 0.6f);
            var lg = BandwidthGain(FilterKind.Bandpass, 3600f, 0.8f);
            var sg = BandwidthGain(FilterKind.Bandpass, 1500f, 0.6f);
            var p = rng.Unit();
            for (var i = 0; i < n; i++)
            {
                var u = (double)i / n;
                var m = Math.Max(0.1f, 0.5f + 0.3f * Sin(u * 3.0 + p) + 0.2f * Sin(u * 8.0 + p * 2f));
                var flutter = 0.7f + 0.3f * Sin(u * 61.0 + p * 4f);
                b[i] = leaves[i] * lg * 0.5f * m * flutter + soft[i] * sg * 0.25f * m;
            }

            return Finish(b, 0.5f);
        }

        private static float[] Insects(ref SynthRng rng)
        {
            var b = Buffer(LoopSeconds);
            var n = b.Length;
            // Üç ayrı cırcır böceği: farklı taşıyıcı, ritim ve faz. Tüm frekanslar döngüde tam sayı çevrim.
            var carriers = new[] { 4300f, 5100f, 3700f };
            var chirpRate = new[] { 2.0f, 2.4f, 1.6f }; // cırıltı grubu / sn
            var pulses = new[] { 3, 4, 3 };
            for (var c = 0; c < carriers.Length; c++)
            {
                var f = Cyclic(carriers[c], LoopSeconds);
                var groupHz = Cyclic(chirpRate[c], LoopSeconds);
                var phaseOff = rng.Unit();
                var amp = 0.5f / (1 + c * 0.5f);
                for (var i = 0; i < n; i++)
                {
                    var t = (double)i / SampleRate;
                    var g = (t * groupHz + phaseOff) % 1.0;
                    // Grup içinde kısa darbeler; 0.45'lik bölümde aktif, geri kalan sessiz.
                    float env = 0f;
                    if (g < 0.45)
                    {
                        var x = g / 0.45 * pulses[c];
                        var frac = x - Math.Floor(x);
                        var pe = Sin(frac * 0.5);
                        env = (float)(pe * pe);
                        // Grubun başında/sonunda yumuşat.
                        var edge = Sin(g / 0.45 * 0.5);
                        env *= (float)(edge * Math.Sqrt(Math.Max(0.0, 1.0 - g / 0.45)) * 1.3);
                    }

                    b[i] += Sin(t * f) * env * amp;
                }
            }

            // Hafif gece havası: çok alçak seviyeli bant gürültüsü.
            var air = WhiteNoise(n, ref rng);
            FilterCircular(air, FilterKind.Bandpass, 2200f, 0.5f);
            Mix(b, air, 0.02f * BandwidthGain(FilterKind.Bandpass, 2200f, 0.5f));
            return Finish(b, 0.45f);
        }

        private static float[] Rain(ref SynthRng rng, bool roof)
        {
            var b = Buffer(LoopSeconds);
            var n = b.Length;
            var hiss = WhiteNoise(n, ref rng);
            FilterCircular(hiss, FilterKind.Highpass, roof ? 1800f : 1200f, 0.7f);
            FilterCircular(hiss, FilterKind.Lowpass, roof ? 6000f : 9000f, 0.7f);
            Mix(b, hiss, roof ? 0.35f : 0.5f);
            var bed = WhiteNoise(n, ref rng);
            FilterCircular(bed, FilterKind.Bandpass, roof ? 2600f : 700f, 0.5f);
            Mix(b, bed, (roof ? 0.45f : 0.35f) * BandwidthGain(FilterKind.Bandpass, roof ? 2600f : 700f, 0.5f));

            // Damla darbeleri: çatıda sık, tınlayan (metal/kiremit); dışarıda yumuşak (yaprak/toprak).
            var drops = roof ? 520 : 260;
            for (var d = 0; d < drops; d++)
            {
                var t = rng.Range(0f, LoopSeconds);
                var amp = rng.Range(0.05f, roof ? 0.28f : 0.16f);
                if (roof)
                {
                    Clack(b, ref rng, t, amp, rng.Range(900f, 2200f), true);
                }
                else
                {
                    NoiseBurst(b, ref rng, t, 0.0003f, rng.Range(0.004f, 0.01f), amp, FilterKind.Bandpass,
                        rng.Range(1800f, 4500f), 0.8f, true);
                }
            }

            if (roof)
            {
                // İç mekân: boğuk gürleyen çatı gövdesi.
                var boom = WhiteNoise(n, ref rng);
                FilterCircular(boom, FilterKind.Lowpass, 220f, 0.7f);
                Mix(b, boom, 0.5f * BandwidthGain(FilterKind.Lowpass, 220f, 0.7f));
            }

            return Finish(b, roof ? 0.6f : 0.55f);
        }

        private static float[] Waves(ref SynthRng rng)
        {
            var b = Buffer(WavesLoopSeconds);
            var n = b.Length;
            var surf = WhiteNoise(n, ref rng);
            FilterCircular(surf, FilterKind.Lowpass, 900f, 0.6f);
            var foam = WhiteNoise(n, ref rng);
            FilterCircular(foam, FilterKind.Bandpass, 3200f, 0.6f);
            var sg = BandwidthGain(FilterKind.Lowpass, 900f, 0.6f);
            var fg = BandwidthGain(FilterKind.Bandpass, 3200f, 0.6f);
            var p = rng.Unit();
            for (var i = 0; i < n; i++)
            {
                var u = (double)i / n;
                // Dalga: yavaş kabarma + geç gelen köpük (köpük kabarmanın tepesinin biraz ilerisinde).
                var swell = Math.Max(0f, 0.5f + 0.5f * Sin(u * 2.0 + p));
                var crest = Math.Max(0f, Sin(u * 2.0 + p + 0.12));
                b[i] = surf[i] * sg * 0.5f * (0.2f + swell * swell) + foam[i] * fg * 0.3f * crest * crest * crest;
            }

            return Finish(b, 0.55f);
        }

        // ------------------------------------------------------------------ tek seferlik sesler

        private static void Chirp(float[] b, float start, float duration, float f0, float f1, float amp, float trillHz, float trillDepth)
        {
            var s0 = (int)(start * SampleRate);
            var len = Samples(duration);
            var phase = 0.0;
            var trill = 0.0;
            var step = (float)Math.Pow(f1 / f0, 1.0 / len);
            var f = f0;
            for (var j = 0; j < len; j++)
            {
                var idx = s0 + j;
                if (idx < 0 || idx >= b.Length)
                    break;
                var u = (float)j / len;
                var sh = Sin(u * 0.5);
                f *= step;
                var fi = f;
                if (trillDepth > 0f)
                {
                    trill += trillHz * Dt;
                    fi *= 1f + trillDepth * Sin(trill);
                }

                phase += fi * Dt;
                b[idx] += (Sin(phase) + Sin(phase * 2.0) * 0.12f) * sh * sh * amp;
            }
        }

        private static float[] Bird(ref SynthRng rng, int variant)
        {
            var b = Buffer(1.4f);
            var t = 0.02f;
            switch (variant % 3)
            {
                case 0:
                {
                    var count = rng.RangeInt(4, 7);
                    var f = rng.Range(3800f, 5200f);
                    for (var k = 0; k < count; k++)
                    {
                        var d = rng.Range(0.05f, 0.08f);
                        Chirp(b, t, d, f, f * 0.72f, rng.Range(0.7f, 1f), 0f, 0f);
                        t += d + rng.Range(0.06f, 0.1f);
                    }

                    break;
                }
                case 1:
                {
                    // Çam ormanı ardıç kuşu: tril + kısa nota
                    var f = rng.Range(2800f, 3600f);
                    Chirp(b, t, rng.Range(0.4f, 0.6f), f, f * 1.15f, 0.8f, rng.Range(24f, 34f), 0.06f);
                    Chirp(b, t + 0.7f, 0.12f, f * 1.3f, f * 0.9f, 0.7f, 0f, 0f);
                    break;
                }
                default:
                {
                    var f = rng.Range(2200f, 3000f);
                    Chirp(b, t, 0.16f, f, f * 1.18f, 1f, 5f, 0.01f);
                    Chirp(b, t + 0.2f, 0.22f, f * 1.12f, f * 0.84f, 0.85f, 5f, 0.01f);
                    Chirp(b, t + 0.55f, 0.16f, f, f * 1.18f, 0.7f, 5f, 0.01f);
                    Chirp(b, t + 0.75f, 0.22f, f * 1.12f, f * 0.84f, 0.6f, 5f, 0.01f);
                    break;
                }
            }

            return b;
        }

        private static float[] Owl(ref SynthRng rng, int variant)
        {
            var b = Buffer(2.2f);
            var f = 330f + variant * 25f;
            Note(b, 0.05f, 0.22f, f, 0.05f, 0.08f, 0.7f, 0, 0f, 0f, 0f);
            Note(b, 0.55f, 0.18f, f, 0.05f, 0.08f, 0.6f, 0, 0f, 0f, 0f);
            Note(b, 1.0f, 0.5f, f * 0.95f, 0.08f, 0.2f, 0.85f, 0, 0f, 5f, 0.02f);
            Filter(b, FilterKind.Lowpass, 1100f, 0.7f);
            var dry = (float[])b.Clone();
            Echo(b, dry, 0.28f, 0.22f, 800f);
            return b;
        }

        private static void Bark(float[] b, ref SynthRng rng, float t, float pitch, float amp)
        {
            var d = rng.Range(0.14f, 0.2f);
            Tone(b, t, 360f * pitch, 210f * pitch, 0.07f, 0.004f, d * 0.4f, amp * 0.7f, 1);
            NoiseBurst(b, ref rng, t, 0.003f, d * 0.4f, amp * 0.8f, FilterKind.Bandpass, 900f * pitch, 1.3f);
            NoiseBurst(b, ref rng, t, 0.001f, 0.012f, amp * 0.5f, FilterKind.Bandpass, 2200f, 1f);
        }

        private static float[] DogBark(ref SynthRng rng, int variant)
        {
            var b = Buffer(2.2f);
            var pitch = rng.Range(0.8f, 1.25f);
            var count = 2 + variant % 3 + (rng.Chance(0.4f) ? 1 : 0);
            var t = 0.03f;
            for (var k = 0; k < count; k++)
            {
                Bark(b, ref rng, t, pitch * rng.Range(0.96f, 1.04f), rng.Range(0.7f, 1f));
                t += rng.Range(0.26f, 0.4f);
            }

            Filter(b, FilterKind.Lowpass, 3600f, 0.7f);
            Echo(b, (float[])b.Clone(), 0.32f, 0.3f, 1500f);
            return b;
        }

        private static float[] DogHowl(ref SynthRng rng)
        {
            var b = Buffer(4.2f);
            // Uzayan "uuu": vibratolu, yükselip alçalan ton + nefesli gürültü.
            Note(b, 0.1f, 2.6f, 420f, 0.5f, 0.8f, 0.7f, 0, 0.003f, 5.2f, 0.025f);
            Note(b, 0.1f, 2.6f, 840f, 0.6f, 0.8f, 0.2f, 0, 0f, 5.2f, 0.02f);
            NoiseSwell(b, ref rng, 0.1f, 2.8f, 0.12f, FilterKind.Bandpass, 900f, 700f, 0.8f);
            Filter(b, FilterKind.Lowpass, 2400f, 0.7f);
            Echo(b, (float[])b.Clone(), 0.45f, 0.3f, 1200f);
            return b;
        }

        private static float[] Rooster(ref SynthRng rng)
        {
            var b = Buffer(2.8f);
            // "ü-ürü-üüüü": üç hece, yükselen çırpınan harmonikli ton.
            Note(b, 0.05f, 0.28f, 520f, 0.02f, 0.04f, 0.8f, 1, 0.004f, 12f, 0.04f);
            Note(b, 0.45f, 0.34f, 640f, 0.02f, 0.05f, 0.85f, 1, 0.004f, 12f, 0.05f);
            Note(b, 0.95f, 0.9f, 700f, 0.03f, 0.25f, 0.9f, 1, 0.004f, 10f, 0.06f);
            Filter(b, FilterKind.Bandpass, 1400f, 0.35f);
            Echo(b, (float[])b.Clone(), 0.38f, 0.25f, 1400f);
            return b;
        }

        private static float[] AxeChop(ref SynthRng rng, int variant)
        {
            var b = Buffer(3.2f);
            var count = 3 + variant;
            var t = 0.05f;
            for (var k = 0; k < count; k++)
            {
                NoiseBurst(b, ref rng, t, 0.0006f, 0.02f, 0.8f, FilterKind.Bandpass, 1200f, 0.9f);
                Tone(b, t, 220f, 130f, 0.03f, 0.001f, 0.06f, 0.9f);
                t += rng.Range(0.6f, 0.9f);
            }

            Filter(b, FilterKind.Lowpass, 2800f, 0.7f);
            Echo(b, (float[])b.Clone(), 0.35f, 0.35f, 1300f);
            return b;
        }

        private static float[] SheepBell(ref SynthRng rng, int variant)
        {
            var b = Buffer(3.4f);
            var count = 6 + variant * 2;
            var t = 0.05f;
            var f = rng.Range(780f, 1100f);
            for (var k = 0; k < count; k++)
            {
                Ring(b, t, rng.Range(0.3f, 0.6f), 0.35f, false, f, f * 2.76f, f * 5.4f);
                t += rng.Range(0.18f, 0.5f);
            }

            return b;
        }

        private static float[] Gull(ref SynthRng rng, int variant)
        {
            var b = Buffer(2.0f);
            var count = 3 + variant;
            var t = 0.05f;
            for (var k = 0; k < count; k++)
            {
                var f = rng.Range(1500f, 2000f);
                Chirp(b, t, 0.3f, f * 1.2f, f * 0.8f, 0.9f, 22f, 0.07f);
                t += rng.Range(0.34f, 0.5f);
            }

            return b;
        }
    }
}
