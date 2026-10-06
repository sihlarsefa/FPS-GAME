using System;
using UnityEngine;

namespace Project.Infrastructure.Audio.Dialogue
{
    /// <summary>
    /// Diyalog v2 çalma katmanı: 3 bağırma kaynağı (3D, filtresiz; engel varsa alçak geçiren) + 1 telsiz kaynağı (2D).
    /// Kliplerin ömrünü yönetir: çalma sırasında üretilmiş (sahipli) klipler bitince yok edilir; Resources'tan gelen ve doğrudan
    /// çalınan klipler (örneklenemeyenler) yok EDİLMEZ (varlık). GameAudio havuzundan bağımsızdır.
    /// </summary>
    internal sealed class DialoguePlayback
    {
        private sealed class Slot
        {
            public AudioSource Source;
            public AudioLowPassFilter LowPass;
            public int Speaker = int.MinValue;
            public float EndAt;
            public AudioClip Clip;
            /// <summary>true: klip çalma için üretildi (bitince Destroy). false: Resources varlığı (Destroy edilmez).</summary>
            public bool OwnsClip = true;
            public Transform Follow;
        }

        public const int ShoutSlots = 3;
        public const float RadioBandLowHz = 300f;
        public const float RadioBandHighHz = 3400f;

        private readonly Slot[] _shout = new Slot[ShoutSlots];
        private readonly Slot _radio = new Slot();
        private readonly AudioHighPassFilter _radioHigh;
        private readonly AudioLowPassFilter _radioLow;
        private readonly Transform _root;

        public DialoguePlayback(Transform root)
        {
            _root = root;
            for (var i = 0; i < ShoutSlots; i++)
            {
                var go = new GameObject("DialogueShout" + i);
                go.transform.SetParent(root, false);
                var s = new Slot { Source = go.AddComponent<AudioSource>(), LowPass = go.AddComponent<AudioLowPassFilter>() };
                s.Source.playOnAwake = false;
                s.Source.spatialBlend = 1f;
                s.Source.rolloffMode = AudioRolloffMode.Linear;
                s.Source.minDistance = 3f;
                s.Source.dopplerLevel = 0f;
                s.Source.spread = 20f;
                s.LowPass.cutoffFrequency = 22000f;
                _shout[i] = s;
            }

            var r = new GameObject("DialogueRadio");
            r.transform.SetParent(root, false);
            _radio.Source = r.AddComponent<AudioSource>();
            _radio.Source.playOnAwake = false;
            _radio.Source.spatialBlend = 0f;
            // Doğrudan (DSP'siz) çalınan telsiz klipleri için bant sınırı; DSP'li kliplerde kapalı kalır.
            _radioHigh = r.AddComponent<AudioHighPassFilter>();
            _radioHigh.cutoffFrequency = RadioBandLowHz;
            _radioHigh.enabled = false;
            _radioLow = r.AddComponent<AudioLowPassFilter>();
            _radioLow.cutoffFrequency = RadioBandHighHz;
            _radioLow.enabled = false;
        }

        /// <summary>
        /// Yakın bağırma: konuşmacının üzerinde 3D. <paramref name="occlusion"/> 0 (açık) .. 1 (tam engelli).
        /// <paramref name="ownsClip"/> false ise klip bir varlıktır (Destroy edilmez); <paramref name="pitch"/> doğrudan klipte kimlik perdesini taşır.
        /// </summary>
        public void PlayShout(int speakerId, Transform follow, AudioClip clip, float volume, float maxDistance, float occlusion,
            bool ownsClip = true, float pitch = 1f)
        {
            var slot = Acquire(speakerId);
            Release(slot);
            pitch = Mathf.Clamp(pitch, 0.5f, 2f);
            slot.Speaker = speakerId;
            slot.Clip = clip;
            slot.OwnsClip = ownsClip;
            slot.Follow = follow;
            slot.EndAt = Time.unscaledTime + clip.length / pitch + 0.1f;
            if (follow != null)
                slot.Source.transform.position = follow.position + Vector3.up * 1.6f;
            slot.Source.maxDistance = Mathf.Max(8f, maxDistance);
            slot.LowPass.cutoffFrequency = Mathf.Lerp(22000f, 1400f, Mathf.Clamp01(occlusion));
            slot.Source.volume = Mathf.Clamp01(volume * (1f - 0.45f * Mathf.Clamp01(occlusion)) * GameAudio.MasterVolume);
            slot.Source.pitch = pitch;
            slot.Source.clip = clip;
            slot.Source.Play();
        }

