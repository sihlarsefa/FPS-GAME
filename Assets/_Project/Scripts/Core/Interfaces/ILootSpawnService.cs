using System.Collections.Generic;
using Project.Core.Domain;

namespace Project.Core.Interfaces
{
    public interface ILootSpawnService
    {
        /// <summary>Bir yağma noktasının dolu olma olasılığı.</summary>
        float SpawnChance(LootTier tier);

        /// <summary>Tek bir eşya çeker.</summary>
        LootItemData Roll(LootTier tier, IRandom random);

        /// <summary>Bir yağma noktası için eşya grubu üretir (ör. silah + mermisi). Çıktı listesine ekler.</summary>
        void RollSpawnGroup(LootTier tier, IRandom random, List<LootItemData> output);
    }
}
