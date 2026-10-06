using System;

namespace Project.Infrastructure.Audio.Music
{
    /// <summary>İki kanallı render sonucu.</summary>
    public sealed class StereoBuffer
    {
        public int SampleRate;
        public float[] Left;
        public float[] Right;
        public int Frames => Left.Length;

        public float[] Interleave()
        {
            var o = new float[Left.Length * 2];
            for (var i = 0; i < Left.Length; i++) { o[2 * i] = Left[i]; o[2 * i + 1] = Right[i]; }
            return o;
        }
    }

    /// <summary>
    /// Katmanlı sentez: yay benzeri pad (detune'lu PolyBLEP testere + alçak geçiren), ney/boru benzeri melodi
    /// (vibratolu filtreli testere + nefes gürültüsü), davul, rüzgâr, kısa yankı. Unity'siz; iş parçacığında çalışır.
    /// </summary>
    public static class MusicSynth
    {
        public const int SampleRate = 22050;

        /// <summary>Melodi geldiğinde pad'in en çok ne kadar kısılacağı (0.35 = -3.7 dB).</summary>
        public const float PadDuckDepth = 0.35f;

        private sealed class Rng
        {
            private uint _s;
            public Rng(uint seed) { _s = seed == 0 ? 1u : seed; }
            public float Next() // [-1,1]
            {
                _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5;
                return (_s & 0xFFFFFF) / (float)0x800000 - 1f;
            }
        }

        /// <summary>75 sn'lik kusursuz döngü: tonal katmanlar sarmalı yazılır, gürültü çapraz geçişlidir.</summary>
        public static StereoBuffer RenderMenuLoop()
        {
            var len = (int)Math.Round(MusicScore.LoopSeconds * SampleRate);
            var padL = new float[len];
            var padR = new float[len];
            var mixL = new float[len];
            var mixR = new float[len];

            foreach (var c in MusicScore.BuildChords())
            {
                for (var i = 0; i < c.Midi.Length; i++)
                {
                    var pan = (i - (c.Midi.Length - 1) * 0.5f) / c.Midi.Length;
                    AddPad(padL, padR, true, Sec(c.StartBeat), Sec(c.Beats), MusicScore.MidiToHz(c.Midi[i]), 0.07f, pan);
                }
            }

            foreach (var n in MusicScore.BuildMelody())
                AddLead(mixL, mixR, true, Sec(n.StartBeat), Sec(n.Beats), MusicScore.MidiToHz(n.Midi), 0.2f * n.Velocity, 0.15f);

            // Yan zincir: melodi (ve davul vuruşları) pad'i hafifçe kısar; pad melodinin altında nefes alır.
            var env = MusicMixMath.FollowEnvelope(mixL, SampleRate, 0.02f, 0.45f, true);
            for (var i = 0; i < len; i++)
            {
                var g = MusicMixMath.DuckGain(env[i], PadDuckDepth);
                mixL[i] += padL[i] * g;
                mixR[i] += padR[i] * g;
            }

            foreach (var d in MusicScore.BuildDrums())
                AddDrum(mixL, mixR, true, Sec(d.Beat), d.Kind, d.Velocity * 0.9f);

            ApplyReverb(mixL, 0, 0.32f);
            ApplyReverb(mixR, 1, 0.32f);

            var wind = RenderWind(len);
            for (var i = 0; i < len; i++) { mixL[i] += wind.Left[i]; mixR[i] += wind.Right[i]; }

            return Finish(mixL, mixR, MusicMixMath.CeilingLinear);
        }

        public static StereoBuffer RenderSting(StingKind kind)
        {
            var score = MusicScore.BuildSting(kind);
            var len = (int)((score.Seconds + 3f) * SampleRate);
            var l = new float[len];
            var r = new float[len];

            foreach (var n in score.Drone)
                AddPad(l, r, false, n.StartBeat, n.Beats, MusicScore.MidiToHz(n.Midi), 0.16f * n.Velocity, 0f);
            foreach (var c in score.Chords)
                for (var i = 0; i < c.Midi.Length; i++)
                    AddPad(l, r, false, c.StartBeat, c.Beats, MusicScore.MidiToHz(c.Midi[i]), 0.08f, (i - 2) * 0.15f);
            foreach (var n in score.Melody)
            {
                if (kind == StingKind.MatchStart)
                    AddLead(l, r, false, n.StartBeat, n.Beats, MusicScore.MidiToHz(n.Midi), 0.12f * n.Velocity, -0.2f);
                else
                    AddLead(l, r, false, n.StartBeat, n.Beats, MusicScore.MidiToHz(n.Midi), 0.24f * n.Velocity, 0.1f);
            }
            foreach (var d in score.Drums)
                AddDrum(l, r, false, d.Beat, d.Kind, d.Velocity);

            if (score.Riser)
                AddRiser(l, r, score.Seconds - 0.6f);

            ApplyReverb(l, 0, 0.3f);
            ApplyReverb(r, 1, 0.3f);
            return Finish(l, r, MusicMixMath.CeilingLinear);
        }

