using UnityEngine;

namespace Project.Infrastructure.Characters.WeaponHold
{
    /// <summary>Tutuş hesabı girdisi (hepsi 0..1 ağırlık, açılar derece; pitch: pozitif = aşağı).</summary>
    public struct HoldInput
    {
        public float Pitch;
        public float Run;
        public float Crouch;
        public float Prone;
        public float Seat;
        public float MoveWeight;
        public float Breathing;
        public float Recoil;
        public float LowReady;
        public float WallPull;
        public float StepCos;
        public float StepPhase;
        /// <summary>Yayın hedefe göre artığı (derece): silahın nişan eğimini geriden izlemesi.</summary>
        public float SpringLag;
        public WeaponHoldProfile Profile;
    }

    /// <summary>Tutuş hesabı çıktısı: gövde parçalarına eklenecek açılar ve silah yuvası ofseti.</summary>
    public struct HoldOutput
    {
        /// <summary>Kalça (Body) ek eğimi.</summary>
        public float PelvisPitch;
        /// <summary>Spine'a nişandan gelen eğim payı + atış yaslanması.</summary>
        public float SpineAim;
        public float ChestAim;
        public float HeadPitch;
        /// <summary>Kolların (ArmsPivot) dünya-eşdeğeri toplam eğimi.</summary>
        public float ArmsPitch;
        public float ArmsYaw;
        public Vector3 SocketOffset;
    }

    /// <summary>
    /// Silah tutuşunun saf hesapları: nişan eğiminin gövdeye dağıtımı, alçak hazır / koşu / duvar pozları, geri tepme eğrisi,
    /// sol el kundağı kaydırma (kapalı form) ve el bileği hizalama. SoldierModel yalnızca bu sonuçları kemiklere yazar.
    /// </summary>
    public static class WeaponHoldMath
    {
        public const float MaxSpringLagDegrees = 8f;
        public const float HandMaxDeviationDegrees = 45f;
        public const float WallStartFactor = 1.0f;
        public const float WallFullFactor = 0.45f;

        /// <summary>Geri tepme eğrisi: atışta anında tepe, sonra r^2 ile hızlı düşüş ve uzun kuyruk (r 1'den 0'a iner).</summary>
        public static float RecoilShape(float r)
        {
            r = Mathf.Clamp01(r);
            return r * r;
        }

        /// <summary>Yay artığını (yay - hedef) güvenli aralığa sıkıştırır.</summary>
        public static float ClampLag(float springValue, float target)
        {
            var lag = springValue - target;
            if (float.IsNaN(lag) || float.IsInfinity(lag))
                return 0f;
            return Mathf.Clamp(lag, -MaxSpringLagDegrees, MaxSpringLagDegrees);
        }

