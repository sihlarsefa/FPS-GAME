using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Infrastructure.Rendering
{
    /// <summary>
    /// Unity 6 STP (Spatial-Temporal Post-Processing) macOS/Metal'te RenderGraph NRE → siyah ekran.
    /// İlk kareden önce yükselticiyi Bilinear'a zorlar (URP asset'te STP kalsa bile).
    /// </summary>
    public static class StpCrashGuard
    {
        private const string StpName = "Spatial-Temporal Post-Processing";
        private const string SafeName = "Bilinear";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void BeforeSceneLoad()
        {
            try { DisableStpOnActivePipeline(); }
            catch (Exception e) { Debug.LogWarning("[HAREKÂT] STP kapatılamadı: " + e.Message); }
        }

        /// <summary>Kalite değişiminde de çağrılır.</summary>
        public static void DisableStpOnActivePipeline()
        {
            var asset = GraphicsSettings.currentRenderPipeline;
            if (asset == null)
                asset = GraphicsSettings.defaultRenderPipeline;
            if (asset == null)
                return;

            // URP 17.3+: upscalerName
            TrySetStringProp(asset, "upscalerName", SafeName);
            TrySetField(asset, "m_SelectedUpscalerName", SafeName);

            // Eski enum: Linear = 1 (STP = 4)
            TrySetEnumProp(asset, "upscalingFilter", 1);
            TrySetField(asset, "m_UpscalingFilter", 1);

            // İsim hâlâ STP ise ikinci geçiş
            var nameProp = asset.GetType().GetProperty("upscalerName", BindingFlags.Public | BindingFlags.Instance);
            if (nameProp != null && nameProp.CanRead)
            {
                var n = nameProp.GetValue(asset, null) as string;
                if (string.Equals(n, StpName, StringComparison.OrdinalIgnoreCase))
                    nameProp.SetValue(asset, SafeName, null);
            }
        }

        private static void TrySetStringProp(object target, string prop, string value)
        {
            try
            {
                var p = target.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance);
                if (p != null && p.CanWrite && p.PropertyType == typeof(string))
                    p.SetValue(target, value, null);
            }
            catch (Exception) { /* yoksay */ }
        }

        private static void TrySetEnumProp(object target, string prop, int value)
        {
            try
            {
                var p = target.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance);
                if (p == null || !p.CanWrite || !p.PropertyType.IsEnum) return;
                p.SetValue(target, Enum.ToObject(p.PropertyType, value), null);
            }
            catch (Exception) { /* yoksay */ }
        }

        private static void TrySetField(object target, string field, object value)
        {
            try
            {
                var f = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
                if (f == null) return;
                if (f.FieldType == typeof(string) && value is string s)
                    f.SetValue(target, s);
                else if (f.FieldType.IsEnum && value is int i)
                    f.SetValue(target, Enum.ToObject(f.FieldType, i));
            }
            catch (Exception) { /* yoksay */ }
        }
    }
}
