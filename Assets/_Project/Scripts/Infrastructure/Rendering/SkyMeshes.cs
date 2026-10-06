using Project.Core.Domain;
using UnityEngine;

namespace Project.Infrastructure.Rendering
{
    /// <summary>Gökyüzü katmanları için prosedürel mesh ve doku üreticileri (bulut kubbesi, dağ halkası, ışık huzmeleri, güneş diski).</summary>
    public static class SkyMeshes
    {
        public const int CloudSegments = 48;
        public const int CloudRings = 10;

        /// <summary>Yassı yarım küre bulut kubbesi; köşe alfası ufukta 0'a iner, UV yukarıdan düzlemsel izdüşüm.</summary>
        public static Mesh BuildCloudDome(float radius, float flatten, float uvTiling)
        {
            var verts = new Vector3[(CloudRings + 1) * (CloudSegments + 1)];
            var cols = new Color[verts.Length];
            var uvs = new Vector2[verts.Length];
            var tris = new int[CloudRings * CloudSegments * 6];
            for (var r = 0; r <= CloudRings; r++)
            {
                var e = r / (float)CloudRings * Mathf.PI * 0.5f;
                var ce = Mathf.Cos(e);
                var se = Mathf.Sin(e);
                var alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.03f, 0.3f, se));
                for (var s = 0; s <= CloudSegments; s++)
                {
                    var a = s / (float)CloudSegments * Mathf.PI * 2f;
                    var i = r * (CloudSegments + 1) + s;
                    var x = Mathf.Cos(a) * ce;
                    var z = Mathf.Sin(a) * ce;
                    verts[i] = new Vector3(x * radius, se * radius * flatten, z * radius);
                    uvs[i] = new Vector2(x * uvTiling, z * uvTiling);
                    cols[i] = new Color(1f, 1f, 1f, alpha);
                }
            }

            var t = 0;
            for (var r = 0; r < CloudRings; r++)
            for (var s = 0; s < CloudSegments; s++)
            {
                var a = r * (CloudSegments + 1) + s;
                var b = a + CloudSegments + 1;
                tris[t++] = a; tris[t++] = b; tris[t++] = a + 1;
                tris[t++] = a + 1; tris[t++] = b; tris[t++] = b + 1;
            }

