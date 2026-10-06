using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Project.Infrastructure.Rendering.Grading
{
    /// <summary>
    /// Derecelendirme alanlarına yazan TEK yer (yalnızca PostProcessing.ApplyGrade çağırır).
    /// Beyaz dengesi, LiftGammaGain ve SplitToning burada; renk filtresi/SMH haritadan PostProcessing'de.
    /// </summary>
    public static class GradingWriter
    {
        public static void Apply(VolumeProfile p, GradeSpec s, bool wbActive)
        {
            if (p == null) return;
            s = GradingPresets.Clamp(s);
            if (p.TryGet<ColorAdjustments>(out var ca))
            {
                ca.postExposure.Override(s.PostExposure); ca.contrast.Override(s.Contrast);
            }
            if (p.TryGet<WhiteBalance>(out var wb))
            {
                wb.active = wbActive; wb.temperature.Override(s.Temperature); wb.tint.Override(s.Tint);
            }
            // LiftGammaGain/SplitToning bazı URP sürümlerinde bulunmayabilir: yansıma ile güvenli ayarla.
            SetByName(p, "LiftGammaGain", ("lift", s.Lift), ("gamma", s.Gamma), ("gain", s.Gain));
            SetByName(p, "SplitToning", ("shadows", s.SplitShadows), ("highlights", s.SplitHighlights), ("balance", s.SplitBalance));
        }

        private static void SetByName(VolumeProfile p, string typeName, params (string field, object value)[] values)
        {
            var type = typeof(ColorAdjustments).Assembly.GetType("UnityEngine.Rendering.Universal." + typeName);
            if (type == null) return;
            VolumeComponent comp = null;
            foreach (var c in p.components) if (c != null && c.GetType() == type) { comp = c; break; }
            if (comp == null) comp = p.Add(type, false);
            comp.active = true;
            foreach (var (field, value) in values)
            {
                var f = type.GetField(field);
                if (f == null) continue;
                var param = f.GetValue(comp);
                param?.GetType().GetMethod("Override", new[] { value.GetType() })?.Invoke(param, new[] { value });
            }
        }
    }
}
