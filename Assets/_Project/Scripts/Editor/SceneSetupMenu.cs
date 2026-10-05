using Project.Infrastructure.Config;
using Project.Presentation.Bootstrap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.EditorTools
{
    public static class SceneSetupMenu
    {
        private const string ConfigPath = "Assets/_Project/Settings/PlayerMovementConfig.asset";
        private const string ScenePath = "Assets/_Project/Scenes/TestArena.unity";

        [MenuItem("Project/Setup/Create Test Arena Scene")]
        public static void CreateTestArenaScene()
        {
            EnsureMovementConfig();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var bootstrapGo = new GameObject("TestArenaBootstrap");
            var bootstrap = bootstrapGo.AddComponent<TestArenaBootstrap>();
            var config = AssetDatabase.LoadAssetAtPath<PlayerMovementConfig>(ConfigPath);

            var so = new SerializedObject(bootstrap);
            so.FindProperty("movementConfig").objectReferenceValue = config;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log("[Project] Test Arena sahnesi oluşturuldu: " + ScenePath);
            Debug.Log("[Project] Play'e bas — FPP hareket otomatik kurulur.");
        }

        [MenuItem("Project/Setup/Create Player Movement Config")]
        public static void EnsureMovementConfig()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PlayerMovementConfig>(ConfigPath);
            if (existing != null)
                return;

            if (!AssetDatabase.IsValidFolder("Assets/_Project/Settings"))
                AssetDatabase.CreateFolder("Assets/_Project", "Settings");

            var config = ScriptableObject.CreateInstance<PlayerMovementConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssets();

            Debug.Log("[Project] PlayerMovementConfig oluşturuldu: " + ConfigPath);
        }

        [MenuItem("Project/Setup/Add Scene To Build Settings")]
        public static void AddSceneToBuild()
        {
            EnsureMovementConfig();

            if (!System.IO.File.Exists(ScenePath))
                CreateTestArenaScene();

            var scenes = EditorBuildSettings.scenes;
            foreach (var s in scenes)
            {
                if (s.path == ScenePath)
                {
                    Debug.Log("[Project] Sahne zaten Build Settings'te.");
                    return;
                }
            }

            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(scenes)
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };

            EditorBuildSettings.scenes = list.ToArray();
            Debug.Log("[Project] TestArena build settings'e eklendi.");
        }
    }
}
