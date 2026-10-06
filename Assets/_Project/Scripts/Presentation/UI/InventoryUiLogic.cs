using System;
using System.Collections.Generic;
using Project.Core.Domain;

namespace Project.Presentation.UI
{
    /// <summary>Sürüklenen şeyin kaynağı.</summary>
    public enum InventoryDragKind
    {
        None = 0,
        Weapon = 1,
        Gear = 2,
        Stack = 3,
        Ground = 4
    }

    /// <summary>Bırakma bölgesi.</summary>
    public enum InventoryDropZone
    {
        None = 0,
        /// <summary>"BIRAK" bölgesi (yere at).</summary>
        Drop = 1,
        /// <summary>"EL" bölgesi (kuşan / kullan).</summary>
        Hand = 2,
        /// <summary>Sırt çantası (yerden al).</summary>
        Backpack = 3
    }

    /// <summary>Sürükle-bırakın sonucunda yapılacak iş.</summary>
    public enum InventoryDropIntent
    {
        None = 0,
        EquipWeapon = 1,
        DropWeapon = 2,
        DropGear = 3,
        DropStack = 4,
        UseStack = 5,
        PickupGround = 6
    }

    /// <summary>Hızlı eylem türü (seçili öğe için).</summary>
    public enum InventoryQuickAction
    {
        Primary = 0,
        DropOne = 1,
        DropHalf = 2,
        DropAll = 3
    }

    /// <summary>Yerdeki eşya satırı (mesafeye göre sıralanır).</summary>
    public struct InventoryGroundEntry
    {
        public int SpawnId;
        public float Distance;
        public InventoryRarity Rarity;
    }

    /// <summary>
    /// Envanter ekranının saf mantığı (Unity sahnesi gerektirmez, EditMode'da sınanır): sürükle-bırak kararı,
    /// ızgara gezinmesi, yığın bölme, yerdeki eşya sıralaması, kalibre özeti ve dayanıklılık durumu.
    /// </summary>
    public static class InventoryUiLogic
    {
        public const int GridColumns = 5;

        /// <summary>Sürüklenen kaynak bu bölgeye bırakılınca ne yapılır?</summary>
        public static InventoryDropIntent ResolveDrop(InventoryDragKind kind, InventoryDropZone zone, bool usable = false)
        {
            switch (zone)
            {
                case InventoryDropZone.Drop:
                    switch (kind)
                    {
                        case InventoryDragKind.Weapon: return InventoryDropIntent.DropWeapon;
                        case InventoryDragKind.Gear: return InventoryDropIntent.DropGear;
                        case InventoryDragKind.Stack: return InventoryDropIntent.DropStack;
                    }

                    break;
                case InventoryDropZone.Hand:
                    switch (kind)
                    {
                        case InventoryDragKind.Weapon: return InventoryDropIntent.EquipWeapon;
                        case InventoryDragKind.Stack: return usable ? InventoryDropIntent.UseStack : InventoryDropIntent.None;
                    }

                    break;
                case InventoryDropZone.Backpack:
                    if (kind == InventoryDragKind.Ground)
                        return InventoryDropIntent.PickupGround;
                    break;
            }

            return InventoryDropIntent.None;
        }

        /// <summary>Hızlı eylem için bırakılacak adet: bir paket (<paramref name="unit"/>) / yarısı (yukarı yuvarlanır) / hepsi; en az 1, en çok yığın.</summary>
        public static int SplitQuantity(int count, InventoryQuickAction action, int unit = 1)
        {
            if (count <= 0)
                return 0;
            switch (action)
            {
                case InventoryQuickAction.DropOne: return Math.Min(count, Math.Max(1, unit));
                case InventoryQuickAction.DropHalf: return Math.Max(1, (count + 1) / 2);
                case InventoryQuickAction.DropAll: return count;
                default: return Math.Min(count, 1);
            }
        }

        /// <summary>Izgarada yön tuşu: sütun/satır kayması, kenarda durur; alt satır eksikse son öğeye oturur.</summary>
        public static int NavigateGrid(int index, int count, int columns, int dx, int dy)
        {
            if (count <= 0)
                return -1;
            columns = Math.Max(1, columns);
            index = Math.Max(0, Math.Min(count - 1, index));

            var row = index / columns;
            var col = index % columns;
            var rows = (count + columns - 1) / columns;

            if (dx != 0)
            {
                var next = index + dx;
                if (next < 0 || next >= count)
                    return index;
                // Satır sınırını aşmasın (sağ kenarda sağa, sol kenarda sola gitmez).
                if (next / columns != row)
                    return index;
                return next;
            }

            if (dy != 0)
            {
                var nextRow = Math.Max(0, Math.Min(rows - 1, row + dy));
                var next = Math.Min(count - 1, nextRow * columns + col);
                return next;
            }

            return index;
        }

        /// <summary>Döngüsel liste gezinmesi (yuvalar / yerdeki eşyalar).</summary>
        public static int Wrap(int index, int count, int delta)
        {
            if (count <= 0)
                return -1;
            var next = (index + delta) % count;
            return next < 0 ? next + count : next;
        }

        /// <summary>Yerdeki eşyaları yakından uzağa sıralar (eşitlikte yüksek nadirlik, sonra kimlik; kararlı).</summary>
        public static void SortGround(List<InventoryGroundEntry> entries)
        {
            if (entries == null || entries.Count < 2)
                return;
            entries.Sort(CompareGround);
        }

        private static int CompareGround(InventoryGroundEntry a, InventoryGroundEntry b)
        {
            var byDistance = a.Distance.CompareTo(b.Distance);
            if (byDistance != 0)
                return byDistance;
            var byRarity = b.Rarity.CompareTo(a.Rarity);
            return byRarity != 0 ? byRarity : a.SpawnId.CompareTo(b.SpawnId);
        }

        /// <summary>Kalibre özet sırası (sabit): 9 mm, 5.56, 7.62, 12 kalibre.</summary>
        public static readonly AmmoType[] AmmoOrder = { AmmoType.Mm9, AmmoType.Mm556, AmmoType.Mm762, AmmoType.Gauge12 };

        public static string CaliberLabel(AmmoType type)
        {
            switch (type)
            {
                case AmmoType.Mm9: return "9 mm";
                case AmmoType.Mm556: return "5.56";
                case AmmoType.Mm762: return "7.62";
                case AmmoType.Gauge12: return "12 K";
                default: return "-";
            }
        }

        /// <summary>Dayanıklılık durumu: 0 sağlam, 1 aşınmış (&lt;%30), 2 kırık.</summary>
        public static int DurabilityState(float normalized, bool broken)
        {
            if (broken || normalized <= 0f)
                return 2;
            return normalized < 0.3f ? 1 : 0;
        }

        /// <summary>Yerdeki eşya mesafe metni: "3,4 m".</summary>
        public static string FormatDistance(float meters)
        {
            if (float.IsNaN(meters) || float.IsInfinity(meters) || meters < 0f)
                meters = 0f;
            var tenths = (int)Math.Round(meters * 10f);
            return (tenths / 10) + "," + (tenths % 10) + " m";
        }
    }
}
