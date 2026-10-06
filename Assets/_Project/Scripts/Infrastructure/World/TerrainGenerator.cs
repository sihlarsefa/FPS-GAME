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

        /// <summary>Alphamap (splat) çözünürlüğü (1 km harita için ≥1024: yakında katman geçişleri keskin kalır).</summary>
        public int AlphamapResolution = TerrainGenerator.DefaultAlphamapResolution;

        /// <summary>Katman dokusu boyu (piksel).</summary>
        public int LayerTextureSize = TerrainTextureFactory.DefaultTextureSize;

        /// <summary>Hedef ağaç sayısı (0 → ağaç yok).</summary>
        public int TreeCount = TreeScatter.DefaultTargetCount;

        /// <summary>
        /// Görsel olmayan (sunucu) mod: katman dokuları/alphamap üretilmez, malzeme atanmaz; yükseklik, çarpıştırıcı ve ağaç
        /// (çarpıştırıcıları için) üretilir.
        /// </summary>
        public bool Headless;

        /// <summary>Çimen/çiçek ayrıntı katmanları üretilsin mi (görsel olmayan modda hiç üretilmez). Yoğunluk çalışma zamanında kalite kademesiyle ölçeklenir.</summary>
        public bool Detail = true;

        /// <summary>Tarla parselleri çevresi saha detayı: taş duvar, kaya yığını, saman balyası, çalı çiti.</summary>
        public bool FieldDetail = true;

        /// <summary>Hidrolik + ısıl aşındırma geçişi (sunucu/istemci aynı tohumla aynı sonucu üretir → varsayılan açık).</summary>
        public bool Erosion = true;

        /// <summary>Aşındırma kalite kademesi (0-3); bütçe gen-zamanıdır.</summary>
        public int ErosionTier = 2;

        /// <summary>Renk haritası çözünürlüğü (0 → kapalı). Görsel olmayan modda hiç pişirilmez.</summary>
        public int ColorMapSize = 1024;

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

        /// <summary>1 km harita için varsayılan alphamap (kontrol) çözünürlüğü: 1024 ≈ 1 m/kontrol pikseli (512 yakında pikselleşiyordu).</summary>
        public const int DefaultAlphamapResolution = 1024;

        public const string TerrainObjectName = "Arazi";
        public const string TreePrototypeHolderName = "[Ağaç Prototipleri]";

        /// <summary>Son üretimin modeli (yükseklik/yol/dere maskeleri) — aynı karede WorldGenerator ve mini harita kullanır.</summary>
        public static TerrainModel LastModel { get; private set; }

        /// <summary>Son üretimde oluşturulan ağaç prototip nesneleri (indeks = TreeKind). Editör prefab olarak kaydeder.</summary>
        public static IReadOnlyList<GameObject> LastTreePrototypes { get; private set; } = new GameObject[0];

        /// <summary>Son üretimde oluşturulan arazi katmanları (editör varlık olarak kaydeder; görsel olmayan modda boş).</summary>
        public static IReadOnlyList<TerrainLayer> LastLayers { get; private set; } = new TerrainLayer[0];

        /// <summary>Son üretimin aşınma haritaları (akış, eğrilik, scree); kapalıysa null.</summary>
        public static TerrainErosionMaps LastErosion { get; private set; }

        /// <summary>
        /// Yükseklik alanına aşındırma uygular (yol/dere/yerleşim/su korunur) ve haritaları modele iliştirir.
        /// Ardından yeni yüksekliklerin en üstünü günceller. Hata olursa özgün yükseklikler korunur.
        /// </summary>
        public static TerrainErosionMaps ApplyErosion(TerrainModel model, int seed, int tier)
        {
            if (model == null)
                return null;
            try
            {
                var protect = TerrainErosion.BuildProtectMask(model);
                var maps = TerrainErosion.Erode(model.Heights, model.Resolution, model.CellSize, model.OriginX, model.OriginZ, protect, seed,
                    TerrainErosionSettings.ForTier(tier));
                TerrainErosionMaps.Attach(model, maps);
                return maps;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[TerrainGenerator] Aşındırma atlandı: " + e.Message);
                return null;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            LastErosion = null;
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
            LastErosion = settings.Erosion ? ApplyErosion(model, seed, settings.ErosionTier) : null;

            if (data == null)
                data = new TerrainData();
            data.name = "HK_" + (string.IsNullOrEmpty(layout.Name) ? "KuzgunVadisi" : layout.Name.Replace(" ", string.Empty)) + "_TerrainData";

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
                var layers = TerrainTextureFactory.CreateLayers(seed, settings.LayerTextureSize == TerrainTextureFactory.DefaultTextureSize
                    ? TerrainTextureFactory.TextureSizeForTier(Project.Infrastructure.Rendering.PostProcessing.QualityLevel)
                    : settings.LayerTextureSize);
                if (persist && assetFolder != null)
                    layers = WorldAssetPersistence.PersistLayers(layers, assetFolder);
                LastLayers = layers;
                data.terrainLayers = layers;
                var alphaRes = Mathf.ClosestPowerOfTwo(Mathf.Clamp(settings.AlphamapResolution, 16, 4096));
                data.alphamapResolution = alphaRes;
                // Uzak zemin kompoziti (basemap) ≥2048: basemapDistance ötesi bulanıklaşmasın.
                data.baseMapResolution = Mathf.Clamp(Mathf.Max(alphaRes * 2, 2048), 16, 2048);
                var alphamaps = TerrainPainter.ComputeAlphamaps(model, data.alphamapResolution);
                data.SetAlphamaps(0, 0, alphamaps);
                LogLayers(layers);

                // Makro renk + eğrilik AO + yol izleri tek renk haritasına pişirilir (shader örneklemesi ENTEGRASYON).
                if (LastErosion != null && settings.ColorMapSize > 0)
                {
                    try { TerrainColorMap.Bake(model, LastErosion, settings.ColorMapSize); }
                    catch (System.Exception e) { Debug.LogWarning("[TerrainGenerator] Renk haritası pişirilemedi: " + e.Message); }
                }

                // Çimen/çiçek ayrıntı katmanları (alphamap'ten türetilir; yol/kaya/kar/dik yamaçta yok).
                if (settings.Detail)
                {
                    try
                    {
                        VegetationPainter.Apply(data, model, alphamaps, seed);
                        if (persist && assetFolder != null)
                            WorldAssetPersistence.PersistDetailTextures(data, assetFolder);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning("[TerrainGenerator] Ayrıntı katmanları üretilemedi: " + e.Message);
                    }
                }
            }
            else
            {
                LastLayers = new TerrainLayer[0];
            }

            data.treePrototypes = TreeFactory.ToTreePrototypes(prototypes);
            data.RefreshPrototypes();
            var trees = TreeScatter.Scatter(model, size, seed, Mathf.Max(0, settings.TreeCount));
            if (settings.FieldDetail)
            {
                try
                {
                    trees = FieldDetailProps.FilterTreesInFields(trees, model, size);
                    var hedges = FieldDetailProps.HedgeTrees(model, size, seed);
                    if (hedges.Count > 0)
                    {
                        var all = new System.Collections.Generic.List<TreeInstance>(trees);
                        all.AddRange(hedges);
                        trees = all.ToArray();
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[TerrainGenerator] Saha çalı çitleri üretilemedi: " + e.Message);
                }
            }

            data.SetTreeInstances(trees, false);

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = TerrainObjectName;
            go.layer = GameLayers.Default;
            if (parent != null)
                go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(new Vector3(-layout.HalfSize, 0f, -layout.HalfSize), Quaternion.identity);

            var terrain = go.GetComponent<Terrain>();
            ConfigureTerrain(terrain, settings.Headless);

            if (settings.FieldDetail && !settings.Headless)
            {
                try { FieldDetailProps.Build(model, parent, seed); }
                catch (System.Exception e) { Debug.LogWarning("[TerrainGenerator] Saha propları üretilemedi: " + e.Message); }
            }

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

        /// <summary>Üretimde katman başına tek satır günlük: ad, doku çözünürlüğü, döşeme boyu, normal/mask durumu.</summary>
        private static void LogLayers(IReadOnlyList<TerrainLayer> layers)
        {
            if (layers == null)
                return;
            for (var i = 0; i < layers.Count; i++)
            {
                var l = layers[i];
                if (l == null)
                    continue;
                var d = l.diffuseTexture;
                Debug.Log("[TerrainGenerator] Katman " + l.name + ": doku " + (d != null ? d.width + "x" + d.height : "YOK")
                    + ", döşeme " + l.tileSize.x.ToString("0.#") + " m"
                    + ", normal=" + (l.normalMapTexture != null ? "var" : "YOK")
                    + ", mask=" + (l.maskMapTexture != null ? "var" : "YOK"));
            }
        }

        private static void ConfigureTerrain(Terrain terrain, bool headless)
        {
            if (terrain == null)
                return;

            terrain.allowAutoConnect = false;
            terrain.groupingID = 0;
            // Yakın zemin keskinliği: Yüksek/Ultra'da piksel hatası ≤4; basemap geçişi ≥800 m (uzak zemin bulanıklaşmasın).
            var tier = Mathf.Clamp(Project.Infrastructure.Rendering.PostProcessing.QualityLevel, 0, 3);
            terrain.heightmapPixelError = tier >= 3 ? 3f : tier >= 2 ? 4f : 5f;
            terrain.basemapDistance = Mathf.Max(800f, TerrainPaintRules.BasemapDistance(tier));
            terrain.treeDistance = 1100f;
            terrain.treeBillboardDistance = 1100f;
            terrain.treeCrossFadeLength = 0f;
            terrain.treeMaximumFullLODCount = 4000;
            terrain.detailObjectDistance = PerformanceProfile.DetailObjectDistance(2);
            terrain.detailObjectDensity = PerformanceProfile.DetailDensityScale(2);
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
            {
                terrain.materialTemplate = material;
                try { TerrainShaderBinder.Install(terrain, Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, 3)); }
                catch (System.Exception e) { Debug.LogWarning("[HAREKÂT] TerrainShaderBinder atlandı: " + e.Message); }
            }
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
