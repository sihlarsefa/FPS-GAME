using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>Kuzgun Vadisi arazisi üretici (yükseklik, katmanlar, ağaçlar). STUB — uygulanıyor.</summary>
    public static class TerrainGenerator
    {
        public static Terrain Create(MapLayout layout, TerrainData data, Transform parent, int seed)
        {
            if (data == null)
                data = new TerrainData();
            var go = Terrain.CreateTerrainGameObject(data);
            if (parent != null)
                go.transform.SetParent(parent, false);
            return go.GetComponent<Terrain>();
        }
    }
}
