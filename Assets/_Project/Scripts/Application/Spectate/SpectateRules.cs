using System.Collections.Generic;
using Project.Application.Services;

namespace Project.Application.Spectate
{
    /// <summary>
    /// Ölüm sonrası izleyici kuralları (saf mantık): geçiş listesi SADECE hayattaki takım arkadaşlarıdır.
    /// İstisna: killcam sonrası öldüreni serbest kamerayla takip (manuel geçişe kadar; öldürürse biter).
    /// </summary>
    public static class SpectateRules
    {
        public const float KillcamSeconds = 3f;

        /// <summary>Aday izlenebilir mi: hayatta, kendisi değil, aynı takım.</summary>
        public static bool CanSpectate(SpectatorCandidate c, int localTeam, int localId)
        {
            return c.Alive && c.Id != localId && c.Team == localTeam;
        }

        /// <summary>Sağ/sol geçiş sırası: yalnız hayattaki takım arkadaşları.</summary>
        public static void BuildTeamOrder(IReadOnlyList<SpectatorCandidate> all, int localTeam, int localId, List<int> output)
        {
            if (output == null) return;
            output.Clear();
            if (all == null) return;
            for (var i = 0; i < all.Count; i++)
                if (CanSpectate(all[i], localTeam, localId))
                    output.Add(all[i].Id);
        }

        /// <summary>Geçerli hedef: öldüren takip ediliyorsa ve hayattaysa o; yoksa sıradaki geçerli takım arkadaşı; hiçbiri yoksa -1.</summary>
        public static int Resolve(IReadOnlyList<int> teamOrder, int currentId, bool followKiller, int killerId, bool killerAlive)
        {
            if (followKiller && killerId >= 0 && killerAlive)
                return killerId;
            return SpectatorTargeting.Resolve(teamOrder, currentId);
        }
    }
}
