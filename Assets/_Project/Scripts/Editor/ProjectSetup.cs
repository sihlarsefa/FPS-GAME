using System;
using UnityEditor;
using UnityEngine;

namespace Project.EditorTools
{
    /// <summary>HAREKÂT kurulum adımlarının orkestrasyonu.</summary>
    public static class ProjectSetup
    {
        [MenuItem("HAREKÂT/Kurulum/1) Her Şeyi Kur", priority = 1)]
        public static void RunAllSteps()
        {
            Debug.Log("[HAREKÂT] === Tam kurulum başladı ===");
            RunStep("Katmanlar ve etiketler", SetupLayersAndTags);
            RunStep("Player Settings", SetupPlayerSettings);
            RunStep("URP kalite profili", () => UrpSetup.EnsureAll());
            RunStep("Sanat kütüphanesi", AssetGeneration.EnsureArtLibrary);
            RunStep("Gölgelendiriciler (Always Included)", AlwaysIncludedShaders.EnsureAll);
            RunStep("Üçüncü taraf modeller (Poly Haven bağlama)", BatchEntry.BindThirdParty);
            RunStep("Sahneler", SceneBuilder.EnsureAllScenes);
            RunStep("AAA Benchmark sahnesi", AaaBenchmarkSceneBuilder.EnsureScene);
            RunStep("Build Settings sırası", SceneBuilder.EnsureBuildSettings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[HAREKÂT] === Tam kurulum tamamlandı ===");
        }

        [MenuItem("HAREKÂT/Kurulum/2) Katmanlar ve Etiketler", priority = 2)]
        public static void MenuLayers() => RunStep("Katmanlar ve etiketler", SetupLayersAndTags);

        [MenuItem("HAREKÂT/Kurulum/3) Player Settings", priority = 3)]
        public static void MenuPlayer() => RunStep("Player Settings", SetupPlayerSettings);

        [MenuItem("HAREKÂT/Kurulum/4) URP", priority = 4)]
        public static void MenuUrp() => RunStep("URP", () => UrpSetup.EnsureAll());

        [MenuItem("HAREKÂT/Kurulum/5) Sanat Kütüphanesi", priority = 5)]
        public static void MenuArt() => RunStep("Sanat kütüphanesi", AssetGeneration.EnsureArtLibrary);

        [MenuItem("HAREKÂT/Kurulum/6) Sahneler", priority = 6)]
        public static void MenuScenes()
        {
            RunStep("Sahneler", SceneBuilder.EnsureAllScenes);
            RunStep("Build Settings", SceneBuilder.EnsureBuildSettings);
        }

        public static void RunStep(string name, Action action)
        {
            try
            {
                Debug.Log("[HAREKÂT] ▶ " + name);
                action();
                Debug.Log("[HAREKÂT] ✓ " + name);
            }
            catch (Exception e)
            {
                Debug.LogError("[HAREKÂT] ✗ " + name + ": " + e.Message + "\n" + e.StackTrace);
            }
        }

        public static void SetupLayersAndTags()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            var names = Project.Infrastructure.GameLayers.CustomLayerNames;
            for (var i = 0; i < names.Length && i < layers.arraySize; i++)
            {
                if (string.IsNullOrEmpty(names[i]))
                    continue;
                var sp = layers.GetArrayElementAtIndex(i);
                if (sp.stringValue != names[i])
                    sp.stringValue = names[i];
            }

            var tags = tagManager.FindProperty("tags");
            EnsureTag(tags, "Head");
            tagManager.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[HAREKÂT] Katmanlar ve Head etiketi yazıldı.");
        }

        private static void EnsureTag(SerializedProperty tags, string tag)
        {
            for (var i = 0; i < tags.arraySize; i++)
            {
                if (tags.GetArrayElementAtIndex(i).stringValue == tag)
                    return;
            }

            tags.InsertArrayElementAtIndex(tags.arraySize);
            tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
        }

        public static void SetupPlayerSettings()
        {
            PlayerSettings.companyName = "FPSGameStudio";
            PlayerSettings.productName = "HAREKÂT";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
#if UNITY_2021_2_OR_NEWER
            PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Standalone, ApiCompatibilityLevel.NET_Standard);
#endif
            // Active Input Handling = Input System Package (1)
            try
            {
                var ps = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
                var input = ps.FindProperty("activeInputHandler");
                if (input != null)
                {
                    input.intValue = 1; // Input System Package
                    ps.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[HAREKÂT] activeInputHandler ayarlanamadı: " + e.Message);
            }

            AssetGeneration.EnsureAppIcon();
            Debug.Log("[HAREKÂT] Player Settings güncellendi.");
        }
    }
}
