using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Infrastructure.Weapons
{
    /// <summary>Kurulmuş bir mesh parçası: pivot konumu (model uzayı), mesh ve alt-mesh başına malzemeler.</summary>
    internal sealed class BuiltMeshPart
    {
        public string Name;
        public Vector3 Pivot;
        public Mesh Mesh;
        public Material[] Materials;

        public bool IsValid => Mesh != null && Materials != null && Materials.Length > 0;
    }

    /// <summary>
    /// Düşük poligonlu, düz gölgeli prosedürel mesh kurucu. Şekiller (kutu, konik kutu, 8 köşeli gövde, silindir, boru,
    /// elipsoit) model uzayında verilir; her şekil seçili gruba (parçaya) ve malzemeye göre alt-meshlere toplanır.
    /// Yüz sargısı her yüz için iç noktaya göre otomatik düzeltilir (Unity: saat yönü = ön yüz), bu yüzden şekiller
    /// aynalanabilir ya da serbestçe döndürülebilir. Yalnızca kurulumda kullanılır (sıcak yolda değil).
    /// </summary>
    internal sealed class WeaponMeshBuilder
    {
        public const string BodyGroup = "Body";

        private const float UvScale = 5f;
        private static readonly Vector2 NoEdge = new Vector2(1000f, 1000f);

        private sealed class Section
        {
            public Material Material;
            public readonly List<int> Triangles = new List<int>(96);
        }

        private sealed class Group
        {
            public string Name;
            public Vector3 Pivot;
            public readonly List<Vector3> Vertices = new List<Vector3>(256);
            public readonly List<Vector3> Normals = new List<Vector3>(256);
            public readonly List<Vector2> Uvs = new List<Vector2>(256);
            public readonly List<Color32> Colors = new List<Color32>(0);
            // Silah gölgelendiricisi için: UV1 = dörtgen yerel (u,v), UV2 = dörtgen boyutu (m). Boyut >= 500 ya da 0 = kenar aşınması yok.
            public readonly List<Vector2> QuadUvs = new List<Vector2>(256);
            public readonly List<Vector2> QuadSizes = new List<Vector2>(256);
            public readonly List<Section> Sections = new List<Section>(4);
        }

        private readonly List<Group> _groups = new List<Group>(4);
        private readonly Vector3[] _corners = new Vector3[8];
        private readonly List<Vector3> _ringA = new List<Vector3>(16);
        private readonly List<Vector3> _ringB = new List<Vector3>(16);
        private readonly List<Vector3> _ringC = new List<Vector3>(16);
        private readonly List<Vector3> _ringD = new List<Vector3>(16);
        private Group _current;
        private Matrix4x4 _matrix = Matrix4x4.identity;
        private bool _hasMatrix;

        public WeaponMeshBuilder()
        {
            BeginGroup(BodyGroup, Vector3.zero);
        }

        /// <summary>X eksenini aynalar (sol el / sol taraf parçaları için). Sargı otomatik düzeltilir.</summary>
        public bool MirrorX { get; set; }

        /// <summary>Bundan sonraki şekillere uygulanacak ek dönüşüm (alt montajlar için). null = birim.</summary>
        public void SetTransform(Vector3 position, Quaternion rotation)
        {
            _matrix = Matrix4x4.TRS(position, rotation, Vector3.one);
            _hasMatrix = true;
        }

        public void ClearTransform()
        {
            _matrix = Matrix4x4.identity;
            _hasMatrix = false;
        }

        /// <summary>Adlandırılmış bir gruba (ayrı GameObject olacak hareketli parça) geçer; pivot model uzayındadır.</summary>
        public void BeginGroup(string name, Vector3 pivot)
        {
            for (var i = 0; i < _groups.Count; i++)
            {
                if (_groups[i].Name == name)
                {
                    _current = _groups[i];
                    return;
                }
            }

            _current = new Group { Name = name, Pivot = pivot };
            _groups.Add(_current);
        }

        public void EndGroup() => BeginGroup(BodyGroup, Vector3.zero);

        // ------------------------------------------------------------------ Shapes

        public void Box(Material m, Vector3 center, Vector3 size) => Box(m, center, size, Quaternion.identity);

        public void Box(Material m, Vector3 center, Vector3 size, Vector3 euler) => Box(m, center, size, Quaternion.Euler(euler));

        public void Box(Material m, Vector3 center, Vector3 size, Quaternion rotation)
        {
            var h = size * 0.5f;
            var radius = Mathf.Min(0.0012f, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.22f);
            if (radius <= 0f) return;
            var inner = h - Vector3.one * radius;
            var points = new Vector3[4];
            var normals = new Vector3[4];
            for (var axis = 0; axis < 3; axis++)
            {
                var u = (axis + 1) % 3; var v = (axis + 2) % 3;
                for (var sign = -1; sign <= 1; sign += 2)
                for (var y = 0; y < 3; y++)
                for (var x = 0; x < 3; x++)
                {
                    for (var c = 0; c < 4; c++)
                    {
                        var ix = x + (c == 1 || c == 2 ? 1 : 0);
                        var iy = y + (c >= 2 ? 1 : 0);
                        var q = Vector3.zero;
                        q[axis] = sign * h[axis];
                        q[u] = BevelGrid(ix, h[u], inner[u]); q[v] = BevelGrid(iy, h[v], inner[v]);
                        var near = new Vector3(Mathf.Clamp(q.x, -inner.x, inner.x), Mathf.Clamp(q.y, -inner.y, inner.y),
                            Mathf.Clamp(q.z, -inner.z, inner.z));
                        var n = (q - near).normalized;
                        points[c] = center + rotation * (near + n * radius);
                        normals[c] = rotation * n;
                    }
                    QuadSmooth(m, points[0], points[1], points[2], points[3],
                        normals[0], normals[1], normals[2], normals[3], center, true);
                }
            }
        }

        /// <summary>Toplam köşe sayısı (tüm gruplar). Silah başına köşe bütçesi (WeaponDetailBudget) için.</summary>
        public int VertexCount
        {
            get
            {
                var total = 0;
                for (var i = 0; i < _groups.Count; i++) total += _groups[i].Vertices.Count;
                return total;
            }
        }

        /// <summary>Pahsız ucuz kutu: 24 köşe (Box 216 köşe). Ray dişi, kabartma, nervür gibi küçük detaylar için.</summary>
        public void FlatBox(Material m, Vector3 center, Vector3 size, Vector3 euler) => FlatBox(m, center, size, Quaternion.Euler(euler));

        public void FlatBox(Material m, Vector3 center, Vector3 size, Quaternion rotation)
        {
            var h = size * 0.5f;
            if (h.x <= 0f || h.y <= 0f || h.z <= 0f) return;
            for (var axis = 0; axis < 3; axis++)
            {
                var u = (axis + 1) % 3; var v = (axis + 2) % 3;
                for (var sign = -1; sign <= 1; sign += 2)
                {
                    var pts = new Vector3[4];
                    for (var c = 0; c < 4; c++)
                    {
                        var q = Vector3.zero;
                        q[axis] = sign * h[axis];
                        q[u] = (c == 1 || c == 2) ? h[u] : -h[u];
                        q[v] = c >= 2 ? h[v] : -h[v];
                        pts[c] = center + rotation * q;
                    }
                    QuadInterior(m, pts[0], pts[1], pts[2], pts[3], center);
                }
            }
        }

        private static float BevelGrid(int i, float half, float inner) =>
            i == 0 ? -half : i == 1 ? -inner : i == 2 ? inner : half;

        /// <summary>Arka ve ön kesitleri farklı (X genişlik, Y yükseklik) konik kutu; kesit merkezleri serbest.</summary>
        public void Taper(Material m, Vector3 backCenter, Vector2 backSize, Vector3 frontCenter, Vector2 frontSize)
        {
            var b = backSize * 0.5f;
            var f = frontSize * 0.5f;
            _corners[0] = backCenter + new Vector3(-b.x, -b.y, 0f);
            _corners[1] = backCenter + new Vector3(b.x, -b.y, 0f);
            _corners[2] = backCenter + new Vector3(b.x, b.y, 0f);
            _corners[3] = backCenter + new Vector3(-b.x, b.y, 0f);
            _corners[4] = frontCenter + new Vector3(-f.x, -f.y, 0f);
            _corners[5] = frontCenter + new Vector3(f.x, -f.y, 0f);
            _corners[6] = frontCenter + new Vector3(f.x, f.y, 0f);
            _corners[7] = frontCenter + new Vector3(-f.x, f.y, 0f);
            Hull(m, _corners);
        }

        /// <summary>
        /// Dikey konik kutu: üst ve alt kesit merkezleri ve boyutları (X genişlik, Z derinlik). Kabza/şarjör gibi
        /// eğik parçalar için.
        /// </summary>
        public void VerticalTaper(Material m, Vector3 topCenter, Vector2 topSize, Vector3 bottomCenter, Vector2 bottomSize)
        {
            var t = topSize * 0.5f;
            var b = bottomSize * 0.5f;
            _corners[0] = bottomCenter + new Vector3(-b.x, 0f, -b.y);
            _corners[1] = bottomCenter + new Vector3(b.x, 0f, -b.y);
            _corners[2] = topCenter + new Vector3(t.x, 0f, -t.y);
            _corners[3] = topCenter + new Vector3(-t.x, 0f, -t.y);
            _corners[4] = bottomCenter + new Vector3(-b.x, 0f, b.y);
            _corners[5] = bottomCenter + new Vector3(b.x, 0f, b.y);
            _corners[6] = topCenter + new Vector3(t.x, 0f, t.y);
            _corners[7] = topCenter + new Vector3(-t.x, 0f, t.y);
            Hull(m, _corners);
        }

        /// <summary>
        /// 8 köşeli dışbükey gövde. Köşe sırası: 0(-x,-y,-z) 1(+x,-y,-z) 2(+x,+y,-z) 3(-x,+y,-z) ve 4..7 aynısı +z.
        /// </summary>
        public void Hull(Material m, Vector3[] c)
        {
            if (c == null || c.Length < 8)
                return;

            var interior = Vector3.zero;
            for (var i = 0; i < 8; i++)
                interior += c[i];
            interior *= 0.125f;

            QuadInterior(m, c[0], c[1], c[2], c[3], interior);
            QuadInterior(m, c[4], c[5], c[6], c[7], interior);
            QuadInterior(m, c[0], c[3], c[7], c[4], interior);
            QuadInterior(m, c[1], c[2], c[6], c[5], interior);
            QuadInterior(m, c[0], c[1], c[5], c[4], interior);
            QuadInterior(m, c[3], c[2], c[6], c[7], interior);
        }

        /// <summary>a→b ekseni boyunca (konik olabilen) silindir. Kenarlar düz gölgeli (düşük poligon), smooth = yuvarlak gölge.</summary>
        public void Cylinder(Material m, Vector3 a, Vector3 b, float radiusA, float radiusB, int sides = 8, bool capA = true,
            bool capB = true, bool smooth = false)
        {
            if ((b - a).sqrMagnitude < 1e-12f || radiusA < 0f || radiusB < 0f)
                return;

            if (sides >= 8 && Mathf.Max(radiusA, radiusB) < 0.025f) { sides = Mathf.Max(sides, 20); smooth = true; }
            sides = Mathf.Clamp(sides, 3, 32);
            BuildRing(a, b, radiusA, sides, _ringA);
            BuildRing(a, b, radiusB, sides, _ringB, true);
            var axis = (b - a).normalized;
            var mid = (a + b) * 0.5f;

            for (var i = 0; i < sides; i++)
            {
                var j = (i + 1) % sides;
                if (smooth)
                {
                    var slope = (radiusA - radiusB) / (b - a).magnitude;
                    var na = ((_ringA[i] - a).normalized + axis * slope).normalized;
                    var nb = ((_ringA[j] - a).normalized + axis * slope).normalized;
                    QuadSmooth(m, _ringA[i], _ringA[j], _ringB[j], _ringB[i], na, nb, nb, na, mid);
                }
                else
                {
                    QuadInterior(m, _ringA[i], _ringA[j], _ringB[j], _ringB[i], mid);
                }
            }

            if (capA && radiusA > 0.0001f)
            {
                for (var i = 0; i < sides; i++)
                    TriFacing(m, a, _ringA[i], _ringA[(i + 1) % sides], -axis);
            }

            if (capB && radiusB > 0.0001f)
            {
                for (var i = 0; i < sides; i++)
                    TriFacing(m, b, _ringB[i], _ringB[(i + 1) % sides], axis);
            }
        }

        public void Cylinder(Material m, Vector3 a, Vector3 b, float radius, int sides = 8) =>
            Cylinder(m, a, b, radius, radius, sides);

        /// <summary>İçi boş boru (dış + iç yüzey + uç halkaları). Nişangâh gövdesi, gez halkası için.</summary>
        public void Tube(Material m, Vector3 a, Vector3 b, float outerRadius, float innerRadius, int sides = 10)
        {
            sides = Mathf.Clamp(sides * 2, 16, 48);
            innerRadius = Mathf.Clamp(innerRadius, 0.0001f, outerRadius * 0.98f);
            if ((b - a).sqrMagnitude < 1e-10f)
                return;

            BuildRing(a, b, outerRadius, sides, _ringA);
            BuildRing(a, b, outerRadius, sides, _ringB, true);
            BuildRing(a, b, innerRadius, sides, _ringC);
            BuildRing(a, b, innerRadius, sides, _ringD, true);
            var axis = (b - a).normalized;
            var mid = (a + b) * 0.5f;

            for (var i = 0; i < sides; i++)
            {
                var j = (i + 1) % sides;
                // Dış yüzey: eksenden dışarı.
                var na = (_ringA[i] - a).normalized;
                var nb = (_ringA[j] - a).normalized;
                QuadSmooth(m, _ringA[i], _ringA[j], _ringB[j], _ringB[i], na, nb, nb, na, mid);
                // İç yüzey: eksene doğru.
                var faceCenter = (_ringC[i] + _ringC[j] + _ringD[j] + _ringD[i]) * 0.25f;
                var toAxis = Vector3.ProjectOnPlane(mid - faceCenter, axis);
                QuadFacing(m, _ringC[i], _ringC[j], _ringD[j], _ringD[i], toAxis);
                // Uç halkaları.
                QuadFacing(m, _ringA[i], _ringA[j], _ringC[j], _ringC[i], -axis);
                QuadFacing(m, _ringB[i], _ringB[j], _ringD[j], _ringD[i], axis);
            }
        }

        /// <summary>Düşük poligonlu elipsoit (enlem/boylam). El bombası, düğme, topuz için.</summary>
        public void Ellipsoid(Material m, Vector3 center, Vector3 radii, int rings = 4, int segments = 8)
        {
            rings = Mathf.Clamp(rings, 2, 12);
            segments = Mathf.Clamp(segments, 3, 24);
            for (var r = 0; r < rings; r++)
            {
                var t0 = Mathf.PI * r / rings;
                var t1 = Mathf.PI * (r + 1) / rings;
                for (var s = 0; s < segments; s++)
                {
                    var p0 = (Mathf.PI * 2f) * s / segments;
                    var p1 = (Mathf.PI * 2f) * (s + 1) / segments;
                    var a = center + Scale(Spherical(t0, p0), radii);
                    var b = center + Scale(Spherical(t0, p1), radii);
                    var c = center + Scale(Spherical(t1, p1), radii);
                    var d = center + Scale(Spherical(t1, p0), radii);
                    var faceCenter = (a + b + c + d) * 0.25f;
                    QuadFacing(m, a, b, c, d, faceCenter - center);
                }
            }
        }

        /// <summary>Çift taraflı düz dörtgen (ışıma/efekt, ince levha). UV 0..1, köşe rengi beyaz.</summary>
        public void DoubleSidedQuad(Material m, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            a = Apply(a);
            b = Apply(b);
            c = Apply(c);
            d = Apply(d);
            var group = _current;
            var section = SectionFor(group, m);
            for (var side = 0; side < 2; side++)
            {
                var start = group.Vertices.Count;
                var n = Vector3.Cross(c - a, d - b).normalized;
                if (side == 1)
                    n = -n;

                AddVertex(group, a, n, new Vector2(0f, 0f));
                AddVertex(group, b, n, new Vector2(0f, 1f));
                AddVertex(group, c, n, new Vector2(1f, 1f));
                AddVertex(group, d, n, new Vector2(1f, 0f));
                if (side == 0)
                {
                    section.Triangles.Add(start);
                    section.Triangles.Add(start + 1);
                    section.Triangles.Add(start + 2);
                    section.Triangles.Add(start);
                    section.Triangles.Add(start + 2);
                    section.Triangles.Add(start + 3);
                }
                else
                {
                    section.Triangles.Add(start);
                    section.Triangles.Add(start + 2);
                    section.Triangles.Add(start + 1);
                    section.Triangles.Add(start);
                    section.Triangles.Add(start + 3);
                    section.Triangles.Add(start + 2);
                }
            }
        }

        // ------------------------------------------------------------------ Faces

        /// <summary>Dörtgen; sargı iç noktadan uzağa bakacak şekilde düzeltilir.</summary>
        public void QuadInterior(Material m, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 interior)
        {
            var center = (a + b + c + d) * 0.25f;
            QuadFacing(m, a, b, c, d, center - interior);
        }

        /// <summary>Dörtgen; normal verilen yöne bakacak şekilde sargı düzeltilir.</summary>
        public void QuadFacing(Material m, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 facing)
        {
            a = Apply(a);
            b = Apply(b);
            c = Apply(c);
            d = Apply(d);
            facing = ApplyDirection(facing);

            var n = Vector3.Cross(c - a, d - b);
            if (n.sqrMagnitude < 1e-14f)
                return;

            if (Vector3.Dot(n, facing) < 0f)
            {
                var tmp = b;
                b = d;
                d = tmp;
                n = -n;
            }

            n.Normalize();
            EmitQuad(m, a, b, c, d, n, n, n, n);
        }

        public void TriFacing(Material m, Vector3 a, Vector3 b, Vector3 c, Vector3 facing)
        {
            a = Apply(a);
            b = Apply(b);
            c = Apply(c);
            facing = ApplyDirection(facing);

            var n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-14f)
                return;

            if (Vector3.Dot(n, facing) < 0f)
            {
                var tmp = b;
                b = c;
                c = tmp;
                n = -n;
            }

            n.Normalize();
            var group = _current;
            var section = SectionFor(group, m);
            var start = group.Vertices.Count;
            AddVertex(group, a, n, ProjectUv(a, n));
            AddVertex(group, b, n, ProjectUv(b, n));
            AddVertex(group, c, n, ProjectUv(c, n));
            section.Triangles.Add(start);
            section.Triangles.Add(start + 1);
            section.Triangles.Add(start + 2);
        }

        private void QuadSmooth(Material m, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 na, Vector3 nb, Vector3 nc,
            Vector3 nd, Vector3 interior, bool seamless = false)
        {
            a = Apply(a);
            b = Apply(b);
            c = Apply(c);
            d = Apply(d);
            interior = Apply(interior);
            na = ApplyDirection(na);
            nb = ApplyDirection(nb);
            nc = ApplyDirection(nc);
            nd = ApplyDirection(nd);

            var n = Vector3.Cross(c - a, d - b);
            if (n.sqrMagnitude < 1e-14f)
                return;

            var smoothMode = 1;   // 1: a→b facet (u kenarsız), 2: b/d takaslandı (v kenarsız)
            if (Vector3.Dot(n, (a + b + c + d) * 0.25f - interior) < 0f)
            {
                smoothMode = 2;
                var tmp = b;
                b = d;
                d = tmp;
                var tn = nb;
                nb = nd;
                nd = tn;
            }

            EmitQuad(m, a, b, c, d, na, nb, nc, nd, seamless ? 3 : smoothMode);
        }

        private void EmitQuad(Material m, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd, int smoothMode = 0)
        {
            var group = _current;
            var section = SectionFor(group, m);
            var start = group.Vertices.Count;
            var flat = (na + nb + nc + nd).normalized;
            // Dörtgen yerel koordinatı + metre boyutu: gölgelendirici kenara uzaklığı buradan çıkarır (eğri yüzeylerde yalnız eksen uçları).
            var size = new Vector2((smoothMode == 1 || smoothMode == 3) ? 1000f : (b - a).magnitude, (smoothMode == 2 || smoothMode == 3) ? 1000f : (d - a).magnitude);
            AddVertex(group, a, na, ProjectUv(a, flat), new Vector2(0f, 0f), size);
            AddVertex(group, b, nb, ProjectUv(b, flat), new Vector2(1f, 0f), size);
            AddVertex(group, c, nc, ProjectUv(c, flat), new Vector2(1f, 1f), size);
            AddVertex(group, d, nd, ProjectUv(d, flat), new Vector2(0f, 1f), size);
            section.Triangles.Add(start);
            section.Triangles.Add(start + 1);
            section.Triangles.Add(start + 2);
            section.Triangles.Add(start);
            section.Triangles.Add(start + 2);
            section.Triangles.Add(start + 3);
        }

        // ------------------------------------------------------------------ Output

        /// <summary>Grupları mesh parçalarına dönüştürür. Boş gruplar atlanır.</summary>
        public List<BuiltMeshPart> Build(string namePrefix, bool withColors = false)
        {
            var result = new List<BuiltMeshPart>(_groups.Count);
            for (var g = 0; g < _groups.Count; g++)
            {
                var group = _groups[g];
                if (group.Vertices.Count == 0)
                    continue;

                var sections = new List<Section>(group.Sections.Count);
                for (var s = 0; s < group.Sections.Count; s++)
                {
                    if (group.Sections[s].Triangles.Count > 0)
                        sections.Add(group.Sections[s]);
                }

                if (sections.Count == 0)
                    continue;

                var mesh = new Mesh
                {
                    name = namePrefix + "_" + group.Name,
                    hideFlags = HideFlags.DontUnloadUnusedAsset
                };
                if (group.Vertices.Count > 65000)
                    mesh.indexFormat = IndexFormat.UInt32;

                mesh.SetVertices(group.Vertices);
                mesh.SetNormals(group.Normals);
                mesh.SetUVs(0, group.Uvs);
                if (group.QuadUvs.Count == group.Vertices.Count)
                {
                    mesh.SetUVs(1, group.QuadUvs);
                    mesh.SetUVs(2, group.QuadSizes);
                }
                if (withColors && group.Colors.Count == group.Vertices.Count)
                    mesh.SetColors(group.Colors);

                mesh.subMeshCount = sections.Count;
                var materials = new Material[sections.Count];
                for (var s = 0; s < sections.Count; s++)
                {
                    mesh.SetTriangles(sections[s].Triangles, s, false);
                    materials[s] = sections[s].Material;
                }

                mesh.RecalculateBounds();
                mesh.UploadMeshData(false);
                result.Add(new BuiltMeshPart { Name = group.Name, Pivot = group.Pivot, Mesh = mesh, Materials = materials });
            }

            return result;
        }

        // ------------------------------------------------------------------ Helpers

        private Vector3 Apply(Vector3 p)
        {
            if (_hasMatrix)
                p = _matrix.MultiplyPoint3x4(p);
            if (MirrorX)
                p.x = -p.x;
            return p;
        }

        private Vector3 ApplyDirection(Vector3 v)
        {
            if (_hasMatrix)
                v = _matrix.MultiplyVector(v);
            if (MirrorX)
                v.x = -v.x;
            return v;
        }

        private static Section SectionFor(Group group, Material material)
        {
            for (var i = 0; i < group.Sections.Count; i++)
            {
                if (group.Sections[i].Material == material)
                    return group.Sections[i];
            }

            var section = new Section { Material = material };
            group.Sections.Add(section);
            return section;
        }

        /// <summary>Kenar aşınması verisi olmayan köşe (UV2 = kenarsız).</summary>
        private static void AddVertex(Group group, Vector3 modelPosition, Vector3 normal, Vector2 uv) =>
            AddVertex(group, modelPosition, normal, uv, new Vector2(0.5f, 0.5f), NoEdge);

        private static void AddVertex(Group group, Vector3 modelPosition, Vector3 normal, Vector2 uv, Vector2 quadUv, Vector2 quadSize)
        {
            group.QuadUvs.Add(quadUv);
            group.QuadSizes.Add(quadSize);
            group.Vertices.Add(modelPosition - group.Pivot);
            group.Normals.Add(normal);
            group.Uvs.Add(uv);
            group.Colors.Add(new Color32(255, 255, 255, 255));
        }

        private static Vector2 ProjectUv(Vector3 p, Vector3 n)
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

        /// <summary>a→b eksenine dik halka (a ya da atEnd ise b ucunda). Düz yüzler üst/alt/yanlara hizalanır.</summary>
        private static void BuildRing(Vector3 a, Vector3 b, float radius, int sides, List<Vector3> output, bool atEnd = false)
        {
            output.Clear();
            var axis = (b - a).normalized;
            var reference = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) < 0.95f ? Vector3.up : Vector3.right;
            var u = Vector3.Cross(axis, reference).normalized;
            var v = Vector3.Cross(axis, u).normalized;
            var center = atEnd ? b : a;
            var step = Mathf.PI * 2f / sides;
            for (var i = 0; i < sides; i++)
            {
                var angle = (i + 0.5f) * step;
                output.Add(center + (u * Mathf.Cos(angle) + v * Mathf.Sin(angle)) * radius);
            }
        }

        private static Vector3 Spherical(float theta, float phi)
        {
            return new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi));
        }

        private static Vector3 Scale(Vector3 a, Vector3 b) => new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
    }
}
