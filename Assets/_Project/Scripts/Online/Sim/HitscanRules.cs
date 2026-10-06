using System.Collections.Generic;
using UnityEngine;

namespace Project.Online.Sim
{
    /// <summary>Sunucu atış doğrulaması ret nedeni.</summary>
    public enum ShotReject : byte
    {
        None = 0,
        FireRate,
        NoAmmo,
        BadDirection,
        BadNumbers
    }

    /// <summary>Doğrulanmış atışın sunucu tarafı parametreleri (geri sarma tick'i ve temizlenmiş ışın).</summary>
    public readonly struct ShotCheck
    {
        public readonly ShotReject Reject;
        public readonly uint RewindTick;
        public readonly uint RewindTicks;
        public readonly bool WasClamped;
        public readonly Vector3 Origin;
        public readonly Vector3 Direction;
        public bool Accepted => Reject == ShotReject.None;

        public ShotCheck(ShotReject reject, uint rewindTick, uint rewindTicks, bool wasClamped, Vector3 origin, Vector3 direction)
        {
            Reject = reject;
            RewindTick = rewindTick;
            RewindTicks = rewindTicks;
            WasClamped = wasClamped;
            Origin = origin;
            Direction = direction;
        }
    }

    /// <summary>Hasar için aday: kimlik + o tick'teki kapsül örneği.</summary>
    public readonly struct HitCandidate
    {
        public readonly int Id;
        public readonly PositionHistory.Sample Sample;

        public HitCandidate(int id, PositionHistory.Sample sample)
        {
            Id = id;
            Sample = sample;
        }
    }

    /// <summary>
    /// Sunucu hitscan doğrulaması (saf mantık): atış sıklığı / mermi / geometri kontrolü, istemci origin'inin
    /// sunucu göz noktasına sıkıştırılması, geri sarma tick'inin 1 sn ile sınırlanması, geri sarılmış kapsüllere
    /// karşı en yakın isabet seçimi ve dünya engeli (duvar) mesafesiyle kırpma.
    /// </summary>
    public static class HitscanRules
    {
        public const float MaxRange = 400f;
        public const float MaxOriginOffset = 2.5f;
        public const float BodyDamage = 28f;
        public const float HeadDamage = 70f;
        /// <summary>İzin verilen en küçük atış aralığı = 1/hz × bu oran (ağ jitter payı).</summary>
        public const float FireIntervalSlack = 0.9f;

        /// <summary>İstemcinin bildirdiği tick'i [serverTick - maxRewind, serverTick] aralığına çeker. 0 = bildirilmedi → şimdi.</summary>
        public static uint ClampRewindTick(uint requested, uint serverTick, uint maxRewindTicks, out bool clamped)
        {
            clamped = false;
            if (requested == 0u)
                return serverTick;
            if (requested > serverTick)
            {
                clamped = true;
                return serverTick;
            }

            var oldest = serverTick > maxRewindTicks ? serverTick - maxRewindTicks : 0u;
            if (requested < oldest)
            {
                clamped = true;
                return oldest;
            }

            return requested;
        }

        public static ShotCheck Evaluate(
            float now, float lastShotTime, float maxFireRateHz, int ammoInMag,
            Vector3 clientOrigin, Vector3 clientDirection, Vector3 serverEyeOrigin,
            uint requestedTick, uint serverTick, uint maxRewindTicks)
        {
            var none = new ShotCheck(ShotReject.BadNumbers, serverTick, 0u, false, serverEyeOrigin, Vector3.forward);
            if (!IsFinite(clientOrigin) || !IsFinite(clientDirection) || !IsFinite(serverEyeOrigin))
                return none;

            var minInterval = 1f / Mathf.Max(1f, maxFireRateHz);
            if (now - lastShotTime < minInterval * FireIntervalSlack)
                return new ShotCheck(ShotReject.FireRate, serverTick, 0u, false, serverEyeOrigin, Vector3.forward);

            if (ammoInMag <= 0)
                return new ShotCheck(ShotReject.NoAmmo, serverTick, 0u, false, serverEyeOrigin, Vector3.forward);

            if (clientDirection.sqrMagnitude < 1e-6f)
                return new ShotCheck(ShotReject.BadDirection, serverTick, 0u, false, serverEyeOrigin, Vector3.forward);

            var origin = (clientOrigin - serverEyeOrigin).sqrMagnitude > MaxOriginOffset * MaxOriginOffset
                ? serverEyeOrigin
                : clientOrigin;

            var tick = ClampRewindTick(requestedTick, serverTick, maxRewindTicks, out var clamped);
            return new ShotCheck(ShotReject.None, tick, serverTick - tick, clamped, origin, clientDirection.normalized);
        }

