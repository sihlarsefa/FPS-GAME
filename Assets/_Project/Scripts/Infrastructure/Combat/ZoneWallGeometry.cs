using System;
using Project.Core.Domain;
using UnityEngine;

namespace Project.Infrastructure.Combat
{
    /// <summary>
    /// Bölge duvarı için saf geometri/matematik (Unity nesnesi gerektirmez, EditMode'da test edilir): segment konumları,
    /// küçülme yumuşatması ve interpolasyonu, yakınlık sönümü, tekrar sayısı.
    /// </summary>
    public static class ZoneWallGeometry
    {
        public const int Segments = 128;
        public const float BandHeight = 60f;
        /// <summary>Duvarın kamera yüksekliğinin kaç metre altından başladığı.</summary>
        public const float BandBelowCamera = 20f;
        public const float PatternWorldSize = 60f;

        /// <summary>i. segment sütununun birim çember üzerindeki yönü (x, z).</summary>
        public static void Direction(int index, int segments, out float x, out float z)
        {
            if (segments < 3)
                throw new ArgumentOutOfRangeException(nameof(segments));
            var angle = (float)index / segments * Mathf.PI * 2f;
            x = Mathf.Cos(angle);
            z = Mathf.Sin(angle);
        }

        /// <summary>Dünya uzayında i. sütunun alt (y0) ya da üst (y0+BandHeight) noktası.</summary>
        public static Vector3 WorldPoint(int index, int segments, float centerX, float centerZ, float radius, float baseY, bool top)
        {
            Direction(index, segments, out var dx, out var dz);
            return new Vector3(centerX + dx * radius, baseY + (top ? BandHeight : 0f), centerZ + dz * radius);
        }

        public static int VertexCount(int segments) => (segments + 1) * 2;
        public static int IndexCount(int segments) => segments * 6;

        /// <summary>
        /// Birim yarıçaplı, 0..1 yükseklikli kapaksız halka bandı. Köşe 2*i = alt, 2*i+1 = üst; u=0..1 çevre, v=0..1 yükseklik.
        /// Tek yüzlü üçgenler; malzeme Cull Off ile iki taraftan görünür.
        /// </summary>
        public static void Fill(int segments, Vector3[] vertices, Vector2[] uvs, int[] triangles)
        {
            if (vertices.Length < VertexCount(segments) || uvs.Length < VertexCount(segments) || triangles.Length < IndexCount(segments))
                throw new ArgumentException("Diziler küçük.");

            for (var i = 0; i <= segments; i++)
            {
                Direction(i, segments, out var x, out var z);
                var u = (float)i / segments;
                vertices[i * 2] = new Vector3(x, 0f, z);
                vertices[i * 2 + 1] = new Vector3(x, 1f, z);
                uvs[i * 2] = new Vector2(u, 0f);
                uvs[i * 2 + 1] = new Vector2(u, 1f);
            }

            var t = 0;
            for (var i = 0; i < segments; i++)
            {
                var a = i * 2;
                var b = a + 2;
                triangles[t++] = a;
                triangles[t++] = a + 1;
                triangles[t++] = b;
                triangles[t++] = b;
                triangles[t++] = a + 1;
                triangles[t++] = b + 1;
            }
        }

        /// <summary>Üstel yumuşatma (kare hızından bağımsız). rate sn⁻¹.</summary>
        public static float Smooth(float current, float target, float dt, float rate)
        {
            if (dt <= 0f)
                return current;
            var k = 1f - Mathf.Exp(-Mathf.Max(0f, rate) * dt);
            return current + (target - current) * k;
        }

        /// <summary>İki bölge durumu arasında doğrusal interpolasyon (t 0..1'e kenetlenir).</summary>
        public static ZoneState Lerp(ZoneState a, ZoneState b, float t)
        {
            t = Mathf.Clamp01(t);
            return new ZoneState(
                a.CenterX + (b.CenterX - a.CenterX) * t,
                a.CenterZ + (b.CenterZ - a.CenterZ) * t,
                a.Radius + (b.Radius - a.Radius) * t,
                a.DamagePerSecond + (b.DamagePerSecond - a.DamagePerSecond) * t);
        }

        /// <summary>Gösterilen bölgeyi hedefe yumuşatır; ilk kare/büyük sıçrama/yarıçap artışında doğrudan hedefe geçer.</summary>
        public static ZoneState Follow(ZoneState shown, ZoneState target, float dt, float rate, bool snap)
        {
            if (snap || shown.Radius <= 0f || target.Radius > shown.Radius + 0.5f)
                return target;
            var dist = Mathf.Sqrt((target.CenterX - shown.CenterX) * (target.CenterX - shown.CenterX)
                                  + (target.CenterZ - shown.CenterZ) * (target.CenterZ - shown.CenterZ));
            if (dist > 400f)
                return target;
            var k = 1f - Mathf.Exp(-Mathf.Max(0f, rate) * Mathf.Max(0f, dt));
            return Lerp(shown, target, k);
        }

        /// <summary>Çevre boyunca desen tekrar sayısı (tam sayı: dikişte kayma olmaz), en az 1.</summary>
        public static int TilesAround(float radius)
        {
            var circumference = 2f * Mathf.PI * Mathf.Max(0f, radius);
            return Mathf.Max(1, Mathf.RoundToInt(circumference / PatternWorldSize));
        }

        /// <summary>Kameranın duvara uzaklığına göre parlaklık çarpanı: yakında 0,45'e düşer (göz almasın), 25 m ve ötesinde 1.</summary>
        public static float ProximityFade(float distanceToWall)
        {
            var t = Mathf.Clamp01(Mathf.Abs(distanceToWall) / 25f);
            return Mathf.Lerp(0.45f, 1f, t * t * (3f - 2f * t));
        }

        /// <summary>Konumun merkezden yatay uzaklığı - yarıçap (negatif = içeride).</summary>
        public static float SignedDistance(ZoneState zone, float x, float z)
        {
            var dx = x - zone.CenterX;
            var dz = z - zone.CenterZ;
            return Mathf.Sqrt(dx * dx + dz * dz) - zone.Radius;
        }

        /// <summary>Duvar bandının alt kenarı: kamera yüksekliğine göre.</summary>
        public static float BaseY(float cameraY) => cameraY - BandBelowCamera;

        /// <summary>
        /// Duvar deseni alfa değeri (0..1) için dikey sönüm: alt ve üstte yumuşakça 0'a iner, ortada 1.
        /// v 0..1 bant boyunca.
        /// </summary>
        public static float VerticalFade(float v)
        {
            v = Mathf.Clamp01(v);
            var edge = Mathf.Min(v, 1f - v) / 0.28f;
            edge = Mathf.Clamp01(edge);
            return edge * edge * (3f - 2f * edge);
        }
    }
}
