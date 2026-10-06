using Project.Core.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Infrastructure.Rendering
{
    /// <summary>
    /// Gökyüzü katmanları: saate göre prosedürel skybox parametreleri, kayan bulut kubbesi, güneş/ay diski + hale, uzak dağ
    /// silüeti (iki halka), şafak/akşam ışık huzmeleri. Kameraya konumca bağlıdır (sonsuz uzaklık hissi). Atmosphere.Apply tek
    /// satırla <see cref="Apply"/> çağırır; salt okunur ön ayar kullanılır.
    /// </summary>
    public sealed class SkyEnvironment : MonoBehaviour
    {
        private const float CloudRadius = 1000f;
        private const float NearMountRadius = 1120f;
        private const float FarMountRadius = 1180f; // eski halka sabitleri (HorizonVista katman yarıçapları kullanılır)
        private const float SunRadius = 1250f;

        private static readonly int SunSizeId = Shader.PropertyToID("_SunSize");
        private static readonly int AtmosphereThicknessId = Shader.PropertyToID("_AtmosphereThickness");

        private static SkyEnvironment _instance;

        private Transform _cloudRoot;
        private Material _cloudMat;
        private Texture2D _cloudTex;
        private Material _sunMat, _glowMat, _shaftMat;
        private MeshFilter _shafts;
        private HorizonVista _vista;
        private Transform _sun, _glow;
        private float _cloudSpeed;
        private Texture2D _discTex, _glowTex;

        /// <summary>Atmosphere kancası: gökyüzü katmanlarını saat/hava/ön ayara göre yeniler. Hatalar yutulur.</summary>
        public static void Apply(TimeOfDay time, WeatherKind weather, string mapId, AtmospherePreset preset)
        {
            try
            {
                TuneSkybox(time, weather);
                if (_instance == null)
                    _instance = FindAnyObjectByType<SkyEnvironment>();
                if (_instance == null)
                    _instance = new GameObject("[Sky]").AddComponent<SkyEnvironment>();
                _instance.Configure(time, weather, preset);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static void TuneSkybox(TimeOfDay time, WeatherKind weather)
        {
            var sky = RenderSettings.skybox;
            if (sky == null)
                return;
            float size = time == TimeOfDay.Gunduz ? 0.045f : time == TimeOfDay.Gece ? 0f : 0.075f;
            float thick = time == TimeOfDay.Gunduz ? 1f : time == TimeOfDay.Safak ? 1.35f : time == TimeOfDay.Aksam ? 1.6f : 0.5f;
            if (weather != WeatherKind.Acik)
            {
                size = 0f; // kapalı gökte keskin güneş yok
                thick *= 1.15f;
            }

            if (sky.HasProperty(SunSizeId)) sky.SetFloat(SunSizeId, size);
            if (sky.HasProperty(AtmosphereThicknessId)) sky.SetFloat(AtmosphereThicknessId, thick);
        }

        private static Material MakeMat(string name, int queue)
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
                return null;
            return new Material(shader) { name = name, renderQueue = queue };
        }

        private static GameObject MakePart(Transform parent, string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go;
        }

        private void EnsureBuilt()
        {
            if (_cloudRoot != null)
                return;

            _cloudMat = MakeMat("HK_CloudMat", 3004);
            _sunMat = MakeMat("HK_SunDiscMat", 3000);
            _glowMat = MakeMat("HK_SunGlowMat", 3000);
            _shaftMat = MakeMat("HK_ShaftMat", 3001);
            if (_cloudMat == null)
                return;

            _discTex = SkyMeshes.BuildRadialTexture(64, true);
            _glowTex = SkyMeshes.BuildRadialTexture(64, false);
            _sunMat.mainTexture = _discTex;
            _glowMat.mainTexture = _glowTex;
            _shaftMat.mainTexture = _glowTex;

            var cloudGo = MakePart(transform, "Bulutlar", SkyMeshes.BuildCloudDome(CloudRadius, 0.36f, 2.5f), _cloudMat);
            _cloudRoot = cloudGo.transform;

            var quad = SkyMeshes.BuildQuad();
            var glow = MakePart(transform, "GüneşHale", quad, _glowMat);
            var sun = MakePart(transform, "GüneşDisk", quad, _sunMat);
            _glow = glow.transform;
            _sun = sun.transform;

            _vista = new HorizonVista(transform);
            _shafts = MakePart(transform, "IşıkHuzmeleri", new Mesh(), _shaftMat).GetComponent<MeshFilter>();
        }

        private void Configure(TimeOfDay time, WeatherKind weather, AtmospherePreset p)
        {
            EnsureBuilt();
            if (_cloudRoot == null)
                return;

            // Bulutlar
            var coverage = SkyWaterRules.CloudCoverage(time, weather);
            if (_cloudTex != null) Destroy(_cloudTex);
            _cloudTex = new Texture2D(256, 256, TextureFormat.RGBA32, true) { name = "HK_Clouds", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 2 };
            _cloudTex.SetPixels32(AtmosphereDetailMath.BuildCumulusPixels(256, coverage, 5));
            _cloudTex.Apply(true, true);
            _cloudMat.mainTexture = _cloudTex;
            _cloudMat.color = CloudTint(time, weather);
            _cloudSpeed = SkyWaterRules.CloudScrollSpeed(weather);

            // Güneş / ay yönü (ışık yönünün tersi; gece ay ufuk üstüne yansıtılır)
            var sunLight = RenderSettings.sun;
            var fwd = sunLight != null ? sunLight.transform.forward : Quaternion.Euler(p.SunEuler) * Vector3.forward;
            var dir = -fwd;
            var night = time == TimeOfDay.Gece;
            if (night)
                dir = new Vector3(dir.x, Mathf.Max(0.3f, Mathf.Abs(dir.y)), dir.z);
            dir.Normalize();

            var clear = weather == WeatherKind.Acik;
            var sunOn = (SkyWaterRules.SunVisible(time) || night) && clear;
            _sun.gameObject.SetActive(sunOn);
            _glow.gameObject.SetActive(sunOn || (!clear && time != TimeOfDay.Gece));
            var rot = Quaternion.LookRotation(dir);
            var pos = dir * SunRadius;
            _sun.SetLocalPositionAndRotation(pos, rot);
            _glow.SetLocalPositionAndRotation(pos, rot);
            if (night)
            {
                _sun.localScale = Vector3.one * 48f;
                _glow.localScale = Vector3.one * 260f;
                _sunMat.color = new Color(0.86f, 0.9f, 1f, 0.95f);
                _glowMat.color = new Color(0.55f, 0.65f, 0.9f, 0.3f);
            }
            else
            {
                var low = time == TimeOfDay.Safak || time == TimeOfDay.Aksam;
                _sun.localScale = Vector3.one * (low ? 70f : 52f);
                _glow.localScale = Vector3.one * (low ? 900f : clear ? 520f : 600f);
                var core = Color.Lerp(p.SunColor, Color.white, 0.45f);
                core.a = 1f;
                _sunMat.color = core;
                var halo = p.SunColor;
                halo.a = Mathf.Min(1f, (clear ? (low ? 0.5f : 0.3f) : 0.22f) * AtmosphereMath.MieHaloGain(time, weather));
                _glowMat.color = halo;
            }

            if (Atmosphere.SkyOverrideActive)
            {
                // HDRI kendi güneş/bulutunu taşır: prosedürel katmanlar gizlenir.
                _cloudRoot.gameObject.SetActive(false);
                _sun.gameObject.SetActive(false);
                _glow.gameObject.SetActive(false);
            }
            else
                _cloudRoot.gameObject.SetActive(true);

            // Işık huzmeleri
            var shaft = SkyWaterRules.ShaftStrength(time, weather);
            _shafts.gameObject.SetActive(shaft > 0f);
            if (shaft > 0f)
            {
                var old = _shafts.sharedMesh;
                _shafts.sharedMesh = SkyMeshes.BuildShafts(dir, SunRadius, 9, 700f, 0.22f * shaft, 31);
                if (old != null) Destroy(old);
                var c = Color.Lerp(p.SunColor, Color.white, 0.2f);
                c.a = 1f;
                _shaftMat.color = c;
            }

            // Ufuk manzarası: 3 katman dağ + sis bandı + gece yıldızları (HorizonVista)
            if (_vista != null)
            {
                var mix = SkyWaterRules.MountainFogMix(time, weather);
                _vista.Configure(night, clear, mix, p.FogColor, p.SkyTint, p.SunColor, dir, sunOn && !night);
            }

            // GX7: bulut gölgeleri (ana ışık çerezi) + atmosfer detayı (uzak sis, sirüs, toz, güneş parlaması). Kademe Atmosphere.QualityLevel'dan.
            CloudShadows.Configure(time, weather, Atmosphere.QualityLevel);
            AtmosphereDetail.Configure(transform, time, weather, p, dir, sunOn && !night);
        }

        private static Color CloudTint(TimeOfDay time, WeatherKind weather)
        {
            Color c;
            switch (time)
            {
                case TimeOfDay.Safak: c = new Color(1f, 0.78f, 0.74f, 0.9f); break;
                case TimeOfDay.Aksam: c = new Color(1f, 0.62f, 0.42f, 0.92f); break;
                case TimeOfDay.Gece: c = new Color(0.16f, 0.19f, 0.3f, 0.85f); break;
                default: c = new Color(0.97f, 0.98f, 1f, 0.9f); break;
            }

            if (weather == WeatherKind.Yagmur) c = time == TimeOfDay.Gunduz ? new Color(0.45f, 0.48f, 0.52f, 1f) : new Color(c.r * 0.5f, c.g * 0.53f, c.b * 0.58f, 1f);
            else if (weather == WeatherKind.Kar) c = new Color(c.r * 0.8f, c.g * 0.82f, c.b * 0.86f, 1f);
            return c;
        }

        private static Camera ViewCamera()
        {
            var rigs = CameraRig.Active;
            if (rigs != null && rigs.Count > 0 && rigs[0] != null && rigs[0].WorldCamera != null)
                return rigs[0].WorldCamera;
            return Camera.main;
        }

        private void LateUpdate()
        {
            var cam = ViewCamera();
            if (cam != null)
            {
                transform.position = cam.transform.position;
                if (_vista != null) _vista.Tick(cam.transform.position);
            }
            if (_cloudRoot != null)
                _cloudRoot.Rotate(0f, _cloudSpeed * Time.deltaTime, 0f, Space.World);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }
}
