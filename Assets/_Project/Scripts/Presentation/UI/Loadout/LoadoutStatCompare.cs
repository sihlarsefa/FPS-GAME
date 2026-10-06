using System;
using System.Collections.Generic;
using System.Globalization;
using Project.Application.Catalogs;
using Project.Core.Domain;

namespace Project.Presentation.UI
{
    /// <summary>Bir çubuktaki değişimin yönü (iyi/kötü, yüksek = iyi kuralıyla).</summary>
    public enum StatTrend
    {
        Same = 0,
        Better = 1,
        Worse = -1
    }

    /// <summary>Tek bir çubuğun önce/sonra değeri (0..1) ve 0..100 puan farkı.</summary>
    public readonly struct StatDelta
    {
        public readonly int Index;
        public readonly float Before;
        public readonly float After;
        public readonly int Points;

        public StatDelta(int index, float before, float after)
        {
            Index = index;
            Before = before;
            After = after;
            Points = LoadoutStats.DeltaPoints(before, after);
        }

        public StatTrend Trend => Points > 0 ? StatTrend.Better : (Points < 0 ? StatTrend.Worse : StatTrend.Same);
    }

    /// <summary>
    /// Çubuğun iki parçalı çizimi (CoD Gunsmith tarzı): düz dolgu + fark uzantısı.
    /// Kazançta düz dolgu = eski değer, uzantı = kazanç (yeşil); kayıpta düz dolgu = yeni değer, uzantı = kayıp (kırmızı).
    /// </summary>
    public readonly struct BarSegments
    {
        public readonly float Solid;
        public readonly float Extra;
        public readonly StatTrend Trend;

        public BarSegments(float solid, float extra, StatTrend trend)
        {
            Solid = solid;
            Extra = extra;
            Trend = trend;
        }
    }

    /// <summary>İki istatistik kümesi arasındaki tam karşılaştırma.</summary>
    public sealed class StatComparison
    {
        public StatDelta[] Bars;
        public int RpmDelta;
        public int MagazineDelta;
        public float WeightDelta;
        public int OverallBefore;
        public int OverallAfter;

        public int OverallDelta => OverallAfter - OverallBefore;

        /// <summary>Hiçbir çubuk/ham değer değişmediyse true.</summary>
        public bool IsIdentical
        {
            get
            {
                for (var i = 0; i < Bars.Length; i++)
                    if (Bars[i].Points != 0)
                        return false;
                return RpmDelta == 0 && MagazineDelta == 0 && Math.Abs(WeightDelta) < 0.005f;
            }
        }

        /// <summary>İyileşen çubuk sayısı.</summary>
        public int BetterCount => CountTrend(StatTrend.Better);

        /// <summary>Kötüleşen çubuk sayısı.</summary>
        public int WorseCount => CountTrend(StatTrend.Worse);

        private int CountTrend(StatTrend t)
        {
            var n = 0;
            for (var i = 0; i < Bars.Length; i++)
                if (Bars[i].Trend == t)
                    n++;
            return n;
        }
    }

    /// <summary>Bir yuvadaki aday eklentinin mevcut kuruluma göre etkisi (liste satırı).</summary>
    public sealed class SlotOption
    {
        /// <summary>Eklenti kimliği; null = yuva boş.</summary>
        public string ItemId;
        public string Name;
        public bool IsCurrent;
        public StatComparison VsCurrent;
    }

    /// <summary>
    /// Donanım ekranı karşılaştırma mantığı (saf, Unity bağımsız): aday eklentinin çubuklara etkisi (+/-),
    /// sınıfa göre ağırlıklı genel puan, harf notu ve yuva seçenek sıralaması. LoadoutStats'ın değerlerini kullanır.
    /// </summary>
    public static class LoadoutStatCompare
    {

        // Sıra: HASAR, ATIŞ HIZI, MENZİL, KONTROL, HAREKET, NİŞAN.
        private static readonly float[] DefaultWeights = { 1.0f, 0.8f, 0.9f, 1.1f, 0.8f, 0.7f };
        private static readonly float[] SmgWeights = { 0.8f, 1.0f, 0.6f, 1.1f, 1.3f, 1.1f };
        private static readonly float[] SniperWeights = { 1.5f, 0.3f, 1.5f, 1.0f, 0.5f, 0.6f };
        private static readonly float[] LmgWeights = { 1.0f, 1.2f, 0.9f, 1.4f, 0.4f, 0.5f };
        private static readonly float[] ShotgunWeights = { 1.6f, 0.5f, 0.4f, 0.8f, 1.0f, 0.9f };
        private static readonly float[] PistolWeights = { 0.9f, 0.8f, 0.5f, 1.0f, 1.2f, 1.3f };

