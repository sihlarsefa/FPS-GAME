using Project.Infrastructure.Config;
using UnityEditor;
using UnityEngine;

namespace Project.EditorTools
{
    /// <summary>Eski menü girişleri — yeni HAREKÂT menüsüne yönlendirir (derlenebilir kalsın).</summary>
    public static class SceneSetupMenu
    {
        [MenuItem("Project/Setup/Create Test Arena Scene")]
        public static void CreateTestArenaScene()
        {
            Debug.LogWarning("[HAREKÂT] Eski Test Arena menüsü. Yeni: HAREKÂT/Kurulum/1) Her Şeyi Kur");
            ProjectSetup.RunAllSteps();
        }

        [MenuItem("Project/Setup/Create Player Movement Config")]
        public static void EnsureMovementConfig()
        {
            AssetGeneration.EnsureMovementConfig();
            // Eski yol da doldurulsun
            const string ConfigPath = "Assets/_Project/Settings/PlayerMovementConfig.asset";
            if (AssetDatabase.LoadAssetAtPath<PlayerMovementConfig>(ConfigPath) == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/_Project/Settings"))
                    AssetDatabase.CreateFolder("Assets/_Project", "Settings");
                var config = ScriptableObject.CreateInstance<PlayerMovementConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }
        }

        [MenuItem("Project/Setup/Add Scene To Build Settings")]
        public static void AddSceneToBuild()
        {
            SceneBuilder.EnsureBuildSettings();
        }
    }
}
