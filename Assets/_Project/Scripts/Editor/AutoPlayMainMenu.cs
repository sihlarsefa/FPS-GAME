using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.EditorTools
{
    /// <summary>
    /// HAREKAT_AUTOPLAY=1 ile Editor açılınca MainMenu Play Mode.
    /// Batch: -executeMethod Project.EditorTools.AutoPlayMainMenu.Enter
    /// </summary>
    public static class AutoPlayMainMenu
    {
        private const string ScenePath = "Assets/_Project/Scenes/MainMenu.unity";

        [InitializeOnLoadMethod]
        private static void OnLoad()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HAREKAT_AUTOPLAY")))
                return;
            EditorApplication.delayCall += Enter;
        }

        public static void Enter()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            try
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                if (!scene.IsValid())
                {
                    Debug.LogError("[HAREKÂT] MainMenu sahnesi açılamadı: " + ScenePath);
                    return;
                }
                EditorApplication.isPlaying = true;
                Debug.Log("[HAREKÂT] AutoPlay MainMenu");
            }
            catch (Exception e)
            {
                Debug.LogError("[HAREKÂT] AutoPlay hata: " + e.Message);
            }
        }

        [MenuItem("HAREKÂT/Oyna/Ana Menü (Play)", priority = 1)]
        public static void MenuEnter() => Enter();
    }
}
