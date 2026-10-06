using System;
using System.Collections.Generic;
using Project.Core.Domain;
using Project.Infrastructure.Audio.HdrMix;
using UnityEngine;

namespace Project.Infrastructure.Audio.Ambience
{
    /// <summary>
    /// Ortam yatakları yöneticisi: biyom + günün saati + hava + iç/dış bağlamından döngü katmanları (rüzgâr esintileri,
    /// çam hışırtısı, böcek, yağmur, dalga) ve rastgele tek seferlik olaylar (kuş, baykuş, köpek, horoz, balta, çan, martı)
    /// ile uzak çatışmayı çalar. Klipler Resources/Audio/Ambience/&lt;biyom&gt;/ altında aranır
    /// (loop_windgust, shot_bird_1.. gibi); yoksa <see cref="AmbienceSynth"/> ile prosedürel üretilir.
    /// </summary>
    public static class AmbienceBeds
    {
        public const string ResourceRoot = "Audio/Ambience/";

        private static AmbienceDirector _director;
        private static AmbienceContext _context = new AmbienceContext { Biome = AmbienceBiome.DagCam, Time = TimeOfDay.Gunduz, Weather = WeatherKind.Acik };

        public static AmbienceContext Context => _context;

        /// <summary>Haritadan biyom seçer ve bağlamı kurar (maç başında bir kez).</summary>
        public static void Configure(string mapId, TimeOfDay time, WeatherKind weather, bool nearVillage = false)
        {
            _context.Biome = AmbienceRules.BiomeForMap(mapId);
            _context.Time = time;
            _context.Weather = weather;
            _context.NearVillage = nearVillage;
            Apply();
        }

        public static void SetTime(TimeOfDay time) { _context.Time = time; Apply(); }
        public static void SetWeather(WeatherKind weather) { _context.Weather = weather; Apply(); }
        public static void SetNearVillage(bool near) { _context.NearVillage = near; Apply(); }

        /// <summary>Üs bölgesi: telsiz parazit fısıltısı yatağını açar/kapatır.</summary>
        public static void SetNearBase(bool near) { _context.NearBase = near; Apply(); }

        /// <summary>Canlı hava: yağış ve rüzgâr şiddeti (0..1). Yalnız anlamlı değişimde planı yeniler.</summary>
        public static void SetLiveWeather(float rain, float wind)
        {
            rain = Mathf.Round(Mathf.Clamp01(rain) * 5f) / 5f;
            wind = Mathf.Round(Mathf.Clamp01(wind) * 5f) / 5f;
            if (Mathf.Approximately(_context.RainLevel, rain) && Mathf.Approximately(_context.WindLevel, wind))
                return;
            _context.RainLevel = rain;
            _context.WindLevel = wind;
            Apply();
        }

        /// <summary>İç mekân durumu: yatakları (çatı yağmuru, boğuk rüzgâr) ve miks anlık görüntüsünü birlikte günceller.</summary>
        public static void SetIndoor(bool indoor)
        {
            if (_context.Indoor == indoor)
                return;
            _context.Indoor = indoor;
            AudioMix.SetIndoor(indoor);
            Apply();
        }

        /// <summary>Uzak çatışmanın sentetik yedeğini açar/kapatır (varsayılan açık).</summary>
        public static void SetSyntheticDistantBattle(bool on)
        {
            Ensure();
            if (_director != null)
                _director.Distant.SyntheticEnabled = on;
        }

        public static void Ensure()
        {
            if (_director != null || !UnityEngine.Application.isPlaying || !GameAudio.Enabled)
                return;
            var go = new GameObject("[AmbienceBeds]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _director = go.AddComponent<AmbienceDirector>();
            go.AddComponent<BaseZoneProbe>();
            _director.SetPlan(AmbienceRules.Build(_context), _context);
            GameAudio.SuppressLegacyAmbience();
        }

        /// <summary>Yatak yöneticisi çalışıyor mu (eski rüzgâr/ortam döngüsü bu durumda susturulur).</summary>
        public static bool IsRunning => _director != null;

        public static void Stop()
        {
            if (_director != null)
                UnityEngine.Object.Destroy(_director.gameObject);
            _director = null;
        }

        private static void Apply()
        {
            Ensure();
            if (_director != null)
                _director.SetPlan(AmbienceRules.Build(_context), _context);
        }

        internal static void Clear() => _director = null;

        internal static AmbienceDirector Director => _director;
    }

