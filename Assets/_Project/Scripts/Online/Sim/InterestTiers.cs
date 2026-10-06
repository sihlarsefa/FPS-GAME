using UnityEngine;

namespace Project.Online.Sim
{
    /// <summary>Gözlemci-hedef ilgi kademesi: ne sıklıkta replike edileceğini belirler.</summary>
    public enum InterestTier : byte { Culled = 0, Far = 1, Mid = 2, Near = 3 }

    /// <summary>
    /// Mesafe + görünürlük kademesi (saf): yakın/aynı tim tam hız, orta 20 Hz, uzak 5 Hz, menzil dışı kesik.
    /// Görünürlük (hat engelli mi) bir kademe düşürür; aynı tim her zaman Near'dır. Histerezis InterestRules ile ortak.
    /// </summary>
    public static class InterestTiers
    {
        public const float NearRadius = 80f;
        public const float MidRadius = 220f;
        public const float FarHz = 5f;
        public const float MidHz = 20f;

        public static InterestTier Classify(bool currentlyVisible, Vector3 observer, Vector3 target,
            int observerTeam, int targetTeam, bool lineOfSight)
        {
            if (observerTeam >= 0 && observerTeam == targetTeam)
                return InterestTier.Near;
            if (!InterestRules.ShouldReplicate(currentlyVisible, observer, target, observerTeam, targetTeam))
                return InterestTier.Culled;

            var d2 = (observer - target).sqrMagnitude;
            var tier = d2 <= NearRadius * NearRadius ? InterestTier.Near
                : d2 <= MidRadius * MidRadius ? InterestTier.Mid : InterestTier.Far;
            // Görüş hattı yoksa bir kademe düşür (duvar arkası / tepe arkası daha az önemli).
            if (!lineOfSight && tier > InterestTier.Far)
                tier--;
            return tier;
        }

        /// <summary>Kademenin hedef gönderim frekansı (Hz); Near = sunucu tick hızı.</summary>
        public static float TargetHz(InterestTier tier, float serverTickRate)
        {
            switch (tier)
            {
                case InterestTier.Near: return serverTickRate;
                case InterestTier.Mid: return Mathf.Min(MidHz, serverTickRate);
                case InterestTier.Far: return Mathf.Min(FarHz, serverTickRate);
                default: return 0f;
            }
        }

        /// <summary>Kaç tick'te bir gönderilir (Culled için 0 = hiç).</summary>
        public static int TickInterval(InterestTier tier, int serverTickRate)
        {
            var hz = TargetHz(tier, serverTickRate);
            return hz <= 0f ? 0 : Mathf.Max(1, Mathf.RoundToInt(serverTickRate / hz));
        }

        /// <summary>
        /// Bu tick'te gönderilsin mi? phase (örn. hedef id) gönderimleri tick'lere dağıtır, böylece uzak oyuncular aynı kareye yığılmaz.
        /// </summary>
        public static bool ShouldSend(InterestTier tier, uint tick, int phase, int serverTickRate)
        {
            var iv = TickInterval(tier, serverTickRate);
            if (iv == 0) return false;
            if (iv == 1) return true;
            return (int)((tick + (uint)(phase & 0x7FFFFFFF)) % (uint)iv) == 0;
        }
    }
}
