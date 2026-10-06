using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Infrastructure.Characters
{
    /// <summary>
    /// Asker ve antrenman hedefi parçaları için düz gölgeli (flat shaded) düşük poligonlu ağlar.
    /// Ağlar anahtar başına bir kez üretilir ve tüm askerler arasında paylaşılır (asker başına ağ tahsisi yok).
    /// UV'ler metre ölçeklidir (1 UV = 1 m) — dijital kamuflaj tüm parçalarda aynı piksel boyutunda görünür.
    /// Parça Transform'ları ölçeklenmez; boyut ağın içine gömülüdür (vuruş kutuları ve UV'ler bozulmaz).
    /// </summary>
    internal static partial class CharacterMeshes
    {
        private static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>(128);

        // Üretim sırasında yeniden kullanılan geçici listeler (yalnızca ana iş parçacığı, kurulum anında).
        private static readonly List<Vector3> Verts = new List<Vector3>(512);
        private static readonly List<Vector3> Normals = new List<Vector3>(512);
        private static readonly List<Vector2> Uvs = new List<Vector2>(512);
        private static readonly List<int> Tris = new List<int>(1024);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Cache.Clear();
        }

        /// <summary>Önbellekte varsa döndürür.</summary>
        private static bool TryGet(string key, out Mesh mesh)
        {
            if (!_audit && Cache.TryGetValue(key, out mesh) && mesh != null)
                return true;

            mesh = null;
            return false;
        }

        /// <summary>
        /// Kesik piramit kutu: alt yüz (y0) bottom boyutunda, üst yüz (y1) top boyutunda; x genişlik, y derinlik (z).
        /// bottomZ/topZ yüzlerin z kaydırması (eğimli parçalar için). Merkez x = 0.
        /// </summary>
        public static Mesh Frustum(string key, float y0, float y1, Vector2 bottom, Vector2 top, float bottomZ = 0f, float topZ = 0f,
            float centerX = 0f)
        {
            if (TryGet(key, out var cached))
                return cached;

            Begin();
            var bx = bottom.x * 0.5f;
            var bz = bottom.y * 0.5f;
            var tx = top.x * 0.5f;
            var tz = top.y * 0.5f;

            // Alt köşeler (saat yönü yukarıdan bakınca): 0 (-x,-z) 1 (+x,-z) 2 (+x,+z) 3 (-x,+z)
            var b0 = new Vector3(centerX - bx, y0, bottomZ - bz);
            var b1 = new Vector3(centerX + bx, y0, bottomZ - bz);
            var b2 = new Vector3(centerX + bx, y0, bottomZ + bz);
            var b3 = new Vector3(centerX - bx, y0, bottomZ + bz);
            var t0 = new Vector3(centerX - tx, y1, topZ - tz);
            var t1 = new Vector3(centerX + tx, y1, topZ - tz);
            var t2 = new Vector3(centerX + tx, y1, topZ + tz);
            var t3 = new Vector3(centerX - tx, y1, topZ + tz);

            AddQuad(b3, b2, t2, t3); // ön (+z)
            AddQuad(b1, b0, t0, t1); // arka (-z)
            AddQuad(b2, b1, t1, t2); // sağ (+x)
            AddQuad(b0, b3, t3, t0); // sol (-x)
            AddQuad(t3, t2, t1, t0); // üst
            AddQuad(b0, b1, b2, b3); // alt
            return Finish(key);
        }

        /// <summary>Merkezli düz kutu.</summary>
        public static Mesh Box(string key, Vector3 center, Vector3 size)
        {
            if (TryGet(key, out var cached))
                return cached;

            var half = size * 0.5f;
            var mesh = Frustum(key, center.y - half.y, center.y + half.y, new Vector2(size.x, size.z), new Vector2(size.x, size.z),
                center.z, center.z, center.x);
            return mesh;
        }

        /// <summary>Y ekseni boyunca konik/düz silindir (yanlar düz gölgeli). caps: alt/üst kapaklar.</summary>
        public static Mesh Cylinder(string key, float y0, float y1, float radiusBottom, float radiusTop, int sides, bool caps,
            float scaleZ = 1f)
        {
            if (TryGet(key, out var cached))
                return cached;

            Begin();
            sides = Mathf.Clamp(sides, 3, 32);
            for (var i = 0; i < sides; i++)
            {
                var a0 = (i / (float)sides) * Mathf.PI * 2f;
                var a1 = ((i + 1) / (float)sides) * Mathf.PI * 2f;
                var c0 = Mathf.Cos(a0);
                var s0 = Mathf.Sin(a0);
                var c1 = Mathf.Cos(a1);
                var s1 = Mathf.Sin(a1);
                var p0 = new Vector3(c0 * radiusBottom, y0, s0 * radiusBottom * scaleZ);
                var p1 = new Vector3(c1 * radiusBottom, y0, s1 * radiusBottom * scaleZ);
                var p2 = new Vector3(c1 * radiusTop, y1, s1 * radiusTop * scaleZ);
                var p3 = new Vector3(c0 * radiusTop, y1, s0 * radiusTop * scaleZ);
                if (sides >= 8)
                {
                    // Yumuşak gölge: yanal normaller radyal + eğim bileşeni.
                    var slope = (radiusBottom - radiusTop) / Mathf.Max(1e-4f, y1 - y0);
                    var n0 = SafeNormalize(new Vector3(c0, slope, s0 / Mathf.Max(0.05f, scaleZ)), Vector3.up);
                    var n1 = SafeNormalize(new Vector3(c1, slope, s1 / Mathf.Max(0.05f, scaleZ)), Vector3.up);
                    AddQuadN(p1, p0, p3, p2, n1, n0, n0, n1);
                }
                else
                {
                    AddQuad(p1, p0, p3, p2);
                }

                if (!caps)
                    continue;

                AddTri(new Vector3(0f, y1, 0f), p2, p3);
                AddTri(new Vector3(0f, y0, 0f), p0, p1);
            }

            return Finish(key);
        }

        /// <summary>
        /// Elipsoit (düşük poligon enlem-boylam). latMin/latMax derece (-90 alt kutup, 90 üst kutup) — kubbeler için
        /// latMin > -90 verilir (alt açık kalır). flare: alt halkanın dışa açılması (kask kenarı için).
        /// </summary>
        public static Mesh Ellipsoid(string key, Vector3 center, Vector3 radii, int segments, int rings, float latMin = -90f,
            float latMax = 90f, float flare = 0f)
        {
            if (TryGet(key, out var cached))
                return cached;

            Begin();
            segments = Mathf.Clamp(segments, 3, 32);
            rings = Mathf.Clamp(rings, 2, 24);
            for (var r = 0; r < rings; r++)
            {
                var lat0 = Mathf.Lerp(latMin, latMax, r / (float)rings) * Mathf.Deg2Rad;
                var lat1 = Mathf.Lerp(latMin, latMax, (r + 1) / (float)rings) * Mathf.Deg2Rad;
                var f0 = r == 0 ? 1f + flare : 1f;
                for (var s = 0; s < segments; s++)
                {
                    var lon0 = (s / (float)segments) * Mathf.PI * 2f;
                    var lon1 = ((s + 1) / (float)segments) * Mathf.PI * 2f;
                    var p00 = Point(center, radii, lat0, lon0, f0);
                    var p01 = Point(center, radii, lat0, lon1, f0);
                    var p10 = Point(center, radii, lat1, lon0, 1f);
                    var p11 = Point(center, radii, lat1, lon1, 1f);
                    var n00 = EllipsoidNormal(radii, lat0, lon0);
                    var n01 = EllipsoidNormal(radii, lat0, lon1);
                    var n10 = EllipsoidNormal(radii, lat1, lon0);
                    var n11 = EllipsoidNormal(radii, lat1, lon1);

                    var topPole = Mathf.Abs(lat1 - Mathf.PI * 0.5f) < 1e-4f;
                    var bottomPole = Mathf.Abs(lat0 + Mathf.PI * 0.5f) < 1e-4f;
                    // Dışarıdan bakınca saat yönü: p00 (sağ-alt) → p01 (sol-alt) → p11 (sol-üst) → p10 (sağ-üst).
                    if (topPole)
                        AddTriN(p00, p01, p10, n00, n01, n10);
                    else if (bottomPole)
                        AddTriN(p00, p11, p10, n00, n11, n10);
                    else
                        AddQuadN(p00, p01, p11, p10, n00, n01, n11, n10);
                }
            }

            return Finish(key);
        }

        /// <summary>+Z yönüne bakan dörtgen (UV 0..1 — bayrak/arma dokuları için). İsteğe bağlı çift taraflı.</summary>
        public static Mesh Quad(string key, float width, float height, bool doubleSided = false)
        {
            if (TryGet(key, out var cached))
                return cached;

            Begin();
            var hw = width * 0.5f;
            var hh = height * 0.5f;
            // +Z'den bakınca sağ = -X. Doku düz okunur: sol-alt (+hw,-hh) = uv(0,0).
            AddQuadUv(new Vector3(-hw, -hh, 0f), new Vector3(hw, -hh, 0f), new Vector3(hw, hh, 0f), new Vector3(-hw, hh, 0f),
                new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f));
            if (doubleSided)
            {
                // Arka yüz (-Z'ye bakar): aynı noktalar aynı UV'yi alır (arkadan bakınca ayna görüntüsü, bayrak gibi).
                AddQuadUv(new Vector3(hw, -hh, 0f), new Vector3(-hw, -hh, 0f), new Vector3(-hw, hh, 0f), new Vector3(hw, hh, 0f),
                    new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f));
            }

            return Finish(key);
        }

        // ------------------------------------------------------------------ İç yardımcılar

        private static Vector3 Point(Vector3 center, Vector3 radii, float lat, float lon, float flare)
        {
            var cl = Mathf.Cos(lat);
            return center + new Vector3(Mathf.Sin(lon) * cl * radii.x * flare, Mathf.Sin(lat) * radii.y, Mathf.Cos(lon) * cl * radii.z * flare);
        }

        private static Vector3 EllipsoidNormal(Vector3 radii, float lat, float lon)
        {
            var cl = Mathf.Cos(lat);
            var n = new Vector3(Mathf.Sin(lon) * cl / radii.x, Mathf.Sin(lat) / radii.y, Mathf.Cos(lon) * cl / radii.z);
            return SafeNormalize(n, Vector3.up);
        }

        /// <summary>Yumuşak gölgeli dörtgen: köşe normalleri verilir; UV izdüşümü yüz normaline göre seçilir (sürekli doku).</summary>
        private static void AddQuadN(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd)
        {
            var face = Vector3.Cross(b - a, c - a);
            if (face.sqrMagnitude < 1e-12f)
                face = Vector3.Cross(c - a, d - a);
            face = SafeNormalize(face, Vector3.up);
            var start = Verts.Count;
            AddVertexN(a, na, face);
            AddVertexN(b, nb, face);
            AddVertexN(c, nc, face);
            AddVertexN(d, nd, face);
            Tris.Add(start);
            Tris.Add(start + 1);
            Tris.Add(start + 2);
            Tris.Add(start);
            Tris.Add(start + 2);
            Tris.Add(start + 3);
        }

        private static void AddTriN(Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc)
        {
            var face = Vector3.Cross(b - a, c - a);
            face = SafeNormalize(face, Vector3.up);
            var start = Verts.Count;
            AddVertexN(a, na, face);
            AddVertexN(b, nb, face);
            AddVertexN(c, nc, face);
            Tris.Add(start);
            Tris.Add(start + 1);
            Tris.Add(start + 2);
        }

        private static void AddVertexN(Vector3 p, Vector3 shadeNormal, Vector3 uvNormal)
        {
            AddVertex(p, uvNormal);
            Normals[Normals.Count - 1] = shadeNormal;
        }

        private static void Begin()
        {
            Verts.Clear();
            Normals.Clear();
            Uvs.Clear();
            Tris.Clear();
        }

        /// <summary>
        /// Dörtgen: a,b,c,d dışarıdan bakınca SAAT YÖNÜNDE verilir (sağ-alt → sol-alt → sol-üst → sağ-üst).
        /// Unity'de ön yüz saat yönüdür; dış normal = Cross(b - a, c - a).
        /// </summary>
        private static void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            var n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-12f)
                n = Vector3.Cross(c - a, d - a);
            n = SafeNormalize(n, Vector3.up);

            var start = Verts.Count;
            AddVertex(a, n);
            AddVertex(b, n);
            AddVertex(c, n);
            AddVertex(d, n);
            Tris.Add(start);
            Tris.Add(start + 1);
            Tris.Add(start + 2);
            Tris.Add(start);
            Tris.Add(start + 2);
            Tris.Add(start + 3);
        }

        private static void AddQuadUv(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 uvA, Vector2 uvB, Vector2 uvC, Vector2 uvD)
        {
            var n = SafeNormalize(Vector3.Cross(b - a, c - a), Vector3.up);
            var start = Verts.Count;
            Verts.Add(a);
            Verts.Add(b);
            Verts.Add(c);
            Verts.Add(d);
            Normals.Add(n);
            Normals.Add(n);
            Normals.Add(n);
            Normals.Add(n);
            Uvs.Add(uvA);
            Uvs.Add(uvB);
            Uvs.Add(uvC);
            Uvs.Add(uvD);
            Tris.Add(start);
            Tris.Add(start + 1);
            Tris.Add(start + 2);
            Tris.Add(start);
            Tris.Add(start + 2);
            Tris.Add(start + 3);
        }

        /// <summary>Üçgen: dışarıdan bakınca saat yönünde; dış normal = Cross(b - a, c - a).</summary>
        private static void AddTri(Vector3 a, Vector3 b, Vector3 c)
        {
            var n = Vector3.Cross(b - a, c - a);
            n = SafeNormalize(n, Vector3.up);
            var start = Verts.Count;
            AddVertex(a, n);
            AddVertex(b, n);
            AddVertex(c, n);
            Tris.Add(start);
            Tris.Add(start + 1);
            Tris.Add(start + 2);
        }

        private static void AddVertex(Vector3 p, Vector3 n)
        {
            Verts.Add(p);
            Normals.Add(n);

            // Metre ölçekli düzlemsel UV: yüzün baskın eksenine göre izdüşüm.
            var ax = Mathf.Abs(n.x);
            var ay = Mathf.Abs(n.y);
            var az = Mathf.Abs(n.z);
            Vector2 uv;
            if (ay >= ax && ay >= az)
                uv = new Vector2(p.x, p.z);
            else if (ax >= az)
                uv = new Vector2(p.z, p.y);
            else
                uv = new Vector2(p.x, p.y);
            Uvs.Add(uv);
        }

        // Denetim kipi (EditMode testi): Mesh nesnesi (yerel köprü) oluşturmadan ham liste anlık görüntüsü alınır.
        private static bool _audit;
        private static AuditSnapshot _auditResult;

        internal sealed class AuditSnapshot
        {
            public string Key;
            public Vector3[] Verts;
            public Vector3[] Normals;
            public Vector2[] Uvs;
            public int Sanitized;
        }

        /// <summary>Yalnızca test: build() içindeki ağ üretimini önbelleksiz çalıştırır, ham köşe verisini döndürür (sterilizasyondan ÖNCE).</summary>
        internal static AuditSnapshot AuditBuild(System.Action build)
        {
            _audit = true;
            _auditResult = null;
            try { build(); }
            finally { _audit = false; }
            return _auditResult;
        }

        /// <summary>
        /// Vector3.normalized büyüklüğü 1e-5'in altındaki vektörleri SIFIR döndürür; ince/küçük yüzlerin (kenar ~mm) çaprazı
        /// bunun altına düşüp sıfır normal üretiyor, gölgelendiricide normalize(0) = NaN olup parça beyaza patlıyordu.
        /// Burada ölçek-bağımsız (kare büyüklük 1e-24'e kadar) normalizasyon yapılır; çok küçükse fallback.
        /// </summary>
        private static Vector3 SafeNormalize(Vector3 v, Vector3 fallback)
        {
            var m2 = v.x * v.x + v.y * v.y + v.z * v.z;
            if (!(m2 > 1e-24f) || float.IsInfinity(m2))
                return fallback;
            return v / Mathf.Sqrt(m2);
        }

        private static bool Bad(Vector3 v)
        {
            return float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
                   float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z);
        }

        private static Mesh Finish(string key)
        {
            if (_audit)
            {
                var snap = new AuditSnapshot { Key = key, Verts = Verts.ToArray(), Normals = Normals.ToArray(), Uvs = Uvs.ToArray() };
                _auditResult = snap;
                return null;
            }

            // Son çare koruma: NaN/sonsuz köşe, ÖNCEKİ geçerli köşeye çekilir (orijine sıçramaz); normal/uv de süpürülür.
            // Kaynak düzeltmeleri esastır — bu dal tetiklenirse player.log'da uyarı görünür.
            var sanitized = 0;
            for (var i = 0; i < Verts.Count; i++)
            {
                var v = Verts[i];
                if (Bad(v))
                {
                    Verts[i] = i > 0 ? Verts[i - 1] : Vector3.zero;
                    sanitized++;
                }

                var n = Normals[i];
                if (Bad(n) || n.sqrMagnitude < 1e-8f)
                {
                    Normals[i] = i > 0 ? Normals[i - 1] : Vector3.up;
                    if (Bad(Normals[i]) || Normals[i].sqrMagnitude < 1e-8f)
                        Normals[i] = Vector3.up;
                    sanitized++;
                }
                else
                    Normals[i] = SafeNormalize(n, Vector3.up);

                var u = Uvs[i];
                if (float.IsNaN(u.x) || float.IsNaN(u.y) || float.IsInfinity(u.x) || float.IsInfinity(u.y))
                {
                    Uvs[i] = i > 0 ? Uvs[i - 1] : Vector2.zero;
                    sanitized++;
                }
            }

            if (sanitized > 0)
                Debug.LogWarning("[CharacterMeshes] NaN/Inf guard triggered for mesh '" + key + "' (" + sanitized + " values repaired) - fix the generator.");

            var mesh = new Mesh { name = "HK_" + key };
            if (Verts.Count > 65000)
                mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(Verts);
            mesh.SetNormals(Normals);
            mesh.SetUVs(0, Uvs);
            mesh.SetTriangles(Tris, 0, true);
            mesh.RecalculateTangents();
            mesh.hideFlags = HideFlags.DontSave;
            Cache[key] = mesh;
            return mesh;
        }
    }
}
