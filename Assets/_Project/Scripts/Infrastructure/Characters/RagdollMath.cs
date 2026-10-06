using System;
using UnityEngine;

namespace Project.Infrastructure.Characters
{
    /// <summary>Ragdoll parçaları (kemik sırası sabittir; dizi indeksi olarak kullanılır).</summary>
    public enum RagdollPart
    {
        Hips = 0,
        Spine = 1,
        Chest = 2,
        Head = 3,
        UpperArmL = 4,
        UpperArmR = 5,
        ForearmL = 6,
        ForearmR = 7,
        ThighL = 8,
        ThighR = 9,
        ShinL = 10,
        ShinR = 11
    }

    public enum RagdollJointKind
    {
        Root = 0,
        Ball = 1,
        Hinge = 2
    }

    /// <summary>Tek parçanın eklem/kütle tanımı. Açılar derece; sarkaç ekseni dünya "sağ" eksenidir.</summary>
    public readonly struct RagdollJointSpec
    {
        public readonly RagdollPart Part;
        public readonly int Parent;
        public readonly RagdollJointKind Kind;
        public readonly float TwistLow;
        public readonly float TwistHigh;
        public readonly float Swing1;
        public readonly float Swing2;
        public readonly float HingeMin;
        public readonly float HingeMax;
        public readonly float MassFraction;
        public readonly float Radius;

        public RagdollJointSpec(RagdollPart part, int parent, RagdollJointKind kind, float twistLow, float twistHigh,
            float swing1, float swing2, float hingeMin, float hingeMax, float massFraction, float radius)
        {
            Part = part;
            Parent = parent;
            Kind = kind;
            TwistLow = twistLow;
            TwistHigh = twistHigh;
            Swing1 = swing1;
            Swing2 = swing2;
            HingeMin = hingeMin;
            HingeMax = hingeMax;
            MassFraction = massFraction;
            Radius = radius;
        }
    }

    /// <summary>Isabet itkisi sonucu (saf veri).</summary>
    public struct RagdollKick
    {
        /// <summary>Tüm parçalara eklenen ortak hız değişimi (m/s).</summary>
        public Vector3 SharedDeltaV;

        /// <summary>Yalnız vurulan parçaya eklenen ek hız (m/s).</summary>
        public Vector3 HitPartDeltaV;

        /// <summary>Vurulan parçaya eklenen açısal hız (rad/s).</summary>
        public Vector3 Spin;
    }

    /// <summary>Ragdoll için saf matematik: eklem sınırları, kütle dağılımı, itki hesabı, kademe sınırları.</summary>
    public static class RagdollMath
    {
        public const int PartCount = 12;
        /// <summary>Toplam vücut kütlesi (kg); Winter antropometrik oranlarına göre dağıtılır.</summary>
        public const float TotalMass = 80f;

        /// <summary>Gerçek mermi momentumunu oyun hissine çeviren çarpan (Tarkov: ölçülü, CoD: görünür).</summary>
        public const float ImpulseScale = 22f;

        public const float MaxSharedSpeed = 4.5f;
        public const float MaxHitSpeed = 7f;
        public const float MaxSpin = 10f;

        /// <summary>Mermi/darbe için net itki üst sınırı (N·s, ImpulseScale sonrası): tüfek vuruşu yığar, fırlatmaz.</summary>
        public const float MaxNetImpulseNs = 250f;

        /// <summary>Patlama/araç için net itki üst sınırı (N·s).</summary>
        public const float MaxExplosionImpulseNs = 520f;

        /// <summary>Patlamada ortak / isabet parçası dikey hız üst sınırları (m/s): en fazla ~2 m yükselir.</summary>
        public const float MaxSharedLoft = 3.2f;
        public const float MaxHitLoft = 2.8f;

        /// <summary>Kafa vuruşunda isabet parçasına eklenen hafif geri kırılma dönüşü (rad/s).</summary>
        public const float HeadSnapSpin = 4f;

        // Sürtünme / sönüm: gövdeler eğimde kaymasın.
        public const float BodyDynamicFriction = 0.9f;
        public const float BodyStaticFriction = 1.1f;
        public const float BodyLinearDamping = 0.12f;

        // Düşen silah: devralınan hız çarpanı ve küçük dönüş (rad/s).
        public const float WeaponInheritScale = 0.85f;
        public const float WeaponSpinMax = 3.5f;
        public const float WeaponMass = 3.5f;

