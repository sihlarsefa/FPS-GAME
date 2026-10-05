using Project.Infrastructure.World;
using UnityEngine;

namespace Project.Infrastructure.Transport
{
    /// <summary>
    /// İntikal araçları için zemin sorguları. WorldMetadata (arazi) varsa onu kullanır; yoksa aşağı ışın (yalnızca Default
    /// katmanı — araçların kendisi ve askerler sayılmaz); o da yoksa y = 0. Tahsis yapmaz.
    /// </summary>
    internal static class TransportGround
    {
        private const float RayTop = 1500f;
        private const float RayLength = 3000f;
        private const float ObstacleTolerance = 0.45f;

        private static readonly Collider[] OverlapBuffer = new Collider[24];

        /// <summary>Yüzey ışınlarında kullanılan maske (yapılar, kayalar, ağaçlar, arazi). Araç katmanı hariç.</summary>
        public static int SurfaceMask => 1 << GameLayers.Default;

        /// <summary>Arazinin yüksekliği (yapılar hariç, hızlı). Arazi yoksa yüzey ışını, o da yoksa 0.</summary>
        public static float TerrainHeight(Vector3 position)
        {
            var meta = WorldMetadata.Instance;
            if (meta != null && meta.Terrain != null && meta.Terrain.terrainData != null)
                return meta.SampleGroundHeight(position);

            return TrySurface(position, out var hit) ? hit.point.y : 0f;
        }

        /// <summary>En üstteki katı yüzey (yapı çatısı/ağaç/kaya dahil). Bulunamazsa arazi yüksekliği.</summary>
        public static float SurfaceHeight(Vector3 position)
        {
            if (TrySurface(position, out var hit))
                return hit.point.y;

            var meta = WorldMetadata.Instance;
            if (meta != null && meta.Terrain != null && meta.Terrain.terrainData != null)
                return meta.SampleGroundHeight(position);

            return 0f;
        }

        public static bool TrySurface(Vector3 position, out RaycastHit hit)
        {
            var origin = new Vector3(position.x, RayTop, position.z);
            return Physics.Raycast(origin, Vector3.down, out hit, RayLength, SurfaceMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>Su seviyesi (WorldMetadata yoksa çok düşük bir değer — su yok kabul).</summary>
        public static float WaterLevel
        {
            get
            {
                var meta = WorldMetadata.Instance;
                return meta != null ? meta.WaterLevel : -10000f;
            }
        }

        public static float MapHalfSize
        {
            get
            {
                var meta = WorldMetadata.Instance;
                return meta != null && meta.MapHalfSize > 1f ? meta.MapHalfSize : 512f;
            }
        }

        public static Vector2 MapCenter
        {
            get
            {
                var meta = WorldMetadata.Instance;
                return meta != null ? meta.MapCenter : Vector2.zero;
            }
        }

        /// <summary>
        /// İstenen noktanın çevresinde iniş/park için uygun (düz, engelsiz, su dışı, harita içi) bir yer arar.
        /// Halka halka genişler; ilk kabul edilebilir halkadaki en iyi noktayı döndürür. Hiçbiri yoksa en az kötü olanı.
        /// Dönen noktanın Y'si zemin yüksekliğidir (ayak izindeki en yüksek nokta).
        /// </summary>
        public static Vector3 FindLandingSite(Vector3 desired, float footprintRadius, float searchRadius, float maxHeightDelta, float clearHeight)
        {
            var halfSize = MapHalfSize;
            var center = MapCenter;
            var limit = Mathf.Max(10f, halfSize - footprintRadius - 8f);
            desired.x = Mathf.Clamp(desired.x, center.x - limit, center.x + limit);
            desired.z = Mathf.Clamp(desired.z, center.y - limit, center.y + limit);

            var bestScore = float.MaxValue;
            var best = desired;
            var bestY = SurfaceHeight(desired);
            var foundAcceptable = false;

            var ringStep = Mathf.Max(4f, footprintRadius);
            var rings = Mathf.Max(1, Mathf.CeilToInt(searchRadius / ringStep));
            for (var ring = 0; ring <= rings; ring++)
            {
                var radius = ring * ringStep;
                var samples = ring == 0 ? 1 : 8 + ring * 2;
                var ringBestScore = float.MaxValue;
                var ringBest = desired;
                var ringBestY = 0f;
                var ringFound = false;
                for (var i = 0; i < samples; i++)
                {
                    var angle = (i + ring * 0.37f) / samples * Mathf.PI * 2f;
                    var candidate = desired + new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius);
                    if (Mathf.Abs(candidate.x - center.x) > limit || Mathf.Abs(candidate.z - center.y) > limit)
                        continue;

                    var score = EvaluateSite(candidate, footprintRadius, clearHeight, out var siteY, out var delta, out var blocked);
                    score += radius * 0.05f;
                    var acceptable = !blocked && delta <= maxHeightDelta;
                    if (acceptable && score < ringBestScore)
                    {
                        ringBestScore = score;
                        ringBest = candidate;
                        ringBestY = siteY;
                        ringFound = true;
                    }

                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = candidate;
                        bestY = siteY;
                    }
                }

                if (ringFound)
                {
                    best = ringBest;
                    bestY = ringBestY;
                    foundAcceptable = true;
                    break;
                }
            }

            if (!foundAcceptable && float.IsNaN(bestY))
                bestY = TerrainHeight(best);

            best.y = bestY;
            return best;
        }