        // ---- yardımcılar ----

        private static float Sec(float beats) => MusicScore.BeatsToSeconds(beats);

        private static void Put(float[] buf, bool wrap, int idx, float v)
        {
            if (wrap) { idx %= buf.Length; if (idx < 0) idx += buf.Length; }
            else if (idx < 0 || idx >= buf.Length) return;
            buf[idx] += v;
        }

        private static float PolyBlep(float t, float dt)
        {
            if (t < dt) { t /= dt; return t + t - t * t - 1f; }
            if (t > 1f - dt) { t = (t - 1f) / dt; return t * t + t + t + 1f; }
            return 0f;
        }

        private static float Saw(float phase, float dt) => 2f * phase - 1f - PolyBlep(phase, dt);

        private static float Cents(float cents) => (float)Math.Pow(2.0, cents / 1200.0);

        /// <summary>Yay benzeri pad notası. start/dur saniye.</summary>
        private static void AddPad(float[] L, float[] R, bool wrap, float start, float dur, float hz, float gain, float pan)
        {
            const float attack = 1.4f, release = 2.0f;
            var total = dur + release;
            var s0 = (int)(start * SampleRate);
            var n = (int)(total * SampleRate);
            float p0 = 0f, p1 = 0.31f, p2 = 0.67f, lp = 0f;
            var fc = Math.Min(900f, hz * 5f);
            var a = 1f - (float)Math.Exp(-2.0 * Math.PI * fc / SampleRate);
            var gl = gain * (1f - Math.Max(0f, pan) * 0.6f);
            var gr = gain * (1f + Math.Min(0f, pan) * 0.6f);
            for (var i = 0; i < n; i++)
            {
                var t = i / (float)SampleRate;
                var env = t < attack ? (float)Math.Sin(t / attack * Math.PI * 0.5) : 1f;
                if (t > dur) env *= (float)Math.Exp(-(t - dur) * 3.2f);
                var wob = 1f + 0.0018f * (float)Math.Sin(2.0 * Math.PI * 0.27 * t);
                var d0 = hz * Cents(-7f) * wob / SampleRate;
                var d1 = hz * wob / SampleRate;
                var d2 = hz * Cents(7f) * wob / SampleRate;
                p0 = Wrap1(p0 + d0); p1 = Wrap1(p1 + d1); p2 = Wrap1(p2 + d2);
                var x0 = Saw(p0, d0);
                var x1 = Saw(p1, d1);
                var x2 = Saw(p2, d2);
                lp += a * ((x0 + x1 + x2) * 0.33f - lp);
                var tremolo = 0.92f + 0.08f * (float)Math.Sin(2.0 * Math.PI * 4.6 * t);
                var o = lp * env * tremolo;
                Put(L, wrap, s0 + i, o * gl);
                Put(R, wrap, s0 + i, o * gr);
            }
        }

        private static float Wrap1(float p) => p >= 1f ? p - 1f : p;

