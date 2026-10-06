using Project.Core.Domain;
using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// Küresel rüzgâr: hava durumundan (Açık/Yağmur/Kar) ve kalite kademesinden shader globallerini besler
    /// (_HarekatWind, _HarekatWindParams). Değişimler yumuşak geçer; yön yavaşça salınır.
    /// HAREKAT/Grass ve HAREKAT/VegetationWind shader'ları bu globalleri okur; shader yoksa hiçbir şey bozulmaz.
    /// </summary>
    public static class WindSystem
    {
        private static readonly int WindId = Shader.PropertyToID("_HarekatWind");
        private static readonly int ParamsId = Shader.PropertyToID("_HarekatWindParams");

        private static int _weather;
        private static int _tier = 2;
        private static float _baseAngle = 65f;
        private static WindState _current;
        private static WindState _target;
        private static bool _hasTarget;
        private static float _time;
        private static float _overrideStrength = -1f;
        private static WindDriver _driver;
        private static float _gustBias;

        /// <summary>Etkin (yumuşatılmış) rüzgâr durumu (test/hata ayıklama).</summary>
        public static WindState Current => _current;
        public static int Tier => _tier;
        public static int Weather => _weather;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _weather = 0; _tier = 2; _baseAngle = 65f; _current = default; _target = default;
            _hasTarget = false; _time = 0f; _overrideStrength = -1f; _driver = null; _gustBias = 0f;
        }

        /// <summary>
        /// Esinti dalgası eki (0..1): AmbientLife yavaş hareket eden bantlarla sürer; shader gust'una eklenir (hava gust'u korunur).
        /// </summary>
        public static float GustBias { get => _gustBias; set => _gustBias = Mathf.Clamp01(value); }

        /// <summary>Hava durumunu ayarlar (0 Açık, 1 Yağmur, 2 Kar). Rüzgâr hedefi değişir, geçiş yumuşaktır.</summary>
        public static void SetWeather(WeatherKind weather) => SetWeather((int)weather);

        public static void SetWeather(int weatherIndex)
        {
            _weather = GrassRules.Clamp(weatherIndex, 0, 2);
            Retarget(true);
        }

        /// <summary>Kalite kademesi (0 Düşük: rüzgâr kapalı).</summary>
        public static void SetTier(int tier)
        {
            _tier = GrassRules.Clamp(tier, 0, 3);
            Retarget(false);
        }

        /// <summary>Ana yön (derece, 0 = +Z, saat yönü). Yön bunun etrafında ±25 derece salınır.</summary>
        public static void SetBaseDirection(float degrees)
        {
            _baseAngle = degrees;
            Retarget(false);
        }

        /// <summary>Test/sinematik için gücü sabitler (negatif = hava durumuna dön).</summary>
        public static void SetStrengthOverride(float strength)
        {
            _overrideStrength = strength;
            Retarget(false);
        }

        private static void Retarget(bool snapIfFirst)
        {
            _target = WindRules.ForWeather(_weather, _tier, WindRules.DriftAngle(_baseAngle, _time));
            if (_overrideStrength >= 0f && _tier > 0)
            {
                _target.Strength = _overrideStrength;
                _target.Enabled = _overrideStrength > 0f;
            }

            if (!_hasTarget)
            {
                _current = _target;
                _hasTarget = true;
            }

            EnsureDriver();
            Upload(_current);
        }

        /// <summary>Her karede çağrılır (GrassSystem veya otomatik sürücü). Yumuşak geçiş + yön salınımı.</summary>
        public static void Tick(float dt)
        {
            if (!_hasTarget)
                Retarget(false);
            _time += dt;
            DirOf(out var dx, out var dz);
            _target.DirX = dx; _target.DirZ = dz;
            _current = WindRules.Approach(_current, _target, 0.6f, dt);
            _current.DirX = dx; _current.DirZ = dz;
            Upload(_current);
        }

        private static void DirOf(out float dx, out float dz)
        {
            WindRules.DirFromAngle(WindRules.DriftAngle(_baseAngle, _time), out dx, out dz);
        }

        private static void Upload(WindState s)
        {
            float on = s.Enabled ? 1f : 0f;
            Shader.SetGlobalVector(WindId, new Vector4(s.DirX, 0f, s.DirZ, s.Strength));
            Shader.SetGlobalVector(ParamsId, new Vector4(Mathf.Clamp01(s.Gust + _gustBias * 0.6f), s.Speed, s.Turbulence, on));
        }

        private static void EnsureDriver()
        {
            if (_driver != null || !UnityEngine.Application.isPlaying)
                return;
            var go = new GameObject("HK_WindDriver") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(go);
            _driver = go.AddComponent<WindDriver>();
        }

        private sealed class WindDriver : MonoBehaviour
        {
            private int _lastFrame = -1;

            private void Update()
            {
                if (_lastFrame == Time.frameCount)
                    return;
                _lastFrame = Time.frameCount;
                Tick(Time.deltaTime);
            }
        }
    }
}
