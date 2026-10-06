using System;
using System.Collections.Generic;
using Project.Core.Domain;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Audio.HdrMix;
using Project.Infrastructure.Audio.Weather;
using Project.Infrastructure.Rendering.Features;
using Project.Infrastructure.Vfx;
using Project.Infrastructure.World;
using UnityEngine;

namespace Project.Infrastructure.Rendering
{
    /// <summary>
    /// Maç içi dinamik hava: <see cref="WeatherTimeline"/> (maç tohumundan) Açık → bulutlanma → yağmur → açılma geçişlerini 60-120 sn'de yumuşakça sürer.
    /// Atmosphere.Apply yalnız ayrık tür değişiminde (histerezisli) çağrılır; aradaki sürekli değerler güneş/ambient, ıslaklık, rüzgâr ve GPU yağışına yazılır.
    /// Yağmur başlamadan önce ufukta uzak yağmur perdesi yaklaşır; şimşek = güneş+ambient darbesi, ardından mesafe gecikmeli gök gürültüsü.
    /// Sunucu yetkili: tohum MatchConfig.RandomSeed; MatchConfig.DynamicWeather=false kapatır. Başlangıç havası Açık değilse (Kar/Yağmur) çalışmaz.
    /// </summary>
    public sealed class WeatherSystem : MonoBehaviour
    {
        private const float DarkenSun = 0.5f;

        private WeatherTimeline _timeline;
        private MatchConfig _config;
        private float _clock;
        private float _lastClock;
        private WeatherKind _kind = WeatherKind.Acik;
        private float _baseSun = -1f, _baseAmbient = -1f;
        private float _lastWind = -1f;
        private bool _gpuBound;
        private Light _sun;
        private readonly List<LightningStrike> _scratch = new List<LightningStrike>();
        private readonly List<PendingThunder> _thunder = new List<PendingThunder>();
        private float _flashStart = -10f;
        private int _flashSeed;
        private readonly AudioClip[] _thunderClips = new AudioClip[3];
        private float _flashAzimuth;
        private Transform _curtain;
        private Material _curtainMat;
        private Texture2D _curtainTex;

        private struct PendingThunder { public float At, Volume, Distance; }

        public static WeatherSystem Instance { get; private set; }
        public WeatherTimeline Timeline => _timeline;
        public float Clock => _clock;
        public WeatherSample Current { get; private set; }

        /// <summary>Tek satır kanca (MatchBootstrap). Null-güvenli; kapalıysa/Açık dışı başlangıçta hiçbir şey yapmaz.</summary>
        public static WeatherSystem Attach(MatchConfig config, bool headless)
        {
            try
            {
                if (config == null || !config.DynamicWeather || config.Weather != WeatherKind.Acik)
                    return null;
                if (Instance != null) { Instance.Begin(config); return Instance; }
                var go = new GameObject("[WeatherSystem]");
                var ws = go.AddComponent<WeatherSystem>();
                ws.Begin(config);
                return ws;
            }
            catch (Exception e) { Debug.LogWarning("[WeatherSystem] " + e.Message); return null; }
        }

        private void Begin(MatchConfig config)
        {
            Instance = this;
            _config = config;
            _timeline = new WeatherTimeline(config.RandomSeed, 3600f);
            _clock = 0f; _lastClock = 0f; _kind = WeatherKind.Acik; _baseSun = -1f;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_curtainTex != null) Destroy(_curtainTex);
            if (_curtainMat != null) Destroy(_curtainMat);
        }

        /// <summary>Test/sunucu senkronu: saati ayarla (maç saniyesi).</summary>
        public void SetClock(float seconds) { _clock = seconds; _lastClock = seconds; }

        private void Update()
        {
            if (_timeline == null) return;
            _lastClock = _clock;
            _clock += Time.deltaTime;
            var s = _timeline.Sample(_clock);
            try { Project.Infrastructure.Combat.FireZone.Rain = s.Rain; } catch (Exception) { /* yangın bölgesi isteğe bağlı */ }
            Current = s;

            var kind = WeatherTimeline.KindFor(s.Rain, _kind);
            if (kind != _kind)
            {
                _kind = kind;
                try { Atmosphere.Apply(_config.TimeOfDay, kind, false, _config.MapName); } catch (Exception e) { Debug.LogWarning("[WeatherSystem] " + e.Message); }
                _baseSun = -1f; _baseAmbient = -1f; _gpuBound = false;
            }

            if (_sun == null) _sun = RenderSettings.sun;
            if (_baseSun < 0f && _sun != null) { _baseSun = _sun.intensity; _baseAmbient = RenderSettings.ambientIntensity; }

            DriveContinuous(s);
            DriveLightning();
            DriveCurtain(s);
        }

        private void DriveContinuous(WeatherSample s)
        {
            try
            {
                var wet = Mathf.Lerp(0.2f, 1f, s.Rain);
                ScreenSpaceSettings.Wetness = wet;
                TerrainShaderBinder.SetWetness(wet);
            }
            catch (Exception) { }

            var w = WeatherTimeline.WindStrength(s);
            if (Mathf.Abs(w - _lastWind) > 0.02f)
            {
                _lastWind = w;
                try { WindSystem.SetStrengthOverride(w); } catch (Exception) { }
            }

            var cam = Camera.main;
            if (cam != null)
            {
                try { GpuVfx.SetWeather(GpuVfxEffect.Rain, cam.transform, s.Rain); } catch (Exception) { }
            }
        }

