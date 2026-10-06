using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Menü dioramasının ışık ve görünüm ayarı (PUBG lobisi gibi keskin): soldan sıcak alçak anahtar ışık (yumuşak gölge),
    /// arkadan soğuk kontur ışığı, küçük bir dolgu; ortam ışığı sabit; sis çok seyrek; menü kamerasına özel yüksek öncelikli
    /// yerel post-process hacmi (koyu kenar vinyeti; alan derinliği ve kromatik sapma kapalı; hafif bloom ve tane). Genel hacmi değiştirmez.
    /// </summary>
    internal sealed class MenuBackdropLook
    {
        /// <summary>Menü sis yoğunluğu (ExponentialSquared; 0.004 ile 15 m'de ~%1 sis).</summary>
        public const float FogDensity = 0.0009f;

        /// <summary>Ufuk rengi (mavi saat); gökyüzü gradyanı dekorda, bu renk kamera arka planı ve sis tonudur.</summary>
        public static readonly Color BackgroundColor = new Color(0.34f, 0.43f, 0.58f);

        private GameObject _volumeObject;
        private VolumeProfile _profile;

        /// <summary>Işıkları, ortamı, sisi ve yerel hacmi kurar. Her adım ayrı korunur.</summary>
        public static MenuBackdropLook Apply(Transform root)
        {
            var look = new MenuBackdropLook();
            Guard(() => ConfigureKey(root), "anahtar ışık");
            Guard(() => BuildRim(root), "kontur ışığı");
            Guard(() => BuildFill(root), "dolgu ışığı");
            Guard(() => BuildSpots(root), "spot ışıkları");
            Guard(ConfigureAmbientAndFog, "ortam/sis");
            Guard(() => look.BuildVolume(root), "post-process");
            return look;
        }

        /// <summary>Yerel hacmi ve profilini yok eder.</summary>
        public void Dispose()
        {
            if (_volumeObject != null)
                Object.Destroy(_volumeObject);
            _volumeObject = null;

            if (_profile == null)
                return;

            var profile = _profile;
            _profile = null;
            if (profile.components != null)
            {
                for (var i = 0; i < profile.components.Count; i++)
                {
                    if (profile.components[i] != null)
                        Object.Destroy(profile.components[i]);
                }
            }

            Object.Destroy(profile);
        }

        private static void Guard(Action action, string name)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MenuBackdrop] " + name + " kurulamadı: " + e.Message);
            }
        }

        private static void ConfigureKey(Transform root)
        {
            var sun = RenderSettings.sun;
            if (sun == null)
            {
                var go = new GameObject("MenüGüneşi");
                go.transform.SetParent(root, false);
                sun = go.AddComponent<Light>();
                sun.type = LightType.Directional;
                RenderSettings.sun = sun;
            }

            // Soldan (−X) gelen alçak, soğuk mavi saat ışığı (karanlık mod; ana vurgu ateş ve spotlar).
            sun.transform.rotation = Quaternion.Euler(30f, 72f, 0f);
            sun.color = new Color(0.50f, 0.62f, 0.95f);   // Mavi saat: soğuk ay ışığı.
            sun.intensity = 1.5f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.9f;
            sun.shadowNormalBias = 0.6f;
            sun.shadowBias = 0.04f;
        }

        private static void BuildRim(Transform root)
        {
            var go = new GameObject("KonturIşığı");
            go.transform.SetParent(root, false);
            // Arkadan sağdan kameraya doğru: asker silüetini duvardan ayırır.
            go.transform.localRotation = Quaternion.Euler(18f, 215f, 0f);   // Hafif kaydırılmış: sağ kenar ve tüfek silüeti.
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.45f, 0.65f, 1f);
            light.intensity = 2.6f;
            light.shadows = LightShadows.None;
        }

        private static void BuildFill(Transform root)
        {
            var go = new GameObject("DolguIşığı");
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(1.4f, 1.5f, 0.4f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.62f, 0.7f, 0.9f);
            light.range = 7f;
            light.intensity = 1.8f;
            light.shadows = LightShadows.None;

            // Geniş yumuşak dolgu: zemin dokusu ve dekor okunur.
            var wide = new GameObject("GenişDolgu");
            wide.transform.SetParent(root, false);
            wide.transform.localPosition = new Vector3(0f, 5f, 2f);
            var wl = wide.AddComponent<Light>();
            wl.type = LightType.Point;
            wl.color = new Color(0.45f, 0.56f, 0.85f);
            wl.range = 16f;
            wl.intensity = 3.5f;   // Gölge kapalı geniş dolgu: üniformayı patlatmaz, zemini okutur.
            wl.shadows = LightShadows.None;
        }

        private static void BuildSpots(Transform root)
        {
            // Soğuk spotlar: karakteri ve sipere vurup karanlık zeminden ayırır.
            AddSpot(root, "SpotAsker", new Vector3(-1.9f, 3.6f, 0.8f), new Vector3(0.2f, 1.2f, 2.6f), new Color(0.72f, 0.82f, 1f), 9f, 11f, 42f, true);
            AddSpot(root, "SpotSiper", new Vector3(1.5f, 4.6f, 5.2f), new Vector3(3.1f, 0.5f, 7.8f), new Color(0.6f, 0.74f, 1f), 7f, 9f, 55f, false);
            // Arka plan tim: ateşin sıcak yansıması gibi hafif sıcak spot.
            AddSpot(root, "SpotTim", new Vector3(2.9f, 4.4f, 6.4f), new Vector3(3.0f, 1.0f, 9.0f), new Color(1f, 0.78f, 0.52f), 6f, 9f, 60f, false);
            // Kirpi: ana hatlarını arka planda okutan hafif soğuk spot.
            AddSpot(root, "SpotKirpi", new Vector3(-1.0f, 4.5f, 6.5f), new Vector3(-3.9f, 1.0f, 9.6f), new Color(0.55f, 0.7f, 1f), 10f, 12f, 50f, false);
        }

        private static void AddSpot(Transform root, string name, Vector3 position, Vector3 target, Color color, float intensity, float range, float angle, bool shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.LookRotation(target - position, Vector3.up);
            var light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.spotAngle = angle;
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        }

        private static void ConfigureAmbientAndFog()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.32f, 0.40f, 0.58f);
            RenderSettings.ambientEquatorColor = new Color(0.24f, 0.29f, 0.43f);
            RenderSettings.ambientGroundColor = new Color(0.14f, 0.16f, 0.23f);
            RenderSettings.skybox = null;   // Gökyüzü dekordaki gradyan yüzeyle çizilir (kamera SolidColor = ufuk rengi).
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.reflectionIntensity = 0.45f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.26f, 0.34f, 0.48f);   // Ufuk mavisi; yoğunluk düşük, netliği yıkamaz.
            RenderSettings.fogDensity = FogDensity;
        }

        private void BuildVolume(Transform root)
        {
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.name = "HK_MenuSharpProfile";

            var vignette = _profile.Add<Vignette>(true);
            vignette.intensity.Override(0.30f);
            vignette.smoothness.Override(0.55f);
            vignette.color.Override(Color.black);

            var dof = _profile.Add<DepthOfField>(true);
            dof.mode.Override(DepthOfFieldMode.Off);

            var chroma = _profile.Add<ChromaticAberration>(true);
            chroma.intensity.Override(0f);

            var grain = _profile.Add<FilmGrain>(true);
            grain.intensity.Override(0.04f);

            var bloom = _profile.Add<Bloom>(true);
            bloom.threshold.Override(1.2f);
            bloom.intensity.Override(0.35f);
            bloom.scatter.Override(0.35f);

            _volumeObject = new GameObject("MenüKeskinHacmi");
            _volumeObject.transform.SetParent(root, false);
            var volume = _volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100f;
            volume.weight = 1f;
            volume.sharedProfile = _profile;
        }
    }
}
