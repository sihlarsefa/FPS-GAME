using Project.Core.Interfaces;
using UnityEngine;

namespace Project.Infrastructure.Loot
{
    /// <summary>ILootProximityQuery uyarlayıcısı: LootRegistry'nin uzamsal ızgarasını kullanır (sahne taraması yok).</summary>
    public sealed class UnityLootProximityQuery : MonoBehaviour, ILootProximityQuery
    {
        [SerializeField] private float defaultRadius = 2f;

        /// <summary>radius ≤ 0 verildiğinde kullanılan arama yarıçapı (m).</summary>
        public float DefaultRadius
        {
            get => defaultRadius;
            set => defaultRadius = Mathf.Max(0.1f, value);
        }

        public bool TryGetNearestPickup(float originX, float originY, float originZ, float radius, out ILootPickup pickup)
        {
            var found = LootRegistry.FindNearest(new Vector3(originX, originY, originZ), radius > 0f ? radius : defaultRadius);
            pickup = found;
            return found != null;
        }
    }
}
