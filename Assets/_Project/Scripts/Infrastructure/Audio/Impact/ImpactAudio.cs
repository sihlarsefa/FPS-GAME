using System;
using System.Collections.Generic;
using Project.Infrastructure.Audio.HdrMix;
using Project.Infrastructure.Vfx;
using UnityEngine;

namespace Project.Infrastructure.Audio
{
    /// <summary>
    /// Yüzey darbe sesi çalıcı: (yüzey, varyasyon, mesafe kovası) başına tembel üretilen prosedürel klipler, küçük 3B
    /// ses havuzu. Dinleyici mesafesine göre kova (alçak geçiren) seçilir; ses hızı gecikmesi uzak isabetlere uygulanır.
    /// Başarısızlıkta false döner (çağıran GameAudio'ya düşer).
    /// </summary>
    public static class ImpactAudio
    {
        private const int VoiceCount = 10;
        private const float MaxPlayPerWindow = 6f;
        private const float WindowSeconds = 0.1f;

        private static readonly Dictionary<int, AudioClip> Clips = new Dictionary<int, AudioClip>();
        private static AudioSource[] _voices;
        private static int _next;
        private static float _windowStart;
        private static int _windowCount;

        /// <summary>Yüzey darbe sesi. false: çalınamadı (hazır değil/menzil dışı vb.).</summary>
        public static bool PlayImpact(SurfaceKind surface, Vector3 point, Vector3 listener, uint seed, float volume,
            float maxDistance)
        {
            try
            {
                var distance = Vector3.Distance(listener, point);
                if (!(distance <= maxDistance))
                    return true; // menzil dışı: sessiz, ama 'ele alındı'
                if (!Throttle())
                    return true;

                var variant = ImpactSoundRules.VariantFor(seed, surface);
                var bucket = ImpactSoundRules.DistanceBucket(distance);
                var key = ((int)ImpactSoundRules.Family(surface) << 8) | (variant << 4) | bucket;
                if (!Clips.TryGetValue(key, out var clip) || clip == null)
                {
                    clip = MakeClip("imp_" + surface + "_" + variant + "_" + bucket,
                        ImpactSynth.Render(surface, variant, bucket));
                    Clips[key] = clip;
                }

                return Emit(clip, point, distance, volume * SurfaceImpactAudio.VolumeScale(surface),
                    ImpactSoundRules.Pitch(seed), maxDistance);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Sekme vızıltısı (frekans süpürmeli). Pitch hafif değişir.</summary>
        public static bool PlayRicochet(Vector3 point, Vector3 listener, uint seed, float volume, float maxDistance)
        {
            try
            {
                var distance = Vector3.Distance(listener, point);
                if (!(distance <= maxDistance))
                    return true;
                if (!Throttle())
                    return true;

                var variant = Math.Min((int)(RicochetRules.Roll01(seed, 21) * ImpactSynth.RicochetVariations),
                    ImpactSynth.RicochetVariations - 1);
                var bucket = ImpactSoundRules.DistanceBucket(distance);
                var key = (1 << 16) | (variant << 4) | bucket;
                if (!Clips.TryGetValue(key, out var clip) || clip == null)
                {
                    clip = MakeClip("ric_" + variant + "_" + bucket, ImpactSynth.RenderRicochet(variant, bucket));
                    Clips[key] = clip;
                }

                return Emit(clip, point, distance, volume, 0.9f + 0.25f * RicochetRules.Roll01(seed, 22), maxDistance);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool Throttle()
        {
            var now = Time.unscaledTime;
            if (now - _windowStart > WindowSeconds)
            {
                _windowStart = now;
                _windowCount = 0;
            }

            return ++_windowCount <= MaxPlayPerWindow;
        }

        private static AudioClip MakeClip(string name, float[] samples)
        {
            if (samples == null || samples.Length == 0)
                return null;
            var clip = AudioClip.Create(name, samples.Length, 1, SynthDsp.SampleRate, false);
            clip.SetData(samples, 0);
            clip.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return clip;
        }

        private static bool Emit(AudioClip clip, Vector3 point, float distance, float volume, float pitch, float maxDistance)
        {
            if (clip == null || !UnityEngine.Application.isPlaying || !EnsureVoices())
                return false;

            var edge = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(maxDistance * 0.75f, maxDistance, distance));
            var v = Mathf.Clamp01(volume * edge);
            if (v <= 0.0005f)
                return true;

            var src = _voices[_next];
            _next = (_next + 1) % _voices.Length;
            if (src == null)
            {
                _voices = null;
                return false;
            }

            src.Stop();
            src.transform.position = point;
            src.clip = clip;
            src.volume = v;
            src.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
            src.minDistance = Mathf.Clamp(maxDistance * 0.05f, 1f, 25f);
            src.maxDistance = maxDistance;
            // Ses hızı gecikmesi (yakında ihmal edilir).
            var delay = AcousticsMath.Delay(distance);
            if (delay > 0.03f)
                src.PlayDelayed(Mathf.Min(delay, 1.5f));
            else
                src.Play();
            return true;
        }

        private static bool EnsureVoices()
        {
            if (_voices != null && _voices.Length == VoiceCount && _voices[0] != null)
                return true;

            var root = new GameObject("ImpactAudioVoices") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(root);
            _voices = new AudioSource[VoiceCount];
            for (var i = 0; i < VoiceCount; i++)
            {
                var go = new GameObject("v" + i);
                go.transform.SetParent(root.transform, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 1f;
                s.rolloffMode = AudioRolloffMode.Logarithmic;
                s.dopplerLevel = 0f;
                s.loop = false;
                MixerRouting.Route(s, MixChannel.Darbe); // gerçek mikser Darbe grubu (mixer yoksa no-op)
                _voices[i] = s;
            }

            return true;
        }
    }
}
