using System;
using Project.Core.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Infrastructure.Rendering
{
    /// <summary>
    /// Gökyüzü/hava görselleri: iki katmanlı bulut (farklı hız + paralaks), güneş ışık huzmesi billboard'ı, fırtına geçişi
    /// (bulut kararma + rüzgâr hızı WeatherSystem örneğiyle senkron), gece yıldız alanı (titreşimli) + ay evresi diski.
    /// Kendi kendini kurar (sahne yüklenince) ve Atmosphere.CurrentTime/CurrentWeather + WeatherSystem.Current'tan beslenir; SkyEnvironment'ın
    /// tek katmanlı bulut kubbesini ve gece güneş diskini yerini aldıkça gizler. HDRI gökyüzünde (SkyOverrideActive) tümü gizlenir.
    /// Kalite kademesi Atmosphere.QualityLevel (PipelineTiers ile aynı 0..3 ölçeği).
    /// </summary>
    public sealed class SkyLayers : MonoBehaviour
    {
        private const float LowRadius = 940f;
        private const float HighRadius = 985f;
        private const float StarRadius = 1290f;
        private const float MoonRadius = 1240f;
        private const float RayRadius = 1200f;

        private static SkyLayers _instance;

        private Transform _lowRoot, _highRoot, _stars, _moon, _rayA, _rayB;
        private Material _lowMat, _highMat, _starMatA, _starMatB, _moonMat, _rayMat;
        private Texture2D _lowTex, _highTex, _moonTex, _rayTex;
        private MeshFilter _starMfA, _starMfB;
        private MeshRenderer _moonRenderer;

        private TimeOfDay _time = (TimeOfDay)(-1);
        private WeatherKind _weather = (WeatherKind)(-1);
        private int _tier = -1;
        private float _coverageKey = -1f;
        private float _darken = 1f;
        private float _lowAngle, _highAngle;
        private Transform _skyEnvCloud, _skyEnvSun;
        private float _moonPhase = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // Sahne yüklenince kendini kurar; SkyEnvironment ayrıca `SkyLayers.Ensure()` çağırabilir.
            Ensure();
        }

        public static bool Active => _instance != null && _instance.isActiveAndEnabled;

        public static SkyLayers Ensure()
        {
            if (_instance != null) return _instance;
            try
            {
                if (UnityEngine.Application.isBatchMode) return null;
                _instance = FindAnyObjectByType<SkyLayers>();
                if (_instance == null)
                    _instance = new GameObject("[SkyLayers]").AddComponent<SkyLayers>();
            }
            catch (Exception e) { Debug.LogWarning("[SkyLayers] kurulamadı: " + e.Message); }
            return _instance;
        }

        private static Material MakeMat(string name, int queue)
        {
            var shader = Shader.Find("Sprites/Default");
            return shader == null ? null : new Material(shader) { name = name, renderQueue = queue };
        }

        private static Transform MakePart(Transform parent, string name, Mesh mesh, Material mat)
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
            return go.transform;
        }

        private bool _built;

        private void EnsureBuilt()
        {
            if (_built) return;
            _lowMat = MakeMat("HK_CloudLow", 3002);
            _highMat = MakeMat("HK_CloudHigh", 3001);
            _starMatA = MakeMat("HK_StarsA", 2997);
            _starMatB = MakeMat("HK_StarsB", 2997);
            _moonMat = MakeMat("HK_MoonPhase", 3003);
            _rayMat = MakeMat("HK_GodRay", 3006);
            if (_lowMat == null || _moonMat == null) return;
            _built = true;

            _lowRoot = MakePart(transform, "AlçakBulut", SkyMeshes.BuildCloudDome(LowRadius, 0.30f, 3.2f), _lowMat);
            _highRoot = MakePart(transform, "YüksekBulut", SkyMeshes.BuildCloudDome(HighRadius, 0.42f, 1.8f), _highMat);

            var starTex = SkyMeshes.BuildRadialTexture(32, false);
            _starMatA.mainTexture = starTex;
            _starMatB.mainTexture = starTex;
            var starsGo = new GameObject("Yıldızlar");
            _stars = starsGo.transform;
            _stars.SetParent(transform, false);
            _starMfA = MakePart(_stars, "A", new Mesh(), _starMatA).GetComponent<MeshFilter>();
            _starMfB = MakePart(_stars, "B", new Mesh(), _starMatB).GetComponent<MeshFilter>();

            var quad = SkyMeshes.BuildQuad();
            _moon = MakePart(transform, "AyEvresi", quad, _moonMat);
            _moonRenderer = _moon.GetComponent<MeshRenderer>();
            _rayTex = BuildRayTexture(128, 41);
            _rayMat.mainTexture = _rayTex;
            _rayA = MakePart(transform, "HuzmeA", quad, _rayMat);
            _rayB = MakePart(transform, "HuzmeB", quad, _rayMat);
        }

        private void Reconfigure(TimeOfDay time, WeatherKind weather, int tier)
        {
            var cfg = SkyLayersMath.ForTier(tier);
            var coverage = SkyWaterRules.CloudCoverage(time, weather);
            var key = Mathf.Round(coverage * 20f) / 20f;
            var tierChanged = tier != _tier;

            if (key != _coverageKey || tierChanged)
            {
                if (_lowTex != null) Destroy(_lowTex);
                if (_highTex != null) Destroy(_highTex);
                _lowTex = MakeCloudTex("HK_CloudLowTex", cfg.CloudTexSize, key, 11);
                _highTex = MakeCloudTex("HK_CloudHighTex", cfg.CloudTexSize, Mathf.Clamp01(key * 0.8f), 23);
                _lowMat.mainTexture = _lowTex;
                _highMat.mainTexture = _highTex;
                _coverageKey = key;
            }

            if (tierChanged)
            {
                Swap(_starMfA, BuildStars(cfg.StarCount / 2, 101));
                Swap(_starMfB, BuildStars(cfg.StarCount - cfg.StarCount / 2, 303));
            }

            if (_moonTex == null)
                RebuildMoon();

            _time = time; _weather = weather; _tier = tier;
        }

        private static Texture2D MakeCloudTex(string name, int size, float coverage, int seed)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 2 };
            tex.SetPixels32(AtmosphereDetailMath.BuildCumulusPixels(size, coverage, seed));
            tex.Apply(true, true);
            return tex;
        }

        private static void Swap(MeshFilter mf, Mesh mesh)
        {
            var old = mf.sharedMesh;
            mf.sharedMesh = mesh;
            if (old != null) Destroy(old);
        }

        private static Mesh BuildStars(int count, int seed)
        {
            var verts = new Vector3[count * 4];
            var cols = new Color[count * 4];
            var uvs = new Vector2[count * 4];
            var tris = new int[count * 6];
            for (var i = 0; i < count; i++)
            {
                var dir = SkyLayersMath.StarDirection(i, seed);
                var bright = SkyLayersMath.StarBrightness(i, seed);
                var size = Mathf.Lerp(3f, 9f, bright);
                var c = dir * StarRadius;
                var right = Vector3.Cross(Vector3.up, dir).normalized;
                var up = Vector3.Cross(dir, right);
                var warm = SkyLayersMath.Hash01(i, seed + 9);
                var col = Color.Lerp(new Color(0.8f, 0.88f, 1f), new Color(1f, 0.92f, 0.8f), warm);
                col.a = bright;
                var v = i * 4;
                verts[v] = c - right * size - up * size;
                verts[v + 1] = c + right * size - up * size;
                verts[v + 2] = c + right * size + up * size;
                verts[v + 3] = c - right * size + up * size;
                uvs[v] = new Vector2(0, 0); uvs[v + 1] = new Vector2(1, 0); uvs[v + 2] = new Vector2(1, 1); uvs[v + 3] = new Vector2(0, 1);
                cols[v] = cols[v + 1] = cols[v + 2] = cols[v + 3] = col;
                var t = i * 6;
                tris[t] = v; tris[t + 1] = v + 2; tris[t + 2] = v + 1; tris[t + 3] = v; tris[t + 4] = v + 3; tris[t + 5] = v + 2;
            }
            var m = new Mesh { name = "HK_SkyStars", indexFormat = verts.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            m.vertices = verts; m.colors = cols; m.uv = uvs; m.triangles = tris;
            m.bounds = new Bounds(Vector3.zero, Vector3.one * 4000f);
            return m;
        }

        private static Texture2D BuildRayTexture(int size, int seed)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "HK_GodRayTex", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var dx = (x + 0.5f) / size * 2f - 1f;
                var dy = (y + 0.5f) / size * 2f - 1f;
                var r = Mathf.Sqrt(dx * dx + dy * dy);
                var ang = (Mathf.Atan2(dy, dx) / (2f * Mathf.PI)) + 0.5f;
                var a = SkyLayersMath.RayIntensity(ang, r, seed);
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        private void RebuildMoon()
        {
            var days = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalDays;
            var phase = SkyLayersMath.MoonPhase(days);
            _moonPhase = phase;
            const int S = 64;
            if (_moonTex == null)
                _moonTex = new Texture2D(S, S, TextureFormat.RGBA32, false) { name = "HK_MoonPhaseTex", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[S * S];
            for (var y = 0; y < S; y++)
            for (var x = 0; x < S; x++)
            {
                var nx = (x + 0.5f) / S * 2f - 1f;
                var ny = (y + 0.5f) / S * 2f - 1f;
                var v = SkyLayersMath.MoonPixel(nx, ny, phase);
                if (v < 0f) { px[y * S + x] = new Color32(0, 0, 0, 0); continue; }
                var edge = 1f - Mathf.SmoothStep(0.92f, 1f, Mathf.Sqrt(nx * nx + ny * ny));
                var b = (byte)Mathf.RoundToInt(Mathf.Clamp01(v) * 255f);
                px[y * S + x] = new Color32(b, b, (byte)Mathf.Min(255, b + 10), (byte)Mathf.RoundToInt(edge * 255f));
            }
            _moonTex.SetPixels32(px);
            _moonTex.Apply(false, false);
            _moonMat.mainTexture = _moonTex;
        }

        private static Camera ViewCamera()
        {
            var rigs = CameraRig.Active;
            if (rigs != null && rigs.Count > 0 && rigs[0] != null && rigs[0].WorldCamera != null)
                return rigs[0].WorldCamera;
            return Camera.main;
        }

        private static Vector3 SunDir(TimeOfDay time)
        {
            var sun = RenderSettings.sun;
            var d = sun != null ? -sun.transform.forward : new Vector3(0.4f, 0.6f, 0.5f);
            if (time == TimeOfDay.Gece) d = new Vector3(d.x, Mathf.Max(0.3f, Mathf.Abs(d.y)), d.z);
            return d.normalized;
        }

        private void HideSkyEnvironmentDuplicates(bool night)
        {
            if (_skyEnvCloud == null || _skyEnvSun == null)
            {
                var env = FindAnyObjectByType<SkyEnvironment>();
                if (env == null) return;
                if (_skyEnvCloud == null) _skyEnvCloud = env.transform.Find("Bulutlar");
                if (_skyEnvSun == null) _skyEnvSun = env.transform.Find("GüneşDisk");
            }
            // SkyEnvironment.Configure bulut kökünü yeniden SetActive eder; yalnız MeshRenderer kapatıldığı için çakışmaz.
            if (_skyEnvCloud != null)
            {
                var r = _skyEnvCloud.GetComponent<MeshRenderer>();
                if (r != null) r.enabled = false; // yerini iki katmanlı bulut alır
            }
            if (_skyEnvSun != null)
            {
                var r = _skyEnvSun.GetComponent<MeshRenderer>();
                if (r != null && night) r.enabled = false;
                else if (r != null) r.enabled = true;
            }
        }

        private void LateUpdate()
        {
            EnsureBuilt();
            if (!_built) return;

            var cam = ViewCamera();
            if (cam == null) return;

            var time = Atmosphere.CurrentTime;
            var weather = Atmosphere.CurrentWeather;
            var tier = Atmosphere.QualityLevel;
            if (time != _time || weather != _weather || tier != _tier)
                Reconfigure(time, weather, tier);

            var cfg = SkyLayersMath.ForTier(_tier);
            var hdri = Atmosphere.SkyOverrideActive;
            var dt = Time.deltaTime;
            var camPos = cam.transform.position;
            transform.position = camPos;

            var ws = WeatherSystem.Instance;
            var s = ws != null ? ws.Current : SkyLayersMath.DefaultSample(weather);
            var wind = WeatherTimeline.WindStrength(s);

            // Fırtına geçişi: kararma hedefe yumuşakça yaklaşır (örnek zaten 60-120 sn'de akar; burası kare titremesini giderir).
            var targetDark = SkyLayersMath.StormDarken(s.Cloud, s.Rain, s.Storm);
            _darken = Mathf.Lerp(_darken, targetDark, 1f - Mathf.Exp(-dt * 1.5f));

            var baseCol = SkyLayersMath.CloudBase(time);
            var alpha = SkyLayersMath.CloudAlpha(s.Cloud, s.Rain);
            var lowCol = baseCol * _darken; lowCol.a = alpha;
            var highCol = baseCol * SkyLayersMath.HighLayerDarken(_darken); highCol.a = alpha * 0.8f;
            _lowMat.color = lowCol;
            _highMat.color = highCol;

            var baseSpeed = SkyWaterRules.CloudScrollSpeed(weather) * 0.5f + 0.15f;
            _lowAngle += SkyLayersMath.LowLayerSpeed(baseSpeed, wind) * dt;
            _highAngle += SkyLayersMath.HighLayerSpeed(baseSpeed, wind) * dt;
            _lowRoot.localRotation = Quaternion.Euler(0f, _lowAngle, 0f);
            _highRoot.localRotation = Quaternion.Euler(0f, _highAngle, 0f);

            // Paralaks: alçak katman kamera hareketine biraz geride kalır, yüksek daha az (derinlik hissi).
            _lowRoot.localPosition = new Vector3(Mathf.Clamp(-camPos.x * 0.03f, -40f, 40f), 0f, Mathf.Clamp(-camPos.z * 0.03f, -40f, 40f));
            _highRoot.localPosition = new Vector3(Mathf.Clamp(-camPos.x * 0.008f, -12f, 12f), 0f, Mathf.Clamp(-camPos.z * 0.008f, -12f, 12f));

            _lowRoot.gameObject.SetActive(!hdri);
            _highRoot.gameObject.SetActive(!hdri && cfg.HighCloudLayer);

            // Yıldızlar
            var starVis = hdri ? 0f : SkyLayersMath.StarVisibility(time, s.Cloud, s.Storm);
            _stars.gameObject.SetActive(starVis > 0.01f);
            if (starVis > 0.01f)
            {
                _stars.localRotation = Quaternion.Euler(18f, Time.time * 0.02f, 0f);
                var ta = cfg.Twinkle ? SkyLayersMath.Twinkle(Time.time, 0) : 1f;
                var tb = cfg.Twinkle ? SkyLayersMath.Twinkle(Time.time, 1) : 1f;
                _starMatA.color = new Color(1f, 1f, 1f, starVis * ta);
                _starMatB.color = new Color(1f, 1f, 1f, starVis * tb);
            }

            // Ay evresi (gece diski SkyEnvironment'ınkinin yerini alır)
            var dir = SunDir(time);
            var moonVis = hdri ? 0f : SkyLayersMath.MoonVisibility(time, s.Cloud, s.Storm);
            _moon.gameObject.SetActive(moonVis > 0.01f && time == TimeOfDay.Gece);
            if (_moon.gameObject.activeSelf)
            {
                _moon.SetLocalPositionAndRotation(dir * MoonRadius, Quaternion.LookRotation(dir));
                _moon.localScale = Vector3.one * 56f;
                _moonMat.color = new Color(1f, 1f, 1f, moonVis);
            }
            HideSkyEnvironmentDuplicates(time == TimeOfDay.Gece);

            // Güneş ışık huzmeleri (camera'ya dönük billboard, yavaş dönen iki kat)
            var facing = Mathf.Clamp01(Vector3.Dot(cam.transform.forward, dir) * 1.4f);
            var rayA = hdri || !cfg.GodRays ? 0f : SkyLayersMath.GodRayAlpha(time, s.Cloud, s.Rain, facing);
            var showRay = rayA > 0.01f;
            _rayA.gameObject.SetActive(showRay);
            _rayB.gameObject.SetActive(showRay && _tier >= 2);
            if (showRay)
            {
                var pos = dir * RayRadius;
                var look = Quaternion.LookRotation(dir);
                _rayA.SetLocalPositionAndRotation(pos, look * Quaternion.Euler(0f, 0f, Time.time * 1.2f));
                _rayB.SetLocalPositionAndRotation(pos, look * Quaternion.Euler(0f, 0f, -Time.time * 0.8f + 17f));
                _rayA.localScale = Vector3.one * 1500f;
                _rayB.localScale = Vector3.one * 1100f;
                var c = Color.Lerp(new Color(1f, 0.93f, 0.78f), Color.white, 0.3f);
                var sun = RenderSettings.sun;
                if (sun != null) c = Color.Lerp(sun.color, Color.white, 0.25f);
                c.a = rayA;
                _rayMat.color = c;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            if (_lowTex != null) Destroy(_lowTex);
            if (_highTex != null) Destroy(_highTex);
            if (_moonTex != null) Destroy(_moonTex);
            if (_rayTex != null) Destroy(_rayTex);
        }
    }
}
