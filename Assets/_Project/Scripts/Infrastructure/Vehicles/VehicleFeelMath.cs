using System;

namespace Project.Infrastructure.Vehicles
{
    /// <summary>Hasar kademesi (motor dumanı / yangın).</summary>
    public enum VehicleDamageState { Saglam, Hafif, Agir, Yangin }

    /// <summary>Araç hissi için saf matematik (Unity'siz): vites/devir, gövde yatışı, taret hız sınırı, hasar kademesi, pusula.</summary>
    public static class VehicleFeelMath
    {
        public const int GearCount = 5;
        public const float YawDegPerSec = 70f;
        public const float PitchDegPerSec = 40f;
        public const float MaxBodyPitch = 3.2f;
        public const float MaxBodyRoll = 4.5f;

        private static readonly string[] Cardinals = { "K", "KD", "D", "GD", "G", "GB", "B", "KB" };
        public static readonly string[] ZoneNames = { "MOTOR", "KABİN", "TEKER", "TARET" };

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        /// <summary>Hıza göre vites (1..GearCount); sınırlar hızın karesel dağılımıyla (alt vitesler kısa).</summary>
        public static int GearFor(float speedKmh, float maxSpeedKmh)
        {
            var f = Clamp01(Math.Abs(speedKmh) / Math.Max(1f, maxSpeedKmh));
            for (var g = 1; g < GearCount; g++)
                if (f < GearUpper(g))
                    return g;
            return GearCount;
        }

        /// <summary>Vitesin üst sınırı (0..1 hız oranı): sqrt eğrisi.</summary>
        public static float GearUpper(int gear)
        {
            var t = Clamp01(gear / (float)GearCount);
            return t * t * 0.95f + 0.05f * t;
        }

        /// <summary>Vites içi devir 0..1: vitese girişte ~0.35, üst sınırda 1.</summary>
        public static float Rpm01(float speedKmh, float maxSpeedKmh)
        {
            var g = GearFor(speedKmh, maxSpeedKmh);
            var f = Clamp01(Math.Abs(speedKmh) / Math.Max(1f, maxSpeedKmh));
            var lo = g == 1 ? 0f : GearUpper(g - 1);
            var hi = g == GearCount ? 1f : GearUpper(g);
            var t = Clamp01((f - lo) / Math.Max(0.001f, hi - lo));
            return 0.3f + 0.7f * t;
        }

        /// <summary>Motor sesi perde: devir + gaz; vites değişiminde kısa düşüş (shiftDip 0..1).</summary>
        public static float EnginePitch(float rpm01, float throttleAbs, float shiftDip)
            => 0.7f + rpm01 * 0.85f + Clamp01(throttleAbs) * 0.12f - Clamp01(shiftDip) * 0.22f;

        /// <summary>Hızlanma → gövde yunuslaması (derece; +x = burun aşağı). İvmelenmede burun kalkar, frende çöker.</summary>
        public static float BodyPitchTarget(float longAccel)
            => Clamp(longAccel * 0.28f, -MaxBodyPitch, MaxBodyPitch) * -1f;

        /// <summary>Yanal ivme (yerel +x) → gövde yatışı (derece; +z = sola yatar, yani dönüşün dışına).</summary>
        public static float BodyRollTarget(float latAccel)
            => Clamp(latAccel * 0.32f, -MaxBodyRoll, MaxBodyRoll);

        /// <summary>Yaylı yumuşatma (üstel); k ~ 6-10.</summary>
        public static float Smooth(float current, float target, float k, float dt)
            => current + (target - current) * (1f - (float)Math.Exp(-k * Math.Max(0f, dt)));

        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);

        public static float DeltaAngle(float current, float target)
        {
            var d = (target - current) % 360f;
            if (d > 180f) d -= 360f;
            if (d < -180f) d += 360f;
            return d;
        }

        /// <summary>Taret dönüş hız sınırı: hedefe en fazla maxRate*dt derece ilerler.</summary>
        public static float TraverseStep(float current, float target, float maxRateDegPerSec, float dt)
        {
            var d = DeltaAngle(current, target);
            var step = maxRateDegPerSec * Math.Max(0f, dt);
            if (Math.Abs(d) <= step) return current + d;
            return current + (d > 0f ? step : -step);
        }

        /// <summary>Taret motor uğultusu seviyesi 0..1 (açısal hız / sınır).</summary>
        public static float WhineLevel(float yawRateDegPerSec, float pitchRateDegPerSec)
        {
            var y = Math.Abs(yawRateDegPerSec) / YawDegPerSec;
            var p = Math.Abs(pitchRateDegPerSec) / PitchDegPerSec;
            return Clamp01(Math.Max(y, p));
        }

        public static VehicleDamageState DamageStateFor(float hp01, bool destroyed)
        {
            if (destroyed || hp01 <= 0f) return VehicleDamageState.Yangin;
            if (hp01 <= 0.15f) return VehicleDamageState.Yangin;
            if (hp01 <= 0.3f) return VehicleDamageState.Agir;
            if (hp01 <= 0.55f) return VehicleDamageState.Hafif;
            return VehicleDamageState.Saglam;
        }

        /// <summary>Duman saçılma aralığı (sn); sağlamda 0 (yok).</summary>
        public static float SmokeInterval(VehicleDamageState s)
        {
            switch (s)
            {
                case VehicleDamageState.Hafif: return 0.9f;
                case VehicleDamageState.Agir: return 0.45f;
                case VehicleDamageState.Yangin: return 0.25f;
                default: return 0f;
            }
        }

        /// <summary>Gösterge şeması bölge durumu: 0 sağlam, 1 hasarlı, 2 kritik. Bölgeler can yüzdesine kademeli bozulur.</summary>
        public static int ZoneState(int zone, float hp01)
        {
            float warn, crit;
            switch (zone)
            {
                case 0: warn = 0.55f; crit = 0.3f; break;   // motor ilk bozulur
                case 1: warn = 0.4f; crit = 0.2f; break;    // kabin
                case 2: warn = 0.6f; crit = 0.35f; break;   // tekerler
                default: warn = 0.3f; crit = 0.12f; break;  // taret
            }

            return hp01 <= crit ? 2 : (hp01 <= warn ? 1 : 0);
        }

        /// <summary>Yön derecesi (0=K, saat yönü) → "KB 315".</summary>
        public static string HeadingLabel(float yawDeg)
        {
            var y = ((yawDeg % 360f) + 360f) % 360f;
            var idx = (int)((y + 22.5f) / 45f) % 8;
            return Cardinals[idx] + " " + ((int)Math.Round(y) % 360).ToString("000");
        }

        /// <summary>Farlar: gece/şafak/akşam otomatik açık.</summary>
        public static bool AutoHeadlights(bool dark) => dark;
    }
}
