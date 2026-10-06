using System;
using Project.Core.Domain;

namespace Project.Application.AI
{
    /// <summary>
    /// Takım arkadaşı siper puanı (saf mantık): yakınlık + tehdide göre açı + siper yüksekliği.
    /// Yüksek puan = daha iyi siper. Fizik/Unity bağımlılığı yok; BotController aday noktaları buraya besler.
    /// </summary>
    public static class TeammateCoverScoring
    {
        public const float MaxSearchDistance = 25f;
        public const float StandingCoverHeight = 1.7f;
        public const float CrouchCoverHeight = 0.9f;

        public readonly struct Candidate
        {
            public readonly Float3 Position;
            public readonly float Height;   // Sipere ait engel yüksekliği (m)

            public Candidate(Float3 position, float height)
            {
                Position = position;
                Height = height;
            }
        }

        /// <summary>Mesafe puanı 0..1: yakın = yüksek, MaxSearchDistance'ta 0.</summary>
        public static float DistanceScore(float distance)
        {
            if (distance <= 1f) return 1f;
            if (distance >= MaxSearchDistance) return 0f;
            return 1f - (distance - 1f) / (MaxSearchDistance - 1f);
        }

        /// <summary>
        /// Açı puanı 0..1: tehdit, bot ile siper arasındaki hattın tam karşı tarafındaysa (siper tehditle bot arasında) 1.
        /// Yani bot-siper yönü ile bot-tehdit yönü aynıysa iyi; siper tehdidin arkasındaysa 0.
        /// </summary>
        public static float AngleScore(Float3 self, Float3 cover, Float3 threat)
        {
            var toCover = (cover - self).Flat.Normalized;
            var toThreat = (threat - self).Flat.Normalized;
            if (toCover.SqrMagnitude < 0.0001f || toThreat.SqrMagnitude < 0.0001f)
                return 0.5f;
            var dot = Float3.Dot(toCover, toThreat);   // 1 = siper tehdit yönünde (önümüzde)
            // Siper tehdit yönünde ise ve tehdide bottan yakınsa kalkan olur.
            var coverToThreat = Float3.DistanceXZ(cover, threat);
            var selfToThreat = Float3.DistanceXZ(self, threat);
            var inFront = coverToThreat < selfToThreat ? 1f : 0.3f;
            var align = (dot + 1f) * 0.5f;
            return Clamp01(align * inFront);
        }

        /// <summary>Yükseklik puanı 0..1: çömelince saklayan (≥0.9m) orta, ayakta saklayan (≥1.7m) tam; alçak engel az.</summary>
        public static float HeightScore(float height)
        {
            if (height <= 0.3f) return 0f;
            if (height >= StandingCoverHeight) return 1f;
            if (height >= CrouchCoverHeight)
                return 0.6f + 0.4f * (height - CrouchCoverHeight) / (StandingCoverHeight - CrouchCoverHeight);
            return 0.6f * (height - 0.3f) / (CrouchCoverHeight - 0.3f);
        }

        public static float Score(Float3 self, Float3 threat, Candidate c)
        {
            var dist = Float3.DistanceXZ(self, c.Position);
            if (dist > MaxSearchDistance) return 0f;
            // Tehdide dönük siper yoksa (yükseklik yok) puan sıfır: açıkta durmak siper sayılmaz.
            var h = HeightScore(c.Height);
            if (h <= 0f) return 0f;
            return 0.35f * DistanceScore(dist) + 0.4f * AngleScore(self, c.Position, threat) + 0.25f * h;
        }

        /// <summary>En iyi siper indeksi; geçerli aday yoksa -1. <paramref name="minScore"/> altı reddedilir.</summary>
        public static int PickBest(Float3 self, Float3 threat, Candidate[] candidates, int count, float minScore = 0.35f)
        {
            var best = -1;
            var bestScore = minScore;
            if (candidates == null) return -1;
            var n = Math.Min(count, candidates.Length);
            for (var i = 0; i < n; i++)
            {
                var s = Score(self, threat, candidates[i]);
                if (s > bestScore)
                {
                    bestScore = s;
                    best = i;
                }
            }

            return best;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
