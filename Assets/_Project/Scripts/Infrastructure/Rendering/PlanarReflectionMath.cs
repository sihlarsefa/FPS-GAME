using System.Collections.Generic;
using UnityEngine;

namespace Project.Infrastructure.Rendering
{
    /// <summary>S3-yansima: Planar yansıma kademe tablosu (saf veri).</summary>
    public readonly struct PlanarReflectionTier
    {
        public readonly bool Enabled;
        /// <summary>Çözünürlük bölücü: 2 = yarı, 4 = çeyrek (ekran çözünürlüğüne göre).</summary>
        public readonly int ResolutionDivisor;
        /// <summary>Yansıma kamerası uzak kırpma (m).</summary>
        public readonly float FarClip;
        /// <summary>Her N karede bir yenile (1 = her kare).</summary>
        public readonly int UpdateInterval;
        /// <summary>Su yüzeyi bu mesafeden uzaksa yansıma çizilmez (m).</summary>
        public readonly float MaxSurfaceDistance;
        public readonly bool Shadows;
        /// <summary>Bot/oyuncu çizim mesafesi yansımada (m).</summary>
        public readonly float CharacterDistance;
        public readonly int MaxActiveSurfaces;

        public PlanarReflectionTier(bool enabled, int divisor, float far, int interval, float maxSurface, bool shadows, float charDist)
        {
            Enabled = enabled; ResolutionDivisor = divisor; FarClip = far; UpdateInterval = interval;
            MaxSurfaceDistance = maxSurface; Shadows = shadows; CharacterDistance = charDist; MaxActiveSurfaces = 1;
        }
    }

    /// <summary>
    /// S3-yansima: Planar yansıma matematiği (Unity bağımsız denecek kadar saf; Matrix4x4/Vector4 dışında Unity çağrısı yok, EditMode testli).
    /// </summary>
    public static class PlanarReflectionMath
    {
        public const int TierCount = 4;

        // Düşük kapalı; Orta çeyrek çözünürlük, 3 karede bir; Yüksek yarı, 2 karede bir; Ultra yarı, her kare + gölge.
        private static readonly PlanarReflectionTier[] Tiers =
        {
            new PlanarReflectionTier(false, 4, 0f,   0, 0f,   false, 0f),
            new PlanarReflectionTier(true,  4, 250f, 3, 120f, false, 80f),
            new PlanarReflectionTier(true,  2, 400f, 2, 220f, false, 140f),
            new PlanarReflectionTier(true,  2, 600f, 1, 350f, true,  220f)
        };

        public static PlanarReflectionTier Get(int tier) => Tiers[Mathf.Clamp(tier, 0, TierCount - 1)];

