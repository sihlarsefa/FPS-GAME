using UnityEngine;

namespace Project.Infrastructure.Weapons.Viewmodel
{
    /// <summary>Bilek bükülme sınırı: el, ön kol eksenine göre makul açıda kalır (kırık bilek yok).</summary>
    public static class WristLimit
    {
        /// <summary>Bilek bükülmesi (avuç içi/sırtı yönü) ve yana sapma sınırları, derece.</summary>
        public const float MaxFlexDegrees = 55f;
        public const float MaxDeviationDegrees = 32f;

        /// <summary>Elin ileri ekseninin ön kola göre yaw/pitch'ini sınırlar; dönüş (twist) korunur.</summary>
        public static Quaternion Clamp(Vector3 foreDir, Quaternion handRot, float maxFlexDeg, float maxDevDeg)
        {
            if (foreDir.sqrMagnitude < 1e-10f)
                return handRot;

            var up = handRot * Vector3.up;
            var f = foreDir.normalized;
            if (Vector3.Cross(f, up).sqrMagnitude < 1e-4f)
                up = Mathf.Abs(f.y) < 0.95f ? Vector3.up : Vector3.forward;
            var foreRot = Quaternion.LookRotation(f, up);
            var local = Quaternion.Inverse(foreRot) * handRot;
            var fwd = local * Vector3.forward;
            var yaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
            var pitch = Mathf.Asin(Mathf.Clamp(fwd.y, -1f, 1f)) * Mathf.Rad2Deg;
            var cy = Mathf.Clamp(yaw, -maxDevDeg, maxDevDeg);
            var cp = Mathf.Clamp(pitch, -maxFlexDeg, maxFlexDeg);
            if (Mathf.Approximately(cy, yaw) && Mathf.Approximately(cp, pitch))
                return handRot;

            var clampedFwd = Quaternion.Euler(-cp, cy, 0f) * Vector3.forward;
            return foreRot * (Quaternion.FromToRotation(fwd, clampedFwd) * local);
        }

        /// <summary>Elin ileri ekseni ile ön kol yönü arasındaki açı (derece).</summary>
        public static float BendAngle(Vector3 foreDir, Quaternion handRot)
        {
            return Vector3.Angle(foreDir, handRot * Vector3.forward);
        }
    }

    /// <summary>İki kemikli kol çözücü (saf): omuz-dirsek-bilek; erişim dışında omuzu kaydırır.</summary>
    public static class TwoBoneSolver
    {
        public struct Result
        {
            public Vector3 Shoulder;
            public Vector3 Elbow;
            /// <summary>0 = kol tam kıvrık, 1 = tam düz.</summary>
            public float Extension;
            /// <summary>Omuz hedefe ulaşmak için kaydırıldı mı.</summary>
            public bool ShoulderShifted;
        }

        public static Result Solve(Vector3 shoulder, Vector3 wrist, Vector3 pole, float upper, float fore)
        {
            var r = new Result { Shoulder = shoulder };
            var toTarget = wrist - shoulder;
            var distance = toTarget.magnitude;
            var dir = distance > 1e-5f ? toTarget / distance : Vector3.forward;

            var maxReach = (upper + fore) * 0.995f;
            var minReach = Mathf.Abs(upper - fore) + 0.05f;
            if (distance > maxReach)
            {
                r.Shoulder = wrist - dir * maxReach;
                distance = maxReach;
                r.ShoulderShifted = true;
            }
            else if (distance < minReach)
            {
                r.Shoulder = wrist - dir * minReach;
                distance = minReach;
                r.ShoulderShifted = true;
            }

            var cosA = Mathf.Clamp((upper * upper + distance * distance - fore * fore) / (2f * upper * distance), -1f, 1f);
            var sinA = Mathf.Sqrt(Mathf.Max(0f, 1f - cosA * cosA));
            var p = pole - Vector3.Dot(pole, dir) * dir;
            if (p.sqrMagnitude < 1e-6f)
                p = Vector3.Cross(dir, Vector3.right);
            p.Normalize();
            r.Elbow = r.Shoulder + dir * (upper * cosA) + p * (upper * sinA);
            r.Extension = Mathf.Clamp01((distance - minReach) / Mathf.Max(1e-4f, maxReach - minReach));
            return r;
        }
    }

