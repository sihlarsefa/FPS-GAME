using UnityEngine;

namespace Project.Infrastructure.AI
{
    /// <summary>
    /// Bot güncelleme LOD'u: yerel oyuncuya (kamera) uzaklığa göre 0 = tam hız, 1 = &gt;120 m, 2 = &gt;250 m.
    /// Çatışmadaki botlar (hedefi var, ateş ediyor, yakın zamanda hasar aldı) mesafeden bağımsız tam hızda kalır.
    /// </summary>
    public static class BotLod
    {
        public const float NearToMidMeters = 120f;
        public const float MidToFarMeters = 250f;
        /// <summary>Kademe değişiminde titremeyi önleyen pay (metre).</summary>
        public const float Hysteresis = 10f;

        public static readonly float[] TickInterval = { 0f, 0.05f, 0.15f };
        public static readonly float[] RateScale = { 1f, 2f, 4f };

        private static int _statFrame = -1;
        private static int _statCount0, _statCount1, _statCount2, _statCombat;
        private static int _cnt0, _cnt1, _cnt2, _cntCombat;
        private static int _cameraFrame = -1;
        private static Vector3 _cameraPosition;
        private static bool _hasCamera;

        /// <summary>Saf mantık: mesafe (m) ve mevcut kademeden yeni kademe (histerezisli).</summary>
        public static int ComputeTier(float distance, int currentTier)
        {
            var h = Hysteresis;
            // Simetrik pay: yukarı kademeye limit+h'de, aşağıya limit-h'de geçilir.
            var midLimit = currentTier >= 1 ? NearToMidMeters - h : NearToMidMeters + h;
            var farLimit = currentTier >= 2 ? MidToFarMeters - h : MidToFarMeters + h;
            if (distance > farLimit)
                return 2;
            return distance > midLimit ? 1 : 0;
        }

        /// <summary>Referans nokta (ana kamera). Yoksa false: tüm botlar tam hız.</summary>
        public static bool TryGetReference(out Vector3 position)
        {
            var frame = Time.frameCount;
            if (frame != _cameraFrame)
            {
                _cameraFrame = frame;
                var cam = Camera.main;
                _hasCamera = cam != null;
                if (_hasCamera)
                    _cameraPosition = cam.transform.position;
            }

            position = _cameraPosition;
            return _hasCamera;
        }

        internal static void Report(int tier, bool combat)
        {
            var frame = Time.frameCount;
            if (frame != _statFrame)
            {
                _statFrame = frame;
                _statCount0 = _cnt0; _statCount1 = _cnt1; _statCount2 = _cnt2; _statCombat = _cntCombat;
                _cnt0 = _cnt1 = _cnt2 = _cntCombat = 0;
            }

            if (tier == 0) _cnt0++; else if (tier == 1) _cnt1++; else _cnt2++;
            if (combat) _cntCombat++;
        }

        /// <summary>Geliştirici konsolu için tek satır istatistik (önceki kare).</summary>
        public static string StatsLine =>
            "AI LOD: tam=" + _statCount0 + " orta=" + _statCount1 + " uzak=" + _statCount2 + " çatışma=" + _statCombat;
    }
}
