using UnityEngine;

namespace Project.Infrastructure.Characters
{
    /// <summary>
    /// Yüksek ayrıntılı asker ağları: yumuşak gölgeli eliptik kesitli koniler (uzuv/gövde), parmaklı eller.
    /// Üçgen bütçesi: asker başına ~3-5k (uzuv başına 8 kenar x 3 halka ≈ 48-64 üçgen).
    /// </summary>
    internal static partial class CharacterMeshes
    {
        /// <summary>Halka profili: y yüksekliği, rx/rz yarıçapları, cz z kayması.</summary>
        public struct Ring
        {
            public float Y;
            public float Rx;
            public float Rz;
            public float Cz;

            public Ring(float y, float rx, float rz, float cz = 0f)
            {
                Y = y;
                Rx = rx;
                Rz = rz;
                Cz = cz;
            }
        }

        /// <summary>
        /// Y ekseni boyunca dönel (eliptik) profil: yumuşak normaller, metre ölçekli UV (u = çevre, v = y).
        /// Halkalar alttan üste sıralı olmalıdır. Kapaklar düz gölgeli.
        /// </summary>
        public static Mesh Lathe(string key, Ring[] rings, int sides, bool capBottom, bool capTop)
        {
            if (TryGet(key, out var cached))
                return cached;

            Begin();
            sides = Mathf.Clamp(sides, 3, 24);
            var n = rings != null ? rings.Length : 0;
            if (n < 2)
                return Finish(key);

            for (var r = 0; r < n - 1; r++)
            {
                for (var s = 0; s < sides; s++)
                {
                    var a0 = s / (float)sides * Mathf.PI * 2f;
                    var a1 = (s + 1) / (float)sides * Mathf.PI * 2f;
                    var p0 = RingPoint(rings[r], a0);
                    var p1 = RingPoint(rings[r], a1);
                    var p2 = RingPoint(rings[r + 1], a1);
                    var p3 = RingPoint(rings[r + 1], a0);
                    var n0 = RingNormal(rings, r, a0);
                    var n1 = RingNormal(rings, r, a1);
                    var n2 = RingNormal(rings, r + 1, a1);
                    var n3 = RingNormal(rings, r + 1, a0);
                    var circ0 = (rings[r].Rx + rings[r].Rz) * Mathf.PI;
                    var circ1 = (rings[r + 1].Rx + rings[r + 1].Rz) * Mathf.PI;
                    var u0 = s / (float)sides;
                    var u1 = (s + 1) / (float)sides;
                    var start = Verts.Count;
                    // AddQuad(p1, p0, p3, p2) ile aynı sıra.
                    AddSmoothVertex(p1, n1, new Vector2(u1 * circ0, rings[r].Y));
                    AddSmoothVertex(p0, n0, new Vector2(u0 * circ0, rings[r].Y));
                    AddSmoothVertex(p3, n3, new Vector2(u0 * circ1, rings[r + 1].Y));
                    AddSmoothVertex(p2, n2, new Vector2(u1 * circ1, rings[r + 1].Y));
                    Tris.Add(start);
                    Tris.Add(start + 1);
                    Tris.Add(start + 2);
                    Tris.Add(start);
                    Tris.Add(start + 2);
                    Tris.Add(start + 3);
                }
            }

            for (var s = 0; s < sides; s++)
            {
                var a0 = s / (float)sides * Mathf.PI * 2f;
                var a1 = (s + 1) / (float)sides * Mathf.PI * 2f;
                if (capTop)
                {
                    var top = rings[n - 1];
                    AddTri(new Vector3(0f, top.Y, top.Cz), RingPoint(top, a1), RingPoint(top, a0));
                }

                if (capBottom)
                {
                    var bottom = rings[0];
                    AddTri(new Vector3(0f, bottom.Y, bottom.Cz), RingPoint(bottom, a0), RingPoint(bottom, a1));
                }
            }

            return Finish(key);
        }

        /// <summary>
        /// Parmaklı el bloğu (parmak ucu aşağı, +z öne): avuç + 4 kıvrık parmak + başparmak. Sağ/sol için ayna (thumbSide: başparmağın x işareti).
        /// El kemiği ekseni: y aşağı doğru; boyut ≈ eski eldiven kutusu (0.05 x 0.1 x 0.085).
        /// </summary>
        public static Mesh Hand(string key, float thumbSide)
        {
            if (TryGet(key, out var cached))
                return cached;

            Begin();
            AddOval(new Vector3(0f, -0.035f, 0.004f), new Vector3(0.029f, 0.040f, 0.026f));
            AddOval(new Vector3(0f, -0.006f, 0f), new Vector3(0.027f, 0.021f, 0.024f));
            for (var i = 0; i < 4; i++)
            {
                var x = (i - 1.5f) * 0.013f;
                var length = i == 1 || i == 2 ? 0.051f : 0.043f;
                var a = new Vector3(x, -0.063f, 0.006f);
                var b = new Vector3(x, -0.063f - length * 0.56f, 0.022f);
                var c = new Vector3(x, -0.063f - length * 0.78f, 0.042f);
                AddCord(a, b, 0.0062f); AddCord(b, c, 0.0056f);
                AddOval(b, new Vector3(0.0064f, 0.008f, 0.007f));
                AddOval(c, new Vector3(0.0056f, 0.006f, 0.006f));
            }
            var thumb = new Vector3(thumbSide * 0.029f, -0.043f, 0.021f);
            var joint = new Vector3(thumbSide * 0.038f, -0.063f, 0.036f);
            var tip = new Vector3(thumbSide * 0.026f, -0.081f, 0.045f);
            AddCord(thumb, joint, 0.008f); AddCord(joint, tip, 0.007f);
            AddOval(joint, new Vector3(0.008f, 0.010f, 0.009f));
            AddOval(tip, new Vector3(0.007f, 0.008f, 0.007f));
            return Finish(key);
        }

