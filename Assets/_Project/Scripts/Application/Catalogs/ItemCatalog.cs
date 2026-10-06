using System;
using System.Collections.Generic;
using Project.Core.Domain;

namespace Project.Application.Catalogs
{
    /// <summary>
    /// Tüm eşya tanımları (mermi, tıbbi, boost, bomba, zırh, kask, çanta) + her silah için Weapon kategorisinde bir eşya.
    /// Ağ için sabit indeks eşlemesi sağlar (All sırası kalıcıdır; yeni eşyalar sona eklenir).
    /// Silah eşyaları WeaponIds sabitlerinden üretilir; statik kurulumda WeaponCatalog çağrılmaz.
    /// </summary>
    public static class ItemCatalog
    {
        private static readonly List<ItemDefinition> _all = new();
        private static readonly Dictionary<string, ItemDefinition> _byId = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, int> _indexById = new(StringComparer.Ordinal);
        private static readonly IReadOnlyList<ItemDefinition> _allReadOnly = _all.AsReadOnly();

        static ItemCatalog()
        {
            // ---- Mühimmat (sıra kalıcıdır: ağ indeksleri) ----
            AddAmmo(ItemIds.Ammo9, "9mm Mermi", AmmoType.Mm9, 0.375f, 30);
            AddAmmo(ItemIds.Ammo556, "5.56 Mermi", AmmoType.Mm556, 0.5f, 30);
            AddAmmo(ItemIds.Ammo762, "7.62 Mermi", AmmoType.Mm762, 0.7f, 30);
            AddAmmo(ItemIds.Ammo12, "12 Kalibre Fişek", AmmoType.Gauge12, 1.25f, 10);

            // ---- Tıbbi ----
            AddMedical(ItemIds.Bandage, "Sargı Bezi", 2f, 5, healAmount: 10f, healCap: 75f, useSeconds: 4f);
            AddMedical(ItemIds.FirstAid, "İlk Yardım Çantası", 10f, 1, healAmount: 75f, healCap: 75f, useSeconds: 6f);
            AddMedical(ItemIds.MedKit, "Sıhhiye Çantası", 20f, 1, healAmount: 100f, healCap: 100f, useSeconds: 8f);

            // ---- Boost ----
            AddBoost(ItemIds.EnergyDrink, "Enerji İçeceği", 4f, boostAmount: 40f, useSeconds: 4f);
            AddBoost(ItemIds.Painkiller, "Ağrı Kesici", 10f, boostAmount: 60f, useSeconds: 6f);

            // ---- Atılabilir ----
            AddThrowable(ItemIds.FragGrenade, "El Bombası", 12f);
            AddThrowable(ItemIds.SmokeGrenade, "Sis Bombası", 14f);

            // ---- Çelik yelek ----
            AddArmor(ItemIds.Vest1, "Çelik Yelek (Sv.1)", ItemCategory.Armor, 1, 200f, 0.30f);
            AddArmor(ItemIds.Vest2, "Çelik Yelek (Sv.2)", ItemCategory.Armor, 2, 220f, 0.40f);
            AddArmor(ItemIds.Vest3, "Çelik Yelek (Sv.3)", ItemCategory.Armor, 3, 250f, 0.55f);

            // ---- Kask ----
            AddArmor(ItemIds.Helmet1, "Kask (Sv.1)", ItemCategory.Helmet, 1, 80f, 0.30f);
            AddArmor(ItemIds.Helmet2, "Kask (Sv.2)", ItemCategory.Helmet, 2, 150f, 0.40f);
            AddArmor(ItemIds.Helmet3, "Kask (Sv.3)", ItemCategory.Helmet, 3, 230f, 0.55f);

            // ---- Sırt çantası ----
            AddBackpack(ItemIds.Backpack1, "Sırt Çantası (Sv.1)", 1, 150f);
            AddBackpack(ItemIds.Backpack2, "Sırt Çantası (Sv.2)", 2, 200f);
            AddBackpack(ItemIds.Backpack3, "Sırt Çantası (Sv.3)", 3, 250f);

            // ---- Silahlar (eşya kimliği = silah kimliği). Level = bot tercih kademesi. ----
            AddWeapon(WeaponIds.Sar9, "SAR 9", AmmoType.Mm9, 1);
            AddWeapon(WeaponIds.Tp9, "Canik TP9", AmmoType.Mm9, 1);
            AddWeapon(WeaponIds.Sar109, "SAR 109T", AmmoType.Mm9, 2);
            AddWeapon(WeaponIds.Mpt55, "MPT-55", AmmoType.Mm556, 3);
            AddWeapon(WeaponIds.Mpt76, "MPT-76", AmmoType.Mm762, 4);
            AddWeapon(WeaponIds.G3, "G3A7", AmmoType.Mm762, 3);
            AddWeapon(WeaponIds.Knt76, "KNT-76", AmmoType.Mm762, 4);
            AddWeapon(WeaponIds.Jng90, "JNG-90", AmmoType.Mm762, 4);
            AddWeapon(WeaponIds.Pmt76, "PMT-76", AmmoType.Mm762, 4);
            AddWeapon(WeaponIds.Escort, "Escort", AmmoType.Gauge12, 2);

            // ---- Eklentiler (sona eklenir: ağ indeksleri kalıcı) ----
            var attachments = AttachmentCatalog.All;
            for (var i = 0; i < attachments.Count; i++)
            {
                Add(new ItemDefinition
                {
                    Id = attachments[i].ItemId,
                    DisplayName = attachments[i].DisplayName,
                    Category = ItemCategory.Attachment,
                    Level = (int)attachments[i].Slot,
                    Weight = 0f,
                    PickupQuantity = 1
                });
            }

            // ---- Teçhizat (sona eklenir: ağ indeksleri kalıcı) ----
            Add(new ItemDefinition
            {
                Id = ItemIds.NightVision,
                DisplayName = "Gece Görüş Gözlüğü",
                Category = ItemCategory.Equipment,
                Weight = 1.5f,
                PickupQuantity = 1
            });

            // ---- silahlar2 (ağ indeksleri kalıcı kalsın diye eklentilerden sonra) ----
            AddWeapon(WeaponIds.Sar223, "SAR 223", AmmoType.Mm556, 3);
            AddWeapon(WeaponIds.Mpt76K, "MPT-76K", AmmoType.Mm762, 3);
            AddWeapon(WeaponIds.Mete, "Canik METE SFT", AmmoType.Mm9, 1);
            AddWeapon(WeaponIds.Sar762Mt, "SAR 762 MT", AmmoType.Mm762, 4);
            AddWeapon(WeaponIds.Mg3, "MG3", AmmoType.Mm762, 4);
            AddWeapon(WeaponIds.EscortMagnum, "Escort Magnum", AmmoType.Gauge12, 2);

            // ---- El bombası çeşitleri (sona eklendi: ağ indeksleri kalıcı) ----
            AddThrowable(ItemIds.FlashGrenade, "Flaş Bombası", 10f);
            AddThrowable(ItemIds.MolotovGrenade, "Molotof Kokteyli", 12f);
            AddThrowable(ItemIds.DecoyGrenade, "Aldatma Bombası", 10f);
        }

