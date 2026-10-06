using Project.Infrastructure.World;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Project.Infrastructure.Vfx
{
    /// <summary>
    /// Patlama zamanlama / eğri kuralları (saf, Unity sahnesi gerektirmez; testlenir). Süreler sn, oranlar 0..1.
    /// </summary>
    public static class ExplosionFxRules
    {
        /// <summary>Turuncudan siyaha duman geçiş süresi.</summary>
        public const float SmokeTransition = 1.2f;

        /// <summary>Ateş topu katmanlarının toplam ömrü.</summary>
        public const float FireLife = 3.2f;

        /// <summary>Kalıcı duman sürüklenmesi (rüzgârla) süresi.</summary>
        public const float SmokeLinger = 8f;

        public const float DistortLife = 0.7f;
        public const float RingLife = 1.6f;
        public const float ColumnLife = 3.4f;

        /// <summary>Bir örneğin toplam ömrü (en uzun katman).</summary>
        public const float TotalLife = SmokeLinger;

        /// <summary>Eşzamanlı patlama örneği sınırı (kademeye göre).</summary>
        public static int MaxInstances(VfxTier tier)
        {
            return tier == VfxTier.High ? 3 : tier == VfxTier.Medium ? 2 : 1;
        }

        /// <summary>Ateş topu katman sayısı (2-3; Düşük kademede 1).</summary>
        public static int FireLayers(VfxTier tier)
        {
            return tier == VfxTier.High ? 3 : tier == VfxTier.Medium ? 2 : 1;
        }

        /// <summary>Kalıcı duman kümesi sayısı.</summary>
        public static int SmokeLayers(VfxTier tier)
        {
            return tier == VfxTier.High ? 2 : tier == VfxTier.Medium ? 1 : 0;
        }

        public static bool AllowDistortion(VfxTier tier) { return tier != VfxTier.Low; }
        public static bool AllowRing(VfxTier tier) { return tier != VfxTier.Low; }
        public static bool AllowColumn(VfxTier tier) { return tier == VfxTier.High; }

        /// <summary>Genişleme: yumuşak yavaşlayan (ease-out kübik) 0..1.</summary>
        public static float Expansion(float age, float duration)
        {
            if (duration <= 0f)
                return 1f;
            var t = Mathf.Clamp01(age / duration);
            var inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        /// <summary>Duman karışımı: 0 = saf ateş, 1 = saf duman; <see cref="SmokeTransition"/> sn'de smoothstep.</summary>
        public static float SmokeBlend(float age)
        {
            var t = Mathf.Clamp01(age / SmokeTransition);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Isı (parlaklık) 1 → 0: duman geçişinin ilk yarısında hızla söner.</summary>
        public static float Heat(float age)
        {
            var t = Mathf.Clamp01(age / (SmokeTransition * 0.9f));
            return (1f - t) * (1f - t);
        }

        /// <summary>Aşınma eşiği: zamanla artar (0.05 → 0.85), gövde parçalanarak dağılır.</summary>
        public static float Erosion(float age, float life)
        {
            if (life <= 0f)
                return 0.85f;
            return Mathf.Lerp(0.05f, 0.85f, Mathf.Clamp01(age / life));
        }

        /// <summary>Katman alfa zarfı: hızlı yükselir, son %40'ta söner.</summary>
        public static float AlphaEnvelope(float age, float life, float peak)
        {
            if (age < 0f || life <= 0f || age >= life)
                return 0f;
            var rise = Mathf.Clamp01(age / 0.06f);
            var fall = Mathf.Clamp01((life - age) / (life * 0.4f));
            return peak * rise * fall;
        }

        /// <summary>Kalıcı duman alfası: 1.2 sn'de tepe, 8 sn'ye doğrusal sönüm.</summary>
        public static float LingerAlpha(float age, float peak)
        {
            if (age <= 0f || age >= SmokeLinger)
                return 0f;
            var rise = Mathf.Clamp01(age / SmokeTransition);
            var fall = 1f - Mathf.Clamp01((age - SmokeTransition) / (SmokeLinger - SmokeTransition));
            return peak * rise * fall;
        }

        /// <summary>Rüzgâr hızı (m/s): kapalıysa 0, güç ve esinti ile 0.4..2.4 aralığı.</summary>
        public static float WindSpeed(bool enabled, float strength, float gust)
        {
            if (!enabled)
                return 0f;
            return Mathf.Clamp(0.4f + Mathf.Clamp(strength, 0f, 1.5f) * 1.2f + Mathf.Clamp01(gust) * 0.6f, 0f, 2.4f);
        }

        /// <summary>Duman sürüklenmesi (m): rüzgâr yönünde hızlanarak, yukarıda yumuşak yükselerek.</summary>
        public static Vector3 SmokeDrift(float age, float dirX, float dirZ, float windSpeed, float rise)
        {
            age = Mathf.Max(0f, age);
            var len = Mathf.Sqrt(dirX * dirX + dirZ * dirZ);
            var nx = len > 1e-4f ? dirX / len : 0f;
            var nz = len > 1e-4f ? dirZ / len : 0f;
            // Hız 0'dan rüzgâr hızına ~2.5 sn'de oturur: x(t) = v * (t - tau * (1 - e^(-t/tau))).
            const float tau = 2.5f;
            var run = windSpeed * (age - tau * (1f - Mathf.Exp(-age / tau)));
            var up = rise * (1f - Mathf.Exp(-age / 3f));
            return new Vector3(nx * run, up, nz * run);
        }

        /// <summary>Güneşe dönük yüzey aydınlığı 0.3..1 (dot -1..1), gece söner.</summary>
        public static float SunLit(float dotSun, float night)
        {
            var d = Mathf.Clamp01(dotSun * 0.5f + 0.5f);
            return Mathf.Lerp(0.3f, 1f, d) * Mathf.Lerp(1f, 0.3f, Mathf.Clamp01(night));
        }

        /// <summary>Isı kırılması gücü: kısa sürede tepe, sonra söner.</summary>
        public static float DistortStrength(float age)
        {
            if (age < 0f || age >= DistortLife)
                return 0f;
            var rise = Mathf.Clamp01(age / 0.08f);
            var fall = 1f - Mathf.Clamp01((age - 0.08f) / (DistortLife - 0.08f));
            return rise * fall;
        }

        /// <summary>Örnek ömrü doldu mu.</summary>
        public static bool Finished(float age)
        {
            return age >= TotalLife;
        }
    }

    /// <summary>
    /// Sinematik patlama: 1-3 genişleyen yumuşak ateş topu (aşınma maskeli, turuncu→siyah duman), ısı kırılması dörtgeni,
    /// zemin normali boyunca toprak sütunu + yatay toz halkası, güneşe doğru aydınlanan, rüzgârla sürüklenen 8 sn'lik duman.
    /// Havuzlu, kademe sınırlı. Gölgelendirici yoksa <see cref="TryPlay"/> false döner (GameVfx eski efekte düşer).
    /// </summary>
    internal sealed class ExplosionFx
    {
        private const string ShaderName = "HAREKAT/Vfx/Fireball";

        private static readonly int ModeId = Shader.PropertyToID("_FxMode");
        private static readonly int Params1Id = Shader.PropertyToID("_FxParams");
        private static readonly int Params2Id = Shader.PropertyToID("_FxParams2");
        private static readonly int Params3Id = Shader.PropertyToID("_FxParams3");
        private static readonly int SunDirId = Shader.PropertyToID("_FxSunDir");
        private static readonly int SunColorId = Shader.PropertyToID("_FxSunColor");

        private enum LayerKind { Fire = 0, Distort = 1, Ring = 2, Column = 3, Smoke = 4 }

        private sealed class Layer
        {
            public Transform Transform;
            public MeshRenderer Renderer;
            public LayerKind Kind;
            public float Seed;
            public float Size;
            public float Delay;
            public Vector3 Offset;
            public float Peak;
            public bool Used;
        }

        private sealed class Instance
        {
            public GameObject Root;
            public Layer[] Layers;
            public bool Active;
            public float Age;
            public Vector3 Origin;
            public Vector3 Normal;
            public float Radius;
            public float Night;
            public Vector3 SunDir;
            public Color SunColor;
        }

        private readonly Transform _container;
        private readonly Instance[] _instances;
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private Material _material;
        private Mesh _quad;
        private bool _shaderMissing;
        private bool _supportsOpaque;
        private bool _supportsDepth;
        private bool _rendererChecked;
        private int _nextVictim;
        private VfxRandomLite _rng = new VfxRandomLite(0xE8B10Du);

        public ExplosionFx(Transform container)
        {
            _container = container;
            _instances = new Instance[3];
        }

        public int ActiveCount
        {
            get
            {
                var n = 0;
                for (var i = 0; i < _instances.Length; i++)
                    if (_instances[i] != null && _instances[i].Active)
                        n++;
                return n;
            }
        }

        /// <summary>Gölgelendirici bulundu ve efekt kurulabilir mi.</summary>
        public bool Available
        {
            get
            {
                EnsureAssets();
                return !_shaderMissing && _material != null;
            }
        }

        /// <summary>Patlamayı başlatır. Gölgelendirici/malzeme yoksa false (çağıran eski efekti oynatır).</summary>
        public bool TryPlay(Vector3 position, float radius, Vector3 groundNormal, float night, VfxTier tier)
        {
            if (!Available)
                return false;

            var instance = Acquire();
            if (instance == null)
                return false;

            var normal = groundNormal.sqrMagnitude > 0.25f ? groundNormal.normalized : Vector3.up;
            instance.Origin = position;
            instance.Normal = normal;
            instance.Radius = Mathf.Clamp(radius, 0.5f, 60f);
            instance.Night = Mathf.Clamp01(night);
            ReadSun(out instance.SunDir, out instance.SunColor);
            instance.Age = 0f;
            instance.Active = true;
            Configure(instance, tier);
            instance.Root.SetActive(true);
            Apply(instance, true, Vector3.zero, Quaternion.identity);
            return true;
        }

        public void Tick(float deltaTime, bool hasCamera, Vector3 cameraPosition, Quaternion cameraRotation)
        {
            if (_material == null)
                return;

            var rotation = hasCamera ? cameraRotation : Quaternion.identity;
            var wind = WindSystem.Current;
            var speed = ExplosionFxRules.WindSpeed(wind.Enabled, wind.Strength, wind.Gust);
            for (var i = 0; i < _instances.Length; i++)
            {
                var instance = _instances[i];
                if (instance == null || !instance.Active)
                    continue;

                instance.Age += Mathf.Max(0f, deltaTime);
                if (ExplosionFxRules.Finished(instance.Age))
                {
                    Deactivate(instance);
                    continue;
                }

                var drift = ExplosionFxRules.SmokeDrift(instance.Age, wind.DirX, wind.DirZ, speed, 1.2f + instance.Radius * 0.15f);
                Apply(instance, hasCamera, cameraPosition, rotation, drift);
            }
        }

        public void Clear()
        {
            for (var i = 0; i < _instances.Length; i++)
                if (_instances[i] != null)
                    Deactivate(_instances[i]);
        }

        // ------------------------------------------------------------------ kurulum

        private void EnsureAssets()
        {
            if (_material != null || _shaderMissing)
                return;

            Shader shader = null;
            try { shader = Shader.Find(ShaderName); }
            catch { shader = null; }

            if (shader == null || !shader.isSupported)
            {
                _shaderMissing = true;
                return;
            }

            _material = new Material(shader) { name = "ExplosionFx", hideFlags = HideFlags.HideAndDontSave, renderQueue = 3010 };
            _quad = BuildQuad();
            CheckRenderer();
        }

        private void CheckRenderer()
        {
            if (_rendererChecked)
                return;
            _rendererChecked = true;
            try
            {
                var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                _supportsOpaque = asset != null && asset.supportsCameraOpaqueTexture;
                _supportsDepth = asset != null && asset.supportsCameraDepthTexture;
            }
            catch
            {
                _supportsOpaque = false;
                _supportsDepth = false;
            }
        }

        private static Mesh BuildQuad()
        {
            var mesh = new Mesh { name = "ExplosionFxQuad", hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
            };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(1f, 1f, 0.1f));
            return mesh;
        }

        private Instance Acquire()
        {
            var cap = Mathf.Min(_instances.Length, ExplosionFxRules.MaxInstances(VfxQuality.Tier));
            for (var i = 0; i < cap; i++)
            {
                if (_instances[i] == null)
                    _instances[i] = BuildInstance(i);
                if (!_instances[i].Active)
                    return _instances[i];
            }

            // Hepsi dolu: en eskiyi taşı (yuvarlak sıra).
            _nextVictim = (_nextVictim + 1) % cap;
            return _instances[_nextVictim];
        }

        private Instance BuildInstance(int index)
        {
            const int maxLayers = 3 + 1 + 1 + 1 + 2; // ateş + kırılma + halka + sütun + duman
            var root = new GameObject("ExplosionFx" + index);
            root.transform.SetParent(_container, false);
            root.SetActive(false);
            var layers = new Layer[maxLayers];
            for (var i = 0; i < maxLayers; i++)
            {
                var go = new GameObject("L" + i);
                go.transform.SetParent(root.transform, false);
                var filter = go.AddComponent<MeshFilter>();
                filter.sharedMesh = _quad;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = _material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.enabled = false;
                layers[i] = new Layer { Transform = go.transform, Renderer = renderer };
            }

            return new Instance { Root = root, Layers = layers };
        }

        private void Configure(Instance instance, VfxTier tier)
        {
            CheckRenderer();
            var layers = instance.Layers;
            var radius = instance.Radius;
            var slot = 0;

            var fire = ExplosionFxRules.FireLayers(tier);
            for (var i = 0; i < fire; i++)
            {
                var l = layers[slot++];
                l.Kind = LayerKind.Fire;
                l.Used = true;
                l.Seed = _rng.Next01() * 10f;
                l.Size = radius * (2.6f - i * 0.5f);
                l.Delay = i * 0.05f;
                l.Offset = new Vector3((_rng.Next01() - 0.5f) * radius * 0.5f, radius * (0.35f + i * 0.3f), (_rng.Next01() - 0.5f) * radius * 0.5f);
                l.Peak = 0.95f - i * 0.12f;
            }

            if (ExplosionFxRules.AllowDistortion(tier) && _supportsOpaque)
            {
                var l = layers[slot++];
                l.Kind = LayerKind.Distort; l.Used = true; l.Seed = _rng.Next01() * 10f;
                l.Size = radius * 4.5f; l.Delay = 0f; l.Offset = new Vector3(0f, radius * 0.7f, 0f); l.Peak = 1f;
            }

            if (ExplosionFxRules.AllowRing(tier))
            {
                var l = layers[slot++];
                l.Kind = LayerKind.Ring; l.Used = true; l.Seed = _rng.Next01() * 10f;
                l.Size = radius * 5.5f; l.Delay = 0f; l.Offset = Vector3.zero; l.Peak = 0.8f;
            }

            if (ExplosionFxRules.AllowColumn(tier))
            {
                var l = layers[slot++];
                l.Kind = LayerKind.Column; l.Used = true; l.Seed = _rng.Next01() * 10f;
                l.Size = radius * 1.6f; l.Delay = 0.05f; l.Offset = Vector3.zero; l.Peak = 0.85f;
            }

            var smoke = ExplosionFxRules.SmokeLayers(tier);
            for (var i = 0; i < smoke; i++)
            {
                var l = layers[slot++];
                l.Kind = LayerKind.Smoke; l.Used = true; l.Seed = _rng.Next01() * 10f;
                l.Size = radius * (3.6f + i * 0.8f); l.Delay = 0.2f + i * 0.15f;
                l.Offset = new Vector3((_rng.Next01() - 0.5f) * radius * 0.6f, radius * (1.1f + i * 0.5f), (_rng.Next01() - 0.5f) * radius * 0.6f);
                l.Peak = 0.55f - i * 0.1f;
            }

            for (; slot < layers.Length; slot++)
                layers[slot].Used = false;

            for (var i = 0; i < layers.Length; i++)
                layers[i].Renderer.enabled = layers[i].Used;
        }

        private static void ReadSun(out Vector3 toSun, out Color color)
        {
            toSun = new Vector3(0.3f, 0.8f, 0.5f).normalized;
            color = new Color(1f, 0.95f, 0.85f, 1f);
            try
            {
                var sun = RenderSettings.sun;
                if (sun != null)
                {
                    toSun = -sun.transform.forward;
                    var c = sun.color;
                    var k = Mathf.Clamp(sun.intensity, 0.2f, 1.5f);
                    color = new Color(c.r * k, c.g * k, c.b * k, 1f);
                }
            }
            catch { }
        }

        // ------------------------------------------------------------------ çizim

        private void Apply(Instance instance, bool hasCamera, Vector3 cameraPosition, Quaternion rotation)
        {
            Apply(instance, hasCamera, cameraPosition, rotation, Vector3.zero);
        }

        private void Apply(Instance instance, bool hasCamera, Vector3 cameraPosition, Quaternion rotation, Vector3 drift)
        {
            var age = instance.Age;
            for (var i = 0; i < instance.Layers.Length; i++)
            {
                var l = instance.Layers[i];
                if (!l.Used)
                    continue;

                var la = age - l.Delay;
                float life, alpha, heat, smoke, erode, size, distort = 0f;
                var position = instance.Origin;
                var rot = rotation;
                var scale = new Vector3(1f, 1f, 1f);

                switch (l.Kind)
                {
                    case LayerKind.Fire:
                        life = ExplosionFxRules.FireLife;
                        alpha = ExplosionFxRules.AlphaEnvelope(la, life, l.Peak);
                        heat = ExplosionFxRules.Heat(Mathf.Max(0f, la));
                        smoke = ExplosionFxRules.SmokeBlend(Mathf.Max(0f, la));
                        erode = ExplosionFxRules.Erosion(Mathf.Max(0f, la), life);
                        size = l.Size * Mathf.Lerp(0.25f, 1f, ExplosionFxRules.Expansion(la, 1.4f)) * 0.8f;
                        position += l.Offset * Mathf.Clamp01(la / 1.4f) + drift * 0.35f;
                        break;
                    case LayerKind.Smoke:
                        life = ExplosionFxRules.SmokeLinger;
                        alpha = ExplosionFxRules.LingerAlpha(la, l.Peak);
                        heat = 0f;
                        smoke = 1f;
                        erode = Mathf.Lerp(0.1f, 0.6f, Mathf.Clamp01(la / life));
                        size = l.Size * Mathf.Lerp(0.5f, 1.25f, ExplosionFxRules.Expansion(la, 4f));
                        position += l.Offset + drift;
                        break;
                    case LayerKind.Distort:
                        life = ExplosionFxRules.DistortLife;
                        alpha = la > 0f && la < life ? 1f : 0f;
                        distort = ExplosionFxRules.DistortStrength(la);
                        heat = 0f; smoke = 0f; erode = 0f;
                        size = l.Size * Mathf.Lerp(0.5f, 1f, ExplosionFxRules.Expansion(la, life));
                        position += l.Offset;
                        break;
                    case LayerKind.Ring:
                        life = ExplosionFxRules.RingLife;
                        alpha = ExplosionFxRules.AlphaEnvelope(la, life, l.Peak);
                        heat = ExplosionFxRules.Heat(Mathf.Max(0f, la)) * 0.5f;
                        smoke = 0f;
                        erode = Mathf.Lerp(0.12f, 0.7f, Mathf.Clamp01(la / life));
                        size = l.Size * Mathf.Lerp(0.3f, 1f, ExplosionFxRules.Expansion(la, 0.8f));
                        position += instance.Normal * 0.15f + drift * 0.15f;
                        rot = Quaternion.FromToRotation(Vector3.forward, instance.Normal);
                        break;
                    default: // Column
                        life = ExplosionFxRules.ColumnLife;
                        alpha = ExplosionFxRules.AlphaEnvelope(la, life, l.Peak);
                        heat = ExplosionFxRules.Heat(Mathf.Max(0f, la)) * 0.6f;
                        smoke = 0f;
                        erode = Mathf.Lerp(0.1f, 0.75f, Mathf.Clamp01(la / life));
                        var grow = Mathf.Lerp(0.2f, 1f, ExplosionFxRules.Expansion(la, 0.9f));
                        size = l.Size * grow;
                        scale = new Vector3(size * 0.7f, size * 2.2f, 1f);
                        position += instance.Normal * (size * 1.1f) + drift * 0.25f;
                        var toCam = hasCamera ? cameraPosition - position : Vector3.back;
                        var flat = Vector3.ProjectOnPlane(toCam, instance.Normal);
                        if (flat.sqrMagnitude < 1e-4f)
                            flat = Vector3.ProjectOnPlane(Vector3.back, instance.Normal);
                        rot = Quaternion.LookRotation(flat.normalized, instance.Normal);
                        break;
                }

                var t = l.Kind == LayerKind.Column ? scale : new Vector3(size, size, 1f);
                l.Transform.position = position;
                l.Transform.rotation = rot;
                l.Transform.localScale = t;
                l.Renderer.enabled = alpha > 0.002f;
                if (alpha <= 0.002f)
                    continue;

                _block.Clear();
                _block.SetFloat(ModeId, l.Kind == LayerKind.Smoke ? 0f : (float)(int)l.Kind);
                _block.SetVector(Params1Id, new Vector4(Mathf.Clamp01(la / life), heat, smoke, erode));
                _block.SetVector(Params2Id, new Vector4(alpha, l.Seed, distort, 1.5f + instance.Radius * 0.15f));
                _block.SetVector(Params3Id, new Vector4(instance.Night, _supportsDepth ? 1f : 0f, 0f, 0f));
                _block.SetVector(SunDirId, new Vector4(instance.SunDir.x, instance.SunDir.y, instance.SunDir.z, 0f));
                _block.SetColor(SunColorId, instance.SunColor);
                l.Renderer.SetPropertyBlock(_block);
            }
        }

        private static void Deactivate(Instance instance)
        {
            instance.Active = false;
            if (instance.Root != null)
                instance.Root.SetActive(false);
        }

        /// <summary>Küçük, ayırmasız rastgele (VfxRandom iç türüne bağımlı olmamak için).</summary>
        private struct VfxRandomLite
        {
            private uint _state;
            public VfxRandomLite(uint seed) { _state = seed == 0 ? 1u : seed; }
            public float Next01()
            {
                _state ^= _state << 13;
                _state ^= _state >> 17;
                _state ^= _state << 5;
                return (_state & 0xFFFFFF) / 16777216f;
            }
        }
    }
}
