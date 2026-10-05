using System;
using System.Collections.Generic;
using System.IO;
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
            EnsureTrainingRange();
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
        {
            var path = SceneNames.PathOf(SceneNames.Operation);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EnsureFolder("Assets/_Project/Art/World");
            EnsureFolder("Assets/_Project/Art/World/KuzgunVadisi");

            var worldRoot = new GameObject("[Dünya]");
            TerrainData terrainData = null;
            try
            {
                var tdPath = "Assets/_Project/Art/World/KuzgunVadisi/TerrainData.asset";
                terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(tdPath);
                if (terrainData == null)
                {
                    terrainData = new TerrainData();
                    terrainData.heightmapResolution = 513;
                    terrainData.size = new Vector3(1024f, 160f, 1024f);
                    AssetDatabase.CreateAsset(terrainData, tdPath);
                }

                var options = new WorldGenerationOptions
                {
                    Seed = 1923,
                    TerrainData = terrainData,
                    Parent = worldRoot.transform,
                    BakeNavMesh = true,
                    GenerateMinimap = true,
                    MinimapSize = 512
                };

                var meta = WorldGenerator.Generate(options);

                // Lokasyonlar (WorldGenerator stub ise burada tamamla)
                try
                {
                    var layout = MapLayout.CreateKuzgunVadisi(1923);
                    var terrain = meta != null ? meta.Terrain : null;
                    if (terrain == null)
                        terrain = UnityEngine.Object.FindFirstObjectByType<Terrain>();
                    var loot = new List<LootSpawnPointData>();
                    var structures = new List<Bounds>();
                    var vehicles = new List<VehicleSpawnData>();
                    LocationBuilder.BuildAll(layout, terrain, worldRoot.transform, 1923, loot, structures, vehicles);
                    if (meta != null)
                    {
                        meta.LootPoints = loot;
                        meta.StructureBounds = structures;
                        meta.VehicleSpawns = vehicles;
                        meta.MapHalfSize = layout.HalfSize;
                        meta.WaterLevel = layout.WaterLevel;
                        meta.MaxHeight = layout.MaxHeight;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[HAREKÂT] LocationBuilder: " + e.Message);
                }

                PersistWorldAssets(meta, worldRoot);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[HAREKÂT] Dünya üretimi kısmi: " + e.Message);
            }

            var bootstrap = new GameObject("MatchBootstrap");
            bootstrap.AddComponent<MatchBootstrap>();
            EnsureLight();
            MarkStaticRecursive(worldRoot);

            EditorSceneManager.SaveScene(scene, path);
            Debug.Log("[HAREKÂT] KuzgunVadisi oluşturuldu: " + path);
        }

        private static void PersistWorldAssets(WorldMetadata meta, GameObject worldRoot)
        {
            if (meta == null)
                return;

            var folder = "Assets/_Project/Art/World/KuzgunVadisi";
            EnsureFolder(folder);

            // NavMesh
            if (meta.NavMesh != null)
            {
                var navPath = folder + "/NavMesh.asset";
                if (AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath) == null)
                {
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
                    if (!File.Exists(png) && meta.MinimapTexture.isReadable)
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
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log("[HAREKÂT] TrainingRange oluşturuldu: " + path);
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

        private static void MarkStaticRecursive(GameObject root)
        {
            if (root == null)
                return;
            GameObjectUtility.SetStaticEditorFlags(root,
                StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.NavigationStatic);
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
