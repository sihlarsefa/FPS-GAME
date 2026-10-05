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
                return string.IsNullOrEmpty(definition.DisplayName) ? sourceId : definition.DisplayName;

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
                    FalloffStart = 70f,
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
                    FalloffStart = 90f,
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
                    FalloffStart = 90f,
                    FalloffEnd = 400f,
                    MinDamageFactor = 0.7f,
                    Weight = 4.4f,
                    EquipSeconds = 0.65f
                },

                // --- Nişancı tüfeği (DMR, 3x) ---
                new WeaponDefinitionData(WeaponIds.Knt76, WeaponCategory.Dmr,
                    damage: 52f, magazineSize: 10, fireIntervalSeconds: 0.28f, reloadDurationSeconds: 2.8f,
                    range: 800f, headshotMultiplier: 2.2f)
                {
                    DisplayName = "KNT-76",
                    AmmoType = AmmoType.Mm762,
                    LimbMultiplier = 0.8f,
                    MuzzleVelocity = 840f,
                    RecoilVertical = 1.5f,
                    RecoilHorizontal = 0.35f,
                    HipSpread = 4.0f,
                    AdsSpread = 0.12f,
                    BloomPerShot = 0.6f,
                    MaxBloom = 3f,
                    AdsZoom = 3f,
                    HasScope = true,
                    FireModes = new[] { FireMode.Single },
                    FalloffStart = 150f,
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
                    AmmoType = AmmoType.Mm762,
                    LimbMultiplier = 0.75f,
                    MuzzleVelocity = 850f,
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
                    FalloffStart = 200f,
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
                    FalloffStart = 90f,
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
                    FalloffStart = 8f,
                    FalloffEnd = 40f,
                    MinDamageFactor = 0.2f,
                    Weight = 3.6f,
                    EquipSeconds = 0.6f
                }
            };
        }
    }
}
