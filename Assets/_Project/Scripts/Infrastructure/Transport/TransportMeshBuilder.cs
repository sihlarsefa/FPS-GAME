using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Infrastructure.Transport
{
    /// <summary>
    /// Araç modelleri için küçük, düz gölgelendirmeli (low-poly) mesh kurucu. Her yüz kendi köşelerini ve yüz normalini
    /// alır; sarım yönü "dış" ipucuna göre otomatik düzeltilir (aynalı/döndürülmüş parçalar güvenlidir).
    /// Malzeme yuvaları (slot) alt-mesh olarak üretilir; Build boş yuvaları atlar ve kullanılan yuva listesini döndürür.
    /// Yalnızca oluşturma anında kullanılır (önbelleğe alınan paylaşımlı meshler) — sıcak yolda değildir.
    /// </summary>
    internal sealed class TransportMeshBuilder
    {
        private const float UvScale = 0.5f;

        private readonly List<Vector3> _vertices = new List<Vector3>(2048);
        private readonly List<Vector3> _normals = new List<Vector3>(2048);
        private readonly List<Vector2> _uvs = new List<Vector2>(2048);
        private readonly List<int>[] _triangles;

        public TransportMeshBuilder(int slotCount)
        {
            _triangles = new List<int>[Mathf.Max(1, slotCount)];
            for (var i = 0; i < _triangles.Length; i++)
                _triangles[i] = new List<int>(256);
        }

        /// <summary>Sonraki eklemelere uygulanan dönüşüm (konum + yön). Varsayılan birim matristir.</summary>
        public Matrix4x4 Matrix { get; set; } = Matrix4x4.identity;

        public int SlotCount => _triangles.Length;

        // ------------------------------------------------------------------ primitives

        /// <summary>Tek üçgen; <paramref name="outward"/> görünür tarafı gösteren yön (dünya/mesh uzayında, dönüşüm sonrası).</summary>
        public void TriangleRaw(int slot, Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
        {
            var normal = Vector3.Cross(b - a, c - a);
            var area = normal.magnitude;
            if (area < 1e-7f)
                return;

            normal /= area;
            if (Vector3.Dot(normal, outward) < 0f)
            {
                (b, c) = (c, b);
                normal = -normal;
            }

            var list = _triangles[ClampSlot(slot)];
            var start = _vertices.Count;
            AddVertex(a, normal);
            AddVertex(b, normal);
            AddVertex(c, normal);
            list.Add(start);
            list.Add(start + 1);
            list.Add(start + 2);
        }

        /// <summary>Dönüşüm uygulanmış dörtgen (iki üçgen). Köşeler çevre sırasıyla verilir.</summary>
        public void Quad(int slot, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outwardLocal)
        {
            var m = Matrix;
            var ta = m.MultiplyPoint3x4(a);
            var tb = m.MultiplyPoint3x4(b);
            var tc = m.MultiplyPoint3x4(c);
            var td = m.MultiplyPoint3x4(d);
            var outward = m.MultiplyVector(outwardLocal);
            TriangleRaw(slot, ta, tb, tc, outward);
            TriangleRaw(slot, ta, tc, td, outward);
        }

        /// <summary>Açık UV'li dörtgen (bayrak gibi dokulu çıkartmalar için). a-b-c-d çevre sırası, uv'ler eşleşir.</summary>
        public void QuadUv(int slot, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 uvA, Vector2 uvB, Vector2 uvC, Vector2 uvD, Vector3 outwardLocal)
        {
            var m = Matrix;
            var ta = m.MultiplyPoint3x4(a);
            var tb = m.MultiplyPoint3x4(b);
            var tc = m.MultiplyPoint3x4(c);
            var td = m.MultiplyPoint3x4(d);
            var outward = m.MultiplyVector(outwardLocal);
            TriangleUv(slot, ta, tb, tc, uvA, uvB, uvC, outward);
            TriangleUv(slot, ta, tc, td, uvA, uvC, uvD, outward);
        }

        /// <summary>Eksen hizalı kutu (yerel uzayda), isteğe bağlı döndürme ile.</summary>
        public void Box(int slot, Vector3 center, Vector3 size, Quaternion rotation)
        {
            var h = size * 0.5f;
            var corners = Corners8;
            corners[0] = center + rotation * new Vector3(-h.x, -h.y, -h.z);
            corners[1] = center + rotation * new Vector3(h.x, -h.y, -h.z);
            corners[2] = center + rotation * new Vector3(h.x, h.y, -h.z);
            corners[3] = center + rotation * new Vector3(-h.x, h.y, -h.z);
            corners[4] = center + rotation * new Vector3(-h.x, -h.y, h.z);
            corners[5] = center + rotation * new Vector3(h.x, -h.y, h.z);
            corners[6] = center + rotation * new Vector3(h.x, h.y, h.z);
            corners[7] = center + rotation * new Vector3(-h.x, h.y, h.z);
            Hexahedron(slot, corners);
        }

        public void Box(int slot, Vector3 center, Vector3 size) => Box(slot, center, size, Quaternion.identity);

        /// <summary>Min/max köşeleriyle eksen hizalı kutu.</summary>
        public void BoxMinMax(int slot, Vector3 min, Vector3 max) => Box(slot, (min + max) * 0.5f, max - min, Quaternion.identity);

        private static readonly Vector3[] Corners8 = new Vector3[8];
        private static readonly Vector3[] QuadA = new Vector3[4];
        private static readonly Vector3[] QuadB = new Vector3[4];

        /// <summary>8 köşeli dışbükey gövde: 0-3 arka yüz (çevre sırası), 4-7 ön yüz (aynı sırayla eşleşen).</summary>
        public void Hexahedron(int slot, Vector3[] corners)
        {
            for (var i = 0; i < 4; i++)
            {
                QuadA[i] = corners[i];
                QuadB[i] = corners[i + 4];
            }

            Loft3D(slot, QuadA, QuadB, true, true);
        }

        /// <summary>
        /// İki eşleşen dışbükey çokgen arasında gövde (köşe sayısı eşit, aynı çevre sırası). Yan yüzler ve isteğe bağlı kapaklar.
        /// Dış yön gövde ağırlık merkezinden hesaplanır (dışbükey kabul).
        /// </summary>
        public void Loft3D(int slot, Vector3[] a, Vector3[] b, bool capA, bool capB)
        {
            var count = Mathf.Min(a.Length, b.Length);
            if (count < 3)
                return;

            var m = Matrix;
            var ta = new Vector3[count];
            var tb = new Vector3[count];
            var centroidA = Vector3.zero;
            var centroidB = Vector3.zero;
            for (var i = 0; i < count; i++)
            {
                ta[i] = m.MultiplyPoint3x4(a[i]);
                tb[i] = m.MultiplyPoint3x4(b[i]);
                centroidA += ta[i];
                centroidB += tb[i];
            }

            centroidA /= count;
            centroidB /= count;
            var centroid = (centroidA + centroidB) * 0.5f;

            for (var i = 0; i < count; i++)
            {
                var j = (i + 1) % count;
                var faceCenter = (ta[i] + ta[j] + tb[i] + tb[j]) * 0.25f;
                var outward = faceCenter - centroid;
                TriangleRaw(slot, ta[i], ta[j], tb[j], outward);
                TriangleRaw(slot, ta[i], tb[j], tb[i], outward);
            }

            if (capA)
                Cap(slot, ta, count, centroidA - centroidB);
            if (capB)
                Cap(slot, tb, count, centroidB - centroidA);
        }

        /// <summary>Z ekseni boyunca, (x,y) kesitleri verilen gövde (kesitler eşleşen dışbükey çokgenler).</summary>
        public void LoftZ(int slot, Vector2[] back, float zBack, Vector2[] front, float zFront, bool capBack, bool capFront)
        {
            var count = Mathf.Min(back.Length, front.Length);
            var a = new Vector3[count];
            var b = new Vector3[count];
            for (var i = 0; i < count; i++)
            {
                a[i] = new Vector3(back[i].x, back[i].y, zBack);
                b[i] = new Vector3(front[i].x, front[i].y, zFront);
            }

            Loft3D(slot, a, b, capBack, capFront);
        }

        /// <summary>X ekseni boyunca, (z,y) profili verilen prizma (yan profil çıkarımı; kapaklar = yan yüzler).</summary>
        public void PrismX(int slot, Vector2[] profileZy, float xMin, float xMax)
        {
            var count = profileZy.Length;
            var a = new Vector3[count];
            var b = new Vector3[count];
            for (var i = 0; i < count; i++)
            {
                a[i] = new Vector3(xMin, profileZy[i].y, profileZy[i].x);
                b[i] = new Vector3(xMax, profileZy[i].y, profileZy[i].x);
            }

            Loft3D(slot, a, b, true, true);
        }

        /// <summary>İki nokta arasında düz yüzlü silindir (boru, direk, egzoz).</summary>
        public void CylinderBetween(int slot, Vector3 from, Vector3 to, float radius, int segments, bool caps = true)
        {
            var axis = to - from;
            var length = axis.magnitude;
            if (length < 1e-5f)
                return;

            var rotation = Quaternion.FromToRotation(Vector3.up, axis / length);
            Cylinder(slot, (from + to) * 0.5f, rotation, radius, radius, length, segments, caps);
        }

        /// <summary>Yerel Y ekseni boyunca silindir/koni (alt yarıçap, üst yarıçap).</summary>
        public void Cylinder(int slot, Vector3 center, Quaternion rotation, float bottomRadius, float topRadius, float height, int segments, bool caps = true)
        {
            segments = Mathf.Clamp(segments, 3, 64);
            var a = new Vector3[segments];
            var b = new Vector3[segments];
            var half = height * 0.5f;
            for (var i = 0; i < segments; i++)
            {
                var angle = (i + 0.5f) / segments * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                a[i] = center + rotation * (dir * bottomRadius + Vector3.down * half);
                b[i] = center + rotation * (dir * topRadius + Vector3.up * half);
            }

            Loft3D(slot, a, b, caps, caps);
        }

        /// <summary>Düşük poligonlu elipsoid (kask, FLIR topu, kubbe).</summary>
        public void Sphere(int slot, Vector3 center, Vector3 radii, int longitude = 8, int latitude = 5)
        {
            longitude = Mathf.Clamp(longitude, 4, 32);
            latitude = Mathf.Clamp(latitude, 2, 16);
            var m = Matrix;
            var tc = m.MultiplyPoint3x4(center);
            for (var lat = 0; lat < latitude; lat++)
            {
                var t0 = Mathf.PI * lat / latitude;
                var t1 = Mathf.PI * (lat + 1) / latitude;
                for (var lon = 0; lon < longitude; lon++)
                {
                    var p0 = Mathf.PI * 2f * lon / longitude;
                    var p1 = Mathf.PI * 2f * (lon + 1) / longitude;
                    var a = m.MultiplyPoint3x4(center + SpherePoint(t0, p0, radii));
                    var b = m.MultiplyPoint3x4(center + SpherePoint(t0, p1, radii));
                    var c = m.MultiplyPoint3x4(center + SpherePoint(t1, p1, radii));
                    var d = m.MultiplyPoint3x4(center + SpherePoint(t1, p0, radii));
                    var faceCenter = (a + b + c + d) * 0.25f;
                    var outward = faceCenter - tc;
                    TriangleRaw(slot, a, b, c, outward);
                    TriangleRaw(slot, a, c, d, outward);
                }
            }
        }

        /// <summary>Düz disk (rotor diski gibi). doubleSided ise iki yüzlü.</summary>
        public void Disc(int slot, Vector3 center, float radius, int segments, bool doubleSided)
        {
            segments = Mathf.Clamp(segments, 3, 128);
            var m = Matrix;
            var tc = m.MultiplyPoint3x4(center);
            var up = m.MultiplyVector(Vector3.up);
            for (var i = 0; i < segments; i++)
            {
                var a0 = Mathf.PI * 2f * i / segments;
                var a1 = Mathf.PI * 2f * (i + 1) / segments;
                var p0 = m.MultiplyPoint3x4(center + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius);
                var p1 = m.MultiplyPoint3x4(center + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius);
                TriangleRaw(slot, tc, p0, p1, up);
                if (doubleSided)
                    TriangleRaw(slot, tc, p0, p1, -up);
            }
        }

        // ------------------------------------------------------------------ build

        /// <summary>Mesh üretir. <paramref name="usedSlots"/> alt-mesh sırasıyla kullanılan yuvalar (malzeme dizisi için).</summary>
        public Mesh Build(string name, out int[] usedSlots)
        {
            var used = new List<int>(_triangles.Length);
            for (var i = 0; i < _triangles.Length; i++)
            {
                if (_triangles[i].Count > 0)
                    used.Add(i);
            }

            usedSlots = used.ToArray();
            var mesh = new Mesh { name = name };
            if (_vertices.Count > 65000)
                mesh.indexFormat = IndexFormat.UInt32;

            mesh.SetVertices(_vertices);
            mesh.SetNormals(_normals);
            mesh.SetUVs(0, _uvs);
            mesh.subMeshCount = Mathf.Max(1, usedSlots.Length);
            for (var i = 0; i < usedSlots.Length; i++)
                mesh.SetTriangles(_triangles[usedSlots[i]], i, false);

            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            return mesh;
        }

        /// <summary>Kullanılan yuvalara göre malzeme dizisi üretir.</summary>
        public static Material[] ResolveMaterials(int[] usedSlots, Material[] slotMaterials)
        {
            if (usedSlots == null || usedSlots.Length == 0)
                return new[] { slotMaterials != null && slotMaterials.Length > 0 ? slotMaterials[0] : null };

            var result = new Material[usedSlots.Length];
            for (var i = 0; i < usedSlots.Length; i++)
            {
                var slot = usedSlots[i];
                result[i] = slotMaterials != null && slot >= 0 && slot < slotMaterials.Length ? slotMaterials[slot] : null;
            }

            return result;
        }

        // ------------------------------------------------------------------ internals

        private void TriangleUv(int slot, Vector3 a, Vector3 b, Vector3 c, Vector2 uvA, Vector2 uvB, Vector2 uvC, Vector3 outward)
        {
            var normal = Vector3.Cross(b - a, c - a);
            var area = normal.magnitude;
            if (area < 1e-7f)
                return;

            normal /= area;
            if (Vector3.Dot(normal, outward) < 0f)
            {
                (b, c) = (c, b);
                (uvB, uvC) = (uvC, uvB);
                normal = -normal;
            }

            var list = _triangles[ClampSlot(slot)];
            var start = _vertices.Count;
            _vertices.Add(a);
            _vertices.Add(b);
            _vertices.Add(c);
            _normals.Add(normal);
            _normals.Add(normal);
            _normals.Add(normal);
            _uvs.Add(uvA);
            _uvs.Add(uvB);
            _uvs.Add(uvC);
            list.Add(start);
            list.Add(start + 1);
            list.Add(start + 2);
        }

        private void Cap(int slot, Vector3[] polygon, int count, Vector3 outward)
        {
            for (var i = 1; i < count - 1; i++)
                TriangleRaw(slot, polygon[0], polygon[i], polygon[i + 1], outward);
        }

        private void AddVertex(Vector3 position, Vector3 normal)
        {
            _vertices.Add(position);
            _normals.Add(normal);
            Vector2 uv;
            var ax = Mathf.Abs(normal.x);
            var ay = Mathf.Abs(normal.y);
            var az = Mathf.Abs(normal.z);
            if (ax >= ay && ax >= az)
                uv = new Vector2(position.z, position.y);
            else if (ay >= az)
                uv = new Vector2(position.x, position.z);
            else
                uv = new Vector2(position.x, position.y);
            _uvs.Add(uv * UvScale);
        }

        private int ClampSlot(int slot) => slot < 0 ? 0 : slot >= _triangles.Length ? _triangles.Length - 1 : slot;

        private static Vector3 SpherePoint(float theta, float phi, Vector3 radii)
        {
            var s = Mathf.Sin(theta);
            return new Vector3(s * Mathf.Cos(phi) * radii.x, Mathf.Cos(theta) * radii.y, s * Mathf.Sin(phi) * radii.z);
        }
    }

    /// <summary>Önbelleğe alınmış paylaşımlı mesh + yuva eşlemesi.</summary>
    internal sealed class CachedVehicleMesh
    {
        public Mesh Mesh;
        public int[] Slots;

        public bool IsValid => Mesh != null && Slots != null;
    }
}
