using System;
using System.Collections.Generic;
using Project.Infrastructure.Audio.HdrMix;
using UnityEngine;

namespace Project.Infrastructure.Audio
{
    public enum UiSfx { Hover, Press, Tab, Back, Error, MatchFound, CountdownTick, CountdownGo }

    /// <summary>
    /// Saf (Unity'siz) prosedürel arayüz sesleri. Mono 22.05 kHz; her ses -18 LUFS (yaklaşık) hedefine ayarlanır,
    /// tepe en çok -3 dBFS. Kısa tıklamalar tepe sınırına takılır (daha sessiz ama tutarlı algı).
    /// </summary>
    public static class UiSfxSynth
    {
        public const int SampleRate = 22050;
        public const float TargetLufs = -18f;
        public const float PeakCeilingDb = -3f;

        /// <summary>Atak (lineer) x üstel sönüm zarfı. t, attack, decayTau saniye. Çıktı 0..1.</summary>
        public static float Envelope(float t, float attack, float decayTau)
        {
            if (t < 0f) return 0f;
            var a = attack <= 1e-6f ? 1f : Math.Min(1f, t / attack);
            var d = decayTau <= 1e-6f ? 0f : (float)Math.Exp(-Math.Max(0f, t - attack) / decayTau);
            return a * d;
        }

        /// <summary>Hedef LUFS'a ulaşmak için gerekli kazanç; tepe tavanı (dBFS) aşılmaz. Sessiz girdide 1.</summary>
        public static float NormalizeGain(float measuredLufs, float peakLinear, float targetLufs, float ceilingDb)
        {
            if (measuredLufs <= LoudnessMath.SilenceDb + 1f || peakLinear <= 1e-6f) return 1f;
            var byLoud = LoudnessMath.FromDb(targetLufs - measuredLufs);
            var byPeak = LoudnessMath.FromDb(ceilingDb) / peakLinear;
            return Math.Min(byLoud, byPeak);
        }

        public static float[] Render(UiSfx kind)
        {
            float[] s;
            switch (kind)
            {
                case UiSfx.Hover: s = Hover(); break;
                case UiSfx.Press: s = Press(); break;
                case UiSfx.Tab: s = Whoosh(); break;
                case UiSfx.Back: s = Back(); break;
                case UiSfx.Error: s = Buzz(); break;
                case UiSfx.MatchFound: s = Sting(); break;
                case UiSfx.CountdownTick: s = Beep(0.12f, 880f, 0.05f); break;
                default: s = Beep(0.4f, 1320f, 0.14f); break;
            }
            Normalize(s);
            return s;
        }

        private static void Normalize(float[] s)
        {
            var g = NormalizeGain(LoudnessMath.ApproxLufs(s, SampleRate, s.Length / (float)SampleRate),
                LoudnessMath.PeakLinear(s), TargetLufs, PeakCeilingDb);
            for (var i = 0; i < s.Length; i++) s[i] *= g;
        }

        private static float[] Buf(float sec) => new float[(int)(sec * SampleRate)];
        private static float T(int i) => i / (float)SampleRate;
        private static float Sin(float hz, float t) => (float)Math.Sin(2.0 * Math.PI * hz * t);

        private static uint _rng = 2463534242u;
        private static float Noise()
        {
            _rng ^= _rng << 13; _rng ^= _rng >> 17; _rng ^= _rng << 5;
            return (_rng & 0xFFFFFF) / (float)0x800000 - 1f;
        }

        /// <summary>Yumuşak ~2 ms tık: kısa süzülmüş gürültü + 2.6 kHz sinüs.</summary>
        private static float[] Hover()
        {
            var s = Buf(0.012f);
            float lp = 0;
            for (var i = 0; i < s.Length; i++)
            {
                var t = T(i);
                lp += 0.35f * (Noise() - lp);
                s[i] = (0.6f * lp + 0.4f * Sin(2600f, t)) * Envelope(t, 0.0003f, 0.0022f);
            }
            return s;
        }

        /// <summary>Daha derin tok: 170 -> 85 Hz sinüs süpürme + minik tık.</summary>
        private static float[] Press()
        {
            var s = Buf(0.14f);
            double ph = 0;
            for (var i = 0; i < s.Length; i++)
            {
                var t = T(i);
                var f = 85f + 85f * (float)Math.Exp(-t / 0.02f);
                ph += 2.0 * Math.PI * f / SampleRate;
                s[i] = (float)Math.Sin(ph) * Envelope(t, 0.0008f, 0.04f) + 0.25f * Noise() * Envelope(t, 0f, 0.003f);
            }
            return s;
        }

        /// <summary>Sekme geçişi: bant geçiren süpürmeli gürültü, sinüs zarf.</summary>
        private static float[] Whoosh()
        {
            const float dur = 0.2f;
            var s = Buf(dur);
            float lp = 0, bp = 0;
            for (var i = 0; i < s.Length; i++)
            {
                var t = T(i);
                var fc = 500f + 2500f * (t / dur);
                var f = 2f * (float)Math.Sin(Math.PI * fc / SampleRate);
                var x = Noise();
                lp += f * bp; var hp = x - lp - 0.45f * bp; bp += f * hp;
                s[i] = bp * (float)Math.Sin(Math.PI * t / dur);
            }
            return s;
        }

