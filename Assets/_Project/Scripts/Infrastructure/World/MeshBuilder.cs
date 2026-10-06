using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// Basit prosedürel mesh yazıcı: alt mesh (submesh) destekli köşe/üçgen birikimi, düz gölgeli (low-poly) üçgen/dörtgen
    /// yardımcıları. Üretim zamanında kullanılır (her karede değil).
    /// </summary>
    public sealed class MeshBuilder
    {
        private readonly List<Vector3> _vertices = new List<Vector3>(256);
        private readonly List<Vector3> _normals = new List<Vector3>(256);
        private readonly List<Vector2> _uvs = new List<Vector2>(256);
        private readonly List<Color32> _colors = new List<Color32>(256);
        private readonly List<List<int>> _submeshes = new List<List<int>>(2);
        private bool _useColors;

        public MeshBuilder(int submeshCount = 1)
        {
            for (var i = 0; i < Mathf.Max(1, submeshCount); i++)
                _submeshes.Add(new List<int>(256));
        }

        public int VertexCount => _vertices.Count;
        public int SubmeshCount => _submeshes.Count;

        /// <summary>Sonraki köşelere uygulanacak köşe rengi (varsayılan beyaz; ilk kullanımda renk kanalı açılır).</summary>
        public Color32 CurrentColor { get; set; } = new Color32(255, 255, 255, 255);

        public void EnableColors()
        {
            if (_useColors)
                return;
            _useColors = true;
            while (_colors.Count < _vertices.Count)
                _colors.Add(new Color32(255, 255, 255, 255));
        }

        public void Clear()
        {
            _vertices.Clear();
            _normals.Clear();
            _uvs.Clear();
            _colors.Clear();
            for (var i = 0; i < _submeshes.Count; i++)
                _submeshes[i].Clear();
        }

        public int AddVertex(Vector3 position, Vector3 normal, Vector2 uv)
        {
            _vertices.Add(position);
            _normals.Add(normal);
            _uvs.Add(uv);
            if (_useColors)
                _colors.Add(CurrentColor);
            return _vertices.Count - 1;
        }

        public void AddTriangle(int submesh, int a, int b, int c)
        {
            var list = _submeshes[Mathf.Clamp(submesh, 0, _submeshes.Count - 1)];
            list.Add(a);
            list.Add(b);
            list.Add(c);
        }

        /// <summary>Düz gölgeli üçgen (saat yönü = ön yüz, Unity sol el kuralı). UV: dünya ölçekli düzlemsel izdüşüm.</summary>
        public void AddFlatTriangle(int submesh, Vector3 a, Vector3 b, Vector3 c, float uvScale = 1f)
        {
            var normal = Vector3.Cross(b - a, c - a);
            if (normal.sqrMagnitude < 1e-12f)
                return;
            normal.Normalize();
            var i0 = AddVertex(a, normal, PlanarUV(a, normal, uvScale));
            var i1 = AddVertex(b, normal, PlanarUV(b, normal, uvScale));
            var i2 = AddVertex(c, normal, PlanarUV(c, normal, uvScale));
            AddTriangle(submesh, i0, i1, i2);
        }

        /// <summary>Düz gölgeli dörtgen (a-b-c-d saat yönünde).</summary>
        public void AddFlatQuad(int submesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d, float uvScale = 1f)
        {
            var normal = Vector3.Cross(b - a, c - a) + Vector3.Cross(c - a, d - a);
            if (normal.sqrMagnitude < 1e-12f)
                return;
            normal.Normalize();
            var i0 = AddVertex(a, normal, PlanarUV(a, normal, uvScale));
            var i1 = AddVertex(b, normal, PlanarUV(b, normal, uvScale));
            var i2 = AddVertex(c, normal, PlanarUV(c, normal, uvScale));
            var i3 = AddVertex(d, normal, PlanarUV(d, normal, uvScale));
            AddTriangle(submesh, i0, i1, i2);
            AddTriangle(submesh, i0, i2, i3);
        }

        /// <summary>Dörtgen, verilen UV'lerle (a-b-c-d saat yönünde).</summary>
        public void AddQuad(int submesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 uvA, Vector2 uvB, Vector2 uvC, Vector2 uvD)
        {
            var normal = Vector3.Cross(b - a, c - a) + Vector3.Cross(c - a, d - a);
            if (normal.sqrMagnitude < 1e-12f)
                return;
            normal.Normalize();
            var i0 = AddVertex(a, normal, uvA);
            var i1 = AddVertex(b, normal, uvB);
            var i2 = AddVertex(c, normal, uvC);
            var i3 = AddVertex(d, normal, uvD);
            AddTriangle(submesh, i0, i1, i2);
            AddTriangle(submesh, i0, i2, i3);
        }

        /// <summary>Başka bir mesh'in köşelerini dönüşümle ekler (alt mesh eşlemesi: kaynak i → hedef map[i] ya da submesh).</summary>
        public void Append(Mesh mesh, Matrix4x4 matrix, int targetSubmesh)
        {
            if (mesh == null)
                return;

            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var uvs = mesh.uv;
            var baseIndex = _vertices.Count;
            var normalMatrix = matrix.inverse.transpose;
            for (var i = 0; i < vertices.Length; i++)
            {
                var n = normals != null && normals.Length == vertices.Length ? normalMatrix.MultiplyVector(normals[i]).normalized : Vector3.up;
                var uv = uvs != null && uvs.Length == vertices.Length ? uvs[i] : Vector2.zero;
                AddVertex(matrix.MultiplyPoint3x4(vertices[i]), n, uv);
            }

            var flip = matrix.determinant < 0f;
            for (var s = 0; s < mesh.subMeshCount; s++)
            {
                var tris = mesh.GetTriangles(s);
                for (var t = 0; t + 2 < tris.Length; t += 3)
                {
                    if (flip)
                        AddTriangle(targetSubmesh, baseIndex + tris[t], baseIndex + tris[t + 2], baseIndex + tris[t + 1]);
                    else
                        AddTriangle(targetSubmesh, baseIndex + tris[t], baseIndex + tris[t + 1], baseIndex + tris[t + 2]);
                }
            }
        }

        /// <summary>Append gibi, ama kaynak alt mesh i → hedef submeshMap[i] (harita kısaysa son girdi kullanılır).</summary>
        public void AppendMapped(Mesh mesh, Matrix4x4 matrix, int[] submeshMap)
        {
            if (mesh == null || submeshMap == null || submeshMap.Length == 0)
                return;

            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var uvs = mesh.uv;
            var baseIndex = _vertices.Count;
            var normalMatrix = matrix.inverse.transpose;
            for (var i = 0; i < vertices.Length; i++)
            {
                var n = normals != null && normals.Length == vertices.Length ? normalMatrix.MultiplyVector(normals[i]).normalized : Vector3.up;
                var uv = uvs != null && uvs.Length == vertices.Length ? uvs[i] : Vector2.zero;
                AddVertex(matrix.MultiplyPoint3x4(vertices[i]), n, uv);
            }

            var flip = matrix.determinant < 0f;
            for (var s = 0; s < mesh.subMeshCount; s++)
            {
                var target = submeshMap[Mathf.Min(s, submeshMap.Length - 1)];
                var tris = mesh.GetTriangles(s);
                for (var t = 0; t + 2 < tris.Length; t += 3)
                {
                    if (flip)
                        AddTriangle(target, baseIndex + tris[t], baseIndex + tris[t + 2], baseIndex + tris[t + 1]);
                    else
                        AddTriangle(target, baseIndex + tris[t], baseIndex + tris[t + 1], baseIndex + tris[t + 2]);
                }
            }
        }

        public Mesh ToMesh(string name)
        {
            SanitizeNonFinite(name);
            var mesh = new Mesh { name = name };
            if (_vertices.Count > 65000)
                mesh.indexFormat = IndexFormat.UInt32;

            mesh.SetVertices(_vertices);
            mesh.SetNormals(_normals);
            mesh.SetUVs(0, _uvs);
            if (_useColors && _colors.Count == _vertices.Count)
                mesh.SetColors(_colors);

            mesh.subMeshCount = _submeshes.Count;
            for (var i = 0; i < _submeshes.Count; i++)
                mesh.SetTriangles(_submeshes[i], i, false);

            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        /// <summary>Köşe konumu/normali/UV'si NaN veya sonsuz mu.</summary>
        public static bool IsFinite(Vector3 v) =>
            !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
              float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));

        /// <summary>
        /// Geçersiz (NaN/sonsuz) köşe içeren üçgenleri atar, uyarı yazar ve bozuk köşeleri sıfırlar
        /// ("abnormal mesh bounds" uyarısını kaynağında önler). Atılan üçgen sayısını döndürür.
        /// </summary>
        public int SanitizeNonFinite(string name = null)
        {
            var bad = new bool[_vertices.Count];
            var badVerts = 0;
            for (var i = 0; i < _vertices.Count; i++)
            {
                if (IsFinite(_vertices[i]) && IsFinite(_normals[i]) && !float.IsNaN(_uvs[i].x) && !float.IsNaN(_uvs[i].y))
                    continue;
                bad[i] = true;
                badVerts++;
                _vertices[i] = Vector3.zero;
                _normals[i] = Vector3.up;
                _uvs[i] = Vector2.zero;
            }

            if (badVerts == 0)
                return 0;

            var dropped = 0;
            for (var s = 0; s < _submeshes.Count; s++)
            {
                var list = _submeshes[s];
                var w = 0;
                for (var t = 0; t + 2 < list.Count; t += 3)
                {
                    if (bad[list[t]] || bad[list[t + 1]] || bad[list[t + 2]])
                    {
                        dropped++;
                        continue;
                    }

                    list[w++] = list[t];
                    list[w++] = list[t + 1];
                    list[w++] = list[t + 2];
                }

                list.RemoveRange(w, list.Count - w);
            }

            Debug.LogWarning("[MeshBuilder] '" + name + "': " + badVerts + " geçersiz köşe (NaN/inf), " + dropped + " üçgen atlandı.");
            return dropped;
        }

        public static Vector2 PlanarUV(Vector3 p, Vector3 normal, float scale)
        {
            var ax = Mathf.Abs(normal.x);
            var ay = Mathf.Abs(normal.y);
            var az = Mathf.Abs(normal.z);
            if (ay >= ax && ay >= az)
                return new Vector2(p.x, p.z) * scale;
            if (ax >= az)
                return new Vector2(p.z, p.y) * scale;
            return new Vector2(p.x, p.y) * scale;
        }
    }
}
