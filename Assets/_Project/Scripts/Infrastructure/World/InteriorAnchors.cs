using System.Collections.Generic;
using UnityEngine;

namespace Project.Infrastructure.World
{
    public enum InteriorAnchorKind { Floor, Table, Shelf, Wardrobe, Bed, Crate }

    public struct InteriorAnchor
    {
        public Vector3 Position;
        public InteriorAnchorKind Kind;
        public RoomKind Room;
        /// <summary>0 = sıradan, 1 = iyi (dolap/raf), 2 = nadir (depo sandığı).</summary>
        public int TierHint;
    }

    /// <summary>
    /// Bina içi ganimet çapaları kaydı (dünya konumu). InteriorFurnisher doldurur; LootSpawner/LootSpawnService tüketir.
    /// LootSpawner.SpawnWorldLoot çapaları ek spawn noktası olarak tüketir ve Clear() eder.
    /// </summary>
    public static class InteriorAnchors
    {
        private static readonly List<InteriorAnchor> Items = new List<InteriorAnchor>(256);

        public static int Count => Items.Count;

        public static void Register(Vector3 worldPos, InteriorAnchorKind kind, RoomKind room, int tierHint)
        {
            Items.Add(new InteriorAnchor { Position = worldPos, Kind = kind, Room = room, TierHint = tierHint });
        }

        public static void Clear() => Items.Clear();

        public static void Snapshot(List<InteriorAnchor> output)
        {
            if (output == null)
                return;
            output.AddRange(Items);
        }

        public static int TierFor(InteriorAnchorKind k)
        {
            switch (k)
            {
                case InteriorAnchorKind.Crate: return 2;
                case InteriorAnchorKind.Wardrobe:
                case InteriorAnchorKind.Shelf: return 1;
                default: return 0;
            }
        }
    }
}