    /// <summary>[AmbienceBeds] nesnesi.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class AmbienceDirector : MonoBehaviour
    {
        private const int OneShotVoices = 8;
        private const float LoopFadeSeconds = 2.5f;
        private const float LoopMaxVolume = 0.55f;

        private sealed class LoopSlot
        {
            public AudioSource Source;
            public float Level;
            public float Target;
            public bool ClipTried;
        }

        private sealed class ShotVoice
        {
            public AudioSource Source;
            public AudioLowPassFilter Filter;
        }

        private readonly Dictionary<string, AudioClip> _clipCache = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, AudioClip[]> _resourceCache = new Dictionary<string, AudioClip[]>();
        private LoopSlot[] _loops;
        private ShotVoice[] _voices;
        private int _nextVoice;
        private AmbiencePlan _plan = new AmbiencePlan();
        private AmbienceContext _ctx;
        private bool _ctxSet;
        private bool _swapPending;
        private DistantCannonTimer _cannon;
        private AmbienceOneShotScheduler _scheduler;
        private float _nextListenerSearch;
        private Transform _listener;

        public DistantBattleScheduler Distant { get; private set; }

        private void Awake()
        {
            _scheduler = new AmbienceOneShotScheduler((uint)Environment.TickCount | 1u);
            _cannon = new DistantCannonTimer((uint)(Environment.TickCount * 17) | 1u);
            Distant = new DistantBattleScheduler((uint)(Environment.TickCount * 31) | 1u);

            var loopCount = Enum.GetValues(typeof(AmbienceLoop)).Length;
            _loops = new LoopSlot[loopCount];
            for (var i = 0; i < loopCount; i++)
            {
                var go = new GameObject("Bed_" + (AmbienceLoop)i);
                go.transform.SetParent(transform, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.loop = true;
                s.spatialBlend = 0f;
                s.dopplerLevel = 0f;
                s.priority = 40;
                s.volume = 0f;
                MixerRouting.Route(s, MixChannel.Ortam);
                _loops[i] = new LoopSlot { Source = s };
            }

            _voices = new ShotVoice[OneShotVoices];
            for (var i = 0; i < OneShotVoices; i++)
            {
                var go = new GameObject("Shot_" + i);
                go.transform.SetParent(transform, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.loop = false;
                s.spatialBlend = 1f;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = 20f;
                s.maxDistance = 1800f;
                s.dopplerLevel = 0f;
                s.priority = 90;
                MixerRouting.Route(s, MixChannel.Ortam);
                var f = go.AddComponent<AudioLowPassFilter>();
                f.enabled = false;
                _voices[i] = new ShotVoice { Source = s, Filter = f };
            }
        }

        private void OnDestroy() => AmbienceBeds.Clear();

        internal void SetPlan(AmbiencePlan plan, AmbienceContext ctx)
        {
            var biomeChanged = _ctxSet && ctx.Biome != _ctx.Biome;
            _plan = plan ?? new AmbiencePlan();
            _ctx = ctx;
            _ctxSet = true;
            if (_loops == null || !biomeChanged)
                return;
            // Biyom değişti: eski yatak çapraz geçişle söner, bitince biyoma özel klipler yeniden seçilir.
            _swapPending = true;
        }

        private void SwapBiomeClips()
        {
            _swapPending = false;
            for (var i = 0; i < _loops.Length; i++)
            {
                _loops[i].Source.Stop();
                _loops[i].Source.clip = null;
                _loops[i].ClipTried = false;
                _loops[i].Level = 0f;
            }
        }

        /// <summary>Gerçek maç olayı (EventBus köprüsünden). Mesafe/yön dinleyiciye göre hesaplanır.</summary>
        public static void ReportRealEvent(DistantKind kind, Vector3 position)
        {
            var d = AmbienceBeds.Director;
            if (d == null || !AudioMix.TryListenerPosition(out var l))
                return;
            var delta = position - l;
            var dist = delta.magnitude;
            var angle = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            d.Distant.OnRealEvent(kind, dist, angle, Time.unscaledTime);
        }

        private bool EnsureListener(out Vector3 pos)
        {
            if (_listener == null && Time.unscaledTime >= _nextListenerSearch)
            {
                _nextListenerSearch = Time.unscaledTime + 0.5f;
                var l = FindAnyObjectByType<AudioListener>();
                _listener = l != null ? l.transform : null;
            }

            pos = _listener != null ? _listener.position : Vector3.zero;
            return _listener != null;
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            var now = Time.unscaledTime;
            var gain = GameAudio.AmbientVolume * AudioMix.AmbienceGain * Dialogue.DialogueDirector.DuckGain;

            UpdateLoops(dt, gain);

            if (!EnsureListener(out var lpos))
                return;

            if (_scheduler.TryTick(_plan, now, dt, out var shot))
                PlayAmbientShot(shot, lpos, gain);

            if (!_ctx.Indoor && _cannon.TryTick(now, out var cannon))
                PlayDistant(cannon, lpos, gain);

            if (Distant.TryPop(now, out var far))
                PlayDistant(far, lpos, gain);
        }

        private void UpdateLoops(float dt, float gain)
        {
            var fade = dt / LoopFadeSeconds;
            if (_swapPending)
            {
                var allQuiet = true;
                for (var i = 0; i < _loops.Length; i++)
                    allQuiet &= _loops[i].Level <= 0.01f;
                if (allQuiet)
                    SwapBiomeClips();
            }

            for (var i = 0; i < _loops.Length; i++)
            {
                var slot = _loops[i];
                slot.Target = _swapPending ? 0f : _plan.LoopLevel[i];
                if (slot.Target > 0.001f && slot.Source.clip == null && !slot.ClipTried)
                {
                    slot.ClipTried = true;
                    slot.Source.clip = LoadLoop((AmbienceLoop)i);
                }

                slot.Level = Mathf.MoveTowards(slot.Level, slot.Target, fade);
                var v = slot.Level * LoopMaxVolume * gain;
                slot.Source.volume = v;
                if (v > 0.0005f)
                {
                    if (!slot.Source.isPlaying && slot.Source.clip != null)
                        slot.Source.Play();
                }
                else if (slot.Source.isPlaying)
                {
                    slot.Source.Pause();
                }
            }
        }

        // ------------------------------------------------------------------ klipler

        private AudioClip[] ResourceClips(string folder, string name)
        {
            var key = folder + "/" + name;
            if (_resourceCache.TryGetValue(key, out var found))
                return found;

            var list = new List<AudioClip>(4);
            try
            {
                var single = Resources.Load<AudioClip>(AmbienceBeds.ResourceRoot + key);
                if (single != null)
                    list.Add(single);
                for (var i = 1; i <= 6; i++)
                {
                    var c = Resources.Load<AudioClip>(AmbienceBeds.ResourceRoot + key + "_" + i);
                    if (c != null)
                        list.Add(c);
                }
            }
            catch (Exception)
            {
                // Resources erişilemezse prosedürel yedek kullanılır.
            }

            var arr = list.ToArray();
            _resourceCache[key] = arr;
            return arr;
        }

        private AudioClip LoadLoop(AmbienceLoop loop)
        {
            var folder = AmbienceRules.FolderFor(_ctx.Biome);
            var clips = ResourceClips(folder, AmbienceRules.NameFor(loop));
            if (clips.Length > 0)
                return clips[0];

            var key = "p_loop_" + loop;
            if (_clipCache.TryGetValue(key, out var cached))
                return cached;
            return _clipCache[key] = MakeClip(key, AmbienceSynth.RenderLoop(loop));
        }

        private AudioClip LoadShot(AmbienceOneShot kind, int variant)
        {
            var folder = AmbienceRules.FolderFor(_ctx.Biome);
            var clips = ResourceClips(folder, AmbienceRules.NameFor(kind));
            if (clips.Length > 0)
                return clips[Mathf.Abs(variant) % clips.Length];

            var key = "p_shot_" + kind + "_" + (variant % AmbienceOneShotScheduler.Variants);
            if (_clipCache.TryGetValue(key, out var cached))
                return cached;
            return _clipCache[key] = MakeClip(key, AmbienceSynth.RenderOneShot(kind, variant % AmbienceOneShotScheduler.Variants));
        }

        private static AudioClip MakeClip(string name, float[] samples)
        {
            if (samples == null || samples.Length == 0)
                return null;
            try
            {
                var clip = AudioClip.Create(name, samples.Length, 1, ProceduralAudioSynth.SampleRate, false);
                clip.SetData(samples, 0);
                clip.hideFlags = HideFlags.DontUnloadUnusedAsset;
                return clip;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ------------------------------------------------------------------ tek seferlik

        private ShotVoice Voice()
        {
            for (var k = 0; k < _voices.Length; k++)
            {
                var v = _voices[(_nextVoice + k) % _voices.Length];
                if (!v.Source.isPlaying)
                {
                    _nextVoice = (_nextVoice + k + 1) % _voices.Length;
                    return v;
                }
            }

            return null;
        }

        private static Vector3 PolarOffset(float angleDeg, float distance)
        {
            var a = angleDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(a) * distance, 0f, Mathf.Cos(a) * distance);
        }

        private void PlayAmbientShot(AmbienceShot shot, Vector3 listenerPos, float gain)
        {
            var clip = LoadShot(shot.Kind, shot.Variant);
            var v = Voice();
            if (clip == null || v == null)
                return;
            v.Source.transform.position = listenerPos + PolarOffset(shot.AngleDeg, shot.Distance);
            v.Source.clip = clip;
            v.Source.volume = Mathf.Clamp01(shot.Volume * 0.6f * gain);
            v.Source.pitch = shot.Pitch;
            v.Source.minDistance = 15f;
            v.Source.maxDistance = 400f;
            // Uzaklaştıkça hafif donuklaşma.
            SetFilter(v, Mathf.Lerp(9000f, 3500f, Mathf.Clamp01(shot.Distance / 250f)));
            v.Source.Play();
        }

        private void PlayDistant(DistantEvent e, Vector3 listenerPos, float gain)
        {
            AudioClip clip = null;
            var folder = AmbienceRules.FolderFor(_ctx.Biome);
            var name = e.Kind == DistantKind.Blast ? "shot_distantblast" : "shot_distantgun";
            var res = ResourceClips(folder, name);
            if (res.Length > 0)
                clip = res[UnityEngine.Random.Range(0, res.Length)];
            if (clip == null)
            {
                var id = e.Kind == DistantKind.Blast ? SoundId.Explosion : (e.Distance < 500f ? SoundId.ShotDistantMid : SoundId.ShotDistantFar);
                clip = GameAudio.GetClip(id);
            }

            var v = Voice();
            if (clip == null || v == null)
                return;

            // Ortak 3B konum: dinleyicinin etrafında, yön korunur, ses sürücü mesafe ile yumuşar (gerçek mesafe değil).
            var near = Mathf.Min(e.Distance, 350f);
            v.Source.transform.position = listenerPos + PolarOffset(e.AngleDeg, near);
            v.Source.clip = clip;
            v.Source.volume = Mathf.Clamp01(e.Volume * gain * 1.4f);
            v.Source.pitch = e.Pitch;
            v.Source.minDistance = 120f;
            v.Source.maxDistance = 1800f;
            SetFilter(v, e.LowpassHz);
            v.Source.Play();
        }

        private static void SetFilter(ShotVoice v, float hz)
        {
            v.Filter.cutoffFrequency = Mathf.Clamp(hz, 500f, 22000f);
            v.Filter.enabled = hz < 21000f;
        }
    }
}
