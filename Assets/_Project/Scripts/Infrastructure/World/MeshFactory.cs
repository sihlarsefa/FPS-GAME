using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// Prosedürel low-poly mesh fabrikası. Dönen mesh'ler parametre başına ÖNBELLEKLİ ve PAYLAŞIMLIDIR — değiştirmeyin
    /// (değiştirecekseniz <c>Object.Instantiate(mesh)</c> ile kopyalayın).
    /// Pivot kuralları: Cone/Cylinder/Frustum/Hemisphere/Wedge/ZoneWall/ağaçlar tabanda (y = 0), Box/Sphere/Rock merkezde.
    /// Ağaç mesh'lerinde alt mesh 0 = gövde (kabuk), alt mesh 1 = yaprak/iğne.
    /// UV'ler metre ölçeklidir (dokular dünya ölçeğinde döşenir).
    /// </summary>
    public static class MeshFactory
    {
        public const int TrunkSubmesh = 0;
        public const int FoliageSubmesh = 1;

        private enum Shape
        {
            Box, Cone, Cylinder, Frustum, Hemisphere, Sphere, ZoneWall, Wedge, Disc, Ring, Pine, Oak, Dead, Bush, Rock, Quad
        }

        private readonly struct Key : IEquatable<Key>
        {
            private readonly Shape _shape;
            private readonly float _a, _b, _c, _d, _e, _f;
            private readonly int _i, _j;

            public Key(Shape shape, float a = 0f, float b = 0f, float c = 0f, int i = 0, int j = 0, float d = 0f, float e = 0f, float f = 0f)
            {
                _shape = shape;
                _a = a;
                _b = b;
                _c = c;
                _d = d;
                _e = e;
                _f = f;
                _i = i;
                _j = j;
            }

            public bool Equals(Key o)
            {
                return _shape == o._shape && _a.Equals(o._a) && _b.Equals(o._b) && _c.Equals(o._c) && _d.Equals(o._d)
                       && _e.Equals(o._e) && _f.Equals(o._f) && _i == o._i && _j == o._j;
            }

            public override bool Equals(object obj) => obj is Key k && Equals(k);

            public override int GetHashCode()
            {
                unchecked
                {
                    var h = (int)_shape;
                    h = h * 397 ^ _a.GetHashCode();
                    h = h * 397 ^ _b.GetHashCode();
                    h = h * 397 ^ _c.GetHashCode();
                    h = h * 397 ^ _d.GetHashCode();
                    h = h * 397 ^ _e.GetHashCode();
                    h = h * 397 ^ _f.GetHashCode();
                    h = h * 397 ^ _i;
                    h = h * 397 ^ _j;
                    return h;
                }
            }
        }

        private static readonly Dictionary<Key, Mesh> Cache = new Dictionary<Key, Mesh>();

        /// <summary>Önbelleği temizler (mesh'leri yok etmez).</summary>
        public static void ClearCache()
        {
            Cache.Clear();
        }

        // ================================================================== Temel şekiller

        /// <summary>Merkez pivotlu kutu.</summary>
        public static Mesh Box(Vector3 size)
        {
            return Box(Vector3.zero, size);
        }

        /// <summary>Verilen merkezde kutu (mesh uzayında).</summary>
        public static Mesh Box(Vector3 center, Vector3 size)
        {
            size = Abs(size);
            var key = new Key(Shape.Box, size.x, size.y, size.z, 0, 0, center.x, center.y, center.z);
            return Cached(key, () =>
            {
                var b = new MeshBuilder();
                AddBox(b, 0, center, size);
                return b.ToMesh("HK_Box");
            });
        }

        /// <summary>Koni: taban y = 0 (kapaklı), tepe y = height. Yüzeyler düz gölgeli.</summary>
        public static Mesh Cone(float radius, float height, int segments = 12)
        {
            radius = Mathf.Max(0.001f, radius);
            height = Mathf.Max(0.001f, height);
            segments = Mathf.Clamp(segments, 3, 128);
            return Cached(new Key(Shape.Cone, radius, height, 0f, segments), () =>
            {
                var b = new MeshBuilder();
                var apex = new Vector3(0f, height, 0f);
                for (var i = 0; i < segments; i++)
                {
                    var p0 = Ring(i, segments, radius, 0f);
                    var p1 = Ring(i + 1, segments, radius, 0f);
                    b.AddFlatTriangle(0, p0, apex, p1);
                    b.AddFlatTriangle(0, p0, p1, Vector3.zero);
                }

                return b.ToMesh("HK_Cone");
            });
        }

        /// <summary>Silindir: taban y = 0, üst y = height. Yanlar yumuşak, kapaklar düz.</summary>
        public static Mesh Cylinder(float radius, float height, int segments = 12, bool capped = true)
        {
            return Frustum(radius, radius, height, segments, capped);
        }

        /// <summary>Kesik koni (alt/üst yarıçap): taban y = 0, üst y = height.</summary>
        public static Mesh Frustum(float bottomRadius, float topRadius, float height, int segments = 12, bool capped = true)
        {
            bottomRadius = Mathf.Max(0f, bottomRadius);
            topRadius = Mathf.Max(0f, topRadius);
            height = Mathf.Max(0.001f, height);
            segments = Mathf.Clamp(segments, 3, 128);
            return Cached(new Key(Shape.Frustum, bottomRadius, topRadius, height, segments, capped ? 1 : 0), () =>
            {
                var b = new MeshBuilder();
                AddFrustum(b, 0, Vector3.zero, bottomRadius, topRadius, height, segments, capped, true);
                return b.ToMesh("HK_Cylinder");
            });
        }

        /// <summary>Yarım küre kubbe: taban çemberi y = 0'da (kapaksız), tepe y = radius. Yumuşak normaller.</summary>
        public static Mesh Hemisphere(float radius, int segments = 16, int rings = 6)
        {
            radius = Mathf.Max(0.001f, radius);
            segments = Mathf.Clamp(segments, 4, 128);
            rings = Mathf.Clamp(rings, 2, 64);
            return Cached(new Key(Shape.Hemisphere, radius, 0f, 0f, segments, rings), () =>
            {
                var b = new MeshBuilder();
                var stride = segments + 1;
                for (var r = 0; r <= rings; r++)
                {
                    var phi = r / (float)rings * Mathf.PI * 0.5f; // 0 = ekvator, 90° = tepe
                    var y = Mathf.Sin(phi);
                    var ringRadius = Mathf.Cos(phi);
                    for (var s = 0; s <= segments; s++)
                    {
                        var theta = s / (float)segments * Mathf.PI * 2f;
                        var n = new Vector3(Mathf.Cos(theta) * ringRadius, y, Mathf.Sin(theta) * ringRadius);
                        b.AddVertex(n * radius, n.normalized, new Vector2(s / (float)segments * Mathf.PI * 2f * radius, phi * radius));
                    }
                }

                for (var r = 0; r < rings; r++)
                {
                    for (var s = 0; s < segments; s++)
                    {
                        var a = r * stride + s;
                        var c = (r + 1) * stride + s;
                        b.AddTriangle(0, a, c, a + 1);
                        b.AddTriangle(0, a + 1, c, c + 1);
                    }
                }

                return b.ToMesh("HK_Hemisphere");
            });
        }

        /// <summary>Merkez pivotlu UV küresi (yumuşak normaller).</summary>
        public static Mesh Sphere(float radius, int segments = 12, int rings = 8)
        {
            radius = Mathf.Max(0.001f, radius);
            segments = Mathf.Clamp(segments, 4, 128);
            rings = Mathf.Clamp(rings, 3, 64);
            return Cached(new Key(Shape.Sphere, radius, 0f, 0f, segments, rings), () =>
            {
                var b = new MeshBuilder();
                var stride = segments + 1;
                for (var r = 0; r <= rings; r++)
                {
                    var phi = -Mathf.PI * 0.5f + r / (float)rings * Mathf.PI;
                    var y = Mathf.Sin(phi);
                    var rr = Mathf.Cos(phi);
                    for (var s = 0; s <= segments; s++)
                    {
                        var theta = s / (float)segments * Mathf.PI * 2f;
                        var n = new Vector3(Mathf.Cos(theta) * rr, y, Mathf.Sin(theta) * rr);
                        b.AddVertex(n * radius, n, new Vector2(s / (float)segments, r / (float)rings));
                    }
                }

                for (var r = 0; r < rings; r++)
                {
                    for (var s = 0; s < segments; s++)
                    {
                        var a = r * stride + s;
                        var c = (r + 1) * stride + s;
                        b.AddTriangle(0, a, c, a + 1);
                        b.AddTriangle(0, a + 1, c, c + 1);
                    }
                }

                return b.ToMesh("HK_Sphere");
            });
        }

        /// <summary>
        /// Bölge duvarı: yarıçap 1, y 0..1, açık uçlu tüp (normaller dışa). Transform ölçeğiyle (r, h, r) boyutlandırın.
        /// UV: u çevre boyunca 0..segments/8 (döşenir), v 0..1. Çift taraflı malzemeyle kullanın.
        /// </summary>
        public static Mesh ZoneWall(int segments)
        {
            segments = Mathf.Clamp(segments, 8, 512);
            return Cached(new Key(Shape.ZoneWall, 0f, 0f, 0f, segments), () =>
            {
                var b = new MeshBuilder();
                var uRepeat = segments / 8f;
                for (var i = 0; i <= segments; i++)
                {
                    var t = i / (float)segments;
                    var angle = t * Mathf.PI * 2f;
                    var n = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    b.AddVertex(n, n, new Vector2(t * uRepeat, 0f));
                    b.AddVertex(n + Vector3.up, n, new Vector2(t * uRepeat, 1f));
                }

                for (var i = 0; i < segments; i++)
                {
                    var a = i * 2;
                    b.AddTriangle(0, a, a + 1, a + 2);
                    b.AddTriangle(0, a + 2, a + 1, a + 3);
                }

                var mesh = b.ToMesh("HK_ZoneWall");
                mesh.bounds = new Bounds(new Vector3(0f, 0.5f, 0f), new Vector3(2f, 1f, 2f));
                return mesh;
            });
        }

        /// <summary>
        /// Kama / rampa: taban y = 0 (x: ±width/2, z: ±depth/2), arka yüz z = +depth/2'de dikey (yükseklik height),
        /// eğim ön kenardan (z = -depth/2, y = 0) arkaya yükselir.
        /// </summary>
        public static Mesh Wedge(float width, float height, float depth)
        {
            width = Mathf.Max(0.001f, Mathf.Abs(width));
            height = Mathf.Max(0.001f, Mathf.Abs(height));
            depth = Mathf.Max(0.001f, Mathf.Abs(depth));
            return Cached(new Key(Shape.Wedge, width, height, depth), () =>
            {
                var b = new MeshBuilder();
                float hx = width * 0.5f, hz = depth * 0.5f;
                var fl = new Vector3(-hx, 0f, -hz);
                var fr = new Vector3(hx, 0f, -hz);
                var bl = new Vector3(-hx, 0f, hz);
                var br = new Vector3(hx, 0f, hz);
                var tl = new Vector3(-hx, height, hz);
                var tr = new Vector3(hx, height, hz);
                b.AddFlatQuad(0, fl, tl, tr, fr);      // eğim
                b.AddFlatQuad(0, br, tr, tl, bl);      // arka
                b.AddFlatQuad(0, fl, fr, br, bl);      // taban
                b.AddFlatTriangle(0, fl, bl, tl);      // sol
                b.AddFlatTriangle(0, fr, tr, br);      // sağ
                return b.ToMesh("HK_Wedge");
            });
        }

        /// <summary>Kama / rampa (size = genişlik, yükseklik, derinlik).</summary>
        public static Mesh Wedge(Vector3 size)
        {
            return Wedge(size.x, size.y, size.z);
        }

        /// <summary>Yukarı bakan disk (y = 0).</summary>
        public static Mesh Disc(float radius, int segments = 24)
        {
            radius = Mathf.Max(0.001f, radius);
            segments = Mathf.Clamp(segments, 3, 256);
            return Cached(new Key(Shape.Disc, radius, 0f, 0f, segments), () =>
            {
                var b = new MeshBuilder();
                var center = b.AddVertex(Vector3.zero, Vector3.up, Vector2.zero);
                for (var i = 0; i <= segments; i++)
                {
                    var p = Ring(i, segments, radius, 0f);
                    b.AddVertex(p, Vector3.up, new Vector2(p.x, p.z));
                }

                for (var i = 0; i < segments; i++)
                    b.AddTriangle(0, center, center + 2 + i, center + 1 + i);
                return b.ToMesh("HK_Disc");
            });
        }

        /// <summary>Yukarı bakan halka (y = 0). UV: u çevre (0..1), v iç→dış (0..1).</summary>
        public static Mesh Ring(float innerRadius, float outerRadius, int segments = 48)
        {
            innerRadius = Mathf.Max(0f, innerRadius);
            outerRadius = Mathf.Max(innerRadius + 0.001f, outerRadius);
            segments = Mathf.Clamp(segments, 3, 512);
            return Cached(new Key(Shape.Ring, innerRadius, outerRadius, 0f, segments), () =>
            {
                var b = new MeshBuilder();
                for (var i = 0; i <= segments; i++)
                {
                    var t = i / (float)segments;
                    b.AddVertex(Ring(i, segments, innerRadius, 0f), Vector3.up, new Vector2(t, 0f));
                    b.AddVertex(Ring(i, segments, outerRadius, 0f), Vector3.up, new Vector2(t, 1f));
                }

                for (var i = 0; i < segments; i++)
                {
                    var a = i * 2;
                    b.AddTriangle(0, a, a + 3, a + 1);
                    b.AddTriangle(0, a, a + 2, a + 3);
                }

                return b.ToMesh("HK_Ring");
            });
        }

        /// <summary>Yukarı bakan dörtgen (merkez pivot, y = 0), UV 0..1.</summary>
        public static Mesh Quad(float width, float depth)
        {
            width = Mathf.Max(0.001f, width);
            depth = Mathf.Max(0.001f, depth);
            return Cached(new Key(Shape.Quad, width, depth), () =>
            {
                var b = new MeshBuilder();
                float hx = width * 0.5f, hz = depth * 0.5f;
                b.AddQuad(0, new Vector3(-hx, 0f, -hz), new Vector3(-hx, 0f, hz), new Vector3(hx, 0f, hz), new Vector3(hx, 0f, -hz),
                    new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f));
                return b.ToMesh("HK_Quad");
            });
        }

        // ================================================================== Doğa

        /// <summary>Çam: 6 kenarlı gövde + üst üste 4 düz gölgeli koni. Alt mesh 0 gövde, 1 iğne. Taban y = 0.</summary>
        public static Mesh PineTree(float height = 10f, int seed = 0)
        {
            height = Mathf.Clamp(height, 1f, 60f);
            return Cached(new Key(Shape.Pine, height, 0f, 0f, seed), () => TreeFactory.BuildPineMesh(height, seed));
        }

        /// <summary>Meşe: kalın gövde, 2 dal, 3-4 düzensiz yaprak kümesi. Alt mesh 0 gövde, 1 yaprak. Taban y = 0.</summary>
        public static Mesh OakTree(float height = 8f, int seed = 0)
        {
            height = Mathf.Clamp(height, 1f, 60f);
            return Cached(new Key(Shape.Oak, height, 0f, 0f, seed), () => TreeFactory.BuildOakMesh(height, seed));
        }

        /// <summary>Kuru ağaç: gövde + çıplak dallar. Alt mesh 0 gövde, 1 dallar. Taban y = 0.</summary>
        public static Mesh DeadTree(float height = 7f, int seed = 0)
        {
            height = Mathf.Clamp(height, 1f, 60f);
            return Cached(new Key(Shape.Dead, height, 0f, 0f, seed), () => TreeFactory.BuildDeadMesh(height, seed));
        }

        /// <summary>Çalı: kısa saplar + 3 yaprak kümesi. Alt mesh 0 sap, 1 yaprak. Taban y = 0.</summary>
        public static Mesh Bush(float radius = 1.2f, int seed = 0)
        {
            radius = Mathf.Clamp(radius, 0.2f, 10f);
            return Cached(new Key(Shape.Bush, radius, 0f, 0f, seed), () => TreeFactory.BuildBushMesh(radius, seed));
        }

        /// <summary>Düz gölgeli kaya (yaklaşık 1 m yarıçap, merkez pivot, alt kısmı basık). Transform ölçeğiyle boyutlandırın.</summary>
        public static Mesh Rock(int seed)
        {
            return Rock(seed, 0.22f);
        }

        /// <summary>Pürüzlülük (0..0.5) ayarlı kaya.</summary>
        public static Mesh Rock(int seed, float roughness)
        {
            roughness = Mathf.Clamp(roughness, 0f, 0.5f);
            return Cached(new Key(Shape.Rock, roughness, 0f, 0f, seed), () => RockFactory.BuildRockMesh(seed, roughness));
        }

        // ================================================================== Yardımcılar (diğer üreticiler kullanır)

        /// <summary>Kutuyu (düz yüzlü, metre UV) yazıcıya ekler.</summary>
        public static void AddBox(MeshBuilder b, int submesh, Vector3 center, Vector3 size)
        {
            AddBox(b, submesh, center, size, Quaternion.identity);
        }

        /// <summary>Döndürülmüş kutuyu yazıcıya ekler.</summary>
        public static void AddBox(MeshBuilder b, int submesh, Vector3 center, Vector3 size, Quaternion rotation)
        {
            var h = size * 0.5f;
            var c = new Vector3[8];
            for (var i = 0; i < 8; i++)
            {
                var local = new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z);
                c[i] = center + rotation * local;
            }

            // Yüzler (dışarıdan bakınca saat yönü).
            b.AddFlatQuad(submesh, c[0], c[2], c[3], c[1]); // -z
            b.AddFlatQuad(submesh, c[5], c[7], c[6], c[4]); // +z
            b.AddFlatQuad(submesh, c[4], c[6], c[2], c[0]); // -x
            b.AddFlatQuad(submesh, c[1], c[3], c[7], c[5]); // +x
            b.AddFlatQuad(submesh, c[2], c[6], c[7], c[3]); // +y
            b.AddFlatQuad(submesh, c[0], c[1], c[5], c[4]); // -y
        }

        /// <summary>Kesik koniyi (taban merkezi base, eksen yukarı) yazıcıya ekler.</summary>
        public static void AddFrustum(MeshBuilder b, int submesh, Vector3 basePoint, float bottomRadius, float topRadius, float height,
            int segments, bool capped, bool smooth)
        {
            AddFrustum(b, submesh, basePoint, Quaternion.identity, bottomRadius, topRadius, height, segments, capped, smooth);
        }

        /// <summary>Kesik koniyi (eksen rotation * up) yazıcıya ekler.</summary>
        public static void AddFrustum(MeshBuilder b, int submesh, Vector3 basePoint, Quaternion rotation, float bottomRadius, float topRadius,
            float height, int segments, bool capped, bool smooth)
        {
            segments = Mathf.Clamp(segments, 3, 128);
            var slope = (bottomRadius - topRadius) / Mathf.Max(0.001f, height);
            var circumference = Mathf.PI * 2f * Mathf.Max(bottomRadius, topRadius);
            if (smooth)
            {
                var start = b.VertexCount;
                for (var i = 0; i <= segments; i++)
                {
                    var t = i / (float)segments;
                    var angle = t * Mathf.PI * 2f;
                    var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    var normal = rotation * new Vector3(dir.x, slope, dir.z).normalized;
                    b.AddVertex(basePoint + rotation * (dir * bottomRadius), normal, new Vector2(t * circumference, 0f));
                    b.AddVertex(basePoint + rotation * (dir * topRadius + Vector3.up * height), normal, new Vector2(t * circumference, height));
                }

                for (var i = 0; i < segments; i++)
                {
                    var a = start + i * 2;
                    b.AddTriangle(submesh, a, a + 1, a + 2);
                    b.AddTriangle(submesh, a + 2, a + 1, a + 3);
                }
            }
            else
            {
                for (var i = 0; i < segments; i++)
                {
                    var p0 = basePoint + rotation * Ring(i, segments, bottomRadius, 0f);
                    var p1 = basePoint + rotation * Ring(i + 1, segments, bottomRadius, 0f);
                    var q0 = basePoint + rotation * Ring(i, segments, topRadius, height);
                    var q1 = basePoint + rotation * Ring(i + 1, segments, topRadius, height);
                    if (topRadius <= 0.0001f)
                        b.AddFlatTriangle(submesh, p0, q0, p1);
                    else
                        b.AddFlatQuad(submesh, p0, q0, q1, p1);
                }
            }

            if (!capped)
                return;

            var top = basePoint + rotation * (Vector3.up * height);
            for (var i = 0; i < segments; i++)
            {
                var p0 = basePoint + rotation * Ring(i, segments, bottomRadius, 0f);
                var p1 = basePoint + rotation * Ring(i + 1, segments, bottomRadius, 0f);
                if (bottomRadius > 0.0001f)
                    b.AddFlatTriangle(submesh, p0, p1, basePoint);
                if (topRadius > 0.0001f)
                {
                    var q0 = basePoint + rotation * Ring(i, segments, topRadius, height);
                    var q1 = basePoint + rotation * Ring(i + 1, segments, topRadius, height);
                    b.AddFlatTriangle(submesh, q0, top, q1);
                }
            }
        }

        /// <summary>Çember üzerindeki i. nokta (y yüksekliğinde).</summary>
        public static Vector3 Ring(int i, int segments, float radius, float y)
        {
            var angle = i / (float)segments * Mathf.PI * 2f;
            return new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
        }

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        private static Mesh Cached(Key key, Func<Mesh> build)
        {
            if (Cache.TryGetValue(key, out var mesh) && mesh != null)
                return mesh;

            mesh = build();
            Cache[key] = mesh;
            return mesh;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Cache.Clear();
        }
    }
}
