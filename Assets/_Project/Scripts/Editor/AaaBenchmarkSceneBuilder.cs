using System.IO;
using Project.Presentation.Benchmark;
using Project.Presentation.Bootstrap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Project.EditorTools
{
    /// <summary>
    /// "AAA_Benchmark" sahnesini üretir: yalnız kompozisyon kökü (<see cref="AaaBenchmarkBootstrap"/>) + güneş.
    /// Arazi/ev/araç/asker çalışma zamanında kurulur (üretim kodu tek yerde; sahne dosyası küçük kalır).
    /// Menü: HAREKÂT/Kurulum/AAA Benchmark Sahnesi. ProjectSetup.RunAllSteps için: AaaBenchmarkSceneBuilder.EnsureScene().
    /// </summary>
    public static class AaaBenchmarkSceneBuilder
    {
        [MenuItem("HAREKÂT/Kurulum/AAA Benchmark Sahnesi", priority = 20)]
        public static void MenuBuild()
        {
            EnsureScene();
            SceneBuilder.EnsureBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        /// <summary>Sahne yoksa üretir; varsa üzerine yazar (kompozisyon kökü sabit olduğundan güvenli).</summary>
        public static void EnsureScene()
        {
            if (!AssetDatabase.IsValidFolder(SceneNames.ScenesFolder))
            {
                var parts = SceneNames.ScenesFolder.Split('/');
                var current = parts[0];
                for (var i = 1; i < parts.Length; i++)
                {
                    var next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(current, parts[i]);
                    current = next;
                }
            }

            var path = SceneNames.PathOf(SceneNames.AaaBenchmark);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var boot = new GameObject("AaaBenchmarkBootstrap");
            boot.AddComponent<AaaBenchmarkBootstrap>();

            var light = Object.FindFirstObjectByType<Light>();
            if (light == null)
            {
                var go = new GameObject("Directional Light");
                light = go.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                go.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            }

            light.shadows = LightShadows.Soft;
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log("[HAREKÂT] AAA Benchmark sahnesi oluşturuldu: " + path + (File.Exists(path) ? string.Empty : " (dosya doğrulanamadı)"));
        }
    }
}