        /// <summary>
        /// Geri sarılmış adaylar arasından en yakın isabeti seçer. worldBlockDistance (duvar/arazi çarpma mesafesi,
        /// yoksa +∞) isabetten yakınsa atış engellenmiştir.
        /// </summary>
        public static bool PickVictim(
            IReadOnlyList<HitCandidate> candidates, Vector3 origin, Vector3 direction, float worldBlockDistance,
            out int victimId, out float distance, out bool headshot)
        {
            victimId = -1;
            distance = float.MaxValue;
            headshot = false;
            var limit = Mathf.Min(MaxRange, worldBlockDistance);
            for (var i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];
                var s = c.Sample;
                if (!CapsuleRay.Raycast(s.Position, s.Rotation, s.Height, s.Radius, origin, direction, limit, out var d, out var hs))
                    continue;
                if (d < distance)
                {
                    distance = d;
                    victimId = c.Id;
                    headshot = hs;
                }
            }

            return victimId >= 0;
        }

        public static float DamageFor(bool headshot) => headshot ? HeadDamage : BodyDamage;

        private static bool IsFinite(Vector3 v) =>
            !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z)
              || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
    }

    /// <summary>Işın - kapsül kesişimi (saf). Kafa vuruşu: yerel yukarı eksende yüksekliğin %22'sinin üstü.</summary>
    public static class CapsuleRay
    {
        public static bool Raycast(
            Vector3 position, Quaternion rotation, float height, float radius,
            Vector3 origin, Vector3 direction, float maxDistance, out float distance, out bool headshot)
        {
            distance = 0f;
            headshot = false;
            var up = rotation * Vector3.up;
            var half = Mathf.Max(0f, height * 0.5f - radius);
            var p0 = position + up * half;
            var p1 = position - up * half;

            if (!RaycastCapsule(origin, direction, maxDistance, p0, p1, radius, out distance))
                return false;

            var hitPoint = origin + direction * distance;
            headshot = Vector3.Dot(hitPoint - position, up) > height * 0.22f;
            return true;
        }

        private static bool RaycastCapsule(Vector3 origin, Vector3 dir, float maxDist, Vector3 a, Vector3 b, float radius, out float distance)
        {
            distance = maxDist;
            var hit = false;

            if (RaycastSphere(origin, dir, maxDist, a, radius, out var d0) && d0 < distance)
            {
                distance = d0;
                hit = true;
            }

            if (RaycastSphere(origin, dir, maxDist, b, radius, out var d1) && d1 < distance)
            {
                distance = d1;
                hit = true;
            }

            var ab = b - a;
            var abLenSq = ab.sqrMagnitude;
            if (abLenSq > 1e-6f)
            {
                var n = ab / Mathf.Sqrt(abLenSq);
                var w = origin - a;
                var d = Vector3.Dot(w, n);
                var e = Vector3.Dot(dir, n);
                var m = w - n * d;
                var u = dir - n * e;
                var aQuad = Vector3.Dot(u, u);
                var bQuad = 2f * Vector3.Dot(m, u);
                var cQuad = Vector3.Dot(m, m) - radius * radius;
                if (Mathf.Abs(aQuad) > 1e-8f)
                {
                    var disc = bQuad * bQuad - 4f * aQuad * cQuad;
                    if (disc >= 0f)
                    {
                        var t = (-bQuad - Mathf.Sqrt(disc)) / (2f * aQuad);
                        if (t >= 0f && t <= maxDist)
                        {
                            var y = d + t * e;
                            if (y >= 0f && y * y <= abLenSq && t < distance)
                            {
                                distance = t;
                                hit = true;
                            }
                        }
                    }
                }
            }

            return hit;
        }

        private static bool RaycastSphere(Vector3 origin, Vector3 dir, float maxDist, Vector3 center, float radius, out float t)
        {
            t = 0f;
            var oc = origin - center;
            var b = Vector3.Dot(oc, dir);
            var c = Vector3.Dot(oc, oc) - radius * radius;
            var disc = b * b - c;
            if (disc < 0f)
                return false;
            t = -b - Mathf.Sqrt(disc);
            return t >= 0f && t <= maxDist;
        }
    }
}
