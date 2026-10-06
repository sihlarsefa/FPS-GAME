using Project.Core.Domain;
using Project.Infrastructure.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Infrastructure.Rendering
{
    /// <summary>
    /// Atmosfer detayı (SkyEnvironment'ın çocuğu): ufuk dağları arası uzak sis halkaları, yüksek irtifa sirüs katmanı, güneş huzmesinde süzülen toz zerreleri
    /// (yalnız güneş görünür + hacimsel sis etkinken) ve güneş parlaması/lens hayaletleri (engel ışını ile sönümlü).
    /// Kademe: AtmosphereDetailMath.ForTier; Atmosphere.QualityLevel saniyede bir yoklanır. Tüm gölgelendirici bulunamazsa ilgili parça atlanır (tek uyarı).
    /// HAREKAT_LENSFLARE_SRP tanımlıysa (asmdef versionDefines) ayrıca URP LensFlareComponentSRP ana ışığa eklenir ve sprite hayaletler kapanır.
    /// </summary>
    public sealed class AtmosphereDetail : MonoBehaviour
    {
        private const float CirrusRadius = 1040f;
        private const float GlareRadius = 1230f;
        private static bool _warnedShader;

        private static readonly (float radius, float peak, float top, int sort)[] HazeSpecs =
        {
            (1175f, 20f, 150f, 1),
            (1148f, 4f, 80f, 2),
        };

        private TimeOfDay _time = TimeOfDay.Gunduz;
        private WeatherKind _weather = WeatherKind.Acik;
        private AtmospherePreset _preset;
        private Vector3 _sunDir = Vector3.up;
        private bool _sunOn;
        private bool _hasPreset;
        private int _tier = -1;
        private float _poll;

        private Material _hazeMat, _cirrusMat, _dustMat, _glareMat, _ghostMat;
        private readonly MeshRenderer[] _haze = new MeshRenderer[2];
        private GameObject _cirrus;
        private Texture2D _cirrusTex, _glowTex, _ringTex;
        private ParticleSystem _dust;
        private ParticleSystemRenderer _dustRenderer;
        private float _dustFactor;
        private int _dustMax;
        private Transform _glare;
        private readonly Transform[] _ghosts = new Transform[4];
        private readonly MeshRenderer[] _ghostRenderers = new MeshRenderer[4];
        private MaterialPropertyBlock _mpb; // alan başlatıcısında oluşturulamaz (Unity CreateImpl kısıtı)
        private float _flareVis;
        private Mesh _quad;
        private AtmosphereDetailTier _cfg;
        private float _cirrusScroll;

        /// <summary>SkyEnvironment.Configure kancası; bileşeni ister ve parametreleri uygular. Hatalar yutulur.</summary>
        public static void Configure(Transform skyRoot, TimeOfDay time, WeatherKind weather, AtmospherePreset preset, Vector3 sunDir, bool sunOn)
        {
            try
            {
                if (skyRoot == null)
                    return;
                var child = skyRoot.Find("[AtmosphereDetail]");
                if (child == null)
                {
                    var go = new GameObject("[AtmosphereDetail]");
                    go.transform.SetParent(skyRoot, false);
                    child = go.transform;
                }

                var comp = child.GetComponent<AtmosphereDetail>();
                if (comp == null)
                    comp = child.gameObject.AddComponent<AtmosphereDetail>();
                comp.Apply(time, weather, preset, sunDir, sunOn);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[AtmosphereDetail] uygulanamadı: " + e.Message);
            }
        }

        private static Material MakeMat(string name, int queue)
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                if (!_warnedShader)
                {
                    _warnedShader = true;
                    Debug.LogWarning("[AtmosphereDetail] Sprites/Default gölgelendiricisi bulunamadı; atmosfer detayı atlandı.");
                }

                return null;
            }

            return new Material(shader) { name = name, renderQueue = queue, hideFlags = HideFlags.HideAndDontSave };
        }

        private static Camera ViewCamera()
        {
            var rigs = CameraRig.Active;
            if (rigs != null && rigs.Count > 0 && rigs[0] != null && rigs[0].WorldCamera != null)
                return rigs[0].WorldCamera;
            return Camera.main;
        }

        private void Apply(TimeOfDay time, WeatherKind weather, AtmospherePreset preset, Vector3 sunDir, bool sunOn)
        {
            _time = time; _weather = weather; _preset = preset; _sunDir = sunDir; _sunOn = sunOn; _hasPreset = true;
            _tier = Atmosphere.QualityLevel;
            Rebuild();
        }

        private void Rebuild()
        {
            _cfg = AtmosphereDetailMath.ForTier(_tier);
            var hdri = Atmosphere.SkyOverrideActive;
            BuildHaze(hdri ? 0 : _cfg.HazeRings);
            BuildCirrus(!hdri && _cfg.Cirrus);
            BuildDust(_cfg.DustMax);
            BuildFlare(!hdri && _cfg.Glare, _cfg.FlareGhosts);
        }

        // ---- Uzak sis halkaları ----
        private void BuildHaze(int rings)
        {
            var color = Color.Lerp(_preset.FogColor, _preset.SunColor, 0.15f);
            for (var i = 0; i < _haze.Length; i++)
            {
                if (i >= rings)
                {
                    if (_haze[i] != null) _haze[i].gameObject.SetActive(false);
                    continue;
                }

                if (_hazeMat == null)
                    _hazeMat = MakeMat("HK_HazeMat", 3002);
                if (_hazeMat == null)
                    return;
                var spec = HazeSpecs[i];
                var data = AtmosphereDetailMath.BuildHazeRing(spec.radius, -40f, spec.peak, spec.top, 96);
                var a = AtmosphereDetailMath.HazeAlpha(_time, _weather, i);
                var cols = new Color[data.Alpha.Length];
                for (var c = 0; c < cols.Length; c++)
                    cols[c] = new Color(color.r, color.g, color.b, data.Alpha[c] * a);
                if (_haze[i] == null)
                {
                    var go = new GameObject("UzakSis" + i);
                    go.transform.SetParent(transform, false);
                    go.AddComponent<MeshFilter>();
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = _hazeMat;
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    mr.lightProbeUsage = LightProbeUsage.Off;
                    mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    mr.sortingOrder = spec.sort;
                    _haze[i] = mr;
                }

                var mf = _haze[i].GetComponent<MeshFilter>();
                var old = mf.sharedMesh;
                var mesh = new Mesh { name = "HK_HazeRing" };
                mesh.vertices = data.Vertices;
                mesh.colors = cols;
                mesh.triangles = data.Triangles;
                mesh.bounds = new Bounds(Vector3.zero, new Vector3(4000f, 1000f, 4000f));
                mf.sharedMesh = mesh;
                if (old != null) Destroy(old);
                _haze[i].gameObject.SetActive(true);
            }
        }

        // ---- Sirüs ----
        private void BuildCirrus(bool on)
        {
            var cov = AtmosphereDetailMath.CirrusCoverage(_time, _weather);
            on = on && cov > 0f;
            if (!on)
            {
                if (_cirrus != null) _cirrus.SetActive(false);
                return;
            }

            if (_cirrusMat == null)
                _cirrusMat = MakeMat("HK_CirrusMat", 3004);
            if (_cirrusMat == null)
                return;
            if (_cirrus == null)
            {
                _cirrus = new GameObject("Sirüs");
                _cirrus.transform.SetParent(transform, false);
                _cirrus.AddComponent<MeshFilter>().sharedMesh = SkyMeshes.BuildCloudDome(CirrusRadius, 0.5f, 1.2f);
                var mr = _cirrus.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _cirrusMat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.lightProbeUsage = LightProbeUsage.Off;
                mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
                mr.sortingOrder = -1;
            }

            if (_cirrusTex != null) Destroy(_cirrusTex);
            const int size = 256;
            _cirrusTex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "HK_Cirrus", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var d = AtmosphereDetailMath.CirrusDensity(x / (float)size, y / (float)size, 91);
                var a = AtmosphereDetailMath.CirrusSoftAlpha(d, cov);
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }

            _cirrusTex.SetPixels32(px);
            _cirrusTex.Apply(true, true);
            _cirrusMat.mainTexture = _cirrusTex;
            var tint = _time == TimeOfDay.Safak ? new Color(1f, 0.82f, 0.76f) : _time == TimeOfDay.Aksam ? new Color(1f, 0.7f, 0.5f) : _time == TimeOfDay.Gece ? new Color(0.3f, 0.34f, 0.5f) : Color.white;
            tint.a = AtmosphereDetailMath.CirrusAlpha(_time, _weather);
            _cirrusMat.color = tint;
            var w = WindSystem.Current;
            var yaw = Mathf.Atan2(w.DirX, w.DirZ) * Mathf.Rad2Deg;
            _cirrus.transform.localRotation = Quaternion.Euler(0f, yaw - 90f, 0f);
            _cirrus.SetActive(true);
        }

        // ---- Toz zerreleri ----
        private void BuildDust(int max)
        {
            _dustMax = max;
            if (max <= 0)
            {
                if (_dust != null)
                {
                    _dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    _dust.gameObject.SetActive(false);
                }

                _dustFactor = 0f;
                return;
            }

            if (_dustMat == null)
                _dustMat = MakeMat("HK_DustMat", 3100);
            if (_dustMat == null)
                return;
            EnsureGlowTex();
            _dustMat.mainTexture = _glowTex;
            if (_dust == null)
            {
                var go = new GameObject("Toz");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                _dust = go.AddComponent<ParticleSystem>();
                _dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = _dust.main;
                main.loop = true;
                main.playOnAwake = false;
                main.duration = 5f;
                main.startLifetime = 9f;
                main.startSpeed = 0f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.06f);
                main.startColor = Color.white;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.gravityModifier = 0f;
                var shape = _dust.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(14f, 6f, 14f);
                var noise = _dust.noise;
                noise.enabled = true;
                noise.strength = 0.25f;
                noise.frequency = 0.25f;
                noise.scrollSpeed = 0.1f;
                noise.quality = ParticleSystemNoiseQuality.Low;
                var col = _dust.colorOverLifetime;
                col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
                col.color = g;
                _dustRenderer = go.GetComponent<ParticleSystemRenderer>();
                _dustRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                _dustRenderer.sharedMaterial = _dustMat;
                _dustRenderer.shadowCastingMode = ShadowCastingMode.Off;
                _dustRenderer.receiveShadows = false;
                _dustRenderer.lightProbeUsage = LightProbeUsage.Off;
                _dustRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }

            var m = _dust.main;
            m.maxParticles = max;
            var em = _dust.emission;
            em.enabled = true;
            em.rateOverTime = max / 9f;
            _dust.gameObject.SetActive(true);
        }

        private void EnsureGlowTex()
        {
            if (_glowTex == null) _glowTex = SkyMeshes.BuildRadialTexture(64, false);
        }

        private void UpdateDust(Camera cam, float dt)
        {
            if (_dust == null || _dustMax <= 0 || _dustMat == null)
                return;
            var fogOn = VolumetricFog.IsActive;
            var target = AtmosphereDetailMath.DustVisible(_time, _weather, _sunDir.y, fogOn) ? 1f : 0f;
            _dustFactor = AtmosphereDetailMath.Approach(_dustFactor, target, 1.2f, dt);
            if (_dustFactor < 0.01f)
            {
                if (_dust.isEmitting) _dust.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                return;
            }

            if (!_dust.isPlaying) _dust.Play();
            var dot = cam != null ? Vector3.Dot(cam.transform.forward, _sunDir) : 0f;
            var c = Color.Lerp(_preset.SunColor, Color.white, 0.4f);
            c.a = AtmosphereDetailMath.DustAlpha(dot, 0.55f) * _dustFactor;
            _dustMat.color = c;
        }

        // ---- Parlama / lens hayaletleri ----
        private void BuildFlare(bool on, int ghosts)
        {
            if (!on)
            {
                if (_glare != null) _glare.gameObject.SetActive(false);
                for (var i = 0; i < _ghosts.Length; i++)
                    if (_ghosts[i] != null) _ghosts[i].gameObject.SetActive(false);
                return;
            }

            if (_glareMat == null) _glareMat = MakeMat("HK_GlareMat", 3050);
            if (_ghostMat == null) _ghostMat = MakeMat("HK_GhostMat", 3600);
            if (_glareMat == null || _ghostMat == null)
                return;
            EnsureGlowTex();
            if (_ringTex == null) _ringTex = BuildRingTexture(64);
            if (_quad == null) _quad = SkyMeshes.BuildQuad();
            _glareMat.mainTexture = _glowTex;
            _ghostMat.mainTexture = _ringTex;
            if (_glare == null)
            {
                var go = new GameObject("GüneşParlama");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = _quad;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _glareMat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                _glare = go.transform;
            }

            _glare.localScale = Vector3.one * 1500f;
            for (var i = 0; i < _ghosts.Length; i++)
            {
                if (i >= ghosts)
                {
                    if (_ghosts[i] != null) _ghosts[i].gameObject.SetActive(false);
                    continue;
                }

                if (_ghosts[i] == null)
                {
                    var go = new GameObject("Hayalet" + i);
                    go.transform.SetParent(transform, false);
                    go.AddComponent<MeshFilter>().sharedMesh = _quad;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = _ghostMat;
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    _ghosts[i] = go.transform;
                    _ghostRenderers[i] = mr;
                }

                _ghosts[i].gameObject.SetActive(false);
            }

            _glare.gameObject.SetActive(_sunOn);
#if HAREKAT_LENSFLARE_SRP
            TryEnableSrpFlare(ghosts > 0 && _sunOn);
#endif
        }

        private static Texture2D BuildRingTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "HK_FlareRing", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var d = new Vector2((x + 0.5f) / size - 0.5f, (y + 0.5f) / size - 0.5f).magnitude * 2f;
                var ring = Mathf.Clamp01(1f - Mathf.Abs(d - 0.78f) / 0.2f);
                var fill = Mathf.Clamp01(1f - d) * 0.25f;
                var a = Mathf.Clamp01(ring * ring + fill) * (1f - Mathf.SmoothStep(0.92f, 1f, d));
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }

            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        private void UpdateFlare(Camera cam, float dt)
        {
            if (_glare == null || !_glare.gameObject.activeSelf || cam == null || _glareMat == null)
                return;
            var camPos = cam.transform.position;
            var dot = Vector3.Dot(cam.transform.forward, _sunDir);
            var target = 0f;
            if (dot > 0.5f && _sunOn)
                target = Physics.Raycast(camPos, _sunDir, 1500f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ? 0f : 1f;
            _flareVis = AtmosphereDetailMath.Approach(_flareVis, target, 8f, dt);
            var inten = AtmosphereDetailMath.GlareIntensity(dot, _flareVis, _weather, _time);

            var pos = _sunDir * GlareRadius;
            _glare.SetLocalPositionAndRotation(pos, Quaternion.LookRotation(_sunDir));
            var gc = Color.Lerp(_preset.SunColor, Color.white, 0.3f);
            gc.a = inten * 0.55f;
            _glareMat.color = gc;

            var vp = cam.WorldToViewportPoint(camPos + _sunDir * 100f);
            const float dist = 4f;
            var worldH = 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            for (var i = 0; i < _ghosts.Length; i++)
            {
                var g = _ghosts[i];
                if (g == null)
                    continue;
                var show = inten > 0.01f && vp.z > 0f && !_hasSrpFlare;
                if (g.gameObject.activeSelf != show) g.gameObject.SetActive(show);
                if (!show)
                    continue;
                var k = AtmosphereDetailMath.GhostK(i);
                var gv = AtmosphereDetailMath.GhostViewport(new Vector2(vp.x, vp.y), k);
                g.position = cam.ViewportToWorldPoint(new Vector3(gv.x, gv.y, dist));
                g.rotation = cam.transform.rotation;
                var size = (i % 2 == 0 ? 0.12f : 0.07f) * worldH;
                g.localScale = new Vector3(size, size, 1f);
                var tint = i == 0 ? new Color(1f, 0.85f, 0.6f) : i == 1 ? new Color(0.6f, 0.85f, 1f) : i == 2 ? new Color(0.7f, 1f, 0.8f) : new Color(1f, 0.7f, 0.9f);
                tint.a = inten * 0.16f * (1f - Mathf.Abs(k) * 0.4f);
                if (_mpb == null) _mpb = new MaterialPropertyBlock();
                _mpb.SetColor("_RendererColor", tint);
                _ghostRenderers[i].SetPropertyBlock(_mpb);
            }
        }

        private bool _hasSrpFlare;

#if HAREKAT_LENSFLARE_SRP
        private LensFlareComponentSRP _srp;
        private LensFlareDataSRP _srpData;

        private void TryEnableSrpFlare(bool on)
        {
            var sun = RenderSettings.sun;
            if (sun == null || !on)
            {
                if (_srp != null) _srp.enabled = false;
                _hasSrpFlare = false;
                return;
            }

            if (_srpData == null)
            {
                _srpData = ScriptableObject.CreateInstance<LensFlareDataSRP>();
                _srpData.hideFlags = HideFlags.HideAndDontSave;
                _srpData.elements = new[]
                {
                    MakeElement(_glowTex, 0f, 1.6f, new Color(1f, 0.92f, 0.78f), 0.5f),
                    MakeElement(_ringTex, 0.4f, 0.35f, new Color(0.6f, 0.85f, 1f), 0.18f),
                    MakeElement(_ringTex, 0.9f, 0.2f, new Color(1f, 0.8f, 0.6f), 0.14f),
                };
            }

            _srp = sun.GetComponent<LensFlareComponentSRP>();
            if (_srp == null) _srp = sun.gameObject.AddComponent<LensFlareComponentSRP>();
            _srp.lensFlareData = _srpData;
            _srp.intensity = 0.8f;
            _srp.useOcclusion = true;
            _srp.attenuationByLightShape = false;
            _srp.enabled = true;
            _hasSrpFlare = true;
        }

        private static LensFlareDataElementSRP MakeElement(Texture tex, float position, float scale, Color tint, float intensity)
        {
            var e = new LensFlareDataElementSRP();
            e.visible = true;
            e.lensFlareTexture = tex;
            e.position = position;
            e.uniformScale = scale;
            e.tint = tint;
            e.localIntensity = intensity;
            e.blendMode = SRPLensFlareBlendMode.Additive;
            return e;
        }
#endif

        // ---- Çevrim ----
        private void Update()
        {
            if (!_hasPreset)
                return;
            _poll += Time.unscaledDeltaTime;
            if (_poll >= 1f)
            {
                _poll = 0f;
                var q = Atmosphere.QualityLevel;
                if (q != _tier)
                {
                    _tier = q;
                    Rebuild();
                }
            }

            var cam = ViewCamera();
            var dt = Time.deltaTime;
            UpdateDust(cam, dt);
            UpdateFlare(cam, dt);
            if (_cirrus != null && _cirrus.activeSelf && _cirrusMat != null)
            {
                _cirrusScroll = Mathf.Repeat(_cirrusScroll + 0.0015f * dt, 1f);
                _cirrusMat.mainTextureOffset = new Vector2(_cirrusScroll, 0f);
            }
        }

        private void OnDestroy()
        {
            for (var i = 0; i < _haze.Length; i++)
                if (_haze[i] != null && _haze[i].GetComponent<MeshFilter>().sharedMesh != null)
                    Destroy(_haze[i].GetComponent<MeshFilter>().sharedMesh);
            if (_cirrusTex != null) Destroy(_cirrusTex);
            if (_glowTex != null) Destroy(_glowTex);
            if (_ringTex != null) Destroy(_ringTex);
            if (_quad != null) Destroy(_quad);
            if (_hazeMat != null) Destroy(_hazeMat);
            if (_cirrusMat != null) Destroy(_cirrusMat);
            if (_dustMat != null) Destroy(_dustMat);
            if (_glareMat != null) Destroy(_glareMat);
            if (_ghostMat != null) Destroy(_ghostMat);
        }
    }
}
