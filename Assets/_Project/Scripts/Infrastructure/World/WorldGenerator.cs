using System.Collections.Generic;
using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>Dünya üretim seçenekleri.</summary>
    public sealed class WorldGenerationOptions
    {
        public int Seed = 1923;
        public TerrainData TerrainData;
        public Transform Parent;
        public bool BakeNavMesh = true;
        public bool GenerateMinimap = true;
        public int MinimapSize = 1024;
    }

    /// <summary>Kuzgun Vadisi dünyası üretici. STUB — uygulanıyor.</summary>
    public static class WorldGenerator
    {
        public static IReadOnlyList<GameObject> LastTreePrototypes { get; private set; } = new GameObject[0];

        public static WorldMetadata Generate(WorldGenerationOptions options)
        {
            options ??= new WorldGenerationOptions();
            var root = new GameObject("[Dünya] Kuzgun Vadisi");
            if (options.Parent != null)
                root.transform.SetParent(options.Parent, false);
            var layout = MapLayout.CreateKuzgunVadisi(options.Seed);
            var terrain = TerrainGenerator.Create(layout, options.TerrainData, root.transform, options.Seed);
            var meta = root.AddComponent<WorldMetadata>();
            meta.Terrain = terrain;
            meta.Layout = layout;
            return meta;
        }
    }
}
