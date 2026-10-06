using System;
using System.Collections.Generic;
using Project.Application.Catalogs;
using Project.Core.Domain;
using Project.Infrastructure.Audio.HdrMix;
using UnityEngine;

namespace Project.Infrastructure.Audio.Foley
{
    /// <summary>
    /// Silah/teçhizat foley giriş noktası (statik). Öncelik: <c>Resources/Audio/Weapons/&lt;weaponId&gt;</c> kaydı,
    /// sonra kalibre sınıfı klasörü, sonra prosedürel sentez (<see cref="FoleySynth"/>). Hiçbir durumda istisna fırlatmaz;
    /// ses kapalıysa/dinleyici yoksa sessizce atlar. Yalnızca ana iş parçacığından çağrılmalıdır.
    /// </summary>
    public static class WeaponFoley
    {
        private static readonly Dictionary<int, AudioClip> ProceduralCache = new Dictionary<int, AudioClip>();
        private static WeaponFoleyHost _host;

        public static bool Enabled => GameAudio.Enabled;

        /// <summary>Silahın ses profili (katalogdan kategori ile yedeklenir; asla null).</summary>
        public static WeaponSoundProfile ProfileFor(string weaponId)
        {
            var cat = WeaponCategory.AssaultRifle;
            if (WeaponCatalog.TryGet(weaponId, out var def) && def != null)
                cat = def.Category;
            return WeaponSoundProfiles.Get(weaponId, cat);
        }

