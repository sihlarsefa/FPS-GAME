using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// Çalışma zamanı/editör NavMesh pişirici (NavMeshComponents paketine gerek yok). Fizik çarpıştırıcılarından kaynak toplar,
    /// araziyi ve arazi ağaçlarını (kapsül engel) ekler, yapay zekâ ajanı ölçüleriyle (yarıçap .35, boy 1.8, eğim 40°,
    /// basamak .45, voksel .18) NavMeshData üretir. EnsureLoaded veriyi bir kez NavMesh'e ekler.
    /// </summary>
    public static class NavMeshBaker
    {
        public const float AgentRadius = 0.35f;
        public const float AgentHeight = 1.8f;
        public const float AgentSlope = 40f;
        public const float AgentClimb = 0.45f;
        public const float VoxelSize = 0.18f;
        public const int TileSize = 256;
        public const float MinRegionArea = 4f;

        /// <summary>NavMesh alan kimlikleri (yerleşik): 0 Walkable, 1 Not Walkable, 2 Jump.</summary>
        public const int AreaWalkable = 0;
        public const int AreaNotWalkable = 1;

        private static readonly Dictionary<NavMeshData, NavMeshDataInstance> Loaded = new Dictionary<NavMeshData, NavMeshDataInstance>();
        private static readonly List<NavMeshData> PruneBuffer = new List<NavMeshData>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Loaded.Clear();
        }

        /// <summary>Varsayılan ajan tipinin (0) kopyası, HAREKÂT ölçüleriyle.</summary>
        public static NavMeshBuildSettings CreateSettings()
        {
            var settings = UnityEngine.AI.NavMesh.GetSettingsByID(0);
            settings.agentTypeID = 0;
            settings.agentRadius = AgentRadius;
            settings.agentHeight = AgentHeight;
            settings.agentSlope = AgentSlope;
            settings.agentClimb = AgentClimb;
            settings.overrideVoxelSize = true;
            settings.voxelSize = VoxelSize;
            settings.overrideTileSize = true;
            settings.tileSize = TileSize;
            settings.minRegionArea = MinRegionArea;
            return settings;
        }

        /// <summary>Sözleşme imzası: fizik çarpıştırıcıları (layerMask) + arazi + ağaç kapsülleri.</summary>
        public static NavMeshData Bake(Bounds bounds, Terrain terrain, int layerMask)
        {
            return Bake(bounds, terrain, layerMask, null);
        }

        /// <summary>
        /// Ek kaynaklarla pişirir (ör. derin su için NotWalkable ModifierBox). Kaynak yoksa ya da pişirme başarısızsa null döner
        /// (istisna fırlatmaz).
        /// </summary>
        public static NavMeshData Bake(Bounds bounds, Terrain terrain, int layerMask, IReadOnlyList<NavMeshBuildSource> extraSources)
        {
            var sources = new List<NavMeshBuildSource>(1024);
            try
            {
                Physics.SyncTransforms();
                NavMeshBuilder.CollectSources(bounds, layerMask, NavMeshCollectGeometry.PhysicsColliders, AreaWalkable,
                    new List<NavMeshBuildMarkup>(), sources);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[NavMeshBaker] Kaynak toplanamadı: " + e.Message);
            }

            RemoveInvalidSources(sources);

            if (terrain != null && terrain.terrainData != null)
            {
                if (!ContainsTerrain(sources, terrain.terrainData))
                    sources.Add(CreateTerrainSource(terrain));
                AddTreeSources(terrain, bounds, sources);
            }

            if (extraSources != null)
            {
                for (var i = 0; i < extraSources.Count; i++)
                    sources.Add(extraSources[i]);
            }

            if (sources.Count == 0)
            {
                Debug.LogWarning("[NavMeshBaker] Pişirilecek kaynak yok.");
                return null;
            }

            var settings = CreateSettings();
            var report = settings.ValidationReport(bounds);
            if (report != null)
            {
                for (var i = 0; i < report.Length; i++)
                    Debug.LogWarning("[NavMeshBaker] Ayar uyarısı: " + report[i]);
            }

            NavMeshData data = null;
            try
            {
                data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[NavMeshBaker] Pişirme başarısız: " + e.Message);
            }

            if (data != null)
                data.name = "HK_NavMesh";
            return data;
        }

        /// <summary>Veriyi NavMesh'e bir kez ekler (zaten yüklüyse hiçbir şey yapmaz). null güvenli.</summary>
        public static void EnsureLoaded(NavMeshData data)
        {
            if (data == null)
                return;

            if (Loaded.TryGetValue(data, out var instance) && instance.valid)
                return;

            instance = UnityEngine.AI.NavMesh.AddNavMeshData(data);
            if (instance.valid)
                Loaded[data] = instance;
            else
                Loaded.Remove(data);
        }

        /// <summary>Veri yüklüyse NavMesh'ten kaldırır (sahne kapanırken WorldMetadata çağırır). null güvenli.</summary>
        public static void Unload(NavMeshData data)
        {
            if (ReferenceEquals(data, null))
                return;

            if (Loaded.TryGetValue(data, out var instance))
            {
                if (instance.valid)
                    instance.Remove();
                Loaded.Remove(data);
            }

            PruneDestroyed();
        }

        /// <summary>Veri şu anda yüklü mü?</summary>
        public static bool IsLoaded(NavMeshData data)
        {
            return data != null && Loaded.TryGetValue(data, out var instance) && instance.valid;
        }

        /// <summary>Ağaç gövdesi kapsülü kaynağı (dünya konumu, yarıçap, boy).</summary>
        public static NavMeshBuildSource CreateCapsuleSource(Vector3 bottomCenter, float radius, float height, int area = AreaWalkable)
        {
            height = Mathf.Max(height, radius * 2f);
            return new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Capsule,
                size = new Vector3(radius * 2f, height, radius * 2f),
                transform = Matrix4x4.TRS(bottomCenter + Vector3.up * (height * 0.5f), Quaternion.identity, Vector3.one),
                area = area
            };
        }

        /// <summary>Alan değiştirici kutu (ör. derin su → NotWalkable).</summary>
        public static NavMeshBuildSource CreateModifierBox(Vector3 center, Vector3 size, Quaternion rotation, int area)
        {
            return new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.ModifierBox,
                size = size,
                transform = Matrix4x4.TRS(center, rotation, Vector3.one),
                area = area
            };
        }

        private static NavMeshBuildSource CreateTerrainSource(Terrain terrain)
        {
            return new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Terrain,
                sourceObject = terrain.terrainData,
                transform = Matrix4x4.TRS(terrain.transform.position, Quaternion.identity, Vector3.one),
                area = AreaWalkable
            };
        }

        private static bool ContainsTerrain(List<NavMeshBuildSource> sources, TerrainData data)
        {
            for (var i = 0; i < sources.Count; i++)
            {
                if (sources[i].shape == NavMeshBuildSourceShape.Terrain && sources[i].sourceObject == data)
                    return true;
            }

            return false;
        }

        private static void RemoveInvalidSources(List<NavMeshBuildSource> sources)
        {
            for (var i = sources.Count - 1; i >= 0; i--)
            {
                var s = sources[i];
                var needsObject = s.shape == NavMeshBuildSourceShape.Mesh || s.shape == NavMeshBuildSourceShape.Terrain;
                if (needsObject && s.sourceObject == null)
                {
                    sources.RemoveAt(i);
                    continue;
                }

                // Tetikleyiciler ve dinamik (Rigidbody'li, kinematik olmayan) nesneler yürünebilir zemin değildir.
                var component = s.component;
                if (component is Collider collider && (collider.isTrigger || (collider.attachedRigidbody != null && !collider.attachedRigidbody.isKinematic)))
                    sources.RemoveAt(i);
            }
        }

        private static void AddTreeSources(Terrain terrain, Bounds bounds, List<NavMeshBuildSource> sources)
        {
            var data = terrain.terrainData;
            var prototypes = data.treePrototypes;
            if (prototypes == null || prototypes.Length == 0)
                return;

            // Prototip başına gövde kapsülü (prefab'taki CapsuleCollider'dan; yoksa varsayılan).
            var radius = new float[prototypes.Length];
            var height = new float[prototypes.Length];
            var hasCollider = new bool[prototypes.Length];
            for (var p = 0; p < prototypes.Length; p++)
            {
                radius[p] = 0.3f;
                height[p] = 4f;
                var prefab = prototypes[p] != null ? prototypes[p].prefab : null;
                if (prefab == null)
                    continue;

                var capsule = prefab.GetComponent<CapsuleCollider>();
                if (capsule == null || !capsule.enabled)
                    continue;

                hasCollider[p] = true;
                var scale = prefab.transform.localScale;
                radius[p] = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
                height[p] = Mathf.Max(capsule.height, capsule.center.y + capsule.height * 0.5f) * Mathf.Abs(scale.y);
            }

            var instances = data.treeInstances;
            var size = data.size;
            var origin = terrain.transform.position;
            for (var i = 0; i < instances.Length; i++)
            {
                var tree = instances[i];
                var p = tree.prototypeIndex;
                if (p < 0 || p >= prototypes.Length || !hasCollider[p])
                    continue;

                var world = origin + Vector3.Scale(tree.position, size);
                if (!bounds.Contains(new Vector3(world.x, bounds.center.y, world.z)))
                    continue;

                var r = radius[p] * Mathf.Max(0.1f, tree.widthScale);
                var h = height[p] * Mathf.Max(0.1f, tree.heightScale);
                sources.Add(CreateCapsuleSource(world, Mathf.Max(0.15f, r), Mathf.Max(1f, h)));
            }
        }

        private static void PruneDestroyed()
        {
            PruneBuffer.Clear();
            foreach (var pair in Loaded)
            {
                if (pair.Key == null || !pair.Value.valid)
                    PruneBuffer.Add(pair.Key);
            }

            for (var i = 0; i < PruneBuffer.Count; i++)
                Loaded.Remove(PruneBuffer[i]);
            PruneBuffer.Clear();
        }
    }
}
