using System;
using System.Collections.Generic;
using Project.Core.Domain;

namespace Project.Application.Catalogs
{
    /// <summary>
    /// Tüm Türk silahlarının tanımları (WeaponIds) — denge değerleri burada.
    /// Tanımlar paylaşılan, değişmemesi gereken örneklerdir: çalışma zamanı durumu WeaponRuntimeService'te tutulur.
    /// </summary>
    public static class WeaponCatalog
    {
        /// <summary>Bilinmeyen/boş kaynak kimliği için gösterilen ad.</summary>
        public const string UnknownSourceName = "Bilinmeyen";

        private static readonly WeaponDefinitionData[] Definitions = CreateAll();
        private static readonly Dictionary<string, WeaponDefinitionData> ById = BuildIndex(Definitions);
        private static readonly Dictionary<string, string> SpecialSourceNames = new(StringComparer.Ordinal)
        {
            { DamageSourceIds.Zone, "Harekât Sınırı" },
            { DamageSourceIds.Fall, "Düşme" },
            { DamageSourceIds.FragGrenade, "El Bombası" },
            { DamageSourceIds.Fists, "Yumruk" },
            { DamageSourceIds.Artillery, "Topçu Ateşi" },
            { DamageSourceIds.Vehicle, "Araç" }
        };

        public static IReadOnlyList<WeaponDefinitionData> All => Definitions;

        /// <summary>Kimliğe göre tanım; bilinmeyen ya da boş kimlikte null döner (güvenli kontrol için TryGet).</summary>
        public static WeaponDefinitionData Get(string weaponId)
        {
            return TryGet(weaponId, out var definition) ? definition : null;
        }

        public static bool TryGet(string weaponId, out WeaponDefinitionData definition)
        {
            if (string.IsNullOrEmpty(weaponId))
            {
                definition = null;
                return false;
            }

            return ById.TryGetValue(weaponId, out definition);
        }

        /// <summary>Kimlik katalogda kayıtlı bir silah mı?</summary>
        public static bool Contains(string weaponId) => !string.IsNullOrEmpty(weaponId) && ById.ContainsKey(weaponId);

        /// <summary>Silah ya da özel hasar kaynağı (bölge, düşme, el bombası, yumruk) için Türkçe görünen ad.</summary>
        public static string GetDisplayName(string sourceId)
        {
            if (string.IsNullOrEmpty(sourceId))
                return UnknownSourceName;

            if (ById.TryGetValue(sourceId, out var definition))
                return NameProfile.Get(sourceId, string.IsNullOrEmpty(definition.DisplayName) ? sourceId : definition.DisplayName);

            if (SpecialSourceNames.TryGetValue(sourceId, out var special))
                return special;

            return sourceId;
        }

        /// <summary>Kategori için Türkçe sınıf adı (envanter/HUD).</summary>
        public static string GetCategoryName(WeaponCategory category)
        {
            switch (category)
            {
                case WeaponCategory.Pistol: return "Tabanca";
                case WeaponCategory.Smg: return "Hafif Makineli";
                case WeaponCategory.AssaultRifle: return "Piyade Tüfeği";
                case WeaponCategory.Sniper: return "Keskin Nişancı";
                case WeaponCategory.Shotgun: return "Pompalı";
                case WeaponCategory.Melee: return "Yakın Dövüş";
                case WeaponCategory.Dmr: return "Nişancı Tüfeği";
                case WeaponCategory.Lmg: return "Makineli Tüfek";
                default: return "Silah";
            }
        }

        /// <summary>Mühimmat tipi için Türkçe ad (envanter/HUD).</summary>
        public static string GetAmmoName(AmmoType ammoType)
        {
            switch (ammoType)
            {
                case AmmoType.Mm9: return "9 mm";
                case AmmoType.Mm556: return "5.56 mm";
                case AmmoType.Mm762: return "7.62 mm";
                case AmmoType.Gauge12: return "12 Kalibre";
                default: return "-";
            }
        }