        /// <summary>Sınıfın çubuk ağırlıkları (6 eleman). Yeni dizi döner; çağıran değiştirebilir.</summary>
        public static float[] WeightsFor(WeaponCategory category)
        {
            float[] src;
            switch (category)
            {
                case WeaponCategory.Smg: src = SmgWeights; break;
                case WeaponCategory.Sniper:
                case WeaponCategory.Dmr: src = SniperWeights; break;
                case WeaponCategory.Lmg: src = LmgWeights; break;
                case WeaponCategory.Shotgun: src = ShotgunWeights; break;
                case WeaponCategory.Pistol: src = PistolWeights; break;
                default: src = DefaultWeights; break;
            }

            return (float[])src.Clone();
        }

        /// <summary>Sınıfa göre ağırlıklı genel puan, 0..100.</summary>
        public static int Overall(in LoadoutStatSet set, WeaponCategory category)
        {
            var w = WeightsFor(category);
            float sum = 0f, total = 0f;
            for (var i = 0; i < LoadoutStats.BarCount; i++)
            {
                sum += Clamp01(LoadoutStats.Value(set, i)) * w[i];
                total += w[i];
            }

            return total <= 0f ? 0 : (int)Math.Round(sum / total * 100f, MidpointRounding.AwayFromZero);
        }

        /// <summary>0..1 değer için harf notu: S ≥ 0.85, A ≥ 0.70, B ≥ 0.50, C ≥ 0.30, aksi D.</summary>
        public static string Grade(float value01)
        {
            var v = Clamp01(value01);
            if (v >= 0.85f) return "S";
            if (v >= 0.70f) return "A";
            if (v >= 0.50f) return "B";
            if (v >= 0.30f) return "C";
            return "D";
        }

        /// <summary>0..100 genel puan için harf notu.</summary>
        public static string GradeFromPoints(int points) => Grade(points / 100f);

        /// <summary>İki istatistik kümesini çubuk/çubuk ve ham değerlerle karşılaştırır.</summary>
        public static StatComparison Compare(in LoadoutStatSet before, in LoadoutStatSet after, WeaponCategory category)
        {
            var bars = new StatDelta[LoadoutStats.BarCount];
            for (var i = 0; i < bars.Length; i++)
                bars[i] = new StatDelta(i, LoadoutStats.Value(before, i), LoadoutStats.Value(after, i));
            return new StatComparison
            {
                Bars = bars,
                RpmDelta = (int)Math.Round(after.RoundsPerMinute - before.RoundsPerMinute),
                MagazineDelta = after.MagazineSize - before.MagazineSize,
                WeightDelta = after.WeightKg - before.WeightKg,
                OverallBefore = Overall(before, category),
                OverallAfter = Overall(after, category)
            };
        }

        /// <summary>Çubuğun düz dolgu + fark uzantısı bölümleri (ikisi de 0..1).</summary>
        public static BarSegments Segments(float before, float after)
        {
            before = Clamp01(before);
            after = Clamp01(after);
            if (Math.Abs(after - before) < 0.005f)
                return new BarSegments(after, 0f, StatTrend.Same);
            return after > before
                ? new BarSegments(before, after - before, StatTrend.Better)
                : new BarSegments(after, before - after, StatTrend.Worse);
        }

        /// <summary>Kimlik listesinde yuvadaki eklentiyi aday ile değiştirir (aday null = yuvayı boşalt). Girdi değişmez.</summary>
        public static List<string> ReplaceInSlot(IEnumerable<string> currentIds, AttachmentSlot slot, string candidateId)
        {
            var result = new List<string>(6);
            if (currentIds != null)
            {
                foreach (var id in currentIds)
                {
                    if (string.IsNullOrEmpty(id))
                        continue;
                    var def = AttachmentCatalog.Get(id);
                    if (def != null && def.Slot == slot)
                        continue;
                    result.Add(id);
                }
            }

            if (!string.IsNullOrEmpty(candidateId))
                result.Add(candidateId);
            return result;
        }

        /// <summary>Adayı takınca çubukların mevcut kuruluma göre nasıl değişeceği.</summary>
        public static StatComparison PreviewSlot(WeaponDefinitionData weapon, IEnumerable<string> currentIds, AttachmentSlot slot, string candidateId)
        {
            if (weapon == null)
                return null;
            var current = new List<string>();
            if (currentIds != null)
                foreach (var id in currentIds)
                    current.Add(id);
            var now = LoadoutStats.Compute(weapon, current);
            var next = LoadoutStats.Compute(weapon, ReplaceInSlot(current, slot, candidateId));
            return Compare(now, next, weapon.Category);
        }