        /// <summary>
        /// Telsiz kanalı (2D). <paramref name="ownsClip"/> false ise klip telsiz DSP'sinden geçmemiştir: bant süzgeçleri
        /// (<see cref="RadioBandLowHz"/> .. <paramref name="bandHighCutHz"/>) kaynakta açılır.
        /// </summary>
        public void PlayRadio(int speakerId, AudioClip clip, float volume, bool ownsClip = true, float pitch = 1f, float bandHighCutHz = 0f)
        {
            Release(_radio);
            pitch = Mathf.Clamp(pitch, 0.5f, 2f);
            _radio.Speaker = speakerId;
            _radio.Clip = clip;
            _radio.OwnsClip = ownsClip;
            _radio.EndAt = Time.unscaledTime + clip.length / pitch + 0.1f;

            var band = !ownsClip;
            _radioHigh.enabled = band;
            _radioLow.enabled = band;
            if (band)
                _radioLow.cutoffFrequency = bandHighCutHz > 0f ? bandHighCutHz : RadioBandHighHz;

            _radio.Source.volume = Mathf.Clamp01(volume * GameAudio.MasterVolume);
            _radio.Source.pitch = pitch;
            _radio.Source.clip = clip;
            _radio.Source.Play();
        }

        public void Stop(int speakerId)
        {
            for (var i = 0; i < ShoutSlots; i++)
                if (_shout[i].Speaker == speakerId)
                    Release(_shout[i]);
            if (_radio.Speaker == speakerId)
                Release(_radio);
        }

        public void StopAll()
        {
            for (var i = 0; i < ShoutSlots; i++)
                Release(_shout[i]);
            Release(_radio);
        }

        public void Update(float now)
        {
            for (var i = 0; i < ShoutSlots; i++)
            {
                var s = _shout[i];
                if (s.Clip == null)
                    continue;
                if (now >= s.EndAt)
                {
                    Release(s);
                    continue;
                }

                if (s.Follow != null)
                    s.Source.transform.position = s.Follow.position + Vector3.up * 1.6f;
            }

            if (_radio.Clip != null && now >= _radio.EndAt)
                Release(_radio);
        }

        private Slot Acquire(int speakerId)
        {
            Slot free = null;
            Slot oldest = null;
            for (var i = 0; i < ShoutSlots; i++)
            {
                var s = _shout[i];
                if (s.Speaker == speakerId)
                    return s;
                if (s.Clip == null && free == null)
                    free = s;
                if (oldest == null || s.EndAt < oldest.EndAt)
                    oldest = s;
            }

            return free ?? oldest;
        }

        private static void Release(Slot s)
        {
            try
            {
                if (s.Source != null && s.Source.isPlaying)
                    s.Source.Stop();
                if (s.Source != null)
                {
                    s.Source.clip = null;
                    s.Source.pitch = 1f;
                }
            }
            catch (Exception)
            {
            }

            // Yalnız çalma için üretilen klip yok edilir; Resources varlığı (doğrudan çalınan) Destroy EDİLMEZ.
            if (s.Clip != null && s.OwnsClip)
                UnityEngine.Object.Destroy(s.Clip);
            s.Clip = null;
            s.OwnsClip = true;
            s.Speaker = int.MinValue;
            s.Follow = null;
        }
    }
}
