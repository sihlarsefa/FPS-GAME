using UnityEngine;

namespace Project.Infrastructure.Vehicles
{
    public enum VehicleKind { Kirpi, Cobra }

    /// <summary>Sürülebilir araç ayarları (ScriptableObject'siz sınıf). Kirpi değerleri eski sabitlerle birebir aynıdır.</summary>
    public sealed class VehicleConfig
    {
        public VehicleKind Kind;
        public string ObjectName;
        public string DisplayName;
        public int SeatCount = 9;
        public float MaxHealth = 1200f;
        public float MaxSpeedKmh = 85f;
        public float RoadkillSpeedKmh = 25f;
        public float ExplosionRadius = 8f;
        public float ExplosionDamage = 180f;
        public float MassKg = 14000f;
        public float SpringForce = 52000f;
        public float DamperForce = 6500f;
        public float SuspensionRest = 0.55f;
        public float WheelRadius = 0.48f;
        public float WheelY = 0.48f;
        public float DriveForce = 38000f;
        public float BrakeForce = 52000f;
        public float SteerAngle = 32f;
        public float LateralGrip = 9000f;
        public float AntiRoll = 18000f;
        public float WheelBaseZ = 1.55f;
        public float TrackX = 1.05f;
        public Vector3 CenterOfMass = new Vector3(0f, -0.35f, 0.1f);
        public Vector3 DustOffset = new Vector3(0f, 0.1f, -1.8f);
        public int TurretBelt = 200;
        public float TurretSpread = 0.9f;

        public static readonly VehicleConfig Kirpi = new VehicleConfig
        {
            Kind = VehicleKind.Kirpi,
            ObjectName = "Kirpi",
            DisplayName = "Kirpi"
        };

        /// <summary>Otokar Cobra: hafif 4x4 zırhlı, hızlı, 6 koltuk (sürücü + 6 yolcu), çatı 12.7 mm.</summary>
        public static readonly VehicleConfig Cobra = new VehicleConfig
        {
            Kind = VehicleKind.Cobra,
            ObjectName = "Cobra",
            DisplayName = "Otokar Cobra",
            SeatCount = 6,
            MaxHealth = 750f,
            MaxSpeedKmh = 115f,
            RoadkillSpeedKmh = 25f,
            ExplosionRadius = 6.5f,
            ExplosionDamage = 150f,
            MassKg = 6500f,
            SpringForce = 26000f,
            DamperForce = 3400f,
            SuspensionRest = 0.5f,
            WheelRadius = 0.42f,
            WheelY = 0.42f,
            DriveForce = 24000f,
            BrakeForce = 26000f,
            SteerAngle = 34f,
            LateralGrip = 5200f,
            AntiRoll = 9000f,
            WheelBaseZ = 1.45f,
            TrackX = 0.95f,
            CenterOfMass = new Vector3(0f, -0.3f, 0.1f),
            DustOffset = new Vector3(0f, 0.1f, -1.7f),
            TurretBelt = 150,
            TurretSpread = 0.7f
        };

        public static VehicleConfig For(VehicleKind kind) => kind == VehicleKind.Cobra ? Cobra : Kirpi;

        /// <summary>
        /// Konuma göre araç türü (saf mantık): harita merkezinden uzak (köy/karakol) noktalarda Cobra, merkezde Kirpi.
        /// Aynı seed ve konum her zaman aynı sonucu verir.
        /// </summary>
        public static VehicleKind PickForLocation(Vector3 position, int seed)
        {
            var flat = new Vector2(position.x, position.z).magnitude;
            var h = unchecked((int)(((long)Mathf.RoundToInt(position.x) * 73856093L)
                                    ^ ((long)Mathf.RoundToInt(position.z) * 19349663L) ^ seed));
            var roll = (h & 0x7fffffff) % 100;
            var cobraChance = flat > 250f ? 60 : 25;
            return roll < cobraChance ? VehicleKind.Cobra : VehicleKind.Kirpi;
        }
    }
}
