using System;
using System.Collections.Generic;
using Project.Application.Catalogs;
using Project.Core.Domain;
using Project.Core.Interfaces;

namespace Project.Application.Services
{
    public readonly struct PickupResult
    {
        public bool Accepted { get; }
        public int QuantityTaken { get; }
        public bool FullyTaken { get; }

        public PickupResult(bool accepted, int quantityTaken, bool fullyTaken)
        {
            Accepted = accepted;
            QuantityTaken = quantityTaken;
            FullyTaken = fullyTaken;
        }

        public static PickupResult Rejected => new(false, 0, false);
    }

    /// <summary>
    /// Savaşanın envanteri: 3 silah yuvası (2 ana + tabanca), yelek, kask, sırt çantası ve ağırlık sınırlı yığınlar.
    /// Kurallar:
    ///  - Tabanca → yuva 2. Ana silah → boş ana yuva; yoksa aktif ana yuva (aktif tabancaysa yuva 0) ile değiştirilir,
    ///    eski silah 'dropped' listesine (LoadedAmmo korunarak) eklenir. Silahsızken alınan silah otomatik kuşanılır.
    ///  - Zırh/kask: hiç yoksa ya da daha yüksek seviye / aynı seviye ve daha yüksek dayanıklılıksa kuşanılır, eskisi düşer.
    ///  - Çanta: daha yüksek seviyeyse kuşanılır. Yığınlar kapasite kadar (kısmi) alınır.
    ///  - Kapasite = BaseCapacity + (yelek varsa VestCapacityBonus) + çanta bonusu.
    /// Silahlar ve kuşanılan teçhizat kapasiteye sayılmaz; yalnızca yığınlar (mermi, tıbbi, boost, bomba) ağırlık taşır.
    /// </summary>
    public sealed class InventoryService : IInventory, IAmmoSource, IArmorProvider
    {
        public const int WeaponSlotCount = 3;
        public const int PrimarySlotA = 0;
        public const int PrimarySlotB = 1;
        public const int SidearmSlot = 2;
        public const float BaseCapacity = 60f;
        public const float VestCapacityBonus = 50f;

        /// <summary>InfiniteAmmo açıkken GetAmmo'nun bildirdiği yedek mermi.</summary>
        public const int InfiniteAmmoReport = 999;

        private const double WeightEpsilon = 0.0001;

        private readonly IEventBus _eventBus;
        private readonly WeaponRuntimeService[] _weapons = new WeaponRuntimeService[WeaponSlotCount];
        private readonly int[] _counts;
        private int _activeSlot = -1;
        private ArmorPiece _vest;
        private ArmorPiece _helmet;
        private int _backpackLevel;
        private float _weight;
        private bool _infiniteAmmo;
        private int _batchDepth;
        private bool _changedPending;

        public InventoryService(IEventBus eventBus, PlayerId ownerId)
        {
            _eventBus = eventBus;
            OwnerId = ownerId;
            _counts = new int[ItemCatalog.Count];
        }

        public PlayerId OwnerId { get; }

        /// <summary>Her değişiklikte (eşya, silah, kuşanma, aktif yuva) tetiklenir.</summary>
        public event Action Changed;

        /// <summary>
        /// İsteğe bağlı silah tanımı çözücü (test/mod). Null ise WeaponCatalog.TryGet kullanılır.
        /// </summary>
        public Func<string, WeaponDefinitionData> WeaponResolver { get; set; }

        /// <summary>true ise mermi tükenmez (antrenman modu).</summary>
        public bool InfiniteAmmo
        {
            get => _infiniteAmmo;
            set
            {
                if (_infiniteAmmo == value)
                    return;

                _infiniteAmmo = value;
                for (var i = 0; i < _weapons.Length; i++)
                {
                    if (_weapons[i] != null)
                        _weapons[i].AmmoSource = value ? null : this;
                }

                RaiseChanged();
            }
        }

        public int SlotCount => WeaponSlotCount;
        public int ActiveSlot => _activeSlot;
        public WeaponRuntimeService ActiveWeapon => _activeSlot >= 0 && _activeSlot < WeaponSlotCount ? _weapons[_activeSlot] : null;

        public bool HasAnyWeapon
        {
            get
            {
                for (var i = 0; i < _weapons.Length; i++)
                {
                    if (_weapons[i] != null)
                        return true;
                }

                return false;
            }
        }

        /// <summary>Şarjörde ya da yedekte mermisi olan en az bir silah var mı?</summary>
        public bool HasUsableWeapon
        {
            get
            {
                for (var i = 0; i < _weapons.Length; i++)
                {
                    if (IsUsable(_weapons[i]))
                        return true;
                }

                return false;
            }
        }

