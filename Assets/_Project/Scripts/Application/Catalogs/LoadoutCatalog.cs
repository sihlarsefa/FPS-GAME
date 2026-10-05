using System;
using System.Collections.Generic;
using Project.Core.Domain;

namespace Project.Application.Catalogs
{
    /// <summary>Bir tim görevinin başlangıç teçhizatı.</summary>
    public sealed class Loadout
    {
        public TeamRole Role { get; set; }
        public string RoleName { get; set; }
        public string PrimaryWeaponId { get; set; }
        public string SecondaryWeaponId { get; set; }
        public string SidearmId { get; set; }
        public int VestLevel { get; set; }
        public int HelmetLevel { get; set; }
        public int BackpackLevel { get; set; }

        /// <summary>Envantere eklenecek (eşya id, adet) — mermi, tıbbi malzeme, bomba.</summary>
        public List<KeyValuePair<string, int>> Items { get; } = new();

        /// <summary>Listedeki toplam adet (aynı eşya birden fazla satırda olabilir).</summary>
        public int CountOf(string itemId)
        {
            var total = 0;
            for (var i = 0; i < Items.Count; i++)
            {
                if (string.Equals(Items[i].Key, itemId, StringComparison.Ordinal))
                    total += Items[i].Value;
            }

            return total;
        }

        internal Loadout Add(string itemId, int count)
        {
            if (!string.IsNullOrEmpty(itemId) && count > 0)
                Items.Add(new KeyValuePair<string, int>(itemId, count));

            return this;
        }
    }

    /// <summary>
    /// Görev bazlı başlangıç teçhizatları (Tim Komutanı: MPT-76+SAR 9, Keskin Nişancı: JNG-90, Makineli: PMT-76,
    /// Sıhhiyeci: MPT-55 + fazla tıbbi, Telsizci: MPT-55, Bombacı: G3A7 + fazla bomba, Piyade: MPT-76/MPT-55).
    /// Ayrıca 10 kişilik timin görev dağılımını verir.
    /// Her çağrı yeni (değiştirilebilir) bir Loadout örneği döndürür.
    /// </summary>
    public static class LoadoutCatalog
    {
        /// <summary>Standart tim mevcudu.</summary>
        public const int SquadSize = 10;

        private static readonly TeamRole[] SlotRoles =
        {
            TeamRole.Leader,         // 0 Tim Komutanı
            TeamRole.Marksman,       // 1 Keskin Nişancı
            TeamRole.MachineGunner,  // 2 Makineli Tüfekçi
            TeamRole.Medic,          // 3 Sıhhiyeci
            TeamRole.Radioman,       // 4 Telsizci
            TeamRole.Grenadier,      // 5 Bombacı
            TeamRole.Rifleman,       // 6 Piyade
            TeamRole.Rifleman,       // 7 Piyade
            TeamRole.Rifleman,       // 8 Piyade
            TeamRole.Rifleman        // 9 Piyade
        };

        public static Loadout For(TeamRole role) => For(role, 0);

