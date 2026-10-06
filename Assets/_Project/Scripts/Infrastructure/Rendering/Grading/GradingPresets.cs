using Project.Core.Domain;
using UnityEngine;

namespace Project.Infrastructure.Rendering.Grading
{
    /// <summary>Renk derecelendirme evresi (gün saati dilimi).</summary>
    public enum GradeStage { Safak = 0, Gunduz = 1, AltinSaat = 2, MaviSaat = 3, Gece = 4, Lobi = 5 }

    /// <summary>Saf veri: tek bir derecelendirme ayarı. Renkler RGB (lift/gamma/gain için 1 = nötr, w = ağırlık/ofset).</summary>
    public struct GradeSpec
    {
        public float Temperature, Tint;              // beyaz dengesi -100..100
        public float PostExposure, Contrast, Saturation; // EV, -100..100, -100..100
        public Vector4 Lift, Gamma, Gain;            // rgb + w (URP LiftGammaGain: nötr = (1,1,1,0))
        public Color SplitShadows, SplitHighlights;  // split-toning renkleri
        public float SplitBalance;                   // -100..100
        public Vector4 Shadows, Midtones, Highlights; // SMH (nötr = (1,1,1,0))

        public static readonly GradeSpec Neutral = new GradeSpec
        {
            Lift = new Vector4(1, 1, 1, 0), Gamma = new Vector4(1, 1, 1, 0), Gain = new Vector4(1, 1, 1, 0),
            SplitShadows = new Color(0.5f, 0.5f, 0.5f), SplitHighlights = new Color(0.5f, 0.5f, 0.5f),
            Shadows = new Vector4(1, 1, 1, 0), Midtones = new Vector4(1, 1, 1, 0), Highlights = new Vector4(1, 1, 1, 0)
        };
    }

    /// <summary>Harita + gün saati başına derecelendirme ön ayarları, enterpolasyon ve sınırlar (Unity'siz test edilebilir).</summary>
    public static class GradingPresets
    {
        public const float TempMax = 100f, ContrastMax = 100f, SatMax = 100f, ExposureMax = 3f;

        private static Vector4 V(float r, float g, float b, float w = 0f) => new Vector4(r, g, b, w);

        public static GradeStage StageFor(TimeOfDay t)
        {
            switch (t)
            {
                case TimeOfDay.Safak: return GradeStage.Safak;
                case TimeOfDay.Aksam: return GradeStage.AltinSaat;
                case TimeOfDay.Gece: return GradeStage.Gece;
                default: return GradeStage.Gunduz;
            }
        }

        /// <summary>Evre için taban ayar (harita etkisiz).</summary>
        public static GradeSpec Base(GradeStage stage)
        {
            var s = GradeSpec.Neutral;
            switch (stage)
            {
                case GradeStage.Safak:
                    s.Temperature = 12; s.Tint = 8; s.PostExposure = 0f; s.Contrast = 8; s.Saturation = -6;
                    s.Lift = V(1.02f, 1f, 1.05f); s.Gamma = V(1.04f, 1f, 0.98f); s.Gain = V(1.05f, 0.98f, 0.94f);
                    s.SplitShadows = new Color(0.45f, 0.5f, 0.7f); s.SplitHighlights = new Color(0.85f, 0.6f, 0.5f); s.SplitBalance = 10;
                    break;
                case GradeStage.Gunduz:
                    s.Temperature = 4; s.Tint = 0; s.PostExposure = -0.05f; s.Contrast = 14; s.Saturation = -10;
                    s.Lift = V(1f, 1f, 1.02f); s.Gamma = V(1f, 1f, 1f); s.Gain = V(1.02f, 1.01f, 0.98f);
                    s.SplitShadows = new Color(0.46f, 0.5f, 0.58f); s.SplitHighlights = new Color(0.58f, 0.54f, 0.48f); s.SplitBalance = 0;
                    break;
                case GradeStage.AltinSaat:
                    s.Temperature = 22; s.Tint = 6; s.PostExposure = 0.05f; s.Contrast = 18; s.Saturation = 4;
                    s.Lift = V(1.03f, 1f, 1.04f); s.Gamma = V(1.03f, 1f, 0.96f); s.Gain = V(1.08f, 1f, 0.88f);
                    s.SplitShadows = new Color(0.4f, 0.45f, 0.65f); s.SplitHighlights = new Color(0.95f, 0.62f, 0.35f); s.SplitBalance = 15;
                    break;
                case GradeStage.MaviSaat:
                    s.Temperature = -22; s.Tint = -4; s.PostExposure = 0f; s.Contrast = 16; s.Saturation = -4;
                    s.Lift = V(0.97f, 1f, 1.08f); s.Gamma = V(0.97f, 1f, 1.05f); s.Gain = V(0.96f, 1f, 1.06f);
                    s.SplitShadows = new Color(0.35f, 0.45f, 0.75f); s.SplitHighlights = new Color(0.6f, 0.62f, 0.8f); s.SplitBalance = -10;
                    break;
                case GradeStage.Gece:
                    s.Temperature = -30; s.Tint = -6; s.PostExposure = 0.2f; s.Contrast = 20; s.Saturation = -18;
                    s.Lift = V(0.94f, 0.98f, 1.1f); s.Gamma = V(0.94f, 0.99f, 1.08f); s.Gain = V(0.92f, 0.98f, 1.1f);
                    s.SplitShadows = new Color(0.3f, 0.4f, 0.7f); s.SplitHighlights = new Color(0.5f, 0.6f, 0.75f); s.SplitBalance = -20;
                    break;
                case GradeStage.Lobi: // mavi saat + kırmızı vurgu
                    s.Temperature = -18; s.Tint = 6; s.PostExposure = 0.1f; s.Contrast = 24; s.Saturation = 2;
                    s.Lift = V(0.98f, 0.99f, 1.08f); s.Gamma = V(1f, 0.98f, 1.04f); s.Gain = V(1.1f, 0.96f, 1.02f);
                    s.SplitShadows = new Color(0.3f, 0.42f, 0.78f); s.SplitHighlights = new Color(0.9f, 0.3f, 0.25f); s.SplitBalance = 5;
                    s.Highlights = V(1.08f, 0.96f, 0.96f);
                    break;
            }
            return s;
        }