        /// <summary>Ney/boru benzeri lead: filtreli testere + sine + vibrato + nefes.</summary>
        private static void AddLead(float[] L, float[] R, bool wrap, float start, float dur, float hz, float gain, float pan)
        {
            const float attack = 0.2f, release = 0.9f;
            var total = dur + release;
            var s0 = (int)(start * SampleRate);
            var n = (int)(total * SampleRate);
            var rng = new Rng((uint)(s0 * 2654435761u + 17u));
            float pa = 0f, pb = 0.4f, ps = 0f, lp1 = 0f, lp2 = 0f, nl = 0f, vp = 0f;
            var gl = gain * (1f - pan * 0.5f);
            var gr = gain * (1f + pan * 0.5f);
            for (var i = 0; i < n; i++)
            {
                var t = i / (float)SampleRate;
                var env = t < attack ? t / attack : 1f;
                env *= 0.85f + 0.15f * (float)Math.Sin(2.0 * Math.PI * 0.4 * t);
                if (t > dur) env *= (float)Math.Exp(-(t - dur) * 5f);
                var vibDepth = Math.Min(1f, Math.Max(0f, (t - 0.4f) / 0.8f));
                vp += 5.3f / SampleRate; if (vp >= 1f) vp -= 1f;
                var vib = Cents(vibDepth * 20f * (float)Math.Sin(2.0 * Math.PI * vp));
                var da = hz * Cents(-4f) * vib / SampleRate;
                var db = hz * Cents(4f) * vib / SampleRate;
                var ds = hz * vib / SampleRate;
                pa = Wrap1(pa + da); pb = Wrap1(pb + db); ps = Wrap1(ps + ds);
                var raw = (Saw(pa, da) + Saw(pb, db)) * 0.5f;
                var fc = Math.Min(7000f, hz * (2.4f + 2.5f * Math.Min(1f, t / 0.5f) * env));
                var a = 1f - (float)Math.Exp(-2.0 * Math.PI * fc / SampleRate);
                lp1 += a * (raw - lp1);
                lp2 += a * (lp1 - lp2);
                var sine = (float)Math.Sin(2.0 * Math.PI * ps);
                var breathRaw = rng.Next();
                nl += 0.25f * (breathRaw - nl);
                var o = (lp2 * 0.75f + sine * 0.35f + (breathRaw - nl) * 0.05f * Math.Min(1f, hz / 300f)) * env;
                Put(L, wrap, s0 + i, o * gl);
                Put(R, wrap, s0 + i, o * gr);
            }
        }

        private static void AddDrum(float[] L, float[] R, bool wrap, float start, DrumKind kind, float vel)
        {
            var s0 = (int)(start * SampleRate);
            var rng = new Rng((uint)(s0 + 99));
            var dur = kind == DrumKind.Dum ? 0.8f : kind == DrumKind.Tek ? 0.25f : 0.12f;
            var n = (int)(dur * SampleRate);
            float ph = 0f, nlp = 0f;
            for (var i = 0; i < n; i++)
            {
                var t = i / (float)SampleRate;
                float o;
                var noise = rng.Next();
                if (kind == DrumKind.Dum)
                {
                    var f = 52f + 75f * (float)Math.Exp(-t / 0.035);
                    ph += f / SampleRate;
                    nlp += 0.12f * (noise - nlp);
                    o = (float)Math.Sin(2.0 * Math.PI * ph) * (float)Math.Exp(-t / 0.18) * 0.9f
                        + nlp * (float)Math.Exp(-t / 0.012) * 0.5f
                        + (float)Math.Sin(2.0 * Math.PI * ph * 1.6) * (float)Math.Exp(-t / 0.07) * 0.15f;
                }
                else if (kind == DrumKind.Tek)
                {
                    var f = 190f + 110f * (float)Math.Exp(-t / 0.012);
                    ph += f / SampleRate;
                    nlp += 0.45f * (noise - nlp);
                    o = (float)Math.Sin(2.0 * Math.PI * ph) * (float)Math.Exp(-t / 0.05) * 0.5f
                        + (noise - nlp) * (float)Math.Exp(-t / 0.02) * 0.35f;
                }
                else
                {
                    nlp += 0.7f * (noise - nlp);
                    o = (noise - nlp) * (float)Math.Exp(-t / 0.03) * 0.3f;
                }
                o *= vel * 0.8f;
                Put(L, wrap, s0 + i, o);
                Put(R, wrap, s0 + i, o);
            }
        }

        private static void AddRiser(float[] L, float[] R, float seconds)
        {
            var n = (int)(seconds * SampleRate);
            var rng = new Rng(4242);
            float lp = 0f, lp2 = 0f, ph = 0f;
            for (var i = 0; i < n; i++)
            {
                var u = i / (float)n;
                var fc = 180f + 4200f * u * u;
                var a = 1f - (float)Math.Exp(-2.0 * Math.PI * fc / SampleRate);
                lp += a * (rng.Next() - lp);
                lp2 += a * (lp - lp2);
                ph += 110f * (float)Math.Pow(2.0, u * 2.0) / SampleRate;
                var amp = 0.02f + 0.2f * u * u * u;
                var o = (lp2 * 2.2f + (float)Math.Sin(2.0 * Math.PI * ph) * 0.25f) * amp;
                L[i] += o; R[i] += o * 0.92f;
            }
        }

