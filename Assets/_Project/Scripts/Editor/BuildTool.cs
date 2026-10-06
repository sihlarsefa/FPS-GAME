using System;
using System.IO;
using System.Linq;
using Project.Presentation.Bootstrap;
using UnityEditor;
using UnityEngine;

namespace Project.EditorTools
{
    /// <summary>Windows / macOS istemci ve Windows Dedicated Server build'leri.</summary>
    public static class BuildTool
    {
        [MenuItem("HAREKÂT/Build/Windows İstemci (x64)", priority = 10)]
        public static void BuildWindowsClient()
        {
            EnsureScenesOrDie();
            if (!HasModule(BuildTarget.StandaloneWindows64))
            {
                FailModule("Windows Build Support (Mono)");
                return;
            }

            var dir = "Builds/Windows";
            Directory.CreateDirectory(dir);
            var opts = new BuildPlayerOptions
            {
                scenes = ScenePaths(),
                locationPathName = dir + "/HAREKAT.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
                subtarget = (int)StandaloneBuildSubtarget.Player
            };
            var report = BuildPipeline.BuildPlayer(opts);
            LogResult("Windows İstemci", report);
        }

        [MenuItem("HAREKÂT/Build/Windows Dedicated Server", priority = 11)]
        public static void BuildWindowsServer()
        {
            EnsureScenesOrDie();
            if (!HasModule(BuildTarget.StandaloneWindows64))
            {
                FailModule("Windows Dedicated Server Build Support");
                return;
            }

            var dir = "Builds/WindowsServer";
            Directory.CreateDirectory(dir);
            var opts = new BuildPlayerOptions
            {
                scenes = ScenePaths(),
                locationPathName = dir + "/HAREKAT_Server.exe",
                target = BuildTarget.StandaloneWindows64,
                subtarget = (int)StandaloneBuildSubtarget.Server,
                options = BuildOptions.EnableHeadlessMode
            };
            var report = BuildPipeline.BuildPlayer(opts);
            LogResult("Windows Dedicated Server", report);
        }

        [MenuItem("HAREKÂT/Build/macOS", priority = 12)]
        public static void BuildMac()
        {
            EnsureScenesOrDie();
            if (!HasModule(BuildTarget.StandaloneOSX))
            {
                FailModule("Mac Build Support");
                return;
            }

            var dir = "Builds/macOS";
            Directory.CreateDirectory(dir);
            var opts = new BuildPlayerOptions
            {
                scenes = ScenePaths(),
                locationPathName = dir + "/HAREKAT.app",
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(opts);
            LogResult("macOS", report);
        }

        private static string[] ScenePaths()
        {
            return SceneNames.BuildOrder.Select(SceneNames.PathOf).Where(File.Exists).ToArray();
        }

        private static void EnsureScenesOrDie()
        {
            try { SceneBuilder.EnsureMissingScenes(); }
            catch (Exception e) { Debug.LogWarning("[HAREKÂT] Eksik sahneler üretilemedi: " + e.Message); }
            SceneBuilder.EnsureBuildSettings();
            var scenes = ScenePaths();
            if (scenes.Length == 0)
                throw new InvalidOperationException("Build edilecek sahne yok. Önce HAREKÂT/Kurulum çalıştırın.");
        }

        private static bool HasModule(BuildTarget target)
        {
            return BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target);
        }

        private static void FailModule(string moduleName)
        {
            var msg =
                "[HAREKÂT] Gerekli Unity Hub modülü kurulu değil: " + moduleName + ".\n" +
                "Unity Hub → Installs → bu editör → Add Modules → " + moduleName + " kurun.\n" +
                "Bu Mac'te şu an yalnızca Mac Build Support olabilir; Windows build için Windows Build Support (Mono) " +
                "ve Dedicated Server için Windows Dedicated Server Build Support gerekir.";
            Debug.LogError(msg);
#if UNITY_EDITOR
            if (!UnityEngine.Application.isBatchMode)
                EditorUtility.DisplayDialog("Eksik Build Modülü", msg, "Tamam");
#endif
        }

        private static void LogResult(string label, UnityEditor.Build.Reporting.BuildReport report)
        {
            var summary = report.summary;
            if (summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
                Debug.Log("[HAREKÂT] ✓ " + label + " build OK → " + summary.outputPath + " (" + summary.totalSize + " bayt)");
            else
                Debug.LogError("[HAREKÂT] ✗ " + label + " build başarısız: " + summary.result);
        }
    }
}
