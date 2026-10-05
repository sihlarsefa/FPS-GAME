using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Infrastructure.Loot
{
    /// <summary>
    /// Yerdeki eşya modelleri için küçük, düşük poligonlu ağ oluşturucu. Her malzeme bir alt ağdır; sonuç tek bir
    /// paylaşılan Mesh + malzeme dizisidir (eşya başına tek MeshRenderer → az GameObject, az çizim çağrısı).
    /// Tüm şekiller <see cref="Matrix"/> ile dönüştürülür (ör. silahı yan yatırmak için).
    /// </summary>
    internal sealed class LootMeshBuilder
    {
        private readonly List<Vector3> _vertices = new(512);
        private readonly List<Vector3> _normals = new(512);
        private readonly List<Vector2> _uvs = new(512);
        private readonly List<Material> _materials = new(8);
        private readonly List<List<int>> _triangles = new(8);

        // AppendMesh için yeniden kullanılan geçici listeler.
        private static readonly List<Vector3> TmpVertices = new(1024);
        private static readonly List<Vector3> TmpNormals = new(1024);
        private static readonly List<Vector2> TmpUvs = new(1024);
        private static readonly List<int> TmpTriangles = new(2048);

        /// <summary>Eklenen tüm geometriye uygulanan dönüşüm.</summary>
        public Matrix4x4 Matrix = Matrix4x4.identity;

        public int VertexCount => _vertices.Count;
        public int MaterialCount => _materials.Count;

        public void Clear()
        {
            _vertices.Clear();
            _normals.Clear();
            _uvs.Clear();
            _materials.Clear();
            _triangles.Clear();
            Matrix = Matrix4x4.identity;
        }

        // ------------------------------------------------------------------ Primitives

        /// <summary>Düz gölgeli kutu (merkez, boyut, dönüş).</summary>
        public void Box(Vector3 center, Vector3 size, Quaternion rotation, Material material)
        {
            var tris = Tris(material);
            if (tris == null)
                return;

            var half = size * 0.5f;
            Face(tris, center, rotation, half, Vector3.right, Vector3.forward, Vector3.up);
            Face(tris, center, rotation, half, Vector3.left, Vector3.forward, Vector3.up);
            Face(tris, center, rotation, half, Vector3.up, Vector3.right, Vector3.forward);
            Face(tris, center, rotation, half, Vector3.down, Vector3.right, Vector3.forward);
            Face(tris, center, rotation, half, Vector3.forward, Vector3.right, Vector3.up);
            Face(tris, center, rotation, half, Vector3.back, Vector3.right, Vector3.up);
        }

        public void Box(Vector3 center, Vector3 size, Material material) => Box(center, size, Quaternion.identity, material);

        /// <summary>Yerel Y ekseni boyunca silindir (yumuşak yan yüzey, düz kapaklar).</summary>
        public void Cylinder(Vector3 center, float radius, float height, Quaternion rotation, int segments, Material material,
            bool capTop = true, bool capBottom = true)
        {
            var tris = Tris(material);
            if (tris == null || radius <= 0f || height <= 0f)
                return;

            segments = Mathf.Clamp(segments, 3, 48);
            var halfHeight = height * 0.5f;
            var circumference = 2f * Mathf.PI * radius;

            var sideStart = _vertices.Count;
            for (var i = 0; i <= segments; i++)
            {
                var angle = i * Mathf.PI * 2f / segments;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var u = (float)i / segments * circumference * 2f;
                AddVertex(center + rotation * (dir * radius + Vector3.down * halfHeight), rotation * dir, new Vector2(u, 0f));
                AddVertex(center + rotation * (dir * radius + Vector3.up * halfHeight), rotation * dir, new Vector2(u, height * 2f));
            }

            for (var i = 0; i < segments; i++)
            {
                var b0 = sideStart + i * 2;
                var t0 = b0 + 1;
                var b1 = b0 + 2;
                var t1 = b0 + 3;
                var mid = (i + 0.5f) * Mathf.PI * 2f / segments;
                var outward = rotation * new Vector3(Mathf.Cos(mid), 0f, Mathf.Sin(mid));
                Triangle(tris, b0, t0, b1, outward);
                Triangle(tris, b1, t0, t1, outward);
            }

            if (capTop)
                Disc(tris, center + rotation * (Vector3.up * halfHeight), radius, rotation, segments, rotation * Vector3.up);
            if (capBottom)
                Disc(tris, center + rotation * (Vector3.down * halfHeight), radius, rotation, segments, rotation * Vector3.down);
        }

        /// <summary>Elipsoit (enlem/boylam). radii: yerel yarıçaplar.</summary>
        public void Ellipsoid(Vector3 center, Vector3 radii, Quaternion rotation, int segments, int rings, Material material)
        {
            SphereBand(center, radii, rotation, segments, rings, -90f, 90f, material);
        }

        /// <summary>Kubbe (üst yarım elipsoit, tabanı açık). Taban merkezde, +Y yukarı.</summary>
        public void Dome(Vector3 center, float radius, float height, Quaternion rotation, int segments, int rings, Material material)
        {
            SphereBand(center, new Vector3(radius, height, radius), rotation, segments, rings, 0f, 90f, material);
        }

        /// <summary>Düz halka (yerel +Y'ye bakar).</summary>
        public void Ring(Vector3 center, float innerRadius, float outerRadius, Quaternion rotation, int segments, Material material)
        {
            var tris = Tris(material);
            if (tris == null || outerRadius <= innerRadius)
                return;

            segments = Mathf.Clamp(segments, 3, 96);
            var normal = rotation * Vector3.up;
            var start = _vertices.Count;
            for (var i = 0; i <= segments; i++)
            {
                var angle = i * Mathf.PI * 2f / segments;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                AddVertex(center + rotation * (dir * innerRadius), normal, new Vector2((float)i / segments, 0f));
                AddVertex(center + rotation * (dir * outerRadius), normal, new Vector2((float)i / segments, 1f));
            }

            for (var i = 0; i < segments; i++)
            {
                var i0 = start + i * 2;
                var o0 = i0 + 1;
                var i1 = i0 + 2;
                var o1 = i0 + 3;
                Triangle(tris, i0, o0, i1, normal);
                Triangle(tris, i1, o0, o1, normal);
            }
        }

        /// <summary>
        /// Kızılay tarzı hilal (yerel XZ düzleminde, +Y'ye bakar, açıklığı +X yönünde). Dış daire R, iç daire 0.78R,
        /// merkezler arası 0.32R.
        /// </summary>
        public void Crescent(Vector3 center, float radius, Quaternion rotation, int segments, Material material)
        {
            var tris = Tris(material);
            if (tris == null || radius <= 0f)
                return;

            segments = Mathf.Clamp(segments, 6, 64);
            var outerR = radius;
            var innerR = radius * 0.78f;
            var offset = radius * 0.32f;

            // Dairelerin kesişimi.
            var ix = (outerR * outerR - innerR * innerR + offset * offset) / (2f * offset);
            var iy = Mathf.Sqrt(Mathf.Max(0f, outerR * outerR - ix * ix));
            var phi = Mathf.Atan2(iy, ix);              // dış daire üzerinde
            var psi = Mathf.Atan2(iy, ix - offset);     // iç daire üzerinde
            var normal = rotation * Vector3.up;

            var start = _vertices.Count;
            for (var i = 0; i <= segments; i++)
            {
                var t = (float)i / segments;
                var a = Mathf.Lerp(phi, Mathf.PI * 2f - phi, t);
                var b = Mathf.Lerp(psi, Mathf.PI * 2f - psi, t);
                var outer = new Vector3(Mathf.Cos(a) * outerR, 0f, Mathf.Sin(a) * outerR);
                var inner = new Vector3(offset + Mathf.Cos(b) * innerR, 0f, Mathf.Sin(b) * innerR);
                AddVertex(center + rotation * outer, normal, new Vector2(t, 1f));
                AddVertex(center + rotation * inner, normal, new Vector2(t, 0f));
            }

            for (var i = 0; i < segments; i++)
            {
                var o0 = start + i * 2;
                var n0 = o0 + 1;
                var o1 = o0 + 2;
                var n1 = o0 + 3;
                Triangle(tris, o0, n0, o1, normal);
                Triangle(tris, o1, n0, n1, normal);
            }
        }

        /// <summary>0..1 UV'li düz dörtgen (yerel +Y'ye bakar; ör. bayrak arması).</summary>
        public void Patch(Vector3 center, Vector2 size, Quaternion rotation, Material material)
        {
            var tris = Tris(material);
            if (tris == null)
                return;

            var hx = size.x * 0.5f;
            var hz = size.y * 0.5f;
            var normal = rotation * Vector3.up;
            var start = _vertices.Count;
            AddVertex(center + rotation * new Vector3(-hx, 0f, -hz), normal, new Vector2(0f, 0f));
            AddVertex(center + rotation * new Vector3(hx, 0f, -hz), normal, new Vector2(1f, 0f));
            AddVertex(center + rotation * new Vector3(hx, 0f, hz), normal, new Vector2(1f, 1f));
            AddVertex(center + rotation * new Vector3(-hx, 0f, hz), normal, new Vector2(0f, 1f));
            Triangle(tris, start, start + 1, start + 2, normal);
            Triangle(tris, start, start + 2, start + 3, normal);
        }

        /// <summary>
        /// Okunabilir bir ağı (tüm alt ağlarıyla) ekler. matrix: ağın yerel uzayından oluşturucu uzayına.
        /// Okunamayan ağda false döner (hiçbir şey eklenmez).
        /// </summary>
        public bool AppendMesh(Mesh mesh, Matrix4x4 matrix, Material[] materials)
        {
            if (mesh == null)
                return true;
            if (!mesh.isReadable)
                return false;

            TmpVertices.Clear();
            TmpNormals.Clear();
            TmpUvs.Clear();
            mesh.GetVertices(TmpVertices);
            mesh.GetNormals(TmpNormals);
            mesh.GetUVs(0, TmpUvs);
            if (TmpVertices.Count == 0)
                return true;

            var full = Matrix * matrix;
            var normalMatrix = full.inverse.transpose;
            var hasNormals = TmpNormals.Count == TmpVertices.Count;
            var hasUvs = TmpUvs.Count == TmpVertices.Count;
            var subMeshCount = mesh.subMeshCount;

            for (var s = 0; s < subMeshCount; s++)
            {
                if (mesh.GetTopology(s) != MeshTopology.Triangles)
                    continue;

                Material material = null;
                if (materials != null && materials.Length > 0)
                    material = materials[Mathf.Min(s, materials.Length - 1)];
                var tris = Tris(material);
                if (tris == null)
                    continue;

                TmpTriangles.Clear();
                mesh.GetTriangles(TmpTriangles, s);
                var baseIndex = _vertices.Count;
                // Alt ağ başına tüm köşeleri kopyala (basit ve güvenli; modeller küçük).
                for (var v = 0; v < TmpVertices.Count; v++)
                {
                    var normal = hasNormals ? normalMatrix.MultiplyVector(TmpNormals[v]).normalized : Vector3.up;
                    _vertices.Add(full.MultiplyPoint3x4(TmpVertices[v]));
                    _normals.Add(normal);
                    _uvs.Add(hasUvs ? TmpUvs[v] : Vector2.zero);
                }

                // Aynalama (negatif ölçek) varsa sarım yönünü çevir.
                var flip = full.determinant < 0f;
                for (var t = 0; t + 2 < TmpTriangles.Count; t += 3)
                {
                    tris.Add(baseIndex + TmpTriangles[t]);
                    if (flip)
                    {
                        tris.Add(baseIndex + TmpTriangles[t + 2]);
                        tris.Add(baseIndex + TmpTriangles[t + 1]);
                    }
                    else
                    {
                        tris.Add(baseIndex + TmpTriangles[t + 1]);
                        tris.Add(baseIndex + TmpTriangles[t + 2]);
                    }
                }
            }

            return true;
        }

        // ------------------------------------------------------------------ Result

        public Bounds ComputeBounds()
        {
            if (_vertices.Count == 0)
                return new Bounds(Vector3.zero, Vector3.zero);

            var min = _vertices[0];
            var max = min;
            for (var i = 1; i < _vertices.Count; i++)
            {
                min = Vector3.Min(min, _vertices[i]);
                max = Vector3.Max(max, _vertices[i]);
            }

            var bounds = new Bounds();
            bounds.SetMinMax(min, max);
            return bounds;
        }

        /// <summary>Modeli zemine oturtur: XZ ortalanır, en alt nokta y=0 olur.</summary>
        public void RestOnGround()
        {
            if (_vertices.Count == 0)
                return;

            var bounds = ComputeBounds();
            var offset = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
            for (var i = 0; i < _vertices.Count; i++)
                _vertices[i] += offset;
        }

        /// <summary>Sonuç ağını üretir. Boş oluşturucuda null döner.</summary>
        public Mesh Build(string name, out Material[] materials)
        {
            var used = 0;
            for (var i = 0; i < _triangles.Count; i++)
            {
                if (_triangles[i].Count > 0)
                    used++;
            }

            if (_vertices.Count == 0 || used == 0)
            {
                materials = System.Array.Empty<Material>();
                return null;
            }

            var mesh = new Mesh { name = name };
            if (_vertices.Count > 65000)
                mesh.indexFormat = IndexFormat.UInt32;

            mesh.SetVertices(_vertices);
            mesh.SetNormals(_normals);
            mesh.SetUVs(0, _uvs);
            mesh.subMeshCount = used;
            materials = new Material[used];
            var sub = 0;
            for (var i = 0; i < _triangles.Count; i++)
            {
                if (_triangles[i].Count == 0)
                    continue;

                mesh.SetTriangles(_triangles[i], sub, false);
                materials[sub] = _materials[i];
                sub++;
            }

            mesh.RecalculateBounds();
            return mesh;
        }

        // ------------------------------------------------------------------ Internals

        private List<int> Tris(Material material)
        {
            if (material == null)
                return null;

            for (var i = 0; i < _materials.Count; i++)
            {
                if (ReferenceEquals(_materials[i], material))
                    return _triangles[i];
            }

            var list = new List<int>(256);
            _materials.Add(material);
            _triangles.Add(list);
            return list;
        }

        private void AddVertex(Vector3 position, Vector3 normal, Vector2 uv)
        {
            _vertices.Add(Matrix.MultiplyPoint3x4(position));
            _normals.Add(Matrix.MultiplyVector(normal).normalized);
            _uvs.Add(uv);
        }

        /// <summary>Üçgeni, yüz normali istenen yöne bakacak sarımla ekler (Unity: normal = Cross(b-a, c-a)).</summary>
        private void Triangle(List<int> tris, int a, int b, int c, Vector3 intendedNormal)
        {
            var pa = _vertices[a];
            var cross = Vector3.Cross(_vertices[b] - pa, _vertices[c] - pa);
            var desired = Matrix.MultiplyVector(intendedNormal);
            tris.Add(a);
            if (Vector3.Dot(cross, desired) >= 0f)
            {
                tris.Add(b);
                tris.Add(c);
            }
            else
            {
                tris.Add(c);
                tris.Add(b);
            }
        }

        private void Face(List<int> tris, Vector3 center, Quaternion rotation, Vector3 half, Vector3 n, Vector3 u, Vector3 v)
        {
            var normal = rotation * n;
            var hu = Mathf.Abs(Vector3.Dot(half, u));
            var hv = Mathf.Abs(Vector3.Dot(half, v));
            var start = _vertices.Count;
            for (var k = 0; k < 4; k++)
            {
                var su = k == 1 || k == 2 ? 1f : -1f;
                var sv = k >= 2 ? 1f : -1f;
                var local = Vector3.Scale(n + u * su + v * sv, half);
                var uv = new Vector2((su * hu + hu) * 2f, (sv * hv + hv) * 2f);
                AddVertex(center + rotation * local, normal, uv);
            }

            Triangle(tris, start, start + 1, start + 2, normal);
            Triangle(tris, start, start + 2, start + 3, normal);
        }

        private void Disc(List<int> tris, Vector3 center, float radius, Quaternion rotation, int segments, Vector3 normal)
        {
            var hub = _vertices.Count;
            AddVertex(center, normal, new Vector2(0.5f, 0.5f));
            for (var i = 0; i <= segments; i++)
            {
                var angle = i * Mathf.PI * 2f / segments;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                AddVertex(center + rotation * (dir * radius), normal, new Vector2(0.5f + dir.x * 0.5f, 0.5f + dir.z * 0.5f));
            }

            for (var i = 0; i < segments; i++)
                Triangle(tris, hub, hub + 1 + i, hub + 2 + i, normal);
        }

        private void SphereBand(Vector3 center, Vector3 radii, Quaternion rotation, int segments, int rings, float fromLatitude,
            float toLatitude, Material material)
        {
            var tris = Tris(material);
            if (tris == null)
                return;

            segments = Mathf.Clamp(segments, 4, 48);
            rings = Mathf.Clamp(rings, 1, 32);
            var start = _vertices.Count;
            for (var r = 0; r <= rings; r++)
            {
                var lat = Mathf.Lerp(fromLatitude, toLatitude, (float)r / rings) * Mathf.Deg2Rad;
                var cosLat = Mathf.Cos(lat);
                var sinLat = Mathf.Sin(lat);
                for (var s = 0; s <= segments; s++)
                {
                    var lon = s * Mathf.PI * 2f / segments;
                    var unit = new Vector3(Mathf.Cos(lon) * cosLat, sinLat, Mathf.Sin(lon) * cosLat);
                    var local = Vector3.Scale(unit, radii);
                    var normal = new Vector3(
                        radii.x > 0f ? unit.x / radii.x : 0f,
                        radii.y > 0f ? unit.y / radii.y : 0f,
                        radii.z > 0f ? unit.z / radii.z : 0f).normalized;
                    AddVertex(center + rotation * local, rotation * normal, new Vector2((float)s / segments, (float)r / rings));
                }
            }

            var stride = segments + 1;
            for (var r = 0; r < rings; r++)
            {
                for (var s = 0; s < segments; s++)
                {
                    var a = start + r * stride + s;
                    var b = a + 1;
                    var c = a + stride;
                    var d = c + 1;
                    var midLat = Mathf.Lerp(fromLatitude, toLatitude, (r + 0.5f) / rings) * Mathf.Deg2Rad;
                    var midLon = (s + 0.5f) * Mathf.PI * 2f / segments;
                    var outward = rotation * new Vector3(Mathf.Cos(midLon) * Mathf.Cos(midLat), Mathf.Sin(midLat),
                        Mathf.Sin(midLon) * Mathf.Cos(midLat));

                    // Kutuplardaki dejenere üçgenleri atla.
                    if ((_vertices[a] - _vertices[b]).sqrMagnitude > 1e-12f)
                        Triangle(tris, a, b, c, outward);
                    if ((_vertices[c] - _vertices[d]).sqrMagnitude > 1e-12f)
                        Triangle(tris, b, d, c, outward);
                }
            }
        }
    }
}
