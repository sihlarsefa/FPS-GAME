using System;
using System.Collections.Generic;
using Project.Application.Catalogs;
using Project.Core.Domain;
using Project.Core.Interfaces;

namespace Project.Application.Services
{
    /// <summary>
    /// Ağırlıklı yağma tabloları (Low/Medium/High/Military). Silah çıkarsa yanına 2 yığın uygun mermi eklenir.
    /// Askeri bölgelerde 7.62 tüfekler, KNT-76, JNG-90, PMT-76 ve Sv.2-3 zırh daha sık çıkar.
    /// Silahlar tam şarjörle yere konur. Durumsuzdur; rastgelelik çağırandan gelir (tekrar üretilebilir).
    /// </summary>
    public sealed class LootSpawnService : ILootSpawnService
    {
        /// <summary>Silah çıktığında yanına eklenen mermi yığını sayısı.</summary>
        public const int AmmoStacksPerWeapon = 2;

        private readonly struct Entry
        {
            public readonly string ItemId;
            public readonly float Weight;

            public Entry(string itemId, float weight)
            {
                ItemId = itemId;
                Weight = weight;
            }
        }

        private sealed class Table
        {
            public readonly Entry[] Entries;
            public readonly float TotalWeight;

            public Table(Entry[] entries)
            {
                var valid = new List<Entry>(entries.Length);
                var total = 0f;
                for (var i = 0; i < entries.Length; i++)
                {
                    if (entries[i].Weight > 0f && ItemCatalog.Contains(entries[i].ItemId))
                    {
                        valid.Add(entries[i]);
                        total += entries[i].Weight;
                    }
                }

                Entries = valid.ToArray();
                TotalWeight = total;
            }
        }

        private readonly Table[] _tables;
        private readonly float[] _bonusItemChance;

        /// <summary>Özel tablo kullanılıyorsa: eşya id → yerde duracak adet (katalog varsayılanı yerine).</summary>
        private readonly Dictionary<string, int> _customQuantities;
        private IRandom _fallbackRandom;

        public LootSpawnService()
        {
            _tables = new[]
            {
                new Table(LowEntries()),
                new Table(MediumEntries()),
                new Table(HighEntries()),
                new Table(MilitaryEntries())
            };

            // Silah olmayan bir eşyanın yanına ikinci bir eşya çıkma olasılığı.
            _bonusItemChance = new[] { 0.10f, 0.18f, 0.25f, 0.35f };
        }

        /// <summary>
        /// Sabit özel tablo (antrenman / test / eski kurulum): her kademe bu listeden eşit olasılıkla seçer, adetler
        /// listedekiyle aynıdır. Katalogda olmayan eşyalar atlanır. Geçerli eşya yoksa standart tablolar kullanılır.
        /// </summary>
        public LootSpawnService(IReadOnlyList<LootItemData> customTable)
            : this()
        {
            if (customTable == null || customTable.Count == 0)
                return;

            var entries = new List<Entry>(customTable.Count);
            var quantities = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < customTable.Count; i++)
            {
                var item = customTable[i];
                if (!item.IsValid || !ItemCatalog.Contains(item.ItemId))
                    continue;

                entries.Add(new Entry(item.ItemId, 1f));
                if (!quantities.ContainsKey(item.ItemId))
                    quantities[item.ItemId] = item.Quantity;
            }

            if (entries.Count == 0)
                return;

            var table = new Table(entries.ToArray());
            for (var i = 0; i < _tables.Length; i++)
                _tables[i] = table;

            _customQuantities = quantities;
        }

        /// <summary>Özel (sabit) tablo mu kullanılıyor?</summary>
        public bool UsesCustomTable => _customQuantities != null;

        public float SpawnChance(LootTier tier)
        {
            switch (tier)
            {
                // Konum kademesi: tarla/yol (Low) < köy (Medium) < kasaba/sanayi (High) < askeri üs (Military). Docs/MAC_AKISI.md
                case LootTier.Low: return 0.38f;
                case LootTier.Medium: return 0.58f;
                case LootTier.High: return 0.74f;
                case LootTier.Military: return 0.9f;
                default: return 0.38f;
            }
        }

        public LootItemData Roll(LootTier tier, IRandom random)
        {
            var table = TableFor(tier);
            if (table.Entries.Length == 0 || table.TotalWeight <= 0f)
                return default;

            random ??= FallbackRandom();
            var roll = Clamp01(random.NextFloat()) * table.TotalWeight;
            var entries = table.Entries;
            var chosen = entries[entries.Length - 1].ItemId;
            for (var i = 0; i < entries.Length; i++)
            {
                roll -= entries[i].Weight;
                if (roll < 0f)
                {
                    chosen = entries[i].ItemId;
                    break;
                }
            }

            return CreateSpawnLoot(chosen, CustomQuantity(chosen));
        }

