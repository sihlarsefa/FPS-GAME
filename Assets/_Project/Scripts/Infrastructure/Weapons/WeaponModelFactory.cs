using System;
using System.Collections.Generic;
using Project.Core.Domain;
using Project.Infrastructure.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Infrastructure.Weapons
{
    /// <summary>
    /// Prosedürel düşük poligonlu Türk silah modelleri (MPT-76, MPT-55, G3A7, KNT-76, JNG-90, PMT-76, SAR 109T, SAR 9,
    /// TP9, Escort). Her silah tipinin mesh'leri bir kez üretilir ve paylaşılır (WeaponBlueprints); Build yalnızca hafif bir
    /// hiyerarşi kurar: kök (WeaponModel) → parçalar (Body/Magazine/Bolt/Slide/Pump/Cover) → bağlantılar (Muzzle, Sight,
    /// el bilekleri). Model uzayı: +Z namlu, +Y yukarı, orijin kabzanın üstü; ölçek gerçek boyut (m).
    /// Collider yoktur. Görünüm modeli (forViewmodel) için gölge kapalıdır.
    /// </summary>
    public static class WeaponModelFactory
    {
        private static readonly Dictionary<WeaponStyle, WeaponBlueprint> Cache = new Dictionary<WeaponStyle, WeaponBlueprint>();
        private static readonly Dictionary<WeaponStyle, WeaponBlueprint> CacheLod1 = new Dictionary<WeaponStyle, WeaponBlueprint>();
        private static readonly List<Collider> TmpColliders = new List<Collider>(4);
        private static WeaponBlueprint _fallback;

        /// <summary>
        /// Silah modelini kurar. weapon null ise null döner. Yakın dövüş/bilinmeyen kategori için görüntüsüz (yalnızca
        /// bağlantılı) bir kök döner. muzzle: namlu ucu (+Z atış yönü), asla null değildir (model varsa).
        /// </summary>
        public static GameObject Build(WeaponDefinitionData weapon, Transform parent, int layer, bool forViewmodel, out Transform muzzle)
        {
            muzzle = null;
            if (weapon == null)
                return null;

            var model = BuildModel(WeaponStyles.Resolve(weapon), weapon, parent, layer, forViewmodel);
            if (model == null)
                return null;

            muzzle = model.Muzzle;
            return model.gameObject;
        }

        /// <summary>Build ile aynı; WeaponModel bileşenini döndürür.</summary>
        public static WeaponModel BuildModel(WeaponDefinitionData weapon, Transform parent, int layer, bool forViewmodel)
        {
            return weapon == null ? null : BuildModel(WeaponStyles.Resolve(weapon), weapon, parent, layer, forViewmodel);
        }

        /// <summary>Belirli bir model tipini kurar (definition isteğe bağlıdır; yalnızca kimlik/isim için).</summary>
        public static WeaponModel BuildModel(WeaponStyle style, WeaponDefinitionData definition, Transform parent, int layer, bool forViewmodel)
        {
            var name = "Weapon_" + (definition != null && !string.IsNullOrEmpty(definition.WeaponId) ? definition.WeaponId : style.ToString());
            var root = new GameObject(name);
            root.layer = layer;
            var rootTransform = root.transform;
            if (parent != null)
                rootTransform.SetParent(parent, false);

            var model = root.AddComponent<WeaponModel>();
            model.Definition = definition;
            model.Style = style;
            model.ForViewmodel = forViewmodel;

            if (TryInstantiateOverride(definition, model, layer))
            {
                EnsureAnchors(model, layer);
                RemoveColliders(root);
                if (!forViewmodel)
                    ApplyThirdPersonLook(root);
                model.CaptureHomePoses();
                return model;
            }

            // Uzak/üçüncü-şahıs silahlar LOD1 (yarı detay) blueprint kullanır; viewmodel tam detay.
            var blueprint = style == WeaponStyle.None ? null : (forViewmodel ? GetBlueprint(style) : GetBlueprint(style, 1));
            if (blueprint != null)
                Instantiate(blueprint, model, layer, forViewmodel);
            else if (style != WeaponStyle.None)
                Instantiate(GetFallbackBlueprint(), model, layer, forViewmodel);

            EnsureAnchors(model, layer);
            RemoveColliders(root);
            if (forViewmodel)
            {
                // Lobi VİTRİN'inde seçilen kaplama (varsa) kozmetik tonun yerine geçer.
                if (!ApplySelectedSkin(root, definition))
                    ApplySkinTint(root, definition);
            }
            else
                ApplyThirdPersonLook(root);
            root.AddComponent<WeaponMaterialDriver>().Initialize(model);
            model.CaptureHomePoses();
            return model;
        }

        /// <summary>Lobi VİTRİN'inde silah için seçilmiş kaplamayı uygular (kaplamasızsa/hata olursa false).</summary>
        private static bool ApplySelectedSkin(GameObject root, WeaponDefinitionData definition)
        {
            try
            {
                if (definition == null) return false;
                var skinId = Skins.WeaponSkinSelection.Get(definition.WeaponId);
                if (skinId == Skins.WeaponSkinCatalog.DefaultId) return false;
                return Skins.WeaponSkinApplier.Apply(root.GetComponentsInChildren<MeshRenderer>(true), skinId);
            }
            catch (Exception) { return false; /* görsel; sessiz */ }
        }

        /// <summary>Kozmetik silah kaplaması (yalnızca görsel): malzemeleri kopyalayıp ton uygular. Hata olursa sessizce atlar.</summary>
        private static void ApplySkinTint(GameObject root, WeaponDefinitionData definition)
        {
            try
            {
                if (definition == null || !Project.Infrastructure.Characters.CosmeticsRuntime.TryGetWeaponTint(definition.WeaponId, out var tint))
                    return;
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var mats = r.materials; // örnek kopya: paylaşılan blueprint malzemeleri bozulmaz
                    for (var i = 0; i < mats.Length; i++)
                    {
                        if (mats[i] == null) continue;
                        if (mats[i].HasProperty("_BaseColor")) mats[i].SetColor("_BaseColor", Color.Lerp(mats[i].GetColor("_BaseColor"), tint, 0.7f));
                        else if (mats[i].HasProperty("_Color")) mats[i].SetColor("_Color", Color.Lerp(mats[i].GetColor("_Color"), tint, 0.7f));
                    }

                    r.materials = mats;
                }
            }
            catch (Exception) { /* görsel; sessiz */ }
        }

        // ------------------------------------------------------------------ Üçüncü şahıs görünümü

        private static readonly Dictionary<Material, Material> TpCache = new Dictionary<Material, Material>();

        /// <summary>
        /// Dünya/elde taşınan modelde (forViewmodel=false) koyu-metalik malzemeler uzaktan siyah çubuk gibi görünür.
        /// Paylaşılan malzemeler değişmez; açık gri gövde + renkli mobilya (ahşap/tan/zeytin/polimer) kopyaları atanır.
        /// Optik cam, reticle ve emissive/unlit malzemeler olduğu gibi kalır. Hata olursa sessizce atlar.
        /// </summary>
        private static void ApplyThirdPersonLook(GameObject root)
        {
            try
            {
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var mats = r.sharedMaterials;
                    var changed = false;
                    for (var i = 0; i < mats.Length; i++)
                    {
                        var remapped = ThirdPersonMaterial(mats[i]);
                        if (remapped != null && remapped != mats[i])
                        {
                            mats[i] = remapped;
                            changed = true;
                        }
                    }

                    if (changed)
                        r.sharedMaterials = mats;
                }
            }
            catch (Exception) { /* görsel; sessiz */ }
        }

        private static Material ThirdPersonMaterial(Material src)
        {
            if (src == null)
                return null;
            if (TpCache.TryGetValue(src, out var cached) && cached != null)
                return cached;

            Material result = src;
            var n = (src.name ?? string.Empty).ToLowerInvariant();
            var shaderName = src.shader != null ? src.shader.name : string.Empty;
            // Authored PBR surfaces must retain their texture, roughness and wear in world models.
            if (shaderName.StartsWith("HAREKAT/Weapon/", StringComparison.Ordinal))
            {
                TpCache[src] = src;
                return src;
            }

            var skip = n.Contains("glass") || n.Contains("reticle") || n.Contains("dot") || n.Contains("tritium")
                       || shaderName.Contains("Unlit") || shaderName.Contains("OpticGlass");
            if (!skip)
            {
                Color c;
                var hasColor = true;
                if (n.StartsWith("gun_steel")) c = new Color(0.40f, 0.41f, 0.42f);
                else if (n.StartsWith("gun_anod")) c = new Color(0.30f, 0.31f, 0.33f);
                else if (n.StartsWith("gun_poly")) c = new Color(0.23f, 0.23f, 0.24f);
                else if (n.StartsWith("gun_rubber")) c = new Color(0.17f, 0.17f, 0.17f);
                else if (n.StartsWith("gun_tan")) c = new Color(0.62f, 0.52f, 0.35f);
                else if (n.StartsWith("gun_olive")) c = new Color(0.34f, 0.38f, 0.24f);
                else if (n.StartsWith("gun_woodd")) c = new Color(0.36f, 0.22f, 0.12f);
                else if (n.StartsWith("gun_wood")) c = new Color(0.5f, 0.32f, 0.17f);
                else
                {
                    // Bilinmeyen (hazır varlık / yedek) malzeme: yalnızca çok koyuysa açık griye çek.
                    c = Color.white;
                    if (src.HasProperty("_BaseColor")) c = src.GetColor("_BaseColor");
                    else if (src.HasProperty("_Color")) c = src.GetColor("_Color");
                    else hasColor = false;
                    if (hasColor && c.grayscale < 0.3f)
                        c = Color.Lerp(c, new Color(0.42f, 0.43f, 0.45f), 0.65f);
                    else
                        hasColor = false;
                }

                if (hasColor)
                {
                    var m = MaterialLibrary.Lit(c, 0.35f, 0.15f);
                    if (m != null)
                        result = m;
                }
            }

            TpCache[src] = result;
            return result;
        }

        /// <summary>Tüm silah tiplerinin mesh'lerini önceden üretir (ilk kuşanmada takılmayı önler).</summary>
        public static void Prewarm()
        {
            foreach (WeaponStyle style in Enum.GetValues(typeof(WeaponStyle)))
            {
                if (style != WeaponStyle.None)
                    GetBlueprint(style);
            }
        }

        /// <summary>Önbelleği boşaltır (mesh'ler yok edilmez; sahnede kullanılıyor olabilirler).</summary>
        public static void ClearCache()
        {
            Cache.Clear();
            CacheLod1.Clear();
            TpCache.Clear();
            _fallback = null;
        }

        // ------------------------------------------------------------------ Blueprint cache

        internal static WeaponBlueprint GetBlueprint(WeaponStyle style) => GetBlueprint(style, 0);

        internal static WeaponBlueprint GetBlueprint(WeaponStyle style, int lod)
        {
            var cache = lod > 0 ? CacheLod1 : Cache;
            if (cache.TryGetValue(style, out var cached) && IsAlive(cached))
                return cached;

            WeaponBlueprint blueprint = null;
            try
            {
                blueprint = WeaponBlueprints.Build(style, lod > 0 ? 1 : 0);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Silah] '" + style + "' modeli üretilemedi, basit model kullanılacak: " + e.Message);
            }

            if (!IsAlive(blueprint))
                blueprint = GetFallbackBlueprint();

            cache[style] = blueprint;
            return blueprint;
        }

        private static bool IsAlive(WeaponBlueprint blueprint)
        {
            if (blueprint == null || !blueprint.IsValid)
                return false;

            var parts = blueprint.Parts;
            for (var i = 0; i < parts.Count; i++)
            {
                var materials = parts[i].Materials;
                for (var m = 0; m < materials.Length; m++)
                {
                    if (materials[m] == null)
                        return false;
                }
            }

            return true;
        }

        /// <summary>Her koşulda çalışan basit tüfek (kutular). Malzeme bulunamazsa bile geçerli mesh üretir.</summary>
        private static WeaponBlueprint GetFallbackBlueprint()
        {
            if (IsAlive(_fallback))
                return _fallback;

            var material = SafeMaterial();
            var b = new WeaponMeshBuilder();
            b.Box(material, new Vector3(0f, 0.035f, 0.08f), new Vector3(0.04f, 0.07f, 0.34f));
            b.Box(material, new Vector3(0f, 0.035f, 0.42f), new Vector3(0.02f, 0.02f, 0.34f));
            b.Box(material, new Vector3(0f, 0.025f, -0.2f), new Vector3(0.04f, 0.09f, 0.22f));
            b.Box(material, new Vector3(0f, -0.045f, 0f), new Vector3(0.03f, 0.09f, 0.04f), new Vector3(15f, 0f, 0f));
            b.BeginGroup(WeaponModel.MagazinePart, new Vector3(0f, -0.01f, 0.12f));
            b.Box(material, new Vector3(0f, -0.07f, 0.12f), new Vector3(0.026f, 0.12f, 0.06f), new Vector3(-8f, 0f, 0f));
            b.EndGroup();

            var bp = new WeaponBlueprint { Style = WeaponStyle.Mpt55, EyeRelief = 0.14f };
            bp.Parts = b.Build("Weapon_Fallback");
            bp.Anchors.Add(new AnchorSpec { Name = WeaponModel.MuzzleAnchor, Parent = WeaponModel.BodyPart, Position = new Vector3(0f, 0.035f, 0.6f), Rotation = Quaternion.identity });
            bp.Anchors.Add(new AnchorSpec { Name = WeaponModel.SightAnchor, Parent = WeaponModel.BodyPart, Position = new Vector3(0f, 0.08f, 0f), Rotation = Quaternion.identity });
            _fallback = bp;
            return bp;
        }

        private static Material SafeMaterial()
        {
            Material material = null;
            try
            {
                material = MaterialLibrary.Get(MaterialId.GunMetal);
            }
            catch (Exception)
            {
                // Malzeme kütüphanesi kullanılamıyor — aşağıda yedek gölgelendirici denenir.
            }

            if (material != null)
                return material;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            material = shader != null ? new Material(shader) : null;
            if (material != null)
            {
                material.color = new Color(0.13f, 0.13f, 0.14f);
                material.name = "GunFallback";
                material.hideFlags = HideFlags.DontSave;
            }

            return material;
        }

        // ------------------------------------------------------------------ Instantiation

        private static void Instantiate(WeaponBlueprint blueprint, WeaponModel model, int layer, bool forViewmodel)
        {
            if (blueprint == null)
                return;

            var root = model.transform;
            var parts = blueprint.Parts;
            for (var i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                if (part == null || !part.IsValid)
                    continue;

                var go = new GameObject(part.Name);
                go.layer = layer;
                var t = go.transform;
                t.SetParent(root, false);
                t.localPosition = part.Pivot;
                t.localRotation = Quaternion.identity;

                var filter = go.AddComponent<MeshFilter>();
                filter.sharedMesh = part.Mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = part.Materials;
                ConfigureRenderer(renderer, forViewmodel);
                model.AddRenderer(renderer);

                switch (part.Name)
                {
                    case WeaponModel.BodyPart: model.Body = t; break;
                    case WeaponModel.MagazinePart: model.Magazine = t; break;
                    case WeaponModel.BoltPart: model.Bolt = t; break;
                    case WeaponModel.SlidePart: model.Slide = t; break;
                    case WeaponModel.PumpPart: model.Pump = t; break;
                    case WeaponModel.CoverPart: model.FeedCover = t; break;
                }
            }

            for (var i = 0; i < blueprint.Anchors.Count; i++)
            {
                var spec = blueprint.Anchors[i];
                var parent = FindPart(model, spec.Parent);
                var pivot = parent == root ? Vector3.zero : blueprint.PivotOf(spec.Parent);
                var anchor = CreateAnchor(spec.Name, parent, spec.Position - pivot, spec.Rotation, layer);
                AssignAnchor(model, spec.Name, anchor);
            }

            model.EyeRelief = blueprint.EyeRelief;
            model.HasScope = blueprint.HasScope;
            model.HasOptic = blueprint.HasOptic;
            model.MagazineEjectDirection = blueprint.MagazineEject.sqrMagnitude > 1e-6f ? blueprint.MagazineEject.normalized : Vector3.down;
            model.BoltTravel = blueprint.BoltTravel;
            model.BoltLiftDegrees = blueprint.BoltLift;
            model.PumpTravel = blueprint.PumpTravel;
            model.SlideTravel = blueprint.SlideTravel;
        }

        private static void ConfigureRenderer(MeshRenderer renderer, bool forViewmodel)
        {
            if (forViewmodel)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                // Ortam yansıması + SH: kamera yanındaki reflection/ışık probları silahı aydınlatır (iç mekânda düz görünmesin).
                renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbesAndSkybox;
                renderer.allowOcclusionWhenDynamic = false;
            }
            else
            {
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.Object;
            }
        }

        private static Transform FindPart(WeaponModel model, string partName)
        {
            switch (partName)
            {
                case WeaponModel.MagazinePart: return model.Magazine != null ? model.Magazine : model.transform;
                case WeaponModel.BoltPart: return model.Bolt != null ? model.Bolt : model.transform;
                case WeaponModel.SlidePart: return model.Slide != null ? model.Slide : model.transform;
                case WeaponModel.PumpPart: return model.Pump != null ? model.Pump : model.transform;
                case WeaponModel.CoverPart: return model.FeedCover != null ? model.FeedCover : model.transform;
                default: return model.transform;
            }
        }

        private static Transform CreateAnchor(string name, Transform parent, Vector3 localPosition, Quaternion localRotation, int layer)
        {
            var go = new GameObject(name);
            go.layer = layer;
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            t.localRotation = localRotation;
            return t;
        }

        private static void AssignAnchor(WeaponModel model, string name, Transform anchor)
        {
            switch (name)
            {
                case WeaponModel.MuzzleAnchor: model.Muzzle = anchor; break;
                case WeaponModel.SightAnchor: model.SightPoint = anchor; break;
                case WeaponModel.FrontSightAnchor: model.FrontSightPoint = anchor; break;
                case WeaponModel.RightHandAnchor: model.RightHandGrip = anchor; break;
                case WeaponModel.LeftHandAnchor: model.LeftHandGrip = anchor; break;
                case WeaponModel.MagazineHandAnchor: model.MagazineHandGrip = anchor; break;
                case WeaponModel.BoltHandAnchor: model.BoltHandGrip = anchor; break;
                case WeaponModel.LoadingPortAnchor: model.LoadingPort = anchor; break;
                case WeaponModel.EjectAnchor: model.EjectPort = anchor; break;
                case WeaponModel.CoverHandAnchor: model.CoverHandGrip = anchor; break;
            }
        }

        /// <summary>Eksik zorunlu bağlantıları makul varsayılanlarla tamamlar (namlu, nişan, el bilekleri).</summary>
        private static void EnsureAnchors(WeaponModel model, int layer)
        {
            var root = model.transform;
            var pistol = WeaponStyles.IsPistol(model.Style);
            if (model.Muzzle == null)
                model.Muzzle = CreateAnchor(WeaponModel.MuzzleAnchor, root, new Vector3(0f, 0.035f, pistol ? 0.17f : 0.6f), Quaternion.identity, layer);
            if (model.SightPoint == null)
                model.SightPoint = CreateAnchor(WeaponModel.SightAnchor, root, new Vector3(0f, pistol ? 0.048f : 0.08f, 0f), Quaternion.identity, layer);
            if (model.RightHandGrip == null)
            {
                var rot = Quaternion.Euler(15f, 0f, 0f) * Quaternion.LookRotation(new Vector3(-0.16f, 0f, 1f), Vector3.right);
                var palm = new Vector3(0.031f, -0.045f, -0.006f);
                model.RightHandGrip = CreateAnchor(WeaponModel.RightHandAnchor, root, palm - rot * new Vector3(0f, 0f, WeaponBlueprints.PalmCenter), rot, layer);
            }

            if (model.LeftHandGrip == null)
            {
                var rot = Quaternion.LookRotation(new Vector3(0.55f, 0.08f, 0.83f), new Vector3(0f, -1f, 0.1f));
                var palm = pistol ? new Vector3(-0.03f, -0.06f, 0f) : new Vector3(-0.008f, -0.01f, 0.3f);
                model.LeftHandGrip = CreateAnchor(WeaponModel.LeftHandAnchor, root, palm - rot * new Vector3(0f, 0f, WeaponBlueprints.PalmCenter), rot, layer);
            }
        }

        /// <summary>Hazır varlık (ContentOverrides) silah prefab'ı; yoksa/hata olursa false (prosedürel yol devam eder).</summary>
        private static bool TryInstantiateOverride(WeaponDefinitionData definition, WeaponModel model, int layer)
        {
            try
            {
                if (definition == null || string.IsNullOrEmpty(definition.WeaponId))
                    return false;
                if (!Project.Infrastructure.Content.ContentOverrides.TryGetWeapon(definition.WeaponId, out GameObject prefab) || prefab == null)
                    return false;

                var instance = UnityEngine.Object.Instantiate(prefab, model.transform, false);
                instance.name = prefab.name;
                SetLayerRecursive(instance.transform, layer);
                var muzzle = FindDeep(instance.transform, WeaponModel.MuzzleAnchor);
                if (muzzle != null)
                    model.Muzzle = muzzle;

                RepairMaterials(instance);
                if (!ValidateOverride(instance.transform, model.transform, out var why))
                {
                    Debug.LogWarning("[WeaponModelFactory] Hazır silah varlığı geçersiz (" + definition.WeaponId + "): " + why + " — prosedürel modele dönülüyor.");
                    model.Muzzle = null;
                    if (UnityEngine.Application.isPlaying)
                        UnityEngine.Object.Destroy(instance);
                    else
                        UnityEngine.Object.DestroyImmediate(instance);
                    return false;
                }

                foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
                    model.AddRenderer(r);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[WeaponModelFactory] Hazır silah varlığı kurulamadı, prosedürel modele dönülüyor: " + e.Message);
                return false;
            }
        }

        /// <summary>Eksik/pembe (shader'sız) malzemeleri koyu metal malzemeyle değiştirir.</summary>
        private static void RepairMaterials(GameObject instance)
        {
            Material fallback = null;
            foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                var changed = false;
                for (var i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m != null && m.shader != null && m.shader.isSupported && m.shader.name != "Hidden/InternalErrorShader")
                        continue;
                    if (fallback == null)
                        fallback = SafeMaterial();
                    if (fallback == null)
                        continue;
                    mats[i] = fallback;
                    changed = true;
                }

                if (changed)
                    r.sharedMaterials = mats;
            }
        }

        /// <summary>Etkin en az bir Renderer; birleşik sınır boyutu 0.1–2.0 m, merkezi kökün 1 m yakınında olmalı.</summary>
        private static bool ValidateOverride(Transform instance, Transform root, out string why)
        {
            why = null;
            var any = false;
            Bounds total = default;
            foreach (var r in instance.GetComponentsInChildren<Renderer>(false))
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy)
                    continue;
                var lb = r.localBounds;
                var c = lb.center;
                var e = lb.extents;
                for (var k = 0; k < 8; k++)
                {
                    var corner = new Vector3(c.x + ((k & 1) == 0 ? -e.x : e.x), c.y + ((k & 2) == 0 ? -e.y : e.y), c.z + ((k & 4) == 0 ? -e.z : e.z));
                    var p = root.InverseTransformPoint(r.transform.TransformPoint(corner));
                    if (!any) { total = new Bounds(p, Vector3.zero); any = true; }
                    else total.Encapsulate(p);
                }
            }

            if (!any) { why = "etkin Renderer yok"; return false; }
            var size = Mathf.Max(total.size.x, Mathf.Max(total.size.y, total.size.z));
            if (float.IsNaN(size) || size < 0.1f || size > 2.0f) { why = "sınır boyutu " + size.ToString("0.###") + " m"; return false; }
            if (total.center.magnitude > 1f) { why = "merkez kökten " + total.center.magnitude.ToString("0.##") + " m uzakta"; return false; }
            return true;
        }

        private static void SetLayerRecursive(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (var i = 0; i < t.childCount; i++)
                SetLayerRecursive(t.GetChild(i), layer);
        }

        private static Transform FindDeep(Transform t, string name)
        {
            for (var i = 0; i < t.childCount; i++)
            {
                var c = t.GetChild(i);
                if (c.name == name)
                    return c;
                var r = FindDeep(c, name);
                if (r != null)
                    return r;
            }
            return null;
        }

        private static void RemoveColliders(GameObject root)
        {
            TmpColliders.Clear();
            root.GetComponentsInChildren(true, TmpColliders);
            for (var i = 0; i < TmpColliders.Count; i++)
            {
                var c = TmpColliders[i];
                if (c == null)
                    continue;

                if (UnityEngine.Application.isPlaying)
                    UnityEngine.Object.Destroy(c);
                else
                    UnityEngine.Object.DestroyImmediate(c);
            }

            TmpColliders.Clear();
        }
    }
}
