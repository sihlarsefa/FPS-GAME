using UnityEngine;

namespace Project.Infrastructure.Audio
{
    /// <summary>Balistik akustik için çaplı sınıfı.</summary>
    public enum CaliberClass { Pistol = 0, Rifle = 1, MachineGun = 2, Sniper = 3, Explosion = 4 }

    /// <summary>Çap sınıfına göre sabit akustik profil.</summary>
    public readonly struct CaliberProfile
    {
        public readonly float Loudness;       // 0..1.5 başlangıç seviyesi
        public readonly float RefDistance;    // m: seviyenin düşmeye başladığı mesafe
        public readonly float AudibleRange;   // m: bu mesafede tamamen duyulmaz
        public readonly float HalfCutoffDist; // m: alçak geçiren kesim frekansının yarıya indiği mesafe
        public readonly float MinCutoffHz;
        public readonly float MuzzleVelocity; // m/s
        public CaliberProfile(float loud, float refD, float range, float halfD, float minHz, float v)
        { Loudness = loud; RefDistance = refD; AudibleRange = range; HalfCutoffDist = halfD; MinCutoffHz = minHz; MuzzleVelocity = v; }
        public bool Supersonic => MuzzleVelocity > AcousticsMath.SpeedOfSound;
    }

    /// <summary>Planlanmış tek bir yansıma (vadi/bina yankısı).</summary>
    public struct EchoPlan
    {
        public int RayIndex;
        public float Delay;   // sn: doğrudan sesten sonra
        public float Gain;
        public float CutoffHz;
    }

    /// <summary>
    /// Balistik akustik saf matematiği (Unity nesnesi yok): ses hızı gecikmesi, hava sönümü (seviye + alçak geçiren),
    /// mermi yolunun dinleyiciye en yakın noktası, yakın geçiş crack/whiz kazancı, yansıma planı, kapalı alan slapback.
    /// </summary>
    public static class AcousticsMath
    {
        public const float SpeedOfSound = 343f;
        public const float OpenCutoffHz = 22000f;
        public const float NearMissRadius = 6f;
        /// <summary>Bu mesafeye kadar hava soğurması uygulanmaz (m).</summary>
        public const float NearOpenDistance = 30f;
        public const float EchoMinDistance = 40f;
        public const float EchoMaxDistance = 400f;
        public const float MaxEchoDelay = 2.5f;
        public const float MinEchoDelay = 0.08f;

        public static CaliberProfile Profile(CaliberClass c)
        {
            switch (c)
            {
                case CaliberClass.Pistol: return new CaliberProfile(0.7f, 12f, 350f, 25f, 700f, 340f);
                case CaliberClass.MachineGun: return new CaliberProfile(1.1f, 25f, 900f, 70f, 500f, 850f);
                case CaliberClass.Sniper: return new CaliberProfile(1.3f, 35f, 1400f, 95f, 450f, 900f);
                case CaliberClass.Explosion: return new CaliberProfile(1.5f, 50f, 1800f, 150f, 300f, 0f);
                default: return new CaliberProfile(1.0f, 20f, 800f, 60f, 550f, 880f);
            }
        }

        /// <summary>Ses hızı gecikmesi (sn).</summary>
        public static float Delay(float distance) => distance > 0f ? distance / SpeedOfSound : 0f;

        /// <summary>Merminin dinleyiciye en yakın noktaya varış süresi (sn); mermi hızı 0/negatifse 0.</summary>
        public static float BulletTravelDelay(float alongDistance, float muzzleVelocity) =>
            muzzleVelocity > 1f && alongDistance > 0f ? alongDistance / muzzleVelocity : 0f;

        /// <summary>Susturucu menzili ve gürültüsünü düşürür.</summary>
        public static float EffectiveRange(CaliberClass c, bool suppressed) =>
            Profile(c).AudibleRange * (suppressed ? 0.35f : 1f);

        /// <summary>Mesafeye göre seviye (0..~1.5): referans mesafeden sonra 1/d, menzil sonunda yumuşak sönme.</summary>
        public static float DistanceGain(CaliberClass c, float distance, bool suppressed = false)
        {
            var p = Profile(c);
            var range = EffectiveRange(c, suppressed);
            if (distance >= range)
                return 0f;
            var refD = suppressed ? p.RefDistance * 0.6f : p.RefDistance;
            var g = p.Loudness * (suppressed ? 0.3f : 1f) * (refD / Mathf.Max(distance, refD));
            var fadeStart = range * 0.7f;
            if (distance > fadeStart)
            {
                var t = Mathf.Clamp01((distance - fadeStart) / (range - fadeStart));
                g *= 1f - t * t * (3f - 2f * t);
            }
            return g;
        }

        /// <summary>Hava sönümü: mesafeyle azalan alçak geçiren kesim frekansı (Hz).</summary>
        public static float CutoffHz(CaliberClass c, float distance, bool suppressed = false)
        {
            var p = Profile(c);
            var half = suppressed ? p.HalfCutoffDist * 0.7f : p.HalfCutoffDist;
            // 30 m içi (kendi atışlarımız dahil) tam parlaklık: süzgeç yok. Sonrası yumuşak hava sönümü.
            if (distance <= NearOpenDistance)
                return OpenCutoffHz;
            var hz = OpenCutoffHz / (1f + (distance - NearOpenDistance) / Mathf.Max(1f, half * 1.5f));
            return Mathf.Clamp(hz, p.MinCutoffHz, OpenCutoffHz);
        }

