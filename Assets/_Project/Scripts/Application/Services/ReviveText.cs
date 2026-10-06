using System;

namespace Project.Application.Services
{
    /// <summary>Yaralı/kaldırma HUD metinleri (Türkçe, saf).</summary>
    public static class ReviveText
    {
        public static string DownedSelf(float bleedRemaining, float reviveProgress01)
        {
            if (reviveProgress01 > 0.001f)
                return "YARALISIN - Müttefik seni kaldırıyor %" + (int)Math.Round(reviveProgress01 * 100f);

            return "YARALISIN - Kanıyorsun: " + Math.Max(0, (int)Math.Ceiling(bleedRemaining)) + " sn";
        }

        public static string ReviveHint(string victimName, bool medic, string keyTag = "[F]") =>
            keyTag + " Basılı tut: " + (string.IsNullOrEmpty(victimName) ? "Yaralıyı" : victimName) + " kaldır" + (medic ? " (Sıhhiyeci hızlı)" : string.Empty);

        public static string Reviving(string victimName, float progress01) =>
            "Kaldırılıyor: " + (string.IsNullOrEmpty(victimName) ? "Yaralı" : victimName) + " %" + (int)Math.Round(Math.Min(1f, Math.Max(0f, progress01)) * 100f);
    }
}