        // Kask düşmesi (yalnız kafadan vuruşta): fırlama hızı ve dönüş.
        public const float HelmetPopSpeed = 2.4f;
        public const float HelmetPopUp = 1.6f;
        public const float HelmetSpinMax = 9f;
        public const float HelmetMass = 1.4f;

        // Eklem sınırları (derece). Baş: baykuş duruşu yok; diz/dirsek ters bükülmez.
        public const float HeadTwist = 30f;
        public const float HeadSwing1 = 35f;
        public const float HeadSwing2 = 25f;
        public const float KneeMax = 135f;
        public const float ElbowMax = 140f;
        public const float HipTwist = 15f;
        public const float HipSwing1 = 70f;
        public const float HipSwing2 = 30f;

        private static readonly int[] TierMaxActive = { 2, 6, 12, 20 };
        private static readonly int[] TierSolver = { 6, 8, 10, 12 };
        private static readonly float[] TierAngularDamping = { 0.9f, 0.6f, 0.5f, 0.4f };

        public static readonly RagdollJointSpec[] Specs =
        {
            //                       parça                   üst  tür                      twL    twH   sw1   sw2  hMin  hMax  kütle  yarıçap
            new RagdollJointSpec(RagdollPart.Hips,      -1, RagdollJointKind.Root,   0f,    0f,   0f,   0f,  0f,   0f,   0.170f, 0.00f),
            new RagdollJointSpec(RagdollPart.Spine,      0, RagdollJointKind.Ball,  -15f,  15f,  20f,  15f, 0f,   0f,   0.110f, 0.00f),
            new RagdollJointSpec(RagdollPart.Chest,      1, RagdollJointKind.Ball,  -15f,  15f,  20f,  15f, 0f,   0f,   0.220f, 0.00f),
            new RagdollJointSpec(RagdollPart.Head,       2, RagdollJointKind.Ball,  -HeadTwist, HeadTwist, HeadSwing1, HeadSwing2, 0f, 0f, 0.070f, 0.11f),
            new RagdollJointSpec(RagdollPart.UpperArmL,  2, RagdollJointKind.Ball,  -35f,  50f,  95f,  60f, 0f,   0f,   0.030f, 0.045f),
            new RagdollJointSpec(RagdollPart.UpperArmR,  2, RagdollJointKind.Ball,  -35f,  50f,  95f,  60f, 0f,   0f,   0.030f, 0.045f),
            new RagdollJointSpec(RagdollPart.ForearmL,   4, RagdollJointKind.Hinge,  0f,    0f,   0f,   0f, -ElbowMax, 0f, 0.024f, 0.04f),
            new RagdollJointSpec(RagdollPart.ForearmR,   5, RagdollJointKind.Hinge,  0f,    0f,   0f,   0f, -ElbowMax, 0f, 0.024f, 0.04f),
            new RagdollJointSpec(RagdollPart.ThighL,     0, RagdollJointKind.Ball,  -HipTwist, HipTwist, HipSwing1, HipSwing2, 0f, 0f, 0.105f, 0.075f),
            new RagdollJointSpec(RagdollPart.ThighR,     0, RagdollJointKind.Ball,  -HipTwist, HipTwist, HipSwing1, HipSwing2, 0f, 0f, 0.105f, 0.075f),
            new RagdollJointSpec(RagdollPart.ShinL,      8, RagdollJointKind.Hinge,  0f,    0f,   0f,   0f,  0f,  KneeMax, 0.056f, 0.06f),
            new RagdollJointSpec(RagdollPart.ShinR,      9, RagdollJointKind.Hinge,  0f,    0f,   0f,   0f,  0f,  KneeMax, 0.056f, 0.06f)
        };

        public static int ClampTier(int tier) => tier < 0 ? 0 : (tier > 3 ? 3 : tier);

        /// <summary>Kademeye göre aynı anda aktif (fizikli) ragdoll sayısı: 2 / 6 / 12 / 20.</summary>
        public static int MaxActive(int tier) => TierMaxActive[ClampTier(tier)];

        public static int SolverIterations(int tier) => TierSolver[ClampTier(tier)];

        public static float AngularDamping(int tier) => TierAngularDamping[ClampTier(tier)];