        public void RollSpawnGroup(LootTier tier, IRandom random, List<LootItemData> output)
        {
            if (output == null)
                return;

            random ??= FallbackRandom();
            var item = Roll(tier, random);
            if (!item.IsValid)
                return;

            output.Add(item);

            if (item.Category == ItemCategory.Weapon)
            {
                var ammoId = ItemCatalog.AmmoItemId(ItemCatalog.WeaponAmmoType(item.ItemId));
                if (ammoId != null)
                {
                    for (var i = 0; i < AmmoStacksPerWeapon; i++)
                        output.Add(ItemCatalog.CreateLoot(ammoId));
                }

                return;
            }

            var tierIndex = TierIndex(tier);
            if (random.NextFloat() < _bonusItemChance[tierIndex])
            {
                var bonus = Roll(tier, random);
                if (bonus.IsValid && bonus.Category != ItemCategory.Weapon)
                    output.Add(bonus);
            }
        }

        // ------------------------------------------------------------------ Başlangıç kiti + ikmal sandığı

        private static readonly string[] StartSidearms = { WeaponIds.Sar9, WeaponIds.Tp9, WeaponIds.Mete };
        private static readonly string[] StartPrimaries = { WeaponIds.Sar109, WeaponIds.Mpt55, WeaponIds.Sar223, WeaponIds.Escort };

        /// <summary>İkmal sandığı silah havuzu (id, ağırlık): yüksek kademe 7.62 + nadir keskin nişancı/MG.</summary>
        private static readonly Entry[] CrateWeapons =
        {
            new Entry(WeaponIds.Mpt76, 4f),
            new Entry(WeaponIds.Knt76, 3f),
            new Entry(WeaponIds.Sar762Mt, 3f),
            new Entry(WeaponIds.Pmt76, 1.5f),
            new Entry(WeaponIds.Jng90, 1.5f),
            new Entry(WeaponIds.Mg3, 0.8f)
        };

        private static readonly string[] CrateAttachments =
            { ItemIds.Scope4x, ItemIds.Suppressor, ItemIds.ExtMag, ItemIds.SniperStock };

        /// <summary>Sandıkta 3. seviye sırt çantasının çıkma olasılığı (zırh+kask garantidir).</summary>
        public const float CrateBackpackChance = 0.5f;

        /// <summary>Takım başlangıç kitinde birincil silah verilen üye aralığı: her N. üye (0, N, 2N...).</summary>
        public const int StartPrimaryEvery = 3;

        /// <summary>
        /// Garantili asgari başlangıç kiti (intikal inişi yakınında): yan silah + 2 mermi yığını + 2 sargı; includePrimary ise ek olarak
        /// bir ana silah + 2 mermi yığını. Rastgelelik yalnızca hangi silahın çıkacağıdır; içerik kümesi her zaman aynı boyuttadır.
        /// </summary>
        public void RollStartKit(IRandom random, List<LootItemData> output, bool includePrimary)
        {
            if (output == null)
                return;

            random ??= FallbackRandom();
            AddWeaponWithAmmo(StartSidearms[PickIndex(random, StartSidearms.Length)], output);
            output.Add(ItemCatalog.CreateLoot(ItemIds.Bandage));
            output.Add(ItemCatalog.CreateLoot(ItemIds.Bandage));
            if (includePrimary)
                AddWeaponWithAmmo(StartPrimaries[PickIndex(random, StartPrimaries.Length)], output);
        }

        /// <summary>
        /// İkmal sandığı içeriği (yüksek kademe, garantili): 1 yüksek kademe silah + 2 mermi yığını, 3. seviye zırh + kask,
        /// MedKit + İlk Yardım, 1 el bombası, 1 aksesuar; %50 3. seviye çanta.
        /// </summary>
        public void RollSupplyCrate(IRandom random, List<LootItemData> output)
        {
            if (output == null)
                return;

            random ??= FallbackRandom();
            var table = new Table(CrateWeapons);
            if (table.Entries.Length > 0)
            {
                var roll = Clamp01(random.NextFloat()) * table.TotalWeight;
                var chosen = table.Entries[table.Entries.Length - 1].ItemId;
                for (var i = 0; i < table.Entries.Length; i++)
                {
                    roll -= table.Entries[i].Weight;
                    if (roll < 0f)
                    {
                        chosen = table.Entries[i].ItemId;
                        break;
                    }
                }

                AddWeaponWithAmmo(chosen, output);
            }

            AddIfKnown(ItemIds.Vest3, output);
            AddIfKnown(ItemIds.Helmet3, output);
            AddIfKnown(ItemIds.MedKit, output);
            AddIfKnown(ItemIds.FirstAid, output);
            AddIfKnown(ItemIds.FragGrenade, output);
            AddIfKnown(CrateAttachments[PickIndex(random, CrateAttachments.Length)], output);
            if (random.NextFloat() < CrateBackpackChance)
                AddIfKnown(ItemIds.Backpack3, output);
        }