        public ArmorPiece Vest => _vest;
        public ArmorPiece Helmet => _helmet;
        public int BackpackLevel => _backpackLevel;
        public string BackpackItemId => ItemCatalog.BackpackId(_backpackLevel);

        public float Capacity
        {
            get
            {
                var capacity = BaseCapacity;
                if (_vest != null)
                    capacity += VestCapacityBonus;

                var backpack = ItemCatalog.Get(ItemCatalog.BackpackId(_backpackLevel));
                if (backpack != null)
                    capacity += backpack.Capacity;

                return capacity;
            }
        }

        public float CurrentWeight => _weight;
        public float FreeCapacity => Math.Max(0f, Capacity - _weight);

        /// <summary>Doluluk oranı (0..1+, HUD/envanter çubuğu için). Çanta bırakılınca 1'i aşabilir.</summary>
        public float LoadFraction
        {
            get
            {
                var capacity = Capacity;
                return capacity > 0f ? _weight / capacity : 0f;
            }
        }

        /// <summary>Taşınan yük kapasiteyi aşıyor mu (ör. çanta bırakıldı)? Bu durumda yeni yığın alınamaz.</summary>
        public bool IsOverweight => _weight > Capacity + (float)WeightEpsilon;

        public WeaponRuntimeService GetWeapon(int slot) => slot >= 0 && slot < WeaponSlotCount ? _weapons[slot] : null;

        /// <summary>Silahın bulunduğu yuva; yoksa -1.</summary>
        public int FindWeaponSlot(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId))
                return -1;