        /// <summary>Tek foley adımı. <paramref name="isLocal"/> true ise 2B (oyuncunun kendi silahı).</summary>
        public static void Play(FoleyStep stepId, string weaponId, Vector3 position, bool isLocal = false, float volumeScale = 1f)
        {
            try
            {
                if (stepId == FoleyStep.None || !Enabled || !(volumeScale > 0.001f))
                    return;
                var host = Host();
                if (host == null)
                    return;

                var info = FoleyStepInfo.For(stepId);
                if (!isLocal && !host.Audible(position, info.MaxDistance))
                    return;

                var profile = ProfileFor(weaponId);
                var clip = ResolveClip(stepId, weaponId, profile);
                if (clip == null)
                    return;

                var pitch = 1f + UnityEngine.Random.Range(-info.PitchJitter, info.PitchJitter);
                host.PlayClip(clip, position, info.Volume * volumeScale * (isLocal ? 1f : 0.85f), pitch, info.MaxDistance, isLocal);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>
        /// Şarjör değiştirme dizisini başlatır; adımlar doldurma süresine göre zamanlanır (şarjör bırak, çıkar, kese,
        /// tak, tokat, boşsa sürgü/kol). <paramref name="source"/> sahibi izler (konum + anahtar); null ise <paramref name="fallbackPosition"/>.
        /// </summary>
        public static void BeginReload(string weaponId, float durationSeconds, bool emptyReload, Transform source,
            bool isLocal, Vector3 fallbackPosition = default)
        {
            try
            {
                var host = Host();
                if (host == null || !Enabled)
                    return;
                var cat = WeaponCategory.AssaultRifle;
                if (WeaponCatalog.TryGet(weaponId, out var def) && def != null)
                    cat = def.Category;
                host.BeginReload(weaponId, ReloadFoleyPlanner.Plan(cat, emptyReload, durationSeconds),
                    durationSeconds, source, isLocal, fallbackPosition);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>Doldurma iptal (silah değişimi, koşu, ölüm): bekleyen adımlar çalınmaz.</summary>
        public static void CancelReload(Transform source)
        {
            if (_host != null)
                _host.CancelReload(source);
        }

        public static void PlayEquip(string weaponId, Vector3 position, bool isLocal) => Play(FoleyStep.WeaponEquip, weaponId, position, isLocal);
        public static void PlayHolster(string weaponId, Vector3 position, bool isLocal) => Play(FoleyStep.WeaponHolster, weaponId, position, isLocal);
        public static void PlaySelector(string weaponId, Vector3 position, bool isLocal) => Play(FoleyStep.SelectorClick, weaponId, position, isLocal);
        public static void PlayDryFire(string weaponId, Vector3 position, bool isLocal) => Play(FoleyStep.DryFire, weaponId, position, isLocal);
        public static void PlayAds(string weaponId, Vector3 position, bool isLocal) => Play(FoleyStep.AdsRustle, weaponId, position, isLocal);

        /// <summary>İniş gövde sesi (düşme hızına göre şiddet; duyulmayacak kadar yavaşsa çalmaz).</summary>
        public static void PlayLanding(float fallSpeed, Vector3 position, bool isLocal)
        {
            if (!BodyFoleyRules.LandingAudible(fallSpeed))
                return;
            Play(FoleyStep.LandBody, null, position, isLocal, BodyFoleyRules.LandingIntensity(fallSpeed));
        }

        public static void PlayProne(Vector3 position, bool isLocal) => Play(FoleyStep.ProneDown, null, position, isLocal);

        /// <summary>
        /// Ateş katmanı için hazır kayıt (varsa). C11 katmanlı atışı bunu önce dener; false ise prosedürel
        /// <c>SoundId</c> yoluna düşer. Silah, ardından kalibre klasörüne bakılır.
        /// </summary>
        public static bool TryGetFireClip(string weaponId, FireLayer layer, out AudioClip clip)
        {
            clip = null;
            try
            {
                var profile = ProfileFor(weaponId);
                return WeaponClipLibrary.TryPick(weaponId, profile.CaliberFolder, FoleyNaming.FireLayerName(layer), out clip);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static AudioClip ResolveClip(FoleyStep step, string weaponId, WeaponSoundProfile profile)
        {
            if (WeaponClipLibrary.TryPick(weaponId, profile.CaliberFolder, FoleyNaming.StepLayer(step), out var clip))
                return clip;

            var variant = UnityEngine.Random.Range(0, FoleySynth.VariantCount);
            var key = ((int)step << 12) | ((int)profile.Caliber << 4) | variant;
            if (ProceduralCache.TryGetValue(key, out var cached) && cached != null)
                return cached;
            cached = FoleySynth.ToClip(step, profile.Caliber, variant);
            ProceduralCache[key] = cached;
            return cached;
        }

        private static WeaponFoleyHost Host()
        {
            if (_host != null)
                return _host;
            if (!UnityEngine.Application.isPlaying)
                return null;
            var go = new GameObject("[WeaponFoley]") { hideFlags = HideFlags.DontSave };
            UnityEngine.Object.DontDestroyOnLoad(go);
            _host = go.AddComponent<WeaponFoleyHost>();
            return _host;
        }
    }

    /// <summary>Foley kaynak havuzu + doldurma zamanlayıcısı (dahili; GameAudio havuzundan bağımsız, AudioListener.volume uygulanır).</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    internal sealed class WeaponFoleyHost : MonoBehaviour
    {
        private const int VoiceCount = 14;
        private const int MaxSessions = 16;

        private sealed class Session
        {
            public readonly ReloadFoleyTracker Tracker = new ReloadFoleyTracker();
            public string WeaponId;
            public float Elapsed;
            public float Duration;
            public Transform Source;
            public Vector3 Position;
            public bool IsLocal;
            public int Key;
        }

        private AudioSource[] _voices;
        private float[] _startTime;
        private readonly List<Session> _sessions = new List<Session>(MaxSessions);
        private readonly List<FoleyStep> _due = new List<FoleyStep>(4);
        private Transform _listener;
        private float _nextListenerSearch;

        private void Awake()
        {
            _voices = new AudioSource[VoiceCount];
            _startTime = new float[VoiceCount];
            for (var i = 0; i < VoiceCount; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.dopplerLevel = 0f;
                s.rolloffMode = AudioRolloffMode.Logarithmic;
                MixerRouting.Route(s, MixChannel.Silah); // silah elleme sesleri: gerçek mikser Silah grubu (mixer yoksa no-op)
                _voices[i] = s;
            }
        }

        public bool Audible(Vector3 position, float maxDistance)
        {
            var l = Listener();
            if (l == null)
                return false;
            return (l.position - position).sqrMagnitude <= maxDistance * maxDistance;
        }

        private Transform Listener()
        {
            if (_listener != null)
                return _listener;
            if (Time.unscaledTime < _nextListenerSearch)
                return null;
            _nextListenerSearch = Time.unscaledTime + 0.5f;
            var al = UnityEngine.Object.FindFirstObjectByType<AudioListener>();
            _listener = al != null ? al.transform : null;
            return _listener;
        }

        public void PlayClip(AudioClip clip, Vector3 position, float volume, float pitch, float maxDistance, bool local)
        {
            var best = -1;
            var oldest = float.MaxValue;
            for (var i = 0; i < _voices.Length; i++)
            {
                if (!_voices[i].isPlaying)
                {
                    best = i;
                    break;
                }

                if (_startTime[i] < oldest)
                {
                    oldest = _startTime[i];
                    best = i;
                }
            }

            if (best < 0)
                return;
            var s = _voices[best];
            s.Stop();
            s.clip = clip;
            s.volume = Mathf.Clamp01(volume);
            s.pitch = pitch;
            s.spatialBlend = local ? 0f : 1f;
            s.minDistance = 1.5f;
            s.maxDistance = maxDistance;
            if (!local)
                s.transform.position = position;
            _startTime[best] = Time.unscaledTime;
            s.Play();
        }

        public void BeginReload(string weaponId, ReloadStepPlan[] plan, float duration, Transform source, bool isLocal, Vector3 fallback)
        {
            var key = source != null ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(source) : 0;
            CancelKey(key);
            if (_sessions.Count >= MaxSessions)
                _sessions.RemoveAt(0);
            var sess = new Session
            {
                WeaponId = weaponId, Elapsed = 0f, Duration = duration > 0.2f ? duration : 2f, Source = source,
                Position = fallback, IsLocal = isLocal, Key = key
            };
            sess.Tracker.Begin(plan);
            _sessions.Add(sess);
        }

        public void CancelReload(Transform source) => CancelKey(source != null ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(source) : 0);

        private void CancelKey(int key)
        {
            for (var i = _sessions.Count - 1; i >= 0; i--)
                if (_sessions[i].Key == key)
                    _sessions.RemoveAt(i);
        }

        private void Update()
        {
            for (var i = _sessions.Count - 1; i >= 0; i--)
            {
                var s = _sessions[i];
                if (s.Source == null && s.Key != 0)
                {
                    _sessions.RemoveAt(i);
                    continue;
                }

                s.Elapsed += Time.deltaTime;
                _due.Clear();
                s.Tracker.Advance(s.Elapsed / s.Duration, _due);
                var pos = s.Source != null ? s.Source.position : s.Position;
                for (var k = 0; k < _due.Count; k++)
                    WeaponFoley.Play(_due[k], s.WeaponId, pos, s.IsLocal);
                if (!s.Tracker.Active)
                    _sessions.RemoveAt(i);
            }
        }
    }
}
