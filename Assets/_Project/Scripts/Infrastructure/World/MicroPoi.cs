using System.Collections.Generic;
using Project.Core.Domain;
using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// Mikro-POI geçişi: yerleşimler ARASINDAKİ boş araziyi PUBG gibi doldurur. Yaklaşık 80 m'lik hücre ızgarasında her hücreye
    /// (yol/su/yerleşim yarıçapı dışında) bir "mikro nokta" konur: kaya öbeği, 2-6 ağaçlık koru, çalı hattı, yıkık duvar köşesi,
    /// çoban kulübesi, koyun ağılı, saman yığını, devrik kütük, kum torbalı siperlik, terk edilmiş araba; karlı bölgelerde kar
    /// yığını, kaya dikmesi (cairn) ve donmuş kuru ağaçlar. Her öğe en az diz boyu siper sağlar.
    /// Kaya/duvar/kulübe gibi katı parçalar 128 m'lik parçalarda tek mesh + MeshCollider olarak birleştirilir (az çizim çağrısı,
    /// parça başına kesme, NavMesh'e engel olarak girer); ağaç/çalılar arazi ağaç örneği olarak eklenir. Tohuma bağlı
    /// deterministiktir; harita kişiliği (Kuzgun orman ağırlıklı, Ayaz seyrek + karlı, Liman/Kartal) ağırlıkları değiştirir.
    /// </summary>
    public static class MicroPoi
    {
        public const string RootName = "MikroNoktalar";
        public const float CellSize = 80f;
        public const float MaxPerKm2 = 250f;
        public const float ChunkSize = 128f;

        // Palet (alt mesh indeksleri).
        private const int SubRock = 0, SubRockDark = 1, SubMoss = 2, SubStone = 3, SubWood = 4, SubHay = 5, SubSandbag = 6, SubSnow = 7,
            SubBark = 8, SubCount = 9;

        public enum PoiKind
        {
            RockCluster, Copse, BushLine, WallCorner, Hut, SheepPen, Haystack, FallenLog, Foxhole, Cart, SnowDrift, Cairn, FrozenTrees
        }

        /// <summary>Üretim sonucu: sayımlar ve yapı izleri (kaya/spawn çakışmasını önlemek için).</summary>
        public sealed class Result
        {
            public GameObject Root;
            public int PoiCount;
            public int ItemCount;
            public readonly List<Bounds> Footprints = new List<Bounds>();
        }

        /// <summary>Harita kişiliği: hücre doluluk olasılığı ve tür ağırlıkları (PoiKind sırasıyla).</summary>
        public sealed class Personality
        {
            public float Fill;
            public float[] Weights;
            public TreeKind[] Trees;
            public float[] TreeWeights;
        }

        private static readonly float[] SnowWeights = { 10f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 8f, 0f, 40f, 25f, 25f };

        public static Personality PersonalityFor(MapLayout layout)
        {
            var name = layout != null ? layout.Name : string.Empty;
            if (name == MapCatalog.AyazGecidiName)
            {
                return new Personality
                {
                    Fill = 0.72f,
                    //        Rock Copse Bush Wall Hut Pen Hay Log Fox Cart Drift Cairn Frozen
                    Weights = new[] { 16f, 6f, 3f, 6f, 4f, 2f, 0f, 6f, 9f, 3f, 8f, 5f, 6f },
                    Trees = new[] { TreeKind.PineA, TreeKind.PineB, TreeKind.Dead },
                    TreeWeights = new[] { 4f, 3f, 2f }
                };
            }

            if (name == MapCatalog.MaviLimanName)
            {
                return new Personality
                {
                    Fill = 0.85f,
                    Weights = new[] { 14f, 6f, 10f, 12f, 3f, 2f, 5f, 3f, 6f, 9f, 0f, 0f, 0f },
                    Trees = new[] { TreeKind.Oak, TreeKind.PineA },
                    TreeWeights = new[] { 3f, 2f }
                };
            }

            if (name == MapCatalog.KartalYaylasiName)
            {
                return new Personality
                {
                    Fill = 0.9f,
                    Weights = new[] { 16f, 8f, 8f, 14f, 9f, 11f, 6f, 3f, 5f, 4f, 0f, 4f, 0f },
                    Trees = new[] { TreeKind.PineA, TreeKind.PineB, TreeKind.Oak },
                    TreeWeights = new[] { 3f, 2f, 2f }
                };
            }

            // Kuzgun Vadisi (orman ağırlıklı) ve bilinmeyen haritalar.
            return new Personality
            {
                Fill = 0.95f,
                Weights = new[] { 14f, 26f, 16f, 8f, 4f, 3f, 5f, 14f, 5f, 3f, 0f, 0f, 0f },
                Trees = new[] { TreeKind.Oak, TreeKind.PineA, TreeKind.PineB },
                TreeWeights = new[] { 4f, 3f, 3f }
            };
        }

        /// <summary>Mikro noktaları üretir. structures: mevcut yapı sınırları (kaçınılır). Hiçbir şey konmadıysa null.</summary>
        public static Result Build(TerrainModel model, Terrain terrain, Transform parent, int seed, IReadOnlyList<Bounds> structures)
        {
            if (model == null || model.Layout == null)
                return null;

            var layout = model.Layout;
            var b = new Builder(model, terrain, structures, seed, PersonalityFor(layout));
            b.Run();
            return b.Finish(parent);
        }

        // ================================================================== Üretici

        private sealed class Builder
        {
            private readonly TerrainModel _model;
            private readonly Terrain _terrain;
            private readonly IReadOnlyList<Bounds> _structures;
            private readonly Personality _p;
            private readonly System.Random _rng;
            private readonly float _half;
            private readonly int _chunksPerSide;
            private readonly Dictionary<int, MeshBuilder> _builders = new Dictionary<int, MeshBuilder>();
            private readonly List<TreeInstance> _trees = new List<TreeInstance>(512);
            private readonly List<Vector2> _centers = new List<Vector2>(256);
            private readonly Result _result = new Result();
            private MeshBuilder _cur;
            private Vector3 _curOrigin;

            public Builder(TerrainModel model, Terrain terrain, IReadOnlyList<Bounds> structures, int seed, Personality p)
            {
                _model = model;
                _terrain = terrain;
                _structures = structures;
                _p = p;
                _half = model.Layout.HalfSize;
                _chunksPerSide = Mathf.Max(1, Mathf.CeilToInt(_half * 2f / ChunkSize));
                _rng = new System.Random(seed * 7919 + 0x3A5);
            }

            public void Run()
            {
                var areaKm2 = (_half * 2f) * (_half * 2f) / 1000000f;
                var budget = Mathf.CeilToInt(areaKm2 * MaxPerKm2);
                var cells = Mathf.Max(1, Mathf.FloorToInt(_half * 2f / CellSize));
                var cell = _half * 2f / cells;
                var layout = _model.Layout;

                for (var cz = 0; cz < cells && _result.ItemCount < budget; cz++)
                {
                    for (var cx = 0; cx < cells && _result.ItemCount < budget; cx++)
                    {
                        if (R() > _p.Fill)
                            continue;

                        var minX = -_half + cx * cell;
                        var minZ = -_half + cz * cell;
                        var kind = ChooseKind(minX + cell * 0.5f, minZ + cell * 0.5f, layout);
                        var placed = false;
                        for (var attempt = 0; attempt < 5 && !placed; attempt++)
                        {
                            var k = attempt < 3 ? kind : PoiKind.RockCluster;
                            var x = minX + Range(cell * 0.18f, cell * 0.82f);
                            var z = minZ + Range(cell * 0.18f, cell * 0.82f);
                            placed = TryBuild(k, x, z);
                        }

                        if (!placed)
                            continue;

                        // İkincil dolgu: orta mesafede küçük kaya/çalı.
                        if (R() < 0.45f && _result.ItemCount < budget)
                        {
                            var a = R() * Mathf.PI * 2f;
                            var d = Range(18f, 32f);
                            var last = _centers[_centers.Count - 1];
                            TryBuild(R() < 0.6f ? PoiKind.RockCluster : PoiKind.BushLine, last.x + Mathf.Cos(a) * d, last.y + Mathf.Sin(a) * d, true);
                        }
                    }
                }
            }

            public Result Finish(Transform parent)
            {
                if (_result.ItemCount == 0)
                    return null;

                // Yapı izlerindeki mevcut ağaçları temizle, sonra yeni korulukları ekle.
                if (_terrain != null && _terrain.terrainData != null)
                {
                    TreeScatter.RemoveTreesInBounds(_terrain, _result.Footprints, 1.0f);
                    AppendTrees();
                }

                var root = new GameObject(RootName);
                root.layer = GameLayers.Default;
                if (parent != null)
                    root.transform.SetParent(parent, false);
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

                var materials = new[]
                {
                    MaterialLibrary.Get(MaterialId.Rock), MaterialLibrary.Get(MaterialId.RockDark), VegetationMaterials.CreateMoss(),
                    MaterialLibrary.Get(MaterialId.Stone), MaterialLibrary.Get(MaterialId.Wood), MaterialLibrary.Get(MaterialId.Hay),
                    MaterialLibrary.Get(MaterialId.Sandbag), MaterialLibrary.Get(MaterialId.Snow), MaterialLibrary.Get(MaterialId.Bark)
                };
                foreach (var pair in _builders)
                {
                    var cx = pair.Key % _chunksPerSide;
                    var cz = pair.Key / _chunksPerSide;
                    var mesh = pair.Value.ToMesh("HK_MikroPoi_" + cx + "_" + cz);
                    var go = new GameObject("MikroPoi_" + cx + "_" + cz);
                    go.layer = GameLayers.Default;
                    go.transform.SetParent(root.transform, false);
                    go.transform.position = ChunkOrigin(cx, cz);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    go.AddComponent<MeshRenderer>().sharedMaterials = materials;
                    go.AddComponent<MeshCollider>().sharedMesh = mesh;
                    go.isStatic = true;
                }

                _result.Root = root;
                return _result;
            }

            private void AppendTrees()
            {
                if (_trees.Count == 0)
                    return;
                var data = _terrain.terrainData;
                var protoCount = data.treePrototypes != null ? data.treePrototypes.Length : 0;
                var size = data.size;
                var origin = _terrain.transform.position;
                var existing = data.treeInstances;
                var all = new List<TreeInstance>(existing.Length + _trees.Count);
                all.AddRange(existing);
                for (var i = 0; i < _trees.Count; i++)
                {
                    var t = _trees[i];
                    if (t.prototypeIndex >= protoCount)
                        continue;
                    var wx = t.position.x;
                    var wz = t.position.z;
                    t.position = new Vector3((wx - origin.x) / size.x, Mathf.Clamp01(t.position.y / Mathf.Max(1f, size.y)), (wz - origin.z) / size.z);
                    all.Add(t);
                }

                data.SetTreeInstances(all.ToArray(), false);
                WorldAssetPersistence.MarkDirty(data);
            }

            // ------------------------------------------------------------ Seçim ve doğrulama

            private PoiKind ChooseKind(float x, float z, MapLayout layout)
            {
                var h = _model.SampleHeight(x, z);
                var snow = Smooth(layout.SnowLine - 25f, layout.SnowLine + 5f, h);
                var weights = R() < snow ? SnowWeights : _p.Weights;
                var total = 0f;
                for (var i = 0; i < weights.Length; i++)
                    total += weights[i];
                var roll = R() * total;
                for (var i = 0; i < weights.Length; i++)
                {
                    roll -= weights[i];
                    if (roll <= 0f && weights[i] > 0f)
                        return (PoiKind)i;
                }

                return PoiKind.RockCluster;
            }

            private static float Radius(PoiKind k)
            {
                switch (k)
                {
                    case PoiKind.Hut: return 5.5f;
                    case PoiKind.SheepPen: return 7f;
                    case PoiKind.WallCorner: return 7f;
                    case PoiKind.BushLine: return 9f;
                    case PoiKind.Copse: return 7f;
                    case PoiKind.FrozenTrees: return 8f;
                    case PoiKind.Cart: return 3.5f;
                    case PoiKind.Foxhole: return 3.5f;
                    case PoiKind.FallenLog: return 4.5f;
                    case PoiKind.Haystack: return 3.5f;
                    default: return 4f;
                }
            }

            private bool TryBuild(PoiKind kind, float x, float z, bool filler = false)
            {
                var r = filler ? 3.5f : Radius(kind);
                var structural = kind == PoiKind.Hut || kind == PoiKind.SheepPen || kind == PoiKind.WallCorner || kind == PoiKind.Cart
                                 || kind == PoiKind.Foxhole || kind == PoiKind.Haystack;
                var maxSlope = structural ? 14f : 28f;
                if (!Valid(x, z, r, maxSlope, structural ? 1.1f : 3f, filler ? 12f : 30f))
                    return false;

                var o = new Vector3(x, _model.SampleHeight(x, z), z);
                var yaw = R() * 360f;
                var q = Quaternion.Euler(0f, yaw, 0f);
                Begin(o);
                var items = 1;
                var footprint = 0f;
                switch (kind)
                {
                    case PoiKind.RockCluster: items = filler ? Rocks(o, 2, 3.5f, false) : Rocks(o, 3 + _rng.Next(4), 4f, false); break;
                    case PoiKind.Copse: items = Copse(o, TreeSet.Normal); break;
                    case PoiKind.FrozenTrees: items = Copse(o, TreeSet.Frozen); Drifts(o, 1 + _rng.Next(2), 6f); break;
                    case PoiKind.BushLine: items = BushLine(o, q, filler); break;
                    case PoiKind.WallCorner: WallCorner(o, q); footprint = 6f; break;
                    case PoiKind.Hut: Hut(o, q); footprint = 3.6f; break;
                    case PoiKind.SheepPen: Pen(o, q); footprint = 6f; break;
                    case PoiKind.Haystack: Haystack(o, q); footprint = 2.6f; break;
                    case PoiKind.FallenLog: Log(o, q); footprint = 3.6f; break;
                    case PoiKind.Foxhole: Foxhole(o, q); footprint = 2.8f; break;
                    case PoiKind.Cart: Cart(o, q); footprint = 2.8f; break;
                    case PoiKind.SnowDrift: items = Drifts(o, 3 + _rng.Next(3), 4.5f); break;
                    case PoiKind.Cairn: Cairn(o); footprint = 1.8f; break;
                }

                _centers.Add(new Vector2(x, z));
                _result.PoiCount++;
                _result.ItemCount += Mathf.Max(1, items);
                if (footprint > 0f)
                    _result.Footprints.Add(new Bounds(new Vector3(x, o.y, z), new Vector3(footprint * 2f, 4f, footprint * 2f)));
                return true;
            }

            private bool Valid(float x, float z, float r, float maxSlope, float flatDelta, float minSpacing)
            {
                if (!_model.IsClearOfFeatures(x, z, r + 2f, r + 3f, 1.15f))
                    return false;
                var h = _model.SampleHeight(x, z);
                if (h < _model.Layout.WaterLevel + 1.2f)
                    return false;
                if (_model.SampleSlope(x, z) > maxSlope || _model.SampleFlatten(x, z) > 0.35f)
                    return false;

                var rr = Mathf.Min(r, 6f);
                var min = h;
                var max = h;
                for (var i = 0; i < 4; i++)
                {
                    var a = i * Mathf.PI * 0.5f;
                    var px = x + Mathf.Cos(a) * rr;
                    var pz = z + Mathf.Sin(a) * rr;
                    var hh = _model.SampleHeight(px, pz);
                    if (hh < _model.Layout.WaterLevel + 0.6f)
                        return false;
                    min = Mathf.Min(min, hh);
                    max = Mathf.Max(max, hh);
                }

                if (max - min > flatDelta * (rr / 3f + 1f))
                    return false;

                for (var i = 0; i < _centers.Count; i++)
                {
                    var dx = _centers[i].x - x;
                    var dz = _centers[i].y - z;
                    if (dx * dx + dz * dz < minSpacing * minSpacing)
                        return false;
                }

                if (_structures != null)
                {
                    for (var i = 0; i < _structures.Count; i++)
                    {
                        var s = _structures[i];
                        if (x >= s.min.x - r && x <= s.max.x + r && z >= s.min.z - r && z <= s.max.z + r)
                            return false;
                    }
                }

                return true;
            }

            // ------------------------------------------------------------ Parçalar

            private enum TreeSet { Normal, Frozen }

            private int Rocks(Vector3 o, int count, float spread, bool snowy)
            {
                var n = 0;
                for (var i = 0; i < count; i++)
                {
                    var a = R() * Mathf.PI * 2f;
                    var d = i == 0 ? 0f : Range(0.8f, spread);
                    var big = i == 0;
                    var s = big ? Range(1.5f, 2.3f) : Range(0.9f, 1.5f);
                    var scale = new Vector3(s * Range(0.9f, 1.4f), s * Range(0.75f, 1.1f), s * Range(0.9f, 1.3f));
                    Rock(o + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d), scale, R() < 0.35f, snowy);
                    n++;
                }

                return n;
            }

            private void Rock(Vector3 p, Vector3 scale, bool dark, bool snowSubmesh)
            {
                var g = _model.SampleHeight(p.x, p.z);
                var rot = Quaternion.Euler(Range(-8f, 8f), R() * 360f, Range(-8f, 8f));
                var m = Matrix4x4.TRS(new Vector3(p.x, g + 0.25f * scale.y, p.z) - _curOrigin, rot, scale);
                var map = snowSubmesh ? new[] { SubSnow, SubSnow } : new[] { dark ? SubRockDark : SubRock, SubMoss };
                _cur.AppendMapped(RockFactory.GetSplitVariant(_rng.Next(RockFactory.VariantCount)), m, map);
            }

            private int Drifts(Vector3 o, int count, float spread)
            {
                for (var i = 0; i < count; i++)
                {
                    var a = R() * Mathf.PI * 2f;
                    var d = i == 0 ? 0f : Range(1.5f, spread);
                    var p = o + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                    var g = _model.SampleHeight(p.x, p.z);
                    var scale = new Vector3(Range(1.9f, 3.3f), Range(0.75f, 1.1f), Range(1.7f, 2.9f));
                    var m = Matrix4x4.TRS(new Vector3(p.x, g + 0.2f * scale.y, p.z) - _curOrigin, Quaternion.Euler(0f, R() * 360f, 0f), scale);
                    _cur.AppendMapped(RockFactory.GetSplitVariant(_rng.Next(RockFactory.VariantCount)), m, new[] { SubSnow, SubSnow });
                }

                return count;
            }

            private void Cairn(Vector3 o)
            {
                var y = _model.SampleHeight(o.x, o.z) - 0.1f;
                var s = Range(0.95f, 1.2f);
                for (var i = 0; i < 5; i++)
                {
                    var scale = new Vector3(s * Range(1.0f, 1.2f), s * 0.7f, s * Range(1.0f, 1.2f));
                    var off = new Vector3(Range(-0.12f, 0.12f), 0f, Range(-0.12f, 0.12f)) * (i == 0 ? 0f : 1f);
                    var m = Matrix4x4.TRS(new Vector3(o.x, y + 0.3f * scale.y, o.z) + off - _curOrigin,
                        Quaternion.Euler(Range(-6f, 6f), R() * 360f, Range(-6f, 6f)), scale);
                    _cur.AppendMapped(RockFactory.GetSplitVariant(_rng.Next(RockFactory.VariantCount)), m, new[] { i % 2 == 0 ? SubRock : SubRockDark, SubRock });
                    y += scale.y * 0.58f;
                    s *= 0.8f;
                }

                // Taban çevresinde iki dağınık taş.
                for (var i = 0; i < 2; i++)
                {
                    var a = R() * Mathf.PI * 2f;
                    Rock(o + new Vector3(Mathf.Cos(a) * 1.5f, 0f, Mathf.Sin(a) * 1.5f), Vector3.one * Range(0.8f, 1.0f), true, false);
                }
            }

            private int Copse(Vector3 o, TreeSet set)
            {
                var frozen = set == TreeSet.Frozen;
                var n = frozen ? 4 + _rng.Next(4) : 3 + _rng.Next(4);
                var placed = new List<Vector2>(n);
                var count = 0;
                for (var i = 0; i < n * 3 && count < n; i++)
                {
                    var a = R() * Mathf.PI * 2f;
                    var d = Mathf.Sqrt(R()) * (frozen ? 8f : 6f);
                    var x = o.x + Mathf.Cos(a) * d;
                    var z = o.z + Mathf.Sin(a) * d;
                    var tooClose = false;
                    for (var j = 0; j < placed.Count; j++)
                    {
                        if ((placed[j] - new Vector2(x, z)).sqrMagnitude < 2.6f * 2.6f)
                        {
                            tooClose = true;
                            break;
                        }
                    }

                    if (tooClose || !TreeOk(x, z))
                        continue;
                    placed.Add(new Vector2(x, z));
                    AddTree(frozen ? TreeKind.Dead : PickTree(), x, z, frozen);
                    count++;
                }

                if (!frozen)
                {
                    for (var i = 0; i < 2; i++)
                    {
                        var a = R() * Mathf.PI * 2f;
                        var x = o.x + Mathf.Cos(a) * Range(3f, 7f);
                        var z = o.z + Mathf.Sin(a) * Range(3f, 7f);
                        if (TreeOk(x, z))
                        {
                            AddTree(TreeKind.Bush, x, z, false);
                            count++;
                        }
                    }

                    // Gövde dibinde diz boyu kaya (siper).
                    Rock(o + new Vector3(Range(-2f, 2f), 0f, Range(-2f, 2f)), Vector3.one * Range(0.95f, 1.3f), false, false);
                    count++;
                }

                return count;
            }

            private bool TreeOk(float x, float z)
            {
                return _model.SampleSlope(x, z) < 34f && _model.SampleHeight(x, z) > _model.Layout.WaterLevel + 1f
                       && _model.IsClearOfFeatures(x, z, 3f, 3f, 1.1f);
            }

            private TreeKind PickTree()
            {
                var total = 0f;
                for (var i = 0; i < _p.TreeWeights.Length; i++)
                    total += _p.TreeWeights[i];
                var roll = R() * total;
                for (var i = 0; i < _p.TreeWeights.Length; i++)
                {
                    roll -= _p.TreeWeights[i];
                    if (roll <= 0f)
                        return _p.Trees[i];
                }

                return _p.Trees[0];
            }

            private void AddTree(TreeKind kind, float x, float z, bool frosty)
            {
                var v = Range(0.85f, 1.0f);
                var tint = frosty ? new Color(v * 0.95f, v, Mathf.Min(1f, v * 1.05f), 1f) : new Color(v, v, v * 0.96f, 1f);
                var bush = kind == TreeKind.Bush;
                _trees.Add(new TreeInstance
                {
                    prototypeIndex = (int)kind,
                    // Dünya konumu; AppendTrees normalleştirir (y = yükseklik m).
                    position = new Vector3(x, _model.SampleHeight(x, z), z),
                    heightScale = bush ? Range(1.0f, 1.5f) : Range(0.85f, 1.25f),
                    widthScale = bush ? Range(1.1f, 1.6f) : Range(0.9f, 1.2f),
                    rotation = R() * Mathf.PI * 2f,
                    color = tint,
                    lightmapColor = Color.white
                });
            }

            private int BushLine(Vector3 o, Quaternion q, bool filler)
            {
                var length = filler ? Range(8f, 12f) : Range(14f, 22f);
                var n = 0;
                var sway = R() * 6f;
                for (var t = -length * 0.5f; t < length * 0.5f; t += Range(2.0f, 3.0f))
                {
                    var local = new Vector3(Mathf.Sin(t * 0.3f + sway) * 1.2f, 0f, t);
                    var p = o + q * local;
                    if (!TreeOk(p.x, p.z))
                        continue;
                    AddTree(TreeKind.Bush, p.x, p.z, false);
                    n++;
                }

                if (!filler)
                {
                    // Çalının arkasında iki diz boyu taş: gerçek siper.
                    for (var i = 0; i < 2; i++)
                        Rock(o + q * new Vector3(Range(1.5f, 2.2f), 0f, Range(-length * 0.25f, length * 0.25f)), Vector3.one * Range(0.95f, 1.25f), false, false);
                    n += 2;
                }

                return Mathf.Max(1, n);
            }

            private void WallCorner(Vector3 o, Quaternion q)
            {
                var armA = 5 + _rng.Next(4);
                var armB = 4 + _rng.Next(3);
                Arm(o, q, armA, 1);
                Arm(o, q, armB, 0);
                for (var i = 0; i < 3; i++)
                    Rock(o + q * new Vector3(Range(1.5f, 3.5f), 0f, Range(1.5f, 3.5f)), Vector3.one * Range(0.7f, 1.0f), true, false);
            }

            private void Arm(Vector3 o, Quaternion q, int blocks, int axis)
            {
                var broken = false;
                for (var i = 0; i < blocks; i++)
                {
                    if (i > blocks / 2 && R() < 0.2f)
                        broken = true;
                    if (broken && R() < 0.5f)
                        continue;
                    var tall = Mathf.Lerp(1.55f, 0.7f, i / (float)blocks) * Range(0.85f, 1.1f);
                    var along = i * 1.3f;
                    var lx = axis == 1 ? along : 0f;
                    var lz = axis == 1 ? 0f : along;
                    Block(o, q, lx, lz, axis == 1 ? 1.3f : 0.55f, tall, axis == 1 ? 0.55f : 1.3f, Range(-3f, 3f), SubStone, 0.3f);
                }
            }

            private void Hut(Vector3 o, Quaternion q)
            {
                const float w = 4.2f, d = 3.4f, t = 0.45f, hgt = 2.2f;
                Block(o, q, 0f, -d * 0.5f, w, hgt, t, 0f, SubStone, 0.4f);
                Block(o, q, -w * 0.5f, 0f, t, hgt, d, 0f, SubStone, 0.4f);
                Block(o, q, w * 0.5f, 0f, t, hgt, d, 0f, SubStone, 0.4f);
                // Ön duvar: 1.4 m kapı boşluğu.
                var door = Range(-0.6f, 0.6f);
                var leftEnd = door - 0.7f;
                var rightStart = door + 0.7f;
                var leftW = leftEnd - (-w * 0.5f);
                var rightW = w * 0.5f - rightStart;
                Block(o, q, (-w * 0.5f + leftEnd) * 0.5f, d * 0.5f, leftW, hgt, t, 0f, SubStone, 0.4f);
                Block(o, q, (rightStart + w * 0.5f) * 0.5f, d * 0.5f, rightW, hgt, t, 0f, SubStone, 0.4f);
                // Lento + çatı.
                Free(o, q, door, hgt - 0.05f, d * 0.5f, new Vector3(1.6f, 0.25f, t), 0f, 0f, SubWood);
                Free(o, q, 0f, hgt + 0.25f, 0f, new Vector3(w + 0.9f, 0.18f, d + 0.9f), 0f, Range(-5f, 5f), SubWood);
                Block(o, q, w * 0.5f + 1.1f, -0.6f, 0.9f, 0.5f, 0.9f, 20f, SubWood, 0.1f); // odun/sandık
                Rock(o + q * new Vector3(-w * 0.5f - 1.4f, 0f, 1f), Vector3.one * 1.1f, false, false);
            }

            private void Pen(Vector3 o, Quaternion q)
            {
                const float w = 9f, d = 6f, t = 0.5f, hgt = 1.05f;
                Block(o, q, 0f, -d * 0.5f, w, hgt, t, 0f, SubStone, 0.5f);
                Block(o, q, -w * 0.5f, 0f, t, hgt, d, 0f, SubStone, 0.5f);
                Block(o, q, w * 0.5f, 0f, t, hgt, d, 0f, SubStone, 0.5f);
                const float gate = 2.4f;
                var gx = Range(-1.5f, 1.5f);
                var lw = (gx - gate * 0.5f) + w * 0.5f;
                var rw = w * 0.5f - (gx + gate * 0.5f);
                Block(o, q, (-w * 0.5f + gx - gate * 0.5f) * 0.5f, d * 0.5f, lw, hgt, t, 0f, SubStone, 0.5f);
                Block(o, q, (gx + gate * 0.5f + w * 0.5f) * 0.5f, d * 0.5f, rw, hgt, t, 0f, SubStone, 0.5f);
                // İçeride yem teknesi ve balya.
                Block(o, q, -1.5f, -1.2f, 2.2f, 0.55f, 0.55f, 0f, SubWood, 0.1f);
                var hay = o + q * new Vector3(2.2f, 0f, 0.6f);
                Cyl(new Vector3(hay.x, _model.SampleHeight(hay.x, hay.z) + 0.5f, hay.z), 0.5f, 1.1f, q * Quaternion.Euler(0f, 0f, 90f), SubHay);
            }

            private void Haystack(Vector3 o, Quaternion q)
            {
                var g = _model.SampleHeight(o.x, o.z);
                Cyl(new Vector3(o.x, g + 1.15f, o.z), 1.15f, 2.5f, Quaternion.identity, SubHay);
                var p = o + q * new Vector3(2.3f, 0f, 0.5f);
                Cyl(new Vector3(p.x, _model.SampleHeight(p.x, p.z) + 0.85f, p.z), 0.8f, 1.8f, Quaternion.identity, SubHay);
                var b1 = o + q * new Vector3(-0.8f, 0f, 2.3f);
                Cyl(new Vector3(b1.x, _model.SampleHeight(b1.x, b1.z) + 0.5f, b1.z), 0.5f, 1.1f, q * Quaternion.Euler(0f, 0f, 90f), SubHay);
                var b2 = o + q * new Vector3(-2.2f, 0f, 1.0f);
                Cyl(new Vector3(b2.x, _model.SampleHeight(b2.x, b2.z) + 0.5f, b2.z), 0.5f, 1.1f, q * Quaternion.Euler(0f, 30f, 90f), SubHay);
            }

            private void Log(Vector3 o, Quaternion q)
            {
                LogAt(o, q, Range(4.5f, 6.5f), Range(0.42f, 0.55f));
                if (R() < 0.5f)
                    LogAt(o + q * new Vector3(0.6f, 0f, 1.3f), q * Quaternion.Euler(0f, Range(-35f, 35f), 0f), Range(2.5f, 3.5f), 0.32f);
            }

            private void LogAt(Vector3 o, Quaternion q, float length, float radius)
            {
                var a = o + q * new Vector3(-length * 0.5f, 0f, 0f);
                var b = o + q * new Vector3(length * 0.5f, 0f, 0f);
                var ha = _model.SampleHeight(a.x, a.z);
                var hb = _model.SampleHeight(b.x, b.z);
                var pitch = Mathf.Clamp(Mathf.Atan2(hb - ha, length) * Mathf.Rad2Deg, -14f, 14f);
                var center = new Vector3(o.x, Mathf.Max(ha, hb) + radius - 0.15f, o.z);
                Cyl(center, radius, length, q * Quaternion.Euler(0f, 0f, 90f + pitch), SubBark);
            }

            private void Foxhole(Vector3 o, Quaternion q)
            {
                const float radius = 1.9f;
                const float step = 0.6f / radius;
                for (var layer = 0; layer < 2; layer++)
                {
                    var start = 0.9f + (layer == 1 ? step * 0.5f : 0f);
                    for (var a = start; a < Mathf.PI * 2f - 0.9f; a += step)
                    {
                        var lx = Mathf.Sin(a) * radius;
                        var lz = Mathf.Cos(a) * radius;
                        var p = o + q * new Vector3(lx, 0f, lz);
                        var g = _model.SampleHeight(p.x, p.z);
                        var rot = q * Quaternion.Euler(0f, a * Mathf.Rad2Deg + 90f, 0f);
                        Box(new Vector3(p.x, g + 0.2f + layer * 0.4f, p.z), new Vector3(0.6f, 0.45f, 0.4f), rot, SubSandbag);
                    }
                }
            }

            private void Cart(Vector3 o, Quaternion q)
            {
                Free(o, q, 0f, 0.72f, 0f, new Vector3(2.4f, 0.12f, 1.2f), 0f, 0f, SubWood);
                Free(o, q, 0f, 0.95f, 0.58f, new Vector3(2.4f, 0.35f, 0.08f), 0f, 0f, SubWood);
                Free(o, q, 0f, 0.95f, -0.58f, new Vector3(2.4f, 0.35f, 0.08f), 0f, 0f, SubWood);
                Free(o, q, -1.2f, 0.95f, 0f, new Vector3(0.08f, 0.35f, 1.2f), 0f, 0f, SubWood);
                for (var s = -1; s <= 1; s += 2)
                {
                    var p = o + q * new Vector3(-0.2f, 0f, 0.72f * s);
                    Cyl(new Vector3(p.x, _model.SampleHeight(p.x, p.z) + 0.62f, p.z), 0.62f, 0.1f, q * Quaternion.Euler(90f, 0f, 0f), SubWood);
                    var shaft = o + q * new Vector3(1.9f, 0f, 0.45f * s);
                    Box(new Vector3(shaft.x, _model.SampleHeight(shaft.x, shaft.z) + 0.38f, shaft.z), new Vector3(2.1f, 0.08f, 0.08f),
                        q * Quaternion.Euler(0f, 0f, -14f), SubWood);
                }

                var hay = o + q * new Vector3(0.1f, 0f, 0f);
                Cyl(new Vector3(hay.x, _model.SampleHeight(hay.x, hay.z) + 1.0f, hay.z), 0.4f, 1.3f, q * Quaternion.Euler(0f, 0f, 90f), SubHay);
            }

            // ------------------------------------------------------------ Mesh ilkelleri

            private void Begin(Vector3 anchor)
            {
                var cx = Mathf.Clamp(Mathf.FloorToInt((anchor.x + _half) / ChunkSize), 0, _chunksPerSide - 1);
                var cz = Mathf.Clamp(Mathf.FloorToInt((anchor.z + _half) / ChunkSize), 0, _chunksPerSide - 1);
                var key = cz * _chunksPerSide + cx;
                if (!_builders.TryGetValue(key, out _cur))
                {
                    _cur = new MeshBuilder(SubCount);
                    _builders[key] = _cur;
                }

                _curOrigin = ChunkOrigin(cx, cz);
            }

            private Vector3 ChunkOrigin(int cx, int cz)
            {
                return new Vector3(-_half + (cx + 0.5f) * ChunkSize, 0f, -_half + (cz + 0.5f) * ChunkSize);
            }

            /// <summary>Zemine oturan blok: yerel (lx,lz) konumu, görünür yükseklik h, zemine gömülü sink.</summary>
            private void Block(Vector3 o, Quaternion q, float lx, float lz, float w, float h, float d, float localYaw, int sub, float sink)
            {
                var p = o + q * new Vector3(lx, 0f, lz);
                var g = _model.SampleHeight(p.x, p.z);
                Box(new Vector3(p.x, g + (h - sink) * 0.5f, p.z), new Vector3(w, h + sink, d), q * Quaternion.Euler(0f, localYaw, 0f), sub);
            }

            /// <summary>Yerel konumda (y = merkez zemininden yükseklik) serbest kutu.</summary>
            private void Free(Vector3 o, Quaternion q, float lx, float ly, float lz, Vector3 size, float pitch, float roll, int sub)
            {
                var p = o + q * new Vector3(lx, 0f, lz);
                Box(new Vector3(p.x, o.y + ly, p.z), size, q * Quaternion.Euler(pitch, 0f, roll), sub);
            }

            private void Cyl(Vector3 center, float radius, float height, Quaternion rot, int sub)
            {
                _cur.Append(StructureKit.UnitCylinder, Matrix4x4.TRS(center - _curOrigin, rot, new Vector3(radius * 2f, height, radius * 2f)), sub);
            }

            private static readonly Vector3[] Axes = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };

            /// <summary>Dünya konumlu, dönük kutu (6 yüz, düz gölgeli) — parçanın mesh'ine eklenir.</summary>
            private void Box(Vector3 center, Vector3 size, Quaternion rot, int sub)
            {
                var half = size * 0.5f;
                var c = center - _curOrigin;
                for (var f = 0; f < Axes.Length; f++)
                {
                    var d = Axes[f];
                    Vector3 u, v;
                    if (Mathf.Abs(d.x) > 0.5f)
                    {
                        u = Vector3.up;
                        v = Vector3.forward;
                    }
                    else if (Mathf.Abs(d.y) > 0.5f)
                    {
                        u = Vector3.right;
                        v = Vector3.forward;
                    }
                    else
                    {
                        u = Vector3.right;
                        v = Vector3.up;
                    }

                    var a = Vector3.Scale(d, half) + Vector3.Scale(-u, half) + Vector3.Scale(-v, half);
                    var b = Vector3.Scale(d, half) + Vector3.Scale(-u, half) + Vector3.Scale(v, half);
                    var cc = Vector3.Scale(d, half) + Vector3.Scale(u, half) + Vector3.Scale(v, half);
                    var dd = Vector3.Scale(d, half) + Vector3.Scale(u, half) + Vector3.Scale(-v, half);
                    if (Vector3.Dot(Vector3.Cross(b - a, cc - a), d) < 0f)
                    {
                        var tmp = b;
                        b = dd;
                        dd = tmp;
                    }

                    _cur.AddFlatQuad(sub, c + rot * a, c + rot * b, c + rot * cc, c + rot * dd, 0.5f);
                }
            }

            // ------------------------------------------------------------ Yardımcılar

            private float R() => (float)_rng.NextDouble();

            private float Range(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

            private static float Smooth(float a, float b, float x)
            {
                var t = Mathf.Clamp01((x - a) / Mathf.Max(0.0001f, b - a));
                return t * t * (3f - 2f * t);
            }
        }
    }
}
