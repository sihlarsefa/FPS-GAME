using System;
using System.Collections.Generic;
using System.IO;
using Project.Core.Domain;
using Project.Infrastructure.World;
using Project.Presentation.Bootstrap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Project.EditorTools
{
    /// <summary>Oyun sahnelerini üretir ve Build Settings'e yazar.</summary>
    public static class SceneBuilder
    {
        public static void EnsureAllScenes()
        {
            EnsureFolder(SceneNames.ScenesFolder);
            EnsureMainMenu();
            EnsureKuzgunVadisi();
            EnsureAyazGecidi();
            EnsureMaviLiman();
            EnsureKartalYaylasi();
            EnsureTrainingRange();
        }

        /// <summary>Yalnızca diskte olmayan sahneleri üretir (mevcutlara dokunmaz) — build öncesi eksik sahne kalmasın.</summary>
        public static void EnsureMissingScenes()
        {
            EnsureFolder(SceneNames.ScenesFolder);
            if (!File.Exists(SceneNames.PathOf(SceneNames.MainMenu))) EnsureMainMenu();
            if (!File.Exists(SceneNames.PathOf(SceneNames.Operation))) EnsureKuzgunVadisi();
            if (!File.Exists(SceneNames.PathOf(SceneNames.AyazGecidi))) EnsureAyazGecidi();
            if (!File.Exists(SceneNames.PathOf(SceneNames.MaviLiman))) EnsureMaviLiman();
            if (!File.Exists(SceneNames.PathOf(SceneNames.KartalYaylasi))) EnsureKartalYaylasi();
            if (!File.Exists(SceneNames.PathOf(SceneNames.Training))) EnsureTrainingRange();
            if (!File.Exists(SceneNames.PathOf(SceneNames.AaaBenchmark)))
            {
                try { AaaBenchmarkSceneBuilder.EnsureScene(); }
                catch (Exception e) { Debug.LogWarning("[HAREKÂT] AAA Benchmark sahnesi atlandı: " + e.Message); }
            }
        }

        public static void EnsureBuildSettings()
        {
            var list = new List<EditorBuildSettingsScene>();
            foreach (var name in SceneNames.BuildOrder)
            {
                var path = SceneNames.PathOf(name);
                if (!File.Exists(path))
                {
                    Debug.LogWarning("[HAREKÂT] Build Settings: sahne yok — " + path);
                    continue;
                }

                list.Add(new EditorBuildSettingsScene(path, true));
            }

            EditorBuildSettings.scenes = list.ToArray();
            Debug.Log("[HAREKÂT] Build Settings: " + list.Count + " sahne.");
        }

        private static void EnsureMainMenu()
        {
            var path = SceneNames.PathOf(SceneNames.MainMenu);
            if (File.Exists(path))
            {
                Debug.Log("[HAREKÂT] MainMenu zaten var.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var go = new GameObject("MainMenuBootstrap");
            go.AddComponent<MainMenuBootstrap>();
            EnsureLight();
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log("[HAREKÂT] MainMenu oluşturuldu: " + path);
        }

        private static void EnsureKuzgunVadisi()
            => EnsureOperationScene(SceneNames.Operation, "KuzgunVadisi", MapCatalog.Kuzgun, 1024f, 160f);

        private static void EnsureAyazGecidi()
            => EnsureOperationScene(SceneNames.AyazGecidi, "AyazGecidi", MapCatalog.AyazGecidi, 1000f, 220f);

        private static void EnsureMaviLiman()
            => EnsureOperationScene(SceneNames.MaviLiman, "MaviLiman", MapCatalog.MaviLiman, 1000f, 120f);

        private static void EnsureKartalYaylasi()
            => EnsureOperationScene(SceneNames.KartalYaylasi, "KartalYaylasi", MapCatalog.KartalYaylasi, 1000f, 190f);

        private static void EnsureOperationScene(string sceneName, string folderName, string mapId, float terrainSize, float terrainHeight)
        {
            var path = SceneNames.PathOf(sceneName);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EnsureFolder("Assets/_Project/Art/World");
            EnsureFolder("Assets/_Project/Art/World/" + folderName);

            var worldRoot = new GameObject("[Dünya]");
            TerrainData terrainData = null;
            try
            {
                var tdPath = "Assets/_Project/Art/World/" + folderName + "/TerrainData.asset";
                terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(tdPath);
                if (terrainData == null)
                {
                    terrainData = new TerrainData();
                    terrainData.heightmapResolution = 513;
                    terrainData.size = new Vector3(terrainSize, terrainHeight, terrainSize);
                    AssetDatabase.CreateAsset(terrainData, tdPath);
                }

                var options = new WorldGenerationOptions
                {
                    Seed = 1923,
                    MapId = mapId,
                    TerrainData = terrainData,
                    Parent = worldRoot.transform,
                    BakeNavMesh = true,
                    GenerateMinimap = true,
                    MinimapSize = 1024
                };

                // WorldGenerator.Generate lokasyonları (LocationBuilder.BuildAll) zaten kurar ve WorldMetadata'yı doldurur.
                // Burada tekrar çağırmak üst üste binen ikinci bir yapı/çarpıştırıcı takımı üretir ve ganimet noktalarını ezer.
                var meta = WorldGenerator.Generate(options);
                if (meta == null)
                    Debug.LogWarning("[HAREKÂT] WorldGenerator WorldMetadata döndürmedi.");

                PersistWorldAssets(meta, worldRoot, folderName);
                if (EnablePostWorldHooks)
                    RunPostWorldHooks(meta, worldRoot);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[HAREKÂT] Dünya üretimi kısmi: " + e.Message);
            }

            var bootstrap = new GameObject("MatchBootstrap");
            bootstrap.AddComponent<MatchBootstrap>();
            EnsureLight();
            try { EnsureLighting(mapId, worldRoot); }
            catch (Exception e) { Debug.LogWarning("[HAREKÂT] Aydınlatma kurulumu kısmi: " + e.Message); }
            MarkStaticRecursive(worldRoot);
            PersistRuntimeAssets(worldRoot, folderName, terrainData);

            EditorSceneManager.SaveScene(scene, path);
            Debug.Log("[HAREKÂT] " + sceneName + " oluşturuldu: " + path);
        }

        /// <summary>Dünya üretimi sonrası isteğe bağlı kancalar (yansıma probları + HLOD). Kapatmak için false.</summary>
        public static bool EnablePostWorldHooks = true;

        private static void RunPostWorldHooks(WorldMetadata meta, GameObject worldRoot)
        {
            try
            {
                if (meta != null && WorldGenerator.LastLayout != null)
                    Project.Infrastructure.Rendering.ReflectionProbePlacer.PlaceFromWorld(WorldGenerator.LastLayout, meta, -1, worldRoot.transform);
            }
            catch (Exception e) { Debug.LogWarning("[HAREKÂT] ReflectionProbePlacer atlandı: " + e.Message); }

            try
            {
                var locRoot = FindDeep(worldRoot.transform, "[Lokasyonlar]");
                if (locRoot == null) return;
                var made = 0;
                for (var i = 0; i < locRoot.childCount; i++)
                {
                    var child = locRoot.GetChild(i);
                    if (child.name.StartsWith("[")) continue; // [YolKenarı] vb.
                    if (HlodBuilder.Build(child.gameObject, HlodBuilder.MinPartExtent, HlodBuilder.TriBudget)) made++;
                }
                Debug.Log("[HAREKÂT] HLOD: " + made + " yerleşim için proxy üretildi.");
            }
            catch (Exception e) { Debug.LogWarning("[HAREKÂT] HLOD atlandı: " + e.Message); }
        }

        private static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (var i = 0; i < t.childCount; i++)
            {
                var r = FindDeep(t.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        private static void PersistWorldAssets(WorldMetadata meta, GameObject worldRoot, string folderName)
        {
            if (meta == null)
                return;

            var folder = "Assets/_Project/Art/World/" + folderName;
            EnsureFolder(folder);

            // NavMesh
            if (meta.NavMesh != null)
            {
                var navPath = folder + "/NavMesh.asset";
                if (!AssetDatabase.Contains(meta.NavMesh))
                {
                    // Eski (bayat) NavMesh varlığı yeni pişirmeyle değiştirilir.
                    if (AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath) != null)
                        AssetDatabase.DeleteAsset(navPath);
                    AssetDatabase.CreateAsset(UnityEngine.Object.Instantiate(meta.NavMesh), navPath);
                    meta.NavMesh = AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);
                }
            }

            // Minimap PNG
            if (meta.MinimapTexture != null)
            {
                var png = folder + "/Minimap.png";
                try
                {
                    if (!AssetDatabase.Contains(meta.MinimapTexture) && meta.MinimapTexture.isReadable)
                    {
                        File.WriteAllBytes(png, meta.MinimapTexture.EncodeToPNG());
                        AssetDatabase.ImportAsset(png);
                    }

                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(png);
                    if (tex != null)
                        meta.MinimapTexture = tex;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[HAREKÂT] Minimap kaydı: " + e.Message);
                }
            }

            // Ağaç prototipleri
            try
            {
                var trees = WorldGenerator.LastTreePrototypes;
                if (trees == null || trees.Count == 0)
                    trees = TerrainGenerator.LastTreePrototypes;
                if (trees != null)
                {
                    for (var i = 0; i < trees.Count; i++)
                    {
                        if (trees[i] == null)
                            continue;
                        var prefabPath = folder + "/Tree_" + i + ".prefab";
                        if (File.Exists(prefabPath))
                            continue;
                        PrefabUtility.SaveAsPrefabAsset(trees[i], prefabPath);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[HAREKÂT] Ağaç prefab: " + e.Message);
            }

            EditorUtility.SetDirty(meta);
        }

        private static void EnsureTrainingRange()
        {
            var path = SceneNames.PathOf(SceneNames.Training);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var parent = new GameObject("[Poligon]").transform;
            try
            {
                TrainingRangeBuilder.Build(parent);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[HAREKÂT] TrainingRangeBuilder: " + e.Message);
            }

            var boot = new GameObject("TrainingBootstrap");
            boot.AddComponent<TrainingBootstrap>();
            EnsureLight();
            MarkStaticRecursive(parent.gameObject);
            PersistRuntimeAssets(parent.gameObject, "TrainingRange", null);
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log("[HAREKÂT] TrainingRange oluşturuldu: " + path);
        }

        /// <summary>
        /// Kök altındaki tüm bellek içi Mesh/Material/Texture/TerrainLayer nesnelerini Assets/_Project/Generated/&lt;sahne&gt;/ altına
        /// varlık olarak yazar (örnek bazında tekilleştirir) — sahne kaydedilince gömülü/kayıp referans kalmaz. Tekrar çalıştırmada üzerine yazar.
        /// </summary>
        private static void PersistRuntimeAssets(GameObject root, string sceneName, TerrainData terrainData)
        {
            if (root == null)
                return;
            try
            {
                var folder = "Assets/_Project/Generated/" + sceneName;
                if (AssetDatabase.IsValidFolder(folder))
                    AssetDatabase.DeleteAsset(folder);
                EnsureFolder(folder);

                var done = new Dictionary<UnityEngine.Object, UnityEngine.Object>();
                var counter = 0;
                T Save<T>(T obj, string prefix, string ext) where T : UnityEngine.Object
                {
                    if (obj == null || EditorUtility.IsPersistent(obj))
                        return obj;
                    if (done.TryGetValue(obj, out var existing))
                        return (T)existing;
                    var n = string.IsNullOrEmpty(obj.name) ? "Unnamed" : obj.name;
                    var chars = n.ToCharArray();
                    for (var i = 0; i < chars.Length; i++)
                        if (!char.IsLetterOrDigit(chars[i]) || chars[i] > 127)
                            chars[i] = '_';
                    var p = folder + "/" + prefix + "_" + (counter++) + "_" + new string(chars) + ext;
                    AssetDatabase.CreateAsset(obj, p);
                    done[obj] = obj;
                    return obj;
                }

                var texIds = new List<int>(8);
                void SaveMaterial(Material m)
                {
                    if (m == null || EditorUtility.IsPersistent(m) || done.ContainsKey(m))
                        return;
                    texIds.Clear();
                    m.GetTexturePropertyNameIDs(texIds);
                    foreach (var id in texIds.ToArray())
                    {
                        var t = m.GetTexture(id);
                        if (t != null && !EditorUtility.IsPersistent(t))
                            Save(t, "Tex", ".asset");
                    }

                    Save(m, "Mat", ".mat");
                }

                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    var changed = false;
                    for (var i = 0; i < mats.Length; i++)
                    {
                        SaveMaterial(mats[i]);
                        changed = true;
                    }

                    if (changed && mats.Length > 0)
                        r.sharedMaterials = mats;
                }

                foreach (var f in root.GetComponentsInChildren<MeshFilter>(true))
                    if (f.sharedMesh != null)
                        Save(f.sharedMesh, "Mesh", ".asset");
                foreach (var c in root.GetComponentsInChildren<MeshCollider>(true))
                    if (c.sharedMesh != null)
                        Save(c.sharedMesh, "Mesh", ".asset");

                foreach (var terrain in root.GetComponentsInChildren<Terrain>(true))
                {
                    var td = terrain.terrainData != null ? terrain.terrainData : terrainData;
                    if (td == null)
                        continue;
                    var layers = td.terrainLayers;
                    if (layers != null)
                    {
                        for (var i = 0; i < layers.Length; i++)
                        {
                            var l = layers[i];
                            if (l == null || EditorUtility.IsPersistent(l))
                                continue;
                            if (l.diffuseTexture != null) Save(l.diffuseTexture, "Tex", ".asset");
                            if (l.normalMapTexture != null) Save(l.normalMapTexture, "Tex", ".asset");
                            if (l.maskMapTexture != null) Save(l.maskMapTexture, "Tex", ".asset");
                            Save(l, "Layer", ".terrainlayer");
                        }

                        td.terrainLayers = layers;
                    }

                    if (EditorUtility.IsPersistent(td))
                        EditorUtility.SetDirty(td);
                }

                AssetDatabase.SaveAssets();
                Debug.Log("[HAREKÂT] " + sceneName + ": " + done.Count + " üretilen varlık kaydedildi → " + folder);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[HAREKÂT] Üretilen varlık kaydı (" + sceneName + "): " + e.Message);
            }
        }

        private static void EnsureLight()
        {
            if (UnityEngine.Object.FindFirstObjectByType<Light>() != null)
                return;
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        /// <summary>
        /// Gerçekçi aydınlatma: güneş renk sıcaklığı/şiddeti, gradyan ortam, harita sis yoğunluğu, yerleşim merkezlerine
        /// yansıma probları ve yapı çevresine ışık probu grupları (karakter/silah modelleri için doğru dolaylı ışık).
        /// </summary>
        private static void EnsureLighting(string mapId, GameObject worldRoot)
        {
            var grade = MapGradeTable.For(mapId);
            var sun = UnityEngine.Object.FindFirstObjectByType<Light>();
            if (sun != null && sun.type == LightType.Directional)
            {
                var k = MapCatalog.Normalize(mapId) == MapCatalog.AyazGecidi ? 7200f : MapCatalog.Normalize(mapId) == MapCatalog.MaviLiman ? 5900f : MapCatalog.Normalize(mapId) == MapCatalog.KartalYaylasi ? 5700f : 5400f;
                var rgb = MapGradeTable.KelvinToRgb(k);
                sun.color = new Color(rgb[0], rgb[1], rgb[2]);
                sun.intensity = 1.25f * grade.SunIntensityMul;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.9f;
                sun.shadowNormalBias = 0.4f;
                RenderSettings.sun = sun;
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.46f, 0.56f, 0.72f);
            RenderSettings.ambientEquatorColor = new Color(0.40f, 0.42f, 0.40f);
            RenderSettings.ambientGroundColor = new Color(0.20f, 0.18f, 0.15f);
            RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Skybox;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0016f * AtmosphereRules.MapFogMultiplier(mapId, TimeOfDay.Gunduz) * grade.FogMul;

            var meta = UnityEngine.Object.FindFirstObjectByType<WorldMetadata>();
            if (meta == null)
                return;

            var root = new GameObject("[Aydınlatma]");
            if (worldRoot != null)
                root.transform.SetParent(worldRoot.transform, false);

            // Yansıma probları: ana yerleşim merkezleri (en çok 8; kutu 80 m, gerçek zamanlı ama yalnız bir kez).
            var probeCount = 0;
            for (var i = 0; i < meta.Locations.Count && probeCount < 8; i++)
            {
                var loc = meta.Locations[i];
                if (loc == null || !loc.IsMajor)
                    continue;
                var h = meta.SampleGroundHeight(new Vector3(loc.Center.x, 0f, loc.Center.y));
                var go = new GameObject("ReflectionProbe_" + loc.Name);
                go.transform.SetParent(root.transform, false);
                go.transform.position = new Vector3(loc.Center.x, h + 12f, loc.Center.y);
                var probe = go.AddComponent<ReflectionProbe>();
                probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
                probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.OnAwake;
                probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.IndividualFaces;
                probe.resolution = 128;
                probe.size = new Vector3(Mathf.Max(80f, loc.Radius * 2f), 60f, Mathf.Max(80f, loc.Radius * 2f));
                probe.boxProjection = true;
                probe.intensity = 1f;
                probeCount++;
            }

            // Işık probu grupları: yapı sınırlarının köşelerinde ve ortasında, iki yükseklikte (en çok 40 yapı).
            var groupCount = 0;
            for (var i = 0; i < meta.StructureBounds.Count && groupCount < 40; i++)
            {
                var b = meta.StructureBounds[i];
                if (b.size.x < 4f || b.size.z < 4f)
                    continue;
                var go = new GameObject("LightProbes_" + i);
                go.transform.SetParent(root.transform, false);
                go.transform.position = b.center;
                var group = go.AddComponent<LightProbeGroup>();
                var e = b.extents + new Vector3(2f, 0f, 2f);
                var lo = -e.y + 0.5f;
                var hi = Mathf.Max(lo + 1f, e.y - 0.5f);
                group.probePositions = new[]
                {
                    new Vector3(-e.x, lo, -e.z), new Vector3(e.x, lo, -e.z), new Vector3(-e.x, lo, e.z), new Vector3(e.x, lo, e.z),
                    new Vector3(0f, lo, 0f), new Vector3(0f, hi, 0f),
                    new Vector3(-e.x, hi, -e.z), new Vector3(e.x, hi, e.z)
                };
                groupCount++;
            }

            try { DynamicGI.UpdateEnvironment(); } catch (Exception) { }
        }

        /// <summary>Açık sahne için oklüzyon bake'i (isteğe bağlı; sahne büyükse dakikalar sürebilir).</summary>
        [MenuItem("HAREKÂT/Performans/Oklüzyon Bake (açık sahne)")]
        public static void BakeOcclusion()
        {
            try
            {
                StaticOcclusionCulling.Compute();
                Debug.Log("[HAREKÂT] Oklüzyon bake tamamlandı: " + SceneManager.GetActiveScene().name);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[HAREKÂT] Oklüzyon bake başarısız: " + e.Message);
            }
        }

        private static void MarkStaticRecursive(GameObject root)
        {
            if (root == null)
                return;
            var flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.NavigationStatic;
            var mf = root.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null && mf.sharedMesh.bounds.size.magnitude * root.transform.lossyScale.magnitude >= 12f)
                flags |= StaticEditorFlags.OccluderStatic; // büyük yapılar oklüzyon perdesi olur
            GameObjectUtility.SetStaticEditorFlags(root, flags);
            for (var i = 0; i < root.transform.childCount; i++)
                MarkStaticRecursive(root.transform.GetChild(i).gameObject);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            var parts = path.Split('/');
            var cur = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
    }
}