        /// <summary>
        /// Işının (origin + dir*t, 0..maxDist) noktaya en yakın yaklaşması. along = en yakın noktanın ışık üzerindeki
        /// mesafesi, miss = kaçırma mesafesi. dir birim değilse normalize edilir; dir sıfırsa false.
        /// </summary>
        public static bool ClosestApproach(Vector3 origin, Vector3 dir, float maxDist, Vector3 point,
            out float along, out float miss, out Vector3 closestPoint)
        {
            along = 0f; miss = 0f; closestPoint = origin;
            var m = dir.sqrMagnitude;
            if (m < 1e-8f || maxDist <= 0f)
                return false;
            dir /= Mathf.Sqrt(m);
            along = Mathf.Clamp(Vector3.Dot(point - origin, dir), 0f, maxDist);
            closestPoint = origin + dir * along;
            miss = (point - closestPoint).magnitude;
            return true;
        }

        /// <summary>Süpersonik çatlama kazancı: yakın geçişte güçlü, 6 m'de 0.</summary>
        public static float CrackGain(float miss)
        {
            if (miss >= NearMissRadius) return 0f;
            var t = 1f - miss / NearMissRadius;
            return 0.35f + 0.65f * t;
        }

        /// <summary>Whiz (vınlama) kazancı; yaklaştıkça artar.</summary>
        public static float WhizGain(float miss)
        {
            if (miss >= NearMissRadius) return 0f;
            var t = 1f - miss / NearMissRadius;
            return 0.2f + 0.8f * t * t;
        }

        /// <summary>Yakın geçişte çatlama ve whiz çalınmalı mı (mermi hızına göre).</summary>
        public static void NearMissLayers(float miss, float muzzleVelocity, out bool crack, out bool whiz)
        {
            var near = miss < NearMissRadius;
            crack = near && muzzleVelocity > SpeedOfSound;
            whiz = near;
        }

        /// <summary>
        /// Yansıma planı. listenerHit[i] = dinleyiciden i. ışının çarptığı mesafe (&lt;=0: çarpma yok),
        /// shooterHit[i] = atıcıdan o çarpma noktasına mesafe. Yol = shooterHit + listenerHit; gecikme =
        /// (yol - doğrudan)/c. En fazla maxEchoes adet, gecikmeye göre sıralı, her biri öncekinden daha sönük.
        /// </summary>
        public static int PlanEchoes(float[] listenerHit, float[] shooterHit, int rayCount, float directDistance,
            CaliberClass c, bool suppressed, EchoPlan[] result, int maxEchoes)
        {
            if (listenerHit == null || shooterHit == null || result == null || maxEchoes <= 0)
                return 0;
            var n = 0;
            for (var i = 0; i < rayCount && i < listenerHit.Length && i < shooterHit.Length; i++)
            {
                var lh = listenerHit[i];
                if (lh < EchoMinDistance || lh > EchoMaxDistance)
                    continue;
                var path = lh + shooterHit[i];
                var delay = Delay(path - directDistance);
                if (delay < MinEchoDelay || delay > MaxEchoDelay)
                    continue;
                var gain = DistanceGain(c, path, suppressed) * 0.55f;
                if (gain < 0.01f)
                    continue;
                var e = new EchoPlan { RayIndex = i, Delay = delay, Gain = gain, CutoffHz = CutoffHz(c, path, suppressed) * 0.55f };
                // Ekleme sıralaması (gecikme artan).
                var j = n < result.Length ? n : result.Length - 1;
                if (n >= result.Length && e.Delay >= result[j].Delay)
                    continue;
                while (j > 0 && result[j - 1].Delay > e.Delay)
                {
                    result[j] = result[j - 1];
                    j--;
                }
                result[j] = e;
                if (n < result.Length) n++;
            }
            if (n > maxEchoes) n = maxEchoes;
            for (var k = 0; k < n; k++)
                result[k].Gain *= Mathf.Pow(0.72f, k);
            return n;
        }

        /// <summary>
        /// Kapalı alan puanı (0..1): tavan var ve duvarlar yakınsa yüksek. ceilingDist &lt;=0: tavan yok;
        /// wallHits: yatay ışınlardan kaç tanesi 12 m içinde çarptı (0..4).
        /// </summary>
        public static float IndoorScore(float ceilingDist, int wallHits)
        {
            if (ceilingDist <= 0f || ceilingDist > 8f)
                return 0f;
            var w = Mathf.Clamp01(wallHits / 3f);
            return Mathf.Clamp01(0.35f + 0.65f * w);
        }

        /// <summary>Kapalı alan slapback gecikmesi: en yakın duvara gidiş-dönüş (20..120 ms).</summary>
        public static float SlapbackDelay(float nearestWallDist) =>
            Mathf.Clamp(Delay(2f * Mathf.Max(0f, nearestWallDist)), 0.02f, 0.12f);

        /// <summary>Kapalı alan slapback seviyesi.</summary>
        public static float SlapbackGain(CaliberClass c, float indoorScore, float listenerDistance, bool suppressed)
        {
            if (indoorScore < 0.5f) return 0f;
            return DistanceGain(c, Mathf.Max(listenerDistance, 5f), suppressed) * 0.6f * indoorScore;
        }
    }
}
