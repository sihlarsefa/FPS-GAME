using System.Collections.Generic;
using Project.Infrastructure.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// Su yüzeyleri: dere boyunca WaterLevel yüksekliğinde şerit mesh ve göletler için disk. Çarpıştırıcı yoktur (yürünebilir
    /// dere yatağı / su altı zemini), katman <see cref="GameLayers.Water"/> (zemin/mermi ışınlarına girmez).
    /// Şerit kıyı çizgisinden biraz geniştir; fazlası kıyı arazisinin altında kalır.
    /// </summary>
    public static class WorldWater
    {
        public const string RootName = "Su";

        /// <summary>Su yüzeylerini üretir; kök nesneyi döner (su yoksa null).</summary>
        public static GameObject Build(MapLayout layout, TerrainModel model, Transform parent)
        {
            if (layout == null)
                return null;

            var hasRiver = layout.Rivers.Count > 0;
            var hasLake = layout.Lakes.Count > 0;
            if (!hasRiver && !hasLake)
                return null;

            var root = new GameObject(RootName);
            root.layer = GameLayers.Water;
            if (parent != null)
                root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var material = MaterialLibrary.Get(MaterialId.Water);
            var halfWidth = (model != null ? model.RiverWaterHalfWidth : 6f) + 3f;
            for (var r = 0; r < layout.Rivers.Count; r++)
            {
                var river = layout.Rivers[r];
                if (river?.Points == null || river.Points.Count < 2)
                    continue;

                // Harita dışına taşan kısım (arazi bitince boşlukta yüzen su) kırpılır.
                var points = ClipToSquare(river.Points, layout.HalfSize);
                if (points.Count < 2)
                    continue;

                var mesh = BuildRiverMesh(points, layout.WaterLevel, model != null ? halfWidth : river.Width * 0.5f + 3f);
                CreateSurface(root.transform, "Dere_" + r, mesh, material);
            }

            for (var l = 0; l < layout.Lakes.Count; l++)
            {
                var lake = layout.Lakes[l];
                if (lake == null || lake.Radius <= 1f)
                    continue;

                var mesh = BuildDiscMesh(new Vector3(lake.Center.x, layout.WaterLevel, lake.Center.y),
                    lake.Radius * TerrainModel.MaxLakeShoreScale + 5f, 56);
                CreateSurface(root.transform, "Gölet_" + l, mesh, material);
            }

            return root;
        }

        private static void CreateSurface(Transform parent, string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.layer = GameLayers.Water;
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        /// <summary>
        /// Çoklu çizgiyi [-half, half]² karesine kırpar (kareden çıkan/giren segmentlerde sınır noktası eklenir). Kare dışında
        /// birden fazla kez girip çıkan çizgilerde içerideki parçalar tek listede birleşir (dere için yeterli).
        /// </summary>
        public static List<Vector2> ClipToSquare(List<Vector2> points, float half)
        {
            var result = new List<Vector2>(points.Count);
            for (var i = 0; i < points.Count; i++)
            {
                var p = points[i];
                var inside = Inside(p, half);
                if (i > 0)
                {
                    var prev = points[i - 1];
                    var prevInside = Inside(prev, half);
                    if (inside != prevInside)
                        result.Add(BoundaryPoint(prevInside ? prev : p, prevInside ? p : prev, half));
                }

                if (inside)
                    result.Add(p);
            }

            return result;
        }

        private static bool Inside(Vector2 p, float half) => Mathf.Abs(p.x) <= half && Mathf.Abs(p.y) <= half;

        /// <summary>İçerideki a'dan dışarıdaki b'ye giden segmentin kare sınırını kestiği nokta (ikili arama).</summary>
        private static Vector2 BoundaryPoint(Vector2 inside, Vector2 outside, float half)
        {
            var a = inside;
            var b = outside;
            for (var k = 0; k < 24; k++)
            {
                var m = (a + b) * 0.5f;
                if (Inside(m, half))
                    a = m;
                else
                    b = m;
            }

            return a;
        }

        /// <summary>Çoklu çizgi boyunca yukarı bakan şerit (dünya koordinatı, y = waterLevel). UV metre ölçekli.</summary>
        public static Mesh BuildRiverMesh(List<Vector2> points, float waterLevel, float halfWidth)
        {
            var count = points.Count;
            var vertices = new Vector3[count * 2];
            var normals = new Vector3[count * 2];
            var uvs = new Vector2[count * 2];
            var triangles = new int[(count - 1) * 6];
            var along = 0f;
            for (var i = 0; i < count; i++)
            {
                var prev = points[Mathf.Max(0, i - 1)];
                var next = points[Mathf.Min(count - 1, i + 1)];
                var tangent = next - prev;
                if (tangent.sqrMagnitude < 1e-6f)
                    tangent = Vector2.up;
                tangent.Normalize();
                // Sağ dik (Unity: x sağ, z ileri) → (t.y, -t.x).
                var right = new Vector2(tangent.y, -tangent.x);
                if (i > 0)
                    along += Vector2.Distance(points[i - 1], points[i]);

                var p = points[i];
                var l = p - right * halfWidth;
                var r = p + right * halfWidth;
                vertices[i * 2] = new Vector3(l.x, waterLevel, l.y);
                vertices[i * 2 + 1] = new Vector3(r.x, waterLevel, r.y);
                normals[i * 2] = Vector3.up;
                normals[i * 2 + 1] = Vector3.up;
                uvs[i * 2] = new Vector2(0f, along);
                uvs[i * 2 + 1] = new Vector2(halfWidth * 2f, along);
            }

            for (var i = 0; i + 1 < count; i++)
            {
                var a = i * 2;
                var t = i * 6;
                // Yukarıdan bakınca saat yönü: sol(i) → sol(i+1) → sağ(i+1), sol(i) → sağ(i+1) → sağ(i).
                triangles[t] = a;
                triangles[t + 1] = a + 2;
                triangles[t + 2] = a + 3;
                triangles[t + 3] = a;
                triangles[t + 4] = a + 3;
                triangles[t + 5] = a + 1;
            }

            var mesh = new Mesh { name = "HK_RiverSurface" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Yukarı bakan disk (dünya koordinatı).</summary>
        public static Mesh BuildDiscMesh(Vector3 center, float radius, int segments)
        {
            segments = Mathf.Clamp(segments, 8, 256);
            var vertices = new Vector3[segments + 1];
            var normals = new Vector3[segments + 1];
            var uvs = new Vector2[segments + 1];
            var triangles = new int[segments * 3];
            vertices[0] = center;
            normals[0] = Vector3.up;
            uvs[0] = new Vector2(center.x, center.z);
            for (var i = 0; i < segments; i++)
            {
                var angle = i / (float)segments * Mathf.PI * 2f;
                var p = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                vertices[i + 1] = p;
                normals[i + 1] = Vector3.up;
                uvs[i + 1] = new Vector2(p.x, p.z);
            }

            for (var i = 0; i < segments; i++)
            {
                // Açı artışı yukarıdan bakınca saat yönünün tersi → (merkez, i+1, i) saat yönü.
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = 1 + (i + 1) % segments;
                triangles[i * 3 + 2] = 1 + i;
            }

            var mesh = new Mesh { name = "HK_LakeSurface" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
