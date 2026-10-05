using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// Sahnedeki dünyanın çalışma zamanı verisi: harita boyu, su seviyesi, arazi, mini harita dokusu, ganimet/iniş/araç noktaları,
    /// adlandırılmış bölgeler ve NavMesh verisi. Editörde üretilip sahneye kaydedilir (alanlar serileştirilir) ya da çalışma
    /// zamanında <see cref="WorldGenerator.Generate"/> / TrainingRangeBuilder ile doldurulur.
    /// Instance Awake/OnEnable'da atanır, OnDestroy'da temizlenir. Awake NavMesh verisini bir kez yükler.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldMetadata : MonoBehaviour
    {
        /// <summary>Hiçbir bölgenin içinde değilken <see cref="GetLocationName"/> dönüşü.</summary>
        public const string DefaultRegionName = "Kuzgun Vadisi";

        /// <summary>Ham zemin ışını bu yükseklikten (en yüksek arazinin üstünden) aşağı atılır.</summary>
        private const float RaycastHeadroom = 2f;

        public static WorldMetadata Instance { get; private set; }

        [Tooltip("Harita yarı boyu (m). Harita x,z ∈ [-MapHalfSize, MapHalfSize].")]
        public float MapHalfSize = 512f;

        [Tooltip("Su yüzeyi yüksekliği (dünya Y).")]
        public float WaterLevel = 18f;

        public Terrain Terrain;
        public Texture2D MinimapTexture;
        public List<LootSpawnPointData> LootPoints = new List<LootSpawnPointData>();
        public List<NamedLocation> Locations = new List<NamedLocation>();
        public List<Vector3> GroundSpawnPoints = new List<Vector3>();
        public List<VehicleSpawnData> VehicleSpawns = new List<VehicleSpawnData>();
        public NavMeshData NavMesh;

        [Tooltip("Haritanın dünya merkezi (XZ). Kuzgun Vadisi için (0,0).")]
        public Vector2 MapCenter = Vector2.zero;

        [Tooltip("Yapı ayak izleri (mini harita / yapay zekâ siper araması için, isteğe bağlı).")]
        public List<Bounds> StructureBounds = new List<Bounds>();

        [Tooltip("Helikopter iniş bölgeleri (düz, açık alanlar). Boşsa GroundSpawnPoints kullanılır.")]
        public List<Vector3> LandingZones = new List<Vector3>();

        [Tooltip("Haritadaki en yüksek nokta (m). Işın testleri bunun üstünden başlar.")]
        public float MaxHeight = 160f;

        /// <summary>Çalışma zamanında üretildiyse yerleşim verisi (serileştirilmez; editörde üretilen sahnede null olabilir).</summary>
        [NonSerialized] public MapLayout Layout;

        [NonSerialized] private bool _terrainSearched;

        /// <summary>Dünya sınırları (XZ haritası, Y 0..MaxHeight).</summary>
        public Bounds WorldBounds
        {
            get
            {
                var size = new Vector3(MapHalfSize * 2f, Mathf.Max(1f, MaxHeight) + 40f, MapHalfSize * 2f);
                var center = new Vector3(MapCenter.x, size.y * 0.5f - 20f, MapCenter.y);
                return new Bounds(center, size);
            }
        }

        private void Awake()
        {
            Instance = this;
            ResolveTerrain();
            EnsureNavMeshLoaded();
        }

        private void OnEnable()
        {
            if (Instance == null || Instance != this)
                Instance = this;
        }

        private void OnDestroy()
        {
            NavMeshBaker.Unload(NavMesh);
            if (Instance == this)
                Instance = null;
        }

        /// <summary>NavMesh verisini (varsa) bir kez yükler. Birden fazla çağrı güvenlidir.</summary>
        public void EnsureNavMeshLoaded()
        {
            if (NavMesh != null)
                NavMeshBaker.EnsureLoaded(NavMesh);
        }

        /// <summary>
        /// Arazinin XZ'deki yüksekliği (dünya Y). Yalnızca arazi (yapılar hariç), fizik sorgusu yok — ucuzdur.
        /// Arazi yoksa aşağı ışın; o da yoksa 0.
        /// </summary>
        public float SampleGroundHeight(Vector3 position)
        {
            var terrain = ResolveTerrain();
            if (terrain != null && terrain.terrainData != null)
                return terrain.SampleHeight(position) + terrain.transform.position.y;

            var origin = new Vector3(position.x, MaxHeight + 200f, position.z);
            if (Physics.Raycast(origin, Vector3.down, out var hit, MaxHeight + 1000f, GameLayers.GroundMask, QueryTriggerInteraction.Ignore))
                return hit.point.y;

            return 0f;
        }

        /// <summary>
        /// Verilen noktanın altındaki katı zemini bulur (yapı zemini/çatısı dahil). Işın max(position.y, arazi) + 2 m'den aşağı
        /// atılır — en üst yüzeyi istemek için position.y'yi yüksek verin. Harita dışında false.
        /// </summary>
        public bool TryGetGroundPoint(Vector3 position, out Vector3 point)
        {
            point = position;
            if (!IsInsideMap(position, 0f))
                return false;

            var terrainY = SampleGroundHeight(position);
            var startY = Mathf.Max(position.y, terrainY) + RaycastHeadroom;
            var origin = new Vector3(position.x, startY, position.z);
            var distance = startY - Mathf.Min(terrainY, 0f) + 50f;
            if (Physics.Raycast(origin, Vector3.down, out var hit, distance, GameLayers.GroundMask, QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                return true;
            }

            var terrain = ResolveTerrain();
            if (terrain != null && terrain.terrainData != null)
            {
                point = new Vector3(position.x, terrainY, position.z);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Dünya konumu → mini harita UV'si (0..1; (0,0) güneybatı köşe, v kuzeye artar). Harita dışı için sınırlandırılmaz.
        /// </summary>
        public Vector2 WorldToMapUV(Vector3 world)
        {
            var size = Mathf.Max(1f, MapHalfSize * 2f);
            return new Vector2(
                (world.x - (MapCenter.x - MapHalfSize)) / size,
                (world.z - (MapCenter.y - MapHalfSize)) / size);
        }

        /// <summary>Mini harita UV'si → dünya konumu (Y = arazi yüksekliği).</summary>
        public Vector3 MapUVToWorld(Vector2 uv)
        {
            var size = MapHalfSize * 2f;
            var p = new Vector3(MapCenter.x - MapHalfSize + uv.x * size, 0f, MapCenter.y - MapHalfSize + uv.y * size);
            p.y = SampleGroundHeight(p);
            return p;
        }

        /// <summary>Nokta harita içinde mi (kenar payı kadar içeride)?</summary>
        public bool IsInsideMap(Vector3 position, float margin)
        {
            return Mathf.Abs(position.x - MapCenter.x) <= MapHalfSize - margin
                   && Mathf.Abs(position.z - MapCenter.y) <= MapHalfSize - margin;
        }

        /// <summary>Konum su yüzeyinin altında mı (nehir/göl)?</summary>
        public bool IsUnderWater(Vector3 position)
        {
            return position.y < WaterLevel && SampleGroundHeight(position) < WaterLevel - 0.05f;
        }

        /// <summary>Konumun içinde bulunduğu bölge adı; hiçbir bölgede değilse <see cref="DefaultRegionName"/>.</summary>
        public string GetLocationName(Vector3 position)
        {
            return TryGetLocation(position, out var location) && !string.IsNullOrEmpty(location.Name)
                ? location.Name
                : DefaultRegionName;
        }

        /// <summary>Konumu içeren (yarıçapına göre en yakın) bölge. Yoksa false.</summary>
        public bool TryGetLocation(Vector3 position, out NamedLocation location)
        {
            location = null;
            if (Locations == null)
                return false;

            var best = float.MaxValue;
            for (var i = 0; i < Locations.Count; i++)
            {
                var l = Locations[i];
                if (l == null)
                    continue;

                var radius = Mathf.Max(1f, l.Radius);
                var dx = position.x - l.Center.x;
                var dz = position.z - l.Center.y;
                var normalized = Mathf.Sqrt(dx * dx + dz * dz) / radius;
                if (normalized <= 1f && normalized < best)
                {
                    best = normalized;
                    location = l;
                }
            }

            return location != null;
        }

        /// <summary>Konuma en yakın bölge (mesafe sınırı yok). Bölge yoksa null.</summary>
        public NamedLocation FindNearestLocation(Vector3 position, bool majorOnly = false)
        {
            if (Locations == null)
                return null;

            NamedLocation best = null;
            var bestSqr = float.MaxValue;
            for (var i = 0; i < Locations.Count; i++)
            {
                var l = Locations[i];
                if (l == null || (majorOnly && !l.IsMajor))
                    continue;

                var dx = position.x - l.Center.x;
                var dz = position.z - l.Center.y;
                var sqr = dx * dx + dz * dz;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = l;
                }
            }

            return best;
        }

        private Terrain ResolveTerrain()
        {
            if (Terrain != null)
                return Terrain;
            if (_terrainSearched)
                return null;

            _terrainSearched = true;
            Terrain = GetComponentInChildren<Terrain>();
            if (Terrain == null && transform.parent != null)
                Terrain = transform.parent.GetComponentInChildren<Terrain>();
            return Terrain;
        }
    }
}
