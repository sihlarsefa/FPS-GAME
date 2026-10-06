using System;
using System.Collections.Generic;
using Project.Application.Services;

namespace Project.Presentation.UI
{
    public enum CareerWeaponSort { Kills, HeadshotPercent, Accuracy, Longest }

    /// <summary>Silah tablosu satırı (saf veri).</summary>
    public readonly struct CareerWeaponRow
    {
        public readonly string Id;
        public readonly int Shots, Kills;
        public readonly float HeadshotPercent, Accuracy, LongestMeters;

        public CareerWeaponRow(string id, int shots, int kills, float headshotPercent, float accuracy, float longestMeters)
        {
            Id = id; Shots = shots; Kills = kills; HeadshotPercent = headshotPercent; Accuracy = accuracy; LongestMeters = longestMeters;
        }
    }

    /// <summary>Kariyer paneli saf mantığı: sıralama, madalya ipucu/halka değerleri, harita oranları. Unity'siz test edilir.</summary>
    public static class CareerPanelLogic
    {
        public static List<CareerWeaponRow> BuildWeaponRows(IReadOnlyDictionary<string, WeaponCareer> weapons, CareerWeaponSort sort, bool descending)
        {
            var rows = new List<CareerWeaponRow>();
            if (weapons != null)
            {
                foreach (var kv in weapons)
                {
                    var w = kv.Value;
                    if (w == null || (w.Shots <= 0 && w.Kills <= 0)) continue;
                    var hs = w.Kills > 0 ? Math.Min(1f, (float)w.Headshots / w.Kills) * 100f : 0f;
                    rows.Add(new CareerWeaponRow(kv.Key, w.Shots, w.Kills, hs, w.Accuracy * 100f, w.LongestKillMeters));
                }
            }

            rows.Sort((a, b) =>
            {
                var c = Key(a, sort).CompareTo(Key(b, sort));
                if (descending) c = -c;
                return c != 0 ? c : string.CompareOrdinal(a.Id, b.Id);
            });
            return rows;
        }

        private static float Key(CareerWeaponRow r, CareerWeaponSort s)
        {
            switch (s)
            {
                case CareerWeaponSort.HeadshotPercent: return r.HeadshotPercent;
                case CareerWeaponSort.Accuracy: return r.Accuracy;
                case CareerWeaponSort.Longest: return r.LongestMeters;
                default: return r.Kills;
            }
        }

        public static string TierName(MedalTier tier)
        {
            switch (tier)
            {
                case MedalTier.Bronz: return "Bronz";
                case MedalTier.Gumus: return "Gümüş";
                case MedalTier.Altin: return "Altın";
                default: return "Kilitli";
            }
        }

        /// <summary>Halka dolumu 0..1: kilitliyken bronza, altındayken bir sonraki kademeye ilerleme.</summary>
        public static float RingFill(MedalDefinition d, int value)
        {
            if (d == null) return 0f;
            var tier = ForValue(d, value);
            if (tier == MedalTier.Altin) return 1f;
            var lo = d.Threshold(tier);
            var hi = d.Threshold(tier + 1);
            if (hi <= lo) return 1f;
            return Math.Max(0f, Math.Min(1f, (float)(value - lo) / (hi - lo)));
        }

        public static MedalTier ForValue(MedalDefinition d, int value) =>
            value >= d.Gold ? MedalTier.Altin : value >= d.Silver ? MedalTier.Gumus : value >= d.Bronze ? MedalTier.Bronz : MedalTier.None;

        public static string MedalTooltip(MedalDefinition d, int value)
        {
            if (d == null) return string.Empty;
            var tier = ForValue(d, value);
            var next = tier == MedalTier.Altin ? "Azami kademe" : "Sıradaki: " + d.Threshold(tier + 1) + (d.IsMax ? " (rekor)" : string.Empty);
            return d.Title + "  [" + TierName(tier) + "]\n" + d.Description + "\n" +
                   "Eşikler: Bronz " + d.Bronze + "  ·  Gümüş " + d.Silver + "  ·  Altın " + d.Gold + "\n" +
                   "Şu an: " + value + "  ·  " + next;
        }

        public static float Rate(int part, int whole) => whole > 0 ? Math.Max(0f, Math.Min(1f, (float)part / whole)) : 0f;

        /// <summary>Sezon seviyesi: rütbe sırası (1 tabanlı) ve toplam.</summary>
        public static string SeasonLevelText(int rankIndex, int rankCount) => (rankIndex + 1) + " / " + Math.Max(1, rankCount);
    }
}
