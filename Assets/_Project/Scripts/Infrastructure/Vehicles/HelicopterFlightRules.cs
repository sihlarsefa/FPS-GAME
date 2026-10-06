using System;

namespace Project.Infrastructure.Vehicles
{
    /// <summary>Oyuncu helikopteri (T-70) uçuş kararlarının saf mantığı (Unity'siz, test edilebilir).</summary>
    public static class HelicopterFlightRules
    {
        public const float Gravity = 9.81f;
        /// <summary>Bu kolektifte (rotor tam devirde) yerçekimi dengelenir.</summary>
        public const float HoverCollective = 0.5f;
        public const float SpinUpSeconds = 6f;
        public const float SpinDownSeconds = 12f;
        public const float MinLiftRotor = 0.85f;
        public const float MaxPitchDeg = 28f;
        public const float MaxRollDeg = 32f;
        public const float SafeImpactSpeed = 4f;
        public const float HardImpactSpeed = 7f;
        public const float HardTiltDeg = 25f;
        public const float RotorStrikeBaseDamage = 220f;

        public const int PassengerSeats = 9;
        public const int GunnerSeats = 2;

        public enum SeatKind { None, Pilot, Gunner, Passenger }

        /// <summary>Rotor devri 0..1: motor açıkken hedefe, kapalıyken 0'a lineer yaklaşır.</summary>
        public static float StepRotor(float current, bool engineOn, float dt)
        {
            if (dt <= 0f || float.IsNaN(current))
                return Clamp01(float.IsNaN(current) ? 0f : current);
            var step = dt / (engineOn ? SpinUpSeconds : SpinDownSeconds);
            return Clamp01(engineOn ? current + step : current - step);
        }

        /// <summary>Kaldırma çarpanı: rotor devrinin karesi; MinLiftRotor altında aracı kaldıramaz.</summary>
        public static float LiftFactor(float rotor01)
        {
            var r = Clamp01(rotor01);
            return r < MinLiftRotor ? r * r * 0.5f : r * r;
        }

        /// <summary>Yer etkisi: rotor çapı kadar yüksekliğin altında kaldırma en çok %25 artar.</summary>
        public static float GroundEffect(float agl, float rotorRadius)
        {
            if (rotorRadius <= 0.01f || agl >= rotorRadius || float.IsNaN(agl))
                return 1f;
            var t = 1f - Clamp01(Math.Max(agl, 0f) / rotorRadius);
            return 1f + 0.25f * t;
        }

        /// <summary>Dikey ivme (m/s²): kolektif, rotor devri ve yer etkisiyle; eğimde dikey bileşen cos(tilt) ile azalır (çağıran uygular).</summary>
        public static float LiftAcceleration(float collective, float rotor01, float agl, float rotorRadius)
            => Gravity * (Clamp01(collective) / HoverCollective) * LiftFactor(rotor01) * GroundEffect(agl, rotorRadius);

        /// <summary>Kolektifi girdiyle değiştirir (W artırır, S azaltır); 0..1.</summary>
        public static float StepCollective(float collective, float input, float dt)
            => Clamp01(collective + Clamp(input, -1f, 1f) * 0.45f * dt);

        /// <summary>Otomatik havada asılı kalma: dikey hızı sıfırlayacak kolektif (P kontrol, yumuşak).</summary>
        public static float AutoHoverCollective(float verticalSpeed, float targetVerticalSpeed = 0f)
        {
            var error = targetVerticalSpeed - verticalSpeed;
            return Clamp(HoverCollective + error * 0.08f, 0.2f, 0.8f);
        }

        /// <summary>Pilot yokken havadaki araç kontrollü alçalır (otorotasyon benzeri).</summary>
        public static float UnpilotedCollective(float verticalSpeed)
            => AutoHoverCollective(verticalSpeed, -3f);

        /// <summary>Fare birikimi → hedef eğim (derece). Girdi -1..1.</summary>
        public static float TargetPitch(float input) => Clamp(input, -1f, 1f) * MaxPitchDeg;
        public static float TargetRoll(float input) => Clamp(input, -1f, 1f) * MaxRollDeg;

        /// <summary>Fare delta'sını eğim komutuna işler; merkeze dönüş (decay) uygular.</summary>
        public static float StepStick(float stick, float mouseDelta, float dt, float sensitivity = 0.012f, float decayPerSecond = 1.4f)
        {
            var s = stick + mouseDelta * sensitivity;
            var decay = decayPerSecond * Math.Max(dt, 0f);
            if (Math.Abs(s) <= decay)
                s = 0f;
            else
                s -= Math.Sign(s) * decay;
            return Clamp(s, -1f, 1f);
        }

        /// <summary>Sert iniş / çarpma hasarı. Güvenli hız altı 0; sert eğimde ek çarpan.</summary>
        public static float CrashDamage(float impactSpeed, float tiltDegrees)
        {
            if (float.IsNaN(impactSpeed) || impactSpeed <= SafeImpactSpeed)
                return 0f;
            var dmg = (impactSpeed - SafeImpactSpeed) * 55f;
            if (impactSpeed >= HardImpactSpeed)
                dmg += 120f;
            if (tiltDegrees > HardTiltDeg)
                dmg *= 1.5f;
            return dmg;
        }

        /// <summary>Rotor engel/zemine çarparsa hasar; rotor duruyorsa 0.</summary>
        public static float RotorStrikeDamage(float rotor01)
        {
            var r = Clamp01(rotor01);
            return r < 0.2f ? 0f : RotorStrikeBaseDamage * r;
        }

        /// <summary>Koltuk seçimi: önce pilot, sonra yolcu, sonra kapı nişancısı. Doluluk dizileri null olabilir.</summary>
        public static SeatKind PickSeat(bool pilotFree, bool[] passengerOccupied, bool[] gunnerOccupied, out int index)
        {
            index = -1;
            if (pilotFree)
                return SeatKind.Pilot;
            var p = KirpiCrewRules.FirstFreeSeat(passengerOccupied);
            if (p >= 0)
            {
                index = p;
                return SeatKind.Passenger;
            }

            var g = KirpiCrewRules.FirstFreeSeat(gunnerOccupied);
            if (g >= 0)
            {
                index = g;
                return SeatKind.Gunner;
            }

            return SeatKind.None;
        }

        /// <summary>Yere basıyor sayılma: yükseklik küçük ve dikey hız düşükse (iniş kızakları temasta).</summary>
        public static bool IsLanded(float agl, float verticalSpeed) => agl < 0.35f && Math.Abs(verticalSpeed) < 1.2f;

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
    }
}
