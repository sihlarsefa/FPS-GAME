using System;
using System.Collections.Generic;
using Project.Infrastructure.Rendering;
using Project.Infrastructure.Vfx;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Infrastructure.World
{
    /// <summary>Yapı parçası çarpışma türü.</summary>
    public enum StructureCollider
    {
        /// <summary>Sadece görsel (çarpışmasız).</summary>
        None = 0,
        /// <summary>Kutu çarpıştırıcı (eksene hizalı ise malzeme nesnesine, eğik ise alt nesneye eklenir).</summary>
        Box = 1,
        /// <summary>Şeklin kendisinden konveks MeshCollider (kubbe, silindir, prizma).</summary>
        Convex = 2
    }

    /// <summary>
    /// Duvar boşluğu (kapı / pencere / mazgal). Değerler duvar başlangıcından (Center) ve duvar tabanından (Bottom) ölçülür.
    /// Üst kenarı duvar yüksekliğini aşan boşluk duvarı tepeye kadar keser (siper çentiği, yıkık gedik).
    /// </summary>
    [Serializable]
    public struct WallOpening
    {
        public float Center;
        public float Width;
        public float Bottom;
        public float Height;

        public WallOpening(float center, float width, float bottom, float height)
        {
            Center = center;
            Width = width;
            Bottom = bottom;
            Height = height;
        }

        public float Start => Center - Width * 0.5f;
        public float End => Center + Width * 0.5f;
        public float Top => Bottom + Height;

        /// <summary>Standart kapı boşluğu (1.4 × 2.3 m).</summary>
        public static WallOpening Door(float center, float width = StructureKit.DoorWidth, float height = StructureKit.DoorHeight)
            => new WallOpening(center, width, 0f, height);

        public static WallOpening Window(float center, float width, float sill, float height)
            => new WallOpening(center, width, sill, height);
    }

    /// <summary>
    /// Düşük seviyeli yapı yardımcıları: tek parça kutu/silindir nesneleri, malzeme → yüzey sınıfı, statik işaretleme.
    /// Birleştirilmiş (az nesneli) yapılar için <see cref="StructureBuilder"/> kullanın.
    /// Tüm parçalar Default katmanında, MaterialLibrary malzemeleriyle üretilir.
    /// </summary>
    public static class StructureKit
    {
        public const float DoorWidth = 1.4f;
        public const float DoorHeight = 2.3f;
        /// <summary>Rampa merdivenlerin azami eğimi (derece). NavMesh ajan eğimi 40°.</summary>
        public const float MaxStairAngle = 33f;
        /// <summary>Tercih edilen merdiven eğimi (derece).</summary>
        public const float PreferredStairAngle = 31f;
        public const float FoundationDepth = 2f;

        private static Mesh _unitCube;
        private static Mesh _unitCylinder;
        private static readonly PhysicsMaterial[] SurfaceMaterials = new PhysicsMaterial[8];

        /// <summary>1 m küp (merkezde, yüz başına 0..1 UV).</summary>
        public static Mesh UnitCube
        {
            get
            {
                if (_unitCube == null)
                {
                    var b = new StructureBuilder { UvScale = 1f };
                    b.Box(Vector3.zero, Vector3.one, MaterialId.Gray, StructureCollider.None);
                    _unitCube = b.CreateMesh(MaterialId.Gray, "BirimKup");
                }

                return _unitCube;
            }
        }

        /// <summary>Yarıçap 0.5, yükseklik 1 silindir (merkezde, Y ekseni).</summary>
        public static Mesh UnitCylinder
        {
            get
            {
                if (_unitCylinder == null)
                {
                    var b = new StructureBuilder { UvScale = 1f };
                    b.Cylinder(Vector3.zero, Quaternion.identity, 0.5f, 0.5f, 1f, 16, MaterialId.Gray, StructureCollider.None);
                    _unitCylinder = b.CreateMesh(MaterialId.Gray, "BirimSilindir");
                }

                return _unitCylinder;
            }
        }

        /// <summary>Tek kutu nesnesi (ölçek = boyut). Çarpışma isteğe bağlı.</summary>
        public static GameObject CreateBox(Transform parent, string name, Vector3 localPosition, Vector3 size, Quaternion localRotation,
            MaterialId material, bool collider = true)
        {
            var go = CreateRendererObject(parent, name, localPosition, localRotation, Abs(size), UnitCube, material);
            if (collider)
            {
                var box = go.AddComponent<BoxCollider>();
                box.sharedMaterial = PhysicsMaterialFor(SurfaceOf(material));
            }

            return go;
        }

        /// <summary>Dikey silindir nesnesi (localPosition = taban merkezi değil, gövde merkezi).</summary>
        public static GameObject CreateCylinder(Transform parent, string name, Vector3 localPosition, float radius, float height,
            MaterialId material, bool collider = true)
        {
            radius = Mathf.Max(0.005f, radius);
            height = Mathf.Max(0.005f, height);
            var go = CreateRendererObject(parent, name, localPosition, Quaternion.identity, new Vector3(radius * 2f, height, radius * 2f),
                UnitCylinder, material);
            if (collider)
            {
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = UnitCylinder;
                mc.convex = true;
                mc.sharedMaterial = PhysicsMaterialFor(SurfaceOf(material));
            }

            return go;
        }

        /// <summary>Boş kap nesnesi (Default katman).</summary>
        public static GameObject CreateGroup(Transform parent, string name, Vector3 localPosition, Quaternion localRotation)
        {
            var go = new GameObject(string.IsNullOrEmpty(name) ? "Grup" : name);
            go.layer = GameLayers.Default;
            var t = go.transform;
            if (parent != null)
                t.SetParent(parent, false);
            t.localPosition = localPosition;
            t.localRotation = localRotation;
            return go;
        }

        /// <summary>
        /// Nesne ağacını statik işaretler (editörde static batching / NavMesh / ışık için). Çalışma zamanında zararsızdır.
        /// </summary>
        public static void MarkStatic(GameObject root)
        {
            if (root == null)
                return;

            root.isStatic = true;
            var t = root.transform;
            for (var i = 0; i < t.childCount; i++)
                MarkStatic(t.GetChild(i).gameObject);
        }

        /// <summary>Malzemenin mermi isabet yüzeyi sınıfı (efekt seçimi için).</summary>
        public static SurfaceKind SurfaceOf(MaterialId material)
        {
            switch (material)
            {
                case MaterialId.Concrete:
                case MaterialId.ConcreteDark:
                case MaterialId.Plaster:
                case MaterialId.PlasterWarm:
                case MaterialId.Stone:
                case MaterialId.StoneDark:
                case MaterialId.Brick:
                case MaterialId.RoofTile:
                case MaterialId.Rock:
                case MaterialId.RockDark:
                case MaterialId.Asphalt:
                case MaterialId.Glass:
                case MaterialId.White:
                case MaterialId.Gray:
                    return SurfaceKind.Concrete;
                case MaterialId.RoofMetal:
                case MaterialId.MetalPanel:
                case MaterialId.MetalDark:
                case MaterialId.Rust:
                case MaterialId.MosqueDome:
                case MaterialId.VehicleOlive:
                case MaterialId.VehicleTan:
                case MaterialId.VehicleDark:
                case MaterialId.GunMetal:
                    return SurfaceKind.Metal;
                case MaterialId.Wood:
                case MaterialId.WoodDark:
                case MaterialId.Bark:
                case MaterialId.DeadWood:
                case MaterialId.GunWood:
                    return SurfaceKind.Wood;
                case MaterialId.Dirt:
                case MaterialId.Mud:
                case MaterialId.Grass:
                case MaterialId.DryGrass:
                case MaterialId.Sand:
                case MaterialId.Gravel:
                case MaterialId.Sandbag:
                case MaterialId.Hesco:
                case MaterialId.Snow:
                    return SurfaceKind.Dirt;
                case MaterialId.Foliage:
                case MaterialId.FoliageDark:
                case MaterialId.PineNeedles:
                case MaterialId.Hay:
                case MaterialId.CamoNet:
                case MaterialId.TentCanvas:
                    return SurfaceKind.Foliage;
                case MaterialId.Water:
                    return SurfaceKind.Water;
                default:
                    return SurfaceKind.Default;
            }
        }

        /// <summary>Yüzey sınıfı başına paylaşılan fizik malzemesi (adı SurfaceKind adıdır: "Concrete", "Metal"...).</summary>
        public static PhysicsMaterial PhysicsMaterialFor(SurfaceKind surface)
        {
            var index = (int)surface;
            if (index < 0 || index >= SurfaceMaterials.Length)
                index = 0;

            var m = SurfaceMaterials[index];
            if (m == null)
            {
                m = new PhysicsMaterial(((SurfaceKind)index).ToString())
                {
                    dynamicFriction = 0.6f,
                    staticFriction = 0.6f,
                    bounciness = 0f,
                    frictionCombine = PhysicsMaterialCombine.Average,
                    bounceCombine = PhysicsMaterialCombine.Average
                };
                SurfaceMaterials[index] = m;
            }

            return m;
        }

        /// <summary>Verilen yükseklik için tercih edilen eğimde rampa uzunluğu (≤ MaxStairAngle garanti).</summary>
        public static float StairRun(float rise, float angleDegrees = PreferredStairAngle)
        {
            angleDegrees = Mathf.Clamp(angleDegrees, 10f, MaxStairAngle);
            return Mathf.Max(0.3f, rise) / Mathf.Tan(angleDegrees * Mathf.Deg2Rad);
        }

        internal static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        private static GameObject CreateRendererObject(Transform parent, string name, Vector3 localPosition, Quaternion localRotation,
            Vector3 scale, Mesh mesh, MaterialId material)
        {
            var go = CreateGroup(parent, name, localPosition, localRotation);
            go.transform.localScale = scale;
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = MaterialLibrary.Get(material);
            mr.shadowCastingMode = ShadowCastingMode.On;
            mr.receiveShadows = true;
            return go;
        }
    }

    /// <summary>
    /// Birleştirilmiş yapı üreticisi. Parçalar (kutu, duvar, döşeme, merdiven, silindir, kubbe, prizma) malzemeye göre gruplanır;
    /// <see cref="Build"/> her malzeme için TEK nesne (MeshFilter + MeshRenderer + o malzemenin çarpıştırıcıları) üretir.
    /// Koordinatlar yapı yerel uzayındadır (kök nesneye göre). Eksene hizalı kutular aynı nesnede BoxCollider olur,
    /// eğik kutular "Egim" alt nesnelerine, eğri şekiller konveks MeshCollider'a dönüşür. Her çarpıştırıcıya yüzey
    /// sınıfı adında PhysicsMaterial atanır.
    /// </summary>
    public sealed class StructureBuilder
    {
        private const float Eps = 0.002f;

        private sealed class Bucket
        {
            public readonly MaterialId Material;
            public readonly List<Vector3> Vertices = new List<Vector3>(256);
            public readonly List<Vector3> Normals = new List<Vector3>(256);
            public readonly List<Vector2> Uvs = new List<Vector2>(256);
            public readonly List<int> Triangles = new List<int>(384);
            public readonly List<Bounds> Boxes = new List<Bounds>();
            public readonly List<RotatedBox> Rotated = new List<RotatedBox>();
            public readonly List<Mesh> Convex = new List<Mesh>();

            public Bucket(MaterialId material)
            {
                Material = material;
            }
        }

        private struct RotatedBox
        {
            public Vector3 Center;
            public Vector3 Size;
            public Quaternion Rotation;
        }

        private struct Column
        {
            public float A;
            public float B;
            public int Start;
            public int Count;
        }

        private struct RuinChunk
        {
            public float A;
            public float B;
            public float Top;
        }

        private readonly List<Bucket> _buckets = new List<Bucket>();
        private readonly Dictionary<int, Bucket> _lookup = new Dictionary<int, Bucket>();
        private Vector3 _min;
        private Vector3 _max;
        private bool _hasBounds;

        // Tekrar kullanılan geçici listeler
        private readonly List<float> _breaks = new List<float>(32);
        private readonly List<Vector2> _intervals = new List<Vector2>(64);
        private readonly List<Vector2> _scratch = new List<Vector2>(16);
        private readonly List<Column> _columns = new List<Column>(32);
        private readonly List<RuinChunk> _chunks = new List<RuinChunk>(32);
        private readonly List<float> _xs = new List<float>(16);
        private readonly List<float> _zs = new List<float>(16);

        /// <summary>Doku ölçeği (metre başına UV). 0.5 → doku 2 m'de bir tekrarlar.</summary>
        public float UvScale { get; set; } = 0.5f;

        public bool HasGeometry => _hasBounds;

        /// <summary>Tüm parçaların yerel sınır kutusu.</summary>
        public Bounds LocalBounds
        {
            get
            {
                if (!_hasBounds)
                    return new Bounds(Vector3.zero, Vector3.zero);
                var b = new Bounds();
                b.SetMinMax(_min, _max);
                return b;
            }
        }

        public void Clear()
        {
            _buckets.Clear();
            _lookup.Clear();
            _hasBounds = false;
        }

        /// <summary>Sınır kutusunu elle genişletir (ör. görselsiz alanlar).</summary>
        public void Encapsulate(Vector3 point)
        {
            if (!_hasBounds)
            {
                _min = point;
                _max = point;
                _hasBounds = true;
                return;
            }

            _min = Vector3.Min(_min, point);
            _max = Vector3.Max(_max, point);
        }

        // ================================================================== Kutular

        public void Box(Vector3 center, Vector3 size, MaterialId material, StructureCollider collider = StructureCollider.Box)
            => Box(center, size, Quaternion.identity, material, collider);

        /// <summary>Kutu parça. Y ekseni etrafında 90° katları eksene hizalı sayılır (BoxCollider doğrudan eklenir).</summary>
        public void Box(Vector3 center, Vector3 size, Quaternion rotation, MaterialId material, StructureCollider collider = StructureCollider.Box)
        {
            size = StructureKit.Abs(size);
            if (size.x < 0.001f || size.y < 0.001f || size.z < 0.001f)
                return;

            if (TrySnapAxisAligned(rotation, ref size))
                rotation = Quaternion.identity;

            var bucket = GetBucket(material);
            var v0 = bucket.Vertices.Count;
            var t0 = bucket.Triangles.Count;
            EmitBox(bucket, center, size, rotation);

            switch (collider)
            {
                case StructureCollider.Box:
                    AddBoxCollider(bucket, center, size, rotation);
                    break;
                case StructureCollider.Convex:
                    AddConvexFromRange(bucket, v0, t0);
                    break;
            }
        }

        /// <summary>A ve B noktaları arasında (merkez çizgisi) kiriş/boru gibi kutu.</summary>
        public void Beam(Vector3 a, Vector3 b, float width, float height, MaterialId material, StructureCollider collider = StructureCollider.None)
        {
            var d = b - a;
            var len = d.magnitude;
            if (len < 0.001f)
                return;

            var up = Mathf.Abs(Vector3.Dot(d / len, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;
            var rot = Quaternion.LookRotation(d / len, up);
            Box((a + b) * 0.5f, new Vector3(width, height, len), rot, material, collider);
        }

        /// <summary>Sadece çarpıştırıcı kutu (görsel yok).</summary>
        public void ColliderBox(Vector3 center, Vector3 size, Quaternion rotation, MaterialId material)
        {
            size = StructureKit.Abs(size);
            if (size.x < 0.001f || size.y < 0.001f || size.z < 0.001f)
                return;
            if (TrySnapAxisAligned(rotation, ref size))
                rotation = Quaternion.identity;
            AddBoxCollider(GetBucket(material), center, size, rotation);
            Encapsulate(center + rotation * (size * 0.5f));
            Encapsulate(center - rotation * (size * 0.5f));
        }

        // ================================================================== Duvar

        /// <summary>
        /// Boşluklu duvar. start/end: duvar merkez çizgisinin XZ uçları (y yok sayılır). Boşluklar duvar başlangıcından ölçülür.
        /// ruinRandom verilirse duvar kırık yükseklikli parçalara ayrılır (ruinAmount 0..1).
        /// Duvar parçalara (kolonlara) ayrılır, aynı kesitli komşu kolonlar birleştirilir → az kutu.
        /// </summary>
        public void Wall(Vector3 start, Vector3 end, float baseY, float height, float thickness, MaterialId material,
            IReadOnlyList<WallOpening> openings = null, System.Random ruinRandom = null, float ruinAmount = 0f,
            StructureCollider collider = StructureCollider.Box)
        {
            var dir = end - start;
            dir.y = 0f;
            var length = dir.magnitude;
            if (length < 0.01f || height < 0.01f || thickness < 0.005f)
                return;

            dir /= length;
            var rotation = Quaternion.Euler(0f, Mathf.Atan2(-dir.z, dir.x) * Mathf.Rad2Deg, 0f);
            var origin = new Vector3(start.x, baseY, start.z);

            // Kırık parçalar
            _chunks.Clear();
            var ruined = ruinRandom != null && ruinAmount > 0.001f;
            if (ruined)
            {
                var u = 0f;
                while (u < length - 0.01f)
                {
                    var w = Mathf.Lerp(0.6f, 1.6f, (float)ruinRandom.NextDouble());
                    var b = Mathf.Min(length, u + w);
                    if (length - b < 0.4f)
                        b = length;
                    var mid = (u + b) * 0.5f / length;
                    var edgeBias = 1f - Mathf.Abs(mid - 0.5f) * 2f; // uçlarda (köşeler) daha sağlam
                    var keep = Mathf.Lerp(1f, Mathf.Lerp(0.15f, 0.95f, (float)ruinRandom.NextDouble()), ruinAmount * (0.45f + 0.55f * edgeBias));
                    if (ruinRandom.NextDouble() < ruinAmount * 0.22f * edgeBias)
                        keep = Mathf.Lerp(0f, 0.3f, (float)ruinRandom.NextDouble());
                    _chunks.Add(new RuinChunk { A = u, B = b, Top = Mathf.Clamp(keep, 0f, 1f) * height });
                    u = b;
                }
            }

            // Kesim noktaları
            _breaks.Clear();
            _breaks.Add(0f);
            _breaks.Add(length);
            if (openings != null)
            {
                for (var i = 0; i < openings.Count; i++)
                {
                    var o = openings[i];
                    if (o.Width <= 0.01f || o.Height <= 0.01f)
                        continue;
                    _breaks.Add(Mathf.Clamp(o.Start, 0f, length));
                    _breaks.Add(Mathf.Clamp(o.End, 0f, length));
                }
            }

            for (var i = 0; i < _chunks.Count; i++)
                _breaks.Add(_chunks[i].B);

            _breaks.Sort();

            // Kolonlar
            _columns.Clear();
            _intervals.Clear();
            var prev = _breaks[0];
            for (var i = 1; i < _breaks.Count; i++)
            {
                var cur = _breaks[i];
                if (cur - prev < 0.01f)
                    continue;

                var mid = (prev + cur) * 0.5f;
                var top = height;
                if (ruined)
                {
                    for (var c = 0; c < _chunks.Count; c++)
                    {
                        if (mid >= _chunks[c].A && mid <= _chunks[c].B)
                        {
                            top = _chunks[c].Top;
                            break;
                        }
                    }
                }

                _scratch.Clear();
                if (top > 0.02f)
                    _scratch.Add(new Vector2(0f, top));

                if (openings != null)
                {
                    for (var o = 0; o < openings.Count; o++)
                    {
                        var op = openings[o];
                        if (op.Width <= 0.01f || op.Height <= 0.01f)
                            continue;
                        if (op.Start <= mid && op.End >= mid)
                            Subtract(_scratch, op.Bottom, op.Top);
                    }
                }

                var col = new Column { A = prev, B = cur, Start = _intervals.Count, Count = 0 };
                for (var k = 0; k < _scratch.Count; k++)
                {
                    if (_scratch[k].y - _scratch[k].x > 0.02f)
                    {
                        _intervals.Add(_scratch[k]);
                        col.Count++;
                    }
                }

                // Aynı kesitli komşu kolonu genişlet
                if (_columns.Count > 0 && SameIntervals(_columns[_columns.Count - 1], col))
                {
                    var last = _columns[_columns.Count - 1];
                    last.B = cur;
                    _columns[_columns.Count - 1] = last;
                    _intervals.RemoveRange(col.Start, col.Count);
                }
                else
                {
                    _columns.Add(col);
                }

                prev = cur;
            }

            for (var i = 0; i < _columns.Count; i++)
            {
                var col = _columns[i];
                var w = col.B - col.A;
                for (var k = 0; k < col.Count; k++)
                {
                    var iv = _intervals[col.Start + k];
                    var local = new Vector3((col.A + col.B) * 0.5f, (iv.x + iv.y) * 0.5f, 0f);
                    Box(origin + rotation * local, new Vector3(w, iv.y - iv.x, thickness), rotation, material, collider);
                }
            }
        }

        private bool SameIntervals(Column a, Column b)
        {
            if (a.Count != b.Count)
                return false;
            for (var i = 0; i < a.Count; i++)
            {
                var x = _intervals[a.Start + i];
                var y = _intervals[b.Start + i];
                if (Mathf.Abs(x.x - y.x) > 0.005f || Mathf.Abs(x.y - y.y) > 0.005f)
                    return false;
            }

            return true;
        }

        private static void Subtract(List<Vector2> intervals, float a, float b)
        {
            for (var i = intervals.Count - 1; i >= 0; i--)
            {
                var iv = intervals[i];
                if (b <= iv.x || a >= iv.y)
                    continue;

                intervals.RemoveAt(i);
                if (iv.x < a)
                    intervals.Insert(i, new Vector2(iv.x, a));
                if (b < iv.y)
                    intervals.Insert(iv.x < a ? i + 1 : i, new Vector2(b, iv.y));
            }

            intervals.Sort((p, q) => p.x.CompareTo(q.x));
        }

        // ================================================================== Döşeme

        /// <summary>
        /// Yatay döşeme/tavan [minX..maxX]×[minZ..maxZ], üst yüzü topY. holes: plan dikdörtgenleri (Rect.x = X, Rect.y = Z).
        /// Delikli döşeme eksene hizalı şeritlere ayrılır.
        /// </summary>
        public void Slab(float minX, float minZ, float maxX, float maxZ, float topY, float thickness, MaterialId material,
            IReadOnlyList<Rect> holes = null, StructureCollider collider = StructureCollider.Box)
        {
            if (maxX - minX < 0.01f || maxZ - minZ < 0.01f || thickness < 0.005f)
                return;

            var cy = topY - thickness * 0.5f;
            if (holes == null || holes.Count == 0)
            {
                Box(new Vector3((minX + maxX) * 0.5f, cy, (minZ + maxZ) * 0.5f), new Vector3(maxX - minX, thickness, maxZ - minZ), material, collider);
                return;
            }

            _xs.Clear();
            _zs.Clear();
            _xs.Add(minX);
            _xs.Add(maxX);
            _zs.Add(minZ);
            _zs.Add(maxZ);
            for (var i = 0; i < holes.Count; i++)
            {
                var h = holes[i];
                _xs.Add(Mathf.Clamp(h.xMin, minX, maxX));
                _xs.Add(Mathf.Clamp(h.xMax, minX, maxX));
                _zs.Add(Mathf.Clamp(h.yMin, minZ, maxZ));
                _zs.Add(Mathf.Clamp(h.yMax, minZ, maxZ));
            }

            _xs.Sort();
            _zs.Sort();

            for (var j = 0; j + 1 < _zs.Count; j++)
            {
                var z0 = _zs[j];
                var z1 = _zs[j + 1];
                if (z1 - z0 < 0.01f)
                    continue;
                var zc = (z0 + z1) * 0.5f;

                var runStart = float.NaN;
                var runEnd = 0f;
                for (var i = 0; i + 1 < _xs.Count; i++)
                {
                    var x0 = _xs[i];
                    var x1 = _xs[i + 1];
                    if (x1 - x0 < 0.01f)
                        continue;
                    var xc = (x0 + x1) * 0.5f;
                    var solid = true;
                    for (var h = 0; h < holes.Count; h++)
                    {
                        if (holes[h].Contains(new Vector2(xc, zc)))
                        {
                            solid = false;
                            break;
                        }
                    }

                    if (solid)
                    {
                        if (float.IsNaN(runStart))
                            runStart = x0;
                        runEnd = x1;
                    }
                    else if (!float.IsNaN(runStart))
                    {
                        Box(new Vector3((runStart + runEnd) * 0.5f, cy, zc), new Vector3(runEnd - runStart, thickness, z1 - z0), material, collider);
                        runStart = float.NaN;
                    }
                }

                if (!float.IsNaN(runStart))
                    Box(new Vector3((runStart + runEnd) * 0.5f, cy, zc), new Vector3(runEnd - runStart, thickness, z1 - z0), material, collider);
            }
        }

        // ================================================================== Merdiven / rampa

        /// <summary>
        /// Yığma merdiven: görselde basamaklar (alttan dolu), çarpışmada tek eğik rampa + altını dolduran kutular.
        /// bottom: alt kenarın orta noktası (zemin seviyesi); yaw: çıkış yönü. extendBelow: görsel bloğun zemine gömülme derinliği.
        /// </summary>
        public void SolidStairs(Vector3 bottom, float yawDegrees, float width, float rise, float run, MaterialId material,
            float extendBelow = 0f, bool collider = true)
        {
            if (rise < 0.05f || run < 0.05f || width < 0.1f)
                return;

            var rot = Quaternion.Euler(0f, yawDegrees, 0f);
            var steps = Mathf.Max(2, Mathf.RoundToInt(rise / 0.19f));
            var h = rise / steps;
            var d = run / steps;
            for (var k = 0; k < steps; k++)
            {
                var top = (k + 1) * h;
                var local = new Vector3(0f, (top - extendBelow) * 0.5f, (k + 0.5f) * d);
                Box(bottom + rot * local, new Vector3(width, top + extendBelow, d), rot, material, StructureCollider.None);
            }

            if (!collider)
                return;

            RampCollider(bottom, yawDegrees, width, rise, run, material);

            // Bloğun alt kısmını dolduran kutular (rampa yüzeyinin altında kalır)
            for (var j = 1; j <= 2; j++)
            {
                var z0 = run * j / 3f;
                var top = rise * j / 3f - 0.03f;
                var bottomY = -Mathf.Max(0f, extendBelow);
                if (top - bottomY < 0.05f)
                    continue;
                var local = new Vector3(0f, (top + bottomY) * 0.5f, (z0 + run) * 0.5f);
                ColliderBox(bottom + rot * local, new Vector3(width, top - bottomY, run - z0), rot, material);
            }
        }

        /// <summary>
        /// Açık (ahşap/çelik) merdiven: iki yan kiriş, basamaklar, isteğe bağlı korkuluk; çarpışma ince eğik rampa.
        /// </summary>
        public void OpenStairs(Vector3 bottom, float yawDegrees, float width, float rise, float run, MaterialId treadMaterial,
            MaterialId frameMaterial, bool railings)
        {
            if (rise < 0.05f || run < 0.05f || width < 0.2f)
                return;

            var rot = Quaternion.Euler(0f, yawDegrees, 0f);
            var length = Mathf.Sqrt(rise * rise + run * run);
            var angle = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            var slopeRot = rot * Quaternion.Euler(-angle, 0f, 0f);
            var slopeNormal = slopeRot * Vector3.up;
            var mid = bottom + rot * new Vector3(0f, rise * 0.5f, run * 0.5f);

            // Yan kirişler
            var side = width * 0.5f - 0.04f;
            for (var s = -1; s <= 1; s += 2)
            {
                var c = mid + rot * new Vector3(side * s, 0f, 0f) - slopeNormal * 0.12f;
                Box(c, new Vector3(0.07f, 0.26f, length), slopeRot, frameMaterial, StructureCollider.None);
            }

            // Basamaklar
            var steps = Mathf.Max(2, Mathf.RoundToInt(rise / 0.2f));
            var h = rise / steps;
            var d = run / steps;
            for (var k = 1; k < steps; k++)
            {
                var local = new Vector3(0f, k * h - 0.025f, (k - 0.5f) * d);
                Box(bottom + rot * local, new Vector3(width - 0.1f, 0.05f, d + 0.06f), rot, treadMaterial, StructureCollider.None);
            }

            RampCollider(bottom, yawDegrees, width, rise, run, treadMaterial, 0.12f);

            if (!railings)
                return;

            for (var s = -1; s <= 1; s += 2)
            {
                var x = side * s;
                var railMid = mid + rot * new Vector3(x, 0f, 0f) + Vector3.up * 0.95f;
                Box(railMid, new Vector3(0.05f, 0.05f, length), slopeRot, frameMaterial, StructureCollider.None);
                // Görünmez korkuluk paneli (düşmeyi engeller)
                ColliderBox(mid + rot * new Vector3(x, 0f, 0f) + Vector3.up * 0.55f, new Vector3(0.06f, 0.9f, length), slopeRot, frameMaterial);
                for (var p = 0; p < 3; p++)
                {
                    var tz = Mathf.Lerp(0.1f, run - 0.1f, p / 2f);
                    var ty = rise * tz / run;
                    var postBase = bottom + rot * new Vector3(x, ty, tz);
                    Box(postBase + Vector3.up * 0.48f, new Vector3(0.05f, 0.96f, 0.05f), rot, frameMaterial, StructureCollider.None);
                }
            }
        }

        /// <summary>Eğik rampa çarpıştırıcısı: üst yüzü (0,0)→(run,rise) doğrusundan geçer (yerel yaw çerçevesinde).</summary>
        public void RampCollider(Vector3 bottom, float yawDegrees, float width, float rise, float run, MaterialId material, float thickness = 0.3f)
        {
            var rot = Quaternion.Euler(0f, yawDegrees, 0f);
            var length = Mathf.Sqrt(rise * rise + run * run);
            var angle = Mathf.Atan2(rise, run);
            var slopeRot = rot * Quaternion.Euler(-angle * Mathf.Rad2Deg, 0f, 0f);
            var topMid = bottom + rot * new Vector3(0f, rise * 0.5f, run * 0.5f);
            var center = topMid - (slopeRot * Vector3.up) * (thickness * 0.5f);
            ColliderBox(center, new Vector3(width, thickness, length), slopeRot, material);
        }

        // ================================================================== Korkuluk

        /// <summary>Yatay korkuluk (dikmeler + iki yatay lata) ve görünmez çarpışma paneli.</summary>
        public void Railing(Vector3 from, Vector3 to, float baseY, float height, MaterialId material, bool collider = true)
        {
            from.y = baseY;
            to.y = baseY;
            var d = to - from;
            var len = d.magnitude;
            if (len < 0.05f)
                return;

            var dir = d / len;
            var rot = Quaternion.LookRotation(dir, Vector3.up);
            var posts = Mathf.Max(2, Mathf.CeilToInt(len / 1.2f) + 1);
            for (var i = 0; i < posts; i++)
            {
                var p = Vector3.Lerp(from, to, i / (float)(posts - 1));
                Box(p + Vector3.up * (height * 0.5f), new Vector3(0.05f, height, 0.05f), rot, material, StructureCollider.None);
            }

            Box((from + to) * 0.5f + Vector3.up * (height - 0.025f), new Vector3(0.06f, 0.05f, len), rot, material, StructureCollider.None);
            Box((from + to) * 0.5f + Vector3.up * (height * 0.5f), new Vector3(0.04f, 0.04f, len), rot, material, StructureCollider.None);
            if (collider)
                ColliderBox((from + to) * 0.5f + Vector3.up * (height * 0.5f), new Vector3(0.06f, height, len), rot, material);
        }

        // ================================================================== Eğri şekiller

        /// <summary>
        /// Kesik koni / silindir. center: gövde merkezi, rotation: eksen (yerel Y). radiusTop = 0 → koni.
        /// Box çarpışma istenirse konvekse çevrilir.
        /// </summary>
        public void Cylinder(Vector3 center, Quaternion rotation, float radiusBottom, float radiusTop, float height, int segments,
            MaterialId material, StructureCollider collider = StructureCollider.Convex, bool caps = true)
        {
            if (height < 0.005f || (radiusBottom < 0.001f && radiusTop < 0.001f))
                return;

            segments = Mathf.Clamp(segments, 3, 48);
            var bucket = GetBucket(material);
            var v0 = bucket.Vertices.Count;
            var t0 = bucket.Triangles.Count;
            var half = height * 0.5f;
            var slope = (radiusBottom - radiusTop) / height;
            var circumference = Mathf.PI * 2f * Mathf.Max(radiusBottom, radiusTop);

            // Yan yüzey (yumuşak normaller)
            var ring = bucket.Vertices.Count;
            for (var i = 0; i <= segments; i++)
            {
                var a = i / (float)segments * Mathf.PI * 2f;
                var cos = Mathf.Cos(a);
                var sin = Mathf.Sin(a);
                var n = rotation * new Vector3(cos, slope, sin).normalized;
                var u = i / (float)segments * circumference * UvScale;
                AddVertex(bucket, center + rotation * new Vector3(cos * radiusBottom, -half, sin * radiusBottom), n, new Vector2(u, 0f));
                AddVertex(bucket, center + rotation * new Vector3(cos * radiusTop, half, sin * radiusTop), n, new Vector2(u, height * UvScale));
            }

            for (var i = 0; i < segments; i++)
            {
                var b0 = ring + i * 2;
                var t0i = b0 + 1;
                var b1 = b0 + 2;
                var t1 = b0 + 3;
                var a = (i + 0.5f) / segments * Mathf.PI * 2f;
                var outward = rotation * new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                if (radiusBottom > 0.0005f)
                    Tri(bucket, b0, b1, t1, outward);
                if (radiusTop > 0.0005f)
                    Tri(bucket, b0, t1, t0i, outward);
                else
                    Tri(bucket, b0, b1, t0i, outward);
            }

            if (caps)
            {
                if (radiusTop > 0.0005f)
                    Disc(bucket, center + rotation * new Vector3(0f, half, 0f), rotation, radiusTop, segments, true);
                if (radiusBottom > 0.0005f)
                    Disc(bucket, center + rotation * new Vector3(0f, -half, 0f), rotation, radiusBottom, segments, false);
            }

            if (collider != StructureCollider.None)
                AddConvexFromRange(bucket, v0, t0);
        }

        /// <summary>Dikey silindir (taban merkezinden).</summary>
        public void VerticalCylinder(Vector3 baseCenter, float radius, float height, int segments, MaterialId material,
            StructureCollider collider = StructureCollider.Convex)
            => Cylinder(baseCenter + Vector3.up * (height * 0.5f), Quaternion.identity, radius, radius, height, segments, material, collider);

        /// <summary>
        /// Küre dilimi. latFrom/latTo derece (-90 alt kutup, 0 ekvator, 90 üst kutup). heightScale: dikey basıklık/sivrilik.
        /// capBottom: alt kenarı kapatan düz disk (kubbe tabanı).
        /// </summary>
        public void SphereSection(Vector3 center, float radius, float latFrom, float latTo, int segments, int rings,
            MaterialId material, StructureCollider collider = StructureCollider.Convex, bool capBottom = true, float heightScale = 1f)
        {
            if (radius < 0.01f)
                return;

            segments = Mathf.Clamp(segments, 6, 40);
            rings = Mathf.Clamp(rings, 2, 24);
            latFrom = Mathf.Clamp(latFrom, -90f, 90f);
            latTo = Mathf.Clamp(latTo, latFrom + 1f, 90f);
            var bucket = GetBucket(material);
            var v0 = bucket.Vertices.Count;
            var t0 = bucket.Triangles.Count;
            var start = bucket.Vertices.Count;
            var cols = segments + 1;

            for (var r = 0; r <= rings; r++)
            {
                var lat = Mathf.Lerp(latFrom, latTo, r / (float)rings) * Mathf.Deg2Rad;
                var y = Mathf.Sin(lat);
                var rr = Mathf.Cos(lat);
                for (var i = 0; i <= segments; i++)
                {
                    var a = i / (float)segments * Mathf.PI * 2f;
                    var dirv = new Vector3(Mathf.Cos(a) * rr, y, Mathf.Sin(a) * rr);
                    var p = new Vector3(dirv.x * radius, dirv.y * radius * heightScale, dirv.z * radius);
                    var n = new Vector3(dirv.x, dirv.y / Mathf.Max(0.05f, heightScale), dirv.z).normalized;
                    AddVertex(bucket, center + p, n, new Vector2(i / (float)segments * Mathf.PI * 2f * radius * UvScale, (lat * radius) * UvScale));
                }
            }

            for (var r = 0; r < rings; r++)
            {
                for (var i = 0; i < segments; i++)
                {
                    var a = start + r * cols + i;
                    var b = a + 1;
                    var c = a + cols + 1;
                    var d = a + cols;
                    var outward = (bucket.Normals[a] + bucket.Normals[c]).normalized;
                    if (outward.sqrMagnitude < 0.0001f)
                        outward = bucket.Normals[c];
                    Tri(bucket, a, b, c, outward);
                    Tri(bucket, a, c, d, outward);
                }
            }

            if (capBottom && latFrom > -89.5f)
            {
                var lat = latFrom * Mathf.Deg2Rad;
                Disc(bucket, center + Vector3.up * (Mathf.Sin(lat) * radius * heightScale), Quaternion.identity, Mathf.Cos(lat) * radius, segments, false);
            }

            if (collider != StructureCollider.None)
                AddConvexFromRange(bucket, v0, t0);
        }

        /// <summary>Yarım küre kubbe (center = taban merkezi).</summary>
        public void Dome(Vector3 baseCenter, float radius, int segments, MaterialId material,
            StructureCollider collider = StructureCollider.Convex, float heightScale = 1f)
            => SphereSection(baseCenter, radius, 0f, 90f, segments, Mathf.Max(4, segments / 3), material, collider, true, heightScale);

        /// <summary>
        /// Dışbükey çokgen prizması. polygon: (axisX, axisY) düzleminde dışbükey köşeler; prizma axisX×axisY yönünde
        /// thickness kalınlığında, origin düzlemine göre ortalanır.
        /// </summary>
        public void Prism(IReadOnlyList<Vector2> polygon, Vector3 origin, Vector3 axisX, Vector3 axisY, float thickness,
            MaterialId material, StructureCollider collider = StructureCollider.Convex)
        {
            if (polygon == null || polygon.Count < 3 || thickness < 0.001f)
                return;

            axisX = axisX.normalized;
            axisY = axisY.normalized;
            var axisZ = Vector3.Cross(axisX, axisY).normalized;
            if (axisZ.sqrMagnitude < 0.5f)
                return;

            var bucket = GetBucket(material);
            var v0 = bucket.Vertices.Count;
            var t0 = bucket.Triangles.Count;
            var half = thickness * 0.5f;
            var count = polygon.Count;

            var centroid = Vector2.zero;
            for (var i = 0; i < count; i++)
                centroid += polygon[i];
            centroid /= count;

            // Ön ve arka yüzler
            for (var side = -1; side <= 1; side += 2)
            {
                var n = axisZ * side;
                var baseIndex = bucket.Vertices.Count;
                for (var i = 0; i < count; i++)
                {
                    var p = polygon[i];
                    AddVertex(bucket, origin + axisX * p.x + axisY * p.y + axisZ * (half * side), n, p * UvScale);
                }

                for (var i = 1; i + 1 < count; i++)
                    Tri(bucket, baseIndex, baseIndex + i, baseIndex + i + 1, n);
            }

            // Yan yüzler
            var acc = 0f;
            for (var i = 0; i < count; i++)
            {
                var p0 = polygon[i];
                var p1 = polygon[(i + 1) % count];
                var e = p1 - p0;
                var len = e.magnitude;
                if (len < 0.0005f)
                    continue;

                var n2 = new Vector2(e.y, -e.x) / len;
                var midEdge = (p0 + p1) * 0.5f;
                if (Vector2.Dot(n2, midEdge - centroid) < 0f)
                    n2 = -n2;
                var n = (axisX * n2.x + axisY * n2.y).normalized;
                var a = origin + axisX * p0.x + axisY * p0.y;
                var b = origin + axisX * p1.x + axisY * p1.y;
                Quad(bucket,
                    a - axisZ * half, b - axisZ * half, b + axisZ * half, a + axisZ * half, n,
                    new Vector2(acc, 0f) * UvScale, new Vector2(acc + len, 0f) * UvScale,
                    new Vector2(acc + len, thickness) * UvScale, new Vector2(acc, thickness) * UvScale);
                acc += len;
            }

            if (collider != StructureCollider.None)
                AddConvexFromRange(bucket, v0, t0);
        }

        /// <summary>Dışbükey nokta kümesinden (ör. kırma çatı) kapalı katı: köşe listesi ve üçgen grupları verilir.</summary>
        public void ConvexSolid(IReadOnlyList<Vector3> points, IReadOnlyList<int[]> faces, MaterialId material,
            StructureCollider collider = StructureCollider.Convex)
        {
            if (points == null || faces == null || points.Count < 4)
                return;

            var bucket = GetBucket(material);
            var v0 = bucket.Vertices.Count;
            var t0 = bucket.Triangles.Count;
            var centroid = Vector3.zero;
            for (var i = 0; i < points.Count; i++)
                centroid += points[i];
            centroid /= points.Count;

            for (var f = 0; f < faces.Count; f++)
            {
                var face = faces[f];
                if (face == null || face.Length < 3)
                    continue;

                var a = points[face[0]];
                var n = Vector3.zero;
                for (var i = 1; i + 1 < face.Length; i++)
                    n += Vector3.Cross(points[face[i]] - a, points[face[i + 1]] - a);
                if (n.sqrMagnitude < 1e-8f)
                    continue;
                n.Normalize();
                if (Vector3.Dot(n, a - centroid) < 0f)
                    n = -n;

                var baseIndex = bucket.Vertices.Count;
                for (var i = 0; i < face.Length; i++)
                {
                    var p = points[face[i]];
                    AddVertex(bucket, p, n, PlanarUv(p, n));
                }

                for (var i = 1; i + 1 < face.Length; i++)
                    Tri(bucket, baseIndex, baseIndex + i, baseIndex + i + 1, n);
            }

            if (collider != StructureCollider.None)
                AddConvexFromRange(bucket, v0, t0);
        }

        // ================================================================== Çıktı

        /// <summary>parent altında name adlı kap nesnesi oluşturur ve malzeme nesnelerini içine kurar.</summary>
        public GameObject Build(Transform parent, string name)
        {
            var root = StructureKit.CreateGroup(parent, string.IsNullOrEmpty(name) ? "Yapi" : name, Vector3.zero, Quaternion.identity);
            BuildInto(root.transform);
            return root;
        }

        /// <summary>Malzeme başına bir alt nesne (yerel kimlik dönüşümü) üretir. Oluşan nesne sayısını döndürür.</summary>
        public int BuildInto(Transform root)
        {
            if (root == null)
                return 0;

            var created = 0;
            for (var i = 0; i < _buckets.Count; i++)
            {
                var bucket = _buckets[i];
                var hasMesh = bucket.Triangles.Count > 0;
                var hasColliders = bucket.Boxes.Count > 0 || bucket.Rotated.Count > 0 || bucket.Convex.Count > 0;
                if (!hasMesh && !hasColliders)
                    continue;

                var go = StructureKit.CreateGroup(root, bucket.Material.ToString(), Vector3.zero, Quaternion.identity);
                created++;
                if (hasMesh)
                {
                    var mf = go.AddComponent<MeshFilter>();
                    mf.sharedMesh = CreateMesh(bucket, root.name + "_" + bucket.Material);
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = MaterialLibrary.Get(bucket.Material);
                    mr.shadowCastingMode = ShadowCastingMode.On;
                    mr.receiveShadows = true;
                }

                var physics = StructureKit.PhysicsMaterialFor(StructureKit.SurfaceOf(bucket.Material));
                for (var b = 0; b < bucket.Boxes.Count; b++)
                {
                    var box = go.AddComponent<BoxCollider>();
                    box.center = bucket.Boxes[b].center;
                    box.size = bucket.Boxes[b].size;
                    box.sharedMaterial = physics;
                }

                for (var r = 0; r < bucket.Rotated.Count; r++)
                {
                    var rb = bucket.Rotated[r];
                    var child = StructureKit.CreateGroup(go.transform, "Egim", rb.Center, rb.Rotation);
                    var box = child.AddComponent<BoxCollider>();
                    box.size = rb.Size;
                    box.sharedMaterial = physics;
                }

                for (var c = 0; c < bucket.Convex.Count; c++)
                {
                    var mc = go.AddComponent<MeshCollider>();
                    mc.sharedMesh = bucket.Convex[c];
                    mc.convex = true;
                    mc.sharedMaterial = physics;
                }
            }

            return created;
        }

        /// <summary>Tek malzemenin birleşik ağını üretir (yoksa null).</summary>
        public Mesh CreateMesh(MaterialId material, string name)
        {
            return _lookup.TryGetValue((int)material, out var bucket) ? CreateMesh(bucket, name) : null;
        }

        private static Mesh CreateMesh(Bucket bucket, string name)
        {
            var mesh = new Mesh { name = name };
            if (bucket.Vertices.Count > 65000)
                mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(bucket.Vertices);
            mesh.SetNormals(bucket.Normals);
            mesh.SetUVs(0, bucket.Uvs);
            mesh.SetTriangles(bucket.Triangles, 0, true);
            return mesh;
        }

        // ================================================================== İç yardımcılar

        private Bucket GetBucket(MaterialId material)
        {
            var key = (int)material;
            if (!_lookup.TryGetValue(key, out var bucket))
            {
                bucket = new Bucket(material);
                _lookup.Add(key, bucket);
                _buckets.Add(bucket);
            }

            return bucket;
        }

        private int AddVertex(Bucket bucket, Vector3 position, Vector3 normal, Vector2 uv)
        {
            bucket.Vertices.Add(position);
            bucket.Normals.Add(normal);
            bucket.Uvs.Add(uv);
            Encapsulate(position);
            return bucket.Vertices.Count - 1;
        }

        private static void Tri(Bucket bucket, int a, int b, int c, Vector3 outward)
        {
            var v = bucket.Vertices;
            var cross = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
            if (Vector3.Dot(cross, outward) >= 0f)
            {
                bucket.Triangles.Add(a);
                bucket.Triangles.Add(b);
                bucket.Triangles.Add(c);
            }
            else
            {
                bucket.Triangles.Add(a);
                bucket.Triangles.Add(c);
                bucket.Triangles.Add(b);
            }
        }

        private void Quad(Bucket bucket, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 normal,
            Vector2 uv0, Vector2 uv1, Vector2 uv2, Vector2 uv3)
        {
            var i0 = AddVertex(bucket, p0, normal, uv0);
            var i1 = AddVertex(bucket, p1, normal, uv1);
            var i2 = AddVertex(bucket, p2, normal, uv2);
            var i3 = AddVertex(bucket, p3, normal, uv3);
            var cross = Vector3.Cross(p2 - p0, p3 - p1);
            if (Vector3.Dot(cross, normal) >= 0f)
            {
                bucket.Triangles.Add(i0);
                bucket.Triangles.Add(i1);
                bucket.Triangles.Add(i2);
                bucket.Triangles.Add(i0);
                bucket.Triangles.Add(i2);
                bucket.Triangles.Add(i3);
            }
            else
            {
                bucket.Triangles.Add(i0);
                bucket.Triangles.Add(i2);
                bucket.Triangles.Add(i1);
                bucket.Triangles.Add(i0);
                bucket.Triangles.Add(i3);
                bucket.Triangles.Add(i2);
            }
        }

        private void Disc(Bucket bucket, Vector3 center, Quaternion rotation, float radius, int segments, bool up)
        {
            var n = rotation * (up ? Vector3.up : Vector3.down);
            var c = AddVertex(bucket, center, n, Vector2.zero);
            var first = bucket.Vertices.Count;
            for (var i = 0; i <= segments; i++)
            {
                var a = i / (float)segments * Mathf.PI * 2f;
                var local = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                AddVertex(bucket, center + rotation * local, n, new Vector2(local.x, local.z) * UvScale);
            }

            for (var i = 0; i < segments; i++)
                Tri(bucket, c, first + i, first + i + 1, n);
        }

        private void EmitBox(Bucket bucket, Vector3 center, Vector3 size, Quaternion rotation)
        {
            var h = size * 0.5f;
            var uvOrigin = Quaternion.Inverse(rotation) * center;
            for (var f = 0; f < 6; f++)
            {
                Vector3 n, a, b;
                switch (f)
                {
                    case 0: n = Vector3.right; a = Vector3.forward; b = Vector3.up; break;
                    case 1: n = Vector3.left; a = Vector3.forward; b = Vector3.up; break;
                    case 2: n = Vector3.up; a = Vector3.right; b = Vector3.forward; break;
                    case 3: n = Vector3.down; a = Vector3.right; b = Vector3.forward; break;
                    case 4: n = Vector3.forward; a = Vector3.right; b = Vector3.up; break;
                    default: n = Vector3.back; a = Vector3.right; b = Vector3.up; break;
                }

                var nc = Vector3.Scale(n, h);
                var ha = Vector3.Scale(a, h);
                var hb = Vector3.Scale(b, h);
                var l0 = nc - ha - hb;
                var l1 = nc + ha - hb;
                var l2 = nc + ha + hb;
                var l3 = nc - ha + hb;
                var worldN = rotation * n;
                Quad(bucket,
                    center + rotation * l0, center + rotation * l1, center + rotation * l2, center + rotation * l3, worldN,
                    PlanarUv(uvOrigin + l0, n), PlanarUv(uvOrigin + l1, n), PlanarUv(uvOrigin + l2, n), PlanarUv(uvOrigin + l3, n));
            }
        }

        private Vector2 PlanarUv(Vector3 p, Vector3 n)
        {
            var ax = Mathf.Abs(n.x);
            var ay = Mathf.Abs(n.y);
            var az = Mathf.Abs(n.z);
            if (ax >= ay && ax >= az)
                return new Vector2(p.z, p.y) * UvScale;
            if (ay >= az)
                return new Vector2(p.x, p.z) * UvScale;
            return new Vector2(p.x, p.y) * UvScale;
        }

        private static bool TrySnapAxisAligned(Quaternion rotation, ref Vector3 size)
        {
            var up = rotation * Vector3.up;
            if (up.y < 0.9999f)
                return false;

            var f = rotation * Vector3.forward;
            if (Mathf.Abs(f.z) > 0.9999f)
                return true;
            if (Mathf.Abs(f.x) > 0.9999f)
            {
                size = new Vector3(size.z, size.y, size.x);
                return true;
            }

            return false;
        }

        private static void AddBoxCollider(Bucket bucket, Vector3 center, Vector3 size, Quaternion rotation)
        {
            if (rotation == Quaternion.identity || Quaternion.Angle(rotation, Quaternion.identity) < 0.01f)
                bucket.Boxes.Add(new Bounds(center, size));
            else
                bucket.Rotated.Add(new RotatedBox { Center = center, Size = size, Rotation = rotation });
        }

        private static void AddConvexFromRange(Bucket bucket, int v0, int t0)
        {
            var vCount = bucket.Vertices.Count - v0;
            var tCount = bucket.Triangles.Count - t0;
            if (vCount < 4 || tCount < 3)
                return;

            var verts = new Vector3[vCount];
            for (var i = 0; i < vCount; i++)
                verts[i] = bucket.Vertices[v0 + i];
            var tris = new int[tCount];
            for (var i = 0; i < tCount; i++)
                tris[i] = bucket.Triangles[t0 + i] - v0;

            var mesh = new Mesh { name = "KonveksCarpisma" };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            bucket.Convex.Add(mesh);
        }
    }
}