        private static int PickIndex(IRandom random, int length)
        {
            var i = (int)(Clamp01(random.NextFloat()) * length);
            return i >= length ? length - 1 : i;
        }

        private static void AddIfKnown(string itemId, List<LootItemData> output)
        {
            if (ItemCatalog.Contains(itemId))
                output.Add(ItemCatalog.CreateLoot(itemId));
        }

        private static void AddWeaponWithAmmo(string weaponId, List<LootItemData> output)
        {
            if (!ItemCatalog.Contains(weaponId))
                return;

            output.Add(ItemCatalog.CreateWeaponLoot(weaponId, -1));
            var ammoId = ItemCatalog.AmmoItemId(ItemCatalog.WeaponAmmoType(weaponId));
            if (ammoId == null)
                return;

            for (var i = 0; i < AmmoStacksPerWeapon; i++)
                output.Add(ItemCatalog.CreateLoot(ammoId));
        }

        /// <summary>Tablodaki bir eşyanın göreli ağırlığı (test/denge için). Yoksa 0.</summary>
        public float GetWeight(LootTier tier, string itemId)
        {
            var entries = TableFor(tier).Entries;
            for (var i = 0; i < entries.Length; i++)
            {
                if (string.Equals(entries[i].ItemId, itemId, StringComparison.Ordinal))
                    return entries[i].Weight;
            }

            return 0f;
        }

        public float GetTotalWeight(LootTier tier) => TableFor(tier).TotalWeight;

        private static LootItemData CreateSpawnLoot(string itemId, int quantity)
        {
            var definition = ItemCatalog.Get(itemId);
            if (definition == null)
                return default;

            if (definition.Category == ItemCategory.Weapon)
                return ItemCatalog.CreateWeaponLoot(itemId, -1);

            // Yığınlanmayan eşyalar (zırh, kask, çanta) her zaman tek adettir.
            return ItemCatalog.CreateLoot(itemId, definition.IsStackable && quantity > 0 ? quantity : -1);
        }

        private int CustomQuantity(string itemId) =>
            _customQuantities != null && _customQuantities.TryGetValue(itemId, out var quantity) ? quantity : -1;

        private Table TableFor(LootTier tier) => _tables[TierIndex(tier)];

        private static int TierIndex(LootTier tier)
        {
            var index = (int)tier;
            return index < 0 ? 0 : index > 3 ? 3 : index;
        }

        private IRandom FallbackRandom() => _fallbackRandom ??= new SeededRandom(Environment.TickCount);

        private static float Clamp01(float value) => value < 0f ? 0f : value >= 1f ? 0.9999f : value;

        // ------------------------------------------------------------------ Tablolar

        private static Entry[] LowEntries() => new[]
        {
            new Entry(ItemIds.Ammo9, 14f),
            new Entry(ItemIds.Ammo556, 7f),
            new Entry(ItemIds.Ammo762, 5f),
            new Entry(ItemIds.Ammo12, 6f),
            new Entry(ItemIds.Bandage, 14f),
            new Entry(ItemIds.FirstAid, 4f),
            new Entry(ItemIds.MedKit, 0.5f),
            new Entry(ItemIds.EnergyDrink, 5f),
            new Entry(ItemIds.Painkiller, 1.5f),
            new Entry(ItemIds.FragGrenade, 2.5f),
            new Entry(ItemIds.SmokeGrenade, 3f),
            new Entry(ItemIds.Vest1, 5f),
            new Entry(ItemIds.Helmet1, 5f),
            new Entry(ItemIds.Backpack1, 5f),
            new Entry(ItemIds.Vest2, 1f),
            new Entry(ItemIds.Helmet2, 1f),
            new Entry(ItemIds.Backpack2, 1f),
            new Entry(ItemIds.RedDot, 1f),
            new Entry(ItemIds.Suppressor, 0.5f),
            new Entry(ItemIds.ExtMag, 0.8f),
            new Entry(ItemIds.VerticalGrip, 0.8f),
            new Entry(WeaponIds.Sar9, 5f),
            new Entry(WeaponIds.Tp9, 4f),
            new Entry(WeaponIds.Sar109, 3f),
            new Entry(WeaponIds.Escort, 4f),
            new Entry(WeaponIds.Mpt55, 2f),
            new Entry(WeaponIds.G3, 1f),
            new Entry(WeaponIds.Mete, 3f),
            new Entry(WeaponIds.Sar223, 1f),
            new Entry(WeaponIds.EscortMagnum, 0.8f),
            // El bombası çeşitleri (sona eklendi)
            new Entry(ItemIds.FlashGrenade, 0.6f),
            new Entry(ItemIds.MolotovGrenade, 0.5f),
            new Entry(ItemIds.DecoyGrenade, 0.4f),
        };

