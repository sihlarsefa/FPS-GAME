using System;
using System.IO;
using UnityEngine;

namespace Project.Infrastructure.Diagnostics
{
    /// <summary>Proje kökündeki <c>Logs/</c> klasörü (Editör ve player).</summary>
    public static class DiagnosticsPaths
    {
        public static string ProjectRoot
        {
            get
            {
#if UNITY_EDITOR
                return Directory.GetParent(UnityEngine.Application.dataPath)?.FullName ?? UnityEngine.Application.dataPath;
#else
                // Player: exe yanında veya persistentDataPath üstü
                var data = UnityEngine.Application.dataPath;
                var parent = Directory.GetParent(data);
                return parent?.FullName ?? UnityEngine.Application.persistentDataPath;
#endif
            }
        }

        public static string LogsDirectory => Path.Combine(ProjectRoot, "Logs");
    }
}
