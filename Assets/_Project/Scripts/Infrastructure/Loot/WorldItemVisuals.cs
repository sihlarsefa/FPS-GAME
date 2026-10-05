using System;
using System.Collections.Generic;
using Project.Application.Catalogs;
using Project.Core.Domain;
using Project.Infrastructure.Rendering;
using Project.Infrastructure.Weapons;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Project.Infrastructure.Loot
{
    /// <summary>
    /// Bir eşya türünün paylaşılan yer modeli: tek Mesh (son alt ağ = soluk vurgu halkası) + malzemeler.
    /// Pivot zeminde, model XZ'de ortalanmış ve en alt noktası y=0'dadır.
    /// </summary>
    public sealed class WorldItemVisual
    {
        internal WorldItemVisual(string key, Mesh mesh, Material[] materials, Material[] modelMaterials, Bounds bounds,
            float ringRadius, GameObject prototype)
        {
            Key = key;
            Mesh = mesh;
            Materials = materials;
            ModelMaterials = modelMaterials;
            Bounds = bounds;
            RingRadius = ringRadius;
            Prototype = prototype;
            _needsPrototype = prototype != null;
        }

        public string Key { get; }

        /// <summary>Model + vurgu halkası (son alt ağ). Paylaşılır — değiştirmeyin.</summary>
        public Mesh Mesh { get; }

        /// <summary>Halka dahil tüm alt ağların malzemeleri.</summary>
        public Material[] Materials { get; }

        /// <summary>Halkasız malzemeler (son alt ağ çizilmez) — önizleme/sandık içi gösterim için.</summary>
        public Material[] ModelMaterials { get; }

        /// <summary>Modelin yerel sınırları (halka hariç).</summary>
        public Bounds Bounds { get; }

        public float RingRadius { get; }

        /// <summary>Ağ okunamadığı için birleştirilemeyen silah modeli (etkin değil). Genelde null.</summary>
        public GameObject Prototype { get; }

        private readonly bool _needsPrototype;

        /// <summary>Paylaşılan kaynaklar hâlâ geçerli mi (sahne/varlık boşaltması sonrası yeniden üretim için).</summary>
        internal bool IsAlive
        {
            get
            {
                if (Mesh == null || Materials == null || (_needsPrototype && Prototype == null))
                    return false;

                for (var i = 0; i < Materials.Length; i++)
                {
                    if (Materials[i] == null)
                        return false;
                }

                return true;
            }
        }
    }

    /// <summary>
    /// Yerdeki eşyaların prosedürel düşük poligonlu modelleri (kategoriye göre): silahlar WeaponModelFactory ile
    /// (dünya ölçeği, yan yatırılmış), zeytin yeşili mühimmat kutuları (kalibre renk şeridi), beyaz sıhhiye kutuları
    /// (kırmızı Kızılay hilali), takviye kutuları/şişeleri, el ve sis bombaları, çelik yelek, kask ve sırt çantası.
    /// Her eşya türü bir kez üretilip önbelleğe alınır; yerdeki her eşya tek MeshRenderer kullanır.
    /// </summary>
    public static class WorldItemVisuals
    {
        /// <summary>Bakış ışını için tetik kutusunun en küçük kenarı (m).</summary>
        public const float MinColliderSize = 0.8f;

        public const float MinColliderHeight = 0.45f;

        private const float RingLift = 0.018f;
        private const float RingWidth = 0.035f;

        private static readonly Dictionary<string, WorldItemVisual> Cache = new(StringComparer.Ordinal);
        private static readonly LootMeshBuilder Builder = new();
        private static readonly List<MeshRenderer> TmpRenderers = new(32);

        private static Mesh _focusRingMesh;
        private static GameObject _focusMarker;
        private static GameObject _prototypeHolder;

        // ---- Palet
        private static readonly Color Olive = new(0.30f, 0.33f, 0.20f);
        private static readonly Color OliveDark = new(0.20f, 0.23f, 0.14f);
        private static readonly Color OliveLight = new(0.40f, 0.42f, 0.27f);
        private static readonly Color Khaki = new(0.52f, 0.47f, 0.33f);
        private static readonly Color Tan = new(0.58f, 0.52f, 0.38f);
        private static readonly Color NearBlack = new(0.08f, 0.08f, 0.08f);
        private static readonly Color Webbing = new(0.14f, 0.15f, 0.11f);
        private static readonly Color SteelGray = new(0.36f, 0.37f, 0.38f);
        private static readonly Color Brass = new(0.72f, 0.56f, 0.24f);
        private static readonly Color MedWhite = new(0.91f, 0.91f, 0.88f);
        private static readonly Color OffWhite = new(0.86f, 0.84f, 0.78f);
        private static readonly Color CrescentRed = new(0.80f, 0.06f, 0.07f);
        private static readonly Color Stencil = new(0.86f, 0.80f, 0.52f);
        private static readonly Color HighlightColor = new(1f, 0.88f, 0.5f, 0.2f);
        private static readonly Color FocusColor = new(1f, 0.9f, 0.55f, 0.6f);

        /// <summary>Eşyanın şablonu (önbellekli). Hiçbir zaman null dönmez.</summary>
        public static WorldItemVisual Get(LootItemData item) => Get(item.ItemId, item.Category);

        /// <summary>Kimlik ve kategoriye göre şablon (önbellekli). Hiçbir zaman null dönmez.</summary>
        public static WorldItemVisual Get(string itemId, ItemCategory category)
        {
            if (string.IsNullOrEmpty(itemId) || (!ItemCatalog.Contains(itemId) && !WeaponCatalog.Contains(itemId)))
                itemId = null;

            var key = itemId ?? CategoryKey(category);
            if (Cache.TryGetValue(key, out var cached) && cached != null && cached.IsAlive)
                return cached;

            WorldItemVisual visual;
            try
            {
                visual = Create(key, itemId, category);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                visual = null;
            }

            visual ??= CreateGeneric(key);
            Cache[key] = visual;
            return visual;
        }

        /// <summary>Halkasız bağımsız model (önizleme, sergi). Çarpıştırıcısızdır.</summary>
        public static GameObject CreateModel(LootItemData item, Transform parent, int layer)
        {
            var visual = Get(item);
            var go = new GameObject("Model_" + (item.ItemId ?? "Esya"));
            go.layer = layer;
            if (parent != null)
                go.transform.SetParent(parent, false);

            go.AddComponent<MeshFilter>().sharedMesh = visual.Mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = visual.ModelMaterials;
            renderer.shadowCastingMode = ShadowCastingMode.On;

            if (visual.Prototype != null)
            {
                var copy = Object.Instantiate(visual.Prototype, go.transform, false);
                copy.SetActive(true);
                GameLayers.SetLayerRecursively(copy, layer);
            }

            return go;
        }

        /// <summary>Önbelleği temizler (ağlar yok edilir). Sahne/kütüphane değişiminde çağrılabilir.</summary>
        public static void ClearCache()
        {
            foreach (var pair in Cache)
            {
                if (pair.Value?.Mesh != null)
                    SafeDestroy(pair.Value.Mesh);
                if (pair.Value?.Prototype != null)
                    SafeDestroy(pair.Value.Prototype);
            }

            Cache.Clear();
        }

        // ------------------------------------------------------------------ Focus marker

        /// <summary>Oyuncunun baktığı eşyanın altındaki parlak halka (tek, paylaşılan). null → gizle.</summary>
        internal static void ShowFocus(LootPickupComponent pickup)
        {
            if (pickup == null || !pickup.IsAvailable)
            {
                if (_focusMarker != null)
                    _focusMarker.SetActive(false);
                return;
            }

            if (_focusMarker == null)
            {
                _focusMarker = new GameObject("[Eşya Odak Halkası]") { layer = GameLayers.IgnoreRaycast };
                _focusMarker.AddComponent<MeshFilter>().sharedMesh = FocusRingMesh();
                var renderer = _focusMarker.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = MaterialLibrary.Transparent(FocusColor, true);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            var t = pickup.transform;
            var radius = Mathf.Max(0.3f, pickup.RingRadius + 0.05f);
            _focusMarker.transform.SetPositionAndRotation(t.position + t.up * (RingLift + 0.004f), t.rotation);
            _focusMarker.transform.localScale = new Vector3(radius, 1f, radius);
            if (!_focusMarker.activeSelf)
                _focusMarker.SetActive(true);
        }

        private static Mesh FocusRingMesh()
        {
            if (_focusRingMesh != null)
                return _focusRingMesh;

            Builder.Clear();
            var material = MaterialLibrary.Transparent(FocusColor, true);
            Builder.Ring(Vector3.zero, 0.86f, 1f, Quaternion.identity, 40, material);
            _focusRingMesh = Builder.Build("LootFocusRing", out _);
            Builder.Clear();
            return _focusRingMesh;
        }

        // ------------------------------------------------------------------ Creation

        private static string CategoryKey(ItemCategory category) => "#" + category;

        private static WorldItemVisual Create(string key, string itemId, ItemCategory category)
        {
            if (itemId != null && ItemCatalog.TryGet(itemId, out var definition))
                category = definition.Category;

            if (category == ItemCategory.Weapon && itemId != null)
                return CreateWeapon(key, itemId);

            Builder.Clear();
            switch (category)
            {
                case ItemCategory.Ammunition:
                    BuildAmmo(itemId);
                    break;
                case ItemCategory.Medical:
                    BuildMedical(itemId);
                    break;
                case ItemCategory.Boost:
                    BuildBoost(itemId);
                    break;
                case ItemCategory.Throwable:
                    BuildThrowable(itemId);
                    break;
                case ItemCategory.Armor:
                    BuildVest(LevelOf(itemId));
                    break;
                case ItemCategory.Helmet:
                    BuildHelmet(LevelOf(itemId));
                    break;
                case ItemCategory.Backpack:
                    BuildBackpack(LevelOf(itemId));
                    break;
                case ItemCategory.Weapon:
                    BuildFallbackWeapon(WeaponCategory.AssaultRifle);
                    break;
                default:
                    BuildCrate();
                    break;
            }

            return Finish(key, null);
        }

        private static WorldItemVisual CreateGeneric(string key)
        {
            Builder.Clear();
            BuildCrate();
            return Finish(key, null);
        }

        /// <summary>Oluşturucudaki modeli zemine oturtur, halkayı ekler ve şablonu üretir.</summary>
        private static WorldItemVisual Finish(string key, GameObject prototype, Bounds? overrideBounds = null)
        {
            Builder.Matrix = Matrix4x4.identity;
            Bounds bounds;
            if (Builder.VertexCount > 0)
            {
                Builder.RestOnGround();
                bounds = Builder.ComputeBounds();
            }
            else
            {
                bounds = overrideBounds ?? new Bounds(new Vector3(0f, 0.15f, 0f), new Vector3(0.4f, 0.3f, 0.4f));
            }

            var footprint = Mathf.Max(bounds.size.x, bounds.size.z);
            var ringRadius = Mathf.Clamp(footprint * 0.5f + 0.08f, 0.28f, 0.7f);
            var highlight = MaterialLibrary.Transparent(HighlightColor, true);
            Builder.Ring(new Vector3(bounds.center.x, RingLift, bounds.center.z), ringRadius - RingWidth, ringRadius,
                Quaternion.identity, 32, highlight);

            var mesh = Builder.Build("Loot_" + key, out var materials);
            Builder.Clear();
            if (mesh == null)
                return null;

            // Halka her zaman son alt ağdır (vurgu malzemesi yalnızca halkada kullanılır).
            var modelMaterials = materials;
            if (materials.Length > 0 && ReferenceEquals(materials[materials.Length - 1], highlight))
            {
                modelMaterials = new Material[materials.Length - 1];
                Array.Copy(materials, modelMaterials, modelMaterials.Length);
            }

            return new WorldItemVisual(key, mesh, materials, modelMaterials, bounds, ringRadius, prototype);
        }

        private static int LevelOf(string itemId)
        {
            if (itemId != null && ItemCatalog.TryGet(itemId, out var definition))
                return Mathf.Clamp(definition.Level, 1, 3);
            return 1;
        }

        private static Material Lit(Color color, float smoothness = 0.2f, float metallic = 0f) =>
            MaterialLibrary.Lit(color, smoothness, metallic);

        private static Material Mat(MaterialId id) => MaterialLibrary.Get(id);

        private static readonly Quaternion FaceForward = Quaternion.FromToRotation(Vector3.up, Vector3.forward);
        private static readonly Quaternion AlongX = Quaternion.FromToRotation(Vector3.up, Vector3.right);
        private static readonly Quaternion AlongZ = Quaternion.FromToRotation(Vector3.up, Vector3.forward);

        // ------------------------------------------------------------------ Weapons

        private static WorldItemVisual CreateWeapon(string key, string weaponId)
        {
            var definition = WeaponCatalog.Get(weaponId);
            GameObject temp = null;
            try
            {
                if (definition != null)
                {
                    temp = new GameObject("LootWeaponBake");
                    temp.transform.position = new Vector3(0f, -10000f, 0f);
                    var model = WeaponModelFactory.Build(definition, temp.transform, GameLayers.Loot, false, out _);
                    if (model != null)
                    {
                        var visual = BakeWeapon(key, temp.transform, model);
                        if (visual != null)
                            return visual;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Loot] '{weaponId}' silah modeli üretilemedi, basit model kullanılıyor: {e.Message}");
            }
            finally
            {
                if (temp != null)
                {
                    temp.SetActive(false);
                    SafeDestroy(temp);
                }
            }

            Builder.Clear();
            BuildFallbackWeapon(definition != null ? definition.Category : WeaponCategory.AssaultRifle);
            return Finish(key, null);
        }

        /// <summary>Silah hiyerarşisini tek ağa birleştirir (yan yatırılmış). Okunamayan ağ varsa prototip saklanır.</summary>
        private static WorldItemVisual BakeWeapon(string key, Transform bakeRoot, GameObject model)
        {
            // Silah +Z namlu, +Y üst kabulüyle sağ yanı yukarı bakacak şekilde yatırılır.
            var layDown = Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, 90f));
            var rootInverse = bakeRoot.worldToLocalMatrix;

            Builder.Clear();
            TmpRenderers.Clear();
            model.GetComponentsInChildren(true, TmpRenderers);
            var readable = TmpRenderers.Count > 0;
            for (var i = 0; i < TmpRenderers.Count && readable; i++)
            {
                var renderer = TmpRenderers[i];
                if (renderer == null || !renderer.enabled || !IsActiveUnder(renderer.transform, bakeRoot))
                    continue;

                if (!renderer.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null)
                    continue;

                var matrix = layDown * rootInverse * renderer.transform.localToWorldMatrix;
                if (!Builder.AppendMesh(filter.sharedMesh, matrix, renderer.sharedMaterials))
                    readable = false;
            }

            TmpRenderers.Clear();
            if (readable && Builder.VertexCount > 0)
                return Finish(key, null);

            Builder.Clear();
            if (!UnityEngine.Application.isPlaying)
                return null;

            // Okunamayan ağlar: hiyerarşiyi prototip olarak sakla (her eşyada kopyalanır).
            if (_prototypeHolder == null)
            {
                _prototypeHolder = new GameObject("[Loot Prototypes]") { hideFlags = HideFlags.HideInHierarchy };
                _prototypeHolder.SetActive(false);
                Object.DontDestroyOnLoad(_prototypeHolder);
            }

            var prototype = model;
            prototype.transform.SetParent(_prototypeHolder.transform, false);
            prototype.transform.localPosition = Vector3.zero;
            prototype.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            var bounds = ComputeRendererBounds(prototype.transform, out var ok);
            if (!ok)
                bounds = new Bounds(new Vector3(0f, 0.04f, 0f), new Vector3(0.1f, 0.08f, 0.9f));

            // Prototipi zemine oturt.
            prototype.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
            bounds.center += prototype.transform.localPosition;
            prototype.SetActive(false);

            Builder.Clear();
            return Finish(key, prototype, bounds);
        }

        private static bool IsActiveUnder(Transform t, Transform root)
        {
            while (t != null && t != root)
            {
                if (!t.gameObject.activeSelf)
                    return false;
                t = t.parent;
            }

            return true;
        }

        private static Bounds ComputeRendererBounds(Transform root, out bool any)
        {
            any = false;
            var bounds = new Bounds();
            TmpRenderers.Clear();
            root.GetComponentsInChildren(true, TmpRenderers);
            var toLocal = root.parent != null ? root.parent.worldToLocalMatrix : Matrix4x4.identity;
            for (var i = 0; i < TmpRenderers.Count; i++)
            {
                var r = TmpRenderers[i];
                if (r == null || !r.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null)
                    continue;

                var local = filter.sharedMesh.bounds;
                var m = toLocal * r.transform.localToWorldMatrix;
                for (var c = 0; c < 8; c++)
                {
                    var corner = local.center + Vector3.Scale(local.extents,
                        new Vector3((c & 1) == 0 ? -1f : 1f, (c & 2) == 0 ? -1f : 1f, (c & 4) == 0 ? -1f : 1f));
                    var p = m.MultiplyPoint3x4(corner);
                    if (!any)
                    {
                        bounds = new Bounds(p, Vector3.zero);
                        any = true;
                    }
                    else
                    {
                        bounds.Encapsulate(p);
                    }
                }
            }

            TmpRenderers.Clear();
            return bounds;
        }

        /// <summary>WeaponModelFactory yoksa/başarısızsa kullanılan basit silah silueti (yan yatmış).</summary>
        private static void BuildFallbackWeapon(WeaponCategory category)
        {
            float length;
            switch (category)
            {
                case WeaponCategory.Pistol: length = 0.22f; break;
                case WeaponCategory.Smg: length = 0.55f; break;
                case WeaponCategory.Shotgun: length = 1.0f; break;
                case WeaponCategory.Dmr: length = 1.05f; break;
                case WeaponCategory.Sniper: length = 1.15f; break;
                case WeaponCategory.Lmg: length = 1.1f; break;
                default: length = 0.95f; break;
            }

            var metal = Mat(MaterialId.GunMetal);
            var polymer = Mat(MaterialId.GunPolymer);
            Builder.Matrix = Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, 90f));

            if (category == WeaponCategory.Pistol)
            {
                Builder.Box(new Vector3(0f, 0.03f, 0.02f), new Vector3(0.03f, 0.035f, 0.19f), metal);
                Builder.Box(new Vector3(0f, -0.03f, -0.04f), new Vector3(0.028f, 0.1f, 0.045f), Quaternion.Euler(-12f, 0f, 0f), polymer);
                Builder.Box(new Vector3(0f, -0.005f, 0.03f), new Vector3(0.026f, 0.02f, 0.06f), polymer);
                return;
            }

            var receiver = length * 0.32f;
            Builder.Box(new Vector3(0f, 0f, 0f), new Vector3(0.055f, 0.085f, receiver), metal);
            Builder.Box(new Vector3(0f, 0.005f, receiver * 0.5f + length * 0.12f), new Vector3(0.05f, 0.06f, length * 0.24f), polymer);
            Builder.Cylinder(new Vector3(0f, 0.015f, receiver * 0.5f + length * 0.3f), 0.011f, length * 0.36f, AlongZ, 8, metal);
            Builder.Box(new Vector3(0f, -0.01f, -receiver * 0.5f - length * 0.13f), new Vector3(0.045f, 0.1f, length * 0.26f), polymer);
            Builder.Box(new Vector3(0f, -0.085f, -receiver * 0.25f), new Vector3(0.035f, 0.1f, 0.04f), Quaternion.Euler(-15f, 0f, 0f), polymer);

            switch (category)
            {
                case WeaponCategory.Shotgun:
                    Builder.Cylinder(new Vector3(0f, -0.03f, receiver * 0.5f + length * 0.18f), 0.014f, length * 0.32f, AlongZ, 8, metal);
                    break;
                case WeaponCategory.Lmg:
                    Builder.Box(new Vector3(0f, -0.08f, receiver * 0.15f), new Vector3(0.11f, 0.1f, 0.1f), Mat(MaterialId.MetalDark));
                    Builder.Box(new Vector3(0.02f, -0.06f, receiver * 0.5f + length * 0.36f), new Vector3(0.01f, 0.12f, 0.012f), metal);
                    break;
                default:
                    Builder.Box(new Vector3(0f, -0.1f, receiver * 0.2f), new Vector3(0.03f, 0.14f, 0.06f), Quaternion.Euler(12f, 0f, 0f), metal);
                    break;
            }

            if (category == WeaponCategory.Sniper || category == WeaponCategory.Dmr)
            {
                Builder.Cylinder(new Vector3(0f, 0.08f, 0f), 0.022f, receiver * 1.1f, AlongZ, 10, polymer);
                Builder.Box(new Vector3(0f, 0.05f, 0f), new Vector3(0.02f, 0.03f, 0.05f), metal);
            }
        }

        // ------------------------------------------------------------------ Ammo

        private static void BuildAmmo(string itemId)
        {
            Vector3 size;
            Color stripe;
            switch (itemId)
            {
                case ItemIds.Ammo9:
                    size = new Vector3(0.26f, 0.13f, 0.16f);
                    stripe = new Color(0.92f, 0.76f, 0.12f);
                    break;
                case ItemIds.Ammo556:
                    size = new Vector3(0.30f, 0.16f, 0.16f);
                    stripe = new Color(0.30f, 0.68f, 0.26f);
                    break;
                case ItemIds.Ammo12:
                    size = new Vector3(0.26f, 0.15f, 0.20f);
                    stripe = new Color(0.95f, 0.48f, 0.10f);
                    break;
                default: // 7.62 ve bilinmeyen
                    size = new Vector3(0.34f, 0.18f, 0.18f);
                    stripe = new Color(0.78f, 0.14f, 0.11f);
                    break;
            }

            var body = Lit(Olive, 0.25f);
            var lid = Lit(OliveDark, 0.25f);
            var metal = Lit(SteelGray, 0.45f, 0.6f);
            var stencil = Lit(Stencil, 0.1f);
            var w = size.x;
            var h = size.y;
            var d = size.z;

            Builder.Box(new Vector3(0f, h * 0.44f, 0f), new Vector3(w, h * 0.88f, d), body);
            Builder.Box(new Vector3(0f, h * 0.94f, 0f), new Vector3(w + 0.012f, h * 0.12f, d + 0.012f), lid);
            // Kalibre renk şeridi (dört yanda bant).
            Builder.Box(new Vector3(0f, h * 0.58f, 0f), new Vector3(w + 0.006f, h * 0.13f, d + 0.006f), Lit(stripe, 0.3f));
            // Taşıma kulpu.
            Builder.Box(new Vector3(0f, h + 0.022f, 0f), new Vector3(w * 0.42f, 0.014f, 0.026f), Lit(NearBlack, 0.3f));
            Builder.Box(new Vector3(-w * 0.2f, h + 0.01f, 0f), new Vector3(0.012f, 0.024f, 0.02f), metal);
            Builder.Box(new Vector3(w * 0.2f, h + 0.01f, 0f), new Vector3(0.012f, 0.024f, 0.02f), metal);
            // Kilit mandalı.
            Builder.Box(new Vector3(0f, h * 0.8f, d * 0.5f + 0.008f), new Vector3(0.05f, 0.045f, 0.012f), metal);
            // Şablon yazı izleri (ön ve arka).
            Builder.Box(new Vector3(-w * 0.16f, h * 0.3f, d * 0.5f + 0.003f), new Vector3(w * 0.42f, 0.02f, 0.004f), stencil);
            Builder.Box(new Vector3(w * 0.22f, h * 0.3f, d * 0.5f + 0.003f), new Vector3(w * 0.2f, 0.02f, 0.004f), stencil);
            Builder.Box(new Vector3(0f, h * 0.3f, -d * 0.5f - 0.003f), new Vector3(w * 0.5f, 0.02f, 0.004f), stencil);

            if (itemId == ItemIds.Ammo12)
            {
                // Kutunun üstünde iki fişek.
                var shell = Lit(new Color(0.75f, 0.12f, 0.1f), 0.35f);
                var brass = Lit(Brass, 0.55f, 0.8f);
                for (var i = 0; i < 2; i++)
                {
                    var z = (i == 0 ? -1f : 1f) * 0.035f;
                    Builder.Cylinder(new Vector3(0.01f, h + 0.06f, z), 0.011f, 0.06f, AlongX, 8, shell);
                    Builder.Cylinder(new Vector3(-0.035f, h + 0.06f, z), 0.0115f, 0.018f, AlongX, 8, brass);
                }
            }
        }

        // ------------------------------------------------------------------ Medical (Kızılay)

        private static void BuildMedical(string itemId)
        {
            var white = Lit(MedWhite, 0.35f);
            var red = Lit(CrescentRed, 0.3f);
            var dark = Lit(NearBlack, 0.3f);

            switch (itemId)
            {
                case ItemIds.Bandage:
                {
                    // Küçük beyaz paket + sargı rulosu.
                    var w = 0.2f;
                    var h = 0.07f;
                    var d = 0.14f;
                    Builder.Box(new Vector3(-0.04f, h * 0.5f, 0f), new Vector3(w, h, d), white);
                    Builder.Crescent(new Vector3(-0.04f, h + 0.002f, 0f), 0.04f, Quaternion.identity, 20, red);
                    Builder.Cylinder(new Vector3(0.12f, 0.036f, 0.01f), 0.036f, 0.09f, AlongZ, 12, Lit(OffWhite, 0.15f));
                    Builder.Box(new Vector3(0.12f, 0.004f, 0.075f), new Vector3(0.05f, 0.006f, 0.05f), Lit(OffWhite, 0.15f));
                    break;
                }

                case ItemIds.MedKit:
                {
                    // Büyük sıhhiye çantası: beyaz, kırmızı bant, iki yanda hilal, kulp ve mandallar.
                    var w = 0.5f;
                    var h = 0.24f;
                    var d = 0.32f;
                    Builder.Box(new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d), white);
                    Builder.Box(new Vector3(0f, h * 0.62f, 0f), new Vector3(w + 0.006f, 0.022f, d + 0.006f), red);
                    Builder.Crescent(new Vector3(0f, h + 0.002f, 0f), 0.1f, Quaternion.identity, 28, red);
                    Builder.Crescent(new Vector3(0f, h * 0.36f, d * 0.5f + 0.002f), 0.06f, FaceForward, 24, red);
                    Builder.Box(new Vector3(0f, h + 0.03f, -d * 0.3f), new Vector3(0.16f, 0.018f, 0.03f), dark);
                    Builder.Box(new Vector3(-0.07f, h + 0.014f, -d * 0.3f), new Vector3(0.014f, 0.028f, 0.022f), dark);
                    Builder.Box(new Vector3(0.07f, h + 0.014f, -d * 0.3f), new Vector3(0.014f, 0.028f, 0.022f), dark);
                    var metal = Lit(SteelGray, 0.45f, 0.6f);
                    Builder.Box(new Vector3(-w * 0.32f, h * 0.8f, d * 0.5f + 0.008f), new Vector3(0.04f, 0.04f, 0.012f), metal);
                    Builder.Box(new Vector3(w * 0.32f, h * 0.8f, d * 0.5f + 0.008f), new Vector3(0.04f, 0.04f, 0.012f), metal);
                    break;
                }

                default: // İlk Yardım Çantası ve bilinmeyen tıbbi
                {
                    var w = 0.36f;
                    var h = 0.16f;
                    var d = 0.26f;
                    Builder.Box(new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d), white);
                    Builder.Box(new Vector3(0f, h * 0.97f, 0f), new Vector3(w + 0.008f, h * 0.08f, d + 0.008f), Lit(OffWhite, 0.3f));
                    Builder.Crescent(new Vector3(0f, h + 0.008f, 0f), 0.08f, Quaternion.identity, 24, red);
                    Builder.Crescent(new Vector3(0f, h * 0.45f, d * 0.5f + 0.002f), 0.05f, FaceForward, 20, red);
                    Builder.Box(new Vector3(0f, h + 0.026f, -d * 0.32f), new Vector3(0.12f, 0.016f, 0.026f), Lit(SteelGray, 0.4f, 0.3f));
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ Boost

        private static void BuildBoost(string itemId)
        {
            if (itemId == ItemIds.Painkiller)
            {
                // Kehribar ilaç şişesi + beyaz kapak + blister kutusu.
                Builder.Cylinder(new Vector3(-0.04f, 0.055f, 0f), 0.042f, 0.11f, Quaternion.identity, 12, Lit(new Color(0.72f, 0.42f, 0.1f), 0.6f));
                Builder.Cylinder(new Vector3(-0.04f, 0.126f, 0f), 0.045f, 0.032f, Quaternion.identity, 12, Lit(MedWhite, 0.3f));
                Builder.Box(new Vector3(-0.04f, 0.055f, 0.042f), new Vector3(0.05f, 0.05f, 0.004f), Lit(MedWhite, 0.2f));
                Builder.Box(new Vector3(0.07f, 0.016f, 0.01f), new Vector3(0.13f, 0.032f, 0.08f), Lit(MedWhite, 0.3f));
                Builder.Box(new Vector3(0.07f, 0.0165f, 0.01f), new Vector3(0.132f, 0.012f, 0.082f), Lit(CrescentRed, 0.3f));
                return;
            }

            // Enerji içeceği: biri dik, biri yatık iki kutu.
            var can = Lit(new Color(0.08f, 0.22f, 0.62f), 0.55f, 0.3f);
            var rim = Lit(new Color(0.75f, 0.76f, 0.78f), 0.6f, 0.8f);
            var band = Lit(new Color(0.9f, 0.72f, 0.1f), 0.5f, 0.2f);
            Builder.Cylinder(new Vector3(-0.04f, 0.08f, 0f), 0.044f, 0.15f, Quaternion.identity, 14, can);
            Builder.Cylinder(new Vector3(-0.04f, 0.161f, 0f), 0.04f, 0.012f, Quaternion.identity, 14, rim);
            Builder.Cylinder(new Vector3(-0.04f, 0.004f, 0f), 0.04f, 0.008f, Quaternion.identity, 14, rim);
            Builder.Cylinder(new Vector3(-0.04f, 0.1f, 0f), 0.0445f, 0.03f, Quaternion.identity, 14, band);
            Builder.Cylinder(new Vector3(0.08f, 0.044f, 0.03f), 0.044f, 0.15f, Quaternion.Euler(0f, 30f, 0f) * AlongX, 14, can);
            Builder.Cylinder(new Vector3(0.08f, 0.044f, 0.03f), 0.0445f, 0.03f, Quaternion.Euler(0f, 30f, 0f) * AlongX, 14, band);
        }

        // ------------------------------------------------------------------ Throwables

        private static void BuildThrowable(string itemId)
        {
            var metal = Lit(SteelGray, 0.45f, 0.7f);
            if (itemId == ItemIds.SmokeGrenade)
            {
                var body = Lit(new Color(0.36f, 0.4f, 0.34f), 0.3f, 0.2f);
                Builder.Cylinder(new Vector3(0f, 0.1f, 0f), 0.06f, 0.2f, Quaternion.identity, 14, body);
                Builder.Cylinder(new Vector3(0f, 0.165f, 0f), 0.0605f, 0.035f, Quaternion.identity, 14, Lit(MedWhite, 0.3f));
                Builder.Cylinder(new Vector3(0f, 0.22f, 0f), 0.022f, 0.04f, Quaternion.identity, 10, metal);
                Builder.Box(new Vector3(0.035f, 0.17f, 0f), new Vector3(0.012f, 0.1f, 0.022f), Quaternion.Euler(0f, 0f, -8f), metal);
                Builder.Cylinder(new Vector3(-0.03f, 0.225f, 0f), 0.016f, 0.004f, Quaternion.Euler(0f, 0f, 90f), 10, metal);
                return;
            }

            // El bombası: zeytin gövde, tapa, emniyet kolu ve pim halkası.
            var bodyFrag = Lit(new Color(0.22f, 0.27f, 0.15f), 0.3f);
            Builder.Ellipsoid(new Vector3(0f, 0.085f, 0f), new Vector3(0.068f, 0.085f, 0.068f), Quaternion.identity, 12, 8, bodyFrag);
            Builder.Cylinder(new Vector3(0f, 0.18f, 0f), 0.022f, 0.035f, Quaternion.identity, 10, metal);
            Builder.Box(new Vector3(0.045f, 0.14f, 0f), new Vector3(0.012f, 0.1f, 0.024f), Quaternion.Euler(0f, 0f, -18f), metal);
            Builder.Cylinder(new Vector3(-0.036f, 0.185f, 0f), 0.018f, 0.004f, Quaternion.Euler(0f, 0f, 90f), 10, metal);
        }

        // ------------------------------------------------------------------ Vest / Helmet / Backpack

        private static Material GearMaterial(int level)
        {
            switch (level)
            {
                case 2: return Mat(MaterialId.CamoWoodland);
                case 3: return Lit(new Color(0.15f, 0.16f, 0.13f), 0.2f);
                default: return Lit(OliveLight, 0.15f);
            }
        }

        private static void BuildVest(int level)
        {
            // Sırtüstü yatmış plaka taşıyıcı (ön yüz yukarı).
            var main = GearMaterial(level);
            var pouch = level == 2 ? Lit(Olive, 0.15f) : Lit(level == 3 ? Webbing : OliveDark, 0.15f);
            var strap = Lit(Webbing, 0.15f);

            Builder.Box(new Vector3(0f, 0.04f, 0f), new Vector3(0.44f, 0.08f, 0.52f), main);
            Builder.Box(new Vector3(-0.13f, 0.05f, 0.29f), new Vector3(0.09f, 0.06f, 0.12f), main);
            Builder.Box(new Vector3(0.13f, 0.05f, 0.29f), new Vector3(0.09f, 0.06f, 0.12f), main);
            Builder.Box(new Vector3(-0.255f, 0.035f, -0.05f), new Vector3(0.07f, 0.06f, 0.32f), strap);
            Builder.Box(new Vector3(0.255f, 0.035f, -0.05f), new Vector3(0.07f, 0.06f, 0.32f), strap);
            // MOLLE şeritleri.
            for (var i = 0; i < 3; i++)
                Builder.Box(new Vector3(0f, 0.082f, 0.12f - i * 0.05f), new Vector3(0.4f, 0.006f, 0.018f), strap);

            // Şarjör cepleri (seviyeye göre).
            var pouchCount = level >= 2 ? 3 : 2;
            for (var i = 0; i < pouchCount; i++)
            {
                var x = pouchCount == 3 ? (i - 1) * 0.12f : (i == 0 ? -0.07f : 0.07f);
                Builder.Box(new Vector3(x, 0.115f, -0.12f), new Vector3(0.1f, 0.07f, 0.16f), pouch);
                Builder.Box(new Vector3(x, 0.153f, -0.07f), new Vector3(0.104f, 0.008f, 0.06f), strap);
            }

            if (level >= 3)
            {
                // Telsiz cebi + boyun koruması.
                Builder.Box(new Vector3(-0.2f, 0.11f, 0.1f), new Vector3(0.07f, 0.08f, 0.1f), pouch);
                Builder.Cylinder(new Vector3(-0.2f, 0.17f, 0.12f), 0.007f, 0.08f, Quaternion.identity, 6, Lit(NearBlack, 0.3f));
                Builder.Box(new Vector3(0f, 0.06f, 0.34f), new Vector3(0.2f, 0.05f, 0.08f), main);
            }

            // Türk bayrağı arması (göğüs).
            Builder.Patch(new Vector3(0.12f, 0.0815f, 0.17f), new Vector2(0.075f, 0.05f), Quaternion.identity, Mat(MaterialId.TurkishFlag));
        }

        private static void BuildHelmet(int level)
        {
            Material shell;
            switch (level)
            {
                case 2: shell = Mat(MaterialId.CamoWoodland); break;
                case 3: shell = Lit(Tan, 0.2f); break;
                default: shell = Lit(Olive, 0.25f); break;
            }

            var dark = Lit(NearBlack, 0.3f);
            Builder.Dome(new Vector3(0f, 0.025f, 0f), 0.15f, 0.16f, Quaternion.identity, 14, 6, shell);
            Builder.Cylinder(new Vector3(0f, 0.025f, 0f), 0.156f, 0.03f, Quaternion.identity, 14, shell, false, false);
            Builder.Ring(new Vector3(0f, 0.012f, 0f), 0.13f, 0.158f, Quaternion.identity, 14, Lit(Webbing, 0.2f));
            // Çene kayışı.
            Builder.Box(new Vector3(0f, 0.004f, 0.19f), new Vector3(0.16f, 0.008f, 0.022f), dark);
            Builder.Box(new Vector3(-0.09f, 0.004f, 0.16f), new Vector3(0.022f, 0.008f, 0.07f), Quaternion.Euler(0f, 25f, 0f), dark);
            Builder.Box(new Vector3(0.09f, 0.004f, 0.16f), new Vector3(0.022f, 0.008f, 0.07f), Quaternion.Euler(0f, -25f, 0f), dark);

            if (level >= 2)
            {
                // Yan raylar.
                Builder.Box(new Vector3(-0.145f, 0.08f, 0f), new Vector3(0.018f, 0.025f, 0.12f), Quaternion.Euler(0f, 0f, 14f), dark);
                Builder.Box(new Vector3(0.145f, 0.08f, 0f), new Vector3(0.018f, 0.025f, 0.12f), Quaternion.Euler(0f, 0f, -14f), dark);
            }

            if (level >= 3)
            {
                // Gece görüş bağlantı plakası.
                Builder.Box(new Vector3(0f, 0.12f, 0.122f), new Vector3(0.055f, 0.045f, 0.02f), Quaternion.Euler(-35f, 0f, 0f), dark);
            }
        }

        private static void BuildBackpack(int level)
        {
            var s = level == 1 ? 0.85f : level == 3 ? 1.15f : 1f;
            Material main;
            switch (level)
            {
                case 2: main = Lit(Khaki, 0.15f); break;
                case 3: main = Mat(MaterialId.CamoWoodland); break;
                default: main = Lit(Olive, 0.15f); break;
            }

            var pouch = Lit(level == 2 ? new Color(0.42f, 0.38f, 0.26f) : OliveDark, 0.15f);
            var strap = Lit(Webbing, 0.15f);

            Builder.Box(new Vector3(0f, 0.23f * s, 0f), new Vector3(0.34f, 0.46f, 0.2f) * s, main);
            Builder.Cylinder(new Vector3(0f, 0.46f * s, 0f), 0.1f * s, 0.34f * s, AlongX, 10, main);
            Builder.Box(new Vector3(0f, 0.17f * s, 0.125f * s), new Vector3(0.26f, 0.22f, 0.06f) * s, pouch);
            Builder.Box(new Vector3(0f, 0.285f * s, 0.13f * s), new Vector3(0.265f, 0.03f, 0.065f) * s, strap);
            Builder.Box(new Vector3(-0.2f * s, 0.16f * s, 0f), new Vector3(0.06f, 0.2f, 0.14f) * s, pouch);
            Builder.Box(new Vector3(0.2f * s, 0.16f * s, 0f), new Vector3(0.06f, 0.2f, 0.14f) * s, pouch);
            // Omuz askıları (arka).
            Builder.Box(new Vector3(-0.09f * s, 0.28f * s, -0.115f * s), new Vector3(0.05f, 0.4f, 0.03f) * s, strap);
            Builder.Box(new Vector3(0.09f * s, 0.28f * s, -0.115f * s), new Vector3(0.05f, 0.4f, 0.03f) * s, strap);
            // Kapak kayışları.
            Builder.Box(new Vector3(-0.08f * s, 0.4f * s, 0.105f * s), new Vector3(0.03f, 0.18f, 0.012f) * s, strap);
            Builder.Box(new Vector3(0.08f * s, 0.4f * s, 0.105f * s), new Vector3(0.03f, 0.18f, 0.012f) * s, strap);

            if (level >= 3)
            {
                // Uyku tulumu rulosu (alt).
                Builder.Cylinder(new Vector3(0f, 0.06f * s, 0.02f), 0.06f * s, 0.38f * s, AlongX, 10, Lit(OliveDark, 0.1f));
            }
        }

        private static void BuildCrate()
        {
            var body = Lit(Olive, 0.2f);
            var edge = Lit(OliveDark, 0.2f);
            Builder.Box(new Vector3(0f, 0.12f, 0f), new Vector3(0.32f, 0.24f, 0.24f), body);
            Builder.Box(new Vector3(0f, 0.245f, 0f), new Vector3(0.33f, 0.02f, 0.25f), edge);
            Builder.Box(new Vector3(0f, 0.12f, 0.122f), new Vector3(0.1f, 0.06f, 0.006f), Lit(Stencil, 0.1f));
        }

        // ------------------------------------------------------------------ Utils

        internal static void SafeDestroy(Object obj)
        {
            if (obj == null)
                return;

            if (UnityEngine.Application.isPlaying)
                Object.Destroy(obj);
            else
                Object.DestroyImmediate(obj);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Cache.Clear();
            _focusRingMesh = null;
            _focusMarker = null;
            _prototypeHolder = null;
        }
    }
}
