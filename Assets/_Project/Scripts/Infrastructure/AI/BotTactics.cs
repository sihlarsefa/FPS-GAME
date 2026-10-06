using Project.Core.Domain;
using Project.Infrastructure.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace Project.Infrastructure.AI
{
    /// <summary>
    /// Tim düzeni (kama), siper arama ve el bombası atış çözümü — durumsuz, GC'siz yardımcılar.
    /// </summary>
    public static class BotTactics
    {
        /// <summary>Kama düzeninde yanal aralık (m).</summary>
        public const float WedgeSpacingX = 3.4f;

        /// <summary>Kama düzeninde geri aralık (m).</summary>
        public const float WedgeSpacingZ = 3.2f;

        private const int CoverBufferSize = 48;
        private const int MaxCoverLinecasts = 10;
        private static readonly Collider[] CoverBuffer = new Collider[CoverBufferSize];

        // ------------------------------------------------------------------ kama düzeni

        /// <summary>
        /// Liderin yerel uzayında kama (ters V) yuvası: 1 → sol-arka, 2 → sağ-arka, 3 → sol iki sıra geri ...
        /// index ≤ 0 → liderin hemen arkası. spacingScale toplanma emrinde düzeni sıkılaştırır.
        /// </summary>
        public static Vector3 WedgeOffset(int index, float spacingScale = 1f)
        {
            if (index <= 0)
                return new Vector3(0f, 0f, -WedgeSpacingZ * spacingScale);

            var row = (index + 1) / 2;
            var side = (index & 1) == 1 ? -1f : 1f;
            return new Vector3(side * row * WedgeSpacingX * spacingScale, 0f, -row * WedgeSpacingZ * spacingScale);
        }

        /// <summary>Kama yuvasının dünya konumu (lider ileriye doğru hareket ediyorsa biraz önüne kaydırılır).</summary>
        public static Vector3 WedgeSlot(Vector3 leaderPosition, Vector3 leaderVelocity, float formationYaw, int index, float spacingScale = 1f)
        {
            var rotation = Quaternion.Euler(0f, formationYaw, 0f);
            var lead = new Vector3(leaderVelocity.x, 0f, leaderVelocity.z) * 0.8f;
            return leaderPosition + lead + rotation * WedgeOffset(index, spacingScale);
        }

        /// <summary>Bir nokta çevresinde halka şeklinde dağılım (mevzi/taarruz noktası için).</summary>
        public static Vector3 RingSlot(Vector3 center, int index, float radius)
        {
            if (index <= 0)
                return center;

            // Altın açı: üye sayısından bağımsız düzgün dağılım.
            var angle = index * 137.5f * Mathf.Deg2Rad;
            var r = radius * (0.6f + 0.4f * ((index % 3) / 2f));
            return center + new Vector3(Mathf.Sin(angle) * r, 0f, Mathf.Cos(angle) * r);
        }

        // ------------------------------------------------------------------ NavMesh

        /// <summary>NavMesh üzerinde en yakın nokta (yoksa false).</summary>
        public static bool SampleNavMesh(Vector3 point, float maxDistance, out Vector3 result)
        {
            if (NavMesh.SamplePosition(point, out var hit, maxDistance, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }

            result = point;
            return false;
        }

        /// <summary>Zemine oturtma (NavMesh yoksa ışınla).</summary>
        public static bool TryGroundPoint(Vector3 point, out Vector3 ground)
        {
            var origin = point + Vector3.up * 30f;
            if (Physics.Raycast(origin, Vector3.down, out var hit, 200f, GameLayers.GroundMask, QueryTriggerInteraction.Ignore))
            {
                ground = hit.point;
                return true;
            }

            ground = point;
            return false;
        }

        // ------------------------------------------------------------------ siper

        /// <summary>
        /// Tehdide göre en yakın siper noktası: çevredeki katı nesnelerin (yapı duvarı, kaya, kum torbası, araç) tehdidin
        /// arka tarafında, NavMesh üzerinde ve tehdit gözünden görünmeyen bir nokta. Arazi ve dev nesneler atlanır.
        /// (Basit sürüm: <see cref="TryFindCoverScored"/> varsayılan sorgusu.)
        /// </summary>
        public static bool TryFindCover(Vector3 self, Vector3 threatEye, float searchRadius, bool useNavMesh, out Vector3 cover)
        {
            var query = new CoverQuery
            {
                Self = self,
                Threat = threatEye,
                Radius = searchRadius,
                UseNavMesh = useNavMesh,
                PreferredRange = 30f
            };

            var found = TryFindCoverScored(in query, out var spot);
            cover = found ? spot.Point : self;
            return found;
        }

        /// <summary>Siper arama sorgusu (tehdit yönü, ikinci tehdit, tim bağı, baskı).</summary>
        public struct CoverQuery
        {
            public Vector3 Self;
            public Vector3 Threat;
            public bool HasSecondary;
            public Vector3 Secondary;
            public float Radius;
            public bool UseNavMesh;
            public float PreferredRange;
            public bool HasAllyAnchor;
            public Vector3 AllyAnchor;
            public float Suppression;
        }

        /// <summary>Bulunan siper: duracak nokta, yüksek mi, başı/gövdeyi açacak köşe noktası (yaslanma/peek).</summary>
        public struct CoverSpot
        {
            public Vector3 Point;
            public bool High;
            public bool HasPeek;
            public Vector3 PeekPoint;
        }

        private const int MaxCoverLinecastsScored = 14;

        /// <summary>
        /// Skorlu siper: tehdit yönünden gizli (isteğe bağlı ikinci tehdit yönünden de), yürüme mesafesi, tehdide yaklaştırma,
        /// silah menzili, tim bütünlüğü ve baskı hesaba katılır (<see cref="BotCombatRules.CoverScore"/>). Yüksek siperde köşe noktası verir.
        /// </summary>
        public static bool TryFindCoverScored(in CoverQuery q, out CoverSpot spot)
        {
            spot = new CoverSpot { Point = q.Self };
            var self = q.Self;
            var threatEye = q.Threat;
            var mask = (1 << GameLayers.Default) | (1 << GameLayers.Vehicle);
            var count = Physics.OverlapSphereNonAlloc(self, q.Radius, CoverBuffer, mask, QueryTriggerInteraction.Ignore);
            if (count <= 0)
                return false;

            var bestScore = float.MaxValue;
            var found = false;
            var linecasts = 0;
            var selfToThreat = DistanceXZ(self, threatEye);
            var allyDistance = 0f;

            for (var i = 0; i < count && linecasts < MaxCoverLinecastsScored; i++)
            {
                var col = CoverBuffer[i];
                CoverBuffer[i] = null;
                if (col == null || col is TerrainCollider || !col.enabled)
                    continue;

                var bounds = col.bounds;
                var size = bounds.size;
                if (size.y < 0.85f || size.x > 70f || size.z > 70f)
                    continue;

                if (Mathf.Max(size.x, size.z) < 0.45f)
                    continue;

                var center = bounds.center;
                var away = center - threatEye;
                away.y = 0f;
                if (away.sqrMagnitude < 0.25f)
                    continue;

                away.Normalize();
                var extent = Mathf.Abs(away.x) * bounds.extents.x + Mathf.Abs(away.z) * bounds.extents.z;
                var candidate = center + away * (extent + 0.85f);
                candidate.y = bounds.min.y + 0.1f;

                // Tehdide çok yaklaştıran siper işe yaramaz.
                var candidateToThreat = DistanceXZ(candidate, threatEye);
                if (candidateToThreat < 6f || candidateToThreat < selfToThreat - 12f)
                    continue;

                var travel = DistanceXZ(self, candidate);
                var high = size.y >= 1.4f;
                if (q.HasAllyAnchor)
                    allyDistance = DistanceXZ(candidate, q.AllyAnchor);

                // Ucuz ön eleme: ikinci tehdit cezası eklenmeden bile en iyiden kötüyse görüş ışını harcama.
                var prelim = BotCombatRules.CoverScore(travel, candidateToThreat, selfToThreat, q.PreferredRange,
                    false, true, allyDistance, high, q.Suppression);
                if (prelim >= bestScore)
                    continue;

                if (q.UseNavMesh)
                {
                    if (!SampleNavMesh(candidate, 2.2f, out var onMesh))
                        continue;
                    candidate = onMesh;
                }
                else if (TryGroundPoint(candidate, out var ground) && Mathf.Abs(ground.y - candidate.y) < 3f)
                {
                    candidate = ground;
                }

                linecasts++;
                var probeHeight = high ? 1.2f : 0.75f;
                var probe = candidate + Vector3.up * probeHeight;
                if (!Physics.Linecast(threatEye, probe, GameLayers.LineOfSightMask, QueryTriggerInteraction.Ignore))
                    continue; // görünüyor — siper değil

                var hiddenSecondary = true;
                if (q.HasSecondary)
                {
                    linecasts++;
                    hiddenSecondary = Physics.Linecast(q.Secondary, probe, GameLayers.LineOfSightMask, QueryTriggerInteraction.Ignore);
                }

                var score = BotCombatRules.CoverScore(travel, candidateToThreat, selfToThreat, q.PreferredRange,
                    q.HasSecondary, hiddenSecondary, allyDistance, high, q.Suppression);
                if (score >= bestScore)
                    continue;

                bestScore = score;
                found = true;
                spot.Point = candidate;
                spot.High = high;
                spot.HasPeek = false;

                if (high)
                {
                    // Köşe: siperin yan kenarının hemen ötesi (tehdit hattına açılan nokta).
                    var lateral = new Vector3(-away.z, 0f, away.x);
                    var latExtent = Mathf.Abs(lateral.x) * bounds.extents.x + Mathf.Abs(lateral.z) * bounds.extents.z;
                    if (latExtent < 3.2f)
                    {
                        var side = Vector3.Dot(self - candidate, lateral) >= 0f ? 1f : -1f;
                        var peek = candidate + lateral * (side * (latExtent + 0.65f));
                        if (!q.UseNavMesh || SampleNavMesh(peek, 1.2f, out peek))
                        {
                            spot.HasPeek = true;
                            spot.PeekPoint = peek;
                        }
                    }
                }
            }

            for (var i = 0; i < count; i++)
                CoverBuffer[i] = null;

            return found;
        }

        /// <summary>
        /// Yapı/kaya siperi yoksa arazi çukuru: etrafta 8 yönde zemin yüksekliğini örnekle, tehdit gözünden görünmeyen
        /// en yakın noktayı seç. En fazla 8 ışın + 8 görüş testi (çağıran seyrekleştirir).
        /// </summary>
        public static bool TryFindDipCover(Vector3 self, Vector3 threatEye, float radius, bool useNavMesh, out Vector3 cover)
        {
            cover = self;
            var best = float.MaxValue;
            var found = false;
            var selfToThreat = DistanceXZ(self, threatEye);
            for (var i = 0; i < 8; i++)
            {
                var angle = i * 45f * Mathf.Deg2Rad;
                var point = self + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;
                if (!TryGroundPoint(point, out var ground))
                    continue;

                var toThreat = DistanceXZ(ground, threatEye);
                if (toThreat < 6f || toThreat < selfToThreat - 8f)
                    continue;

                if (useNavMesh)
                {
                    if (!SampleNavMesh(ground, 2f, out var onMesh))
                        continue;
                    ground = onMesh;
                }

                var travel = DistanceXZ(self, ground);
                if (travel >= best)
                    continue;

                if (!Physics.Linecast(threatEye, ground + Vector3.up * 0.9f, GameLayers.LineOfSightMask, QueryTriggerInteraction.Ignore))
                    continue; // görünüyor

                best = travel;
                cover = ground;
                found = true;
            }

            return found;
        }

        // ------------------------------------------------------------------ el bombası

        /// <summary>
        /// Sabit atış açısıyla <paramref name="to"/>'ya düşecek ilk hız. Açılar sırayla denenir; azami hızı aşarsa false.
        /// </summary>
        public static bool SolveThrow(Vector3 from, Vector3 to, float maxSpeed, out Vector3 velocity)
        {
            velocity = Vector3.zero;
            var g = Mathf.Abs(Physics.gravity.y);
            if (g < 0.1f)
                g = 9.81f;

            var delta = to - from;
            var flat = new Vector3(delta.x, 0f, delta.z);
            var d = flat.magnitude;
            if (d < 0.5f)
                return false;

            var dir = flat / d;
            var h = delta.y;

            for (var attempt = 0; attempt < 3; attempt++)
            {
                var angle = (attempt == 0 ? 38f : attempt == 1 ? 48f : 60f) * Mathf.Deg2Rad;
                var cos = Mathf.Cos(angle);
                var denom = 2f * cos * cos * (d * Mathf.Tan(angle) - h);
                if (denom <= 0.01f)
                    continue;

                var speed = Mathf.Sqrt(g * d * d / denom);
                if (float.IsNaN(speed) || speed > maxSpeed)
                    continue;

                velocity = dir * (speed * cos) + Vector3.up * (speed * Mathf.Sin(angle));
                return true;
            }

            return false;
        }

        /// <summary>Noktanın çevresinde (radius) canlı bir tim arkadaşı var mı (bomba/topçu güvenliği)?</summary>
        public static bool AllyNear(Vector3 point, float radius, int team, Combatant ignore)
        {
            var sqr = radius * radius;
            var all = CombatantRegistry.All;
            for (var i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || c == ignore || !c.IsAlive || c.Team != team)
                    continue;

                if ((c.transform.position - point).sqrMagnitude < sqr)
                    return true;
            }

            return false;
        }

        /// <summary>Silah için tercih edilen çatışma mesafesi (m).</summary>
        public static float PreferredRange(WeaponDefinitionData weapon)
        {
            if (weapon == null)
                return 2f;

            switch (weapon.Category)
            {
                case WeaponCategory.Pistol: return 14f;
                case WeaponCategory.Smg: return 16f;
                case WeaponCategory.Shotgun: return 9f;
                case WeaponCategory.AssaultRifle: return 35f;
                case WeaponCategory.Dmr: return 70f;
                case WeaponCategory.Lmg: return 45f;
                case WeaponCategory.Sniper: return 120f;
                default: return 25f;
            }
        }

        /// <summary>Silahın bu mesafede ne kadar uygun olduğu (yüksek = iyi). Bot silah değiştirme kararı için.</summary>
        public static float RangeSuitability(WeaponDefinitionData weapon, float distance)
        {
            if (weapon == null)
                return -100f;

            var range = weapon.Range > 1f ? weapon.Range : 150f;
            if (distance > range * 1.05f)
                return -50f;

            var score = 0f;
            switch (weapon.Category)
            {
                case WeaponCategory.Pistol:
                    score = distance < 20f ? 4f : 0f;
                    break;
                case WeaponCategory.Smg:
                    score = distance < 30f ? 9f : distance < 60f ? 4f : 1f;
                    break;
                case WeaponCategory.Shotgun:
                    score = distance < 15f ? 12f : distance < 25f ? 4f : -5f;
                    break;
                case WeaponCategory.AssaultRifle:
                    score = distance < 150f ? 9f : 5f;
                    break;
                case WeaponCategory.Lmg:
                    score = distance < 120f ? 9.5f : 6f;
                    break;
                case WeaponCategory.Dmr:
                    score = distance > 50f ? 10f : distance > 20f ? 6f : 3f;
                    break;
                case WeaponCategory.Sniper:
                    score = distance > 70f ? 12f : distance > 30f ? 6f : 0f;
                    break;
                default:
                    score = 5f;
                    break;
            }

            return score;
        }

        public static float DistanceXZ(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
