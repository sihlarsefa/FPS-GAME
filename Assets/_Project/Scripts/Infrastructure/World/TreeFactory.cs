using System.Collections.Generic;
using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>Arazi ağacı türleri (TreePrototype sırası).</summary>
    public enum TreeKind
    {
        PineA = 0,
        PineB = 1,
        Oak = 2,
        Dead = 3,
        Bush = 4
    }

    /// <summary>
    /// Low-poly ağaç mesh'leri ve arazi ağaç prototipleri (kök: MeshFilter + 2 malzemeli MeshRenderer + gövde CapsuleCollider;
    /// çalının çarpıştırıcısı yoktur — içinden geçilebilir). Prototipler editörde prefab olarak kaydedilebilir.
    /// </summary>
    public static class TreeFactory
    {
        public const int KindCount = 5;

        /// <summary>Türün temel (ölçek 1) yüksekliği, m.</summary>
        public static float BaseHeight(TreeKind kind)
        {
            switch (kind)
            {
                case TreeKind.PineA: return 11f;
                case TreeKind.PineB: return 14f;
                case TreeKind.Oak: return 8.5f;
                case TreeKind.Dead: return 7.5f;
                default: return 1.3f;
            }
        }

        /// <summary>Gövde yarıçapı (çarpıştırıcı/NavMesh), m.</summary>
        public static float TrunkRadius(TreeKind kind)
        {
            switch (kind)
            {
                case TreeKind.PineA: return 0.3f;
                case TreeKind.PineB: return 0.36f;
                case TreeKind.Oak: return 0.42f;
                case TreeKind.Dead: return 0.3f;
                default: return 0f;
            }
        }

        /// <summary>Tüm türlerin prototip nesnelerini üretir (dizi indeksi = (int)TreeKind). parent etkin değilse sahnede görünmezler.</summary>
        public static GameObject[] CreateDefaultPrototypes(Transform parent, int seed)
        {
            var result = new GameObject[KindCount];
            for (var i = 0; i < KindCount; i++)
                result[i] = CreatePrototype((TreeKind)i, parent, seed + i * 7919);
            return result;
        }

        /// <summary>Tek bir ağaç prototipi (prefab adayı) üretir.</summary>
        public static GameObject CreatePrototype(TreeKind kind, Transform parent, int seed)
        {
            var height = BaseHeight(kind);
            Mesh mesh;
            Material trunk;
            Material foliage;
            switch (kind)
            {
                case TreeKind.PineA:
                case TreeKind.PineB:
                    mesh = MeshFactory.PineTree(height, seed);
                    trunk = MaterialLibrary.Get(MaterialId.Bark);
                    foliage = MaterialLibrary.Get(MaterialId.PineNeedles);
                    break;
                case TreeKind.Oak:
                    mesh = MeshFactory.OakTree(height, seed);
                    trunk = MaterialLibrary.Get(MaterialId.Bark);
                    foliage = MaterialLibrary.Get(MaterialId.Foliage);
                    break;
                case TreeKind.Dead:
                    mesh = MeshFactory.DeadTree(height, seed);
                    trunk = MaterialLibrary.Get(MaterialId.DeadWood);
                    foliage = MaterialLibrary.Get(MaterialId.Bark);
                    break;
                default:
                    mesh = MeshFactory.Bush(height, seed);
                    trunk = MaterialLibrary.Get(MaterialId.Bark);
                    foliage = MaterialLibrary.Get(MaterialId.FoliageDark);
                    break;
            }

            var go = new GameObject("HK_Tree_" + kind);
            if (parent != null)
                go.transform.SetParent(parent, false);
            go.layer = GameLayers.Default;

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { trunk, foliage };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;

            var radius = TrunkRadius(kind);
            if (radius > 0f)
            {
                var capsule = go.AddComponent<CapsuleCollider>();
                capsule.direction = 1;
                capsule.radius = radius;
                var colliderHeight = kind == TreeKind.Dead ? height * 0.85f : height * 0.6f;
                capsule.height = colliderHeight;
                capsule.center = new Vector3(0f, colliderHeight * 0.5f, 0f);
            }

            return go;
        }

        /// <summary>Prototip nesnelerinden TreePrototype dizisi.</summary>
        public static TreePrototype[] ToTreePrototypes(IReadOnlyList<GameObject> prefabs)
        {
            var count = prefabs != null ? prefabs.Count : 0;
            var result = new TreePrototype[count];
            for (var i = 0; i < count; i++)
                result[i] = new TreePrototype { prefab = prefabs[i], bendFactor = 0f };
            return result;
        }

        // ================================================================== Mesh üreticileri

        internal static Mesh BuildPineMesh(float height, int seed)
        {
            var rng = new System.Random(seed * 31 + 7);
            var b = new MeshBuilder(2);
            var trunkRadius = height * 0.028f;
            MeshFactory.AddFrustum(b, MeshFactory.TrunkSubmesh, Vector3.zero, trunkRadius, trunkRadius * 0.45f, height * 0.55f, 6, false, false);

            const int layers = 4;
            for (var k = 0; k < layers; k++)
            {
                var t = k / (float)(layers - 1);
                var baseY = height * Mathf.Lerp(0.2f, 0.66f, t);
                var coneHeight = height * Mathf.Lerp(0.36f, 0.34f, t);
                if (k == layers - 1)
                    coneHeight = height - baseY;
                var radius = height * Mathf.Lerp(0.27f, 0.11f, t) * Range(rng, 0.92f, 1.08f);
                var segments = 7;
                var rotation = Range(rng, 0f, Mathf.PI * 2f);
                var ring = new Vector3[segments];
                for (var i = 0; i < segments; i++)
                {
                    var angle = rotation + i / (float)segments * Mathf.PI * 2f;
                    var r = radius * Range(rng, 0.82f, 1.12f);
                    ring[i] = new Vector3(Mathf.Cos(angle) * r, baseY + height * Range(rng, -0.025f, 0.02f), Mathf.Sin(angle) * r);
                }

                var apex = new Vector3(Range(rng, -0.04f, 0.04f) * height * 0.1f, baseY + coneHeight, Range(rng, -0.04f, 0.04f) * height * 0.1f);
                var under = new Vector3(0f, baseY + coneHeight * 0.18f, 0f);
                for (var i = 0; i < segments; i++)
                {
                    var p0 = ring[i];
                    var p1 = ring[(i + 1) % segments];
                    b.AddFlatTriangle(MeshFactory.FoliageSubmesh, p0, apex, p1, 0.5f);
                    b.AddFlatTriangle(MeshFactory.FoliageSubmesh, p0, p1, under, 0.5f);
                }
            }

            return b.ToMesh("HK_PineTree");
        }

        internal static Mesh BuildOakMesh(float height, int seed)
        {
            var rng = new System.Random(seed * 37 + 11);
            var b = new MeshBuilder(2);
            var trunkRadius = height * 0.05f;
            var trunkHeight = height * 0.55f;
            MeshFactory.AddFrustum(b, MeshFactory.TrunkSubmesh, Vector3.zero, trunkRadius, trunkRadius * 0.6f, trunkHeight, 7, false, false);

            // İki ana dal.
            var branchCount = 2 + rng.Next(2);
            for (var i = 0; i < branchCount; i++)
            {
                var yaw = i / (float)branchCount * 360f + Range(rng, -25f, 25f);
                var tilt = Range(rng, 35f, 50f);
                var rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(0f, 0f, -tilt);
                var start = new Vector3(0f, trunkHeight * Range(rng, 0.65f, 0.85f), 0f);
                MeshFactory.AddFrustum(b, MeshFactory.TrunkSubmesh, start, rotation, trunkRadius * 0.45f, trunkRadius * 0.2f,
                    height * Range(rng, 0.24f, 0.3f), 5, false, false);
            }

            // Taç: ana küme + 3 yan küme.
            var crownY = height * 0.68f;
            AddBlob(b, MeshFactory.FoliageSubmesh, new Vector3(0f, crownY, 0f), new Vector3(1.1f, 0.78f, 1.1f) * (height * 0.3f), 1, 0.16f, rng);
            var blobs = 3;
            for (var i = 0; i < blobs; i++)
            {
                var angle = i / (float)blobs * Mathf.PI * 2f + Range(rng, -0.4f, 0.4f);
                var offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * height * Range(rng, 0.17f, 0.24f);
                offset.y = height * Range(rng, -0.08f, 0.1f);
                var size = height * Range(rng, 0.17f, 0.22f);
                AddBlob(b, MeshFactory.FoliageSubmesh, new Vector3(0f, crownY, 0f) + offset, new Vector3(1.1f, 0.8f, 1.1f) * size, 1, 0.18f, rng);
            }

            return b.ToMesh("HK_OakTree");
        }

        internal static Mesh BuildDeadMesh(float height, int seed)
        {
            var rng = new System.Random(seed * 41 + 13);
            var b = new MeshBuilder(2);
            var trunkRadius = height * 0.045f;
            var lean = Quaternion.Euler(Range(rng, -6f, 6f), 0f, Range(rng, -6f, 6f));
            MeshFactory.AddFrustum(b, MeshFactory.TrunkSubmesh, Vector3.zero, lean, trunkRadius, trunkRadius * 0.25f, height, 6, false, false);

            var branches = 4 + rng.Next(2);
            for (var i = 0; i < branches; i++)
            {
                var y = height * Range(rng, 0.35f, 0.82f);
                var yaw = Range(rng, 0f, 360f);
                var tilt = Range(rng, 38f, 65f);
                var rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(0f, 0f, -tilt);
                var start = lean * new Vector3(0f, y, 0f);
                var length = height * Range(rng, 0.18f, 0.32f);
                MeshFactory.AddFrustum(b, MeshFactory.FoliageSubmesh, start, rotation, trunkRadius * 0.35f, trunkRadius * 0.08f, length, 4, false, false);

                // Küçük çatal.
                var forkStart = start + rotation * (Vector3.up * length * 0.6f);
                var forkRotation = rotation * Quaternion.Euler(Range(rng, -30f, 30f), 0f, Range(rng, 20f, 35f));
                MeshFactory.AddFrustum(b, MeshFactory.FoliageSubmesh, forkStart, forkRotation, trunkRadius * 0.15f, trunkRadius * 0.04f,
                    length * 0.5f, 4, false, false);
            }

            return b.ToMesh("HK_DeadTree");
        }

        internal static Mesh BuildBushMesh(float radius, int seed)
        {
            var rng = new System.Random(seed * 43 + 17);
            var b = new MeshBuilder(2);
            for (var i = 0; i < 3; i++)
            {
                var rotation = Quaternion.Euler(Range(rng, -20f, 20f), Range(rng, 0f, 360f), Range(rng, -20f, 20f));
                MeshFactory.AddFrustum(b, MeshFactory.TrunkSubmesh, Vector3.zero, rotation, radius * 0.05f, radius * 0.02f, radius * 0.55f, 4, false, false);
            }

            AddBlob(b, MeshFactory.FoliageSubmesh, new Vector3(0f, radius * 0.5f, 0f), new Vector3(1f, 0.7f, 1f) * (radius * 0.75f), 1, 0.18f, rng);
            for (var i = 0; i < 2; i++)
            {
                var angle = Range(rng, 0f, Mathf.PI * 2f);
                var offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius * 0.45f;
                AddBlob(b, MeshFactory.FoliageSubmesh, new Vector3(offset.x, radius * 0.38f, offset.z), new Vector3(1f, 0.75f, 1f) * (radius * 0.5f), 0, 0.15f, rng);
            }

            return b.ToMesh("HK_Bush");
        }

        /// <summary>Düzensiz, düz gölgeli ikosfer kümesi ekler.</summary>
        internal static void AddBlob(MeshBuilder b, int submesh, Vector3 center, Vector3 radii, int subdivisions, float jitter, System.Random rng)
        {
            Icosphere.Get(subdivisions, out var vertices, out var triangles);
            var jittered = new Vector3[vertices.Length];
            for (var i = 0; i < vertices.Length; i++)
            {
                var v = vertices[i] * (1f + Range(rng, -jitter, jitter));
                jittered[i] = center + Vector3.Scale(v, radii);
            }

            for (var t = 0; t + 2 < triangles.Length; t += 3)
                b.AddFlatTriangle(submesh, jittered[triangles[t]], jittered[triangles[t + 1]], jittered[triangles[t + 2]], 0.5f);
        }

        internal static float Range(System.Random rng, float min, float max)
        {
            return min + (float)rng.NextDouble() * (max - min);
        }
    }

    /// <summary>Birim ikosfer (indeksli; alt bölüm 0..3), önbellekli. Üçgenler dışa bakar (Cross(b-a, c-a) dışa).</summary>
    internal static class Icosphere
    {
        private static readonly Dictionary<int, Vector3[]> VertexCache = new Dictionary<int, Vector3[]>();
        private static readonly Dictionary<int, int[]> TriangleCache = new Dictionary<int, int[]>();

        public static void Get(int subdivisions, out Vector3[] vertices, out int[] triangles)
        {
            subdivisions = Mathf.Clamp(subdivisions, 0, 3);
            if (VertexCache.TryGetValue(subdivisions, out vertices) && TriangleCache.TryGetValue(subdivisions, out triangles))
                return;

            var t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var verts = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
            };
            for (var i = 0; i < verts.Count; i++)
                verts[i] = verts[i].normalized;

            var tris = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
                1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
                4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            };

            for (var s = 0; s < subdivisions; s++)
            {
                var midCache = new Dictionary<long, int>();
                var next = new List<int>(tris.Count * 4);
                for (var i = 0; i < tris.Count; i += 3)
                {
                    int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                    var ab = Mid(verts, midCache, a, b);
                    var bc = Mid(verts, midCache, b, c);
                    var ca = Mid(verts, midCache, c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }

                tris = next;
            }

            // Not: bu sıralamada Cross(b - a, c - a) dışa bakar — Unity ön yüz kuralıyla uyumlu, çevirmeye gerek yok.

            vertices = verts.ToArray();
            triangles = tris.ToArray();
            VertexCache[subdivisions] = vertices;
            TriangleCache[subdivisions] = triangles;
        }

        private static int Mid(List<Vector3> verts, Dictionary<long, int> cache, int a, int b)
        {
            var key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (cache.TryGetValue(key, out var index))
                return index;
            verts.Add(((verts[a] + verts[b]) * 0.5f).normalized);
            index = verts.Count - 1;
            cache[key] = index;
            return index;
        }
    }
}