        /// <summary>Harita ince ayarı (sıcaklık, doygunluk ofseti).</summary>
        public static void MapOffset(string mapId, out float temp, out float sat)
        {
            switch (mapId)
            {
                case MapCatalog.AyazGecidi: temp = -8f; sat = -8f; break;   // soğuk, soluk
                case MapCatalog.MaviLiman: temp = -2f; sat = 6f; break;
                case MapCatalog.KartalYaylasi: temp = 4f; sat = 2f; break;
                default: temp = 0f; sat = 0f; break;                        // Kuzgun / bilinmeyen
            }
        }

        public static GradeSpec For(string mapId, TimeOfDay time) => For(mapId, StageFor(time));

        public static GradeSpec For(string mapId, GradeStage stage)
        {
            var s = Base(stage);
            if (stage != GradeStage.Lobi)
            {
                MapOffset(mapId, out var t, out var sat);
                s.Temperature += t; s.Saturation += sat;
            }
            return Clamp(s);
        }

        public static GradeSpec Lerp(GradeSpec a, GradeSpec b, float t)
        {
            t = Mathf.Clamp01(t);
            return new GradeSpec
            {
                Temperature = Mathf.Lerp(a.Temperature, b.Temperature, t), Tint = Mathf.Lerp(a.Tint, b.Tint, t),
                PostExposure = Mathf.Lerp(a.PostExposure, b.PostExposure, t), Contrast = Mathf.Lerp(a.Contrast, b.Contrast, t),
                Saturation = Mathf.Lerp(a.Saturation, b.Saturation, t),
                Lift = Vector4.Lerp(a.Lift, b.Lift, t), Gamma = Vector4.Lerp(a.Gamma, b.Gamma, t), Gain = Vector4.Lerp(a.Gain, b.Gain, t),
                SplitShadows = Color.Lerp(a.SplitShadows, b.SplitShadows, t), SplitHighlights = Color.Lerp(a.SplitHighlights, b.SplitHighlights, t),
                SplitBalance = Mathf.Lerp(a.SplitBalance, b.SplitBalance, t),
                Shadows = Vector4.Lerp(a.Shadows, b.Shadows, t), Midtones = Vector4.Lerp(a.Midtones, b.Midtones, t),
                Highlights = Vector4.Lerp(a.Highlights, b.Highlights, t)
            };
        }

        /// <summary>Tüm alanları URP'nin kabul ettiği aralığa sıkıştırır.</summary>
        public static GradeSpec Clamp(GradeSpec s)
        {
            s.Temperature = Mathf.Clamp(s.Temperature, -TempMax, TempMax);
            s.Tint = Mathf.Clamp(s.Tint, -TempMax, TempMax);
            s.PostExposure = Mathf.Clamp(s.PostExposure, -ExposureMax, ExposureMax);
            s.Contrast = Mathf.Clamp(s.Contrast, -ContrastMax, ContrastMax);
            s.Saturation = Mathf.Clamp(s.Saturation, -SatMax, SatMax);
            s.SplitBalance = Mathf.Clamp(s.SplitBalance, -100f, 100f);
            s.Lift = ClampVec(s.Lift); s.Gamma = ClampVec(s.Gamma); s.Gain = ClampVec(s.Gain);
            s.Shadows = ClampVec(s.Shadows); s.Midtones = ClampVec(s.Midtones); s.Highlights = ClampVec(s.Highlights);
            s.SplitShadows = new Color(Mathf.Clamp01(s.SplitShadows.r), Mathf.Clamp01(s.SplitShadows.g), Mathf.Clamp01(s.SplitShadows.b), 1f);
            s.SplitHighlights = new Color(Mathf.Clamp01(s.SplitHighlights.r), Mathf.Clamp01(s.SplitHighlights.g), Mathf.Clamp01(s.SplitHighlights.b), 1f);
            return s;
        }

        private static Vector4 ClampVec(Vector4 v) => new Vector4(
            Mathf.Clamp(v.x, 0f, 2f), Mathf.Clamp(v.y, 0f, 2f), Mathf.Clamp(v.z, 0f, 2f), Mathf.Clamp(v.w, -1f, 1f));
    }
}
