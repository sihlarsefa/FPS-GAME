using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.Infrastructure.Combat
{
    /// <summary>
    /// Görüşü kesen sis küreleri (sis bombası). Görsel bulut GameVfx.SmokeCloud ile ayrıca çizilir; bu sınıf yalnızca
    /// görüş hattı (AI algısı, nişan) için geometrik testi yapar. Bulut ilk saniyelerde genişler, son saniyelerde dağılır.
    /// Süresi dolan küreler sorgularda tembelce temizlenir; tahsis yapmaz.
    /// </summary>
    public static class SmokeVolume
    {
        /// <summary>Bulutun tam boyuta ulaşma süresi (sn).</summary>
        public const float GrowSeconds = 2.5f;

        /// <summary>Bulutun dağılma süresi (sn, ömrün sonunda).</summary>
        public const float FadeSeconds = 4f;

        /// <summary>Görüşü kesen yoğun çekirdek, yarıçapın bu oranıdır (kenarlar yarı saydam).</summary>
        public const float DenseCoreFraction = 0.85f;

        private struct SmokeSphere
        {
            public Vector3 Center;
            public float Radius;
            public float StartTime;
            public float EndTime;
        }

        private static readonly List<SmokeSphere> Spheres = new(16);

        /// <summary>Etkin sis bulutu sayısı.</summary>
        public static int ActiveCount
        {
            get
            {
                Prune(Time.time);
                return Spheres.Count;
            }
        }

        public static void Spawn(Vector3 center, float radius, float duration)
        {
            if (radius <= 0f || duration <= 0f)
                return;

            var now = Time.time;
            Prune(now);
            Spheres.Add(new SmokeSphere
            {
                Center = center,
                Radius = radius,
                StartTime = now,
                EndTime = now + duration
            });
        }

        /// <summary>İki nokta arasındaki görüş hattı bir sis bulutunun yoğun çekirdeğinden geçiyor mu?</summary>
        public static bool BlocksLineOfSight(Vector3 from, Vector3 to)
        {
            if (Spheres.Count == 0)
                return false;

            var now = Time.time;
            Prune(now);

            var segment = to - from;
            var lengthSqr = segment.sqrMagnitude;
            for (var i = 0; i < Spheres.Count; i++)
            {
                var s = Spheres[i];
                var r = EffectiveRadius(s, now) * DenseCoreFraction;
                if (r <= 0.05f)
                    continue;

                if (SegmentDistanceSqr(from, segment, lengthSqr, s.Center) <= r * r)
                    return true;
            }

            return false;
        }

        /// <summary>Nokta bir sis bulutunun içinde mi (ör. AI'nın "sisin içindeyim" kontrolü)?</summary>
        public static bool Contains(Vector3 point)
        {
            if (Spheres.Count == 0)
                return false;

            var now = Time.time;
            for (var i = 0; i < Spheres.Count; i++)
            {
                var s = Spheres[i];
                if (now >= s.EndTime)
                    continue;

                var r = EffectiveRadius(s, now);
                if ((point - s.Center).sqrMagnitude <= r * r)
                    return true;
            }

            return false;
        }

        public static void Clear()
        {
            Spheres.Clear();
        }

        private static float EffectiveRadius(SmokeSphere s, float now)
        {
            if (now >= s.EndTime)
                return 0f;

            var age = now - s.StartTime;
            var grow = Mathf.Clamp01(age / GrowSeconds);
            grow = 1f - (1f - grow) * (1f - grow); // ease-out
            var remaining = s.EndTime - now;
            var fade = Mathf.Clamp01(remaining / FadeSeconds);
            return s.Radius * Mathf.Max(0.25f, grow) * fade;
        }

        private static float SegmentDistanceSqr(Vector3 from, Vector3 segment, float lengthSqr, Vector3 point)
        {
            if (lengthSqr < 1e-8f)
                return (point - from).sqrMagnitude;

            var t = Vector3.Dot(point - from, segment) / lengthSqr;
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            var closest = from + segment * t;
            return (point - closest).sqrMagnitude;
        }

        private static void Prune(float now)
        {
            for (var i = Spheres.Count - 1; i >= 0; i--)
            {
                if (now >= Spheres[i].EndTime)
                    Spheres.RemoveAt(i);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Spheres.Clear();
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private static void OnActiveSceneChanged(Scene previous, Scene next) => Spheres.Clear();
    }
}