        /// <summary>Kalıcı sırayla tüm eşyalar (ağ indeksi = liste indeksi).</summary>
        public static IReadOnlyList<ItemDefinition> All => _allReadOnly;

        public static int Count => _all.Count;

        /// <summary>Eşya tanımı; bilinmeyen/boş kimlikte null döner.</summary>
        public static ItemDefinition Get(string itemId)
        {
            if (itemId == null)
                return null;

            return _byId.TryGetValue(itemId, out var definition) ? definition : null;
        }

        public static bool TryGet(string itemId, out ItemDefinition definition)
        {
            if (itemId == null)
            {
                definition = null;
                return false;
            }

            return _byId.TryGetValue(itemId, out definition);
        }

        public static bool Contains(string itemId) => itemId != null && _byId.ContainsKey(itemId);

        /// <summary>Mermi türünün eşya kimliği (AmmoType.None → null).</summary>
        public static string AmmoItemId(AmmoType type)
        {
            switch (type)
            {
                case AmmoType.Mm9: return ItemIds.Ammo9;
                case AmmoType.Mm556: return ItemIds.Ammo556;
                case AmmoType.Mm762: return ItemIds.Ammo762;
                case AmmoType.Gauge12: return ItemIds.Ammo12;
                default: return null;
            }
        }