        /// <summary>Saniyedeki atış aralığından dakikadaki atış (RPM) hesabı.</summary>
        public static float RoundsPerMinute(WeaponDefinitionData weapon)
        {
            if (weapon == null || weapon.FireIntervalSeconds <= 0f)
                return 0f;

            return 60f / weapon.FireIntervalSeconds;
        }

        private static float IntervalFromRpm(float rpm) => 60f / rpm;

        private static Dictionary<string, WeaponDefinitionData> BuildIndex(WeaponDefinitionData[] definitions)
        {
            var index = new Dictionary<string, WeaponDefinitionData>(definitions.Length, StringComparer.Ordinal);
            for (var i = 0; i < definitions.Length; i++)
                index[definitions[i].WeaponId] = definitions[i];
            return index;
        }

        private static WeaponDefinitionData[] CreateAll()
        {
            return new[]
            {
                // --- Tabancalar (9 mm) ---
                new WeaponDefinitionData(WeaponIds.Sar9, WeaponCategory.Pistol,
                    damage: 28f, magazineSize: 15, fireIntervalSeconds: 0.16f, reloadDurationSeconds: 1.5f,
                    range: 120f, headshotMultiplier: 2f)
                {
                    DisplayName = "SAR 9",
                    AdsTime = 0.15f,
                    RecoilRecovery = 1.2f,
                    AmmoType = AmmoType.Mm9,
                    LimbMultiplier = 0.8f,
                    MuzzleVelocity = 360f,
                    RecoilVertical = 1.1f,
                    RecoilHorizontal = 0.45f,
                    HipSpread = 2.2f,
                    AdsSpread = 0.55f,
                    BloomPerShot = 0.55f,
                    MaxBloom = 3f,
                    AdsZoom = 1.15f,
                    FireModes = new[] { FireMode.Single },
                    FalloffStart = 20f,
                    FalloffEnd = 70f,
                    MinDamageFactor = 0.55f,
                    Weight = 1f,
                    EquipSeconds = 0.35f
                },
                new WeaponDefinitionData(WeaponIds.Tp9, WeaponCategory.Pistol,
                    damage: 26f, magazineSize: 18, fireIntervalSeconds: 0.15f, reloadDurationSeconds: 1.6f,
                    range: 120f, headshotMultiplier: 2f)
                {
                    DisplayName = "Canik TP9",
                    AdsTime = 0.15f,
                    RecoilRecovery = 1.2f,
                    AmmoType = AmmoType.Mm9,
                    LimbMultiplier = 0.8f,
                    MuzzleVelocity = 365f,
                    RecoilVertical = 1.0f,
                    RecoilHorizontal = 0.4f,
                    HipSpread = 2.1f,
                    AdsSpread = 0.5f,
                    BloomPerShot = 0.5f,
                    MaxBloom = 3f,
                    AdsZoom = 1.15f,
                    FireModes = new[] { FireMode.Single },
                    FalloffStart = 20f,
                    FalloffEnd = 70f,
                    MinDamageFactor = 0.55f,
                    Weight = 0.9f,
                    EquipSeconds = 0.35f
                },

                // --- Hafif makineli (9 mm) ---
                new WeaponDefinitionData(WeaponIds.Sar109, WeaponCategory.Smg,
                    damage: 22f, magazineSize: 30, fireIntervalSeconds: IntervalFromRpm(800f), reloadDurationSeconds: 2.0f,
                    range: 180f, headshotMultiplier: 1.8f)
                {
                    DisplayName = "SAR 109T",
                    AdsTime = 0.22f,
                    RecoilRecovery = 1.3f,
                    AmmoType = AmmoType.Mm9,
                    LimbMultiplier = 0.85f,
                    MuzzleVelocity = 400f,
                    RecoilVertical = 0.45f,
                    RecoilHorizontal = 0.32f,
                    HipSpread = 2.4f,
                    AdsSpread = 0.6f,
                    BloomPerShot = 0.22f,
                    MaxBloom = 2.4f,
                    AdsZoom = 1.2f,
                    FireModes = new[] { FireMode.Single, FireMode.Burst, FireMode.Auto },
                    BurstCount = 3,
                    FalloffStart = 25f,
                    FalloffEnd = 100f,
                    MinDamageFactor = 0.5f,
                    Weight = 3f,
                    EquipSeconds = 0.45f
                },

                // --- Piyade tüfekleri ---
                new WeaponDefinitionData(WeaponIds.Mpt55, WeaponCategory.AssaultRifle,
                    damage: 26f, magazineSize: 30, fireIntervalSeconds: IntervalFromRpm(750f), reloadDurationSeconds: 2.3f,
                    range: 450f, headshotMultiplier: 2f)
                {
                    DisplayName = "MPT-55",
                    AdsTime = 0.23f,
                    RecoilRecovery = 1.0f,
                    AmmoType = AmmoType.Mm556,
                    LimbMultiplier = 0.8f,
                    MuzzleVelocity = 880f,
                    RecoilVertical = 0.55f,
                    RecoilHorizontal = 0.3f,
                    HipSpread = 2.8f,
                    AdsSpread = 0.35f,
                    BloomPerShot = 0.25f,
                    MaxBloom = 2.6f,
                    AdsZoom = 1.35f,
                    FireModes = new[] { FireMode.Single, FireMode.Burst, FireMode.Auto },
                    BurstCount = 3,
                    FalloffStart = 80f,
                    FalloffEnd = 300f,
                    MinDamageFactor = 0.65f,
                    Weight = 3.4f,
                    EquipSeconds = 0.55f
                },
                new WeaponDefinitionData(WeaponIds.Mpt76, WeaponCategory.AssaultRifle,
                    damage: 36f, magazineSize: 20, fireIntervalSeconds: IntervalFromRpm(600f), reloadDurationSeconds: 2.5f,
                    range: 600f, headshotMultiplier: 2f)
                {
                    DisplayName = "MPT-76",
                    AdsTime = 0.25f,
                    RecoilRecovery = 1.0f,
                    AmmoType = AmmoType.Mm762,
                    LimbMultiplier = 0.8f,
                    MuzzleVelocity = 820f,
                    RecoilVertical = 0.85f,
                    RecoilHorizontal = 0.4f,
                    HipSpread = 3.0f,
                    AdsSpread = 0.3f,
                    BloomPerShot = 0.32f,
                    MaxBloom = 3f,
                    AdsZoom = 1.35f,
                    FireModes = new[] { FireMode.Single, FireMode.Auto },
                    FalloffStart = 100f,
                    FalloffEnd = 400f,
                    MinDamageFactor = 0.7f,
                    Weight = 4.1f,
                    EquipSeconds = 0.6f
                },
                new WeaponDefinitionData(WeaponIds.G3, WeaponCategory.AssaultRifle,
                    damage: 38f, magazineSize: 20, fireIntervalSeconds: IntervalFromRpm(550f), reloadDurationSeconds: 2.6f,
                    range: 600f, headshotMultiplier: 2f)
                {
                    DisplayName = "G3A7",
                    AdsTime = 0.26f,
                    RecoilRecovery = 0.8f,
                    AmmoType = AmmoType.Mm762,
                    LimbMultiplier = 0.8f,
                    MuzzleVelocity = 800f,
                    RecoilVertical = 0.95f,
                    RecoilHorizontal = 0.45f,
                    HipSpread = 3.2f,
                    AdsSpread = 0.32f,
                    BloomPerShot = 0.36f,
                    MaxBloom = 3.2f,
                    AdsZoom = 1.35f,
                    FireModes = new[] { FireMode.Single, FireMode.Auto },
                    FalloffStart = 110f,
                    FalloffEnd = 400f,
                    MinDamageFactor = 0.7f,
                    Weight = 4.4f,
                    EquipSeconds = 0.65f
                },

                // --- Nişancı tüfeği (DMR, 3x) ---
                new WeaponDefinitionData(WeaponIds.Knt76, WeaponCategory.Dmr,
                    damage: 52f, magazineSize: 10, fireIntervalSeconds: 0.34f, reloadDurationSeconds: 2.8f,
                    range: 800f, headshotMultiplier: 2.2f)
                {
                    DisplayName = "KNT-76",
                    AdsTime = 0.32f,
                    RecoilRecovery = 0.9f,
                    AmmoType = AmmoType.Mm762,
                    LimbMultiplier = 0.8f,
                    MuzzleVelocity = 850f,
                    RecoilVertical = 1.5f,
                    RecoilHorizontal = 0.35f,
                    HipSpread = 4.0f,
                    AdsSpread = 0.12f,
                    BloomPerShot = 0.6f,
                    MaxBloom = 3f,
                    AdsZoom = 3f,
                    HasScope = true,
                    FireModes = new[] { FireMode.Single },
                    FalloffStart = 160f,
                    FalloffEnd = 600f,
                    MinDamageFactor = 0.75f,
                    Weight = 5.2f,
                    EquipSeconds = 0.75f
                },

                // --- Keskin nişancı (sürgülü, 6x) ---
                new WeaponDefinitionData(WeaponIds.Jng90, WeaponCategory.Sniper,
                    damage: 90f, magazineSize: 5, fireIntervalSeconds: 1.4f, reloadDurationSeconds: 3.4f,
                    range: 1000f, headshotMultiplier: 2.5f)
                {
                    DisplayName = "JNG-90",
                    AdsTime = 0.37f,
                    RecoilRecovery = 0.6f,
                    AmmoType = AmmoType.Mm762,
                    LimbMultiplier = 0.75f,
                    MuzzleVelocity = 915f,
                    RecoilVertical = 2.6f,
                    RecoilHorizontal = 0.3f,
                    HipSpread = 6f,
                    AdsSpread = 0.03f,
                    BloomPerShot = 1.5f,
                    MaxBloom = 3f,
                    AdsZoom = 6f,
                    HasScope = true,
                    IsBoltAction = true,
                    FireModes = new[] { FireMode.Single },
                    FalloffStart = 250f,
                    FalloffEnd = 800f,
                    MinDamageFactor = 0.8f,
                    Weight = 6.5f,
                    EquipSeconds = 0.9f
                },

                // --- Makineli tüfek ---
                new WeaponDefinitionData(WeaponIds.Pmt76, WeaponCategory.Lmg,
                    damage: 34f, magazineSize: 100, fireIntervalSeconds: IntervalFromRpm(650f), reloadDurationSeconds: 6f,
                    range: 600f, headshotMultiplier: 1.8f)
                {
                    DisplayName = "PMT-76",
                    AdsTime = 0.47f,
                    RecoilRecovery = 0.7f,
                    AmmoType = AmmoType.Mm762,
                    LimbMultiplier = 0.8f,
                    MuzzleVelocity = 830f,
                    RecoilVertical = 0.7f,
                    RecoilHorizontal = 0.55f,
                    HipSpread = 4.2f,
                    AdsSpread = 0.6f,
                    BloomPerShot = 0.28f,
                    MaxBloom = 3.6f,
                    AdsZoom = 1.3f,
                    FireModes = new[] { FireMode.Auto },
                    FalloffStart = 80f,
                    FalloffEnd = 400f,
                    MinDamageFactor = 0.7f,
                    Weight = 11f,
                    EquipSeconds = 1.1f
                },

                // --- Pompalı (12 kalibre, 9 saçma) ---
                new WeaponDefinitionData(WeaponIds.Escort, WeaponCategory.Shotgun,
                    damage: 20f, magazineSize: 7, fireIntervalSeconds: 0.85f, reloadDurationSeconds: 4.2f,
                    range: 70f, headshotMultiplier: 1.5f)
                {
                    DisplayName = "Escort",
                    AdsTime = 0.23f,
                    RecoilRecovery = 0.8f,
                    AmmoType = AmmoType.Gauge12,
                    LimbMultiplier = 0.85f,
                    MuzzleVelocity = 400f,
                    RecoilVertical = 2.4f,
                    RecoilHorizontal = 0.6f,
                    HipSpread = 1.5f,
                    AdsSpread = 1.0f,
                    BloomPerShot = 0.8f,
                    MaxBloom = 2.5f,
                    PelletCount = 9,
                    PelletSpread = 4.5f,
                    AdsZoom = 1.15f,
                    FireModes = new[] { FireMode.Single },
                    FalloffStart = 6f,
                    FalloffEnd = 30f,
                    MinDamageFactor = 0.12f,
                    Weight = 3.6f,
                    EquipSeconds = 0.6f
                },

                // --- silahlar2 ---
                new WeaponDefinitionData(WeaponIds.Sar223, WeaponCategory.AssaultRifle,
                    damage: 24f, magazineSize: 30, fireIntervalSeconds: IntervalFromRpm(850f), reloadDurationSeconds: 2.2f,
                    range: 420f, headshotMultiplier: 2f)
                {
                    DisplayName = "SAR 223",
                    AdsTime = 0.22f,
                    RecoilRecovery = 1.05f,
                    AmmoType = AmmoType.Mm556,
                    LimbMultiplier = 0.8f,
                    MuzzleVelocity = 930f,
                    RecoilVertical = 0.5f,
                    RecoilHorizontal = 0.3f,
                    HipSpread = 2.8f,
                    AdsSpread = 0.36f,
                    BloomPerShot = 0.24f,
                    MaxBloom = 2.6f,
                    AdsZoom = 1.35f,
                    FireModes = new[] { FireMode.Single, FireMode.Auto },
                    FalloffStart = 70f,
                    FalloffEnd = 290f,
                    MinDamageFactor = 0.65f,
                    Weight = 3.3f,
                    EquipSeconds = 0.5f
                },
                new WeaponDefinitionData(WeaponIds.Mpt76K, WeaponCategory.AssaultRifle,
                    damage: 34f, magazineSize: 20, fireIntervalSeconds: IntervalFromRpm(700f), reloadDurationSeconds: 2.2f,
                    range: 380f, headshotMultiplier: 2f)
                {
                    DisplayName = "MPT-76K",
                    AdsTime = 0.23f,
                    RecoilRecovery = 0.95f,
                    AmmoType = AmmoType.Mm762,
                    LimbMultiplier = 0.8f,
                    MuzzleVelocity = 740f,
                    RecoilVertical = 0.95f,
                    RecoilHorizontal = 0.45f,
                    HipSpread = 2.7f,
                    AdsSpread = 0.38f,
                    BloomPerShot = 0.34f,
                    MaxBloom = 3.2f,
                    AdsZoom = 1.3f,
                    FireModes = new[] { FireMode.Single, FireMode.Auto },
                    FalloffStart = 60f,
                    FalloffEnd = 260f,
                    MinDamageFactor = 0.6f,
                    Weight = 3.5f,
                    EquipSeconds = 0.48f
                },
                new WeaponDefinitionData(WeaponIds.Mete, WeaponCategory.Pistol,
                    damage: 27f, magazineSize: 17, fireIntervalSeconds: 0.14f, reloadDurationSeconds: 1.5f,
                    range: 125f, headshotMultiplier: 2f)
                {
                    DisplayName = "Canik METE SFT",
                    AdsTime = 0.15f,
                    RecoilRecovery = 1.25f,
                    AmmoType = AmmoType.Mm9,
                    LimbMultiplier = 0.8f,
                    MuzzleVelocity = 370f,
                    RecoilVertical = 1.0f,
                    RecoilHorizontal = 0.4f,
                    HipSpread = 2.1f,
                    AdsSpread = 0.5f,
                    BloomPerShot = 0.5f,
                    MaxBloom = 3f,
                    AdsZoom = 1.15f,
                    FireModes = new[] { FireMode.Single },
                    FalloffStart = 22f,
                    FalloffEnd = 72f,
                    MinDamageFactor = 0.55f,
                    Weight = 0.95f,
                    EquipSeconds = 0.33f
                },
                new WeaponDefinitionData(WeaponIds.Sar762Mt, WeaponCategory.Dmr,
                    damage: 46f, magazineSize: 15, fireIntervalSeconds: 0.22f, reloadDurationSeconds: 2.7f,
                    range: 750f, headshotMultiplier: 2.2f)
                {
                    DisplayName = "SAR 762 MT",
                    AdsTime = 0.32f,
                    RecoilRecovery = 0.9f,
                    AmmoType = AmmoType.Mm762,
                    LimbMultiplier = 0.8f,
                    MuzzleVelocity = 840f,
                    RecoilVertical = 1.3f,
                    RecoilHorizontal = 0.35f,
                    HipSpread = 4.0f,
                    AdsSpread = 0.14f,
                    BloomPerShot = 0.55f,
                    MaxBloom = 3f,
                    AdsZoom = 2.5f,
                    HasScope = true,
                    FireModes = new[] { FireMode.Single },
                    FalloffStart = 140f,
                    FalloffEnd = 560f,
                    MinDamageFactor = 0.75f,
                    Weight = 5f,
                    EquipSeconds = 0.72f
                },
                new WeaponDefinitionData(WeaponIds.Mg3, WeaponCategory.Lmg,
                    damage: 21f, magazineSize: 120, fireIntervalSeconds: IntervalFromRpm(1100f), reloadDurationSeconds: 7.5f,
                    range: 550f, headshotMultiplier: 1.7f)
                {
                    DisplayName = "MG3",
                    AdsTime = 0.49f,
                    RecoilRecovery = 0.65f,
                    AmmoType = AmmoType.Mm762,
                    LimbMultiplier = 0.8f,
                    MuzzleVelocity = 820f,
                    RecoilVertical = 0.6f,
                    RecoilHorizontal = 0.6f,
                    HipSpread = 5f,
                    AdsSpread = 0.7f,
                    BloomPerShot = 0.22f,
                    MaxBloom = 4f,
                    AdsZoom = 1.3f,
                    FireModes = new[] { FireMode.Auto },
                    FalloffStart = 80f,
                    FalloffEnd = 360f,
                    MinDamageFactor = 0.65f,
                    Weight = 11.5f,
                    EquipSeconds = 1.2f
                },
                new WeaponDefinitionData(WeaponIds.EscortMagnum, WeaponCategory.Shotgun,
                    damage: 11f, magazineSize: 6, fireIntervalSeconds: 0.45f, reloadDurationSeconds: 4.5f,
                    range: 65f, headshotMultiplier: 1.5f)
                {
                    DisplayName = "Escort Magnum",
                    AdsTime = 0.24f,
                    RecoilRecovery = 0.8f,
                    AmmoType = AmmoType.Gauge12,
                    LimbMultiplier = 0.85f,
                    MuzzleVelocity = 400f,
                    RecoilVertical = 2.6f,
                    RecoilHorizontal = 0.7f,
                    HipSpread = 1.6f,
                    AdsSpread = 1.1f,
                    BloomPerShot = 0.9f,
                    MaxBloom = 2.8f,
                    PelletCount = 8,
                    PelletSpread = 5.2f,
                    AdsZoom = 1.15f,
                    FireModes = new[] { FireMode.Single },
                    FalloffStart = 6f,
                    FalloffEnd = 28f,
                    MinDamageFactor = 0.12f,
                    Weight = 3.9f,
                    EquipSeconds = 0.6f
                }
            };
        }
    }
}