        /// <summary>QualitySettings seviyesini 0..3 kademeye eşler (kalite sayısı 4'ten azsa ölçekler).</summary>
        public static int TierFromQuality(int level, int levelCount)
        {
            if (levelCount <= 1) return 2;
            if (levelCount >= TierCount) return Mathf.Clamp(level, 0, TierCount - 1);
            return Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp(level, 0, levelCount - 1) * (TierCount - 1f) / (levelCount - 1f)), 0, TierCount - 1);
        }

        /// <summary>Yansıma dokusu boyutu (en az 64, 2'nin katı olması şart değil).</summary>
        public static Vector2Int TextureSize(int screenW, int screenH, int divisor)
        {
            var d = Mathf.Max(1, divisor);
            return new Vector2Int(Mathf.Max(64, screenW / d), Mathf.Max(64, screenH / d));
        }

        /// <summary>Bu kare yenilenmeli mi? interval ≤ 1 → her kare.</summary>
        public static bool ShouldUpdate(int frame, int interval) => interval <= 1 || frame % interval == 0;

        /// <summary>Düzlem (n, d: n·x + d = 0) için yansıtma matrisi (Householder).</summary>
        public static Matrix4x4 ReflectionMatrix(Vector4 plane)
        {
            var m = Matrix4x4.identity;
            m.m00 = 1f - 2f * plane.x * plane.x;
            m.m01 = -2f * plane.x * plane.y;
            m.m02 = -2f * plane.x * plane.z;
            m.m03 = -2f * plane.w * plane.x;
            m.m10 = -2f * plane.y * plane.x;
            m.m11 = 1f - 2f * plane.y * plane.y;
            m.m12 = -2f * plane.y * plane.z;
            m.m13 = -2f * plane.w * plane.y;
            m.m20 = -2f * plane.z * plane.x;
            m.m21 = -2f * plane.z * plane.y;
            m.m22 = 1f - 2f * plane.z * plane.z;
            m.m23 = -2f * plane.w * plane.z;
            return m;
        }

        /// <summary>Nokta + normalden düzlem vektörü (n.xyz, d).</summary>
        public static Vector4 PlaneFrom(Vector3 point, Vector3 normal)
        {
            var n = normal.normalized;
            return new Vector4(n.x, n.y, n.z, -Vector3.Dot(n, point));
        }

        /// <summary>Konumu düzleme göre yansıtır.</summary>
        public static Vector3 ReflectPosition(Vector3 p, Vector4 plane)
            => ReflectionMatrix(plane).MultiplyPoint3x4(p);

        /// <summary>Yönü düzleme göre yansıtır.</summary>
        public static Vector3 ReflectDirection(Vector3 d, Vector3 normal)
        {
            var n = normal.normalized;
            return d - 2f * Vector3.Dot(d, n) * n;
        }

        /// <summary>
        /// Eğik yakın kırpma için kamera uzayı düzlemi. side = +1 kamera düzlemin üstündeyken yansıma tarafı; clipOffset
        /// küçük pozitif kayma (kenar sızıntısını önler).
        /// </summary>
        public static Vector4 CameraSpacePlane(Matrix4x4 worldToCamera, Vector3 point, Vector3 normal, float side, float clipOffset)
        {
            var offsetPos = point + normal.normalized * clipOffset;
            var cpos = worldToCamera.MultiplyPoint(offsetPos);
            var cnormal = worldToCamera.MultiplyVector(normal).normalized * side;
            return new Vector4(cnormal.x, cnormal.y, cnormal.z, -Vector3.Dot(cpos, cnormal));
        }

        /// <summary>Kameranın düzlemin hangi yanında olduğu: +1 üst, -1 alt.</summary>
        public static float SideOf(Vector3 cameraPos, Vector3 planePoint, Vector3 normal)
            => Vector3.Dot(cameraPos - planePoint, normal) >= 0f ? 1f : -1f;

        /// <summary>Yansıma düzlemi için matris+düzlem kümesi: yansıtılmış view matrisi.</summary>
        public static Matrix4x4 ReflectedView(Matrix4x4 worldToCamera, Vector4 plane)
            => worldToCamera * ReflectionMatrix(plane);

        /// <summary>
        /// En yakın görünür yüzeyin indeksi (-1 = yok). Görünmeyenler ve maxDistance ötesindekiler elenir;
        /// eşitlikte düşük indeks kazanır. Çıktı yalnız 1 yüzeydir (MaxActiveSurfaces = 1).
        /// </summary>
        public static int PickNearest(IList<float> distances, IList<bool> visible, float maxDistance)
        {
            if (distances == null) return -1;
            var best = -1;
            var bestD = float.MaxValue;
            for (var i = 0; i < distances.Count; i++)
            {
                if (visible != null && (i >= visible.Count || !visible[i])) continue;
                var d = distances[i];
                if (float.IsNaN(d) || d < 0f || d > maxDistance) continue;
                if (d < bestD) { bestD = d; best = i; }
            }

            return best;
        }

        /// <summary>Yüzey yansımasının görsel gücü: yakında 1, MaxSurfaceDistance'a doğru 0 (kenarda yumuşak kapanma).</summary>
        public static float IntensityByDistance(float distance, float maxDistance)
        {
            if (maxDistance <= 0f) return 0f;
            var t = Mathf.Clamp01(distance / maxDistance);
            return 1f - t * t;
        }

        /// <summary>Yansıma kamerasının kültüryle dışlanacak katman maskesi: yalnız verilen katmanlar çizilir.</summary>
        public static int BuildCullingMask(params int[] layers)
        {
            var mask = 0;
            if (layers == null) return mask;
            for (var i = 0; i < layers.Length; i++)
                if (layers[i] >= 0 && layers[i] < 32) mask |= 1 << layers[i];
            return mask;
        }
    }
}
