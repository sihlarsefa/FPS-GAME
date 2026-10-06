using Project.Core.Domain;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Project.Infrastructure.World
{
    /// <summary>Dünya üretim seçenekleri.</summary>
    public sealed class WorldGenerationOptions
    {
        // ---------------------------------------------------------------- Sözleşme alanları
        public int Seed = 1923;

        /// <summary>Doldurulacak TerrainData (editör kalıcı varlık verir); null → yeni oluşturulur.</summary>
        public TerrainData TerrainData;

        public Transform Parent;
        public bool BakeNavMesh = true;
        public bool GenerateMinimap = true;
        public int MinimapSize = 1024;

        // ---------------------------------------------------------------- Ek ayarlar
        /// <summary>Hazır yerleşim (null → <see cref="MapLayout.CreateKuzgunVadisi"/>).</summary>
        public MapLayout Layout;

        /// <summary>Harita kimliği (<see cref="MapCatalog"/>): Layout verilmediyse yerleşimi seçer. Varsayılan "kuzgun".</summary>
        public string MapId = MapCatalog.Kuzgun;

        /// <summary>Yapıları kur (LocationBuilder). Kapalıysa yalnızca arazi + yedek noktalar.</summary>
        public bool BuildLocations = true;

        /// <summary>Yapı üreticisinin kurmadığı köprüleri kur.</summary>
        public bool BuildMissingBridges = true;

        public bool ScatterRocks = true;

        /// <summary>Yerleşimler arası mikro-POI geçişi (kaya öbeği, koru, kulübe, ağıl, siperlik...).</summary>
        public bool ScatterMicroPoi = true;
        public int RockCount = RockScatter.DefaultCount;
        public int TreeCount = TreeScatter.DefaultTargetCount;
        public int AlphamapResolution = TerrainGenerator.DefaultAlphamapResolution;
        public int LayerTextureSize = TerrainTextureFactory.DefaultTextureSize;

        /// <summary>NavMesh voksel boyu (m). Sözleşme ~0.18; çalışma zamanı pişirmesini hızlandırmak için büyütülebilir.</summary>
        public float NavMeshVoxelSize = NavMeshBaker.VoxelSize;

        public int GroundSpawnCount = 64;
        public int LandingZoneCount = 28;

        /// <summary>Yapı üreticisi bundan az araç noktası verdiyse yol kenarına tamamlanır.</summary>
        public int MinVehicleSpawns = 6;

        /// <summary>
        /// Görsel olmayan (adanmış sunucu — ör. Windows Server, -batchmode -nographics) üretim: doku/alphamap/su/mini harita
        /// üretilmez, arazi çizilmez; yükseklik, çarpıştırıcılar, ağaç çarpıştırıcıları, yapılar, noktalar ve NavMesh üretilir.
        /// Varsayılan <see cref="DetectHeadless"/>.
        /// </summary>
        public bool Headless = DetectHeadless();

        /// <summary>
        /// Oyuncu derlemesi grafik aygıtı olmadan mı çalışıyor (UNITY_SERVER derlemesi, -batchmode ya da -nographics)?
        /// Editörde her zaman false — editör toplu kurulumu (-batchmode) görsel varlıkları da üretip kaydetmelidir.
        /// </summary>
        /// <summary>
        /// Adanmış sunucu (Windows Server / Linux, grafiksiz) için seçenekler: görsel üretim yok, mini harita yok, NavMesh
        /// pişirilir (sunucu yapay zekâyı yetkili olarak çalıştırır). Not: çevrim içi oyunda istemci ile sunucunun aynı dünyayı
        /// görmesi için tercih edilen yol editörde üretilip sahneye kaydedilmiş KuzgunVadisi sahnesidir; bu çalışma zamanı
        /// üretimi yedektir (aynı tohum aynı dünyayı verir).
        /// </summary>
        public static WorldGenerationOptions ForDedicatedServer(int seed, Transform parent = null)
        {
            return new WorldGenerationOptions
            {
                Seed = seed,
                Parent = parent,
                Headless = true,
                GenerateMinimap = false,
                BakeNavMesh = true
            };
        }

        public static bool DetectHeadless()
        {
#if UNITY_SERVER
            return true;
#else
            if (UnityEngine.Application.isEditor)
                return false;
            return UnityEngine.Application.isBatchMode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null;
#endif
        }
    }

    /// <summary>
    /// Kuzgun Vadisi dünyası üretici. Sıra: arazi (yükseklik, katmanlar, ağaçlar) → yapılar (LocationBuilder.BuildAll) →
    /// eksik köprüler → yapı altındaki ağaçların temizlenmesi → su yüzeyleri → kayalar → WorldMetadata (bölgeler, ganimet,
    /// doğma/iniş/araç noktaları) → mini harita → NavMesh. Her aşama ayrı korunur: biri başarısız olursa kalanlar çalışır.
    /// Editörde (oynatma dışı) ve çalışma zamanında kullanılabilir.
    /// </summary>
    public static class WorldGenerator
    {
        public const string RootNamePrefix = "[Dünya] ";
        public const string StructuresRootName = "Yapılar";

        /// <summary>Son üretimin ağaç prototip nesneleri (indeks = TreeKind). Editör prefab olarak kaydeder.</summary>
        public static IReadOnlyList<GameObject> LastTreePrototypes { get; private set; } = new GameObject[0];

        /// <summary>Son üretimin arazi katmanları (editör varlık olarak kaydeder).</summary>
        public static IReadOnlyList<TerrainLayer> LastTerrainLayers { get; private set; } = new TerrainLayer[0];

        /// <summary>Son üretimin yerleşimi.</summary>
        public static MapLayout LastLayout { get; private set; }

        /// <summary>Son üretimin yapı sınırları (yapılar + köprüler).</summary>
        public static IReadOnlyList<Bounds> LastStructureBounds { get; private set; } = new Bounds[0];

        /// <summary>Son üretimin aşama süreleri (ms) — günlük/teşhis.</summary>
        public static string LastTimingReport { get; private set; } = string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            LastTreePrototypes = new GameObject[0];
            LastTerrainLayers = new TerrainLayer[0];
            LastLayout = null;
            LastStructureBounds = new Bounds[0];
            LastTimingReport = string.Empty;
        }

        public static WorldMetadata Generate(WorldGenerationOptions options)
        {
            options ??= new WorldGenerationOptions();
            var seed = options.Seed;
            var timer = Stopwatch.StartNew();
            var report = new System.Text.StringBuilder(256);

            var layout = options.Layout ?? MapLayout.Create(options.MapId, seed);
            LastLayout = layout;

            var root = new GameObject(RootNamePrefix + (string.IsNullOrEmpty(layout.Name) ? "Harita" : layout.Name));
            root.layer = GameLayers.Default;
            if (options.Parent != null)
                root.transform.SetParent(options.Parent, false);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            // ------------------------------------------------------------ 1) Arazi
            var settings = new TerrainBuildSettings
            {
                Headless = options.Headless,
                TreeCount = options.TreeCount,
                AlphamapResolution = options.AlphamapResolution,
                LayerTextureSize = options.LayerTextureSize
            };
            Terrain terrain;
            try
            {
                terrain = TerrainGenerator.Create(layout, options.TerrainData, root.transform, seed, settings);
            }
            catch (Exception)
            {
                // Arazisiz dünya anlamsız: yarım kalan kökü bırakma, çağıran (bootstrap) yedek zemine düşsün.
                SafeDestroy(root);
                throw;
            }

            var model = TerrainGenerator.LastModel;
            LastTreePrototypes = TerrainGenerator.LastTreePrototypes;
            LastTerrainLayers = TerrainGenerator.LastLayers;
            Mark(report, "arazi", timer);

            // Metadata erken eklenir: Awake (oynatmada) Instance'ı atar — yapı üreticisi WorldMetadata.Instance kullanabilir.
            var meta = root.AddComponent<WorldMetadata>();
            meta.Terrain = terrain;
            meta.Layout = layout;
            meta.MapHalfSize = layout.HalfSize;
            meta.MapCenter = Vector2.zero;
            meta.WaterLevel = layout.WaterLevel;
            meta.MaxHeight = layout.MaxHeight;
            FillLocations(meta, layout);

            // ------------------------------------------------------------ 2) Yapılar
            var loot = new List<LootSpawnPointData>(512);
            var structures = new List<Bounds>(256);
            var vehicles = new List<VehicleSpawnData>(16);
            if (options.BuildLocations)
            {
                var structuresRoot = new GameObject(StructuresRootName);
                structuresRoot.layer = GameLayers.Default;
                structuresRoot.transform.SetParent(root.transform, false);
                Guard("LocationBuilder.BuildAll", () =>
                    LocationBuilder.BuildAll(layout, terrain, structuresRoot.transform, seed, loot, structures, vehicles));
                Mark(report, "yapılar", timer);
            }

            if (options.BuildMissingBridges)
            {
                Guard("WorldBridges", () => WorldBridges.BuildMissing(layout, structures, root.transform, structures));
            }

            if (model != null)
                Guard("Yol detayları", () => RoadsideDetails.Build(layout, model, terrain, root.transform, structures));

            if (model != null && !options.Headless)
                Guard("Bitki çeşitliliği", () =>
                {
                    var vt = Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, 3);
                    var n = TreeScatter.AddVariety(terrain, model, seed, vt, root.transform);
                    UnityEngine.Debug.Log("[Bitki] Çeşitlilik örnekleri: " + n);
                });

            Guard("Cephe hattı", () => FrontlineBuilder.BuildIfApplicable(layout, model, root.transform, seed, structures, options.Headless));

            Guard("Ağaç temizliği", () => TreeScatter.RemoveTreesInBounds(terrain, structures, 1.5f));
            Guard("Çimen temizliği", () => VegetationPainter.RemoveDetailsInBounds(terrain, structures, 1.2f));

            // ------------------------------------------------------------ 3) Su ve kayalar
            if (!options.Headless)
                Guard("WorldWater", () => WorldWater.Build(layout, model, root.transform));

            // Mikro-POI: yerleşimler arası boşluğu doldurur; izleri (kulübe/ağıl/siperlik) kaya ve doğma noktalarından kaçınılır.
            var obstacles = new List<Bounds>(structures);
            if (options.ScatterMicroPoi && model != null)
            {
                Guard("MicroPoi", () =>
                {
                    var micro = MicroPoi.Build(model, terrain, root.transform, seed, structures);
                    if (micro != null)
                    {
                        obstacles.AddRange(micro.Footprints);
                        Debug.Log("[WorldGenerator] Mikro-POI: " + micro.PoiCount + " nokta, " + micro.ItemCount + " öğe.");
                    }
                });
                Mark(report, "mikro-poi", timer);
            }

            if (model != null && !options.Headless)
            {
                Guard("Düşmüş kütükler", () =>
                {
                    var spots = TreeScatter.ScatterLogSpots(model, seed);
                    if (spots == null || spots.Count == 0)
                        return;
                    var logRoot = new GameObject("FallenLogs").transform;
                    logRoot.SetParent(root.transform, false);
                    var placed = 0;
                    for (var i = 0; i < spots.Count; i++)
                    {
                        var spot = spots[i];
                        var pos = spot.Position;
                        var blocked = false;
                        for (var k = 0; k < obstacles.Count; k++)
                        {
                            if (obstacles[k].Contains(pos))
                            {
                                blocked = true;
                                break;
                            }
                        }

                        if (blocked)
                            continue;
                        PropFactory.FallenLog(logRoot, pos, spot.Yaw, spot.Length);
                        placed++;
                    }

                    Debug.Log("[WorldGenerator] Düşmüş kütük: " + placed + ".");
                });
            }

            if (options.ScatterRocks && model != null)
                Guard("RockScatter", () => RockScatter.Scatter(model, root.transform, seed, obstacles, options.RockCount));
            Mark(report, "su+kaya", timer);

            // ------------------------------------------------------------ 4) Metadata noktaları
            meta.StructureBounds = new List<Bounds>(structures);
            LastStructureBounds = meta.StructureBounds;
            Guard("Dünya noktaları", () => FillPoints(meta, options, model, terrain, obstacles, loot, vehicles, seed));
            Mark(report, "noktalar", timer);

            // ------------------------------------------------------------ 5) Mini harita
            if (options.GenerateMinimap && !options.Headless)
            {
                Guard("MinimapTextureGenerator", () =>
                    meta.MinimapTexture = MinimapTextureGenerator.Generate(terrain, layout, structures, options.MinimapSize));
                Mark(report, "mini harita", timer);
            }

            // ------------------------------------------------------------ 6) NavMesh
            if (options.BakeNavMesh)
            {
                Guard("NavMeshBaker", () =>
                {
                    var data = BakeNavMesh(meta, layout, terrain, options.NavMeshVoxelSize);
                    meta.NavMesh = data;
                    if (data != null && UnityEngine.Application.isPlaying)
                        meta.EnsureNavMeshLoaded();
                });
                Mark(report, "navmesh", timer);
            }

            LastTimingReport = report.ToString();
            Debug.Log("[WorldGenerator] " + layout.Name + " üretildi (tohum " + seed + (options.Headless ? ", görsel yok" : string.Empty) + ") — "
                      + LastTimingReport + " | ganimet " + meta.LootPoints.Count + ", iniş " + meta.LandingZones.Count + ", doğma "
                      + meta.GroundSpawnPoints.Count + ", araç " + meta.VehicleSpawns.Count + ", yapı " + structures.Count);
            return meta;
        }

        /// <summary>Var olan bir dünya için NavMesh pişirir (harita sınırları; göl derinliği yürünemez).</summary>
        public static NavMeshData BakeNavMesh(WorldMetadata meta, MapLayout layout, Terrain terrain, float voxelSize)
        {
            if (meta == null)
                return null;

            var bounds = meta.WorldBounds;
            bounds.Expand(new Vector3(0f, 40f, 0f));
            var extra = new List<NavMeshBuildSource>(4);
            if (layout != null)
            {
                for (var i = 0; i < layout.Lakes.Count; i++)
                {
                    var lake = layout.Lakes[i];
                    if (lake == null || lake.Depth < 1.5f || lake.Radius < 8f)
                        continue;

                    // Göletin ~1.5 m'den derin iç kısmı (kare, çemberin içinde kalır).
                    var side = lake.Radius * 0.85f;
                    extra.Add(NavMeshBaker.CreateModifierBox(new Vector3(lake.Center.x, layout.WaterLevel - 3f, lake.Center.y),
                        new Vector3(side, 12f, side), Quaternion.identity, NavMeshBaker.AreaNotWalkable));
                }
            }

            return NavMeshBaker.Bake(bounds, terrain, GameLayers.WorldMask, extra, voxelSize);
        }

        // ================================================================== Doldurma

        private static void FillLocations(WorldMetadata meta, MapLayout layout)
        {
            meta.Locations.Clear();
            for (var i = 0; i < layout.Locations.Count; i++)
            {
                var l = layout.Locations[i];
                if (l == null)
                    continue;
                meta.Locations.Add(new NamedLocation
                {
                    Name = l.Name,
                    Center = l.Center,
                    Radius = l.Radius,
                    IsMajor = l.IsMajor
                });
            }
        }

        private static void FillPoints(WorldMetadata meta, WorldGenerationOptions options, TerrainModel model, Terrain terrain,
            List<Bounds> structures, List<LootSpawnPointData> loot, List<VehicleSpawnData> vehicles, int seed)
        {
            meta.LootPoints.Clear();
            meta.VehicleSpawns.Clear();
            meta.GroundSpawnPoints.Clear();
            meta.LandingZones.Clear();
            meta.LootPoints.AddRange(loot);
            meta.VehicleSpawns.AddRange(vehicles);

            if (model == null)
                return;

            var planner = new WorldSpawnPlanner(model, terrain, structures, seed);
            var supplement = planner.PlanSupplementLoot(meta.LootPoints);
            if (supplement.Count > 0)
            {
                Debug.Log("[WorldGenerator] Ganimet yoğunluğu tamamlandı: yapılardan " + meta.LootPoints.Count + " + açık alan " + supplement.Count
                          + " nokta.");
                meta.LootPoints.AddRange(supplement);
            }

            var groundVehicles = 0;
            for (var vi = 0; vi < meta.VehicleSpawns.Count; vi++)
                if (!meta.VehicleSpawns[vi].IsAir)
                    groundVehicles++;
            if (groundVehicles < options.MinVehicleSpawns)
                meta.VehicleSpawns.AddRange(planner.PlanRoadsideVehicles(options.MinVehicleSpawns - groundVehicles, meta.VehicleSpawns));

            meta.GroundSpawnPoints.AddRange(planner.PlanGroundSpawns(Mathf.Max(0, options.GroundSpawnCount), 55f));
            meta.LandingZones.AddRange(planner.PlanLandingZones(Mathf.Max(0, options.LandingZoneCount), 70f));
        }

        // ================================================================== Yardımcılar

        private static void Guard(string stage, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Debug.LogError("[WorldGenerator] '" + stage + "' aşaması başarısız: " + e);
            }
        }

        private static void Mark(System.Text.StringBuilder report, string stage, Stopwatch timer)
        {
            if (report.Length > 0)
                report.Append(", ");
            report.Append(stage).Append(' ').Append(timer.ElapsedMilliseconds).Append(" ms");
            timer.Restart();
        }

        /// <summary>
        /// Üretilen dünyadaki bellek içi (henüz varlık olmayan) nesneleri toplar — editör sahneyi kaydetmeden önce bunları varlık
        /// olarak kaydetmelidir: mesh'ler (MeshFilter/MeshCollider), TerrainData + katmanları + katman dokuları, mini harita
        /// dokusu, NavMeshData. Malzemeler dahil edilmez (MaterialLibrary/GameArtLibrary yönetir). Tekrarsız; null güvenli.
        /// Ağaç prototipleri ayrıca <see cref="LastTreePrototypes"/> ile prefab'a çevrilmelidir.
        /// </summary>
        public static void CollectGeneratedObjects(WorldMetadata meta, ICollection<Object> output)
        {
            if (meta == null || output == null)
                return;

            var seen = new HashSet<Object>();
            void Add(Object o)
            {
                if (o != null && seen.Add(o))
                    output.Add(o);
            }

            foreach (var filter in meta.GetComponentsInChildren<MeshFilter>(true))
                Add(filter.sharedMesh);
            foreach (var collider in meta.GetComponentsInChildren<MeshCollider>(true))
                Add(collider.sharedMesh);

            var terrain = meta.Terrain;
            if (terrain != null && terrain.terrainData != null)
            {
                Add(terrain.terrainData);
                var layers = terrain.terrainData.terrainLayers;
                if (layers != null)
                {
                    for (var i = 0; i < layers.Length; i++)
                    {
                        if (layers[i] == null)
                            continue;
                        Add(layers[i].diffuseTexture);
                        Add(layers[i].normalMapTexture);
                        Add(layers[i]);
                    }
                }
            }

            Add(meta.MinimapTexture);
            Add(meta.NavMesh);
        }

        /// <summary>Editör/oynatma uyumlu yok etme.</summary>
        internal static void SafeDestroy(Object target)
        {
            if (target == null)
                return;
            if (UnityEngine.Application.isPlaying)
                Object.Destroy(target);
            else
                Object.DestroyImmediate(target);
        }
    }
}
