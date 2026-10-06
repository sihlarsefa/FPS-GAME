using System;

namespace Project.Application.AI
{
    /// <summary>
    /// Kanattan dolaşma rota planlayıcı (saf mantık). Killzone/F.E.A.R. yaklaşımı: düşmanın ateş konisi içindeki
    /// noktalar yüksek maliyetli (açıkta kalma), koni dışına çıkıp yan/arkadan varış ödüllü.
    /// Aday: düşman etrafında yay üzerinde (sol/sağ, farklı açı) bir varış noktası; rota = bot → ara nokta → varış.
    /// Yürünebilirlik (NavMesh) çağırandan gelir (<c>blocked</c> dizisi); bu sınıf geometri ve puanı çözer.
    /// </summary>
    public static class FlankRoutePlanner
    {
        public const int SampleCount = 8;
        public const float ExposureWeight = 9f;
        public const float BehindBonus = 14f;

        /// <summary>Düşman ateş konisinde mi: p, düşman e, bakış yönü (yaw, derece; 0 = +Z), koni yarı açısı, menzil.</summary>
        public static bool InFiringCone(float px, float pz, float ex, float ez, float enemyYawDegrees,
            float halfConeDegrees, float range)
        {
            var dx = px - ex;
            var dz = pz - ez;
            var d2 = dx * dx + dz * dz;
            if (d2 > range * range)
                return false;
            if (d2 < 0.01f)
                return true;
            var yaw = MathF.Atan2(dx, dz) * 57.29578f;
            return MathF.Abs(DeltaAngle(yaw, enemyYawDegrees)) <= halfConeDegrees;
        }

        /// <summary>Rota üzerindeki örnek noktalardan açıkta kalan oran 0..1 (bot → ara → varış polylinei).</summary>
        public static float RouteExposure(float sx, float sz, float mx, float mz, float tx, float tz,
            float ex, float ez, float enemyYawDegrees, float halfConeDegrees, float range)
        {
            var exposed = 0;
            for (var i = 1; i <= SampleCount; i++)
            {
                var t = i / (float)SampleCount;
                float x, z;
                if (t < 0.5f)
                {
                    var u = t * 2f;
                    x = sx + (mx - sx) * u; z = sz + (mz - sz) * u;
                }
                else
                {
                    var u = (t - 0.5f) * 2f;
                    x = mx + (tx - mx) * u; z = mz + (tz - mz) * u;
                }
                if (InFiringCone(x, z, ex, ez, enemyYawDegrees, halfConeDegrees, range))
                    exposed++;
            }
            return exposed / (float)SampleCount;
        }

        /// <summary>Aday varış noktası: düşman etrafında, bot yönünden <paramref name="angleDegrees"/> kadar yan, <paramref name="radius"/> mesafede.</summary>
        public static void ArcPoint(float sx, float sz, float ex, float ez, float angleDegrees, float side, float radius,
            out float x, out float z)
        {
            var dx = sx - ex;
            var dz = sz - ez;
            var baseYaw = MathF.Atan2(dx, dz);
            var yaw = baseYaw + angleDegrees * 0.0174533f * (side >= 0f ? 1f : -1f);
            x = ex + MathF.Sin(yaw) * radius;
            z = ez + MathF.Cos(yaw) * radius;
        }

        /// <summary>Varış noktası düşmanın arkasında/yanında mı (bakışından >= 100° sapma = sırt).</summary>
        public static float BehindFactor(float tx, float tz, float ex, float ez, float enemyYawDegrees)
        {
            var yaw = MathF.Atan2(tx - ex, tz - ez) * 57.29578f;
            var off = MathF.Abs(DeltaAngle(yaw, enemyYawDegrees));
            if (off <= 60f) return 0f;
            return MathF.Min(1f, (off - 60f) / 80f);
        }

        /// <summary>
        /// Aday puanı (DÜŞÜK = iyi). pathLength: toplam rota (m), exposure01: RouteExposure, behind01: BehindFactor,
        /// sameSideAllyFlankers: aynı yönde kanat yapan dost sayısı (çeşitlilik için ceza), blocked: yürünemez.
        /// </summary>
        public static float Score(float pathLength, float exposure01, float behind01, int sameSideAllyFlankers,
            float maxPath, bool blocked)
        {
            if (blocked || pathLength > maxPath)
                return float.MaxValue;
            var s = pathLength;
            s += exposure01 * exposure01 * ExposureWeight * 10f;
            s -= behind01 * BehindBonus;
            s += sameSideAllyFlankers * 18f;
            return s;
        }

