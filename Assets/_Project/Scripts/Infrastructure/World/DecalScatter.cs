using System;
using System.Collections.Generic;
using System.Reflection;
using Project.Core.Domain;
using Project.Infrastructure.Rendering;
using Project.Infrastructure.Vfx;
using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// GX8: dünya çıkartma serpiştirmesi. <see cref="Run"/> planı (DecalScatterPlanner) sahneye URP DecalProjector olarak kurar;
    /// DecalProjector tipi/gölgelendirici yoksa düz quad yedeği (VfxMaterials.CreateDecal) kullanılır. DecalProjector yansımayla
    /// kurulur (Universal Runtime sürüm farkı güvenli). Kademe 0'da (Decal renderer özelliği kapalı) hiçbir şey kurulmaz.
    /// Çizim mesafesi/solma kademeye bağlıdır; <see cref="SetTier"/> QualityTierApplier'dan çağrılır.
    /// </summary>
    public static class DecalScatter
    {
        public const string RootName = "Çıkartmalar";

        private const string ProjectorType = "UnityEngine.Rendering.Universal.DecalProjector, Unity.RenderPipelines.Universal.Runtime";
        private static Type _projectorType;
        private static bool _projectorTried;
        private static bool _warnedNoProjector;

        private sealed class Item
        {
            public GameObject Go;
            public Component Projector;
            public float Rank01;
            public float SizeMax;
        }

        private static readonly List<Item> Items = new List<Item>();
        private static GameObject _root;
        private static int _builtTier;
        private static int _currentTier;
        private static float _half = 512f;
        private static float _wet;

        /// <summary>Kurulu çıkartma sayısı (etkin olsun olmasın).</summary>
        public static int Count => Items.Count;

        /// <summary>
        /// Çıkartmaları kurar; kök nesneyi döner (kademe 0 / üretilecek yok / görsel yok → null).
        /// worldRoot: dünya kökü (altında Terrain aranır). Önceki çalıştırma temizlenir.
        /// </summary>
        public static GameObject Run(Transform worldRoot, MapLayout layout, int seed, int tier)
        {
            Clear();
            tier = DecalScatterTiers.ClampTier(tier);
            if (layout == null || tier <= 0)
                return null;
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                return null;

            var terrain = worldRoot != null ? worldRoot.GetComponentInChildren<Terrain>() : null;
            if (terrain == null)
                terrain = Terrain.activeTerrain;
            if (terrain == null || terrain.terrainData == null)
            {
                Debug.LogWarning("[DecalScatter] Arazi yok; çıkartma serpiştirilmedi.");
                return null;
            }

            var wet = WetFromWeather(Atmosphere.CurrentWeather);
            var input = new DecalScatterInput
            {
                Layout = layout,
                Seed = seed,
                Wet01 = wet,
                Height = (x, z) => terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y,
                Buildings = WorldGenerator.LastStructureBounds ?? (IReadOnlyList<Bounds>)Array.Empty<Bounds>(),
                Trees = CollectTrees(terrain),
                Chimneys = CollectChimneys(worldRoot)
            };

            var plan = DecalScatterPlanner.Plan(input, tier);
            // Arazi mikro-detayı: yol aşınma şeritleri, su birikintileri, kaya çatlakları (aynı çizici).
            var model = TerrainGenerator.LastModel;
            if (model != null && model.Layout != null && model.Layout.Name == layout.Name)
            {
                try { plan.AddRange(TerrainMicroDetailPlanner.Plan(model, tier, wet)); }
                catch (Exception e) { Debug.LogWarning("[DecalScatter] Mikro-detay planlanamadı: " + e.Message); }
            }
            if (plan.Count == 0)
                return null;

            _root = new GameObject(RootName) { layer = GameLayers.Default };
            if (worldRoot != null)
                _root.transform.SetParent(worldRoot, false);
            _builtTier = tier;
            _currentTier = tier;
            _half = layout.HalfSize;
            _wet = wet;

            Physics.SyncTransforms();
            var made = 0;
            for (var i = 0; i < plan.Count; i++)
            {
                if (Build(plan[i], input, tier))
                    made++;
            }

            Debug.Log("[DecalScatter] " + made + "/" + plan.Count + " çıkartma kuruldu (kademe " + tier + ", ıslaklık " + wet.ToString("0.00") + ").");
            return _root;
        }

        /// <summary>Hava durumundan ıslaklık: yağmur 1, kar 0.35, açık 0.</summary>
        public static float WetFromWeather(WeatherKind weather)
        {
            switch (weather)
            {
                case WeatherKind.Yagmur: return 1f;
                case WeatherKind.Kar: return 0.35f;
                default: return 0f;
            }
        }

        /// <summary>
        /// Kalite kademesi değişimi (QualityTierApplier). 0 → tümü gizli; aksi halde çizim mesafesi güncellenir ve
        /// kurulu kademeden düşükse sıralaması yüksek olanlar kapatılır (kurulu kademenin üstüne çıkmak için Run yeniden çağrılmalı).
        /// </summary>
        public static void SetTier(int tier)
        {
            tier = DecalScatterTiers.ClampTier(tier);
            _currentTier = tier;
            if (_root == null)
                return;
            _root.SetActive(tier > 0);
            if (tier <= 0)
                return;

            var eff = Mathf.Min(tier, _builtTier);
            var fraction = DecalScatterTiers.ActiveFraction(_builtTier, eff, _half, _wet);
            for (var i = 0; i < Items.Count; i++)
            {
                var it = Items[i];
                if (it.Go == null)
                    continue;
                it.Go.SetActive(it.Rank01 < fraction || fraction >= 1f);
                if (it.Projector != null)
                    ApplyDistance(it.Projector, tier, it.SizeMax);
            }
        }

        /// <summary>Kurulu çıkartmaları siler.</summary>
        public static void Clear()
        {
            Items.Clear();
            if (_root != null)
            {
                if (UnityEngine.Application.isPlaying)
                    UnityEngine.Object.Destroy(_root);
                else
                    UnityEngine.Object.DestroyImmediate(_root);
            }
            _root = null;
        }

        // ------------------------------------------------------------------------------------------ toplama

        private static IReadOnlyList<Vector3> CollectTrees(Terrain terrain)
        {
            var list = new List<Vector3>();
            var data = terrain.terrainData;
            var trees = data.treeInstances;
            var size = data.size;
            var origin = terrain.transform.position;
            for (var i = 0; i < trees.Length; i++)
            {
                var p = trees[i].position;
                list.Add(new Vector3(origin.x + p.x * size.x, 0f, origin.z + p.z * size.z));
            }
            return list;
        }

        private static IReadOnlyList<Vector3> CollectChimneys(Transform worldRoot)
        {
            var list = new List<Vector3>();
            if (worldRoot == null)
                return list;
            var all = worldRoot.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < all.Length; i++)
            {
                var n = all[i].name;
                if (n.IndexOf("baca", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("chimney", StringComparison.OrdinalIgnoreCase) >= 0)
                    list.Add(all[i].position + Vector3.up * 0.2f);
            }
            return list;
        }

        // ------------------------------------------------------------------------------------------ kurulum

        private static bool Build(DecalPlacement p, DecalScatterInput input, int tier)
        {
            Vector3 pos;
            Quaternion rot;
            if (!Orient(p, input, out pos, out rot))
                return false;

            var go = new GameObject("Decal_" + p.Kind) { layer = GameLayers.Default };
            go.transform.SetParent(_root.transform, false);
            go.transform.SetPositionAndRotation(pos, rot);

            var sizeMax = Mathf.Max(p.Size.x, p.Size.y);
            Component projector = null;
            var mat = DecalLibrary.GetMaterial(p.Kind, p.Variant);
            var type = ProjectorTypeOrNull();
            if (mat != null && type != null)
                projector = ConfigureProjector(go, type, mat, p, tier, sizeMax);

            if (projector == null)
            {
                if (!BuildQuad(go, p))
                {
                    UnityEngine.Object.Destroy(go);
                    return false;
                }
            }

            Items.Add(new Item { Go = go, Projector = projector, Rank01 = p.Rank01, SizeMax = sizeMax });
            return true;
        }

        private static bool Orient(DecalPlacement p, DecalScatterInput input, out Vector3 pos, out Quaternion rot)
        {
            pos = p.Position;
            rot = Quaternion.identity;
            switch (p.Surface)
            {
                case DecalSurface.Ground:
                {
                    var n = DecalScatterPlanner.GroundNormal(input.Height, pos.x, pos.z);
                    var up = Quaternion.Euler(0f, p.Yaw, 0f) * Vector3.forward;
                    rot = Quaternion.LookRotation(-n, up);
                    return true;
                }
                case DecalSurface.Roof:
                    rot = Quaternion.LookRotation(Vector3.down, Quaternion.Euler(0f, p.Yaw, 0f) * Vector3.forward);
                    return true;
                default:
                {
                    // Duvar: dış noktadan içe ışın at; zemin (Terrain) dışındaki dikey yüzeye otur.
                    var dir = new Vector3(p.Dir.x, 0f, p.Dir.y);
                    if (!Physics.Raycast(pos, dir, out var hit, 3.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                        return false;
                    if (hit.collider is TerrainCollider || Mathf.Abs(hit.normal.y) > 0.3f)
                        return false;
                    pos = hit.point + hit.normal * 0.02f;
                    rot = Quaternion.LookRotation(-hit.normal, Vector3.up);
                    return true;
                }
            }
        }

        private static Type ProjectorTypeOrNull()
        {
            if (_projectorTried)
                return _projectorType;
            _projectorTried = true;
            try { _projectorType = Type.GetType(ProjectorType, false); }
            catch (Exception) { _projectorType = null; }
            if (_projectorType == null && !_warnedNoProjector)
            {
                _warnedNoProjector = true;
                Debug.LogWarning("[DecalScatter] DecalProjector tipi bulunamadı; düz quad yedeği kullanılacak.");
            }
            return _projectorType;
        }

        private static Component ConfigureProjector(GameObject go, Type type, Material mat, DecalPlacement p, int tier, float sizeMax)
        {
            try
            {
                var c = go.AddComponent(type);
                SetProp(c, "material", mat);
                SetProp(c, "size", new Vector3(p.Size.x, p.Size.y, p.Depth));
                SetProp(c, "pivot", Vector3.zero);
                SetProp(c, "fadeScale", DecalScatterTiers.FadeScale(tier));
                ApplyDistance(c, tier, sizeMax);
                return c;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DecalScatter] DecalProjector kurulamadı: " + e.Message);
                return null;
            }
        }

        private static void ApplyDistance(Component projector, int tier, float sizeMax)
        {
            SetProp(projector, "drawDistance", DecalScatterTiers.DrawDistance(tier, sizeMax));
            SetProp(projector, "fadeScale", DecalScatterTiers.FadeScale(tier));
        }

        private static void SetProp(object target, string name, object value)
        {
            try
            {
                var prop = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                if (prop != null && prop.CanWrite)
                    prop.SetValue(target, value, null);
            }
            catch (Exception)
            {
                // sürüm farkı: yok say
            }
        }

        /// <summary>Düz quad yedeği: yüzeyin 4 cm üstünde, mevcut efekt çıkartma malzemesiyle.</summary>
        private static bool BuildQuad(GameObject go, DecalPlacement p)
        {
            var tex = DecalLibrary.GetAlbedo(p.Kind, p.Variant);
            var mat = VfxMaterials.CreateDecal("Decal_" + p.Kind, tex);
            if (mat == null)
                return false;
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = DecalPool.QuadMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            go.transform.position -= go.transform.forward * 0.04f;
            go.transform.localScale = new Vector3(p.Size.x, p.Size.y, 1f);
            return true;
        }
    }
}
