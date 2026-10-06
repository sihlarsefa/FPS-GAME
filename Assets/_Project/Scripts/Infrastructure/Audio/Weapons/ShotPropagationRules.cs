using System;

namespace Project.Infrastructure.Audio.Weapons
{
    /// <summary>Sesüstü merminin bir dinleyici için çatlama/gürleme zamanlaması.</summary>
    public readonly struct CrackTiming
    {
        public readonly bool Audible;
        public readonly float CrackSeconds;
        public readonly float ThumpSeconds;
        public readonly float GapSeconds;
        public readonly float BearingErrorDeg;

        public CrackTiming(bool audible, float crack, float thump, float bearingErr)
        {
            Audible = audible;
            CrackSeconds = crack;
            ThumpSeconds = thump;
            GapSeconds = thump - crack;
            BearingErrorDeg = bearingErr;
        }
    }

    /// <summary>
    /// Uzak atış yayılımı (saf): ses hızı gecikmesi, mesafe/hava soğurması, duyulma menzili, balistik çatlama ile
    /// namlu gürlemesi arasındaki fark ve çatlamanın yön yanılgısı. Squad/Arma tarzı gerçek gecikme: 343 m/s,
    /// 700 m'de ~2 sn sonra gürleme gelir; mermi (900 m/s) çatlamayı çok önce yaptırır.
    /// </summary>
    public static class ShotPropagationRules
    {
        public const float ReferenceSpeedOfSound = 343f;
        public const float MaxGameDelaySeconds = 3.5f;
        public const float DefaultAbsorptionDbPerMeter = 0.012f;
        public const float DefaultMaxRange = 700f;
        /// <summary>Oyun dünyası ölçeği: ortam gürültüsü üstünde gerekli algılama payı (dB).</summary>
        public const float DefaultDetectMarginDb = 50f;

        public static float SpeedOfSound(float tempCelsius) => 331.3f + 0.606f * tempCelsius;

        /// <summary>Gecikme (sn): mesafe / ses hızı; oyun için üst sınır.</summary>
        public static float ArrivalDelay(float meters, float tempCelsius = 20f)
        {
            if (!(meters > 0f)) return 0f;
            var t = meters / SpeedOfSound(tempCelsius);
            return t > MaxGameDelaySeconds ? MaxGameDelaySeconds : t;
        }

        /// <summary>Gecikme planlama eşiği: 25 m altında (~73 ms) fark edilmez, atlanır.</summary>
        public static bool DelayWorthScheduling(float meters) => meters >= 25f;

        /// <summary>Dinleyicideki seviye: kaynak - 20log10(d) - soğurma*d (d 1 m'den küçükse 1 m).</summary>
        public static float LevelAtDistance(float sourceDb, float meters, float absorptionDbPerMeter = DefaultAbsorptionDbPerMeter)
        {
            var d = meters < 1f ? 1f : meters;
            return sourceDb - 20f * (float)Math.Log10(d) - absorptionDbPerMeter * d;
        }

        /// <summary>
        /// Duyulma menzili (m): seviye ortam+pay eşiğine düştüğü mesafe (ikili arama), en çok maxRange.
        /// Susturucu menzili kısalttığı hâlde hiçbir zaman sıfırlamaz.
        /// </summary>
        public static float AudibleRange(float sourceDb, float ambientDb, float marginDb = DefaultDetectMarginDb,
            float absorptionDbPerMeter = DefaultAbsorptionDbPerMeter, float maxRange = DefaultMaxRange)
        {
            var floor = ambientDb + marginDb;
            if (LevelAtDistance(sourceDb, 1f, absorptionDbPerMeter) < floor) return 0f;
            if (LevelAtDistance(sourceDb, maxRange, absorptionDbPerMeter) >= floor) return maxRange;
            float lo = 1f, hi = maxRange;
            for (var i = 0; i < 40; i++)
            {
                var mid = 0.5f * (lo + hi);
                if (LevelAtDistance(sourceDb, mid, absorptionDbPerMeter) >= floor) lo = mid; else hi = mid;
            }
            return lo;
        }

        /// <summary>Rüzgar: esen yönde (+) menzil artar, karşı rüzgarda azalır; ±%25 sınırı.</summary>
        public static float WindRangeScale(float windAlongSoundMps)
        {
            var s = 1f + windAlongSoundMps * 0.025f;
            return s < 0.75f ? 0.75f : s > 1.25f ? 1.25f : s;
        }

        /// <summary>Yağmur/ortam gürültüsü artışı: duyulma payını ölçekler (ambient dB doğrudan verilir).</summary>
        public static float AmbientFromWeather(float baseDb, float rain01, float windMps)
        {
            var r = rain01 < 0f ? 0f : rain01 > 1f ? 1f : rain01;
            return baseDb + 14f * r + 0.9f * (windMps < 0f ? 0f : windMps);
        }

        /// <summary>Mermi hızı (m/s) menzilde: v = v0 * exp(-k r). k ~ 0.0005 (tüfek), 0.002 (tabanca).</summary>
        public static float BulletSpeedAt(float v0, float range, float dragK)
            => v0 * (float)Math.Exp(-dragK * (range < 0f ? 0f : range));