        private static Entry[] MediumEntries() => new[]
        {
            new Entry(ItemIds.Ammo9, 9f),
            new Entry(ItemIds.Ammo556, 10f),
            new Entry(ItemIds.Ammo762, 8f),
            new Entry(ItemIds.Ammo12, 4f),
            new Entry(ItemIds.Bandage, 11f),
            new Entry(ItemIds.FirstAid, 5f),
            new Entry(ItemIds.MedKit, 1f),
            new Entry(ItemIds.EnergyDrink, 5f),
            new Entry(ItemIds.Painkiller, 2.5f),
            new Entry(ItemIds.FragGrenade, 3.5f),
            new Entry(ItemIds.SmokeGrenade, 3f),
            new Entry(ItemIds.Vest1, 4f),
            new Entry(ItemIds.Helmet1, 4f),
            new Entry(ItemIds.Backpack1, 3f),
            new Entry(ItemIds.Vest2, 3f),
            new Entry(ItemIds.Helmet2, 3f),
            new Entry(ItemIds.Backpack2, 2.5f),
            new Entry(ItemIds.Vest3, 0.3f),
            new Entry(ItemIds.Helmet3, 0.3f),
            new Entry(ItemIds.Backpack3, 0.3f),
            new Entry(ItemIds.RedDot, 2f),
            new Entry(ItemIds.Scope2x, 1.2f),
            new Entry(ItemIds.Suppressor, 1.2f),
            new Entry(ItemIds.ExtMag, 1.5f),
            new Entry(ItemIds.VerticalGrip, 1.5f),
            new Entry(ItemIds.SniperStock, 0.8f),
            new Entry(WeaponIds.Sar9, 3f),
            new Entry(WeaponIds.Tp9, 3f),
            new Entry(WeaponIds.Sar109, 4f),
            new Entry(WeaponIds.Escort, 3f),
            new Entry(WeaponIds.Mpt55, 4f),
            new Entry(WeaponIds.G3, 3f),
            new Entry(WeaponIds.Mpt76, 1.5f),
            new Entry(WeaponIds.Knt76, 0.5f),
            new Entry(WeaponIds.Mete, 2.5f),
            new Entry(WeaponIds.Sar223, 3f),
            new Entry(WeaponIds.Mpt76K, 1.5f),
            new Entry(WeaponIds.EscortMagnum, 1.5f),
            new Entry(WeaponIds.Sar762Mt, 0.3f),
            // El bombası çeşitleri (sona eklendi)
            new Entry(ItemIds.FlashGrenade, 1.0f),
            new Entry(ItemIds.MolotovGrenade, 0.9f),
            new Entry(ItemIds.DecoyGrenade, 0.7f),
        };