        /// <summary>
        /// En iyi kanat adayı: açı listesi × iki yön. Dönüş: bulundu mu; seçilen açı/yön ve varış noktası.
        /// <paramref name="blockedMask"/> bit i*2+side (side 0 = sol(-1), 1 = sağ(+1)) = yürünemez (null = hepsi açık).
        /// </summary>
        public static bool TryChoose(float sx, float sz, float ex, float ez, float enemyYawDegrees, float halfConeDegrees,
            float range, float arcRadius, float[] anglesDegrees, int leftAllies, int rightAllies, float maxPath,
            ulong blockedMask, out float bestAngle, out float bestSide, out float bestX, out float bestZ)
        {
            bestAngle = 0f; bestSide = 1f; bestX = sx; bestZ = sz;
            if (anglesDegrees == null || anglesDegrees.Length == 0)
                return false;
            var best = float.MaxValue;
            var found = false;
            for (var i = 0; i < anglesDegrees.Length && i < 32; i++)
            {
                for (var k = 0; k < 2; k++)
                {
                    var side = k == 0 ? -1f : 1f;
                    ArcPoint(sx, sz, ex, ez, anglesDegrees[i], side, arcRadius, out var tx, out var tz);
                    // Ara nokta: bot ile varışın orta noktasını düşmandan uzağa (yay dışına) kaydır.
                    var mx = (sx + tx) * 0.5f;
                    var mz = (sz + tz) * 0.5f;
                    var ox = mx - ex;
                    var oz = mz - ez;
                    var om = MathF.Sqrt(ox * ox + oz * oz);
                    if (om > 0.01f)
                    {
                        var push = MathF.Max(0f, arcRadius - om) + 3f;
                        mx += ox / om * push;
                        mz += oz / om * push;
                    }
                    var len = Dist(sx, sz, mx, mz) + Dist(mx, mz, tx, tz);
                    var exposure = RouteExposure(sx, sz, mx, mz, tx, tz, ex, ez, enemyYawDegrees, halfConeDegrees, range);
                    var behind = BehindFactor(tx, tz, ex, ez, enemyYawDegrees);
                    var blocked = (blockedMask & (1UL << (i * 2 + k))) != 0UL;
                    var same = side < 0f ? leftAllies : rightAllies;
                    var score = Score(len, exposure, behind, same, maxPath, blocked);
                    if (score < best)
                    {
                        best = score; found = true;
                        bestAngle = anglesDegrees[i]; bestSide = side; bestX = tx; bestZ = tz;
                    }
                }
            }
            return found;
        }

        /// <summary>Kanatçı varışta ne zaman ateş açmalı: hedefe 25° içinde yan açı kazandı ve görüş var → "şimdi" (diğer ekip bastırmayı keser).</summary>
        public static bool ShouldCommitFire(float distanceToTarget, float flankAngleDegrees, bool hasLineOfSight)
        {
            return hasLineOfSight && (flankAngleDegrees >= 35f || distanceToTarget < 14f);
        }

        /// <summary>Kanat iptal: rota boyunca yeni düşman göründü (koni ihlali) ya da sayı üstünlüğü kayboldu.</summary>
        public static bool ShouldAbort(float exposure01, float health01, bool newThreatOnRoute, float secondsElapsed, float timeLimit)
        {
            return newThreatOnRoute || exposure01 > 0.6f || health01 < 0.35f || secondsElapsed > timeLimit;
        }

        private static float Dist(float ax, float az, float bx, float bz)
        {
            var dx = ax - bx; var dz = az - bz;
            return MathF.Sqrt(dx * dx + dz * dz);
        }

        public static float DeltaAngle(float a, float b)
        {
            var d = (a - b) % 360f;
            if (d > 180f) d -= 360f;
            if (d < -180f) d += 360f;
            return d;
        }
    }
}
