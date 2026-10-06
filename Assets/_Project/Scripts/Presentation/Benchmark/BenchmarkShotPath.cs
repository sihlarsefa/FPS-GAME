using System.Collections.Generic;
using UnityEngine;

namespace Project.Presentation.Benchmark
{
    /// <summary>Vitrin modundaki tek bir kamera planı.</summary>
    public readonly struct BenchmarkShot
    {
        public readonly string Name;
        public readonly float Duration;
        public readonly Vector3 PosFrom, PosTo, LookFrom, LookTo;
        public readonly float Fov;

        /// <summary>true: oyuncunun kendi kamerası kullanılır (silah modeli görünsün); konum/bakış yine bu yoldan okunur.</summary>
        public readonly bool PlayerCamera;

        public readonly bool Fire;
        public readonly bool Reload;

        public BenchmarkShot(string name, float duration, Vector3 posFrom, Vector3 posTo, Vector3 lookFrom, Vector3 lookTo,
            float fov, bool playerCamera, bool fire, bool reload)
        {
            Name = name;
            Duration = Mathf.Max(0.1f, duration);
            PosFrom = posFrom;
            PosTo = posTo;
            LookFrom = lookFrom;
            LookTo = lookTo;
            Fov = fov;
            PlayerCamera = playerCamera;
            Fire = fire;
            Reload = reload;
        }
    }

    /// <summary>Çıkarılan kamera pozu.</summary>
    public struct BenchmarkPose
    {
        public int ShotIndex;
        public float ShotTime01;
        public Vector3 Position;
        public Vector3 LookAt;
        public float Fov;
        public bool PlayerCamera;
        public bool Fire;
        public bool Reload;
    }

    /// <summary>Vitrin yolu: 6 plan, saf matematik (sahne nesnesi yok).</summary>
    public static class BenchmarkShotPath
    {
        public const int ShotCount = 6;

        /// <summary>Planlar için çapa noktaları (dünya, zemin Y dahil).</summary>
        public struct Anchors
        {
            public Vector3 Player;        // oyuncu doğuş noktası (gözün hemen altı)
            public Vector3 PlayerForward; // yatay bakış
            public Vector3 Soldier;
            public Vector3 Vehicle;
            public Vector3 HouseDoor;     // kapı önü
            public Vector3 HouseInside;   // iç mekân merkezi
            public Vector3 Forest;
            public Vector3 Targets;       // çelik hedef grubunun merkezi

            // Özneye bakan yönler (yatay); sıfırsa makul bir varsayılan kullanılır. Vitrin her planın başında canlı özneden doldurur.
            public Vector3 SoldierForward;  // askerin baktığı yön
            public Vector3 VehicleForward;  // Kirpi'nin ön yönü
            public Vector3 HouseOutward;    // evin içinden kapıya (dışarı) yön
            public Vector3 ForestAlong;     // orman kenarından içine doğru patika yönü

            // Vitrin askeri (planlar 1 ve 6, omuz üstü kamera). HasShooter false ise eski oyuncu-kamerası planları kullanılır.
            public bool HasShooter;
            public Vector3 Shooter;         // askerin zemin konumu
            public Vector3 ShooterForward;  // hedeflere bakan yatay yön
        }

        /// <summary>Yatay, normalize yön; sıfıra yakınsa verilen yedek döner.</summary>
        public static Vector3 FlatDir(Vector3 v, Vector3 fallback)
        {
            v.y = 0f;
            if (v.sqrMagnitude < 1e-4f)
            {
                fallback.y = 0f;
                return fallback.sqrMagnitude < 1e-4f ? Vector3.forward : fallback.normalized;
            }

            return v.normalized;
        }

        /// <summary>Oyuncunun doğuş noktasından hedef grubuna bakış yönü (derece, Unity yaw).</summary>
        public static float YawToward(Vector3 from, Vector3 to, float fallbackYaw)
        {
            var d = to - from;
            d.y = 0f;
            return d.sqrMagnitude < 1e-4f ? fallbackYaw : Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }

        public static List<BenchmarkShot> Build(Anchors a)
        {
            var eye = Vector3.up * 1.65f;
            var fwd = FlatDir(a.PlayerForward, Vector3.forward);
            var right = Vector3.Cross(Vector3.up, fwd);
            var shots = new List<BenchmarkShot>(ShotCount);

            var shooterFwd = FlatDir(a.ShooterForward, fwd);
            var shooterRight = Vector3.Cross(Vector3.up, shooterFwd);
            var shoulder = a.Shooter + Vector3.up * 1.45f;

            // 1) Silah yakın plan + şarjör değiştirme (oyuncu kamerası, ADS benzeri; yön Vitrin'de hedeflere çevrilir).
            if (a.HasShooter)
            {
                // Omuz üstü: asker karenin solunda, silah + hedefler karşıda; yavaş yaklaşma.
                var closeA = a.Shooter + shooterFwd * -2.1f + shooterRight * 0.75f + Vector3.up * 1.75f;
                var closeB = a.Shooter + shooterFwd * -1.5f + shooterRight * 0.7f + Vector3.up * 1.7f;
                var lookA = shoulder + shooterFwd * 6f + shooterRight * -0.35f;
                shots.Add(new BenchmarkShot("Silah yakın plan", 6f, closeA, closeB, lookA, lookA + Vector3.up * 0.05f,
                    50f, false, false, true));
            }
            else
            {
                shots.Add(new BenchmarkShot("Silah yakın plan", 6f,
                    a.Player + eye, a.Player + eye + right * 0.05f,
                    a.Player + eye + fwd * 6f - Vector3.up * 0.4f, a.Player + eye + fwd * 6f - Vector3.up * 0.55f,
                    55f, true, false, true));
            }

            // 2) Asker: 3/4 görünüm, 2.2 m, göz yüksekliği 1.5 m, özne üçler kuralıyla sol üçte birde. Yavaş yaklaşma.
            var sf = FlatDir(a.SoldierForward, -fwd);
            var chest = a.Soldier + Vector3.up * 1.25f;
            var dirA = Quaternion.AngleAxis(35f, Vector3.up) * sf;
            var dirB = Quaternion.AngleAxis(48f, Vector3.up) * sf;
            var camA = a.Soldier + dirA * 2.4f + Vector3.up * 1.5f;
            var camB = a.Soldier + dirB * 2.0f + Vector3.up * 1.5f;
            // Kameranın sağına kaydırılmış bakış noktası: özne karenin solunda kalır.
            var rA = Vector3.Cross(Vector3.up, (chest - camA).normalized) * 0.45f;
            var rB = Vector3.Cross(Vector3.up, (chest - camB).normalized) * 0.45f;
            shots.Add(new BenchmarkShot("Asker", 6.5f, camA, camB, chest + rA, chest + rB, 40f, false, false, false));

            // 3) Kirpi: alçak ön-çeyrek, 6 m, ~15° yukarı bakış.
            var vf = FlatDir(a.VehicleForward, right);
            var vc = a.Vehicle + Vector3.up * 1.2f;
            var vA = Quaternion.AngleAxis(38f, Vector3.up) * vf;
            var vB = Quaternion.AngleAxis(26f, Vector3.up) * vf;
            var vCamA = a.Vehicle + vA * 6.4f - Vector3.up * 0.1f;
            var vCamB = a.Vehicle + vB * 5.6f - Vector3.up * 0.1f;
            shots.Add(new BenchmarkShot("Kirpi", 6.5f, vCamA, vCamB, vc, vc + Vector3.up * 0.1f, 46f, false, false, false));

            // 4) Ev içi: kapı eşiğinden içeri bakış (pencere ışığı karşı duvarda), yavaş ilerleme.
            var out_ = FlatDir(a.HouseOutward, FlatDir(a.HouseDoor - a.HouseInside, fwd));
            var side = Vector3.Cross(Vector3.up, out_);
            var inside = a.HouseInside + Vector3.up * 1.4f;
            var door = a.HouseInside + out_ * 4.3f;
            door.y = a.HouseInside.y + 1.55f;
            var doorIn = a.HouseInside + out_ * 3.0f;
            doorIn.y = door.y;
            shots.Add(new BenchmarkShot("Ev içi", 7f,
                door, doorIn, inside - out_ * 1.5f + side * 0.6f, inside - out_ * 1.5f - side * 0.4f, 68f, false, false, false));

            // 5) Orman: ağaç hattının içinden patika boyunca bakış, göz hizası, yavaş ilerleme.
            var fa = FlatDir(a.ForestAlong, fwd);
            var fs = Vector3.Cross(Vector3.up, fa);
            var f = a.Forest + Vector3.up * 1.6f;
            shots.Add(new BenchmarkShot("Orman", 7f,
                f - fa * 3f + fs * 0.8f, f + fa * 3f + fs * 0.8f,
                f + fa * 14f + Vector3.up * 1.0f, f + fa * 14f + Vector3.up * 1.4f - fs * 0.5f, 58f, false, false, false));

            // 6) Hedeflere ateş (oyuncu kamerası, otomatik ateş; yön Vitrin'de hedeflere çevrilir).
            var t = a.Targets + Vector3.up * 1.1f;
            if (a.HasShooter)
            {
                var fireA = a.Shooter + shooterFwd * -2.6f + shooterRight * 0.9f + Vector3.up * 1.8f;
                var fireB = a.Shooter + shooterFwd * -2.2f + shooterRight * 0.8f + Vector3.up * 1.75f;
                shots.Add(new BenchmarkShot("Hedeflere ateş", 9f, fireA, fireB,
                    t + shooterRight * -1.0f, t + shooterRight * 1.0f, 52f, false, true, false));
            }
            else
            {
                shots.Add(new BenchmarkShot("Hedeflere ateş", 9f,
                    a.Player + eye, a.Player + eye,
                    t + right * -1.5f, t + right * 1.5f, 60f, true, true, false));
            }

            return shots;
        }