        private static StereoBuffer RenderWind(int len)
        {
            const int xf = 22050 * 3;
            var res = new StereoBuffer { SampleRate = SampleRate, Left = new float[len], Right = new float[len] };
            for (var ch = 0; ch < 2; ch++)
            {
                var rng = new Rng(7777u + (uint)ch * 31u);
                var tmp = new float[len + xf];
                float lpA = 0f, lpB = 0f;
                var aHi = 1f - (float)Math.Exp(-2.0 * Math.PI * 700.0 / SampleRate);
                var aLo = 1f - (float)Math.Exp(-2.0 * Math.PI * 90.0 / SampleRate);
                for (var i = 0; i < tmp.Length; i++)
                {
                    var x = rng.Next();
                    lpA += aHi * (x - lpA);
                    lpB += aLo * (x - lpB);
                    var u = (i % len) / (float)len;
                    var lfo = 0.55f + 0.3f * (float)Math.Sin(2.0 * Math.PI * (5 * u + ch * 0.13)) + 0.15f * (float)Math.Sin(2.0 * Math.PI * (13 * u + 0.4));
                    tmp[i] = (lpA - lpB) * lfo * 0.55f;
                }
                var dst = ch == 0 ? res.Left : res.Right;
                // Kuyruk ile baş çapraz geçişi: döngü sınırında süreklilik.
                for (var i = 0; i < len; i++)
                {
                    if (i < xf)
                    {
                        var w = i / (float)xf;
                        var g1 = (float)Math.Sin(w * Math.PI * 0.5);
                        var g2 = (float)Math.Cos(w * Math.PI * 0.5);
                        dst[i] = tmp[i] * g1 + tmp[len + i] * g2;
                    }
                    else dst[i] = tmp[i];
                }
            }
            return res;
        }

        private static readonly int[][] CombSets =
        {
            new[] { 2477, 3203, 3911, 4663 },
            new[] { 2549, 3299, 3989, 4721 },
        };

        /// <summary>4 geri beslemeli tarak süzgeç; iki geçiş (ikincisi sürekli durumdan) ile döngüde dikişsiz.</summary>
        private static void ApplyReverb(float[] x, int set, float wetAmount)
        {
            var delays = CombSets[set];
            var bufs = new float[delays.Length][];
            var pos = new int[delays.Length];
            var damp = new float[delays.Length];
            for (var k = 0; k < delays.Length; k++) bufs[k] = new float[delays[k]];
            var wet = new float[x.Length];
            for (var pass = 0; pass < 2; pass++)
            {
                for (var i = 0; i < x.Length; i++)
                {
                    var sum = 0f;
                    for (var k = 0; k < delays.Length; k++)
                    {
                        var y = bufs[k][pos[k]];
                        damp[k] += 0.35f * (y - damp[k]);
                        bufs[k][pos[k]] = x[i] * 0.5f + damp[k] * 0.86f;
                        if (++pos[k] >= delays[k]) pos[k] = 0;
                        sum += y;
                    }
                    if (pass == 1) wet[i] = sum * 0.25f;
                }
            }
            for (var i = 0; i < x.Length; i++) x[i] += wet[i] * wetAmount * 2f;
        }

        private static StereoBuffer Finish(float[] l, float[] r, float targetPeak)
        {
            var peak = 1e-6f;
            for (var i = 0; i < l.Length; i++)
            {
                var a = Math.Abs(l[i]); if (a > peak) peak = a;
                var b = Math.Abs(r[i]); if (b > peak) peak = b;
            }
            var g = targetPeak / peak;
            // Son limiter: yumuşak diz, kesin tavan = targetPeak (-1 dBFS).
            for (var i = 0; i < l.Length; i++)
            {
                l[i] = MusicMixMath.SoftLimit(l[i] * g * 1.08f, targetPeak);
                r[i] = MusicMixMath.SoftLimit(r[i] * g * 1.08f, targetPeak);
            }
            return new StereoBuffer { SampleRate = SampleRate, Left = l, Right = r };
        }
    }
}
