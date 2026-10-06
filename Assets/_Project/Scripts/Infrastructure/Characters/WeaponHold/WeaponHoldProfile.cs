using Project.Core.Domain;

namespace Project.Infrastructure.Characters.WeaponHold
{
    /// <summary>
    /// Silah sınıfı başına üçüncü şahıs tutuş ayarları (saf veri). Ağır silahlar nişan eğimini gövdeye daha az dağıtır,
    /// yayı yavaş izler ve geri tepmeyi omuzda daha az gösterir; hafif silahlar çevik hareket eder (MWII "ThirdPersonWeaponParams"
    /// mantığı: spineBendMultiplier, hipLeanMultiplier, recoilKick sınıf başına).
    /// </summary>
    public readonly struct WeaponHoldProfile
    {
        /// <summary>Nişan eğiminin belden (Spine) üstlenilen payı (0..1).</summary>
        public readonly float SpineShare;
        /// <summary>Nişan eğiminin göğüs (Chest) payı.</summary>
        public readonly float ChestShare;
        /// <summary>Başın nişan eğimini izleme payı.</summary>
        public readonly float HeadShare;
        /// <summary>Kalça, nişan eğiminin ters yönünde bu oranda eğilir (ağırlık dengesi).</summary>
        public readonly float HipCounter;
        /// <summary>Koşuda namlunun aşağı indiği derece (yüksek port).</summary>
        public readonly float SprintPitch;
        /// <summary>Koşuda namlunun gövde dışına çevrildiği derece.</summary>
        public readonly float SprintYaw;
        /// <summary>Alçak hazırda namlunun aşağı indiği derece.</summary>
        public readonly float LowReadyPitch;
        /// <summary>Alçak hazırda silahın gövdeye yaklaştığı mesafe (m, geri).</summary>
        public readonly float LowReadyBack;
        /// <summary>Alçak hazırda silahın indiği mesafe (m, aşağı).</summary>
        public readonly float LowReadyDown;
        /// <summary>Atışta kolların yukarı sekme derecesi.</summary>
        public readonly float RecoilArms;
        /// <summary>Atışta belin geriye yaslanma derecesi.</summary>
        public readonly float RecoilSpine;
        /// <summary>Atışta silahın omuza geri geldiği mesafe (m).</summary>
        public readonly float RecoilBack;
        /// <summary>Nişan yayı doğal frekansı (Hz); düşük = ağır, geriden izleyen silah.</summary>
        public readonly float SpringHz;
        /// <summary>Yay sönümü (1 = kritik; altı hafif aşım yapar).</summary>
        public readonly float SpringDamping;
        /// <summary>Yaklaşık silah uzunluğu (m); duvar tespiti için.</summary>
        public readonly float Length;
        /// <summary>Duvara yaslanınca namlunun yukarı kalktığı derece (Sandstorm tarzı geri çekme).</summary>
        public readonly float WallPitch;
        /// <summary>Duvara yaslanınca silahın gövdeye çekildiği mesafe (m).</summary>
        public readonly float WallBack;
        /// <summary>Alçak hazıra geçmeden önce hareketsiz kalma süresi (sn).</summary>
        public readonly float IdleToLowReady;

        public WeaponHoldProfile(float spineShare, float chestShare, float headShare, float hipCounter, float sprintPitch,
            float sprintYaw, float lowReadyPitch, float lowReadyBack, float lowReadyDown, float recoilArms, float recoilSpine,
            float recoilBack, float springHz, float springDamping, float length, float wallPitch, float wallBack, float idleToLowReady)
        {
            SpineShare = spineShare;
            ChestShare = chestShare;
            HeadShare = headShare;
            HipCounter = hipCounter;
            SprintPitch = sprintPitch;
            SprintYaw = sprintYaw;
            LowReadyPitch = lowReadyPitch;
            LowReadyBack = lowReadyBack;
            LowReadyDown = lowReadyDown;
            RecoilArms = recoilArms;
            RecoilSpine = recoilSpine;
            RecoilBack = recoilBack;
            SpringHz = springHz;
            SpringDamping = springDamping;
            Length = length;
            WallPitch = wallPitch;
            WallBack = wallBack;
            IdleToLowReady = idleToLowReady;
        }

        /// <summary>Mevcut davranışa yakın varsayılan (tüfek).</summary>
        public static WeaponHoldProfile Default => ForCategory(WeaponCategory.AssaultRifle);

        public static WeaponHoldProfile ForCategory(WeaponCategory category)
        {
            switch (category)
            {
                case WeaponCategory.Pistol:
                    return new WeaponHoldProfile(0.22f, 0.22f, 0.90f, 0.04f, 16f, 8f, 20f, 0.02f, 0.03f, 5f, 1.2f, 0.02f, 9f, 0.75f, 0.22f, 10f, 0.05f, 7f);
                case WeaponCategory.Smg:
                    return new WeaponHoldProfile(0.26f, 0.26f, 0.90f, 0.05f, 28f, 22f, 20f, 0.03f, 0.04f, 5.5f, 1.4f, 0.025f, 8f, 0.70f, 0.50f, 14f, 0.06f, 5.5f);
                case WeaponCategory.Shotgun:
                    return new WeaponHoldProfile(0.24f, 0.24f, 0.88f, 0.06f, 30f, 24f, 22f, 0.035f, 0.045f, 7f, 2.0f, 0.04f, 6.5f, 0.80f, 0.85f, 16f, 0.08f, 5f);
                case WeaponCategory.Dmr:
                    return new WeaponHoldProfile(0.22f, 0.22f, 0.88f, 0.07f, 30f, 25f, 24f, 0.04f, 0.05f, 6f, 1.6f, 0.035f, 5.5f, 0.85f, 1.05f, 18f, 0.09f, 4.5f);
                case WeaponCategory.Sniper:
                    return new WeaponHoldProfile(0.20f, 0.20f, 0.86f, 0.08f, 32f, 26f, 26f, 0.045f, 0.055f, 7f, 1.8f, 0.045f, 4.5f, 0.90f, 1.20f, 20f, 0.10f, 4f);
                case WeaponCategory.Lmg:
                    return new WeaponHoldProfile(0.18f, 0.20f, 0.86f, 0.09f, 34f, 28f, 28f, 0.05f, 0.06f, 4.5f, 1.2f, 0.03f, 4f, 0.95f, 1.00f, 20f, 0.10f, 4f);
                default:
                    return new WeaponHoldProfile(0.27f, 0.27f, 0.90f, 0.06f, 30f, 25f, 22f, 0.035f, 0.045f, 6f, 1.5f, 0.03f, 7f, 0.75f, 0.85f, 16f, 0.08f, 5.5f);
            }
        }
    }
}
