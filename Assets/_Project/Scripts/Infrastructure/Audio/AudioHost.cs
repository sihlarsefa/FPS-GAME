using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.Infrastructure.Audio
{
    /// <summary>
    /// "[GameAudio]" ana nesnesi (DontDestroyOnLoad). Ses havuzlarını barındırır ve her karede: dinleyiciyi izler,
    /// takip eden döngüleri hedeflerine taşır, uzak döngülere mesafe süzgeci uygular, döngü kapanışlarını ve ortam
    /// sesi çapraz geçişlerini yürütür. Doğrudan kullanılmaz — <see cref="GameAudio"/> üzerinden yönetilir.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    internal sealed class AudioHost : MonoBehaviour
    {
        public const string ObjectName = "[GameAudio]";

        private const float ListenerRetryInterval = 0.25f;
        private const float LoopDistanceInterval = 0.1f;
        private const float AmbienceFadeSeconds = 1.6f;
        private const float LoopFadeSeconds = 0.12f;

        private AudioListener _listener;
        private Transform _listenerTransform;
        private float _nextListenerSearch;

        private AudioSource[] _ambience;
        private float[] _ambienceLevel;     // 0..1 çapraz geçiş seviyesi
        private float[] _ambienceTarget;    // hedef seviye (0 veya 1)
        private float[] _ambienceVolume;    // istenen taban ses
        private SoundId[] _ambienceId;      // her kaynağın çaldığı ses (durana kadar)
        private int _ambienceCurrent;
        private SoundId _requestedAmbience;

        public AudioPool Spatial { get; private set; }
        public AudioPool Flat { get; private set; }
        public AudioPool Loops { get; private set; }

        public SoundId CurrentAmbience => _requestedAmbience;

        public static AudioHost Create(int spatialVoices, int flatVoices, int loopVoices, int maxLoopVoices)
        {
            var go = new GameObject(ObjectName);
            DontDestroyOnLoad(go);
            var host = go.AddComponent<AudioHost>();
            host.Setup(spatialVoices, flatVoices, loopVoices, maxLoopVoices);
            return host;
        }

        private void Setup(int spatialVoices, int flatVoices, int loopVoices, int maxLoopVoices)
        {
            var root = transform;
            Spatial = new AudioPool(CreateGroup(root, "Spatial"), "Sfx3D", spatialVoices, spatialVoices, true);
            Flat = new AudioPool(CreateGroup(root, "Flat"), "Sfx2D", flatVoices, flatVoices, false);
            Loops = new AudioPool(CreateGroup(root, "Loops"), "Loop", loopVoices, maxLoopVoices, true);

            var ambienceRoot = CreateGroup(root, "Ambience");
            _ambience = new AudioSource[2];
            _ambienceLevel = new float[2];
            _ambienceTarget = new float[2];
            _ambienceVolume = new float[2];
            _ambienceId = new SoundId[2];
            for (var i = 0; i < 2; i++)
            {
                var go = new GameObject("Ambience_" + i);
                go.transform.SetParent(ambienceRoot, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.loop = true;
                s.spatialBlend = 0f;
                s.dopplerLevel = 0f;
                s.priority = 32;
                s.volume = 0f;
                _ambience[i] = s;
            }

            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private static Transform CreateGroup(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private void OnDestroy()
        {
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            GameAudio.NotifyHostDestroyed(this);
        }

        // =====================================================================================
        //  DİNLEYİCİ
        // =====================================================================================

        /// <summary>Etkin bir AudioListener varsa konumunu verir (gerekirse kısıtlı aralıklarla yeniden arar).</summary>
        public bool TryGetListenerPosition(out Vector3 position)
        {
            if (EnsureListener(Time.unscaledTime))
            {
                position = _listenerTransform.position;
                return true;
            }

            position = default;
            return false;
        }

        public bool HasListener => EnsureListener(Time.unscaledTime);

        private bool EnsureListener(float now)
        {
            if (_listener != null && _listener.isActiveAndEnabled)
                return true;

            _listener = null;
            _listenerTransform = null;
            if (now < _nextListenerSearch)
                return false;

            _nextListenerSearch = now + ListenerRetryInterval;
            var candidate = Object.FindAnyObjectByType<AudioListener>();
            if (candidate == null || !candidate.isActiveAndEnabled)
            {
                candidate = null;
                var all = Object.FindObjectsByType<AudioListener>();
                for (var i = 0; i < all.Length; i++)
                {
                    if (all[i] != null && all[i].isActiveAndEnabled)
                    {
                        candidate = all[i];
                        break;
                    }
                }
            }

            if (candidate == null)
                return false;

            _listener = candidate;
            _listenerTransform = candidate.transform;
            return true;
        }

        // =====================================================================================
        //  KARE GÜNCELLEMESİ
        // =====================================================================================

        private void Update()
        {
            var now = Time.unscaledTime;
            var dt = Time.unscaledDeltaTime;
            UpdateLoops(now, dt);
            UpdateAmbience(dt);
        }

        private void UpdateLoops(float now, float dt)
        {
            if (Loops == null)
                return;

            var hasListener = false;
            var listenerPos = default(Vector3);
            var voices = Loops.Voices;
            for (var i = 0; i < voices.Count; i++)
            {
                var v = voices[i];
                if (!v.InUse)
                    continue;

                var src = v.Source;
                if (src == null)
                {
                    // Biri kaynağı yok etti; kanal bir sonraki kullanımda yeniden kurulur.
                    v.ClearLoopState();
                    continue;
                }

                if (v.FadeOutRate > 0f)
                {
                    var vol = src.volume - v.FadeOutRate * dt;
                    if (vol <= 0.0005f)
                        Loops.Release(v);
                    else
                        src.volume = vol;
                    continue;
                }

                if (v.HasFollow)
                {
                    var follow = v.Follow;
                    if (follow == null)
                    {
                        // Takip edilen nesne yok edildi → döngüyü yumuşakça kapat.
                        BeginFadeOut(v);
                        continue;
                    }

                    v.Transform.position = follow.position;
                }

                if (!v.Spatial || now < v.NextDistanceUpdate)
                    continue;

                v.NextDistanceUpdate = now + LoopDistanceInterval;
                if (!hasListener)
                {
                    if (!EnsureListener(now))
                        continue;
                    hasListener = true;
                    listenerPos = _listenerTransform.position;
                }

                var distance = Vector3.Distance(listenerPos, v.Transform.position);
                // Logaritmik azalma maxDistance ötesinde sabit kalır; menzil dışındaki döngüleri sustur.
                var inRange = distance <= v.MaxDistance;
                if (src.mute == inRange)
                    src.mute = !inRange;
                GameAudio.ApplyDistanceFilter(v.LowPass, distance, v.MaxDistance);
            }
        }

        public void BeginFadeOut(AudioPool.Voice v)
        {
            if (v == null || !v.InUse)
                return;

            if (v.Source == null || !v.Source.isPlaying || v.Source.volume <= 0.001f)
            {
                Loops.Release(v);
                return;
            }

            v.HasFollow = false;
            v.Follow = null;
            v.FadeOutRate = Mathf.Max(0.01f, v.Source.volume / LoopFadeSeconds);
        }

        private void OnSceneUnloaded(Scene scene)
        {
            if (Loops == null)
                return;

            // Takip hedefi olmayan döngüler (ör. 2B kalp atışı) başlatıldıkları sahneyle birlikte kapanır;
            // takip edenler hedefleri yok olunca zaten kapanır. Sahne geçişi karesinde (yeni sahnenin Awake'i
            // sırasında etkin sahne henüz eski olabilir) başlatılan döngülere dokunulmaz.
            var recentFrame = Time.frameCount - 1;
            var voices = Loops.Voices;
            for (var i = 0; i < voices.Count; i++)
            {
                var v = voices[i];
                if (v.InUse && !v.HasFollow && v.FadeOutRate <= 0f && v.StartFrame < recentFrame && v.Scene == scene)
                    BeginFadeOut(v);
            }
        }

        // =====================================================================================
        //  ORTAM SESİ (çapraz geçişli 2B döngü)
        // =====================================================================================

        public void SetAmbience(SoundId id, AudioClip clip, float volume)
        {
            if (_ambience == null)
                return;

            volume = Mathf.Clamp01(volume);
            var cur = _ambienceCurrent;
            if (id == SoundId.None || clip == null)
            {
                _ambienceTarget[0] = 0f;
                _ambienceTarget[1] = 0f;
                _requestedAmbience = SoundId.None;
                return;
            }

            _requestedAmbience = id;

            var current = _ambience[cur];
            if (_ambienceId[cur] == id && current != null && current.clip == clip && current.isPlaying)
            {
                _ambienceVolume[cur] = volume;
                _ambienceTarget[cur] = 1f;
                return;
            }

            var next = 1 - cur;
            var src = _ambience[next];
            if (src == null)
                return;

            // Aynı ses henüz kapanırken geri istendiyse kaldığı yerden devam etsin.
            var resume = _ambienceId[next] == id && src.clip == clip && src.isPlaying;
            if (!resume)
            {
                src.Stop();
                src.clip = clip;
                _ambienceLevel[next] = 0f;
                src.volume = 0f;
                if (id != SoundId.MenuMusic && clip.samples > 0)
                    src.timeSamples = Random.Range(0, clip.samples);
                src.Play();
            }

            _ambienceId[next] = id;
            _ambienceVolume[next] = volume;
            _ambienceTarget[next] = 1f;
            _ambienceTarget[cur] = 0f;
            _ambienceCurrent = next;
        }

        private void UpdateAmbience(float dt)
        {
            if (_ambience == null)
                return;

            var step = dt / AmbienceFadeSeconds;
            var ambient = GameAudio.AmbientVolume;
            for (var i = 0; i < 2; i++)
            {
                var src = _ambience[i];
                if (src == null || src.clip == null)
                    continue;

                var level = Mathf.MoveTowards(_ambienceLevel[i], _ambienceTarget[i], step);
                _ambienceLevel[i] = level;
                if (level <= 0f && _ambienceTarget[i] <= 0f)
                {
                    src.Stop();
                    src.clip = null;
                    _ambienceId[i] = SoundId.None;
                    continue;
                }

                // Eşit güçlü geçiş (sin eğrisi) + ortam ses ayarı.
                var gain = Mathf.Sin(level * Mathf.PI * 0.5f);
                var vol = gain * _ambienceVolume[i] * ambient;
                if (Mathf.Abs(src.volume - vol) > 0.0005f)
                    src.volume = vol;
            }
        }

        public void StopAmbienceImmediately()
        {
            if (_ambience == null)
                return;

            for (var i = 0; i < 2; i++)
            {
                if (_ambience[i] != null)
                {
                    _ambience[i].Stop();
                    _ambience[i].clip = null;
                }

                _ambienceLevel[i] = 0f;
                _ambienceTarget[i] = 0f;
                _ambienceId[i] = SoundId.None;
            }

            _requestedAmbience = SoundId.None;
        }
    }
}