            var mesh = new Mesh { name = "HK_CloudDome" };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.colors = cols;
            mesh.triangles = tris;
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(radius * 2.2f, radius * 2.2f, radius * 2.2f));
            return mesh;
        }

        /// <summary>Uzak dağ silüeti halkası: iç yüzü kameraya bakan silindirik duvar. Tepe köşe rengi ridge, taban rengi base.</summary>
        public static Mesh BuildMountainRing(float radius, float baseHeight, float minHeight, float maxHeight, int seed, int segments, Color ridge, Color baseColor)
        {
            var verts = new Vector3[(segments + 1) * 2];
            var cols = new Color[verts.Length];
            var tris = new int[segments * 6];
            for (var s = 0; s <= segments; s++)
            {
                var u = (s % segments) / (float)segments;
                var fbm = Mathf.Clamp01(HorizonVistaMath.Finite(SkyWaterRules.TileableFbm(u, 0.37f, 5, 4, seed), 0.5f));
                var h = HorizonVistaMath.Finite(Mathf.Lerp(minHeight, maxHeight, Mathf.Pow(fbm, 1.4f) * 1.6f), minHeight);
                var a = s / (float)segments * Mathf.PI * 2f;
                var x = Mathf.Cos(a) * radius;
                var z = Mathf.Sin(a) * radius;
                verts[s * 2] = new Vector3(x, baseHeight, z);
                verts[s * 2 + 1] = new Vector3(x, baseHeight + h, z);
                cols[s * 2] = baseColor;
                cols[s * 2 + 1] = ridge;
            }

            for (var s = 0; s < segments; s++)
            {
                var a = s * 2;
                var t = s * 6;
                tris[t] = a; tris[t + 1] = a + 1; tris[t + 2] = a + 2;
                tris[t + 3] = a + 2; tris[t + 4] = a + 1; tris[t + 5] = a + 3;
            }

            var mesh = new Mesh { name = "HK_MountainRing" };
            mesh.vertices = verts;
            mesh.colors = cols;
            mesh.triangles = tris;
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(radius * 2.2f, radius, radius * 2.2f));
            return mesh;
        }

        /// <summary>Güneşten yayılan ışık huzmeleri: sky küresi teğet düzleminde ince, uca doğru genişleyen ve sönen dörtgenler.</summary>
        public static Mesh BuildShafts(Vector3 sunDir, float radius, int count, float length, float baseAlpha, int seed)
        {
            var dir = sunDir.normalized;
            var center = dir * radius;
            var right = Vector3.Cross(Vector3.up, dir);
            if (right.sqrMagnitude < 1e-4f)
                right = Vector3.right;
            right.Normalize();
            var up = Vector3.Cross(dir, right).normalized;
            var verts = new Vector3[count * 4];
            var cols = new Color[count * 4];
            var tris = new int[count * 6];
            var rnd = new System.Random(seed);
            for (var i = 0; i < count; i++)
            {
                // Yukarı yarım düzlem ağırlıklı yelpaze (alt yarı arazi altında kalır).
                var ang = Mathf.Lerp(-20f, 200f, (i + (float)rnd.NextDouble() * 0.6f) / count) * Mathf.Deg2Rad;
                var along = right * Mathf.Cos(ang) + up * Mathf.Sin(ang);
                var side = Vector3.Cross(dir, along).normalized;
                var len = length * (0.6f + (float)rnd.NextDouble() * 0.6f);
                var w0 = 14f;
                var w1 = 70f + (float)rnd.NextDouble() * 60f;
                var o = i * 4;
                verts[o] = center - side * w0;
                verts[o + 1] = center + side * w0;
                verts[o + 2] = center + along * len + side * w1;
                verts[o + 3] = center + along * len - side * w1;
                var a0 = baseAlpha * (0.6f + (float)rnd.NextDouble() * 0.4f);
                cols[o] = cols[o + 1] = new Color(1f, 1f, 1f, a0);
                cols[o + 2] = cols[o + 3] = new Color(1f, 1f, 1f, 0f);
                var t = i * 6;
                tris[t] = o; tris[t + 1] = o + 1; tris[t + 2] = o + 2;
                tris[t + 3] = o; tris[t + 4] = o + 2; tris[t + 5] = o + 3;
            }

            var mesh = new Mesh { name = "HK_LightShafts" };
            mesh.vertices = verts;
            mesh.colors = cols;
            mesh.triangles = tris;
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(radius * 2.4f, radius * 2.4f, radius * 2.4f));
            return mesh;
        }

        /// <summary>Kameraya bakan birim dörtgen (güneş/ay diski ve halesi). UV 0..1.</summary>
        public static Mesh BuildQuad()
        {
            var mesh = new Mesh { name = "HK_SkyQuad" };
            mesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(4000f, 4000f, 4000f));
            return mesh;
        }

        /// <summary>Radyal doku: sert=true keskin kenarlı disk, false yumuşak parıltı (hale).</summary>
        public static Texture2D BuildRadialTexture(int size, bool hardDisc)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = hardDisc ? "HK_SunDisc" : "HK_SunGlow", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var d = new Vector2((x + 0.5f) / size - 0.5f, (y + 0.5f) / size - 0.5f).magnitude * 2f;
                var a = hardDisc ? 1f - Mathf.SmoothStep(0.82f, 1f, d) : Mathf.Pow(Mathf.Clamp01(1f - d), 2.4f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
            }

            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        /// <summary>Döngüsel bulut dokusu: gri tonlu gövde, alfa = kapsama eşiğinden geçmiş yoğunluk.</summary>
        public static Texture2D BuildCloudTexture(int size, float coverage, int seed)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "HK_Clouds", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var d = SkyWaterRules.TileableFbm(x / (float)size, y / (float)size, 4, 5, seed);
                var a = SkyWaterRules.CloudAlpha(d, coverage);
                var shade = Mathf.Lerp(0.72f, 1f, Mathf.Clamp01(1f - d + 0.2f));
                var b = (byte)Mathf.RoundToInt(shade * 255f);
                px[y * size + x] = new Color32(b, b, b, (byte)Mathf.RoundToInt(a * 255f));
            }

            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }
    }
}