        /// <summary>Shared rounded equipment mesh; preserves the original bounds and metre-scale UVs.</summary>
        public static Mesh RoundedBox(string key, Vector3 center, Vector3 size)
        {
            if (TryGet(key, out var cached))
                return cached;

            Begin();
            var half = size * 0.5f;
            var radius = Mathf.Min(half.x, Mathf.Min(half.y, half.z)) * 0.65f;
            var inner = half - Vector3.one * radius;
            // Six 3x3 face grids: flat centres, smooth bevels, no extra renderers.
            for (var axis = 0; axis < 3; axis++)
            {
                var u = (axis + 1) % 3;
                var v = (axis + 2) % 3;
                for (var sign = -1; sign <= 1; sign += 2)
                {
                    for (var y = 0; y < 3; y++)
                    for (var x = 0; x < 3; x++)
                    {
                        var start = Verts.Count;
                        for (var corner = 0; corner < 4; corner++)
                        {
                            var ix = x + (corner == 1 || corner == 2 ? 1 : 0);
                            var iy = y + (corner >= 2 ? 1 : 0);
                            var point = Vector3.zero;
                            point[axis] = sign * half[axis];
                            point[u] = RoundedGrid(ix, half[u], inner[u]);
                            point[v] = RoundedGrid(iy, half[v], inner[v]);
                            var nearest = new Vector3(Mathf.Clamp(point.x, -inner.x, inner.x),
                                Mathf.Clamp(point.y, -inner.y, inner.y), Mathf.Clamp(point.z, -inner.z, inner.z));
                            var normal = SafeNormalize(point - nearest, Vector3.up);
                            var position = nearest + normal * radius;
                            AddSmoothVertex(center + position, normal, new Vector2(position[u], position[v]));
                        }
                        Tris.Add(start); Tris.Add(start + (sign > 0 ? 1 : 2)); Tris.Add(start + (sign > 0 ? 2 : 1));
                        Tris.Add(start); Tris.Add(start + (sign > 0 ? 2 : 3)); Tris.Add(start + (sign > 0 ? 3 : 2));
                    }
                }
            }
            return Finish(key);
        }

        private static float RoundedGrid(int index, float half, float inner)
        {
            return index == 0 ? -half : index == 1 ? -inner : index == 2 ? inner : half;
        }

        /// <summary>Birden çok parçalı birleşik kutu ağı (kutu başına 12 üçgen).</summary>
        private static void AddBox(Vector3 center, Vector3 size)
        {
            var h = size * 0.5f;
            var b0 = center + new Vector3(-h.x, -h.y, -h.z);
            var b1 = center + new Vector3(h.x, -h.y, -h.z);
            var b2 = center + new Vector3(h.x, -h.y, h.z);
            var b3 = center + new Vector3(-h.x, -h.y, h.z);
            var t0 = center + new Vector3(-h.x, h.y, -h.z);
            var t1 = center + new Vector3(h.x, h.y, -h.z);
            var t2 = center + new Vector3(h.x, h.y, h.z);
            var t3 = center + new Vector3(-h.x, h.y, h.z);
            AddQuad(b3, b2, t2, t3);
            AddQuad(b1, b0, t0, t1);
            AddQuad(b2, b1, t1, t2);
            AddQuad(b0, b3, t3, t0);
            AddQuad(t3, t2, t1, t0);
            AddQuad(b0, b1, b2, b3);
        }

        private static Vector3 RingPoint(Ring ring, float angle)
        {
            return new Vector3(Mathf.Cos(angle) * ring.Rx, ring.Y, ring.Cz + Mathf.Sin(angle) * ring.Rz);
        }

        private static Vector3 RingNormal(Ring[] rings, int index, float angle)
        {
            var below = rings[Mathf.Max(0, index - 1)];
            var above = rings[Mathf.Min(rings.Length - 1, index + 1)];
            var ty = RingPoint(above, angle) - RingPoint(below, angle);
            var ring = rings[index];
            var ta = new Vector3(-Mathf.Sin(angle) * ring.Rx, 0f, Mathf.Cos(angle) * ring.Rz);
            var normal = Vector3.Cross(ty, ta);
            return SafeNormalize(normal, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)));
        }

        private static void AddSmoothVertex(Vector3 p, Vector3 n, Vector2 uv)
        {
            Verts.Add(p);
            Normals.Add(n);
            Uvs.Add(uv);
        }
    }
}