        /// <summary>Geri: iki kısa inen nota.</summary>
        private static float[] Back()
        {
            var s = Buf(0.16f);
            for (var i = 0; i < s.Length; i++)
            {
                var t = T(i);
                var a = Sin(540f, t) * Envelope(t, 0.002f, 0.025f);
                var b = t >= 0.06f ? Sin(380f, t) * Envelope(t - 0.06f, 0.002f, 0.03f) : 0f;
                s[i] = a * 0.8f + b;
            }
            return s;
        }

        /// <summary>Hata: iki darbeli alçak kare dalga vızıltısı.</summary>
        private static float[] Buzz()
        {
            var s = Buf(0.24f);
            float lp = 0;
            for (var i = 0; i < s.Length; i++)
            {
                var t = T(i);
                var sq = Sin(120f, t) >= 0f ? 1f : -1f;
                lp += 0.25f * (sq - lp);
                var pulse = (t < 0.1f || (t >= 0.13f && t < 0.23f)) ? 1f : 0f;
                s[i] = lp * pulse * Envelope(t % 0.13f, 0.003f, 0.2f);
            }
            return s;
        }

        /// <summary>Maç bulundu: yükselen üçlü (C5-E5-G5) çan benzeri, sonda uzun G6.</summary>
        private static float[] Sting()
        {
            var s = Buf(0.9f);
            var notes = new[] { 523.25f, 659.25f, 783.99f, 1567.98f };
            var starts = new[] { 0f, 0.09f, 0.18f, 0.18f };
            var amps = new[] { 1f, 0.9f, 0.9f, 0.35f };
            for (var k = 0; k < notes.Length; k++)
                for (var i = (int)(starts[k] * SampleRate); i < s.Length; i++)
                {
                    var t = T(i) - starts[k];
                    s[i] += amps[k] * (Sin(notes[k], t) + 0.3f * Sin(notes[k] * 2f, t)) * Envelope(t, 0.004f, 0.22f);
                }
            return s;
        }

        private static float[] Beep(float dur, float hz, float tau)
        {
            var s = Buf(dur);
            for (var i = 0; i < s.Length; i++)
            {
                var t = T(i);
                s[i] = (Sin(hz, t) + 0.2f * Sin(hz * 2f, t)) * Envelope(t, 0.003f, tau);
            }
            return s;
        }
    }

    /// <summary>
    /// Arayüz sesi girişi: <c>UiSounds.Play(UiSfx.Press)</c>. Klipler ilk kullanımda (küçük, anlık) üretilip önbelleklenir.
    /// Ses = Volume x AudioListener (ana ses). Hover çok hızlı tekrarlanmaz (30 ms).
    /// </summary>
    public static class UiSounds
    {
        /// <summary>Arayüz ses düzeyi 0..1. GameSession SfxVolume'a bağlar.</summary>
        public static Func<float> VolumeProvider = () => 0.8f;

        private static readonly Dictionary<UiSfx, AudioClip> Clips = new Dictionary<UiSfx, AudioClip>();
        private static readonly float[] LastPlay = new float[Enum.GetValues(typeof(UiSfx)).Length];
        private static AudioSource[] _srcs;
        private static int _next;

        public static void Play(UiSfx sfx)
        {
            try
            {
                if (!UnityEngine.Application.isPlaying) return;
                var now = Time.unscaledTime;
                var minGap = sfx == UiSfx.Hover ? 0.03f : 0.01f;
                if (now - LastPlay[(int)sfx] < minGap) return;
                LastPlay[(int)sfx] = now;
                float v;
                try { v = Mathf.Clamp01(VolumeProvider()); } catch (Exception) { v = 0.8f; }
                if (v <= 0f) return;
                EnsureHost();
                var src = _srcs[_next]; _next = (_next + 1) % _srcs.Length;
                src.clip = GetClip(sfx);
                src.volume = v;
                src.Play();
            }
            catch (Exception e) { Debug.LogWarning("[UiSounds] " + e.Message); }
        }

        private static AudioClip GetClip(UiSfx sfx)
        {
            if (Clips.TryGetValue(sfx, out var c) && c != null) return c;
            var data = UiSfxSynth.Render(sfx);
            c = AudioClip.Create("Ui_" + sfx, data.Length, 1, UiSfxSynth.SampleRate, false);
            c.SetData(data, 0);
            Clips[sfx] = c;
            return c;
        }

        private static void EnsureHost()
        {
            if (_srcs != null && _srcs[0] != null) return;
            var go = new GameObject("[UI Sesleri]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _srcs = new AudioSource[6];
            for (var i = 0; i < _srcs.Length; i++)
            {
                var a = go.AddComponent<AudioSource>();
                a.playOnAwake = false; a.spatialBlend = 0f; a.ignoreListenerPause = true;
                MixerRouting.Route(a, MixChannel.UI);
                _srcs[i] = a;
            }
        }
    }
}