        /// <summary>Ağ indeksi; bilinmeyen kimlikte -1.</summary>
        public static int GetNetworkIndex(string itemId)
        {
            if (itemId == null)
                return -1;

            return _indexById.TryGetValue(itemId, out var index) ? index : -1;
        }

        /// <summary>Ağ indeksinden eşya kimliği; aralık dışında null.</summary>
        public static string FromNetworkIndex(int index) =>
            index >= 0 && index < _all.Count ? _all[index].Id : null;

        /// <summary>Yerde duracak eşya verisi; quantity -1 → tanımdaki PickupQuantity. Bilinmeyen kimlikte geçersiz veri.</summary>
        public static LootItemData CreateLoot(string itemId, int quantity = -1)
        {
            var definition = Get(itemId);
            if (definition == null)
                return default;

            var amount = quantity < 0 ? Math.Max(1, definition.PickupQuantity) : quantity;
            return new LootItemData(definition.Id, definition.Category, definition.DisplayName, amount, -1, -1f);
        }

        /// <summary>Silah eşyası: şarjördeki mermi ile (-1 = tam şarjör).</summary>
        public static LootItemData CreateWeaponLoot(string weaponId, int loadedAmmo = -1)
        {
            var definition = Get(weaponId);
            if (definition == null || definition.Category != ItemCategory.Weapon)
                return default;

            return new LootItemData(definition.Id, ItemCategory.Weapon, NameProfile.Get(definition.Id, definition.DisplayName), 1, loadedAmmo, -1f);
        }

        /// <summary>Zırh/kask eşyası: kalan dayanıklılık ile (-1 = yeni).</summary>
        public static LootItemData CreateArmorLoot(string itemId, float durability = -1f)
        {
            var definition = Get(itemId);
            if (definition == null)
                return default;

            return new LootItemData(definition.Id, definition.Category, definition.DisplayName, 1, -1, durability);
        }

        public static string GetDisplayName(string itemId)
        {
            var definition = Get(itemId);
            return definition != null ? NameProfile.Get(definition.Id, definition.DisplayName) : itemId ?? string.Empty;
        }

        /// <summary>Kategorinin Türkçe adı (envanter ekranı başlıkları).</summary>
        public static string GetCategoryName(ItemCategory category)
        {
            switch (category)
            {
                case ItemCategory.Weapon: return "Silah";
                case ItemCategory.Ammunition: return "Mühimmat";
                case ItemCategory.Armor: return "Çelik Yelek";
                case ItemCategory.Medical: return "Tıbbi Malzeme";
                case ItemCategory.Throwable: return "Bomba";
                case ItemCategory.Attachment: return "Eklenti";
                case ItemCategory.Helmet: return "Kask";
                case ItemCategory.Backpack: return "Sırt Çantası";
                case ItemCategory.Boost: return "Takviye";
                case ItemCategory.Equipment: return "Teçhizat";
                default: return "Eşya";
            }
        }

        public static bool IsWeapon(string itemId)
        {
            var definition = Get(itemId);
            return definition != null && definition.Category == ItemCategory.Weapon;
        }

