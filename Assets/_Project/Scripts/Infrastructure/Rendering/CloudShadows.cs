using System;
using System.Reflection;
using Project.Core.Domain;
using Project.Infrastructure.World;
using UnityEngine;

namespace Project.Infrastructure.Rendering
{
    /// <summary>
    /// Bulut gölgeleri: ana yön ışığına URP ışık çerezi (cookie) olarak döngüsel prosedürel bulut dokusu verilir, WindSystem yönünde kaydırılır.
    /// Çerez boyutu ~800 m; hava başına kapsama/derinlik; Düşük kademede ve gece kapalı. SkyEnvironment.Configure tek satırla <see cref="Configure"/> çağırır;
    /// gizli bir sürücü kaydırmayı yürütür ve kademeyi yavaşça yoklar (QualityTierApplier'a ENTEGRASYON gerekmeden çalışır).
    /// UniversalAdditionalLightData (lightCookieSize/Offset) yansıma ile ayarlanır; tip bulunamazsa çerez boyutsuz uygulanmaz (özellik kapanır).
    /// </summary>
    public static class CloudShadows
    {
        private static TimeOfDay _time = TimeOfDay.Gunduz;
        private static WeatherKind _weather = WeatherKind.Acik;
        private static int _tier = -1;
        private static bool _wanted;
        private static Light _light;
        private static Texture2D _tex;
        private static Component _lightData;
        private static Vector2 _offset;
        private static Driver _driver;
        private static CloudShadowTierConfig _cfg;
        private static bool _reflectionTried;
        private static PropertyInfo _sizeProp, _offsetProp;
        private static Type _lightDataType;

        public static bool Active => _tex != null && _light != null && _light.cookie == _tex;
        public static Vector2 Offset => _offset;
        public static Texture2D Texture => _tex;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _time = TimeOfDay.Gunduz; _weather = WeatherKind.Acik; _tier = -1; _wanted = false; _light = null; _tex = null;
            _lightData = null; _offset = Vector2.zero; _driver = null; _cfg = default; _reflectionTried = false;
            _sizeProp = null; _offsetProp = null; _lightDataType = null;
        }

        /// <summary>Saat/hava/kademe değişimi: dokuyu yeniden üretir ve çerezi ana ışığa bağlar (veya kaldırır).</summary>
        public static void Configure(TimeOfDay time, WeatherKind weather, int tier)
        {
            _time = time;
            _weather = weather;
            _tier = tier;
            Rebuild();
        }

        private static void Rebuild()
        {
            try
            {
                EnsureDriver();
                _cfg = CloudShadowsMath.ForTier(_tier);
                _wanted = _cfg.Enabled && CloudShadowsMath.IsActive(_time, _tier);
                if (!_wanted)
                {
                    Detach();
                    return;
                }

                _light = ResolveSun();
                if (_light == null || !EnsureReflection())
                {
                    Detach();
                    return;
                }

                if (_tex != null) UnityEngine.Object.Destroy(_tex);
                _tex = BuildTexture(_cfg.TextureSize, _cfg.Octaves, CloudShadowsMath.Coverage(_time, _weather), CloudShadowsMath.Darkness(_time, _weather));
                _light.cookie = _tex;
                _lightData = _light.GetComponent(_lightDataType);
                if (_lightData == null)
                    _lightData = _light.gameObject.AddComponent(_lightDataType);
                _sizeProp.SetValue(_lightData, new Vector2(_cfg.CookieSize, _cfg.CookieSize));
                _offsetProp.SetValue(_lightData, _offset);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[CloudShadows] uygulanamadı: " + e.Message);
                Detach();
            }
        }

        private static Light ResolveSun()
        {
            var sun = RenderSettings.sun;
            if (sun != null && sun.type == LightType.Directional)
                return sun;
            var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (var i = 0; i < lights.Length; i++)
                if (lights[i] != null && lights[i].type == LightType.Directional)
                    return lights[i];
            return null;
        }

        private static bool EnsureReflection()
        {
            if (_sizeProp != null && _offsetProp != null)
                return true;
            if (_reflectionTried)
                return false;
            _reflectionTried = true;
            _lightDataType = Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalLightData, Unity.RenderPipelines.Universal.Runtime");
            if (_lightDataType == null)
            {
                Debug.LogWarning("[CloudShadows] UniversalAdditionalLightData bulunamadı (URP yok?); bulut gölgesi kapalı.");
                return false;
            }

            _sizeProp = _lightDataType.GetProperty("lightCookieSize", BindingFlags.Public | BindingFlags.Instance);
            _offsetProp = _lightDataType.GetProperty("lightCookieOffset", BindingFlags.Public | BindingFlags.Instance);
            if (_sizeProp == null || _offsetProp == null)
            {
                Debug.LogWarning("[CloudShadows] lightCookieSize/Offset özelliği yok; bulut gölgesi kapalı.");
                _sizeProp = null; _offsetProp = null;
                return false;
            }

            return true;
        }

        private static void Detach()
        {
            try
            {
                if (_light != null && _tex != null && _light.cookie == _tex)
                    _light.cookie = null;
            }
            catch (Exception)
            {
            }

            if (_tex != null)
            {
                UnityEngine.Object.Destroy(_tex);
                _tex = null;
            }

            _lightData = null;
        }

        /// <summary>Döngüsel çerez dokusu: 1 = ışık, düşük = bulut gölgesi.</summary>
        public static Texture2D BuildTexture(int size, int octaves, float coverage, float darkness)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "HK_CloudShadowCookie",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 1
            };
            var px = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var d = SkyWaterRules.TileableFbm(x / (float)size, y / (float)size, 3, octaves, 211);
                var v = CloudShadowsMath.CookieValue(d, coverage, darkness);
                var b = (byte)Mathf.RoundToInt(Mathf.Clamp01(v) * 255f);
                px[y * size + x] = new Color32(b, b, b, b);
            }

            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        private static void EnsureDriver()
        {
            if (_driver != null || !UnityEngine.Application.isPlaying)
                return;
            var go = new GameObject("HK_CloudShadowDriver") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(go);
            _driver = go.AddComponent<Driver>();
        }

        private static void Tick(float dt)
        {
            if (_tex == null || _light == null || _lightData == null)
                return;
            var wind = WindSystem.Current;
            float dx = wind.DirX, dz = wind.DirZ;
            if (dx * dx + dz * dz < 1e-4f)
            {
                dx = 0.57f; dz = 0.82f; // rüzgâr kapalıyken hafif sabit sürüklenme
            }

            var t = _light.transform;
            _offset = CloudShadowsMath.AdvanceOffset(_offset, dx, dz, CloudShadowsMath.SpeedMps(wind.Enabled ? wind.Strength : 0.3f), dt, t.right, t.up, _cfg.CookieSize);
            _offsetProp.SetValue(_lightData, _offset);
        }

        private sealed class Driver : MonoBehaviour
        {
            private float _poll;

            private void Update()
            {
                _poll += Time.unscaledDeltaTime;
                if (_poll >= 1f)
                {
                    _poll = 0f;
                    var q = Atmosphere.QualityLevel;
                    var sunChanged = _wanted && (_light == null || RenderSettings.sun != _light && RenderSettings.sun != null);
                    if (q != _tier || sunChanged)
                        Configure(_time, _weather, q);
                }

                Tick(Time.deltaTime);
            }
        }
    }
}
