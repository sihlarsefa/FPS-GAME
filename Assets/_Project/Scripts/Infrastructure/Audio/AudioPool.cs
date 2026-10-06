using System.Collections.Generic;
using Project.Infrastructure.Audio.HdrMix;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.Infrastructure.Audio
{
    /// <summary>
    /// Önceden oluşturulmuş <see cref="AudioSource"/> havuzu (GameAudio ana nesnesinin çocukları).
    /// Tek seferlik sesler için: boş ses yoksa en önemsiz (dinleyicide en kısık) ses çalınır ya da yeni ses düşürülür.
    /// Döngüler için: kaynak <see cref="Release"/> edilene kadar ayrılmış kalır; gerekirse havuz büyür.
    /// Yok edilmiş kaynaklar (ör. biri kaynağı Destroy ettiyse) bir sonraki kullanımda yeniden oluşturulur.
    /// Yalnızca ana iş parçacığından kullanılır; sıcak yollarda bellek ayırmaz.
    /// </summary>
    public sealed class AudioPool
    {
        /// <summary>Havuzdaki tek ses kanalı.</summary>
        internal sealed class Voice
        {
            public int Index;
            public AudioSource Source;
            public AudioLowPassFilter LowPass;
            public Transform Transform;

            /// <summary>Gerçek zaman (Time.unscaledTime) olarak sesin başladığı an.</summary>
            public float StartTime;

            /// <summary>Gerçek zaman (Time.unscaledTime) olarak sesin biteceği an (tek seferlik sesler).</summary>
            public float EndTime;

            /// <summary>Dinleyicide tahmini algılanan şiddet; çalma (steal) kararında kullanılır.</summary>
            public float Importance;

            public SoundId Id;

            // ---- döngü durumu
            public bool InUse;
            public bool Spatial;
            public bool Ambient;
            public bool HasFollow;
            public Transform Follow;
            public float MaxDistance;
            public float BaseVolume;
            public float FadeOutRate;
            public float NextDistanceUpdate;
            /// <summary>Döngünün başlatıldığı etkin sahne (takip hedefi yoksa sahneyle birlikte kapanır).</summary>
            public Scene Scene;

            /// <summary>Döngünün başlatıldığı kare (sahne geçişi sırasında başlatılanları korumak için).</summary>
            public int StartFrame;

            public void ClearLoopState()
            {
                InUse = false;
                Spatial = false;
                Ambient = false;
                HasFollow = false;
                Follow = null;
                MaxDistance = 0f;
                BaseVolume = 0f;
                FadeOutRate = 0f;
                NextDistanceUpdate = 0f;
                Scene = default;
                StartFrame = 0;
            }
        }

        private readonly List<Voice> _voices;
        private readonly Transform _parent;
        private readonly string _prefix;
        private readonly bool _spatial;
        private readonly int _maxCapacity;
        private int _cursor;

        public AudioPool(Transform parent, string prefix, int initialCapacity, int maxCapacity, bool spatial)
        {
            _parent = parent;
            _prefix = string.IsNullOrEmpty(prefix) ? "Voice" : prefix;
            _spatial = spatial;
            initialCapacity = Mathf.Max(1, initialCapacity);
            _maxCapacity = Mathf.Max(initialCapacity, maxCapacity);
            _voices = new List<Voice>(_maxCapacity);
            for (var i = 0; i < initialCapacity; i++)
                _voices.Add(CreateVoice(i));
        }

        /// <summary>Şu an oluşturulmuş kanal sayısı.</summary>
        public int Capacity => _voices.Count;

        public bool IsSpatial => _spatial;

        internal List<Voice> Voices => _voices;

        /// <summary>Şu an çalan (ya da döngü için ayrılmış) kanal sayısı.</summary>
        public int CountActive(float now)
        {
            var count = 0;
            for (var i = 0; i < _voices.Count; i++)
            {
                var v = _voices[i];
                if (v.InUse || v.EndTime > now)
                    count++;
            }

            return count;
        }

        /// <summary>
        /// Tek seferlik ses için kanal: önce boş kanal, yoksa önemi yeni sesten düşük olan en önemsiz kanal çalınır.
        /// Tüm kanallar daha önemliyse null döner (yeni ses düşürülür).
        /// </summary>
        internal Voice AcquireOneShot(float now, float importance, bool forceSteal = false)
        {
            var count = _voices.Count;
            Voice weakest = null;
            var weakestImportance = float.MaxValue;
            for (var n = 0; n < count; n++)
            {
                var i = _cursor + n;
                if (i >= count)
                    i -= count;

                var v = _voices[i];
                if (v.InUse)
                    continue;

                if (v.EndTime <= now && !forceSteal)
                {
                    _cursor = i + 1 >= count ? 0 : i + 1;
                    return Prepare(v);
                }

                // Sesler kuyruğa doğru kısılır: kalan süre oranıyla ağırlıklandır (bitişe yakın olan önce çalınır).
                var duration = v.EndTime - v.StartTime;
                var remaining = duration > 0.0001f ? Mathf.Clamp01((v.EndTime - now) / duration) : 0f;
                var score = v.Importance * (0.25f + 0.75f * remaining);
                if (score < weakestImportance)
                {
                    weakestImportance = score;
                    weakest = v;
                }
            }

            if (count < _maxCapacity && !forceSteal)
            {
                var created = CreateVoice(count);
                _voices.Add(created);
                return Prepare(created);
            }

            if (weakest == null || weakestImportance > importance)
                return null;

            return Prepare(weakest);
        }

        /// <summary>Döngü için kanal ayırır. Havuz doluysa büyür; sınırdaysa durmuş bir döngü kanalı geri alınır.</summary>
        internal Voice AcquireLoop()
        {
            for (var i = 0; i < _voices.Count; i++)
            {
                var v = _voices[i];
                if (!v.InUse)
                    return Reserve(Prepare(v));
            }

            if (_voices.Count < _maxCapacity)
            {
                var created = CreateVoice(_voices.Count);
                _voices.Add(created);
                return Reserve(Prepare(created));
            }

            // Sınırda: çağıranın Stop() ile durdurup StopLoop çağırmayı unuttuğu kanalı geri kazan.
            for (var i = 0; i < _voices.Count; i++)
            {
                var v = _voices[i];
                if (v.Source == null || (!v.Source.isPlaying && v.FadeOutRate <= 0f))
                {
                    v.ClearLoopState();
                    return Reserve(Prepare(v));
                }
            }

            return null;
        }

        /// <summary>Kanalı durdurur ve havuza geri verir.</summary>
        internal void Release(Voice v)
        {
            if (v == null)
                return;

            if (v.Source != null)
            {
                v.Source.Stop();
                v.Source.clip = null;
                if (v.HasFollow && v.Transform != null)
                    v.Transform.localPosition = Vector3.zero;
            }

            v.ClearLoopState();
            v.StartTime = 0f;
            v.EndTime = 0f;
            v.Importance = 0f;
            v.Id = SoundId.None;
        }

        /// <summary>Kaynağa ait kanalı bulur (bu havuza ait değilse null).</summary>
        internal Voice Find(AudioSource source)
        {
            if (ReferenceEquals(source, null))
                return null;

            for (var i = 0; i < _voices.Count; i++)
            {
                if (ReferenceEquals(_voices[i].Source, source))
                    return _voices[i];
            }

            return null;
        }

        /// <summary>Tüm kanalları durdurur.</summary>
        public void StopAll()
        {
            for (var i = 0; i < _voices.Count; i++)
                Release(_voices[i]);
        }

        // ------------------------------------------------------------------------------------

        private static Voice Reserve(Voice v)
        {
            if (v == null)
                return null;

            v.ClearLoopState();
            v.InUse = true;
            v.EndTime = float.MaxValue;
            return v;
        }

        /// <summary>Kanalı yeniden kullanıma hazırlar (yok edilmişse yeniden oluşturur, varsayılanlara döndürür).</summary>
        private Voice Prepare(Voice v)
        {
            if (v.Source == null || v.Transform == null)
                Rebuild(v);

            var s = v.Source;
            if (s.isPlaying)
                s.Stop();

            s.loop = false;
            s.mute = false;
            s.pitch = 1f;
            s.volume = 1f;
            s.ignoreListenerPause = false;
            s.priority = 128;
            if (v.LowPass != null && v.LowPass.enabled)
                v.LowPass.enabled = false;

            return v;
        }

        private MixChannel _channel = MixChannel.Efekt;

        /// <summary>Havuz kaynaklarının AudioMixer kanalı (mixer yoksa etkisiz). Mevcut ve sonradan kurulan kanallara uygulanır.</summary>
        public void SetMixChannel(MixChannel channel)
        {
            _channel = channel;
            for (var i = 0; i < _voices.Count; i++)
                MixerRouting.Route(_voices[i].Source, channel);
        }

        private Voice CreateVoice(int index)
        {
            var v = new Voice { Index = index };
            Rebuild(v);
            return v;
        }

        private void Rebuild(Voice v)
        {
            if (v.Transform != null)
                Object.Destroy(v.Transform.gameObject);

            var go = new GameObject(_prefix + "_" + v.Index.ToString("00"));
            go.transform.SetParent(_parent, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.dopplerLevel = 0f;
            source.spread = 0f;
            source.reverbZoneMix = 1f;
            if (_spatial)
            {
                source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.minDistance = 2f;
                source.maxDistance = 80f;
            }
            else
            {
                source.spatialBlend = 0f;
            }

            MixerRouting.Route(source, _channel);
            v.Source = source;
            v.Transform = go.transform;
            if (_spatial)
            {
                var lp = go.AddComponent<AudioLowPassFilter>();
                lp.cutoffFrequency = 22000f;
                lp.lowpassResonanceQ = 1f;
                lp.enabled = false;
                v.LowPass = lp;
            }
            else
            {
                v.LowPass = null;
            }
        }
    }
}