        /// <summary>
        /// Yuvanın tüm seçenekleri (boş dahil), genel puan farkına göre azalan sıralı; mevcut seçenek IsCurrent işaretli
        /// ve eşit puanda en üste yakın tutulur. Çağıran seçenek listesini verir (LoadoutSelection.CompatibleAttachments).
        /// </summary>
        public static List<SlotOption> RankSlotOptions(WeaponDefinitionData weapon, IEnumerable<string> currentIds,
            AttachmentSlot slot, IReadOnlyList<string> options, string currentItemId)
        {
            var list = new List<SlotOption>();
            if (weapon == null || options == null)
                return list;
            var ids = new List<string>();
            if (currentIds != null)
                foreach (var id in currentIds)
                    ids.Add(id);

            for (var i = 0; i < options.Count; i++)
            {
                var itemId = string.IsNullOrEmpty(options[i]) ? null : options[i];
                var def = itemId != null ? AttachmentCatalog.Get(itemId) : null;
                list.Add(new SlotOption
                {
                    ItemId = itemId,
                    Name = def != null ? def.DisplayName : "YOK",
                    IsCurrent = (itemId ?? string.Empty) == (currentItemId ?? string.Empty),
                    VsCurrent = PreviewSlot(weapon, ids, slot, itemId)
                });
            }

            // Kararlı sıralama: puan farkı azalan, eşitte mevcut önce, sonra özgün sıra.
            var order = new int[list.Count];
            for (var i = 0; i < order.Length; i++)
                order[i] = i;
            Array.Sort(order, (a, b) =>
            {
                var c = list[b].VsCurrent.OverallDelta.CompareTo(list[a].VsCurrent.OverallDelta);
                if (c != 0) return c;
                c = list[b].IsCurrent.CompareTo(list[a].IsCurrent);
                return c != 0 ? c : a.CompareTo(b);
            });
            var sorted = new List<SlotOption>(list.Count);
            for (var i = 0; i < order.Length; i++)
                sorted.Add(list[order[i]]);
            return sorted;
        }

        /// <summary>"+5" / "-3" / "0" biçimli işaretli sayı (sıfır = "0").</summary>
        public static string Signed(int value) => value > 0 ? "+" + value.ToString(CultureInfo.InvariantCulture) : value.ToString(CultureInfo.InvariantCulture);

        /// <summary>Ağırlık farkı metni: "+0.4 kg" / "-0.2 kg" / boş (fark yok).</summary>
        public static string WeightText(float deltaKg)
        {
            if (Math.Abs(deltaKg) < 0.005f)
                return string.Empty;
            return (deltaKg > 0f ? "+" : "-") + Math.Abs(deltaKg).ToString("0.0#", CultureInfo.InvariantCulture) + " kg";
        }

        /// <summary>Ağırlık artışı kötü, azalış iyidir (çubuk yönünün tersi).</summary>
        public static StatTrend WeightTrend(float deltaKg) => deltaKg > 0.005f ? StatTrend.Worse : (deltaKg < -0.005f ? StatTrend.Better : StatTrend.Same);

        /// <summary>Kısa özet: "3 iyi, 1 kötü" gibi; değişim yoksa "değişim yok".</summary>
        public static string Summary(StatComparison c)
        {
            if (c == null || c.IsIdentical)
                return "değişim yok";
            var parts = new List<string>(2);
            if (c.BetterCount > 0) parts.Add(c.BetterCount + " iyi");
            if (c.WorseCount > 0) parts.Add(c.WorseCount + " kötü");
            return parts.Count == 0 ? "ham değerler değişti" : string.Join(", ", parts);
        }

        /// <summary>Sınıf içi sıralama: değerin katalogdaki aynı sınıf silahlar arasındaki yüzdelik dilimi (0..100).</summary>
        public static int CategoryPercentile(WeaponDefinitionData weapon, int barIndex)
        {
            if (weapon == null)
                return 0;
            var mine = LoadoutStats.Value(LoadoutStats.Compute(weapon), barIndex);
            var all = WeaponCatalog.All;
            int total = 0, below = 0;
            for (var i = 0; i < all.Count; i++)
            {
                if (all[i].Category != weapon.Category)
                    continue;
                total++;
                if (LoadoutStats.Value(LoadoutStats.Compute(all[i]), barIndex) < mine - 0.0001f)
                    below++;
            }

            return total <= 1 ? 100 : (int)Math.Round(below * 100f / (total - 1), MidpointRounding.AwayFromZero);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