        public static float TotalDuration(IReadOnlyList<BenchmarkShot> shots)
        {
            var sum = 0f;
            if (shots != null)
                for (var i = 0; i < shots.Count; i++)
                    sum += shots[i].Duration;
            return sum;
        }

        public static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Toplam süre içindeki zamana karşılık gelen poz; zaman toplamı aşarsa başa sarar.</summary>
        public static BenchmarkPose Evaluate(IReadOnlyList<BenchmarkShot> shots, float time)
        {
            var pose = new BenchmarkPose();
            if (shots == null || shots.Count == 0)
                return pose;

            var total = TotalDuration(shots);
            if (total <= 0f)
                return pose;

            var t = time % total;
            if (t < 0f)
                t += total;

            for (var i = 0; i < shots.Count; i++)
            {
                var shot = shots[i];
                if (t < shot.Duration || i == shots.Count - 1)
                {
                    var u = Mathf.Clamp01(t / shot.Duration);
                    var e = Ease(u);
                    pose.ShotIndex = i;
                    pose.ShotTime01 = u;
                    pose.Position = Vector3.Lerp(shot.PosFrom, shot.PosTo, e);
                    pose.LookAt = Vector3.Lerp(shot.LookFrom, shot.LookTo, e);
                    pose.Fov = shot.Fov;
                    pose.PlayerCamera = shot.PlayerCamera;
                    pose.Fire = shot.Fire;
                    pose.Reload = shot.Reload;
                    return pose;
                }

                t -= shot.Duration;
            }

            return pose;
        }

        /// <summary>Ateş darbesi: plan içinde saniyede ~2 kısa seri (açık/kapalı).</summary>
        public static bool BurstOn(float shotSeconds)
        {
            var phase = shotSeconds % 1.2f;
            return phase < 0.7f;
        }

        /// <summary>Ekran görüntüsü dosya adı (zaman damgalı, güvenli karakterler).</summary>
        public static string ScreenshotName(System.DateTime utc, int counter)
        {
            return "aaa_" + utc.ToString("yyyyMMdd_HHmmss") + "_" + counter.ToString("000") + ".png";
        }
    }
}
