using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Project.Infrastructure.Audio.HdrMix;
using UnityEngine;

namespace Project.Infrastructure.Audio.Music
{
    /// <summary>
    /// Maç başı (20 sn gerilim), zafer ve yenilgi sting'leri. İlk çağrıda (ya da Warmup ile) arka planda üretilir,
    /// bellekte önbelleklenir; sahneler arası kalıcı bir ana nesne çalar.
    /// Bağlantı: GameSession.MatchStarting -> Play(MatchStart); MatchFinished -> Play(IsWinner ? Victory : Defeat).
    /// </summary>
    public static class MusicStings
    {
        private const float Gain = 0.7f;

        /// <summary>Ses düzeyi sağlayıcısı (MusicVolume x MasterVolume); MenuMusicDirector.Ensure atar.</summary>
        public static Func<float> VolumeProvider = () => 0.8f;

        private static readonly Dictionary<StingKind, AudioClip> Clips = new Dictionary<StingKind, AudioClip>();
        private static readonly Dictionary<StingKind, Task<StereoBuffer>> Tasks = new Dictionary<StingKind, Task<StereoBuffer>>();
        private static Host _host;

        public static void Warmup()
        {
            foreach (StingKind k in Enum.GetValues(typeof(StingKind)))
                StartRender(k);
        }

        public static void Play(StingKind kind)
        {
            EnsureHost();
            StartRender(kind);
            _host.Request(kind);
        }

        public static void Stop() { if (_host != null) _host.Halt(); }

        private static void StartRender(StingKind k)
        {
            if (Clips.ContainsKey(k) || Tasks.ContainsKey(k)) return;
            Tasks[k] = Task.Run(() => MusicSynth.RenderSting(k));
        }

        private static void EnsureHost()
        {
            if (_host != null) return;
            var go = new GameObject("[Müzik Sting]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _host = go.AddComponent<Host>();
        }

        private static AudioClip TryGetClip(StingKind k)
        {
            if (Clips.TryGetValue(k, out var c) && c != null) return c;
            if (Tasks.TryGetValue(k, out var t) && t.IsCompleted)
            {
                Tasks.Remove(k);
                if (t.IsFaulted || t.IsCanceled) return null;
                c = MusicClips.Create("HarekatSting_" + k, t.Result);
                Clips[k] = c;
                return c;
            }
            return null;
        }

        private sealed class Host : MonoBehaviour
        {
            private AudioSource _src;
            private StingKind? _pending;
            private float _fadeOut = -1f;

            private void Awake()
            {
                _src = gameObject.AddComponent<AudioSource>();
                _src.playOnAwake = false;
                _src.spatialBlend = 0f;
                _src.ignoreListenerPause = true;
                MixerRouting.Route(_src, MixChannel.Muzik);
            }

            public void Request(StingKind k) { _pending = k; _fadeOut = _src.isPlaying ? 0.4f : -1f; }

            public void Halt() { _pending = null; if (_src.isPlaying) _fadeOut = 0.6f; }

            private void Update()
            {
                float v;
                try { v = Mathf.Clamp01(VolumeProvider()); } catch (Exception) { v = 0.8f; }

                if (_fadeOut >= 0f && _src.isPlaying)
                {
                    _src.volume = Mathf.MoveTowards(_src.volume, 0f, Time.unscaledDeltaTime / Mathf.Max(0.05f, _fadeOut));
                    if (_src.volume <= 0.001f) { _src.Stop(); _fadeOut = -1f; }
                    else if (!_pending.HasValue) return;
                    else return;
                }

                if (_pending.HasValue)
                {
                    var clip = TryGetClip(_pending.Value);
                    if (clip != null)
                    {
                        _src.clip = clip;
                        _src.volume = v * Gain;
                        _src.Play();
                        _pending = null;
                        _fadeOut = -1f;
                    }
                    return;
                }

                if (_src.isPlaying) _src.volume = v * Gain;
            }
        }
    }
}
