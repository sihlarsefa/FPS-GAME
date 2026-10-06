using System;

namespace Project.Infrastructure.Audio
{
    /// <summary>Konuşma stresi (v2 ses setinde klasör adı ile eşleşir).</summary>
    public enum VoiceStress { Sakin = 0, Catisma = 1, Panik = 2 }

    /// <summary>
    /// Saf mantık: v2 Türkçe replik klibinin Resources yolunu ve stres seviyesini seçer.
    /// Klasör düzeni: Audio/Voice/v2/&lt;ses&gt;/&lt;stres&gt;/&lt;id&gt;. Klip yoksa çağıran eski Voice/&lt;id&gt; yoluna (VoiceV2Resolver.LegacyPath) düşer.
    /// RadioVoicePlayer / DialogueClipLibrary klibi önce ResourcePath(...) ile, bulunamazsa LegacyPath(...) ile yükler.
    /// </summary>
    public static class VoiceV2Resolver
    {
        public const string Folder = "Audio/Voice/v2/";
        public static readonly string[] Voices = { "dfki_er", "dfki_kalin", "dfki_genc", "yelda" };

        public static string StressFolder(VoiceStress s)
        {
            switch (s)
            {
                case VoiceStress.Catisma: return "catisma";
                case VoiceStress.Panik: return "panik";
                default: return "sakin";
            }
        }

        /// <summary>Konuşmacı seed'i -> kalıcı ses (aynı asker hep aynı sesle konuşur).</summary>
        public static string VoiceForSpeaker(int speakerSeed)
        {
            int i = Math.Abs(speakerSeed % Voices.Length);
            return Voices[i];
        }

        /// <summary>
        /// Stres: can oranı (0-1), bastırılma (0-1), yakın temas ve düşen takım arkadaşı. Panik &gt; çatışma &gt; sakin.
        /// </summary>
        public static VoiceStress StressFor(float healthFraction, float suppression, bool inCombat, bool allyDownRecently)
        {
            float score = 0f;
            if (inCombat) score += 0.45f;
            score += Clamp01(suppression) * 0.5f;
            score += (1f - Clamp01(healthFraction)) * 0.5f;
            if (allyDownRecently) score += 0.2f;
            if (score >= 0.95f) return VoiceStress.Panik;
            if (score >= 0.4f) return VoiceStress.Catisma;
            return VoiceStress.Sakin;
        }

        public static string ResourcePath(string voice, VoiceStress stress, string id)
        {
            return Folder + voice + "/" + StressFolder(stress) + "/" + id;
        }

        public static string LegacyPath(string id) { return "Audio/Voice/" + id; }

        private static float Clamp01(float v) { return v < 0f ? 0f : (v > 1f ? 1f : v); }
    }
}
