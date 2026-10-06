using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Project.Presentation.Benchmark
{
    /// <summary>
    /// G4 AAA URP özelliklerini (SSAO, temas gölgesi, hacimsel sis, ışık hüzmeleri, SSR, dekal, rüzgâr, çimen instancing,
    /// dinamik çözünürlük, profil aracı...) Ultra kademede açar. Tür adları bu dosya yazılırken henüz yoktu: bu yüzden
    /// çalışma zamanında yansıma ile aranır. Bir tür/yöntem bulunamazsa sessizce atlanır (nothing breaks).
    /// Aranan: ad parçası içeren, statik bir <c>Install</c> (veya <c>Enable</c>/<c>Apply</c>) yöntemi olan ve parametreleri
    /// Camera, Terrain, Transform, int (kademe), bool (true), float yardımcılarıyla doldurulabilen türler.
    /// </summary>
    public static class AaaBenchmarkFeatureInstaller
    {
        public const int UltraTier = 3;

        /// <summary>Ad parçaları: eşleşen herhangi bir tür adı adaydır (büyük/küçük harf duyarsız).</summary>
        public static readonly string[] FeatureNameFragments =
        {
            "ScreenSpaceReflection", "Ssr", "Ssao", "ContactShadow", "VolumetricFog", "VolumetricLight", "LightShaft",
            "ReflectionProbe", "PlanarReflection", "TerrainBlend", "HeightBlend", "GrassInstanc", "MeshGrass", "WindSystem",
            "WindShader", "DecalSystem", "ParallaxDetail", "DetailMapping", "VfxGraph", "ColorGrad", "PostProcessAaa",
            "DynamicResolution", "Upscal", "Hlod", "OcclusionSystem", "FrameBudget", "GpuProfiler", "RendererFeatureInstaller",
            "CustomFog", "GrassSystem", "TerrainShaderBinder", "RendererFeatures", "TextureStreaming", "WindSystem",
            "ReflectionProbePlacer", "GpuVfx", "DynamicRes", "ContactShadows", "HarekatRenderer"
        };

        private static readonly string[] MethodNames = { "Install", "Enable", "Apply" };

        /// <summary>Tür adı dışlama (ses, ağ, arayüz tarafı yanlış eşleşmesin).</summary>
        private static readonly string[] ExcludeNamespaceParts = { ".Audio", ".Online", ".UI", ".Benchmark", ".Tests", ".Editor" };

        public sealed class Report
        {
            public readonly List<string> Installed = new List<string>();
            public readonly List<string> Skipped = new List<string>();
            public int Candidates;
        }

        /// <summary>Adaylar bulunur ve kademe = Ultra ile çağrılır. Her çağrı try/catch içindedir.</summary>
        public static Report InstallAll(Camera camera, Terrain terrain, Transform root, int tier = UltraTier)
        {
            var report = new Report();
            Type[] types;
            try
            {
                types = FindCandidateTypes();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AAA Benchmark] Özellik taraması başarısız: " + e.Message);
                return report;
            }

            for (var i = 0; i < types.Length; i++)
            {
                var type = types[i];
                report.Candidates++;
                try
                {
                    if (TryInvoke(type, camera, terrain, root, tier))
                        report.Installed.Add(type.FullName);
                    else
                        report.Skipped.Add(type.FullName);
                }
                catch (Exception e)
                {
                    report.Skipped.Add(type.FullName + " (hata: " + (e.InnerException != null ? e.InnerException.Message : e.Message) + ")");
                }
            }

            return report;
        }

        /// <summary>Saf yardımcı: tür adı özellik adı parçalarından birini içeriyor mu (test edilebilir).</summary>
        public static bool NameMatches(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return false;
            for (var i = 0; i < FeatureNameFragments.Length; i++)
                if (typeName.IndexOf(FeatureNameFragments[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return false;
        }

        /// <summary>Saf yardımcı: ad alanı dışlanan bir parça içeriyor mu.</summary>
        public static bool NamespaceExcluded(string ns)
        {
            if (string.IsNullOrEmpty(ns))
                return false;
            for (var i = 0; i < ExcludeNamespaceParts.Length; i++)
                if (ns.IndexOf(ExcludeNamespaceParts[i], StringComparison.Ordinal) >= 0)
                    return true;
            return false;
        }

        private static Type[] FindCandidateTypes()
        {
            var result = new List<Type>(16);
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var a = 0; a < assemblies.Length; a++)
            {
                var asm = assemblies[a];
                var asmName = asm.GetName().Name;
                if (asmName == null || !asmName.StartsWith("Project.", StringComparison.Ordinal) || asmName.IndexOf("Tests", StringComparison.Ordinal) >= 0)
                    continue;

                Type[] all;
                try
                {
                    all = asm.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    all = Array.FindAll(e.Types, t => t != null);
                }

                for (var i = 0; i < all.Length; i++)
                {
                    var t = all[i];
                    if (t == null || !t.IsClass || t.IsNested || !t.IsAbstract || !t.IsSealed) // statik sınıf
                        continue;
                    if (NamespaceExcluded(t.Namespace) || !NameMatches(t.Name))
                        continue;
                    if (FindMethod(t) != null)
                        result.Add(t);
                }
            }

            return result.ToArray();
        }

        private static MethodInfo FindMethod(Type type)
        {
            // Aynı ada sahip aşırı yüklemeler arasında bağlanabilen en çok parametreliyi seç (Camera içeren sürüm önce).
            for (var n = 0; n < MethodNames.Length; n++)
            {
                MethodInfo best = null;
                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static);
                for (var m = 0; m < methods.Length; m++)
                {
                    if (methods[m].Name != MethodNames[n])
                        continue;
                    var ps = methods[m].GetParameters();
                    if (!CanBind(ps))
                        continue;
                    if (best == null || ps.Length > best.GetParameters().Length)
                        best = methods[m];
                }

                if (best != null)
                    return best;
            }

            return null;
        }

        private static bool CanBind(ParameterInfo[] parameters)
        {
            for (var i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i].ParameterType;
                if (p != typeof(Camera) && p != typeof(Terrain) && p != typeof(Transform) && p != typeof(GameObject)
                    && p != typeof(int) && p != typeof(bool) && p != typeof(float))
                    return false;
            }

            return true;
        }

        private static bool TryInvoke(Type type, Camera camera, Terrain terrain, Transform root, int tier)
        {
            var method = FindMethod(type);
            if (method == null)
                return false;

            var parameters = method.GetParameters();
            var args = new object[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i].ParameterType;
                if (parameters[i].HasDefaultValue && (p == typeof(bool) || p == typeof(float)))
                {
                    args[i] = parameters[i].DefaultValue;
                    continue;
                }

                if (p == typeof(Camera)) args[i] = camera;
                else if (p == typeof(Terrain)) args[i] = terrain;
                else if (p == typeof(Transform)) args[i] = root;
                else if (p == typeof(GameObject)) args[i] = root != null ? root.gameObject : null;
                else if (p == typeof(int)) args[i] = tier;
                else if (p == typeof(bool)) args[i] = true;
                else if (p == typeof(float)) args[i] = 1f;

                // Zorunlu Unity nesnesi eksikse çağırma (Camera/Terrain null → atla).
                if ((p == typeof(Camera) || p == typeof(Terrain)) && args[i] == null)
                    return false;
            }

            method.Invoke(null, args);
            return true;
        }
    }
}