        public static float DefaultDragK(ShotClass c)
        {
            switch (c)
            {
                case ShotClass.Pistol: return 0.0022f;
                case ShotClass.Smg: return 0.0020f;
                case ShotClass.Shotgun: return 0.0060f;
                case ShotClass.Sniper: return 0.00035f;
                default: return 0.00050f;
            }
        }

        /// <summary>Uçuş süresi (sn): integral exp(-k r) -> (e^(k r) - 1) / (k v0).</summary>
        public static float TimeOfFlight(float v0, float range, float dragK)
        {
            if (!(range > 0f) || !(v0 > 0f)) return 0f;
            if (dragK < 1e-6f) return range / v0;
            return ((float)Math.Exp(dragK * range) - 1f) / (dragK * v0);
        }

        /// <summary>Mach yarı açısı (derece): asin(c/v); sesaltıysa 90.</summary>
        public static float MachHalfAngleDeg(float bulletSpeed, float speedOfSound = ReferenceSpeedOfSound)
        {
            if (!(bulletSpeed > speedOfSound)) return 90f;
            return (float)(Math.Asin(speedOfSound / bulletSpeed) * 180.0 / Math.PI);
        }

        /// <summary>Çatlamanın duyulabileceği en büyük yanal uzaklık (m): hız arttıkça şok konisi genişler (12-45 m).</summary>
        public static float CrackMaxPerpendicular(float bulletSpeed, float speedOfSound = ReferenceSpeedOfSound)
        {
            var mach = bulletSpeed / speedOfSound;
            var t = (mach - 1.1f) / 1.5f;
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            return 12f + 33f * t;
        }

        /// <summary>
        /// Çatlama ve gürleme zamanlaması. alongRange: dinleyicinin mermi yolu üzerindeki izdüşümü (m, namludan),
        /// perpendicular: yoldan yanal uzaklık (m). Çatlama: uçuş süresi + yanal/c; gürleme: namlu mesafesi / c.
        /// </summary>
        public static CrackTiming Crack(ShotClass c, float muzzleVelocity, float alongRange, float perpendicular, float tempCelsius = 20f)
        {
            var sos = SpeedOfSound(tempCelsius);
            var k = DefaultDragK(c);
            var along = alongRange < 0f ? 0f : alongRange;
            var perp = perpendicular < 0f ? 0f : perpendicular;
            var vHere = BulletSpeedAt(muzzleVelocity, along, k);
            var distance = (float)Math.Sqrt(along * along + perp * perp);
            var thump = distance / sos;
            var crackT = TimeOfFlight(muzzleVelocity, along, k) + perp / sos;
            var audible = vHere >= sos * SuppressorRules.SupersonicMargin && perp <= CrackMaxPerpendicular(vHere, sos);
            var err = audible ? CrackBearingErrorDeg(along, perp, vHere, sos) : 0f;
            return new CrackTiming(audible, crackT, thump, err);
        }

        /// <summary>
        /// Çatlama yön yanılgısı (derece): çatlama şok konisinden gelir, dinleyici yola dik yönden namluya doğru
        /// Mach açısı kadar sapan bir yön algılar; gerçek namlu yönü atan2(r, p). İkisinin farkı.
        /// 400 m'de 5 m yanından geçen tüfek mermisi için ~65 derece: oyuncu çatlamayla atıcıyı bulamaz, gürleme bulur.
        /// </summary>
        public static float CrackBearingErrorDeg(float alongRange, float perpendicular, float bulletSpeed, float speedOfSound = ReferenceSpeedOfSound)
        {
            var p = perpendicular < 0.5f ? 0.5f : perpendicular;
            var trueDeg = (float)(Math.Atan2(alongRange, p) * 180.0 / Math.PI);
            var perceived = MachHalfAngleDeg(bulletSpeed, speedOfSound);
            return Math.Abs(trueDeg - perceived);
        }

        /// <summary>Yön belirsizliği olarak ses kaynağı yayılımı (derece): çatlama için hata kadar, gürleme için mesafeyle 3..25.</summary>
        public static float LocalizationSpreadDeg(float distance, bool isCrack, float bearingErrorDeg)
        {
            if (isCrack) return bearingErrorDeg < 8f ? 8f : bearingErrorDeg > 90f ? 90f : bearingErrorDeg;
            var t = distance / 400f;
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            return 3f + 22f * t;
        }

        /// <summary>Uzak atış kuyruk zamanlaması için slapback/yankı gecikmesi: yansıtıcıya gidiş-dönüş (sn).</summary>
        public static float EchoDelay(float reflectorDistance, float tempCelsius = 20f)
            => 2f * (reflectorDistance < 0f ? 0f : reflectorDistance) / SpeedOfSound(tempCelsius);

        /// <summary>İnsan kulağı gecikmeyi >~50 ms ve >~25 m yansıtıcıda ayrı yankı olarak ayırır.</summary>
        public static bool EchoPerceivedSeparate(float reflectorDistance) => EchoDelay(reflectorDistance) >= 0.05f;
    }
}
