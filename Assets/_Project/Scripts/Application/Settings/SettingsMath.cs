using System;

namespace Project.Application.Settings
{
    /// <summary>Ayar verisi için NaN/sonsuz değerlere dayanıklı kırpma yardımcıları.</summary>
    public static class SettingsMath
    {
        public static float Clamp(float v, float lo, float hi, float fallback)
        {
            if (float.IsNaN(v) || float.IsInfinity(v))
                v = fallback;
            return v < lo ? lo : v > hi ? hi : v;
        }

        public static int ClampInt(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;

        public static float DbToLinear(float db) => (float)Math.Pow(10.0, db / 20.0);

        public static float LinearToDb(float linear) => linear <= 0.000001f ? -120f : (float)(20.0 * Math.Log10(linear));
    }
}
