using UnityEngine;

namespace Project.Infrastructure.Characters
{
    internal static partial class CharacterMeshes
    {
        /// <summary>Tailored cloth surface with continuous UVs and real silhouette folds.</summary>
        public static Mesh Cloth(string key, Ring[] profile, float folds = 0.003f)
        {
            if (TryGet(key, out var cached)) return cached;
            Begin();
            const int sides = 24;
            const int rows = 32;
            for (var y = 0; y < rows; y++)
            for (var x = 0; x < sides; x++)
            {
                var start = Verts.Count;
                for (var c = 0; c < 4; c++)
                {
                    var t = (y + (c >= 2 ? 1 : 0)) / (float)rows;
                    var a = (x + (c == 0 || c == 3 ? 1 : 0)) * Mathf.PI * 2f / sides;
                    var p = ClothPoint(profile, t, a, folds);
                    var along = ClothPoint(profile, Mathf.Min(1f, t + 0.001f), a, folds)
                              - ClothPoint(profile, Mathf.Max(0f, t - 0.001f), a, folds);
                    var around = ClothPoint(profile, t, a + 0.001f, folds) - ClothPoint(profile, t, a - 0.001f, folds);
                    AddSmoothVertex(p, SafeNormalize(Vector3.Cross(along, around), new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a))),
                        new Vector2(a * 0.065f, p.y));
                }
                Tris.Add(start); Tris.Add(start + 1); Tris.Add(start + 2);
                Tris.Add(start); Tris.Add(start + 2); Tris.Add(start + 3);
            }
            return Finish(key);
        }

        private static Vector3 ClothPoint(Ring[] profile, float t, float angle, float amplitude)
        {
            var y = Mathf.Lerp(profile[0].Y, profile[profile.Length - 1].Y, t);
            var i = 0;
            while (i < profile.Length - 2 && profile[i + 1].Y < y) i++;
            var f = Mathf.InverseLerp(profile[i].Y, profile[i + 1].Y, y);
            f = f * f * (3f - 2f * f);
            var rx = Mathf.Lerp(profile[i].Rx, profile[i + 1].Rx, f);
            var rz = Mathf.Lerp(profile[i].Rz, profile[i + 1].Rz, f);
            var cz = Mathf.Lerp(profile[i].Cz, profile[i + 1].Cz, f);
            // Compression gathers near joints; quieter broad planes at mid-limb.
            var envelope = Mathf.Sin(Mathf.PI * t);
            var compression = 0.35f + 0.65f * Mathf.Pow(Mathf.Abs(2f * t - 1f), 2f);
            var fold = amplitude * envelope * (Mathf.Sin(t * 38f + Mathf.Sin(angle * 3f) * 1.6f) * compression
                     + 0.32f * Mathf.Sin(angle * 7f + t * 9f));
            return new Vector3(Mathf.Cos(angle) * (rx + fold), y, cz + Mathf.Sin(angle) * (rz + fold));
        }

        /// <summary>Continuous facial surface: brow ridge, recessed sockets, cheekbones, nose, lips and chin.</summary>
        public static Mesh SculptedHead()
        {
            const string key = "anatomicalHeadV1";
            if (TryGet(key, out var cached)) return cached;
            Begin();
            const int rows = 40, sides = 48;
            for (var y = 0; y < rows; y++)
            for (var x = 0; x < sides; x++)
            {
                var start = Verts.Count;
                for (var c = 0; c < 4; c++)
                {
                    var t = (y + (c >= 2 ? 1 : 0)) / (float)rows;
                    var a = (x + (c == 0 || c == 3 ? 1 : 0)) * Mathf.PI * 2f / sides;
                    var p = FacePoint(t, a);
                    var along = FacePoint(Mathf.Min(1f, t + 0.0005f), a) - FacePoint(Mathf.Max(0f, t - 0.0005f), a);
                    var around = FacePoint(t, a + 0.0005f) - FacePoint(t, a - 0.0005f);
                    AddSmoothVertex(p, SafeNormalize(Vector3.Cross(along, around), new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a))), new Vector2(a * 0.078f, p.y));
                }
                Tris.Add(start); Tris.Add(start + 1); Tris.Add(start + 2);
                Tris.Add(start); Tris.Add(start + 2); Tris.Add(start + 3);
            }
            return Finish(key);
        }

        private static readonly Ring[] FaceProfile =
        {
            new Ring(-0.021f, 0.006f, 0.012f, 0.040f), new Ring(-0.008f, 0.034f, 0.043f, 0.032f),
            new Ring(0.015f, 0.054f, 0.061f, 0.018f), new Ring(0.045f, 0.064f, 0.078f, 0.007f),
            new Ring(0.077f, 0.074f, 0.088f), new Ring(0.108f, 0.077f, 0.096f, -0.003f),
            new Ring(0.143f, 0.076f, 0.098f, -0.006f), new Ring(0.177f, 0.068f, 0.088f, -0.009f),
            new Ring(0.202f, 0.042f, 0.057f, -0.010f), new Ring(0.215f, 0.003f, 0.004f, -0.010f)
        };

        private static float Bump(float x, float y, float cx, float cy, float sx, float sy)
        {
            var dx = (x - cx) / sx; var dy = (y - cy) / sy;
            return Mathf.Exp(-(dx * dx + dy * dy) * 2f);
        }

        private static Vector3 FacePoint(float t, float angle)
        {
            var p = ClothPoint(FaceProfile, t, angle, 0f);
            var front = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Sin(angle) * 3f));
            var x = Mathf.Abs(p.x); var y = p.y;
            var depth = 0.009f * Bump(x, y, 0.041f, 0.073f, 0.022f, 0.022f)
                      - 0.011f * Bump(x, y, 0.032f, 0.105f, 0.021f, 0.013f)
                      + 0.008f * Bump(x, y, 0.033f, 0.122f, 0.029f, 0.012f)
                      + 0.023f * Bump(x, y, 0f, 0.090f, 0.012f, 0.029f)
                      + 0.020f * Bump(x, y, 0f, 0.075f, 0.018f, 0.013f)
                      + 0.006f * Bump(x, y, 0f, 0.040f, 0.030f, 0.009f)
                      + 0.009f * Bump(x, y, 0f, 0.012f, 0.033f, 0.015f);
            p.z += depth * front;
            return p;
        }

        public static Mesh CarrierPlate(string key, Vector3 size)
        {
            if (TryGet(key, out var cached)) return cached;
            Begin();
            var w = size.x * 0.5f; var h = size.y * 0.5f; var d = size.z * 0.5f;
            var outline = new[] { new Vector2(-w * 0.82f, -h), new Vector2(w * 0.82f, -h),
                new Vector2(w, -h * 0.82f), new Vector2(w, h * 0.50f), new Vector2(w * 0.60f, h),
                new Vector2(-w * 0.60f, h), new Vector2(-w, h * 0.50f), new Vector2(-w, -h * 0.82f) };
            for (var side = -1; side <= 1; side += 2)
            for (var i = 0; i < outline.Length; i++)
            {
                var a = outline[i]; var b = outline[(i + 1) % outline.Length];
                var p0 = new Vector3(a.x, a.y, side * (d - 0.007f));
                var p1 = new Vector3(b.x, b.y, side * (d - 0.007f));
                var q0 = new Vector3(a.x * 0.94f, a.y * 0.94f, side * d);
                var q1 = new Vector3(b.x * 0.94f, b.y * 0.94f, side * d);
                if (side > 0) { AddQuad(p0, p1, q1, q0); AddTri(new Vector3(0f, 0f, d), q0, q1); }
                else { AddQuad(p1, p0, q0, q1); AddTri(new Vector3(0f, 0f, -d), q1, q0); }
                if (side > 0) AddQuad(new Vector3(a.x, a.y, -d + 0.007f), new Vector3(b.x, b.y, -d + 0.007f), p1, p0);
            }
            return Finish(key);
        }

        public static Mesh Webbing(string key, int columns, int rows, float width, float height)
        {
            if (TryGet(key, out var cached)) return cached;
            Begin();
            for (var row = 0; row < rows; row++)
            for (var col = 0; col < columns; col++)
            {
                var x = (col - (columns - 1) * 0.5f) * width / columns;
                var y = (row - (rows - 1) * 0.5f) * height / rows;
                AddBox(new Vector3(x, y, 0f), new Vector3(width / columns * 0.88f, 0.013f, 0.003f));
                AddBox(new Vector3(x - width / columns * 0.42f, y, 0.0018f), new Vector3(0.0015f, 0.014f, 0.001f));
            }
            return Finish(key);
        }

        public static Mesh BootLacing()
        {
            const string key = "bootLacingSculpt";
            if (TryGet(key, out var cached)) return cached;
            Begin();
            for (var i = 0; i < 6; i++)
            {
                var y = 0.023f + i * 0.010f;
                var z = 0.048f - i * 0.001f;
                AddCord(new Vector3(-0.021f, y, z), new Vector3(0.021f, y + 0.010f, z), 0.0017f);
                AddCord(new Vector3(0.021f, y, z + 0.001f), new Vector3(-0.021f, y + 0.010f, z + 0.001f), 0.0017f);
            }
            return Finish(key);
        }

        private static void AddOval(Vector3 center, Vector3 radii)
        {
            const int rings = 8, segments = 12;
            for (var r = 0; r < rings; r++)
            for (var x = 0; x < segments; x++)
            {
                var a = -Mathf.PI * 0.5f + r * Mathf.PI / rings;
                var b = -Mathf.PI * 0.5f + (r + 1) * Mathf.PI / rings;
                var u = x * Mathf.PI * 2f / segments; var v = (x + 1) * Mathf.PI * 2f / segments;
                var p0 = Point(center, radii, a, u, 1f); var p1 = Point(center, radii, a, v, 1f);
                var p2 = Point(center, radii, b, v, 1f); var p3 = Point(center, radii, b, u, 1f);
                var n0 = EllipsoidNormal(radii, a, u); var n1 = EllipsoidNormal(radii, a, v);
                var n2 = EllipsoidNormal(radii, b, v); var n3 = EllipsoidNormal(radii, b, u);
                if (r == 0) AddTriN(p0, p2, p3, n0, n2, n3);
                else if (r == rings - 1) AddTriN(p0, p1, p3, n0, n1, n3);
                else AddQuadN(p0, p1, p2, p3, n0, n1, n2, n3);
            }
        }

        private static void AddCord(Vector3 a, Vector3 b, float radius)
        {
            var axis = (b - a).normalized;
            var u = Vector3.Cross(axis, Vector3.forward).normalized;
            if (u.sqrMagnitude < 0.1f) u = Vector3.right;
            var v = Vector3.Cross(axis, u).normalized;
            for (var i = 0; i < 8; i++)
            {
                var t0 = i * Mathf.PI * 0.25f; var t1 = (i + 1) * Mathf.PI * 0.25f;
                var n0 = u * Mathf.Cos(t0) + v * Mathf.Sin(t0);
                var n1 = u * Mathf.Cos(t1) + v * Mathf.Sin(t1);
                // Cross(circumferential, axial) points outwards for this frame.
                AddQuadN(a + n0 * radius, a + n1 * radius, b + n1 * radius, b + n0 * radius, n0, n1, n1, n0);
            }
        }
    }
}
