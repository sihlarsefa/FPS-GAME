using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.Infrastructure.Vfx
{
    /// <summary>
    /// Prosedürel görsel efektler (namlu alevi, mermi izi, isabet, kan, patlama, sis, toz).
    /// <para>
    /// Tüm efektler koddan kurulan ParticleSystem şablonlarından havuzlanır; "[GameVfx]" kök nesnesi
    /// DontDestroyOnLoad'dur. <see cref="Initialize"/> çağrılmasa da ilk kullanımda tembel olarak kurulur.
    /// Düzenleyici (oynatma dışı) modunda ve uygulama kapanırken çağrılar sessizce yok sayılır. Kamera yoksa
    /// mesafe ayıklaması yapılmaz, her şey yine çalışır. Sıcak yolda (ateş/isabet) yönetilen bellek ayırmaz.
    /// </para>
    /// </summary>
    public static class GameVfx
    {
        /// <summary>Aynı anda yanabilecek en fazla namlu alevi ışığı.</summary>
        public const int MaxMuzzleLights = 3;

        /// <summary>Aynı anda yanabilecek en fazla patlama ışığı.</summary>
        public const int MaxExplosionLights = 2;

        /// <summary>Mermi deliği + kan lekesi çıkartma üst sınırı (halka tampon, en eskisi taşınır).</summary>
        public const int MaxDecals = 150;

        /// <summary>Patlama yanık izi üst sınırı.</summary>
        public const int MaxScorchDecals = 16;

        /// <summary>Eşzamanlı mermi izi üst sınırı.</summary>
        public const int MaxTracers = 160;

        /// <summary>Çıkartmanın yüzey normali boyunca kaydırılması (z-çakışmasını önler).</summary>
        public const float DecalOffset = 0.01f;

        private const float MuzzleCullDistance = 600f;
        private const float MuzzleLightDistance = 90f;
        private const float TracerCullDistance = 450f;
        private const float ImpactCullDistance = 220f;
        private const float DecalCullDistance = 120f;
        private const float BloodCullDistance = 160f;
        private const float DustCullDistance = 350f;
        private const float ExplosionLightDistance = 400f;
        private const float BloodSpatterRange = 2.2f;
        private const float CameraSearchInterval = 0.5f;
        private const float InitRetryInterval = 5f;

        private static readonly Color MuzzleLightColor = new Color(1f, 0.74f, 0.42f, 1f);
        private static readonly Color ExplosionLightColor = new Color(1f, 0.62f, 0.3f, 1f);
        private static readonly Quaternion FaceUp = Quaternion.Euler(-90f, 0f, 0f);

        private static GameVfxHost _host;
        private static Transform _root;
        private static ParticleEffectPool[] _pools;
        private static DecalPool _decals;
        private static DecalPool _scorches;
        private static TracerPool _tracers;
        private static FlashLightPool _muzzleLights;
        private static FlashLightPool _explosionLights;
        private static bool _quitting;
        private static bool _eventsHooked;
        private static float _retryAfter;
        private static VfxRandom _rng = new VfxRandom(0x5EED1234u);

        private static Camera _camera;
        private static Transform _cameraTransform;
        private static bool _hasCamera;
        private static Vector3 _cameraPosition;
        private static float _nextCameraSearch;
        private static Camera[] _cameraBuffer = new Camera[8];

        // Classify → Impact eşlemesi (çıkartmayı hareketli nesneye bağlamak için).
        private static Collider _lastCollider;
        private static Vector3 _lastPoint;
        private static int _lastFrame = -1;

        /// <summary>Efekt kökü kurulmuş mu.</summary>
        public static bool IsInitialized => _host != null && _pools != null;

        /// <summary>
        /// Su yüzeyi yüksekliği (m). NaN (varsayılan) = sahnedeki WorldMetadata.WaterLevel otomatik kullanılır.
        /// Bu yüksekliğin altındaki arazi isabetleri su sıçraması olur.
        /// </summary>
        public static float WaterLevel
        {
            get => WaterLevelSource.Override;
            set => WaterLevelSource.Override = value;
        }

        /// <summary>Şu an yerleştirilmiş mermi deliği/kan lekesi sayısı.</summary>
        public static int DecalCount => _decals != null ? _decals.Count : 0;

        /// <summary>Efekt kökünü, şablonları ve havuzları kurar. Tekrar çağrılabilir (idempotent).</summary>
        public static void Initialize()
        {
            EnsureReady();
        }

        /// <summary>Namlu alevi: katkılı alev yaprakları + duman + kısa nokta ışık (en fazla 3 ışık).</summary>
        public static void MuzzleFlash(Vector3 position, Vector3 direction, float scale = 1f)
        {
            if (!IsFinite(position) || !EnsureReady())
                return;

            if (IsBeyond(position, MuzzleCullDistance))
                return;

            scale = Sanitize(scale, 1f, 0.1f, 5f);
            var forward = SafeDirection(direction, Vector3.forward);
            Spawn(EffectKind.MuzzleFlash, position, SurfaceRotation(forward), scale);

            if (!IsBeyond(position, MuzzleLightDistance))
            {
                var intensity = 2.2f + 1.4f * scale;
                var range = 4f + 3f * scale;
                _muzzleLights.Flash(position + forward * (0.12f * scale), MuzzleLightColor, intensity, range, 0.05f);
            }
        }

        /// <summary>Mermi izi: havuzlanmış katkılı LineRenderer, süre boyunca kuyruğu başa doğru kısalır.</summary>
        public static void Tracer(Vector3 from, Vector3 to, float duration = 0.05f, float width = 0.025f)
        {
            if (!IsFinite(from) || !IsFinite(to) || !EnsureReady())
                return;

            if ((to - from).sqrMagnitude < 1e-4f)
                return;

            if (_hasCamera && SegmentDistanceSqr(_cameraPosition, from, to) > TracerCullDistance * TracerCullDistance)
                return;

            duration = Sanitize(duration, 0.05f, 0.01f, 2f);
            width = Sanitize(width, 0.025f, 0.002f, 0.5f);
            _tracers.Spawn(from, to, duration, width, _hasCamera, _cameraPosition);
        }

        /// <summary>Yüzey isabeti (parçacık + mermi deliği). Et → kan, su → sıçrama, yaprak → çıkartmasız.</summary>
        public static void Impact(Vector3 point, Vector3 normal, SurfaceKind surface)
        {
            Impact(point, normal, surface, null);
        }

        /// <summary>
        /// <see cref="Impact(Vector3, Vector3, SurfaceKind)"/>; <paramref name="hitCollider"/> verilirse hareketli
        /// nesnelerde (Rigidbody / araç) mermi deliği o nesneye bağlanır. null ise aynı karede
        /// <see cref="Classify"/>'a verilen collider kullanılır, o da yoksa kısa bir yoklama ışını atılır.
        /// </summary>
        public static void Impact(Vector3 point, Vector3 normal, SurfaceKind surface, Collider hitCollider)
        {
            if (!IsFinite(point) || !EnsureReady())
                return;

            var n = SafeDirection(normal, Vector3.up);
            switch (surface)
            {
                case SurfaceKind.Flesh:
                    Blood(point, n);
                    return;
                case SurfaceKind.Water:
                    if (!IsBeyond(point, ImpactCullDistance))
                        WaterSplash(point, 1f);
                    return;
            }

            if (!IsBeyond(point, ImpactCullDistance))
                Spawn(ImpactKindFor(surface), point + n * 0.02f, SurfaceRotation(n), 1f);

            if (surface == SurfaceKind.Foliage || IsBeyond(point, DecalCullDistance))
                return;

            var collider = hitCollider != null ? hitCollider : RecentCollider(point);
            if (collider == null)
                collider = ProbeCollider(point, n);

            var size = DecalSize(surface);
            _decals.Place(point + n * DecalOffset, n, size, _rng.Range(0f, 360f), VfxMaterials.DecalFor(surface),
                AttachTarget(collider));
        }

        /// <summary>Kan: kırmızı bulut + damlacıklar; arkadaki duvara/zemine kan lekesi.</summary>
        public static void Blood(Vector3 point, Vector3 normal)
        {
            if (!IsFinite(point) || !EnsureReady())
                return;

            if (IsBeyond(point, BloodCullDistance))
                return;

            var n = SafeDirection(normal, Vector3.up);
            Spawn(EffectKind.Blood, point, SurfaceRotation(n), 1f);

            if (IsBeyond(point, DecalCullDistance) || _rng.Value() > 0.55f)
                return;

            // Kurşun yönü ≈ -normal: arkadaki yüzeye sıçrama (yalnızca dünya geometrisi).
            if (Physics.Raycast(point, -n, out var hit, BloodSpatterRange, GameLayers.WorldMask, QueryTriggerInteraction.Ignore))
            {
                var hitNormal = SafeDirection(hit.normal, Vector3.up);
                var size = _rng.Range(0.22f, 0.42f) * Mathf.Lerp(1f, 1.6f, hit.distance / BloodSpatterRange);
                _decals.Place(hit.point + hitNormal * DecalOffset, hitNormal, size, _rng.Range(0f, 360f),
                    VfxMaterials.DecalBlood, AttachTarget(hit.collider));
            }
        }

        /// <summary>Patlama: ateş topu, parlama, duman, enkaz, kıvılcım, toz halkası, şok dalgası, ışık, yanık izi.</summary>
        public static void Explosion(Vector3 position, float radius)
        {
            if (!IsFinite(position) || !EnsureReady())
                return;

            radius = Sanitize(radius, VfxEffectLibrary.ReferenceRadius, 0.5f, 60f);
            var scale = Mathf.Clamp(Mathf.Pow(radius / VfxEffectLibrary.ReferenceRadius, 0.85f), 0.3f, 4f);

            var underwater = WaterLevelSource.TryGet(out var waterLevel) && position.y < waterLevel + 0.25f
                                                                        && position.y > waterLevel - radius * 2f;
            if (underwater)
            {
                WaterSplash(new Vector3(position.x, waterLevel, position.z), 2.5f * scale);
                if (position.y > waterLevel - 0.5f)
                    Spawn(EffectKind.Dust, new Vector3(position.x, waterLevel, position.z), Quaternion.identity, scale * 1.5f);
            }
            else
            {
                Spawn(EffectKind.Explosion, position, Quaternion.identity, scale);
            }

            if (!IsBeyond(position, ExplosionLightDistance))
            {
                var intensity = underwater ? 3f : 7f + 2f * scale;
                _explosionLights.Flash(position + Vector3.up * (0.8f * scale), ExplosionLightColor, intensity,
                    radius * 3f + 6f, 0.45f);
            }

            if (underwater || IsBeyond(position, DecalCullDistance * 2f))
                return;

            var probe = 0.5f + radius * 0.6f;
            if (Physics.Raycast(position + Vector3.up * 0.5f, Vector3.down, out var hit, probe, GameLayers.GroundMask,
                    QueryTriggerInteraction.Ignore))
            {
                var hitNormal = SafeDirection(hit.normal, Vector3.up);
                var size = Mathf.Clamp(radius * 0.9f, 1.2f, 14f);
                _scorches.Place(hit.point + hitNormal * (DecalOffset * 1.5f), hitNormal, size, _rng.Range(0f, 360f),
                    VfxMaterials.Scorch, AttachTarget(hit.collider));
            }
        }

        /// <summary>
        /// Sis bulutu: uzun ömürlü büyük alfa parçacıklar. <paramref name="duration"/> sn boyunca yoğun kalır,
        /// ardından dağılır. <paramref name="radius"/> bulut yarıçapı (m).
        /// </summary>
        public static void SmokeCloud(Vector3 position, float radius, float duration)
        {
            if (!IsFinite(position) || !EnsureReady())
                return;

            radius = Sanitize(radius, VfxEffectLibrary.ReferenceRadius, 0.5f, 40f);
            duration = Sanitize(duration, 20f, 1f, 300f);
            var scale = radius / VfxEffectLibrary.ReferenceRadius;

            var lifeMax = Mathf.Clamp(duration * 0.5f, 1.5f, 8f);
            var lifeMin = lifeMax * 0.75f;
            var emitSeconds = Mathf.Max(0.5f, duration - lifeMax * 0.5f);
            var busy = emitSeconds + lifeMax + 0.5f;

            var pool = Pool(EffectKind.Smoke);
            var ps = pool != null ? pool.Prepare(position, Quaternion.identity, scale, busy) : null;
            if (ps == null)
                return;

            var main = ps.main;
            main.duration = emitSeconds;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            var emission = ps.emission;
            // Yaşayan parçacık sayısı ≈ oran × ömür: süreden bağımsız sabit yoğunluk.
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(52f / ((lifeMin + lifeMax) * 0.5f));

            var t = ps.transform;
            if (t.childCount > 0 && t.GetChild(0).TryGetComponent(out ParticleSystem skirt))
            {
                var skirtMain = skirt.main;
                skirtMain.startLifetime = new ParticleSystem.MinMaxCurve(Mathf.Min(4f, lifeMax * 0.6f), Mathf.Min(6f, lifeMax * 0.85f));
            }

            ps.Play(true);
        }

        /// <summary>Toz bulutu (iniş, rotor rüzgârı, araç). Su üstünde su serpintisine döner.</summary>
        public static void Dust(Vector3 position, float scale)
        {
            if (!IsFinite(position) || !EnsureReady())
                return;

            if (IsBeyond(position, DustCullDistance))
                return;

            scale = Sanitize(scale, 1f, 0.1f, 25f);
            if (WaterLevelSource.TryGet(out var waterLevel) && position.y < waterLevel + 0.3f && position.y > waterLevel - 3f)
            {
                WaterSplash(new Vector3(position.x, waterLevel, position.z), 0.6f * scale);
                return;
            }

            Spawn(EffectKind.Dust, position, Quaternion.identity, scale);
        }

        /// <summary>
        /// Yüzey türü: vuruş kutusu/oyuncu → Flesh, araç → Metal, arazi → Dirt (su seviyesi altında Water),
        /// görüntüleyici malzemesi / MaterialId / nesne adı ipuçları (Metal, Wood, Concrete, Foliage...).
        /// </summary>
        public static SurfaceKind Classify(Collider collider, Vector3 point)
        {
            SurfaceKind kind;
            try
            {
                kind = SurfaceClassifier.Classify(collider, point);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[GameVfx] Yüzey sınıflandırılamadı: " + exception.Message);
                kind = collider == null ? SurfaceKind.Default : SurfaceKind.Dirt;
            }

            _lastCollider = collider;
            _lastPoint = point;
            _lastFrame = Time.frameCount;
            return kind;
        }

        /// <summary>Tüm etkin efektleri, izleri, ışıkları ve çıkartmaları temizler (sahne geçişinde otomatik).</summary>
        public static void Clear()
        {
            if (_pools != null)
            {
                for (var i = 0; i < _pools.Length; i++)
                    _pools[i]?.StopAll();
            }

            _decals?.Clear();
            _scorches?.Clear();
            _tracers?.Clear();
            _muzzleLights?.Clear();
            _explosionLights?.Clear();
            _lastCollider = null;
            _lastFrame = -1;
        }

        // ================================================================== kurulum

        private static bool EnsureReady()
        {
            if (_host != null && _pools != null)
                return true;

            if (_quitting || !UnityEngine.Application.isPlaying)
                return false;

            if (Time.unscaledTime < _retryAfter)
                return false;

            return Build();
        }

        private static bool Build()
        {
            DestroyRoot();
            HookEvents();

            GameObject rootObject = null;
            try
            {
                rootObject = new GameObject("[GameVfx]");
                UnityEngine.Object.DontDestroyOnLoad(rootObject);
                var root = rootObject.transform;

                var templates = CreateChild(root, "Templates", false);
                var effects = CreateChild(root, "Effects", true);
                var decals = CreateChild(root, "Decals", true);
                var tracers = CreateChild(root, "Tracers", true);
                var lights = CreateChild(root, "Lights", true);

                var count = (int)EffectKind.Count;
                var pools = new ParticleEffectPool[count];
                for (var i = 0; i < count; i++)
                {
                    var kind = (EffectKind)i;
                    var template = VfxEffectLibrary.Build(kind, templates, out var lifetime);
                    if (template != null)
                        pools[i] = new ParticleEffectPool(kind, template, effects, VfxEffectLibrary.Capacity(kind), lifetime);
                }

                _decals = new DecalPool("BulletHole", decals, MaxDecals);
                _scorches = new DecalPool("Scorch", decals, MaxScorchDecals);
                _tracers = new TracerPool(tracers, MaxTracers);
                _muzzleLights = new FlashLightPool("MuzzleLight", lights, MaxMuzzleLights);
                _explosionLights = new FlashLightPool("ExplosionLight", lights, MaxExplosionLights);
                _root = root;
                _pools = pools;

                Prewarm(EffectKind.MuzzleFlash, 6);
                Prewarm(EffectKind.ImpactDirt, 3);
                Prewarm(EffectKind.ImpactConcrete, 3);
                Prewarm(EffectKind.ImpactMetal, 2);
                Prewarm(EffectKind.ImpactWood, 2);
                Prewarm(EffectKind.Blood, 3);
                Prewarm(EffectKind.Dust, 2);
                Prewarm(EffectKind.Explosion, 1);
                Prewarm(EffectKind.Smoke, 1);

                // AddComponent Awake'i hemen çalıştırır; host durumsuz olduğu için havuzlardan sonra eklenir.
                _host = rootObject.AddComponent<GameVfxHost>();
                RefreshCamera(true);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("[GameVfx] Efekt sistemi kurulamadı: " + exception);
                _host = null;
                _pools = null;
                _root = null;
                if (rootObject != null)
                    UnityEngine.Object.Destroy(rootObject);
                _retryAfter = Time.unscaledTime + InitRetryInterval;
                return false;
            }
        }

        private static Transform CreateChild(Transform parent, string name, bool active)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            if (!active)
                go.SetActive(false);
            return go.transform;
        }

        private static void Prewarm(EffectKind kind, int count)
        {
            Pool(kind)?.Prewarm(count);
        }

        private static void DestroyRoot()
        {
            if (_root != null)
                UnityEngine.Object.Destroy(_root.gameObject);
            _root = null;
            _host = null;
            _pools = null;
        }

        private static void HookEvents()
        {
            if (_eventsHooked)
                return;

            _eventsHooked = true;
            UnityEngine.Application.quitting -= OnQuitting;
            UnityEngine.Application.quitting += OnQuitting;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Alan yeniden yüklemesi kapalıyken oynatma oturumları arasında statik durum taşınmasın.
            UnityEngine.Application.quitting -= OnQuitting;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            _eventsHooked = false;
            _quitting = false;
            _retryAfter = 0f;
            _host = null;
            _root = null;
            _pools = null;
            _decals = null;
            _scorches = null;
            _tracers = null;
            _muzzleLights = null;
            _explosionLights = null;
            _camera = null;
            _cameraTransform = null;
            _hasCamera = false;
            _nextCameraSearch = 0f;
            _lastCollider = null;
            _lastFrame = -1;
            SurfaceClassifier.ClearCaches();
            WaterLevelSource.ResetAll();
        }

        private static void OnQuitting()
        {
            _quitting = true;
        }

        private static void OnActiveSceneChanged(Scene previous, Scene next)
        {
            Clear();
            SurfaceClassifier.ClearCaches();
            WaterLevelSource.Reset();
            _camera = null;
            _cameraTransform = null;
            _hasCamera = false;
            _nextCameraSearch = 0f;
        }

        internal static void HostLateUpdate(GameVfxHost host, float deltaTime)
        {
            if (host != _host)
                return;

            RefreshCamera(false);
            _muzzleLights?.Tick(deltaTime);
            _explosionLights?.Tick(deltaTime);
            _tracers?.Tick(deltaTime, _hasCamera, _cameraPosition);
        }

        internal static void HostDestroyed(GameVfxHost host)
        {
            if (host != _host && _host != null)
                return;

            // _root bilerek korunur: yalnızca bileşen silindiyse bir sonraki Build artık kökü yok eder.
            _host = null;
            _pools = null;
            _decals = null;
            _scorches = null;
            _tracers = null;
            _muzzleLights = null;
            _explosionLights = null;
        }

        // ================================================================== kamera

        private static void RefreshCamera(bool force)
        {
            var camera = _camera;
            if (camera == null || !camera.isActiveAndEnabled)
            {
                var now = Time.unscaledTime;
                if (force || now >= _nextCameraSearch)
                {
                    _nextCameraSearch = now + CameraSearchInterval;
                    camera = FindCamera();
                    _camera = camera;
                    _cameraTransform = camera != null ? camera.transform : null;
                }
                else
                {
                    camera = null;
                }
            }

            _hasCamera = camera != null && _cameraTransform != null;
            if (_hasCamera)
                _cameraPosition = _cameraTransform.position;
        }

        private static Camera FindCamera()
        {
            var main = Camera.main;
            if (main != null && main.isActiveAndEnabled)
                return main;

            var count = Camera.allCamerasCount;
            if (count <= 0)
                return null;

            if (_cameraBuffer.Length < count)
                _cameraBuffer = new Camera[Mathf.NextPowerOfTwo(count)];

            count = Camera.GetAllCameras(_cameraBuffer);
            Camera best = null;
            var defaultBit = 1 << GameLayers.Default;
            for (var i = 0; i < count; i++)
            {
                var candidate = _cameraBuffer[i];
                _cameraBuffer[i] = null;
                if (candidate == null || candidate.targetTexture != null || (candidate.cullingMask & defaultBit) == 0)
                    continue;
                if (best == null || candidate.depth > best.depth)
                    best = candidate;
            }

            return best;
        }

        // ================================================================== yardımcılar

        private static ParticleEffectPool Pool(EffectKind kind)
        {
            var pools = _pools;
            var index = (int)kind;
            if (pools == null || index < 0 || index >= pools.Length)
                return null;

            var pool = pools[index];
            return pool != null && pool.IsValid ? pool : null;
        }

        private static void Spawn(EffectKind kind, Vector3 position, Quaternion rotation, float scale)
        {
            Pool(kind)?.Spawn(position, rotation, scale);
        }

        private static void WaterSplash(Vector3 point, float scale)
        {
            if (WaterLevelSource.TryGet(out var level) && point.y < level + 0.05f && point.y > level - 8f)
                point.y = level;
            Spawn(EffectKind.ImpactWater, point + Vector3.up * 0.02f, FaceUp, Mathf.Clamp(scale, 0.2f, 12f));
        }

        private static EffectKind ImpactKindFor(SurfaceKind surface)
        {
            switch (surface)
            {
                case SurfaceKind.Dirt: return EffectKind.ImpactDirt;
                case SurfaceKind.Metal: return EffectKind.ImpactMetal;
                case SurfaceKind.Wood: return EffectKind.ImpactWood;
                case SurfaceKind.Water: return EffectKind.ImpactWater;
                case SurfaceKind.Foliage: return EffectKind.ImpactFoliage;
                case SurfaceKind.Flesh: return EffectKind.Blood;
                default: return EffectKind.ImpactConcrete;
            }
        }

        private static float DecalSize(SurfaceKind surface)
        {
            switch (surface)
            {
                case SurfaceKind.Metal: return _rng.Range(0.05f, 0.07f);
                case SurfaceKind.Wood: return _rng.Range(0.06f, 0.085f);
                case SurfaceKind.Dirt: return _rng.Range(0.09f, 0.13f);
                default: return _rng.Range(0.07f, 0.1f);
            }
        }

        /// <summary>Yönü (+Z) verilen eksene çeviren, etrafında rastgele döndürülmüş dönüş (dikey yönde de kararlı).</summary>
        private static Quaternion SurfaceRotation(Vector3 normal)
        {
            var up = Mathf.Abs(normal.y) > 0.98f ? Vector3.forward : Vector3.up;
            return Quaternion.LookRotation(normal, up) * Quaternion.AngleAxis(_rng.Range(0f, 360f), Vector3.forward);
        }

        /// <summary>Aynı karede Classify'a verilen collider (nokta eşleşirse).</summary>
        private static Collider RecentCollider(Vector3 point)
        {
            if (_lastFrame != Time.frameCount || _lastCollider == null)
                return null;
            return (point - _lastPoint).sqrMagnitude < 1e-4f ? _lastCollider : null;
        }

        /// <summary>Collider bilinmiyorsa yüzeyin hemen önünden kısa bir ışınla bulur (araç gövdesi vb.).</summary>
        private static Collider ProbeCollider(Vector3 point, Vector3 normal)
        {
            const float back = 0.05f;
            return Physics.Raycast(point + normal * back, -normal, out var hit, back * 3f, GameLayers.GroundMask,
                QueryTriggerInteraction.Ignore)
                ? hit.collider
                : null;
        }

        /// <summary>Hareket edebilen nesneler (Rigidbody, araç katmanı) için çıkartmanın bağlanacağı dönüşüm.</summary>
        private static Transform AttachTarget(Collider collider)
        {
            if (collider == null)
                return null;

            var body = collider.attachedRigidbody;
            if (body != null)
                return body.transform;

            return collider.gameObject.layer == GameLayers.Vehicle ? collider.transform : null;
        }

        private static bool IsBeyond(Vector3 position, float distance)
        {
            return _hasCamera && (position - _cameraPosition).sqrMagnitude > distance * distance;
        }

        private static float SegmentDistanceSqr(Vector3 point, Vector3 a, Vector3 b)
        {
            var ab = b - a;
            var lengthSqr = ab.sqrMagnitude;
            if (lengthSqr < 1e-8f)
                return (point - a).sqrMagnitude;

            var t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / lengthSqr);
            return (point - (a + ab * t)).sqrMagnitude;
        }

        private static Vector3 SafeDirection(Vector3 direction, Vector3 fallback)
        {
            var sqr = direction.sqrMagnitude;
            if (sqr < 1e-8f || float.IsNaN(sqr) || float.IsInfinity(sqr))
                return fallback;
            return direction / Mathf.Sqrt(sqr);
        }

        private static float Sanitize(float value, float fallback, float min, float max)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
                return fallback;
            return Mathf.Clamp(value, min, max);
        }

        private static bool IsFinite(Vector3 v)
        {
            var sum = v.x + v.y + v.z;
            return !float.IsNaN(sum) && !float.IsInfinity(sum);
        }
    }
}
