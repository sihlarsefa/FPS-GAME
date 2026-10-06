using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Infrastructure.Audio
{
    /// <summary>
    /// Telsiz/oyuncu sesli replikleri (Resources/Audio/Voice/&lt;anahtar&gt;.wav; YER TUTUCU macOS TTS).
    /// Dost sesi: telsiz filtresi (HP ~400 Hz, LP ~3.5 kHz, hafif bozulma) + önce/sonra squelch; rol başına perde 0.85-0.95.
    /// Oyuncu sesi: filtresiz, perde 0.92. Klip yoksa sessizce hiçbir şey yapmaz.
    /// </summary>
    public sealed class RadioVoicePlayer : MonoBehaviour
    {
        public const string Folder = "Audio/Voice/";
        public const float PlayerPitch = 0.92f;
        public const float SquelchLead = 0.12f;

        // Tembel + sınırlı: klip ilk kullanımda yüklenir; en çok CacheCapacity klip tutulur (ses bankı toplu yüklenmez).
        private static readonly Dialogue.LruCache<string, AudioClip> Cache =
            new Dialogue.LruCache<string, AudioClip>(Dialogue.DialogueClipLibrary.CacheCapacity, StringComparer.Ordinal);
        private static readonly HashSet<string> Missing = new HashSet<string>(StringComparer.Ordinal);
        private const int MissingLimit = 2048;

        private AudioSource _radio;
        private AudioSource _local;
        private AudioClip _pending;
        private float _pendingPitch = 1f;
        private float _pendingVolume = 1f;
        private float _startAt = -1f;
        private float _endBeepAt = -1f;
        private bool _pendingProcessed;
        private AudioClip _tempClip;
        private AudioHighPassFilter _hp;
        private AudioLowPassFilter _lp;
        private AudioDistortionFilter _dist;

        /// <summary>Role göre sabit perde (0.85-0.95); aynı roldeki farklı konuşmacılar için seed ile ±0.015 ayrım.</summary>
        public static float PitchForRole(string role, int seed = 0)
        {
            float basePitch;
            switch (role)
            {
                case "Leader": basePitch = 0.86f; break;
                case "Rifleman": basePitch = 0.92f; break;
                case "Marksman": basePitch = 0.90f; break;
                case "MachineGunner": basePitch = 0.87f; break;
                case "Medic": basePitch = 0.94f; break;
                case "Radioman": basePitch = 0.93f; break;
                case "Grenadier": basePitch = 0.89f; break;
                default: basePitch = 0.91f; break;
            }

            var jitter = ((seed & 0x7fffffff) % 7 - 3) * 0.005f;
            return Mathf.Clamp(basePitch + jitter, 0.85f, 0.95f);
        }

        /// <summary>Oyuncu çağrısı için klip anahtarı: player_&lt;kategori&gt;_1|2 (roll 0..1).</summary>
        public static string PlayerKey(string category, float roll)
        {
            if (string.IsNullOrEmpty(category))
                return null;
            return "player_" + category + (roll < 0.5f ? "_1" : "_2");
        }

        public static AudioClip LoadClip(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;
            return LoadPath(Folder + key);
        }

        public static bool HasClip(string key) => LoadClip(key) != null;

        /// <summary>
        /// v2 ses çözümü: konuşmacıya kalıcı ses + stres klasörü (VoiceV2Resolver), yoksa eski Voice/&lt;anahtar&gt; klibi, yoksa null (sessizlik).
        /// </summary>
        public static AudioClip LoadClipFor(string key, int speakerSeed, VoiceStress stress)
        {
            if (string.IsNullOrEmpty(key))
                return null;
            var clip = LoadPath(VoiceV2Resolver.ResourcePath(VoiceV2Resolver.VoiceForSpeaker(speakerSeed), stress, key));
            return clip != null ? clip : LoadClip(key);
        }

        /// <summary>Tek Resources yolu için tembel yükleme (LRU önbellek + negatif önbellek). Klip yoksa null.</summary>
        private static AudioClip LoadPath(string path)
        {
            if (Missing.Contains(path))
                return null;

            if (Cache.TryGet(path, out var cached))
            {
                if (cached != null)
                    return cached;
                Cache.Remove(path); // klip boşaltılmış: yeniden yükle
            }

            AudioClip clip = null;
            try
            {
                clip = Resources.Load<AudioClip>(path);
            }
            catch (Exception)
            {
            }

            if (clip == null)
            {
                if (Missing.Count >= MissingLimit)
                    Missing.Clear();
                Missing.Add(path);
                return null;
            }

            Cache.Set(path, clip);
            return clip;
        }

        /// <summary>Dost telsiz repliği: squelch + filtreli ses. Klip yoksa false.</summary>
        public bool PlayRadio(string key, string role, int speakerSeed, float volume = 0.9f, VoiceStress stress = VoiceStress.Sakin)
        {
            var clip = LoadClipFor(key, speakerSeed, stress);
            if (clip == null || !CanPlay())
                return false;

            EnsureSources();
            _pendingPitch = PitchForRole(role, speakerSeed);
            _pendingVolume = volume;

            // v2 telsiz işlemcisi: 300-3400 Hz bant, sıkıştırma, doyum, squelch, tıslama. Okunamazsa eski filtre zinciri
            // (örneklenemeyen/CompressedInMemory klipte GetData hiç çağrılmaz: klip doğrudan çalınır, uyarı yok).
            var processed = Dialogue.RadioClipProcessor.TryProcess(clip, speakerSeed, 1f, Project.Application.Dialogue.DialogueStress.Calm, false);
            _pendingProcessed = processed != null;
            _pending = processed != null ? processed : clip;
            _startAt = Time.unscaledTime + (processed != null ? 0.02f : SquelchLead);
            return true;
        }

        /// <summary>Yerel oyuncu çağrısı: filtresiz, perde 0.92.</summary>
        public bool PlayPlayer(string key, float volume = 0.9f)
        {
            var clip = LoadClip(key);
            if (clip == null || !CanPlay())
                return false;

            EnsureSources();
            _local.pitch = PlayerPitch;
            _local.PlayOneShot(clip, Mathf.Clamp01(volume * GameAudio.MasterVolume));
            return true;
        }

        private static bool CanPlay()
        {
            try
            {
                return GameAudio.Enabled && GameAudio.HasListener;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void Update()
        {
            var now = Time.unscaledTime;
            if (_pending != null && _startAt > 0f && now >= _startAt)
            {
                var clip = _pending;
                _pending = null;
                _startAt = -1f;
                _radio.pitch = _pendingPitch;
                SetLegacyFilters(!_pendingProcessed);
                if (_tempClip != null)
                    Destroy(_tempClip);
                _tempClip = _pendingProcessed ? clip : null;
                _radio.clip = clip;
                _radio.volume = Mathf.Clamp01(_pendingVolume * GameAudio.MasterVolume);
                _radio.Play();
                _endBeepAt = now + clip.length / Mathf.Max(0.1f, _pendingPitch) + 0.03f;
            }

            if (_endBeepAt > 0f && now >= _endBeepAt)
            {
                _endBeepAt = -1f;
                try
                {
                    GameAudio.Play2D(SoundId.RadioBeep, 0.35f, UnityEngine.Random.Range(0.95f, 1.08f));
                }
                catch (Exception)
                {
                }
            }
        }

        private void SetLegacyFilters(bool on)
        {
            if (_hp != null) _hp.enabled = on;
            if (_lp != null) _lp.enabled = on;
            if (_dist != null) _dist.enabled = on;
        }

        private void EnsureSources()
        {
            if (_radio != null && _local != null)
                return;

            var r = new GameObject("RadioVoice");
            r.transform.SetParent(transform, false);
            _radio = r.AddComponent<AudioSource>();
            _radio.playOnAwake = false;
            _radio.spatialBlend = 0f;
            _radio.ignoreListenerPause = false;
            var hp = _hp = r.AddComponent<AudioHighPassFilter>();
            hp.cutoffFrequency = 400f;
            var lp = _lp = r.AddComponent<AudioLowPassFilter>();
            lp.cutoffFrequency = 3500f;
            var dist = _dist = r.AddComponent<AudioDistortionFilter>();
            dist.distortionLevel = 0.18f;

            var l = new GameObject("PlayerVoice");
            l.transform.SetParent(transform, false);
            _local = l.AddComponent<AudioSource>();
            _local.playOnAwake = false;
            _local.spatialBlend = 0f;
        }
    }
}