        /// <summary>
        /// Görev teçhizatı; 'variant' yalnızca Piyade için silah seçimini değiştirir (çift → MPT-76, tek → MPT-55).
        /// </summary>
        public static Loadout For(TeamRole role, int variant)
        {
            var loadout = new Loadout
            {
                Role = role,
                RoleName = GetRoleName(role)
            };

            switch (role)
            {
                case TeamRole.Leader:
                    loadout.PrimaryWeaponId = WeaponIds.Mpt76;
                    loadout.SidearmId = WeaponIds.Sar9;
                    loadout.VestLevel = 2;
                    loadout.HelmetLevel = 2;
                    loadout.BackpackLevel = 2;
                    loadout.Add(ItemIds.Ammo762, 150)
                        .Add(ItemIds.Ammo9, 30)
                        .Add(ItemIds.Bandage, 3)
                        .Add(ItemIds.FirstAid, 1)
                        .Add(ItemIds.FragGrenade, 1)
                        .Add(ItemIds.SmokeGrenade, 2);
                    break;

                case TeamRole.Marksman:
                    loadout.PrimaryWeaponId = WeaponIds.Jng90;
                    loadout.SecondaryWeaponId = WeaponIds.Sar109;
                    loadout.VestLevel = 1;
                    loadout.HelmetLevel = 2;
                    loadout.BackpackLevel = 1;
                    loadout.Add(ItemIds.Ammo762, 120)
                        .Add(ItemIds.Ammo9, 120)
                        .Add(ItemIds.Bandage, 3)
                        .Add(ItemIds.FragGrenade, 1)
                        .Add(ItemIds.SmokeGrenade, 1);
                    break;

                case TeamRole.MachineGunner:
                    loadout.PrimaryWeaponId = WeaponIds.Pmt76;
                    loadout.SidearmId = WeaponIds.Sar9;
                    loadout.VestLevel = 2;
                    loadout.HelmetLevel = 1;
                    loadout.BackpackLevel = 2;
                    loadout.Add(ItemIds.Ammo762, 300)
                        .Add(ItemIds.Ammo9, 30)
                        .Add(ItemIds.Bandage, 3)
                        .Add(ItemIds.FragGrenade, 1)
                        .Add(ItemIds.SmokeGrenade, 1);
                    break;

                case TeamRole.Medic:
                    loadout.PrimaryWeaponId = WeaponIds.Mpt55;
                    loadout.SidearmId = WeaponIds.Sar9;
                    loadout.VestLevel = 1;
                    loadout.HelmetLevel = 1;
                    loadout.BackpackLevel = 2;
                    loadout.Add(ItemIds.Ammo556, 150)
                        .Add(ItemIds.Ammo9, 30)
                        .Add(ItemIds.Bandage, 6)
                        .Add(ItemIds.FirstAid, 4)
                        .Add(ItemIds.MedKit, 2)
                        .Add(ItemIds.Painkiller, 1)
                        .Add(ItemIds.FragGrenade, 1)
                        .Add(ItemIds.SmokeGrenade, 2);
                    break;

                case TeamRole.Radioman:
                    loadout.PrimaryWeaponId = WeaponIds.Mpt55;
                    loadout.SidearmId = WeaponIds.Tp9;
                    loadout.VestLevel = 1;
                    loadout.HelmetLevel = 1;
                    loadout.BackpackLevel = 2;
                    loadout.Add(ItemIds.Ammo556, 150)
                        .Add(ItemIds.Ammo9, 30)
                        .Add(ItemIds.Bandage, 3)
                        .Add(ItemIds.EnergyDrink, 1)
                        .Add(ItemIds.FragGrenade, 1)
                        .Add(ItemIds.SmokeGrenade, 2);
                    break;

                case TeamRole.Grenadier:
                    loadout.PrimaryWeaponId = WeaponIds.G3;
                    loadout.SidearmId = WeaponIds.Sar9;
                    loadout.VestLevel = 2;
                    loadout.HelmetLevel = 1;
                    loadout.BackpackLevel = 2;
                    loadout.Add(ItemIds.Ammo762, 150)
                        .Add(ItemIds.Ammo9, 30)
                        .Add(ItemIds.Bandage, 3)
                        .Add(ItemIds.FragGrenade, 4)
                        .Add(ItemIds.SmokeGrenade, 2);
                    break;

                default: // Piyade
                {
                    var useMpt76 = (variant & 1) == 0;
                    loadout.PrimaryWeaponId = useMpt76 ? WeaponIds.Mpt76 : WeaponIds.Mpt55;
                    loadout.SidearmId = WeaponIds.Sar9;
                    loadout.VestLevel = 1;
                    loadout.HelmetLevel = 1;
                    loadout.BackpackLevel = 1;
                    loadout.Add(useMpt76 ? ItemIds.Ammo762 : ItemIds.Ammo556, 150)
                        .Add(ItemIds.Ammo9, 30)
                        .Add(ItemIds.Bandage, 3)
                        .Add(ItemIds.FragGrenade, 1)
                        .Add(ItemIds.SmokeGrenade, 1);
                    break;
                }
            }

            return loadout;
        }

        /// <summary>Timdeki sıra numarasına göre teçhizat (Piyadeler arasında MPT-76/MPT-55 dönüşümlü).</summary>
        public static Loadout ForSlot(int slotIndex)
        {
            var role = RoleForSlot(slotIndex);
            return For(role, slotIndex < 0 ? 0 : slotIndex);
        }

        /// <summary>Timdeki sıra numarasına göre görev (0 = komutan). 10 kişilik tim dağılımı.</summary>
        public static TeamRole RoleForSlot(int slotIndex)
        {
            if (slotIndex < 0)
                return TeamRole.Rifleman;

            return slotIndex < SlotRoles.Length ? SlotRoles[slotIndex] : TeamRole.Rifleman;
        }

        public static string GetRoleName(TeamRole role)
        {
            switch (role)
            {
                case TeamRole.Leader: return "Tim Komutanı";
                case TeamRole.Rifleman: return "Piyade";
                case TeamRole.Marksman: return "Keskin Nişancı";
                case TeamRole.MachineGunner: return "Makineli Tüfekçi";
                case TeamRole.Medic: return "Sıhhiyeci";
                case TeamRole.Radioman: return "Telsizci";
                case TeamRole.Grenadier: return "Bombacı";
                default: return "Asker";
            }
        }

        /// <summary>Kısa görev kodu (HUD tim paneli için).</summary>
        public static string GetRoleShortName(TeamRole role)
        {
            switch (role)
            {
                case TeamRole.Leader: return "KMT";
                case TeamRole.Rifleman: return "PYD";
                case TeamRole.Marksman: return "KN";
                case TeamRole.MachineGunner: return "MAK";
                case TeamRole.Medic: return "SHH";
                case TeamRole.Radioman: return "TEL";
                case TeamRole.Grenadier: return "BMB";
                default: return "AS";
            }
        }
    }
}
