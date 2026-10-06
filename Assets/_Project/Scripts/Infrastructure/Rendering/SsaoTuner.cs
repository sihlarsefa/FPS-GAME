using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Infrastructure.Rendering
{
    /// <summary>
    /// Kalite kademesine göre URP SSAO renderer özelliğini ayarlar. Özellik yoksa ya da bu URP sürümünde alan adları
    /// farklıysa yansıma (reflection) ile sessizce atlanır; tek uyarı günlüğü yazılır, asla hata fırlatmaz.
    /// Cursor doğrulaması: gerçek URP 17.6 varlığında alan adları (Intensity/Radius/Falloff/DownSample/Samples).
    /// </summary>
    public static class SsaoTuner
    {
        private static bool _warned;

        public static void Apply(int tier)
        {
            try
            {
                var asset = GraphicsSettings.currentRenderPipeline;
                if (asset == null)
                    return;

                var list = asset.GetType().GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(asset) as IEnumerable;
                if (list == null)
                {
                    Warn("renderer listesi bulunamadı");
                    return;
                }

                var t = ScreenEffectsMath.SsaoForTier(tier);
                var found = false;
                foreach (var data in list)
                {
                    if (data == null)
                        continue;
                    var features = data.GetType().GetProperty("rendererFeatures")?.GetValue(data) as IEnumerable;
                    if (features == null)
                        continue;
                    foreach (var feature in features)
                    {
                        if (feature == null || feature.GetType().Name != "ScreenSpaceAmbientOcclusion")
                            continue;
                        found = true;
                        Tune(feature, t);
                    }
                }

                if (!found)
                    Warn("ScreenSpaceAmbientOcclusion özelliği renderer'da yok");
            }
            catch (Exception e)
            {
                Warn(e.Message);
            }
        }

        private static void Tune(object feature, ScreenEffectsMath.SsaoTier t)
        {
            var type = feature.GetType();
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var settings = (type.GetField("m_Settings", flags)?.GetValue(feature))
                           ?? type.GetProperty("settings", flags)?.GetValue(feature);
            if (settings != null)
            {
                Set(settings, "Intensity", t.Intensity);
                Set(settings, "Radius", t.Radius);
                Set(settings, "Falloff", t.Falloff);
                Set(settings, "DownSample", t.Downsample);
                Set(settings, "AfterOpaque", false); // ambient-only: doğrudan ışığı karartmaz
                var samples = settings.GetType().GetField("Samples", flags);
                if (samples != null && samples.FieldType.IsEnum)
                {
                    var names = Enum.GetNames(samples.FieldType);
                    var idx = Math.Min(t.Samples, names.Length - 1);
                    if (idx >= 0)
                        samples.SetValue(settings, Enum.Parse(samples.FieldType, names[idx]));
                }
            }

            type.GetMethod("SetActive", flags)?.Invoke(feature, new object[] { t.Enabled });
            type.GetMethod("Create", flags)?.Invoke(feature, null);
        }

        private static void Set(object target, string name, object value)
        {
            var f = target.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null && f.FieldType == value.GetType())
                f.SetValue(target, value);
        }

        private static void Warn(string why)
        {
            if (_warned)
                return;
            _warned = true;
            Debug.LogWarning("[SsaoTuner] SSAO ayarlanamadı: " + why);
        }
    }
}
