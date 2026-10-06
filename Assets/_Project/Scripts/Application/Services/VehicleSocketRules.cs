using System;

namespace Project.Application.Services
{
    /// <summary>Araç materyal durumu: temiz, kirli, yanmış.</summary>
    public enum VehicleCondition
    {
        Clean = 0,
        Dirty = 1,
        Burnt = 2
    }

    /// <summary>
    /// Override araç prefab'ı soket sözleşmesi (saf mantık): Rotor_Main, Rotor_Tail, Rotor_Blur, Wheel_FL/FR/RL/RR,
    /// Door_*, Seat_N, Light_*. Eski adlar (MainRotor, Wheel_0, Door, Turret) alias olarak kabul edilir.
    /// </summary>
    public static class VehicleSocketRules
    {
        public const string RotorMain = "Rotor_Main";
        public const string RotorTail = "Rotor_Tail";
        public const string RotorBlur = "Rotor_Blur";
        public const string DoorPrefix = "Door_";
        public const string SeatPrefix = "Seat_";
        public const string LightPrefix = "Light_";
        public const string Turret = "Turret";

        /// <summary>Tekerlek soket adları; sıra KirpiModel.WheelPositions ile aynıdır (sol-ön, sağ-ön, sol-arka, sağ-arka).</summary>
        public static readonly string[] WheelNames = { "Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR" };

        /// <summary>Koltuk eşleşmesi için en büyük sapma (m); fazlası güvenilmez sayılır, prosedürel koltuk korunur.</summary>
        public const float SeatSnapTolerance = 1.6f;

        /// <summary>Bulanık disk açılış eşiği (rotor devri 0..1) ve histerezis.</summary>
        public const float BlurOnSpin = 0.6f;
        public const float BlurOffSpin = 0.45f;

        public static string[] MainRotorNames() => new[] { RotorMain, "MainRotor" };

        public static string[] TailRotorNames() => new[] { RotorTail, "TailRotor" };

        public static string[] BlurNames() => new[] { RotorBlur, "RotorBlur", "Rotor_Disc" };

        public static string[] WheelAliases(int index)
        {
            if (index < 0 || index >= WheelNames.Length)
                return new string[0];
            return new[] { WheelNames[index], "Wheel_" + index };
        }

        public static string[] DoorAliases() => new[] { "Door_Rear", "Door_R", "Door" };

        public static string[] SeatAliases(int index) => new[] { SeatPrefix + index, SeatPrefix + (index + 1).ToString("00") };

        /// <summary>Blender/Unity ek eklerini (".001", "(Clone)", boşluk) atıp küçük harfe çevirir.</summary>
        public static string Normalize(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;
            var n = name.Trim();
            var clone = n.IndexOf("(Clone)", StringComparison.Ordinal);
            if (clone >= 0)
                n = n.Substring(0, clone).Trim();
            var dot = n.LastIndexOf('.');
            if (dot > 0 && dot < n.Length - 1)
            {
                var allDigits = true;
                for (var i = dot + 1; i < n.Length; i++)
                    if (n[i] < '0' || n[i] > '9')
                        allDigits = false;
                if (allDigits)
                    n = n.Substring(0, dot);
            }

            return n.ToLowerInvariant();
        }

        /// <summary>Ad, aday adlardan biriyle (normalize edilmiş) eşleşiyor mu?</summary>
        public static bool Matches(string name, string[] candidates)
        {
            if (candidates == null)
                return false;
            var n = Normalize(name);
            if (n.Length == 0)
                return false;
            for (var i = 0; i < candidates.Length; i++)
                if (n == Normalize(candidates[i]))
                    return true;
            return false;
        }

        public static bool HasPrefix(string name, string prefix)
        {
            var n = Normalize(name);
            return n.StartsWith(Normalize(prefix), StringComparison.Ordinal);
        }

        /// <summary>Override koltuk noktası prosedürel olana yeterince yakınsa güvenilir sayılır.</summary>
        public static bool IsSeatTrustworthy(float dx, float dy, float dz)
        {
            if (float.IsNaN(dx) || float.IsNaN(dy) || float.IsNaN(dz))
                return false;
            return dx * dx + dy * dy + dz * dz <= SeatSnapTolerance * SeatSnapTolerance;
        }

        /// <summary>Bulanık disk görünürlüğü (histerezisli).</summary>
        public static bool BlurVisible(float spin01, bool currentlyVisible)
        {
            return currentlyVisible ? spin01 > BlurOffSpin : spin01 > BlurOnSpin;
        }

        /// <summary>Hasara göre durum: yok edilmiş/0 can yanmış, yarıdan az kirli, aksi temiz.</summary>
        public static VehicleCondition ConditionFor(float health01, bool destroyed)
        {
            if (destroyed || health01 <= 0f)
                return VehicleCondition.Burnt;
            return health01 < 0.5f ? VehicleCondition.Dirty : VehicleCondition.Clean;
        }

        /// <summary>Durumun renk çarpanı (rgb) ve pürüzlülük için yumuşaklık çarpanı.</summary>
        public static void ConditionTint(VehicleCondition condition, out float r, out float g, out float b, out float smoothnessMul)
        {
            switch (condition)
            {
                case VehicleCondition.Dirty:
                    r = 0.72f; g = 0.66f; b = 0.55f; smoothnessMul = 0.55f;
                    break;
                case VehicleCondition.Burnt:
                    r = 0.16f; g = 0.15f; b = 0.14f; smoothnessMul = 0.25f;
                    break;
                default:
                    r = 1f; g = 1f; b = 1f; smoothnessMul = 1f;
                    break;
            }
        }
    }
}
