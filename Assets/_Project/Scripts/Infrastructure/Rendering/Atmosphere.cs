using System;
using Project.Infrastructure.Content;
using Project.Infrastructure.Rendering.Features;
using Project.Infrastructure.Vfx;
using Project.Infrastructure.World;
using Project.Core.Domain;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Project.Infrastructure.Rendering
{
    /// <summary>
    /// Günün saati + hava durumu uygulayıcısı. MatchBootstrap.Start tek satırla Atmosphere.Apply çağırır.
    /// Gece: sınırlı görüş (yoğun sis), güçlü namlu alevi, L ile el feneri. Yağmur/kar: kameraya bağlı parçacık.
    /// </summary>
    public sealed class Atmosphere : MonoBehaviour
    {
        private static readonly int SkyTintId = Shader.PropertyToID("_SkyTint");
        private static readonly int GroundColorId = Shader.PropertyToID("_GroundColor");
        private static readonly int ExposureId = Shader.PropertyToID("_Exposure");

        public static TimeOfDay CurrentTime { get; private set; } = TimeOfDay.Gunduz;
        public static WeatherKind CurrentWeather { get; private set; } = WeatherKind.Acik;
        public static string CurrentMapId { get; private set; } = MapCatalog.Kuzgun;

        /// <summary>GameVfx namlu alevi ölçeği çarpanı.</summary>
        public static float MuzzleFlashBoost => AtmosphereRules.MuzzleFlashBoost(CurrentTime);

        private static float _baseFogDensity;
        private static float _lastFogHeight = float.NaN;
        private static Material _proceduralSky;
        private static Material _hdriSky;
        private static int _qualityLevel = -1;
        private static bool _volumetricUser = true;
        private static readonly int AerialId = Shader.PropertyToID("_HK_AerialDensity");
        private static readonly int ShadeFillId = Shader.PropertyToID("_HK_ShadeFill");

        /// <summary>HDRI SkyOverride etkin mi (prosedürel bulut/güneş katmanları gizlenir).</summary>
        public static bool SkyOverrideActive { get; private set; }

        /// <summary>Hacimsel ışık entegrasyon noktası: (etkin mi) bildirir; Yüksek/Ultra ve kullanıcı açıksa true.</summary>
        public static event Action<bool> VolumetricChanged;
        public static bool VolumetricEnabled { get; private set; }

        /// <summary>Grafik kalite kademesi (0-3). Ayarlanmazsa QualitySettings'ten türetilir.</summary>
        public static int QualityLevel
        {
            get => _qualityLevel >= 0 ? _qualityLevel : PerformanceProfile.Clamp(QualitySettings.GetQualityLevel());
            set { _qualityLevel = PerformanceProfile.Clamp(value); RefreshVolumetric(); }
        }

        /// <summary>Hacimsel ışığı kullanıcı tercihiyle aç/kapat.</summary>
        public static void SetVolumetricUser(bool enabled) { _volumetricUser = enabled; RefreshVolumetric(); }

        private static void RefreshVolumetric()
        {
            var on = AtmosphereMath.VolumetricAllowed(QualityLevel, _volumetricUser);
            if (on == VolumetricEnabled) return;
            VolumetricEnabled = on;
            try { VolumetricChanged?.Invoke(on); } catch (Exception e) { Debug.LogException(e); }
        }

        private static float _lastFogScale = 1f;
        private static float _rain01;
        private static bool _gpuWeatherBound;

        private static float FogScale()
        {
            try { return VolumetricFog.BuiltInFogScale; } catch (Exception) { return 1f; }
        }

        /// <summary>Taban sis yoğunluğunu yükseklik + hacimsel sis çarpanıyla yazar (çifte sis olmasın).</summary>
        private static void ApplyFogScaled(float camY)
        {
            if (_baseFogDensity <= 0f)
                return;
            if (!float.IsNaN(camY)) _lastFogHeight = camY;
            _lastFogScale = FogScale();
            var h = float.IsNaN(_lastFogHeight) ? 1f : AtmosphereMath.HeightFogFactor(_lastFogHeight, 0f, 0.008f);
            var camForLayers = float.IsNaN(_lastFogHeight) ? 50f : _lastFogHeight;
            var valley = AtmosphereMath.ValleyFogMultiplier(camForLayers, CurrentTime, CurrentWeather); // sabah sis gölü
            RenderSettings.fogDensity = _baseFogDensity * h * _lastFogScale * valley;
            Shader.SetGlobalFloat(AerialId, AtmosphereMath.AerialDensity(CurrentTime, CurrentWeather) * AtmosphereMath.RidgeHazeMultiplier(camForLayers)); // 600 m+ sırt haze
        }

        /// <summary>Kalite/hacimsel sis durumu değişince sis yoğunluğunu yeniden yazar (QualityTierApplier çağırır).</summary>
        public static void RefreshFog()
        {
            if (_baseFogDensity > 0f)
                ApplyFogScaled(_lastFogHeight);
        }

        /// <summary>Saat/hava değişimini hacimsel sis, rüzgâr, GPU VFX ve ıslaklık sistemlerine iletir (hatalar yutulur).</summary>
        private static void ApplyWeatherSystems(TimeOfDay time, WeatherKind weather)
        {
            _rain01 = weather == WeatherKind.Yagmur ? 1f : weather == WeatherKind.Kar ? 0.3f : 0f;
            var wet = Mathf.Lerp(0.2f, 1f, _rain01); // 0,2 kuru taban
            try { VolumetricFog.ApplyPreset(time, weather, 4f); } catch (Exception e) { Debug.LogWarning("[Atmosphere] VolumetricFog: " + e.Message); }
            try { WindSystem.SetWeather(weather); } catch (Exception e) { Debug.LogWarning("[Atmosphere] Wind: " + e.Message); }
            try { ScreenSpaceSettings.Wetness = wet; } catch (Exception e) { Debug.LogWarning("[Atmosphere] Wetness: " + e.Message); }
            try { ScreenSpaceSettings.Daylight01 = time == TimeOfDay.Gunduz ? 1f : time == TimeOfDay.Gece ? 0f : 0.5f; } catch (Exception e) { Debug.LogWarning("[Atmosphere] Daylight: " + e.Message); }
            try { TerrainShaderBinder.SetWetness(wet); } catch (Exception e) { Debug.LogWarning("[Atmosphere] TerrainWetness: " + e.Message); }
            _gpuWeatherBound = false; // Update'te kameraya bağlanır
            try { GpuVfx.SetWeather(GpuVfxEffect.Rain, null, 0f); GpuVfx.SetWeather(GpuVfxEffect.Snow, null, 0f); } catch (Exception) { }
        }

        private static void BindGpuWeather(Camera cam)
        {
            if (_gpuWeatherBound || cam == null)
                return;
            _gpuWeatherBound = true;
            try
            {
                var weather = CurrentWeather;
                GpuVfx.SetWeather(GpuVfxEffect.Rain, cam.transform, weather == WeatherKind.Yagmur ? 1f : 0f);
                GpuVfx.SetWeather(GpuVfxEffect.Snow, cam.transform, weather == WeatherKind.Kar ? 1f : 0f);
            }
            catch (Exception) { }
        }

        private Light _flashlight;
        private ParticleSystem _precip;
        private bool _flashOn;

        /// <summary>El feneri açık mı (gece görüş bloom'u için).</summary>
        public static bool FlashlightOn { get; private set; }

        /// <summary>Bir tek satır kanca: yapılandırmadaki saat/hava atmosfere uygulanır. Hatalar yutulur.</summary>
        public static void Apply(TimeOfDay time, WeatherKind weather, bool headless = false, string mapId = null)
        {
            CurrentTime = time;
            CurrentWeather = weather;
            CurrentMapId = string.IsNullOrEmpty(mapId) ? MapCatalog.Kuzgun : MapCatalog.Normalize(mapId);
            if (headless)
                return;
            try
            {
                ApplyEnvironment(time, weather, CurrentMapId);
                var host = FindAnyObjectByType<Atmosphere>();
                if (host == null)
                    host = new GameObject("[Atmosphere]").AddComponent<Atmosphere>();
                host.Configure(time, weather);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public static void Apply(MatchConfig config, bool headless = false)
        {
            if (config != null)
                Apply(config.TimeOfDay, config.Weather, headless, config.MapName);
        }

        private static void ApplyEnvironment(TimeOfDay time, WeatherKind weather, string mapId)
        {
            var p = AtmospherePreset.For(time);
            float wet = weather == WeatherKind.Acik ? 0f : 1f;
            Color grey = new Color(0.46f, 0.52f, 0.56f);
            float dim = time == TimeOfDay.Gece ? 0.9f : 0.15f;

            RenderSettings.ambientSkyColor = Color.Lerp(p.AmbientSky, p.AmbientSky * 0.85f, wet);
            RenderSettings.ambientEquatorColor = p.AmbientEquator;
            RenderSettings.ambientGroundColor = p.AmbientGround;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Color.Lerp(p.FogColor, time == TimeOfDay.Gece ? p.FogColor : grey * (time == TimeOfDay.Gunduz ? 1f : 0.6f), wet * 0.5f);
            RenderSettings.fogDensity = p.FogDensity * AtmosphereRules.FogMultiplier(time, weather) * AtmosphereRules.MapFogMultiplier(mapId, time);
            if (AtmosphereRules.HasSeaFog(mapId))
            {
                // Deniz sisi: mavimsi-gri, gece daha koyu.
                var sea = new Color(0.62f, 0.72f, 0.8f) * (time == TimeOfDay.Gece ? 0.25f : time == TimeOfDay.Gunduz ? 1f : 0.7f);
                sea.a = 1f;
                RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, sea, 0.55f);
            }

            _baseFogDensity = RenderSettings.fogDensity;
            _lastFogHeight = float.NaN;
            ApplyWeatherSystems(time, weather);
            ApplyFogScaled(float.NaN);
            // Aerial perspective: uzak sis rengi ufuk gökyüzü tonuna hafifçe kayar (yakın görünüm değişmez).
            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, p.SkyTint, time == TimeOfDay.Gunduz && weather == WeatherKind.Acik ? 0.18f : 0.08f);
            var tint = AtmosphereMath.AerialTint(time); // saat bazlı aerial tint (GD değerlerinin üstüne)
            var tc = Color.Lerp(RenderSettings.fogColor, new Color(tint[0], tint[1], tint[2], 1f), AtmosphereMath.AerialTintStrength(time, weather));
            tc.a = 1f;
            RenderSettings.fogColor = tc;
            Shader.SetGlobalFloat(AerialId, AtmosphereMath.AerialDensity(time, weather));
            RefreshVolumetric();

            var sun = RenderSettings.sun != null ? RenderSettings.sun : RenderSettingsUtil.EnsureSun(false);
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(p.SunEuler);
                var gr = MapGradeTable.For(mapId);
                var tintMul = time == TimeOfDay.Gunduz ? 1f : 0.5f; // gün batımı/gece renkleri korunur
                sun.color = p.SunColor * new Color(Mathf.Lerp(1f, gr.SunTint[0], tintMul), Mathf.Lerp(1f, gr.SunTint[1], tintMul), Mathf.Lerp(1f, gr.SunTint[2], tintMul), 1f);
                sun.intensity = p.SunIntensity * gr.SunIntensityMul * Mathf.Lerp(1f, dim, wet);
                ApplyPhysicalSun(sun, time, weather, p);
                sun.shadows = LightShadows.Soft;
                RenderSettings.sun = sun;
            }

            var sky = RenderSettings.skybox;
            if (sky != null)
            {
                if (sky.HasProperty(SkyTintId)) sky.SetColor(SkyTintId, Color.Lerp(p.SkyTint, grey * 0.6f, wet * 0.5f));
                if (sky.HasProperty(GroundColorId)) sky.SetColor(GroundColorId, p.GroundColor);
                if (sky.HasProperty(ExposureId)) sky.SetFloat(ExposureId, p.SkyExposure * Mathf.Lerp(1f, 0.85f, wet));
            }

            ApplySkyOverride(time, weather);
            ApplyPhysicalAmbient(time, weather, p);

            PostProcessing.ApplyMapGrade(mapId);
            // Pozlama artık PostProcessing derecelendirme presetinde (saat + harita, 0,5 sn blend).

            SkyEnvironment.Apply(time, weather, mapId, p);
            CameraRig.RefreshSkyClear();
            try { DynamicGI.UpdateEnvironment(); } catch (Exception) { }
        }

        /// <summary>Güneşin ufuk üstü yüksekliği (derece). Gece (ay) için sabit 25° döner: ay renk/yoğunluk eğrisine girmez.</summary>
        private static float SunElevation(TimeOfDay time, AtmospherePreset p)
        {
            return time == TimeOfDay.Gece ? 25f : Mathf.Clamp(p.SunEuler.x, 0f, 90f);
        }

        /// <summary>Fiziksel güneş: yüksekliğe göre Kelvin tonu (2000 K ufuk -> 5800 K öğlen, ön ayara ağırlıklı karışım), hava mesafesi sönümü ve hava durumuna göre gölge gücü.</summary>
        private static void ApplyPhysicalSun(Light sun, TimeOfDay time, WeatherKind weather, AtmospherePreset p)
        {
            try
            {
                var elev = SunElevation(time, p);
                var weight = time == TimeOfDay.Gunduz ? 0.4f : time == TimeOfDay.Gece ? 0f : 0.35f;
                var c = sun.color;
                var rgb = LightingMath.BlendSunColor(c.r, c.g, c.b, elev, weight);
                sun.color = new Color(rgb[0], rgb[1], rgb[2], 1f);
                if (time != TimeOfDay.Gece)
                {
                    var ratio = LightingMath.SunIntensityFactor(elev) / Mathf.Max(0.05f, LightingMath.SunIntensityFactor(50f));
                    sun.intensity *= Mathf.Lerp(1f, Mathf.Clamp(ratio, 0.3f, 1.1f), time == TimeOfDay.Gunduz ? 0.25f : 0.5f);
                }
                sun.shadowStrength = LightingMath.ShadowStrength(weather, elev);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Atmosphere] Fiziksel güneş uygulanamadı: " + e.Message);
            }
        }

        /// <summary>Gökyüzünden ortam: hava/yüksekliğe göre ölçek + gölge dolgusu (ekvator/zemin ortamı gökyüzüne doğru kalkar; iç mekân gölgesi probe ile uyumlu kalır). HDRI varsa SH yoğunluğu ölçeklenir.</summary>
        private static void ApplyPhysicalAmbient(TimeOfDay time, WeatherKind weather, AtmospherePreset p)
        {
            try
            {
                var elev = SunElevation(time, p);
                var scale = LightingMath.AmbientScale(elev, weather);
                var fill = time == TimeOfDay.Gece ? 0.02f : LightingMath.ShadeFill(elev, weather);
                Shader.SetGlobalFloat(ShadeFillId, fill);

                if (SkyOverrideActive)
                {
                    RenderSettings.ambientIntensity = scale * (1f + fill * 0.5f);
                    return;
                }

                RenderSettings.ambientIntensity = 1f;
                var sky = RenderSettings.ambientSkyColor * scale;
                var eq = RenderSettings.ambientEquatorColor * scale;
                var gr = RenderSettings.ambientGroundColor * scale;
                eq = Color.Lerp(eq, sky, fill * 0.5f);
                gr = Color.Lerp(gr, eq, fill * 0.4f);
                sky.a = eq.a = gr.a = 1f;
                RenderSettings.ambientSkyColor = sky;
                RenderSettings.ambientEquatorColor = eq;
                RenderSettings.ambientGroundColor = gr;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Atmosphere] Fiziksel ortam uygulanamadı: " + e.Message);
            }
        }

        /// <summary>ContentOverrides'ta HDRI varsa skybox + ortam ışığı ondan alınır; yoksa prosedürel gökyüzü geri yüklenir.</summary>
        private static void ApplySkyOverride(TimeOfDay time, WeatherKind weather)
        {
            try
            {
                if (_proceduralSky == null && !SkyOverrideActive)
                    _proceduralSky = RenderSettings.skybox;
                Cubemap hdri = null;
                float exposure = 1f;
                var found = ContentOverrides.TryGetSky(AtmosphereMath.SkyId(time, weather), out hdri, out exposure) && hdri != null;
                if (found)
                {
                    var shader = Shader.Find("Skybox/Cubemap");
                    if (shader == null)
                    {
                        Debug.LogWarning("[Atmosphere] Skybox/Cubemap shader bulunamadı; HDRI atlandı.");
                        found = false;
                    }
                    else
                    {
                        if (_hdriSky == null) _hdriSky = new Material(shader) { name = "HK_HdriSky" };
                        _hdriSky.SetTexture("_Tex", hdri);
                        _hdriSky.SetFloat(ExposureId, exposure);
                        RenderSettings.skybox = _hdriSky;
                        RenderSettings.ambientMode = AmbientMode.Skybox;
                        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
                    }
                }
                if (!found && SkyOverrideActive && _proceduralSky != null)
                    RenderSettings.skybox = _proceduralSky;
                SkyOverrideActive = found;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Atmosphere] SkyOverride uygulanamadı: " + e.Message);
                SkyOverrideActive = false;
            }
        }

        private void Configure(TimeOfDay time, WeatherKind weather)
        {
            if (_precip != null) Destroy(_precip.gameObject);
            var gpuWeather = false;
            try { gpuWeather = GpuVfx.Available && GpuVfxBudget.Enabled(GpuVfx.Tier); } catch (Exception) { }
            _precip = weather == WeatherKind.Acik || gpuWeather ? null : BuildPrecipitation(weather); // GPU yağış varsa CPU parçacığı atlanır
            if (_flashlight == null)
            {
                var go = new GameObject("Flashlight");
                go.transform.SetParent(transform, false);
                _flashlight = go.AddComponent<Light>();
                _flashlight.type = LightType.Spot;
                _flashlight.range = 45f;
                _flashlight.spotAngle = 55f;
                _flashlight.intensity = 6.0f;
                _flashlight.color = new Color(1f, 0.95f, 0.82f);
                _flashlight.shadows = LightShadows.None;
            }
            _flashOn = false;
            _flashlight.enabled = false;
        }

        private static Camera PlayerCamera()
        {
            var rigs = CameraRig.Active;
            if (rigs != null && rigs.Count > 0 && rigs[0] != null && rigs[0].WorldCamera != null)
                return rigs[0].WorldCamera;
            return Camera.main;
        }

        private void Update()
        {
            var cam = PlayerCamera();
            if (cam == null)
                return;

            var kb = Keyboard.current;
            if (kb != null && kb[Key.L].wasPressedThisFrame && _flashlight != null)
            {
                _flashOn = !_flashOn;
                _flashlight.enabled = _flashOn;
                FlashlightOn = _flashOn;
            }

            if (_flashlight != null && _flashOn)
                _flashlight.transform.SetPositionAndRotation(cam.transform.position + cam.transform.right * 0.12f - cam.transform.up * 0.1f, cam.transform.rotation);

            var camY = cam.transform.position.y;
            if (_baseFogDensity > 0f && (float.IsNaN(_lastFogHeight) || Mathf.Abs(camY - _lastFogHeight) > 1f))
            {
                _lastFogHeight = camY;
                ApplyFogScaled(camY);
            }
            else if (_baseFogDensity > 0f && !Mathf.Approximately(_lastFogScale, FogScale()))
                ApplyFogScaled(_lastFogHeight);

            BindGpuWeather(cam);

            if (_precip != null)
                _precip.transform.position = cam.transform.position + Vector3.up * 14f;
        }

        private static ParticleSystem BuildPrecipitation(WeatherKind weather)
        {
            bool snow = weather == WeatherKind.Kar;
            var go = new GameObject(snow ? "Snow" : "Rain");
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = snow ? 2500 : 3000;
            main.startLifetime = snow ? 7f : 1.2f;
            main.startSpeed = 0f;
            main.gravityModifier = snow ? 0.07f : 3.2f;
            main.startSize = snow ? new ParticleSystem.MinMaxCurve(0.05f, 0.12f) : new ParticleSystem.MinMaxCurve(0.02f, 0.04f);
            main.startColor = snow ? new Color(1f, 1f, 1f, 0.85f) : new Color(0.75f, 0.82f, 0.9f, 0.45f);

            var em = ps.emission;
            em.rateOverTime = snow ? 600f : 900f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(40f, 1f, 40f);

            if (snow)
            {
                var noise = ps.noise;
                noise.enabled = true;
                noise.strength = 0.6f;
                noise.frequency = 0.4f;
            }

            var r = go.GetComponent<ParticleSystemRenderer>();
            if (!snow)
            {
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.lengthScale = 0f;
                r.velocityScale = 0.04f;
                r.cameraVelocityScale = 0f;
            }
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            if (sh != null)
                r.sharedMaterial = new Material(sh) { name = "WeatherParticle" };
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }
    }
}
