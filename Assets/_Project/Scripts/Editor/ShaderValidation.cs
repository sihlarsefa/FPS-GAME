using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Project.EditorTools
{
    /// <summary>Compiles explicit Metal/D3D variants on the current editor device. Not a visual/performance test.</summary>
    public static class ShaderValidation
    {
        [Serializable] private sealed class Entry
        {
            public string path;
            public string shader;
            public int passes;
            public bool supported;
            public List<string> diagnostics = new List<string>();
        }
        [Serializable] private sealed class Report
        {
            public string unity;
            public string device;
            public string utc;
            public int errors;
            public string scope = "Default + instancing + forward-plus/shadows variants of each pass. No visual, Frame Debugger or GPU timing assertion.";
            public List<Entry> shaders = new List<Entry>();
        }

        public static void Run()
        {
            var report = new Report { unity = UnityEngine.Application.unityVersion, device = SystemInfo.graphicsDeviceType + " / " + SystemInfo.graphicsDeviceName, utc = DateTime.UtcNow.ToString("O") };
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Shader validation requires a graphics device; do not use -nographics.");
            var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            var pipeline = UniversalRenderPipelineAsset.Create(renderer);
            var previous = GraphicsSettings.defaultRenderPipeline;
            var previousQuality = QualitySettings.renderPipeline;
            var previousAsync = ShaderUtil.allowAsyncCompilation;
            try
            {
                GraphicsSettings.defaultRenderPipeline = pipeline;
                QualitySettings.renderPipeline = pipeline;
                ShaderUtil.allowAsyncCompilation = false;
                foreach (var guid in AssetDatabase.FindAssets("t:Shader", new[] { "Assets/_Project/Shaders" }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                    var entry = new Entry { path = path, shader = shader != null ? shader.name : "<null>" };
                    report.shaders.Add(entry);
                    if (shader == null) { report.errors++; entry.diagnostics.Add("Shader asset failed to load."); continue; }
                    var material = new Material(shader);
                    try
                    {
                        entry.passes = material.passCount;
                        var variants = new[] {
                            Array.Empty<string>(),
                            new[] { "INSTANCING_ON" },
                            new[] { "_CLUSTER_LIGHT_LOOP", "_MAIN_LIGHT_SHADOWS_CASCADE", "_SHADOWS_SOFT", "_ADDITIONAL_LIGHTS", "INSTANCING_ON" },
                            new[] { "_TERRAIN_NORMAL_MAP", "_MASKMAP", "_TERRAIN_BLEND_HEIGHT", "_CLUSTER_LIGHT_LOOP", "_MAIN_LIGHT_SHADOWS_CASCADE" }
                        };
                        foreach (var keywords in variants)
                        {
                            material.shaderKeywords = keywords;
                            material.enableInstancing = Array.IndexOf(keywords, "INSTANCING_ON") >= 0;
                            for (var pass = 0; pass < material.passCount; pass++)
                                ShaderUtil.CompilePass(material, pass, true);
                        }
                        entry.supported = shader.isSupported;
                        foreach (var message in ShaderUtil.GetShaderMessages(shader))
                        {
                            entry.diagnostics.Add(message.severity + " " + message.file + ":" + message.line + " " + message.message);
                            if (message.severity.ToString() == "Error") report.errors++;
                        }
                        if (!entry.supported) { report.errors++; entry.diagnostics.Add("Unsupported on current graphics device."); }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(material); }
                    Debug.Log("[ShaderValidation] " + entry.shader + " passes=" + entry.passes + " supported=" + entry.supported + " diagnostics=" + entry.diagnostics.Count);
                }
                if (report.shaders.Count == 0) { report.errors++; }
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = previousAsync;
                QualitySettings.renderPipeline = previousQuality;
                GraphicsSettings.defaultRenderPipeline = previous;
                UnityEngine.Object.DestroyImmediate(pipeline);
                UnityEngine.Object.DestroyImmediate(renderer);
                Directory.CreateDirectory("Logs/x2");
                File.WriteAllText("Logs/x2/shader-validation.json", JsonUtility.ToJson(report, true));
            }
            if (report.errors > 0) throw new InvalidOperationException("Shader validation failed: " + report.errors + "; see Logs/x2/shader-validation.json");
            Debug.Log("[ShaderValidation] PASS: " + report.shaders.Count + " shader assets.");
        }
    }
}
