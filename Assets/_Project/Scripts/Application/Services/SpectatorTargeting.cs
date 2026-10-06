using System.Collections.Generic;

namespace Project.Application.Services
{
    /// <summary>İzleyici hedef adayı (saf veri).</summary>
    public readonly struct SpectatorCandidate
    {
        public readonly int Id;
        public readonly int Team;
        public readonly bool Alive;

        public SpectatorCandidate(int id, int team, bool alive)
        {
            Id = id;
            Team = team;
            Alive = alive;
        }
    }

    /// <summary>İzleyici hedef sırası: önce hayattaki takım arkadaşları, sonra hayattaki herkes. Saf mantık.</summary>
    public static class SpectatorTargeting
    {
        public static void BuildOrder(IReadOnlyList<SpectatorCandidate> all, int localTeam, int excludeId, List<int> output)
        {
            if (output == null)
                return;

            output.Clear();
            if (all == null)
                return;

            for (var i = 0; i < all.Count; i++)
                if (all[i].Alive && all[i].Id != excludeId && all[i].Team == localTeam)
                    output.Add(all[i].Id);
            for (var i = 0; i < all.Count; i++)
                if (all[i].Alive && all[i].Id != excludeId && all[i].Team != localTeam)
                    output.Add(all[i].Id);
        }

        /// <summary>Sıradaki (direction=+1) ya da önceki (-1) hedef; mevcut yoksa ilki. Liste boşsa -1.</summary>
        public static int Cycle(IReadOnlyList<int> order, int currentId, int direction)
        {
            if (order == null || order.Count == 0)
                return -1;

            var index = -1;
            for (var i = 0; i < order.Count; i++)
                if (order[i] == currentId)
                {
                    index = i;
                    break;
                }

            if (index < 0)
                return order[0];

            var step = direction < 0 ? -1 : 1;
            var next = ((index + step) % order.Count + order.Count) % order.Count;
            return order[next];
        }

        /// <summary>Mevcut hedef hâlâ sırada mı yoksa başka hedefe geçilmeli mi: geçerli hedef id'si ya da -1.</summary>
        public static int Resolve(IReadOnlyList<int> order, int currentId)
        {
            if (order == null || order.Count == 0)
                return -1;

            for (var i = 0; i < order.Count; i++)
                if (order[i] == currentId)
                    return currentId;

            return order[0];
        }
    }
}