        /// <summary>Düşük kademede omurga ve ön kol fiziği kapatılır (8 gövde); diğerlerinde 12.</summary>
        public static bool PartEnabled(RagdollPart part, int tier)
        {
            if (ClampTier(tier) > 0)
                return true;
            return part != RagdollPart.Spine && part != RagdollPart.ForearmL && part != RagdollPart.ForearmR;
        }

        public static float MassOf(RagdollPart part, float totalMass = TotalMass) => Specs[(int)part].MassFraction * totalMass;

        public static float MassFractionSum()
        {
            var sum = 0f;
            for (var i = 0; i < Specs.Length; i++)
                sum += Specs[i].MassFraction;
            return sum;
        }

        /// <summary>
        /// Menteşe sınırlarını (mutlak açı aralığı) başlangıç açısına göre göreli sınıra çevirir; Unity menteşe sıfırı
        /// eklemin kurulduğu pozdur. Aralık her zaman 0'ı içerir (aksi halde eklem ilk karede sıçrar).
        /// </summary>
        public static void HingeRelativeLimits(float absMin, float absMax, float currentAngle, out float min, out float max)
        {
            min = absMin - currentAngle;
            max = absMax - currentAngle;
            if (min > 0f)
                min = 0f;
            if (max < 0f)
                max = 0f;
        }

