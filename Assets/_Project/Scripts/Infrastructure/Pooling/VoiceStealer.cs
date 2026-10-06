namespace Project.Infrastructure.Pooling
{
    /// <summary>Çalan bir ses sesinin değerlendirme verisi.</summary>
    public struct VoiceInfo
    {
        /// <summary>0 = en önemli (silah sesi, adım ilk), büyük = önemsiz (ortam).</summary>
        public int Priority;
        /// <summary>Dinleyicideki tahmini şiddet 0..1 (mesafe+ses).</summary>
        public float Loudness;
        /// <summary>Çalmaya başlayalı geçen süre (sn).</summary>
        public float Age;
        public bool Looping;
        public bool Active;
    }

    /// <summary>Ses havuzu doluyken hangi sesin kesileceğine karar veren saf mantık (AAA "voice stealing").</summary>
    public static class VoiceStealer
    {
        /// <summary>Kademeye göre eşzamanlı ses üst sınırı (0=Low..3=Ultra).</summary>
        public static int MaxVoices(int tier)
        {
            switch (tier < 0 ? 0 : tier > 3 ? 3 : tier)
            {
                case 0: return 24;
                case 1: return 32;
                case 2: return 48;
                default: return 64;
            }
        }

        /// <summary>Kesilmeye uygunluk puanı: yüksek = önce kesilir.</summary>
        public static float StealScore(in VoiceInfo v)
        {
            var score = v.Priority * 100f;                 // önemsiz sesler önce
            score += (1f - Clamp01(v.Loudness)) * 50f;     // duyulmayan önce
            score += v.Age < 8f ? v.Age * 2f : 16f;        // eski ses, yenisine göre daha kesilebilir
            if (v.Looping) score -= 30f;                   // döngüler kesilince fark edilir; hafif koru
            return score;
        }

        /// <summary>
        /// Yeni ses için kurban indeksini döner. -1 = yeni sesi çalma (hepsi ondan önemli).
        /// Boş (Active=false) slot varsa onun indeksi döner.
        /// </summary>
        public static int PickVictim(VoiceInfo[] voices, int count, int newPriority, float newLoudness)
        {
            if (voices == null || count <= 0) return -1;
            var best = -1;
            var bestScore = float.NegativeInfinity;
            for (var i = 0; i < count && i < voices.Length; i++)
            {
                if (!voices[i].Active) return i;
                var s = StealScore(voices[i]);
                if (s > bestScore) { bestScore = s; best = i; }
            }
            if (best < 0) return -1;
            var incoming = StealScore(new VoiceInfo { Priority = newPriority, Loudness = newLoudness, Age = 0f, Active = true });
            return bestScore > incoming ? best : -1;
        }

        private static float Clamp01(float x) => x < 0f ? 0f : x > 1f ? 1f : x;
    }
}
