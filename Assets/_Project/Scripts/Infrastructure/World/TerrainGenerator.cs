using System.Collections.Generic;
using Project.Infrastructure.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Infrastructure.World
{
    /// <summary>Arazi üretim ayarları (isteğe bağlı; varsayılanlar sözleşmeye uygundur).</summary>
    public sealed class TerrainBuildSettings
    {
        /// <summary>Yükseklik haritası çözünürlüğü (2^n + 1).</summary>
        public int HeightmapResolution = TerrainGenerator.HeightmapResolution;

        /// <summary>Alphamap (splat) çözünürlüğü.</summary>
        public int AlphamapResolution = TerrainPainter.DefaultAlphamapResolution;

        /// <summary>Katman dokusu boyu (piksel).</summary>
        public int LayerTextureSize = TerrainTextureFactory.DefaultTextureSize;

        /// <summary>Hedef ağaç sayısı (0 → ağaç yok).</summary>
        public int TreeCount = TreeScatter.DefaultTargetCount;

        /// <summary>
        /// Görsel olmayan (sunucu) mod: katman dokuları/alphamap üretilmez, malzeme atanmaz; yükseklik, çarpıştırıcı ve ağaç
        /// (çarpıştırıcıları için) üretilir.
        /// </summary>
        public bool Headless;

        public static TerrainBuildSettings CreateDefault()
        {
            return new TerrainBuildSettings { Headless = WorldGenerationOptions.DetectHeadless() };
        }
    }

    /// <summary>
    /// Kuzgun Vadisi arazisi üretici: <see cref="TerrainModel"/> yükseklik alanı (fBm + sırtlar + dere oyuğu + bölge
    /// düzleştirme + yol profili), 8 prosedürel katman (çim, kuru çim, toprak, kaya, çakıl, çamur, kar, asfalt) ve
    /// alphamap, TreePrototype (çam ×2, meşe, kuru ağaç, çalı) + ~3500 ağaç. Arazi (-HalfSize, 0, -HalfSize)'ye yerleştirilir,
    /// boyut (2·HalfSize, MaxHeight, 2·HalfSize). Malzeme: MaterialLibrary.TerrainMaterial (URP Terrain/Lit).
    /// </summary>
    public static class TerrainGenerator
    {
        public const int HeightmapResolution = 513;
        public const string TerrainObjectName = "Arazi";
        public const string TreePrototypeHolderName = "[Ağaç Prototipleri]";

        /// <summary>Son üretimin modeli (yükseklik/yol/dere maskeleri) — aynı karede WorldGenerator ve mini harita kullanır.</summary>
        public static TerrainModel LastModel { get; private set; }

        /// <summary>Son üretimde oluşturulan ağaç prototip nesneleri (indeks = TreeKind). Editör prefab olarak kaydeder.</summary>
        public static IReadOnlyList<GameObject> LastTreePrototypes { get; private set; } = new GameObject[0];

        /// <summary>Son üretimde oluşturulan arazi katmanları (editör varlık olarak kaydeder; görsel olmayan modda boş).</summary>
        public static IReadOnlyList<TerrainLayer> LastLayers { get; private set; } = new TerrainLayer[0];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            LastModel = null;
            LastTreePrototypes = new GameObject[0];
            LastLayers = new TerrainLayer[0];
        }

        /// <summary>Sözleşme imzası. data null ise yeni TerrainData oluşturulur; layout null ise Kuzgun Vadisi.</summary>
        public static Terrain Create(MapLayout layout, TerrainData data, Transform parent, int seed)
        {
            return Create(layout, data, parent, seed, TerrainBuildSettings.CreateDefault());
        }

        /// <summary>Ayarlı üretim.</summary>
        public static Terrain Create(MapLayout layout, TerrainData data, Transform parent, int seed, TerrainBuildSettings settings)
        {
            settings ??= TerrainBuildSettings.CreateDefault();
            layout ??= MapLayout.CreateKuzgunVadisi(seed);

            var resolution = Mathf.ClosestPowerOfTwo(Mathf.Clamp(settings.HeightmapResolution - 1, 32, 4096)) + 1;
            var model = TerrainModel.Build(layout, seed, resolution);
            LastModel = model;

            if (data == null)
                data = new TerrainData();
            data.name = "HK_KuzgunVadisi_TerrainData";

            // Önce çözünürlük, sonra boyut (çözünürlük ataması boyutu ölçekler).
            data.heightmapResolution = resolution;
            var size = new Vector3(model.Size, layout.MaxHeight, model.Size);
            data.size = size;
            ApplyHeights(data, model);

            // Editörde kalıcı TerrainData'ya üretiliyorsa başvurulan nesneler aynı klasöre varlık olarak kaydedilir.
            var persist = WorldAssetPersistence.ShouldPersist(data);
            var assetFolder = persist ? WorldAssetPersistence.FolderOf(data) : null;

            // Ağaç prototipleri (prefab adayları) — görünmeyen bir tutucu altında, haritanın çok altında.
            var holder = CreatePrototypeHolder(parent);
            GameObject[] prototypes = TreeFactory.CreateDefaultPrototypes(holder.transform, seed);
            if (persist && assetFolder != null)
            {
                prototypes = WorldAssetPersistence.PersistTreePrototypes(prototypes, assetFolder);
                if (AllPersistent(prototypes))
                    Object.DestroyImmediate(holder);
            }

            LastTreePrototypes = prototypes;

            if (!settings.Headless)
            {
                var layers = TerrainTextureFactory.CreateLayers(seed, settings.LayerTextureSize);
                if (persist && assetFolder != null)
                    layers = WorldAssetPersistence.PersistLayers(layers, assetFolder);
                LastLayers = layers;
                data.terrainLayers = layers;
                var alphaRes = Mathf.ClosestPowerOfTwo(Mathf.Clamp(settings.AlphamapResolution, 16, 4096));
                data.alphamapResolution = alphaRes;
                data.baseMapResolution = Mathf.Clamp(alphaRes * 2, 16, 2048);
                data.SetAlphamaps(0, 0, TerrainPainter.ComputeAlphamaps(model, data.alphamapResolution));
            }
            else
            {
                LastLayers = new TerrainLayer[0];
            }

            data.treePrototypes = TreeFactory.ToTreePrototypes(prototypes);
            data.RefreshPrototypes();
            var trees = TreeScatter.Scatter(model, size, seed, Mathf.Max(0, settings.TreeCount));
            data.SetTreeInstances(trees, false);

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = TerrainObjectName;
            go.layer = GameLayers.Default;
            if (parent != null)
                go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(new Vector3(-layout.HalfSize, 0f, -layout.HalfSize), Quaternion.identity);

            var terrain = go.GetComponent<Terrain>();
            ConfigureTerrain(terrain, settings.Headless);

            var collider = go.GetComponent<TerrainCollider>();
            if (collider != null)
                collider.terrainData = data; // ağaç çarpıştırıcıları varsayılan olarak açıktır (prototip kökündeki CapsuleCollider)

            if (persist)
                WorldAssetPersistence.MarkDirty(data);
            return terrain;
        }

        /// <summary>Model yüksekliklerini TerrainData'ya yazar (normalize: h / MaxHeight).</summary>
        public static void ApplyHeights(TerrainData data, TerrainModel model)
        {
            var res = model.Resolution;
            var heights = new float[res, res];
            var inv = 1f / Mathf.Max(1f, data.size.y);
            var src = model.Heights;
            for (var z = 0; z < res; z++)
            {
                var row = z * res;
                for (var x = 0; x < res; x++)
                    heights[z, x] = Mathf.Clamp01(src[row + x] * inv);
            }

            data.SetHeights(0, 0, heights);
        }

        private static void ConfigureTerrain(Terrain terrain, bool headless)
        {
            if (terrain == null)
                return;

            terrain.allowAutoConnect = false;
            terrain.groupingID = 0;
            terrain.heightmapPixelError = 5f;
            terrain.basemapDistance = 380f;
            terrain.treeDistance = 1100f;
            terrain.treeBillboardDistance = 1100f;
            terrain.treeCrossFadeLength = 0f;
            terrain.treeMaximumFullLODCount = 4000;
            terrain.detailObjectDistance = 0f;
            terrain.drawTreesAndFoliage = true;
            terrain.drawInstanced = !headless;
            terrain.shadowCastingMode = ShadowCastingMode.TwoSided;
            terrain.reflectionProbeUsage = ReflectionProbeUsage.Off;

            if (headless)
            {
                terrain.drawHeightmap = false;
                terrain.drawTreesAndFoliage = false;
                return;
            }

            var material = MaterialLibrary.TerrainMaterial;
            if (material != null)
                terrain.materialTemplate = material;
        }

        private static bool AllPersistent(GameObject[] objects)
        {
            for (var i = 0; i < objects.Length; i++)
            {
                if (objects[i] == null || objects[i].scene.IsValid())
                    return false;
            }

            return objects.Length > 0;
        }

        private static GameObject CreatePrototypeHolder(Transform parent)
        {
            var holder = new GameObject(TreePrototypeHolderName);
            if (parent != null)
                holder.transform.SetParent(parent, false);
            // Prototipler sahnede görünmesin/çarpışmasın diye haritanın çok altında tutulur (etkin kalırlar ki arazi ağaç
            // çizimi prototip renderer'ını sorunsuz okuyabilsin). Editör bunları prefab'a çevirip tutucuyu silebilir.
            holder.transform.position = new Vector3(0f, -2000f, 0f);
            return holder;
        }
    }
}
