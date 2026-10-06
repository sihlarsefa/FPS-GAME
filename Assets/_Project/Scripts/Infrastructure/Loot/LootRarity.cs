using Project.Application.Catalogs;
using Project.Core.Domain;
using UnityEngine;

namespace Project.Infrastructure.Loot
{
    /// <summary>Yağma nadirliği (PUBG tarzı renk kodu): beyaz → yeşil → mavi → mor → altın.</summary>
    public enum LootRarity
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        Epic = 3,
        Legendary = 4
    }

    /// <summary>
    /// Nadirlik kuralları ve görsel matematik (saf mantık; Unity nesnesi üretmez, testlenebilir).
    /// </summary>
    public static class LootRarityRules
    {
        /// <summary>Işın sütununun uzak solma başlangıcı ve bitişi (m).</summary>
        public const float BeamFadeStart = 30f;
        public const float BeamFadeEnd = 40f;

        private static readonly Color[] Colors =
        {
            new Color(0.92f, 0.92f, 0.90f, 1f),   // Common
            new Color(0.35f, 0.88f, 0.42f, 1f),   // Uncommon
            new Color(0.30f, 0.62f, 1.00f, 1f),   // Rare
            new Color(0.70f, 0.38f, 1.00f, 1f),   // Epic
            new Color(1.00f, 0.74f, 0.18f, 1f)    // Legendary
        };

        /// <summary>Nadirliğin vurgu rengi (alfa 1).</summary>
        public static Color ColorOf(LootRarity rarity)
        {
            var i = (int)rarity;
            return Colors[Mathf.Clamp(i, 0, Colors.Length - 1)];
        }

        /// <summary>Seviyeye (1–3) göre ekipman nadirliği: 1 yeşil, 2 mavi, 3 mor.</summary>
        public static LootRarity FromGearLevel(int level)
        {
            if (level <= 1) return LootRarity.Uncommon;
            if (level == 2) return LootRarity.Rare;
            return LootRarity.Epic;
        }

        /// <summary>Silah kategorisine göre nadirlik.</summary>
        public static LootRarity FromWeaponCategory(WeaponCategory category)
        {
            switch (category)
            {
                case WeaponCategory.Sniper:
                case WeaponCategory.Dmr:
                case WeaponCategory.Lmg:
                    return LootRarity.Epic;
                case WeaponCategory.AssaultRifle:
                    return LootRarity.Rare;
                case WeaponCategory.Smg:
                case WeaponCategory.Shotgun:
                    return LootRarity.Uncommon;
                default:
                    return LootRarity.Common;
            }
        }

        /// <summary>Eşyanın nadirliği (katalogdan; bilinmeyen eşya kategoriye göre).</summary>
        public static LootRarity Of(string itemId, ItemCategory category)
        {
            if (!string.IsNullOrEmpty(itemId) && ItemCatalog.TryGet(itemId, out var def))
            {
                category = def.Category;
                if (category == ItemCategory.Armor || category == ItemCategory.Helmet || category == ItemCategory.Backpack)
                    return FromGearLevel(Mathf.Clamp(def.Level, 1, 3));
            }

            if (category == ItemCategory.Weapon && !string.IsNullOrEmpty(itemId) &&
                WeaponCatalog.TryGet(itemId, out var weapon) && weapon != null)
                return FromWeaponCategory(weapon.Category);

            return FromCategory(category);
        }

        /// <summary>Yalnızca kategoriye göre varsayılan nadirlik.</summary>
        public static LootRarity FromCategory(ItemCategory category)
        {
            switch (category)
            {
                case ItemCategory.Weapon: return LootRarity.Rare;
                case ItemCategory.Armor:
                case ItemCategory.Helmet:
                case ItemCategory.Backpack: return LootRarity.Uncommon;
                case ItemCategory.Attachment:
                case ItemCategory.Boost: return LootRarity.Rare;
                case ItemCategory.Medical:
                case ItemCategory.Throwable:
                case ItemCategory.Equipment: return LootRarity.Uncommon;
                default: return LootRarity.Common;
            }
        }

        /// <summary>Kalite kademesine (0–3) göre aynı anda çizilen en çok ışın sütunu.</summary>
        public static int MaxBeams(int tier)
        {
            switch (tier)
            {
                case 0: return 3;
                case 1: return 6;
                case 2: return 12;
                default: return 20;
            }
        }

        /// <summary>Kalite kademesine göre aynı anda canlandırılan/halkalı eşya sayısı.</summary>
        public static int MaxMarkers(int tier)
        {
            switch (tier)
            {
                case 0: return 8;
                case 1: return 16;
                case 2: return 32;
                default: return 48;
            }
        }

        /// <summary>Işın görünürlüğü 0–1: 30 m'ye kadar tam, 40 m'de sıfır.</summary>
        public static float BeamFade(float distance)
        {
            if (distance <= BeamFadeStart) return 1f;
            if (distance >= BeamFadeEnd) return 0f;
            return 1f - (distance - BeamFadeStart) / (BeamFadeEnd - BeamFadeStart);
        }

        /// <summary>Hafif süzülme yüksekliği (m), saniyede ~0.5 devir.</summary>
        public static float Bob(float time, float phase) => 0.035f + Mathf.Sin(time * 2.4f + phase) * 0.03f;

        /// <summary>Yavaş dönüş açısı (derece), ~36°/sn.</summary>
        public static float Spin(float time, float phase) => time * 36f + phase * Mathf.Rad2Deg;

        /// <summary>Alma animasyonu ölçeği (0–1 ilerleme): hızlı büyür sonra sıfıra iner.</summary>
        public static float PopScale(float t)
        {
            t = Mathf.Clamp01(t);
            if (t < 0.35f) return 1f + 0.3f * (t / 0.35f);
            return 1.3f * (1f - (t - 0.35f) / 0.65f);
        }

        /// <summary>Strobe: periyodun başındaki kısa süre yanar.</summary>
        public static bool StrobeOn(float time, float period = 1f, float duty = 0.12f)
        {
            if (period <= 0f) return true;
            var f = time / period;
            return f - Mathf.Floor(f) < duty;
        }

        /// <summary>Paraşütlü iniş yüksekliği: kalan süreye göre (başlangıç yüksekliği × kalan/toplam).</summary>
        public static float DescentHeight(float startHeight, float secondsLeft, float totalSeconds)
        {
            if (totalSeconds <= 0.001f) return 0f;
            return startHeight * Mathf.Clamp01(secondsLeft / totalSeconds);
        }
    }
}