            for (var i = 0; i < _weapons.Length; i++)
            {
                if (_weapons[i] != null && string.Equals(_weapons[i].WeaponId, weaponId, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        /// <summary>-1 = silahsız (yumruk). Boş yuva seçilemez (false).</summary>
        public bool SetActiveSlot(int slot)
        {
            if (slot < -1 || slot >= WeaponSlotCount)
                return false;

            if (slot >= 0 && _weapons[slot] == null)
                return false;

            if (slot == _activeSlot)
                return true;

            HolsterSafe(ActiveWeapon);
            _activeSlot = slot;
            EquipSafe(ActiveWeapon);
            RaiseChanged();
            return true;
        }

        /// <summary>Fare tekerleği: dolu yuvalar arasında yönüne göre döner (silahsızsa ilk doluya geçer).</summary>
        public bool CycleWeapon(int direction)
        {
            if (!HasAnyWeapon)
                return false;

            var step = direction < 0 ? -1 : 1;
            var slot = _activeSlot < 0 ? (step > 0 ? -1 : WeaponSlotCount) : _activeSlot;
            for (var i = 0; i < WeaponSlotCount; i++)
            {
                slot += step;
                if (slot >= WeaponSlotCount)
                    slot = 0;
                else if (slot < 0)
                    slot = WeaponSlotCount - 1;

                if (_weapons[slot] != null)
                    return SetActiveSlot(slot);
            }

            return false;
        }

        /// <summary>
        /// Botlar için: mermisi olan en yüksek kademeli silahı (ana silahlar önce) kuşanır. Kullanılabilir silah yoksa
        /// herhangi bir silahı kuşanır. Hiç silah yoksa false.
        /// </summary>
        public bool SelectBestWeapon()
        {
            var best = -1;
            var bestScore = int.MinValue;
            for (var i = 0; i < WeaponSlotCount; i++)
            {
                var weapon = _weapons[i];
                if (weapon == null)
                    continue;

                var score = ItemCatalog.WeaponTier(weapon.WeaponId) * 10 + (i == SidearmSlot ? 0 : 5) + (IsUsable(weapon) ? 1000 : 0);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }

            return best >= 0 && SetActiveSlot(best);
        }

        public int GetCount(string itemId)
        {
            var index = ItemCatalog.GetNetworkIndex(itemId);
            return index >= 0 && index < _counts.Length ? _counts[index] : 0;
        }

        /// <summary>Çıktıyı temizler ve sıfırdan büyük yığınları katalog sırasıyla doldurur.</summary>
        public void GetStacks(List<KeyValuePair<string, int>> output)
        {
            if (output == null)
                return;

            output.Clear();
            var all = ItemCatalog.All;
            for (var i = 0; i < _counts.Length; i++)
            {
                if (_counts[i] > 0)
                    output.Add(new KeyValuePair<string, int>(all[i].Id, _counts[i]));
            }
        }

        /// <summary>Bu eşyadan kapasiteye sığabilecek en fazla adet (ağırlıksızsa int.MaxValue).</summary>
        public int MaxAddable(string itemId)
        {
            var definition = ItemCatalog.Get(itemId);
            if (definition == null || !definition.IsStackable)
                return 0;

            return MaxAddable(definition);
        }

        /// <summary>Yerdeki eşyayı almaya çalışır. Yerine düşen eşyalar 'dropped'a eklenir.</summary>
        public PickupResult TryPickup(LootItemData item, List<LootItemData> dropped)
        {
            if (!item.IsValid)
                return PickupResult.Rejected;

            if (!ItemCatalog.TryGet(item.ItemId, out var definition))
            {
                // Katalogda olmayan ama silah tanımı bulunan eşya (ileride eklenen silahlar).
                return item.Category == ItemCategory.Weapon ? PickupWeapon(item, item.ItemId, dropped) : PickupResult.Rejected;
            }

            switch (definition.Category)
            {
                case ItemCategory.Weapon:
                    return PickupWeapon(item, definition.WeaponId ?? definition.Id, dropped);
                case ItemCategory.Armor:
                case ItemCategory.Helmet:
                    return PickupArmor(item, definition, dropped);
                case ItemCategory.Backpack:
                    return PickupBackpack(definition, dropped);
                case ItemCategory.Attachment:
                    return PickupAttachment(definition);
                default:
                    return definition.IsStackable ? PickupStack(item, definition) : PickupResult.Rejected;
            }
        }

        /// <summary>Bot yağma kararları için: bu eşyayı almak faydalı mı?</summary>
        public bool WantsItem(LootItemData item)
        {
            if (!item.IsValid)
                return false;

            if (!ItemCatalog.TryGet(item.ItemId, out var definition))
                return false;

            switch (definition.Category)
            {
                case ItemCategory.Weapon:
                    return WantsWeapon(definition);

                case ItemCategory.Armor:
                case ItemCategory.Helmet:
                    return ShouldEquipArmor(definition, ResolveDurability(item, definition));

                case ItemCategory.Backpack:
                    return definition.Level > _backpackLevel;

                case ItemCategory.Attachment:
                    return FindAttachTarget(definition.Id) != null;

                case ItemCategory.Ammunition:
                    return WantsAmmo(definition);

                case ItemCategory.Medical:
                case ItemCategory.Boost:
                case ItemCategory.Throwable:
                    return GetCount(definition.Id) < DesiredCount(definition.Id) && MaxAddable(definition) > 0;

                default:
                    return false;
            }
        }

        public bool Consume(string itemId, int count = 1)
        {
            if (count <= 0)
                return false;

            var index = ItemCatalog.GetNetworkIndex(itemId);
            if (index < 0 || index >= _counts.Length || _counts[index] < count)
                return false;

            _counts[index] -= count;
            RecalculateWeight();
            RaiseChanged();
            return true;
        }

        /// <summary>Yuvadaki silahı çıkarır (şarjördeki mermi korunur). Boş yuvada geçersiz veri döner.</summary>
        public LootItemData DropWeapon(int slot)
        {
            var weapon = GetWeapon(slot);
            if (weapon == null)
                return default;

            var loot = ToLoot(weapon);
            _weapons[slot] = null;
            if (_activeSlot == slot)
                _activeSlot = -1;

            RaiseChanged();
            return loot;
        }

        public bool TryDropStack(string itemId, int quantity, out LootItemData dropped)
        {
            dropped = default;
            if (quantity <= 0)
                return false;

            var index = ItemCatalog.GetNetworkIndex(itemId);
            if (index < 0 || index >= _counts.Length || _counts[index] <= 0)
                return false;

            var amount = Math.Min(quantity, _counts[index]);
            _counts[index] -= amount;
            dropped = ItemCatalog.CreateLoot(itemId, amount);
            RecalculateWeight();
            RaiseChanged();
            return true;
        }

        /// <summary>Kuşanılan zırhı/kaskı/çantayı çıkarır (envanter ekranından bırakma).</summary>
        public bool TryDropEquipment(ItemCategory category, out LootItemData dropped)
        {
            dropped = default;
            switch (category)
            {
                case ItemCategory.Armor:
                    if (_vest == null)
                        return false;
                    dropped = ItemCatalog.CreateArmorLoot(_vest.ItemId, _vest.Durability);
                    _vest = null;
                    break;
                case ItemCategory.Helmet:
                    if (_helmet == null)
                        return false;
                    dropped = ItemCatalog.CreateArmorLoot(_helmet.ItemId, _helmet.Durability);
                    _helmet = null;
                    break;
                case ItemCategory.Backpack:
                    if (_backpackLevel <= 0)
                        return false;
                    dropped = ItemCatalog.CreateLoot(ItemCatalog.BackpackId(_backpackLevel), 1);
                    _backpackLevel = 0;
                    break;
                default:
                    return false;
            }

            RaiseChanged();
            return true;
        }

        /// <summary>Ölümde her şeyi yere bırakılacak eşyalar olarak çıkarır ve envanteri boşaltır.</summary>
        public void DropAll(List<LootItemData> output)
        {
            for (var i = 0; i < WeaponSlotCount; i++)
            {
                var weapon = _weapons[i];
                if (weapon == null)
                    continue;

                var loot = ToLoot(weapon);
                if (output != null && loot.IsValid)
                    output.Add(loot);

                if (output != null)
                    AppendAttachmentLoot(weapon, output);

                _weapons[i] = null;
            }

            if (_vest != null)
            {
                if (output != null && !_vest.IsBroken)
                    output.Add(ItemCatalog.CreateArmorLoot(_vest.ItemId, _vest.Durability));
                _vest = null;
            }

            if (_helmet != null)
            {
                if (output != null && !_helmet.IsBroken)
                    output.Add(ItemCatalog.CreateArmorLoot(_helmet.ItemId, _helmet.Durability));
                _helmet = null;
            }

            if (_backpackLevel > 0)
            {
                var backpack = ItemCatalog.CreateLoot(ItemCatalog.BackpackId(_backpackLevel), 1);
                if (output != null && backpack.IsValid)
                    output.Add(backpack);
                _backpackLevel = 0;
            }

            var all = ItemCatalog.All;
            for (var i = 0; i < _counts.Length; i++)
            {
                if (_counts[i] <= 0)
                    continue;

                if (output != null)
                    output.Add(ItemCatalog.CreateLoot(all[i].Id, _counts[i]));
                _counts[i] = 0;
            }

            _activeSlot = -1;
            _weight = 0f;
            RaiseChanged();
        }

        /// <summary>Envanteri tamamen boşaltır (yere bir şey düşürmez).</summary>
        public void Clear()
        {
            DropAll(null);
        }

        /// <summary>Kapasiteyi yok sayarak silah verir (antrenman/test). Aktif yuvayı değiştirmez (kuşanmaz).</summary>
        public WeaponRuntimeService GiveWeapon(string weaponId, bool fullMagazine)
        {
            if (!TryResolveWeapon(weaponId, out var definition))
                return null;

            var slot = TargetSlotFor(definition);
            HolsterSafe(_weapons[slot]);

            var weapon = CreateWeapon(definition, fullMagazine ? -1 : 0);
            _weapons[slot] = weapon;
            if (slot == _activeSlot)
                EquipSafe(weapon);

            RaiseChanged();
            return weapon;
        }

        /// <summary>Kapasiteyi yok sayarak eşya verir (antrenman/test).</summary>
        public void GiveItem(string itemId, int quantity)
        {
            if (quantity <= 0 || !ItemCatalog.TryGet(itemId, out var definition))
                return;

            switch (definition.Category)
            {
                case ItemCategory.Weapon:
                    GiveWeapon(definition.WeaponId ?? definition.Id, true);
                    return;
                case ItemCategory.Armor:
                case ItemCategory.Helmet:
                    EquipArmor(definition.Id);
                    return;
                case ItemCategory.Backpack:
                    EquipBackpack(definition.Level);
                    return;
                case ItemCategory.Attachment:
                    PickupAttachment(definition);
                    return;
            }

            if (!definition.IsStackable)
                return;

            var index = ItemCatalog.GetNetworkIndex(definition.Id);
            if (index < 0 || index >= _counts.Length)
                return;

            var total = (long)_counts[index] + quantity;
            _counts[index] = total > int.MaxValue ? int.MaxValue : (int)total;
            RecalculateWeight();
            RaiseChanged();
        }

        /// <summary>Zırh/kask kuşandırır (kurallar ve kapasite yok sayılır; eskisi atılır). durability &lt; 0 → yeni.</summary>
        public bool EquipArmor(string itemId, float durability = -1f)
        {
            var definition = ItemCatalog.Get(itemId);
            if (definition == null || (definition.Category != ItemCategory.Armor && definition.Category != ItemCategory.Helmet))
                return false;

            var piece = CreateArmorPiece(definition, durability < 0f ? definition.Durability : durability);
            if (definition.Category == ItemCategory.Armor)
                _vest = piece;
            else
                _helmet = piece;

            RaiseChanged();
            return true;
        }

        /// <summary>Çanta kuşandırır (0 = çantasız).</summary>
        public void EquipBackpack(int level)
        {
            level = level < 0 ? 0 : level > 3 ? 3 : level;
            if (_backpackLevel == level)
                return;

            _backpackLevel = level;
            RaiseChanged();
        }

        /// <summary>
        /// Görev teçhizatını uygular: silahlar (tam şarjör), yelek/kask/çanta, eşyalar; ilk dolu yuvayı kuşanır.
        /// Changed tek sefer tetiklenir.
        /// </summary>
        public void ApplyLoadout(Loadout loadout, bool clearFirst = true)
        {
            if (loadout == null)
                return;

            BeginBatch();
            try
            {
                if (clearFirst)
                    Clear();

                if (!string.IsNullOrEmpty(loadout.PrimaryWeaponId))
                    GiveWeapon(loadout.PrimaryWeaponId, true);

                if (!string.IsNullOrEmpty(loadout.SecondaryWeaponId))
                    GiveWeapon(loadout.SecondaryWeaponId, true);

                if (!string.IsNullOrEmpty(loadout.SidearmId))
                    GiveWeapon(loadout.SidearmId, true);

                if (loadout.VestLevel > 0)
                    EquipArmor(ItemCatalog.VestId(Math.Min(3, loadout.VestLevel)));

                if (loadout.HelmetLevel > 0)
                    EquipArmor(ItemCatalog.HelmetId(Math.Min(3, loadout.HelmetLevel)));

                if (loadout.BackpackLevel > 0)
                    EquipBackpack(loadout.BackpackLevel);

                var items = loadout.Items;
                for (var i = 0; i < items.Count; i++)
                    GiveItem(items[i].Key, items[i].Value);

                for (var i = 0; i < WeaponSlotCount; i++)
                {
                    if (_weapons[i] != null)
                    {
                        SetActiveSlot(i);
                        break;
                    }
                }
            }
            finally
            {
                EndBatch();
            }
        }

        /// <summary>Verilen cana göre en uygun iyileştirme eşyası id'si (yoksa null).</summary>
        public string BestHealItem(float currentHealth)
        {
            var bandage = ItemCatalog.Get(ItemIds.Bandage);
            var firstAid = ItemCatalog.Get(ItemIds.FirstAid);
            var medKit = ItemCatalog.Get(ItemIds.MedKit);

            var hasBandage = GetCount(ItemIds.Bandage) > 0 && bandage != null && currentHealth < bandage.HealCap;
            var hasFirstAid = GetCount(ItemIds.FirstAid) > 0 && firstAid != null && currentHealth < firstAid.HealCap;
            var hasMedKit = GetCount(ItemIds.MedKit) > 0 && medKit != null && currentHealth < medKit.HealCap;

            if (bandage != null && currentHealth >= bandage.HealCap)
                return hasMedKit ? ItemIds.MedKit : null;

            // Az eksik can: sargı bezi yeterli.
            if (currentHealth >= 50f)
            {
                if (hasBandage)
                    return ItemIds.Bandage;
                if (hasFirstAid)
                    return ItemIds.FirstAid;
                return hasMedKit ? ItemIds.MedKit : null;
            }

            // Ağır yaralı: ilk yardım > sıhhiye çantası > sargı bezi.
            if (hasFirstAid)
                return ItemIds.FirstAid;
            if (hasMedKit)
                return ItemIds.MedKit;
            return hasBandage ? ItemIds.Bandage : null;
        }

        /// <summary>En uygun boost eşyası id'si (yoksa null).</summary>
        public string BestBoostItem() => BestBoostItem(0f);

        /// <summary>Mevcut boost değerine göre en az israf eden boost eşyası (bar doluysa null).</summary>
        public string BestBoostItem(float currentBoost)
        {
            if (currentBoost >= BoostService.Max - 0.5f)
                return null;

            var hasEnergy = GetCount(ItemIds.EnergyDrink) > 0;
            var hasPainkiller = GetCount(ItemIds.Painkiller) > 0;
            if (!hasEnergy && !hasPainkiller)
                return null;

            var painkiller = ItemCatalog.Get(ItemIds.Painkiller);
            var deficit = BoostService.Max - currentBoost;
            var painkillerFits = painkiller == null || deficit >= painkiller.BoostAmount - 5f;

            if (hasPainkiller && (painkillerFits || !hasEnergy))
                return ItemIds.Painkiller;

            return hasEnergy ? ItemIds.EnergyDrink : ItemIds.Painkiller;
        }

        // ------------------------------------------------------------------ IInventory (eski API)

        /// <summary>Yalnızca boş yere ekler (silah/teçhizat değiştirmez, yığın kapasiteye sığmalı).</summary>
        public bool TryAddItem(string itemId, ItemCategory category)
        {
            if (!ItemCatalog.TryGet(itemId, out var definition))
                return false;

            switch (definition.Category)
            {
                case ItemCategory.Weapon:
                {
                    if (!TryResolveWeapon(definition.WeaponId ?? definition.Id, out var weaponDefinition))
                        return false;

                    if (_weapons[TargetSlotFor(weaponDefinition)] != null)
                        return false;

                    return PickupWeapon(ItemCatalog.CreateWeaponLoot(definition.Id), definition.WeaponId ?? definition.Id, null).Accepted;
                }
                case ItemCategory.Armor:
                    if (_vest != null && !_vest.IsBroken)
                        return false;
                    return EquipArmor(definition.Id);
                case ItemCategory.Helmet:
                    if (_helmet != null && !_helmet.IsBroken)
                        return false;
                    return EquipArmor(definition.Id);
                case ItemCategory.Backpack:
                    if (_backpackLevel >= definition.Level)
                        return false;
                    EquipBackpack(definition.Level);
                    return true;
                default:
                    return definition.IsStackable && PickupStack(ItemCatalog.CreateLoot(definition.Id, 1), definition).Accepted;
            }
        }

        public bool TryRemoveItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return false;

            if (GetCount(itemId) > 0)
                return Consume(itemId, 1);

            var slot = FindWeaponSlot(itemId);
            if (slot >= 0)
                return DropWeapon(slot).IsValid;

            if (_vest != null && _vest.ItemId == itemId)
                return TryDropEquipment(ItemCategory.Armor, out _);

            if (_helmet != null && _helmet.ItemId == itemId)
                return TryDropEquipment(ItemCategory.Helmet, out _);

            if (_backpackLevel > 0 && ItemCatalog.BackpackId(_backpackLevel) == itemId)
                return TryDropEquipment(ItemCategory.Backpack, out _);

            return false;
        }

        public bool HasItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return false;

            if (GetCount(itemId) > 0 || FindWeaponSlot(itemId) >= 0)
                return true;

            if (_vest != null && _vest.ItemId == itemId)
                return true;

            if (_helmet != null && _helmet.ItemId == itemId)
                return true;

            return _backpackLevel > 0 && ItemCatalog.BackpackId(_backpackLevel) == itemId;
        }

        // ------------------------------------------------------------------ IAmmoSource

        public int GetAmmo(AmmoType type)
        {
            if (type == AmmoType.None)
                return 0;

            if (_infiniteAmmo)
                return InfiniteAmmoReport;

            return GetCount(ItemCatalog.AmmoItemId(type));
        }

        public int TakeAmmo(AmmoType type, int maxAmount)
        {
            if (maxAmount <= 0 || type == AmmoType.None)
                return 0;

            if (_infiniteAmmo)
                return maxAmount;

            var index = ItemCatalog.GetNetworkIndex(ItemCatalog.AmmoItemId(type));
            if (index < 0 || index >= _counts.Length || _counts[index] <= 0)
                return 0;

            var amount = Math.Min(maxAmount, _counts[index]);
            _counts[index] -= amount;
            RecalculateWeight();
            RaiseChanged();
            return amount;
        }

        // ------------------------------------------------------------------ IArmorProvider

        /// <summary>Kafa → kask, gövde → yelek. Kırık ya da olmayan zırh için null.</summary>
        public ArmorPiece GetArmorFor(BodyPart part)
        {
            switch (part)
            {
                case BodyPart.Head:
                    return _helmet != null && !_helmet.IsBroken ? _helmet : null;
                case BodyPart.Torso:
                    return _vest != null && !_vest.IsBroken ? _vest : null;
                default:
                    return null;
            }
        }

        // ------------------------------------------------------------------ İç işleyiş

        private PickupResult PickupWeapon(LootItemData item, string weaponId, List<LootItemData> dropped)
        {
            if (!TryResolveWeapon(weaponId, out var definition))
                return PickupResult.Rejected;

            var slot = TargetSlotFor(definition);
            var old = _weapons[slot];
            if (old != null)
            {
                var oldLoot = ToLoot(old);
                if (dropped != null && oldLoot.IsValid)
                    dropped.Add(oldLoot);
                if (dropped != null)
                    AppendAttachmentLoot(old, dropped);
            }

            _weapons[slot] = CreateWeapon(definition, item.LoadedAmmo);
            if (_activeSlot < 0)
                _activeSlot = slot;

            if (_activeSlot == slot)
                EquipSafe(_weapons[slot]);

            RaiseChanged();
            return new PickupResult(true, 1, true);
        }

        /// <summary>Eklentiyi takabileceği (uyumlu + yuvası boş) silah: önce aktif, sonra diğerleri.</summary>
        private WeaponRuntimeService FindAttachTarget(string attachmentId)
        {
            var active = ActiveWeapon;
            if (active != null && active.CanAttach(attachmentId))
                return active;

            for (var i = 0; i < WeaponSlotCount; i++)
            {
                var w = _weapons[i];
                if (w != null && w != active && w.CanAttach(attachmentId))
                    return w;
            }

            return null;
        }

        private PickupResult PickupAttachment(ItemDefinition definition)
        {
            var target = FindAttachTarget(definition.Id);
            if (target == null || !target.TryAttach(definition.Id, out _))
                return PickupResult.Rejected;

            RaiseChanged();
            return new PickupResult(true, 1, true);
        }

        /// <summary>Aktif silahtan eklenti çıkarır ve yere bırakılacak veriyi verir.</summary>
        public bool TryDetachFromActive(AttachmentSlot slot, out LootItemData dropped)
        {
            dropped = default;
            var weapon = ActiveWeapon;
            var id = weapon?.Detach(slot);
            if (id == null)
                return false;

            dropped = ItemCatalog.CreateLoot(id, 1);
            RaiseChanged();
            return dropped.IsValid;
        }

        private static void AppendAttachmentLoot(WeaponRuntimeService weapon, List<LootItemData> output)
        {
            for (var s = 0; s < AttachmentCatalog.SlotCount; s++)
            {
                var id = weapon.GetAttachment((AttachmentSlot)s);
                if (id == null)
                    continue;

                var loot = ItemCatalog.CreateLoot(id, 1);
                if (loot.IsValid)
                    output.Add(loot);
            }
        }

        private PickupResult PickupArmor(LootItemData item, ItemDefinition definition, List<LootItemData> dropped)
        {
            var durability = ResolveDurability(item, definition);
            if (!ShouldEquipArmor(definition, durability))
                return PickupResult.Rejected;

            var isVest = definition.Category == ItemCategory.Armor;
            var current = isVest ? _vest : _helmet;
            if (current != null && !current.IsBroken && dropped != null)
                dropped.Add(ItemCatalog.CreateArmorLoot(current.ItemId, current.Durability));

            var piece = CreateArmorPiece(definition, durability);
            if (isVest)
                _vest = piece;
            else
                _helmet = piece;

            RaiseChanged();
            return new PickupResult(true, 1, true);
        }

        private PickupResult PickupBackpack(ItemDefinition definition, List<LootItemData> dropped)
        {
            if (definition.Level <= _backpackLevel)
                return PickupResult.Rejected;

            if (_backpackLevel > 0 && dropped != null)
            {
                var old = ItemCatalog.CreateLoot(ItemCatalog.BackpackId(_backpackLevel), 1);
                if (old.IsValid)
                    dropped.Add(old);
            }

            _backpackLevel = definition.Level;
            RaiseChanged();
            return new PickupResult(true, 1, true);
        }

        private PickupResult PickupStack(LootItemData item, ItemDefinition definition)
        {
            var index = ItemCatalog.GetNetworkIndex(definition.Id);
            if (index < 0 || index >= _counts.Length)
                return PickupResult.Rejected;

            var take = Math.Min(item.Quantity, MaxAddable(definition));
            if (take <= 0)
                return PickupResult.Rejected;

            var total = (long)_counts[index] + take;
            _counts[index] = total > int.MaxValue ? int.MaxValue : (int)total;
            RecalculateWeight();
            RaiseChanged();
            return new PickupResult(true, take, take >= item.Quantity);
        }

        private int MaxAddable(ItemDefinition definition)
        {
            if (definition.Weight <= 0f)
                return int.MaxValue;

            var free = (double)Capacity - _weight;
            if (free <= 0.0)
                return 0;

            var units = Math.Floor(free / definition.Weight + WeightEpsilon);
            return units >= int.MaxValue ? int.MaxValue : (int)units;
        }

        private bool ShouldEquipArmor(ItemDefinition definition, float durability)
        {
            if (durability <= 0f)
                return false;

            var current = definition.Category == ItemCategory.Armor ? _vest : _helmet;
            if (current == null || current.IsBroken)
                return true;

            if (definition.Level != current.Level)
                return definition.Level > current.Level;

            return durability > current.Durability + 0.01f;
        }

        private static float ResolveDurability(LootItemData item, ItemDefinition definition)
        {
            if (item.Durability < 0f)
                return definition.Durability;

            return item.Durability > definition.Durability ? definition.Durability : item.Durability;
        }

        private static ArmorPiece CreateArmorPiece(ItemDefinition definition, float durability) =>
            new(definition.Id, definition.Level, definition.Durability, definition.DamageReduction, durability);

        private bool WantsWeapon(ItemDefinition definition)
        {
            var weaponId = definition.WeaponId ?? definition.Id;
            if (!TryResolveWeapon(weaponId, out var weaponDefinition))
                return false;

            if (!HasAnyWeapon)
                return true;

            if (FindWeaponSlot(weaponId) >= 0)
                return false;

            var slot = TargetSlotFor(weaponDefinition);
            var existing = _weapons[slot];
            if (existing == null)
                return true;

            // Değiştirilecek silahtan daha iyi mi? Mermisi olmayan silah yerine mermisi olanı tercih et.
            var newTier = definition.Level;
            var oldTier = ItemCatalog.WeaponTier(existing.WeaponId);
            var newHasAmmo = GetCount(ItemCatalog.AmmoItemId(definition.AmmoType)) > 0;
            if (!IsUsable(existing) && newHasAmmo)
                return true;

            return newTier > oldTier && (newHasAmmo || newTier - oldTier >= 2);
        }

        private bool WantsAmmo(ItemDefinition definition)
        {
            var usesIt = false;
            for (var i = 0; i < _weapons.Length; i++)
            {
                var weapon = _weapons[i];
                if (weapon != null && weapon.Definition != null && weapon.Definition.AmmoType == definition.AmmoType)
                {
                    usesIt = true;
                    break;
                }
            }

            if (!usesIt)
                return false;

            return GetCount(definition.Id) < DesiredCount(definition.Id) && MaxAddable(definition) > 0;
        }

        /// <summary>Botların taşımak istediği hedef adet.</summary>
        private static int DesiredCount(string itemId)
        {
            switch (itemId)
            {
                case ItemIds.Ammo9: return 150;
                case ItemIds.Ammo556: return 240;
                case ItemIds.Ammo762: return 240;
                case ItemIds.Ammo12: return 40;
                case ItemIds.Bandage: return 15;
                case ItemIds.FirstAid: return 5;
                case ItemIds.MedKit: return 2;
                case ItemIds.EnergyDrink: return 5;
                case ItemIds.Painkiller: return 3;
                case ItemIds.FragGrenade: return 4;
                case ItemIds.SmokeGrenade: return 3;
                default: return 0;
            }
        }

        private int TargetSlotFor(WeaponDefinitionData definition)
        {
            if (definition.IsSidearm)
                return SidearmSlot;

            if (_weapons[PrimarySlotA] == null)
                return PrimarySlotA;

            if (_weapons[PrimarySlotB] == null)
                return PrimarySlotB;

            return _activeSlot == PrimarySlotB ? PrimarySlotB : PrimarySlotA;
        }

        private bool TryResolveWeapon(string weaponId, out WeaponDefinitionData definition)
        {
            definition = null;
            if (string.IsNullOrEmpty(weaponId))
                return false;

            var resolver = WeaponResolver;
            if (resolver != null)
            {
                definition = resolver(weaponId);
                return definition != null;
            }

            return WeaponCatalog.TryGet(weaponId, out definition) && definition != null;
        }

        private WeaponRuntimeService CreateWeapon(WeaponDefinitionData definition, int loadedAmmo)
        {
            if (loadedAmmo > definition.MagazineSize)
                loadedAmmo = definition.MagazineSize;

            return new WeaponRuntimeService(definition, _eventBus, loadedAmmo < -1 ? 0 : loadedAmmo)
            {
                OwnerId = OwnerId,
                AmmoSource = _infiniteAmmo ? null : this
            };
        }

        private bool IsUsable(WeaponRuntimeService weapon)
        {
            if (weapon == null)
                return false;

            if (weapon.CurrentAmmo > 0 || _infiniteAmmo)
                return true;

            var ammoType = weapon.Definition != null ? weapon.Definition.AmmoType : AmmoType.None;
            return ammoType == AmmoType.None || GetAmmo(ammoType) > 0;
        }

        private static LootItemData ToLoot(WeaponRuntimeService weapon)
        {
            HolsterSafe(weapon);
            var loaded = Math.Max(0, weapon.CurrentAmmo);
            var loot = ItemCatalog.CreateWeaponLoot(weapon.WeaponId, loaded);
            if (loot.IsValid)
                return loot;

            var name = weapon.Definition != null ? weapon.Definition.DisplayName : weapon.WeaponId;
            return string.IsNullOrEmpty(weapon.WeaponId)
                ? default
                : new LootItemData(weapon.WeaponId, ItemCategory.Weapon, name, 1, loaded, -1f);
        }

        /// <summary>Elden bırakılan silah: şarjör değiştirme/burst iptal.</summary>
        private static void HolsterSafe(WeaponRuntimeService weapon)
        {
            weapon?.Holster();
        }

        /// <summary>Ele alınan silah: EquipSeconds boyunca ateş edilemez (otorite tarafında kuşanma gecikmesi).</summary>
        private static void EquipSafe(WeaponRuntimeService weapon)
        {
            weapon?.BeginEquip();
        }

        private void RecalculateWeight()
        {
            var all = ItemCatalog.All;
            double weight = 0.0;
            for (var i = 0; i < _counts.Length; i++)
            {
                if (_counts[i] > 0)
                    weight += (double)_counts[i] * all[i].Weight;
            }

            _weight = (float)weight;
        }

        private void BeginBatch()
        {
            _batchDepth++;
        }

        private void EndBatch()
        {
            if (_batchDepth > 0)
                _batchDepth--;

            if (_batchDepth == 0 && _changedPending)
            {
                _changedPending = false;
                Changed?.Invoke();
            }
        }

        private void RaiseChanged()
        {
            if (_batchDepth > 0)
            {
                _changedPending = true;
                return;
            }

            Changed?.Invoke();
        }
    }
}