        public static bool IsExplosive(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId))
                return false;
            var id = weaponId.ToLowerInvariant();
            return id.Contains("grenade") || id.Contains("artillery") || id.Contains("rpg") || id.Contains("rocket") ||
                   id.Contains("c4") || id.Contains("explo") || id.Contains("mine") || id.Contains("mortar") || id.Contains("bomb");
        }

        /// <summary>Silah/kalibre ve hasara göre ham itki (N·s). Patlama ve araç ağırdır; düşme/bölge/yumruk neredeyse sıfır.</summary>
        public static float ImpulseNs(string weaponId, float damage, bool headshot)
        {
            var id = string.IsNullOrEmpty(weaponId) ? string.Empty : weaponId.ToLowerInvariant();
            float baseNs;
            if (IsExplosive(id))
                baseNs = 40f;
            else if (id.Contains("vehicle"))
                baseNs = 30f;
            else if (id == "zone" || id == "fall")
                baseNs = 0.5f;
            else if (id.Contains("melee"))
                baseNs = 2f;
            else if (id.StartsWith("sr_") || id.Contains("jng90"))
                baseNs = 16f;
            else if (id.StartsWith("dmr") || id.Contains("762mt"))
                baseNs = 12f;
            else if (id.StartsWith("lmg") || id.Contains("mg3"))
                baseNs = 11f;
            else if (id.StartsWith("sg_"))
                baseNs = 13f;
            else if (id.Contains("mpt76") || id.Contains("g3"))
                baseNs = 11f;
            else if (id.StartsWith("ar_"))
                baseNs = 7f;
            else if (id.StartsWith("smg"))
                baseNs = 5f;
            else if (id.StartsWith("pistol"))
                baseNs = 4.5f;
            else
                baseNs = 6f;

            var dmgScale = Mathf.Clamp(damage / 35f, 0.5f, 2f);
            if (IsExplosive(id) || id.Contains("vehicle"))
                dmgScale = Mathf.Clamp(damage / 60f, 0.8f, 2f);
            var f = baseNs * dmgScale;
            return headshot ? f * 1.2f : f;
        }

        /// <summary>Patlamada yukarı sapma eklenir (savrulma), mermide hafif.</summary>
        public static Vector3 KickDirection(Vector3 dir, bool explosive)
        {
            if (dir.sqrMagnitude < 1e-6f)
                dir = Vector3.back;
            dir.Normalize();
            dir.y += explosive ? 0.55f : 0.1f;
            return dir.normalized;
        }

        /// <summary>
        /// Ölçeklenmiş net itki (N·s): ham itki x ImpulseScale, mermide 250, patlamada 520 ile sınırlı.
        /// </summary>
        public static float NetImpulse(float impulseNs, bool explosive)
        {
            var j = Mathf.Max(0f, impulseNs) * ImpulseScale;
            return Mathf.Min(j, explosive ? MaxExplosionImpulseNs : MaxNetImpulseNs);
        }

        /// <summary>Ham itkiden (N·s) ortak + isabet parçası hız değişimi üretir; hepsi üst sınırlıdır.</summary>
        public static RagdollKick ComputeKick(Vector3 dir, float impulseNs, bool explosive, float hitPartMass)
        {
            return ComputeKick(dir, impulseNs, explosive, hitPartMass, false);
        }

        public static RagdollKick ComputeKick(Vector3 dir, float impulseNs, bool explosive, float hitPartMass, bool headSnap)
        {
            var d = KickDirection(dir, explosive);
            var j = NetImpulse(impulseNs, explosive);
            var shared = Mathf.Min(j * 0.55f / TotalMass, MaxSharedSpeed);
            var hit = Mathf.Min(j * 0.45f / Mathf.Max(0.5f, hitPartMass), MaxHitSpeed);
            var sharedV = d * shared;
            var hitV = d * hit;
            sharedV.y = Mathf.Min(sharedV.y, MaxSharedLoft);
            hitV.y = Mathf.Min(hitV.y, MaxHitLoft);
            var axis = Vector3.Cross(Vector3.up, d);
            if (axis.sqrMagnitude < 1e-6f)
                axis = Vector3.right;
            axis.Normalize();
            var spin = Mathf.Min(j * 0.01f, MaxSpin) * (explosive ? 1.3f : 1f);
            if (headSnap)
                spin += HeadSnapSpin;
            return new RagdollKick
            {
                SharedDeltaV = sharedV,
                HitPartDeltaV = hitV,
                Spin = axis * Mathf.Min(spin, MaxSpin)
            };
        }

        /// <summary>Düşen silahın başlangıç hızı: sahibin hızının bir kısmı + ortak itkinin payı.</summary>
        public static Vector3 WeaponVelocity(Vector3 ownerVelocity, Vector3 sharedDeltaV)
        {
            var v = ownerVelocity * WeaponInheritScale + sharedDeltaV * 1.1f;
            var max = MaxSharedSpeed + 6f;
            return v.sqrMagnitude > max * max ? v.normalized * max : v;
        }

        /// <summary>Kask fırlama hızı: isabet yönü + yukarı; üst sınırlı.</summary>
        public static Vector3 HelmetVelocity(Vector3 hitDir, Vector3 inherit)
        {
            if (hitDir.sqrMagnitude < 1e-6f)
                hitDir = Vector3.back;
            var h = new Vector3(hitDir.x, 0f, hitDir.z);
            h = h.sqrMagnitude < 1e-6f ? Vector3.zero : h.normalized;
            return inherit + h * HelmetPopSpeed + Vector3.up * HelmetPopUp;
        }

        /// <summary>İsabet bölgesinden hedef ragdoll parçası.</summary>
        public static RagdollPart PartForRegion(int bodyPart, bool leftSide)
        {
            // BodyPart: Torso=0, Head=1, Arm=2, Leg=3
            switch (bodyPart)
            {
                case 1: return RagdollPart.Head;
                case 2: return leftSide ? RagdollPart.UpperArmL : RagdollPart.UpperArmR;
                case 3: return leftSide ? RagdollPart.ThighL : RagdollPart.ThighR;
                default: return RagdollPart.Chest;
            }
        }
    }

    /// <summary>
    /// Yerleşme algılayıcı: hız eşiğinin altında kesintisiz kalırsa (min süre sonrası) ya da sert zaman aşımında
    /// (varsayılan 6 s) donma kararı verir.
    /// </summary>
    public sealed class RagdollSettle
    {
        public float LinearThreshold = 0.12f;
        public float AngularThreshold = 0.6f;
        public float MinTime = 1.2f;
        public float SettleHold = 0.6f;
        public float MaxTime = 6f;

        private float _elapsed;
        private float _calm;

        public float Elapsed => _elapsed;

        public void Reset()
        {
            _elapsed = 0f;
            _calm = 0f;
        }

        /// <summary>True dönerse ragdoll donmalıdır.</summary>
        public bool Tick(float dt, float maxLinear, float maxAngular)
        {
            _elapsed += dt;
            if (maxLinear <= LinearThreshold && maxAngular <= AngularThreshold)
                _calm += dt;
            else
                _calm = 0f;

            if (_elapsed >= MaxTime)
                return true;
            return _elapsed >= MinTime && _calm >= SettleHold;
        }
    }
}
