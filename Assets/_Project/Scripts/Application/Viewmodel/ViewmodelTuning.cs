using System;

namespace Project.Application.Viewmodel
{
    /// <summary>Silah sınıfı: viewmodel hareket tablolarının anahtarı.</summary>
    public enum ViewmodelWeaponClass { Pistol = 0, Smg, Rifle, Lmg, Sniper, Shotgun }

    /// <summary>Sınıfa özel tepme değerleri (m ve derece; model tarafı).</summary>
    public struct RecoilTuning
    {
        /// <summary>Geri vuruş tepe değeri (m, namlu yönünün tersine).</summary>
        public float KickbackM;
        /// <summary>Namlu kalkışı tepe değeri (derece, yukarı +).</summary>
        public float RiseDeg;
        /// <summary>Atış başına rastgele yan sapma genliği (derece).</summary>
        public float SideDeg;
        /// <summary>Atış başına roll genliği (derece).</summary>
        public float RollDeg;
        /// <summary>Toplam kalkışın kameraya giden payı (0..1); kalanı modelde kalır.</summary>
        public float CameraShare;
        /// <summary>Model yayı: sertlik (rad/s)^2 ve sönüm oranı (0..1 salınımlı).</summary>
        public float Stiffness;
        public float DampingRatio;
    }

    /// <summary>Sınıfa özel hareket tabloları (CoD MW2019 / Battlefield / Tarkov'dan esinlenmiş gerçekçi aralıklar).</summary>
    public static class ViewmodelTuning
    {
        /// <summary>ADS süresi (sn): tabanca en hızlı, keskin nişancı/LMG en yavaş.</summary>
        public static float AdsSeconds(ViewmodelWeaponClass c)
        {
            switch (c)
            {
                case ViewmodelWeaponClass.Pistol: return 0.15f;
                case ViewmodelWeaponClass.Smg: return 0.18f;
                case ViewmodelWeaponClass.Shotgun: return 0.24f;
                case ViewmodelWeaponClass.Lmg: return 0.34f;
                case ViewmodelWeaponClass.Sniper: return 0.38f;
                default: return 0.22f;
            }
        }

        public static RecoilTuning Recoil(ViewmodelWeaponClass c)
        {
            switch (c)
            {
                case ViewmodelWeaponClass.Pistol:
                    return new RecoilTuning { KickbackM = 0.022f, RiseDeg = 3.2f, SideDeg = 0.5f, RollDeg = 0.8f, CameraShare = 0.45f, Stiffness = 1600f, DampingRatio = 0.5f };
                case ViewmodelWeaponClass.Smg:
                    return new RecoilTuning { KickbackM = 0.016f, RiseDeg = 1.7f, SideDeg = 0.45f, RollDeg = 0.6f, CameraShare = 0.5f, Stiffness = 2200f, DampingRatio = 0.55f };
                case ViewmodelWeaponClass.Lmg:
                    return new RecoilTuning { KickbackM = 0.026f, RiseDeg = 1.5f, SideDeg = 0.4f, RollDeg = 0.7f, CameraShare = 0.4f, Stiffness = 1100f, DampingRatio = 0.65f };
                case ViewmodelWeaponClass.Sniper:
                    return new RecoilTuning { KickbackM = 0.07f, RiseDeg = 6.5f, SideDeg = 0.8f, RollDeg = 1.6f, CameraShare = 0.6f, Stiffness = 700f, DampingRatio = 0.6f };
                case ViewmodelWeaponClass.Shotgun:
                    return new RecoilTuning { KickbackM = 0.085f, RiseDeg = 7.5f, SideDeg = 1.0f, RollDeg = 2.0f, CameraShare = 0.55f, Stiffness = 650f, DampingRatio = 0.55f };
                default:
                    return new RecoilTuning { KickbackM = 0.02f, RiseDeg = 1.9f, SideDeg = 0.5f, RollDeg = 0.6f, CameraShare = 0.5f, Stiffness = 1800f, DampingRatio = 0.55f };
            }
        }

        /// <summary>Koşu pozu (konum m: x sağ, y yukarı, z ileri; açı derece). Silah göğüs önünde yatık.</summary>
        public static void SprintPose(ViewmodelWeaponClass c, out float x, out float y, out float z, out float pitch, out float yaw, out float roll)
        {
            switch (c)
            {
                case ViewmodelWeaponClass.Pistol: x = 0.02f; y = -0.05f; z = -0.03f; pitch = 22f; yaw = 8f; roll = -6f; break;
                case ViewmodelWeaponClass.Lmg: x = 0.05f; y = -0.09f; z = -0.05f; pitch = 34f; yaw = 20f; roll = -14f; break;
                case ViewmodelWeaponClass.Sniper: x = 0.05f; y = -0.08f; z = -0.05f; pitch = 32f; yaw = 18f; roll = -12f; break;
                default: x = 0.04f; y = -0.07f; z = -0.04f; pitch = 28f; yaw = 16f; roll = -10f; break;
            }
        }

        /// <summary>Koşu poza giriş/çıkış süresi (sn): CoD'da giriş ~0.22, çıkış ~0.16; ağır silah yavaş.</summary>
        public static float SprintInSeconds(ViewmodelWeaponClass c) => 0.22f * Weight(c);
        public static float SprintOutSeconds(ViewmodelWeaponClass c) => 0.16f * Weight(c);

        /// <summary>Ağırlık çarpanı (süreleri ölçekler).</summary>
        public static float Weight(ViewmodelWeaponClass c)
        {
            switch (c)
            {
                case ViewmodelWeaponClass.Pistol: return 0.8f;
                case ViewmodelWeaponClass.Smg: return 0.9f;
                case ViewmodelWeaponClass.Lmg: return 1.4f;
                case ViewmodelWeaponClass.Sniper: return 1.3f;
                case ViewmodelWeaponClass.Shotgun: return 1.1f;
                default: return 1f;
            }
        }

        /// <summary>Adım uzunluğu (m / tam bob döngüsü = iki adım): duruş ve koşuya göre.</summary>
        public static float StrideLength(ViewmodelStanceKind stance, bool sprinting)
        {
            switch (stance)
            {
                case ViewmodelStanceKind.Prone: return 0.9f;
                case ViewmodelStanceKind.Crouch: return 1.3f;
                case ViewmodelStanceKind.Slide: return 3f;
                default: return sprinting ? 2.9f : 1.9f;
            }
        }
    }

    /// <summary>ViewmodelStance ile bire bir (Services bağımlılığı olmadan çağıranın dönüştürmesi için).</summary>
    public enum ViewmodelStanceKind { Stand = 0, Crouch = 1, Prone = 2, Slide = 3 }
}