    /// <summary>Sınıfa göre sol el destek kayması ve nişanda dirsek yönü.</summary>
    public static class SupportHand
    {
        /// <summary>
        /// Nişanda sol elin el kundağı üzerindeki kayması (kök uzayı, aim01=1). Tüfekte el geriye+aşağı toplanır,
        /// LMG/tabancada kaymaz (iki dipli/kabze tutuşu sabit kalır).
        /// </summary>
        public static Vector3 AdsShift(GripClass c)
        {
            switch (c)
            {
                case GripClass.Pistol: return Vector3.zero;
                case GripClass.Smg: return new Vector3(0f, -0.006f, -0.020f);
                case GripClass.Rifle: return new Vector3(0f, -0.010f, -0.028f);
                case GripClass.Dmr: return new Vector3(0f, -0.012f, -0.030f);
                case GripClass.Sniper: return new Vector3(0f, -0.014f, -0.034f);
                case GripClass.Lmg: return new Vector3(0f, -0.004f, -0.008f);
                case GripClass.Shotgun: return new Vector3(0f, -0.008f, -0.036f);
                default: return Vector3.zero;
            }
        }

        /// <summary>Nişanda dirsekler gövdeye doğru toplanır (kalçada dışa-aşağı).</summary>
        public static Vector3 AdaptPole(Vector3 basePole, bool left, float aim01)
        {
            var a = Mathf.Clamp01(aim01);
            var tucked = new Vector3(left ? -0.30f : 0.30f, -1f, -0.35f).normalized;
            var p = Vector3.Lerp(basePole.normalized, tucked, a * 0.55f);
            return p.sqrMagnitude < 1e-8f ? basePole : p.normalized;
        }
    }

    /// <summary>Viewmodel FOV'u ve silahın ekranda kapladığı alan (dünya FOV'undan bağımsız, sınıfa göre).</summary>
    public static class ViewmodelFraming
    {
        /// <summary>Kalça FOV'una sınıf ofseti (derece): uzun silahlar daha geniş açıyla ekranı daha az kaplar.</summary>
        public static float FovOffset(GripClass c)
        {
            switch (c)
            {
                case GripClass.Pistol: return 8f;
                case GripClass.Smg: return 5f;
                case GripClass.Rifle: return 4f;
                case GripClass.Dmr: return 5f;
                case GripClass.Sniper: return 6f;
                case GripClass.Lmg: return 6f;
                case GripClass.Shotgun: return 5f;
                default: return 0f;
            }
        }

        public static float ClassFov(float hipFov, GripClass c)
        {
            return Mathf.Clamp(hipFov + FovOffset(c), 40f, 90f);
        }

        /// <summary>Yatay FOV (derece), dikey FOV ve en/boy oranından.</summary>
        public static float HorizontalFov(float verticalFovDeg, float aspect)
        {
            var v = Mathf.Clamp(verticalFovDeg, 1f, 170f) * Mathf.Deg2Rad;
            return 2f * Mathf.Atan(Mathf.Tan(v * 0.5f) * Mathf.Max(0.1f, aspect)) * Mathf.Rad2Deg;
        }

        /// <summary>Yarı genişliği extent olan nesnenin derinlikte kapladığı yatay ekran oranı (0..1+; yarı genişlik/yarı ekran).</summary>
        public static float HalfWidthFraction(float extent, float depth, float verticalFovDeg, float aspect)
        {
            if (depth <= 1e-4f)
                return 1f;
            var h = HorizontalFov(verticalFovDeg, aspect) * Mathf.Deg2Rad;
            return extent / (depth * Mathf.Tan(h * 0.5f));
        }

        /// <summary>Hedef kaplama oranı için gereken en küçük derinlik (silahı ileri itme miktarını hesaplar).</summary>
        public static float DepthForFraction(float extent, float targetFraction, float verticalFovDeg, float aspect)
        {
            var h = HorizontalFov(verticalFovDeg, aspect) * Mathf.Deg2Rad;
            return extent / (Mathf.Max(0.01f, targetFraction) * Mathf.Tan(h * 0.5f));
        }

        /// <summary>Kalça duruşunda silahın en çok kaplayabileceği yarı-genişlik oranı (sınıfa göre).</summary>
        public static float MaxHalfWidthFraction(GripClass c)
        {
            switch (c)
            {
                case GripClass.Pistol: return 0.34f;
                case GripClass.Smg: return 0.42f;
                case GripClass.Lmg: return 0.52f;
                default: return 0.46f;
            }
        }

        /// <summary>Silah çok büyük görünüyorsa gereken geri çekme (m, +derinlik); küçükse 0.</summary>
        public static float PullBack(float extent, float depth, float verticalFovDeg, float aspect, GripClass c)
        {
            var need = DepthForFraction(extent, MaxHalfWidthFraction(c), verticalFovDeg, aspect);
            return Mathf.Max(0f, need - depth);
        }
    }
}