        private void DriveLightning()
        {
            _scratch.Clear();
            _timeline.StrikesBetween(_lastClock, _clock, _scratch);
            for (int i = 0; i < _scratch.Count; i++)
            {
                var st = _scratch[i];
                _flashStart = _clock; _flashAzimuth = st.AzimuthDeg; _flashSeed = (int)(st.AzimuthDeg * 100f) ^ (int)(_clock * 1000f);
                _thunder.Add(new PendingThunder { At = _clock + st.ThunderDelay, Volume = WeatherTimeline.ThunderVolume(st.DistanceM), Distance = st.DistanceM });
            }

            for (int i = _thunder.Count - 1; i >= 0; i--)
            {
                if (_clock < _thunder[i].At) continue;
                try { PlayThunder(_thunder[i].Distance, _thunder[i].Volume); } catch (Exception) { }
                _thunder.RemoveAt(i);
            }

            if (_sun == null || _baseSun < 0f) return;
            var f = LightningFlash.Envelope(_clock - _flashStart, _flashSeed);
            var dark = Mathf.Lerp(1f, DarkenSun, Current.Cloud);
            _sun.intensity = _baseSun * dark + f * 2.5f;
            RenderSettings.ambientIntensity = _baseAmbient * Mathf.Lerp(1f, 0.8f, Current.Cloud) + f * 1.2f;
        }

        // Gök gürültüsü: mesafe kovasına göre prosedürel klip (ThunderSynth), 2B, Ortam kanalı.
        private void PlayThunder(float distance, float volume)
        {
            var bucket = distance < 900f ? 0 : distance < 1900f ? 1 : 2;
            var rep = bucket == 0 ? 600f : bucket == 1 ? 1400f : 2500f;
            var clip = _thunderClips[bucket];
            if (clip == null)
            {
                var data = ThunderSynth.Render(rep, 17 + bucket);
                clip = AudioClip.Create("Thunder" + bucket, data.Length, 1, SynthDsp.SampleRate, false);
                clip.SetData(data, 0);
                _thunderClips[bucket] = clip;
            }
            var go = new GameObject("Thunder");
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.clip = clip; src.spatialBlend = 0f; src.volume = Mathf.Clamp01(0.9f * volume);
            src.pitch = UnityEngine.Random.Range(0.95f, 1.05f);
            MixerRouting.Route(src, MixChannel.Ortam);
            src.Play();
            Destroy(go, clip.length / Mathf.Max(0.5f, src.pitch) + 0.5f);
        }

        // Uzak yağmur perdesi: ufukta fırtına yönünde, yağmur gelmeden yaklaşan dikey çizgili yarı saydam levha.
        private void DriveCurtain(WeatherSample s)
        {
            var cam = Camera.main;
            bool show = cam != null && s.Curtain > 0.01f && s.Rain < 0.6f;
            if (!show)
            {
                if (_curtain != null) _curtain.gameObject.SetActive(false);
                return;
            }

            if (_curtain == null && !BuildCurtain()) return;
            _curtain.gameObject.SetActive(true);
            float dist = Mathf.Lerp(520f, 140f, s.Curtain);
            float az = (_config != null ? _config.RandomSeed : 0) % 360;
            var dir = Quaternion.Euler(0f, az, 0f) * Vector3.forward;
            var pos = cam.transform.position + dir * dist;
            pos.y = cam.transform.position.y + 35f;
            _curtain.position = pos;
            var look = cam.transform.position - pos; look.y = 0f;
            if (look.sqrMagnitude > 0.01f) _curtain.rotation = Quaternion.LookRotation(-look.normalized);
            _curtain.localScale = new Vector3(Mathf.Lerp(700f, 450f, s.Curtain), 150f, 1f);
            var a = Mathf.Clamp01(s.Curtain * 1.2f) * (1f - Mathf.InverseLerp(0.4f, 0.6f, s.Rain)) * 0.55f;
            _curtainMat.color = new Color(0.62f, 0.66f, 0.72f, a);
        }

        private bool BuildCurtain()
        {
            try
            {
                var sh = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
                if (sh == null) return false;
                const int W = 64, H = 64;
                _curtainTex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, name = "RainCurtain" };
                var rng = new System.Random(7);
                var streak = new float[W];
                for (int x = 0; x < W; x++) streak[x] = (float)rng.NextDouble();
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        float v = y / (float)(H - 1); // 0 alt (dolu), 1 üst (şeffaf bulut tabanı)
                        float a = Mathf.SmoothStep(0f, 1f, 1f - Mathf.Abs(v - 0.35f) * 1.5f) * (0.35f + 0.65f * streak[x]);
                        _curtainTex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a)));
                    }
                _curtainTex.Apply();
                _curtainMat = new Material(sh) { name = "RainCurtainMat", mainTexture = _curtainTex, renderQueue = 3100 };
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "RainCurtain";
                var col = quad.GetComponent<Collider>(); if (col != null) Destroy(col);
                var mr = quad.GetComponent<MeshRenderer>();
                mr.sharedMaterial = _curtainMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                _curtain = quad.transform;
                _curtain.SetParent(transform, false);
                return true;
            }
            catch (Exception e) { Debug.LogWarning("[WeatherSystem] Yağmur perdesi: " + e.Message); return false; }
        }
    }
}