        /// <summary>Engel mesafesinden duvar geri çekme hedefi: silah boyunun 1.0 katında 0, 0.45 katında 1.</summary>
        public static float WallPullTarget(float distance, float weaponLength)
        {
            if (float.IsNaN(distance) || weaponLength <= 0.01f)
                return 0f;
            var start = weaponLength * WallStartFactor;
            var full = weaponLength * WallFullFactor;
            var t = Mathf.Clamp01((start - distance) / (start - full));
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// Nişan eğimini gövdeye dağıtır ve tutuş durumlarını (koşu, alçak hazır, duvar, geri tepme) kollara/yuvaya işler.
        /// Kollar toplam eğimi (Pitch) korur: silah yine nişan yönüne bakar, ama kalça-bel-göğüs-baş zinciri paylaşır.
        /// </summary>
        public static HoldOutput Evaluate(HoldInput i)
        {
            var p = i.Profile;
            var standing = (1f - i.Prone) * (1f - i.Seat);
            var kick = RecoilShape(i.Recoil);
            var o = new HoldOutput();

            o.PelvisPitch = -i.Pitch * p.HipCounter * standing;
            o.SpineAim = i.Pitch * p.SpineShare - kick * p.RecoilSpine;
            o.ChestAim = i.Pitch * p.ChestShare;
            o.HeadPitch = i.Pitch * p.HeadShare;

            // Koşu ve alçak hazır birbirini dışlar: koşu baskın.
            var low = i.LowReady * (1f - i.Run) * (1f - i.Prone) * (1f - i.Seat);
            var wall = i.WallPull * (1f - i.Run) * (1f - i.Prone);
            o.ArmsPitch = i.Pitch
                          + i.Run * p.SprintPitch
                          + i.Seat * 35f
                          + low * p.LowReadyPitch
                          - wall * p.WallPitch
                          - kick * p.RecoilArms
                          + i.Breathing * 0.3f
                          + i.SpringLag;
            o.ArmsYaw = i.Run * p.SprintYaw;

            var bobX = 0.006f * i.StepCos * i.MoveWeight * (1f + i.Run);
            var bobY = 0.004f * Mathf.Sin(i.StepPhase * 2f) * i.MoveWeight;
            o.SocketOffset = new Vector3(
                bobX,
                bobY - low * p.LowReadyDown,
                -kick * p.RecoilBack - low * p.LowReadyBack - wall * p.WallBack);
            return o;
        }

        /// <summary>
        /// Bileği hedef noktadan, namlu ekseni boyunca geriye çekerek omuz erişimine sokar (kapalı form, döngüsüz).
        /// En fazla (nokta-yuva).namlu - minForward kadar geri gider; erişilemiyorsa o sınıra kadar çeker.
        /// </summary>
        public static Vector3 ResolveWristReach(Vector3 socket, Vector3 axis, Vector3 wrist, Vector3 shoulder, float maxReach, float minForward)
        {
            if (axis.sqrMagnitude < 1e-8f)
                return wrist;
            axis.Normalize();
            var forward = Vector3.Dot(wrist - socket, axis);
            var maxRetreat = forward - minForward;
            if (maxRetreat <= 0f)
                return wrist;

            var d = wrist - shoulder;
            var c = d.sqrMagnitude - maxReach * maxReach;
            if (c <= 0f)
                return wrist;

            var b = Vector3.Dot(d, axis);
            var disc = b * b - c;
            float t;
            if (disc < 0f)
                t = b; // Doğru küreye değmez: en yakın noktaya kadar çek.
            else
                t = b - Mathf.Sqrt(disc);
            t = Mathf.Clamp(t, 0f, maxRetreat);
            return wrist - axis * t;
        }

        /// <summary>
        /// El bileğinin yerel dönüşü: önkol dönüşünden silahın tutuş eksenine hizalanır, ama alışılmış (legacy) pozdan
        /// en fazla <see cref="HandMaxDeviationDegrees"/> saptırılır. Böylece el kundağa/kabzaya oturur, bilek kırılmaz.
        /// </summary>
        public static Quaternion AlignHand(Quaternion forearmInPivot, Quaternion gripFrameInPivot, Quaternion bias, Quaternion legacy)
        {
            var desired = Quaternion.Inverse(forearmInPivot) * gripFrameInPivot * bias;
            if (!IsFinite(desired))
                return legacy;
            var angle = Quaternion.Angle(legacy, desired);
            if (angle <= HandMaxDeviationDegrees)
                return desired;
            return Quaternion.Slerp(legacy, desired, HandMaxDeviationDegrees / angle);
        }

        /// <summary>Namlu yönünden (pivot uzayı) el çerçevesi: kemik ekseni -Y namlu boyunca, +Z dışa-yukarı bakar.</summary>
        public static Quaternion GripFrame(Vector3 barrel, float rollDegrees)
        {
            if (barrel.sqrMagnitude < 1e-8f)
                return Quaternion.identity;
            var look = Quaternion.LookRotation(barrel.normalized, Vector3.up) * Quaternion.Euler(-90f, 0f, 0f);
            return look * Quaternion.Euler(0f, rollDegrees, 0f);
        }

        public static bool IsFinite(Quaternion q)
        {
            return !(float.IsNaN(q.x) || float.IsNaN(q.y) || float.IsNaN(q.z) || float.IsNaN(q.w)
                     || float.IsInfinity(q.x) || float.IsInfinity(q.y) || float.IsInfinity(q.z) || float.IsInfinity(q.w));
        }
    }
}