        private static Entry[] HighEntries() => new[]
        {
            new Entry(ItemIds.Ammo9, 6f),
            new Entry(ItemIds.Ammo556, 10f),
            new Entry(ItemIds.Ammo762, 11f),
            new Entry(ItemIds.Ammo12, 3f),
            new Entry(ItemIds.Bandage, 8f),
            new Entry(ItemIds.FirstAid, 6f),
            new Entry(ItemIds.MedKit, 2f),
            new Entry(ItemIds.EnergyDrink, 4f),
            new Entry(ItemIds.Painkiller, 3.5f),
            new Entry(ItemIds.FragGrenade, 4f),
            new Entry(ItemIds.SmokeGrenade, 3f),
            new Entry(ItemIds.Vest1, 2f),
            new Entry(ItemIds.Helmet1, 2f),
            new Entry(ItemIds.Backpack1, 1.5f),
            new Entry(ItemIds.Vest2, 4f),
            new Entry(ItemIds.Helmet2, 4f),
            new Entry(ItemIds.Backpack2, 3f),
            new Entry(ItemIds.Vest3, 1f),
            new Entry(ItemIds.Helmet3, 1f),
            new Entry(ItemIds.Backpack3, 1f),
            new Entry(ItemIds.RedDot, 2f),
            new Entry(ItemIds.Scope2x, 1.5f),
            new Entry(ItemIds.Scope4x, 1f),
            new Entry(ItemIds.Suppressor, 1.5f),
            new Entry(ItemIds.ExtMag, 2f),
            new Entry(ItemIds.VerticalGrip, 1.8f),
            new Entry(ItemIds.SniperStock, 1.2f),
            new Entry(WeaponIds.Sar9, 1.5f),
            new Entry(WeaponIds.Tp9, 2f),
            new Entry(WeaponIds.Sar109, 3f),
            new Entry(WeaponIds.Escort, 2f),
            new Entry(WeaponIds.Mpt55, 4.5f),
            new Entry(WeaponIds.G3, 3.5f),
            new Entry(WeaponIds.Mpt76, 3f),
            new Entry(WeaponIds.Knt76, 1.5f),
            new Entry(WeaponIds.Jng90, 0.6f),
            new Entry(WeaponIds.Pmt76, 0.6f),
            new Entry(WeaponIds.Mete, 1.5f),
            new Entry(WeaponIds.Sar223, 3.5f),
            new Entry(WeaponIds.Mpt76K, 2.5f),
            new Entry(WeaponIds.EscortMagnum, 1.8f),
            new Entry(WeaponIds.Sar762Mt, 1.2f),
            new Entry(WeaponIds.Mg3, 0.3f),
            // El bombası çeşitleri (sona eklendi)
            new Entry(ItemIds.FlashGrenade, 1.4f),
            new Entry(ItemIds.MolotovGrenade, 1.2f),
            new Entry(ItemIds.DecoyGrenade, 1.0f),
        };

        private static Entry[] MilitaryEntries() => new[]
        {
            new Entry(ItemIds.Ammo9, 3f),
            new Entry(ItemIds.Ammo556, 8f),
            new Entry(ItemIds.Ammo762, 14f),
            new Entry(ItemIds.Ammo12, 2f),
            new Entry(ItemIds.Bandage, 6f),
            new Entry(ItemIds.FirstAid, 6f),
            new Entry(ItemIds.MedKit, 3f),
            new Entry(ItemIds.EnergyDrink, 3f),
            new Entry(ItemIds.Painkiller, 4f),
            new Entry(ItemIds.FragGrenade, 5f),
            new Entry(ItemIds.SmokeGrenade, 3.5f),
            new Entry(ItemIds.NightVision, 1.2f),
            new Entry(ItemIds.Vest1, 0.5f),
            new Entry(ItemIds.Helmet1, 0.5f),
            new Entry(ItemIds.Vest2, 4.5f),
            new Entry(ItemIds.Helmet2, 4.5f),
            new Entry(ItemIds.Backpack2, 3f),
            new Entry(ItemIds.Vest3, 2.5f),
            new Entry(ItemIds.Helmet3, 2.5f),
            new Entry(ItemIds.Backpack3, 2f),
            new Entry(ItemIds.Scope2x, 1.5f),
            new Entry(ItemIds.Scope4x, 2f),
            new Entry(ItemIds.Suppressor, 2f),
            new Entry(ItemIds.ExtMag, 2.5f),
            new Entry(ItemIds.VerticalGrip, 2f),
            new Entry(ItemIds.SniperStock, 1.8f),
            new Entry(WeaponIds.Tp9, 1f),
            new Entry(WeaponIds.Sar109, 1.5f),
            new Entry(WeaponIds.Escort, 1f),
            new Entry(WeaponIds.Mpt55, 3f),
            new Entry(WeaponIds.G3, 3.5f),
            new Entry(WeaponIds.Mpt76, 4.5f),
            new Entry(WeaponIds.Knt76, 3f),
            new Entry(WeaponIds.Jng90, 2f),
            new Entry(WeaponIds.Pmt76, 2f),
            new Entry(WeaponIds.Sar223, 2f),
            new Entry(WeaponIds.Mpt76K, 2.5f),
            new Entry(WeaponIds.EscortMagnum, 1f),
            new Entry(WeaponIds.Sar762Mt, 2.5f),
            new Entry(WeaponIds.Mg3, 1.5f),
            // El bombası çeşitleri (sona eklendi)
            new Entry(ItemIds.FlashGrenade, 1.8f),
            new Entry(ItemIds.MolotovGrenade, 1.6f),
            new Entry(ItemIds.DecoyGrenade, 1.2f),
        };
    }
}
