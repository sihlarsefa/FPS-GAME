using System;
using UnityEditor;
using UnityEngine;

namespace Project.EditorTools
{
    /// <summary>Batchmode giriş noktaları (-executeMethod).</summary>
    public static class BatchEntry
    {
        public static void SetupAll()
        {
            Debug.Log("[HAREKÂT] BatchEntry.SetupAll başlıyor…");
            ProjectSetup.RunAllSteps();
            Debug.Log("[HAREKÂT] BatchEntry.SetupAll bitti.");
        }

        public static void BuildWindowsClient() => BuildTool.BuildWindowsClient();
        public static void BuildWindowsServer() => BuildTool.BuildWindowsServer();
        public static void BuildMac() => BuildTool.BuildMac();
    }
}