        /// <summary>Düşük skor iyidir. Engel/su/aşırı eğim büyük ceza alır.</summary>
        private static float EvaluateSite(Vector3 candidate, float radius, float clearHeight, out float siteY, out float delta, out bool blocked)
        {
            blocked = false;
            var minY = float.MaxValue;
            var maxY = float.MinValue;
            var obstaclePenalty = 0f;
            var water = WaterLevel;

            for (var i = 0; i <= 6; i++)
            {
                Vector3 p;
                if (i == 0)
                {
                    p = candidate;
                }
                else
                {
                    var angle = (i - 1) / 6f * Mathf.PI * 2f;
                    p = candidate + new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius);
                }

                var terrain = TerrainHeight(p);
                var surface = SurfaceHeight(p);
                if (surface > terrain + ObstacleTolerance)
                {
                    blocked = true;
                    obstaclePenalty += 50f;
                }

                var y = Mathf.Max(terrain, surface);
                if (y < minY)
                    minY = y;
                if (y > maxY)
                    maxY = y;

                if (terrain < water + 0.3f)
                {
                    blocked = true;
                    obstaclePenalty += 80f;
                }
            }

            siteY = maxY;
            delta = maxY - minY;

            // Gövdenin kapladığı hacimde ağaç gövdesi / direk / duvar var mı (arazi çarpıştırıcısı sayılmaz)?
            if (clearHeight > 0.5f)
            {
                var bottom = new Vector3(candidate.x, maxY + 1.2f, candidate.z);
                var top = new Vector3(candidate.x, maxY + Mathf.Max(1.3f, clearHeight), candidate.z);
                var count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, OverlapBuffer, SurfaceMask | (1 << GameLayers.Vehicle), QueryTriggerInteraction.Ignore);
                for (var i = 0; i < count; i++)
                {
                    var collider = OverlapBuffer[i];
                    OverlapBuffer[i] = null;
                    if (collider == null || collider is TerrainCollider)
                        continue;

                    blocked = true;
                    obstaclePenalty += 40f;
                }
            }

            return delta * 4f + obstaclePenalty;
        }

        /// <summary>Bir noktanın askeri bırakmak için uygun olup olmadığını (kapsül boşluğu) denetler.</summary>
        public static bool IsStandable(Vector3 groundPoint)
        {
            var bottom = groundPoint + Vector3.up * 0.45f;
            var top = groundPoint + Vector3.up * 1.5f;
            var count = Physics.OverlapCapsuleNonAlloc(bottom, top, 0.32f, OverlapBuffer, SurfaceMask | (1 << GameLayers.Vehicle), QueryTriggerInteraction.Ignore);
            var blocked = false;
            for (var i = 0; i < count; i++)
            {
                var collider = OverlapBuffer[i];
                OverlapBuffer[i] = null;
                if (collider == null || collider is TerrainCollider)
                    continue;

                blocked = true;
            }

            return !blocked && groundPoint.y > WaterLevel - 0.6f;
        }
    }
}
