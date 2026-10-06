using Project.Application.Catalogs;
using Project.Core.Domain;
using UnityEngine;

namespace Project.Presentation.UI
{
    /// <summary>Envanter / yağma nadirlik kademesi (renk kodu: yerdeki yağma ışınlarıyla ortak kullanılabilir).</summary>
    public enum InventoryRarity
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        Epic = 3,
        Legendary = 4
    }

    /// <summary>
    /// Saf nadirlik kuralı: eşya kategorisi/kimliği/seviyesi → kademe → renk ve Türkçe ad.
    /// Yağma ışını ve envanter aynı rengi kullansın diye herkese açık ve Unity sahnesinden bağımsızdır.
    /// </summary>
    public static class InventoryRarityRules
    {
        public static readonly Color CommonColor = new Color(0.80f, 0.82f, 0.76f, 1f);
        public static readonly Color UncommonColor = new Color(0.36f, 0.78f, 0.30f, 1f);
        public static readonly Color RareColor = new Color(0.25f, 0.55f, 1.00f, 1f);
        public static readonly Color EpicColor = new Color(0.70f, 0.38f, 0.95f, 1f);
        public static readonly Color LegendaryColor = new Color(1.00f, 0.66f, 0.10f, 1f);

        public static Color ColorOf(InventoryRarity rarity)
        {
            switch (rarity)
            {
                case InventoryRarity.Uncommon: return UncommonColor;
                case InventoryRarity.Rare: return RareColor;
                case InventoryRarity.Epic: return EpicColor;
                case InventoryRarity.Legendary: return LegendaryColor;
                default: return CommonColor;
            }
        }

        public static string NameOf(InventoryRarity rarity)
        {
            switch (rarity)
            {
                case InventoryRarity.Uncommon: return "Yaygın Değil";
                case InventoryRarity.Rare: return "Nadir";
                case InventoryRarity.Epic: return "Destansı";
                case InventoryRarity.Legendary: return "Efsanevi";
                default: return "Standart";
            }
        }

        /// <summary>Silah kategorisine göre kademe (tabanca standart … keskin nişancı efsanevi).</summary>
        public static InventoryRarity OfWeapon(WeaponCategory category)
        {
            switch (category)
            {
                case WeaponCategory.Smg:
                case WeaponCategory.Shotgun:
                    return InventoryRarity.Uncommon;
                case WeaponCategory.AssaultRifle:
                    return InventoryRarity.Rare;
                case WeaponCategory.Dmr:
                case WeaponCategory.Lmg:
                    return InventoryRarity.Epic;
                case WeaponCategory.Sniper:
                    return InventoryRarity.Legendary;
                default:
                    return InventoryRarity.Common;
            }
        }

        /// <summary>Seviye 1..3 → Yaygın değil / Nadir / Destansı (zırh, kask, çanta).</summary>
        public static InventoryRarity OfLevel(int level)
        {
            if (level >= 3) return InventoryRarity.Epic;
            if (level == 2) return InventoryRarity.Rare;
            if (level == 1) return InventoryRarity.Uncommon;
            return InventoryRarity.Common;
        }

        /// <summary>Eşya kategorisi + kimliğine göre kademe.</summary>
        public static InventoryRarity Of(ItemCategory category, string itemId, int level = 0)
        {
            switch (category)
            {
                case ItemCategory.Armor:
                case ItemCategory.Helmet:
                case ItemCategory.Backpack:
                    return OfLevel(level > 0 ? level : LevelFromId(itemId));
                case ItemCategory.Ammunition:
                case ItemCategory.Throwable:
                    return string.Equals(itemId, ItemIds.FragGrenade, System.StringComparison.Ordinal) ? InventoryRarity.Uncommon : InventoryRarity.Common;
                case ItemCategory.Medical:
                    if (string.Equals(itemId, ItemIds.MedKit, System.StringComparison.Ordinal)) return InventoryRarity.Rare;
                    if (string.Equals(itemId, ItemIds.FirstAid, System.StringComparison.Ordinal)) return InventoryRarity.Uncommon;
                    return InventoryRarity.Common;
                case ItemCategory.Boost:
                    return string.Equals(itemId, ItemIds.Painkiller, System.StringComparison.Ordinal) ? InventoryRarity.Rare : InventoryRarity.Uncommon;
                case ItemCategory.Equipment:
                    return InventoryRarity.Epic;
                case ItemCategory.Attachment:
                    return AttachmentRarity(itemId);
                default:
                    return InventoryRarity.Common;
            }
        }

        private static InventoryRarity AttachmentRarity(string itemId)
        {
            if (string.Equals(itemId, ItemIds.Scope4x, System.StringComparison.Ordinal) || string.Equals(itemId, ItemIds.SniperStock, System.StringComparison.Ordinal))
                return InventoryRarity.Epic;
            if (string.Equals(itemId, ItemIds.Scope2x, System.StringComparison.Ordinal) || string.Equals(itemId, ItemIds.Suppressor, System.StringComparison.Ordinal))
                return InventoryRarity.Rare;
            return InventoryRarity.Uncommon;
        }

        /// <summary>"vest_2" / "helmet_3" / "backpack_1" sonundaki rakam; yoksa 0.</summary>
        public static int LevelFromId(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return 0;
            var last = itemId[itemId.Length - 1];
            return last >= '0' && last <= '9' ? last - '0' : 0;
        }
    }
}
