namespace Project.Core.Domain
{
    /// <summary>
    /// Silahın değişmeyen balistik/oynanış tanımı. Çalışma zamanı durumu (şarjör, mod, sekme) WeaponRuntimeService'tedir.
    /// </summary>
    public sealed class WeaponDefinitionData
    {
        public string WeaponId { get; }
        public WeaponCategory Category { get; }
        public float Damage { get; }
        public int MagazineSize { get; }
        public float FireIntervalSeconds { get; }
        public float ReloadDurationSeconds { get; }
        public float Range { get; }
        public float HeadshotMultiplier { get; }

        public string DisplayName { get; set; }
        public AmmoType AmmoType { get; set; }
        public float LimbMultiplier { get; set; } = 0.8f;
        public float MuzzleVelocity { get; set; } = 700f;

        /// <summary>Atış başına dikey sekme (derece).</summary>
        public float RecoilVertical { get; set; } = 0.6f;

        /// <summary>Atış başına rastgele yatay sekme (± derece).</summary>
        public float RecoilHorizontal { get; set; } = 0.35f;

        /// <summary>Kalçadan atışta koni yarı açısı (derece).</summary>
        public float HipSpread { get; set; } = 3f;

        /// <summary>Nişan alırken koni yarı açısı (derece).</summary>
        public float AdsSpread { get; set; } = 0.4f;

        public float BloomPerShot { get; set; } = 0.35f;
        public float MaxBloom { get; set; } = 3f;
        public int PelletCount { get; set; } = 1;
        public float PelletSpread { get; set; }

        /// <summary>Nişan büyütmesi (1 = yok, 4 = 4x dürbün).</summary>
        public float AdsZoom { get; set; } = 1.3f;

        public bool HasScope { get; set; }
        public FireMode[] FireModes { get; set; } = { FireMode.Single };
        public int BurstCount { get; set; } = 3;
        public float FalloffStart { get; set; } = 60f;
        public float FalloffEnd { get; set; } = 250f;
        public float MinDamageFactor { get; set; } = 0.6f;
        public float Weight { get; set; } = 4f;
        public float EquipSeconds { get; set; } = 0.6f;
        public bool IsBoltAction { get; set; }

        public WeaponDefinitionData(
            string weaponId,
            WeaponCategory category,
            float damage,
            int magazineSize,
            float fireIntervalSeconds,
            float reloadDurationSeconds,
            float range,
            float headshotMultiplier = 2f)
        {
            WeaponId = weaponId;
            Category = category;
            Damage = damage;
            MagazineSize = magazineSize;
            FireIntervalSeconds = fireIntervalSeconds;
            ReloadDurationSeconds = reloadDurationSeconds;
            Range = range;
            HeadshotMultiplier = headshotMultiplier;
            DisplayName = weaponId;
        }

        public bool IsSidearm => Category == WeaponCategory.Pistol;

        public bool SupportsFireMode(FireMode mode)
        {
            for (var i = 0; i < FireModes.Length; i++)
            {
                if (FireModes[i] == mode)
                    return true;
            }

            return false;
        }

        public FireMode DefaultFireMode => SupportsFireMode(FireMode.Auto) ? FireMode.Auto : FireModes[0];

        public static WeaponDefinitionData AssaultRifle => new(
            "ar_mk1", WeaponCategory.AssaultRifle, 28f, 30, 0.1f, 2.2f, 200f)
        {
            DisplayName = "AR Mk1",
            AmmoType = AmmoType.Mm556,
            FireModes = new[] { FireMode.Single, FireMode.Auto }
        };

        public static WeaponDefinitionData Pistol => new(
            "pistol_mk1", WeaponCategory.Pistol, 35f, 12, 0.25f, 1.5f, 80f)
        {
            DisplayName = "Pistol Mk1",
            AmmoType = AmmoType.Mm9
        };
    }
}
