using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Infrastructure.Rendering
{
    /// <summary>
    /// Açık hava atmosferi: prosedürel gökyüzü, sis, ortam ışığı ve güneş. Bootstrap'ler sahne kurulurken
    /// ApplyOutdoorAtmosphere(menu) çağırır. Oyun: öğle sonrası dağ puslu, nötr-serin; menü: sıcak gün batımı.
    /// Editör kurulumu aynı metodu sahne üretirken çağırabilir (ayarlar sahneye kaydedilir).
    /// </summary>
    public static class RenderSettingsUtil
    {
        public const string SunObjectName = "Directional Light";

        private static readonly int SunSizeId = Shader.PropertyToID("_SunSize");
        private static readonly int SunSizeConvergenceId = Shader.PropertyToID("_SunSizeConvergence");
        private static readonly int AtmosphereThicknessId = Shader.PropertyToID("_AtmosphereThickness");
        private static readonly int SkyTintId = Shader.PropertyToID("_SkyTint");
        private static readonly int GroundColorId = Shader.PropertyToID("_GroundColor");
        private static readonly int ExposureId = Shader.PropertyToID("_Exposure");
        private static readonly int SunDiskId = Shader.PropertyToID("_SunDisk");

        private static Material _gameplaySky;
        private static Material _menuSky;

        /// <summary>Atmosfer parametreleri (oyun ya da menü).</summary>
        public readonly struct AtmosphereProfile
        {
            public readonly Color FogColor;
            public readonly float FogDensity;
            public readonly Color AmbientSky;
            public readonly Color AmbientEquator;
            public readonly Color AmbientGround;
            public readonly Color SunColor;
            public readonly float SunIntensity;
            public readonly Vector3 SunEuler;
            public readonly Color SkyTint;
            public readonly Color GroundColor;
            public readonly float AtmosphereThickness;
            public readonly float Exposure;
            public readonly float SunSize;

            public AtmosphereProfile(Color fogColor, float fogDensity, Color ambientSky, Color ambientEquator, Color ambientGround,
                Color sunColor, float sunIntensity, Vector3 sunEuler, Color skyTint, Color groundColor, float atmosphereThickness,
                float exposure, float sunSize)
            {
                FogColor = fogColor;
                FogDensity = fogDensity;
                AmbientSky = ambientSky;
                AmbientEquator = ambientEquator;
                AmbientGround = ambientGround;
                SunColor = sunColor;
                SunIntensity = sunIntensity;
                SunEuler = sunEuler;
                SkyTint = skyTint;
                GroundColor = groundColor;
                AtmosphereThickness = atmosphereThickness;
                Exposure = exposure;
                SunSize = sunSize;
            }
        }

        /// <summary>Oyun atmosferi: 1 km'lik haritada yakın net, uzak sırtlar puslu (ExponentialSquared).</summary>
        public static AtmosphereProfile Gameplay => new AtmosphereProfile(
            fogColor: new Color(0.64f, 0.7f, 0.76f),
            fogDensity: 0.0016f,
            ambientSky: new Color(0.56f, 0.64f, 0.76f),
            ambientEquator: new Color(0.47f, 0.48f, 0.45f),
            ambientGround: new Color(0.24f, 0.22f, 0.18f),
            sunColor: new Color(1f, 0.955f, 0.87f),
            sunIntensity: 1.3f,
            sunEuler: new Vector3(48f, -38f, 0f),
            skyTint: new Color(0.48f, 0.53f, 0.6f),
            groundColor: new Color(0.38f, 0.37f, 0.34f),
            atmosphereThickness: 1.05f,
            exposure: 1.2f,
            sunSize: 0.035f);

        /// <summary>Menü atmosferi: alçak, sıcak gün batımı güneşi; yoğun, sıcak pus.</summary>
        public static AtmosphereProfile Menu => new AtmosphereProfile(
            fogColor: new Color(0.84f, 0.67f, 0.52f),
            fogDensity: 0.0042f,
            ambientSky: new Color(0.55f, 0.52f, 0.58f),
            ambientEquator: new Color(0.6f, 0.47f, 0.38f),
            ambientGround: new Color(0.22f, 0.18f, 0.14f),
            sunColor: new Color(1f, 0.74f, 0.5f),
            sunIntensity: 1.15f,
            sunEuler: new Vector3(14f, 62f, 0f),
            skyTint: new Color(0.55f, 0.48f, 0.45f),
            groundColor: new Color(0.42f, 0.35f, 0.3f),
            atmosphereThickness: 1.6f,
            exposure: 1.25f,
            sunSize: 0.05f);

        /// <summary>
        /// Gökyüzü, sis, ortam ışığı ve güneşi uygular. Ana yönlü ışık (RenderSettings.sun ya da sahnedeki ilk etkin
        /// yönlü ışık; yoksa "Directional Light" adıyla üretilir) görünüme uyacak şekilde ayarlanır — gökyüzündeki güneş
        /// konumu ile ışık yönü/rengi tutarlı olsun diye. Sahneye özel ışık isteyen bu çağrıdan SONRA ayarlamalıdır.
        /// Gökyüzü gölgelendiricisi bulunamazsa kameralar sis rengine düz temizlenir. Tekrar çağrılabilir.
        /// </summary>
        public static void ApplyOutdoorAtmosphere(bool menu)
        {
            var profile = menu ? Menu : Gameplay;

            // Gökyüzü.
            var sky = GetSkybox(menu);
            RenderSettings.skybox = sky;

            // Sis (URP Lit/Unlit/Particles sis varyantlarını destekler).
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = profile.FogColor;
            RenderSettings.fogDensity = profile.FogDensity;
            RenderSettings.fogStartDistance = 0f;
            RenderSettings.fogEndDistance = CameraRig.WorldFarClip;

            // Ortam ışığı: üç renkli gradyan (fırınlama gerektirmez).
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = profile.AmbientSky;
            RenderSettings.ambientEquatorColor = profile.AmbientEquator;
            RenderSettings.ambientGroundColor = profile.AmbientGround;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = menu ? 0.8f : 0.65f;

            // Güneş (gökyüzü güneş diski RenderSettings.sun yönünü kullanır).
            var sun = EnsureSun(menu);
            ConfigureSun(sun, menu);
            RenderSettings.sun = sun;

            // Kameraların temizleme modu (gökyüzü yoksa düz renk).
            CameraRig.RefreshSkyClear();

            try
            {
                DynamicGI.UpdateEnvironment();
            }
            catch (System.Exception)
            {
                // Bazı platformlarda/başsız sunucuda GI yok — sorun değil.
            }
        }

        /// <summary>
        /// Sahnedeki etkin yönlü ışığı döndürür (ayarlarına dokunmaz); yoksa görünüme göre ayarlanmış yeni bir güneş üretir.
        /// </summary>
        public static Light EnsureSun(bool menu)
        {
            var existing = FindDirectionalLight();
            if (existing != null)
                return existing;

            var go = new GameObject(SunObjectName);
            var light = go.AddComponent<Light>();
            ConfigureSun(light, menu);
            return light;
        }

        /// <summary>Bir yönlü ışığı görünümün güneş ayarlarına getirir (renk, şiddet, açı, yumuşak gölge).</summary>
        public static void ConfigureSun(Light light, bool menu)
        {
            if (light == null)
                return;

            var profile = menu ? Menu : Gameplay;
            light.type = LightType.Directional;
            light.color = profile.SunColor;
            light.intensity = profile.SunIntensity;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = menu ? 0.8f : 0.88f;
            light.shadowBias = 0.05f;
            light.shadowNormalBias = 0.4f;
            light.renderMode = LightRenderMode.ForcePixel;
            light.transform.rotation = Quaternion.Euler(profile.SunEuler);
        }

        /// <summary>Görünüm için gökyüzü malzemesi (önbellekli). Gölgelendirici yoksa null.</summary>
        public static Material GetSkybox(bool menu)
        {
            var cached = menu ? _menuSky : _gameplaySky;
            if (cached != null)
                return cached;

            var library = MaterialLibrary.Library;
            Material material = null;
            if (library != null && library.skybox != null)
            {
                // Kütüphane varlığını DEĞİŞTİRME: oyun görünümü varlığın kendisi, menü ise ısıtılmış bir kopya.
                if (!menu)
                {
                    material = library.skybox;
                }
                else
                {
                    material = new Material(library.skybox) { name = "HK_Skybox_Menu" };
                    ConfigureSkybox(material, true);
                }
            }
            else
            {
                material = CreateSkyboxMaterial(menu);
            }

            if (menu)
                _menuSky = material;
            else
                _gameplaySky = material;
            return material;
        }

        /// <summary>YENİ prosedürel gökyüzü malzemesi üretir (editör kurulumu varlık olarak kaydedebilir). Gölgelendirici yoksa null.</summary>
        public static Material CreateSkyboxMaterial(bool menu)
        {
            var shader = RenderPipelineInfo.Find(RenderPipelineInfo.SkyboxProcedural);
            if (shader == null)
                return null;

            var material = new Material(shader) { name = menu ? "HK_Skybox_Menu" : "HK_Skybox" };
            ConfigureSkybox(material, menu);
            return material;
        }

        /// <summary>Prosedürel gökyüzü parametrelerini uygular (özelliği olmayan gölgelendiricide etkisiz).</summary>
        public static void ConfigureSkybox(Material material, bool menu)
        {
            if (material == null)
                return;

            var profile = menu ? Menu : Gameplay;
            if (material.HasProperty(SunDiskId))
            {
                material.SetFloat(SunDiskId, 2f); // Yüksek kalite güneş diski
                material.DisableKeyword("_SUNDISK_NONE");
                material.DisableKeyword("_SUNDISK_SIMPLE");
                material.EnableKeyword("_SUNDISK_HIGH_QUALITY");
            }

            SetFloat(material, SunSizeId, profile.SunSize);
            SetFloat(material, SunSizeConvergenceId, menu ? 4f : 6f);
            SetFloat(material, AtmosphereThicknessId, profile.AtmosphereThickness);
            SetFloat(material, ExposureId, profile.Exposure);
            if (material.HasProperty(SkyTintId))
                material.SetColor(SkyTintId, profile.SkyTint);
            if (material.HasProperty(GroundColorId))
                material.SetColor(GroundColorId, profile.GroundColor);
        }

        /// <summary>Kameralar için düz arka plan rengi (gökyüzü yoksa kullanılır): güncel sis rengi.</summary>
        public static Color BackgroundColor => RenderSettings.fog ? RenderSettings.fogColor : Gameplay.FogColor;

        private static void SetFloat(Material material, int id, float value)
        {
            if (material.HasProperty(id))
                material.SetFloat(id, value);
        }

        private static Light FindDirectionalLight()
        {
            var sun = RenderSettings.sun;
            if (sun != null && sun.type == LightType.Directional && sun.isActiveAndEnabled)
                return sun;

            var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude);
            for (var i = 0; i < lights.Length; i++)
            {
                var light = lights[i];
                if (light != null && light.type == LightType.Directional && light.isActiveAndEnabled)
                    return light;
            }

            return null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Malzemeler null kontrolüyle yeniden üretilir; kütüphane değişmiş olabilir.
            _gameplaySky = null;
            _menuSky = null;
        }
    }
}
