using System.Collections.Generic;
using Project.Core.Domain;
using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// Dünya noktaları: yere iniş/doğma noktaları (GroundSpawnPoints), helikopter iniş bölgeleri (LandingZones — düz, ağaçsız,
    /// yapısız açıklıklar), tamamlayıcı ganimet noktaları (yapı üreticisinin az nokta verdiği bölgeler) ve yedek araç noktaları
    /// (yol kenarı).
    /// Arazi ve yapılar kurulduktan sonra çağrılır; yüzey kontrolü fizik ışınıyla yapılır (yalnızca araziye isabet kabul edilir).
    /// </summary>
    public sealed class WorldSpawnPlanner
    {
        private const float TreeCell = 8f;

        private readonly TerrainModel _model;
        private readonly Terrain _terrain;
        private readonly IReadOnlyList<Bounds> _structures;
        private readonly MapLayout _layout;
        private readonly System.Random _rng;
        private readonly Dictionary<long, List<Vector2>> _trees = new Dictionary<long, List<Vector2>>();

        public WorldSpawnPlanner(TerrainModel model, Terrain terrain, IReadOnlyList<Bounds> structures, int seed)
        {
            _model = model;
            _terrain = terrain;
            _structures = structures ?? new List<Bounds>();
            _layout = model.Layout;
            _rng = new System.Random(seed * 4241 + 71);
            IndexTrees();
            Physics.SyncTransforms();
        }

        // ================================================================== Yere doğma noktaları

        /// <summary>Haritaya yayılmış, yürünebilir, açık zemin noktaları (y = zemin).</summary>
        public List<Vector3> PlanGroundSpawns(int count, float minSpacing)
        {
            var result = new List<Vector3>(count);
            var half = _layout.HalfSize;
            var margin = 70f;
            var attempts = count * 120;
            var spacingSqr = minSpacing * minSpacing;
            for (var a = 0; a < attempts && result.Count < count; a++)
            {
                var x = Range(-half + margin, half - margin);
                var z = Range(-half + margin, half - margin);
                if (!IsFarFrom(result, x, z, spacingSqr))
                    continue;
                if (_model.SampleSlope(x, z) > 16f)
                    continue;
                if (_model.SampleHeight(x, z) < _layout.WaterLevel + 0.8f)
                    continue;
                if (_model.SampleRiverDistance(x, z) < _model.RiverWaterHalfWidth + 4f)
                    continue;
                if (RockScatter.OverlapsStructure(_structures, x, z, 3f) || HasTreeWithin(x, z, 2.5f))
                    continue;
                if (!TryTerrainSurface(x, z, out var point))
                    continue;

                result.Add(point + Vector3.up * 0.05f);
            }

            return result;
        }

        // ================================================================== İniş bölgeleri

        /// <summary>
        /// Helikopter iniş bölgeleri: önce her büyük bölgenin çevresinde birer tane, sonra haritayı dolduran açıklıklar.
        /// </summary>
        public List<Vector3> PlanLandingZones(int count, float minSpacing)
        {
            var result = new List<Vector3>(count);
            var spacingSqr = minSpacing * minSpacing;
            var half = _layout.HalfSize;

            // Bölge çevreleri.
            for (var i = 0; i < _layout.Locations.Count && result.Count < count; i++)
            {
                var l = _layout.Locations[i];
                if (l == null || !l.IsMajor || l.Kind == LocationKind.Forest)
                    continue;

                for (var a = 0; a < 80; a++)
                {
                    var angle = Range(0f, Mathf.PI * 2f);
                    var distance = l.Radius + Range(20f, 110f);
                    var x = l.Center.x + Mathf.Cos(angle) * distance;
                    var z = l.Center.y + Mathf.Sin(angle) * distance;
                    if (Mathf.Abs(x) > half - 90f || Mathf.Abs(z) > half - 90f)
                        continue;
                    if (!IsFarFrom(result, x, z, spacingSqr) || !IsLandingZone(x, z, out var point))
                        continue;

                    result.Add(point);
                    break;
                }
            }

            // Genel dağılım.
            var attempts = count * 200;
            for (var a = 0; a < attempts && result.Count < count; a++)
            {
                var x = Range(-half + 90f, half - 90f);
                var z = Range(-half + 90f, half - 90f);
                if (!IsFarFrom(result, x, z, spacingSqr) || !IsLandingZone(x, z, out var point))
                    continue;
                result.Add(point);
            }

            return result;
        }

        private bool IsLandingZone(float x, float z, out Vector3 point)
        {
            point = default;
            const float radius = 9f;
            var w = _layout.WaterLevel;
            var center = _model.SampleHeight(x, z);
            if (center < w + 1f || _model.SampleSlope(x, z) > 9f)
                return false;
            if (_model.SampleRiverDistance(x, z) < _model.RiverWaterHalfWidth + 14f)
                return false;

            for (var i = 0; i < 8; i++)
            {
                var angle = i * Mathf.PI * 0.25f;
                var h = _model.SampleHeight(x + Mathf.Cos(angle) * radius, z + Mathf.Sin(angle) * radius);
                if (Mathf.Abs(h - center) > 1.6f || h < w + 0.8f)
                    return false;
            }

            if (HasTreeWithin(x, z, 11f) || RockScatter.OverlapsStructure(_structures, x, z, 12f))
                return false;
            if (!TryTerrainSurface(x, z, out point))
                return false;

            // Rotor çevresinde kaya/yapı olmasın (yanal kontrol).
            for (var i = 0; i < 4; i++)
            {
                var angle = i * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                if (!TryTerrainSurface(x + Mathf.Cos(angle) * 6f, z + Mathf.Sin(angle) * 6f, out _))
                    return false;
            }

            return true;
        }

        // ================================================================== Yedek / tamamlayıcı ganimet

        /// <summary>
        /// Ganimet yoğunluğu tabanı: yapı üreticisinin bir bölgeye verdiği nokta sayısı, türe göre hedefin yarısından azsa bölge
        /// hedefe tamamlanır (açık zemin noktaları, bölge kademesi). Hiç nokta yoksa yollar boyunca düşük kademe noktalar da eklenir.
        /// Yalnızca EKLENECEK noktaları döner.
        /// </summary>
        public List<LootSpawnPointData> PlanSupplementLoot(IReadOnlyList<LootSpawnPointData> existing)
        {
            var result = new List<LootSpawnPointData>(320);
            var existingCount = existing != null ? existing.Count : 0;
            for (var i = 0; i < _layout.Locations.Count; i++)
            {
                var l = _layout.Locations[i];
                if (l == null)
                    continue;

                var want = LootCount(l.Kind);
                var have = CountWithin(existing, l.Center, Mathf.Max(l.Radius, 10f));
                if (have >= want / 2)
                    continue;

                var count = want - have;
                var radius = Mathf.Max(8f, Mathf.Min(l.Radius, Mathf.Max(l.FlattenRadius, 20f)) * 0.85f);
                var placed = 0;
                for (var a = 0; a < count * 25 && placed < count; a++)
                {
                    var angle = Range(0f, Mathf.PI * 2f);
                    var distance = Mathf.Sqrt(Range(0.02f, 1f)) * radius;
                    var x = l.Center.x + Mathf.Cos(angle) * distance;
                    var z = l.Center.y + Mathf.Sin(angle) * distance;
                    if (_model.SampleSlope(x, z) > 28f || _model.SampleHeight(x, z) < _layout.WaterLevel + 0.3f)
                        continue;
                    if (!TryTerrainSurface(x, z, out var point))
                        continue;

                    result.Add(new LootSpawnPointData(point + Vector3.up * 0.05f, l.Tier));
                    placed++;
                }
            }

            if (existingCount > 0)
                return result;

            // Yol kenarları (düşük kademe).
            for (var r = 0; r < _layout.Roads.Count; r++)
            {
                var road = _layout.Roads[r];
                if (road?.Points == null || road.Points.Count < 4)
                    continue;

                for (var i = 3; i < road.Points.Count - 3; i += 14)
                {
                    var p = road.Points[i];
                    var dir = (road.Points[i + 1] - road.Points[i - 1]).normalized;
                    var side = new Vector2(dir.y, -dir.x) * (road.Width * 0.5f + 2.5f) * (_rng.Next(2) == 0 ? 1f : -1f);
                    var x = p.x + side.x;
                    var z = p.y + side.y;
                    if (_model.SampleSlope(x, z) > 25f || _model.SampleHeight(x, z) < _layout.WaterLevel + 0.5f)
                        continue;
                    if (!TryTerrainSurface(x, z, out var point))
                        continue;
                    result.Add(new LootSpawnPointData(point + Vector3.up * 0.05f, LootTier.Low));
                }
            }

            return result;
        }

        private static int CountWithin(IReadOnlyList<LootSpawnPointData> points, Vector2 center, float radius)
        {
            if (points == null)
                return 0;
            var sqr = radius * radius;
            var count = 0;
            for (var i = 0; i < points.Count; i++)
            {
                var dx = points[i].Position.x - center.x;
                var dz = points[i].Position.z - center.y;
                if (dx * dx + dz * dz <= sqr)
                    count++;
            }

            return count;
        }

        /// <summary>Bölge türüne göre hedef ganimet noktası sayısı.</summary>
        public static int LootCount(LocationKind kind)
        {
            switch (kind)
            {
                case LocationKind.Village: return 34;
                case LocationKind.ForwardBase: return 30;
                case LocationKind.Karakol: return 22;
                case LocationKind.Quarry:
                case LocationKind.Dam:
                case LocationKind.Ruins:
                    return 18;
                case LocationKind.RelayHill: return 16;
                case LocationKind.Farm: return 12;
                case LocationKind.Forest: return 10;
                default: return 7;
            }
        }

        // ================================================================== Yedek araç noktaları

        /// <summary>Yol kenarında (banket), yol yönünde araç noktaları; ana yol öncelikli, köprülerden uzak.</summary>
        public List<VehicleSpawnData> PlanRoadsideVehicles(int count, IReadOnlyList<VehicleSpawnData> existing)
        {
            var result = new List<VehicleSpawnData>(count);
            var taken = new List<Vector3>();
            if (existing != null)
            {
                for (var i = 0; i < existing.Count; i++)
                    taken.Add(existing[i].Position);
            }

            // Asfalt yollar önce.
            for (var pass = 0; pass < 2 && result.Count < count; pass++)
            {
                for (var r = 0; r < _layout.Roads.Count && result.Count < count; r++)
                {
                    var road = _layout.Roads[r];
                    if (road?.Points == null || road.Points.Count < 6)
                        continue;
                    if ((pass == 0) != (road.Kind == RoadKind.Asphalt))
                        continue;

                    var stride = pass == 0 ? 18 : 22;
                    for (var i = 4; i < road.Points.Count - 4 && result.Count < count; i += stride)
                    {
                        var p = road.Points[i];
                        if (Mathf.Abs(p.x) > _layout.HalfSize - 60f || Mathf.Abs(p.y) > _layout.HalfSize - 60f)
                            continue;
                        if (NearBridge(p, 12f) || !IsFarFrom(taken, p.x, p.y, 120f * 120f))
                            continue;

                        var dir = (road.Points[i + 1] - road.Points[i - 1]).normalized;
                        var right = new Vector2(dir.y, -dir.x);
                        var offset = road.Width * 0.5f + 2.2f;
                        var x = p.x + right.x * offset;
                        var z = p.y + right.y * offset;
                        if (_model.SampleSlope(x, z) > 10f || _model.SampleHeight(x, z) < _layout.WaterLevel + 1f)
                            continue;
                        if (HasTreeWithin(x, z, 4f) || RockScatter.OverlapsStructure(_structures, x, z, 4f))
                            continue;
                        if (!TryTerrainSurface(x, z, out var point))
                            continue;

                        var yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
                        result.Add(new VehicleSpawnData(point + Vector3.up * 0.3f, yaw));
                        taken.Add(point);
                    }
                }
            }

            return result;
        }

        private bool NearBridge(Vector2 p, float margin)
        {
            for (var i = 0; i < _layout.Bridges.Count; i++)
            {
                var b = _layout.Bridges[i];
                if (b != null && Vector2.Distance(p, b.Center) < b.Length * 0.5f + margin)
                    return true;
            }

            return false;
        }

        // ================================================================== Yardımcılar

        /// <summary>Noktanın üstündeki en üst yüzey arazi mi? (Yapı/kaya üstü reddedilir.) point = arazi yüzeyi.</summary>
        private bool TryTerrainSurface(float x, float z, out Vector3 point)
        {
            var h = _model.SampleHeight(x, z);
            point = new Vector3(x, h, z);
            var origin = new Vector3(x, h + 60f, z);
            if (!Physics.Raycast(origin, Vector3.down, out var hit, 120f, GameLayers.GroundMask, QueryTriggerInteraction.Ignore))
                return _terrain == null; // fizik yoksa (arazi çarpıştırıcısı yok) modele güven
            // Ağaç çarpıştırıcıları da TerrainCollider'a aittir — gövde tepesine isabet edilmişse reddet.
            if (!(hit.collider is TerrainCollider) || hit.point.y > h + 1.2f)
                return false;
            point = hit.point;
            return true;
        }

        private void IndexTrees()
        {
            if (_terrain == null || _terrain.terrainData == null)
                return;
            var data = _terrain.terrainData;
            var instances = data.treeInstances;
            var size = data.size;
            var origin = _terrain.transform.position;
            for (var i = 0; i < instances.Length; i++)
            {
                var p = instances[i].position;
                var x = origin.x + p.x * size.x;
                var z = origin.z + p.z * size.z;
                var key = Key(Mathf.FloorToInt(x / TreeCell), Mathf.FloorToInt(z / TreeCell));
                if (!_trees.TryGetValue(key, out var list))
                {
                    list = new List<Vector2>(4);
                    _trees[key] = list;
                }

                list.Add(new Vector2(x, z));
            }
        }

        private bool HasTreeWithin(float x, float z, float radius)
        {
            var r = Mathf.CeilToInt(radius / TreeCell);
            var cx = Mathf.FloorToInt(x / TreeCell);
            var cz = Mathf.FloorToInt(z / TreeCell);
            var sqr = radius * radius;
            for (var dz = -r; dz <= r; dz++)
            {
                for (var dx = -r; dx <= r; dx++)
                {
                    if (!_trees.TryGetValue(Key(cx + dx, cz + dz), out var list))
                        continue;
                    for (var i = 0; i < list.Count; i++)
                    {
                        var ox = list[i].x - x;
                        var oz = list[i].y - z;
                        if (ox * ox + oz * oz < sqr)
                            return true;
                    }
                }
            }

            return false;
        }

        private static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;

        private static bool IsFarFrom(List<Vector3> points, float x, float z, float minSqr)
        {
            for (var i = 0; i < points.Count; i++)
            {
                var dx = points[i].x - x;
                var dz = points[i].z - z;
                if (dx * dx + dz * dz < minSqr)
                    return false;
            }

            return true;
        }

        private float Range(float min, float max)
        {
            return min + (float)_rng.NextDouble() * (max - min);
        }
    }
}