        /// <summary>Silahın kullandığı mermi (WeaponCatalog'a bağımlı değildir).</summary>
        public static AmmoType WeaponAmmoType(string weaponId)
        {
            var definition = Get(weaponId);
            return definition != null && definition.Category == ItemCategory.Weapon ? definition.AmmoType : AmmoType.None;
        }

        /// <summary>Silahın bot tercih kademesi (1 tabanca … 4 7.62 tüfek); bilinmeyen 0.</summary>
        public static int WeaponTier(string weaponId)
        {
            var definition = Get(weaponId);
            return definition != null && definition.Category == ItemCategory.Weapon ? definition.Level : 0;
        }

        public static string VestId(int level) => level switch
        {
            1 => ItemIds.Vest1,
            2 => ItemIds.Vest2,
            3 => ItemIds.Vest3,
            _ => null
        };

        public static string HelmetId(int level) => level switch
        {
            1 => ItemIds.Helmet1,
            2 => ItemIds.Helmet2,
            3 => ItemIds.Helmet3,
            _ => null
        };

        public static string BackpackId(int level) => level switch
        {
            1 => ItemIds.Backpack1,
            2 => ItemIds.Backpack2,
            3 => ItemIds.Backpack3,
            _ => null
        };

        private static void Add(ItemDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.Id) || _byId.ContainsKey(definition.Id))
                return;

            _indexById[definition.Id] = _all.Count;
            _byId[definition.Id] = definition;
            _all.Add(definition);
        }

        private static void AddAmmo(string id, string name, AmmoType type, float weight, int pickup)
        {
            Add(new ItemDefinition
            {
                Id = id,
                DisplayName = name,
                Category = ItemCategory.Ammunition,
                AmmoType = type,
                Weight = weight,
                PickupQuantity = pickup
            });
        }

        private static void AddMedical(string id, string name, float weight, int pickup, float healAmount, float healCap, float useSeconds)
        {
            Add(new ItemDefinition
            {
                Id = id,
                DisplayName = name,
                Category = ItemCategory.Medical,
                Weight = weight,
                PickupQuantity = pickup,
                HealAmount = healAmount,
                HealCap = healCap,
                UseSeconds = useSeconds
            });
        }

        private static void AddBoost(string id, string name, float weight, float boostAmount, float useSeconds)
        {
            Add(new ItemDefinition
            {
                Id = id,
                DisplayName = name,
                Category = ItemCategory.Boost,
                Weight = weight,
                PickupQuantity = 1,
                BoostAmount = boostAmount,
                UseSeconds = useSeconds
            });
        }

        private static void AddThrowable(string id, string name, float weight)
        {
            Add(new ItemDefinition
            {
                Id = id,
                DisplayName = name,
                Category = ItemCategory.Throwable,
                Weight = weight,
                PickupQuantity = 1
            });
        }

        private static void AddArmor(string id, string name, ItemCategory category, int level, float durability, float reduction)
        {
            Add(new ItemDefinition
            {
                Id = id,
                DisplayName = name,
                Category = category,
                Level = level,
                Durability = durability,
                DamageReduction = reduction,
                Weight = 0f,
                PickupQuantity = 1
            });
        }

        private static void AddBackpack(string id, string name, int level, float capacity)
        {
            Add(new ItemDefinition
            {
                Id = id,
                DisplayName = name,
                Category = ItemCategory.Backpack,
                Level = level,
                Capacity = capacity,
                Weight = 0f,
                PickupQuantity = 1
            });
        }

        private static void AddWeapon(string weaponId, string name, AmmoType ammo, int tier)
        {
            Add(new ItemDefinition
            {
                Id = weaponId,
                DisplayName = name,
                Category = ItemCategory.Weapon,
                AmmoType = ammo,
                Level = tier,
                Weight = 0f,
                PickupQuantity = 1,
                WeaponId = weaponId
            });
        }
    }
}
