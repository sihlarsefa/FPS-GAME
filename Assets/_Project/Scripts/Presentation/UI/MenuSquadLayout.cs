using UnityEngine;

namespace Project.Presentation.UI
{
    /// <summary>Lobi kadrajındaki asker pozları (referans: ortada bordo bereli komutan, çevresinde savaştan çıkmış 5 asker).</summary>
    public enum SquadPose
    {
        Commander,
        SeatedOnCrate,
        LeaningOnVehicle,
        CheckingRifle,
        Sentry,
        Kneeling
    }

    /// <summary>Tek asker yuvası: konum/yön/poz + yıpranma seviyeleri (saf veri).</summary>
    public struct SquadSlot
    {
        public string Name;
        public SquadPose Pose;
        public Vector3 Position;
        public float Yaw;
        public float AimPitch;
        public float PitchAmplitude;
        public bool Seated;
        public bool Crouching;
        public bool Beret;
        /// <summary>SoldierModel.SetWear: kir/kan/barut isi (0..1).</summary>
        public float Wear;
        /// <summary>SoldierModel.SetWeary: yorgun duruş (0..1).</summary>
        public float Weary;
    }

    /// <summary>
    /// Komutan merkezli kompozisyon yerleşimi. Kamera yerel +Z'ye bakar; komutan kameraya döner (yaw 180), diğerleri
    /// komutanın iki yanında ve gerisinde yay oluşturur. Sahnesiz test edilebilir saf matematik.
    /// </summary>
    public static class MenuSquadLayout
    {
        /// <summary>Kameraya dönük yön (komutan).</summary>
        public const float FacingCameraYaw = 180f;

        /// <summary>Komutanın dışındaki en düşük yıpranma (yüzler kanlı/barut isli kalsın).</summary>
        public const float MinSquadWear = 0.8f;

        public static SquadSlot[] Build(Vector3 commander, Vector3 vehiclePos, float vehicleYaw)
        {
            var slots = new SquadSlot[6];
            slots[0] = new SquadSlot
            {
                Name = "TimKomutanı", Pose = SquadPose.Commander, Position = commander, Yaw = FacingCameraYaw,
                AimPitch = 6f, PitchAmplitude = 1.5f, Beret = true, Wear = 1f, Weary = 0.25f   // Güçlü duruş: az yorgun, çok yıpranmış.
            };
            slots[1] = new SquadSlot
            {
                Name = "SandıktaOturan", Pose = SquadPose.SeatedOnCrate, Position = commander + new Vector3(-1.9f, 0f, 0.6f), Yaw = 152f,
                AimPitch = 14f, PitchAmplitude = 3f, Seated = true, Wear = 0.95f, Weary = 0.9f
            };
            slots[2] = new SquadSlot
            {
                Name = "DizÇöken", Pose = SquadPose.Kneeling, Position = commander + new Vector3(1.25f, 0f, -0.9f), Yaw = 205f,
                AimPitch = 10f, PitchAmplitude = 2.5f, Crouching = true, Wear = 1f, Weary = 0.7f
            };
            slots[3] = new SquadSlot
            {
                Name = "TüfekKontrol", Pose = SquadPose.CheckingRifle, Position = commander + new Vector3(2.1f, 0f, 0.7f), Yaw = 216f,
                AimPitch = 42f, PitchAmplitude = 2f, Wear = 0.9f, Weary = 0.6f
            };
            slots[4] = new SquadSlot
            {
                Name = "Nöbetçi", Pose = SquadPose.Sentry, Position = commander + new Vector3(-3.4f, 0f, 2.2f), Yaw = 8f,
                AimPitch = 0f, PitchAmplitude = 3f, Wear = 0.85f, Weary = 0.4f
            };
            slots[5] = LeaningSlot(commander, vehiclePos, vehicleYaw);
            return slots;
        }

        /// <summary>Kirpi'nin komutana bakan yan tarafında, gövdeye yaslanan asker.</summary>
        public static SquadSlot LeaningSlot(Vector3 commander, Vector3 vehiclePos, float vehicleYaw)
        {
            var right = Quaternion.Euler(0f, vehicleYaw, 0f) * Vector3.right;
            var toCommander = commander - vehiclePos;
            toCommander.y = 0f;
            var side = Vector3.Dot(right, toCommander) >= 0f ? 1f : -1f;
            var pos = vehiclePos + right * (1.55f * side) + Quaternion.Euler(0f, vehicleYaw, 0f) * new Vector3(0f, 0f, -0.4f);
            pos.y = 0f;
            return new SquadSlot
            {
                Name = "KirpiyeYaslanan", Pose = SquadPose.LeaningOnVehicle, Position = pos,
                Yaw = MenuSceneMath.YawToward(pos, commander) + 20f,
                AimPitch = 18f, PitchAmplitude = 1.5f, Wear = 0.9f, Weary = 0.8f
            };
        }
    }
}
