using System;
using System.Collections.Generic;
using Project.Core.Domain;

namespace Project.Application.Services
{
    /// <summary>Maç sonu tecrübe dökümünde tek satır (etiket + TP).</summary>
    public readonly struct XpLine
    {
        public string Label { get; }
        public int Amount { get; }

        public XpLine(string label, int amount)
        {
            Label = label;
            Amount = amount;
        }
    }

    /// <summary>
    /// Maç sonu ilerleme şöleninin saf mantığı (Unity'siz): TP döküm satırları, derece rengi sınıfı, sezon kademesi geçişleri,
    /// kariyer tecrübesiyle açılan kozmetikler ve sayarak dolma yardımcıları.
    /// </summary>
    public static class MatchEndProgression
    {
        /// <summary>Sayarak dolma süresi (sn).</summary>
        public const float CountSeconds = 0.6f;

        /// <summary>Derece sınıfı: 1, 2, 3 özel renkli; diğerleri 0.</summary>
        public static int PodiumClass(int teamPlacement, bool isWinner)
        {
            if (isWinner) return 1;
            return teamPlacement >= 1 && teamPlacement <= 3 ? teamPlacement : 0;
        }

        /// <summary>Kariyer TP dökümü: yerleştirme (geride bırakılan tim), leş (etkisiz bırakma), hasar/kafa isabeti, görev (zafer) bonusu.</summary>
        public static List<XpLine> CareerBreakdown(MatchResult r)
        {
            var lines = new List<XpLine>(4);
            var outlasted = Math.Max(0, r.TeamCount - r.TeamPlacement);
            lines.Add(new XpLine("Yerleştirme  (" + outlasted + " tim geride)", outlasted * CareerStatsService.ExperiencePerTeamOutlasted));
            lines.Add(new XpLine("Etkisiz bırakma  × " + Math.Max(0, r.Kills), Math.Max(0, r.Kills) * CareerStatsService.ExperiencePerKill));
            lines.Add(new XpLine("Kafadan isabet  × " + Math.Max(0, r.Headshots), Math.Max(0, r.Headshots) * CareerStatsService.ExperiencePerHeadshot));
            if (r.IsWinner)
                lines.Add(new XpLine("Görev bonusu  ·  ZAFER", CareerStatsService.ExperienceForWin));
            return lines;
        }

        /// <summary>Verilen tecrübe aralığında geçilen sezon kademeleri (before,after]; azami kademede durur.</summary>
        public static List<int> TiersCrossed(int xpBefore, int xpAfter, int xpPerTier, int maxTier)
        {
            var list = new List<int>();
            if (xpPerTier <= 0) return list;
            var a = Math.Min(maxTier, Math.Max(0, xpBefore) / xpPerTier);
            var b = Math.Min(maxTier, Math.Max(0, xpAfter) / xpPerTier);
            for (var t = a + 1; t <= b; t++) list.Add(t);
            return list;
        }

        /// <summary>Kariyer tecrübesi (before,after] aralığında eşiği geçilen "career_xp" kozmetikleri.</summary>
        public static List<CosmeticDefinition> CareerUnlocks(IEnumerable<CosmeticDefinition> items, int xpBefore, int xpAfter)
        {
            var list = new List<CosmeticDefinition>();
            if (items == null) return list;
            foreach (var d in items)
            {
                if (d == null || d.unlockMethod != CosmeticsService.MethodCareerXp) continue;
                if (d.unlockXp > xpBefore && d.unlockXp <= xpAfter) list.Add(d);
            }

            return list;
        }

        /// <summary>0..1 sayarak dolma ilerlemesi: (elapsed - delay) / CountSeconds, sınırlı.</summary>
        public static float CountT(float elapsed, float delay, float seconds = CountSeconds)
        {
            if (seconds <= 0f) return 1f;
            var t = (elapsed - delay) / seconds;
            return t < 0f ? 0f : t > 1f ? 1f : t;
        }

        /// <summary>Üçlü yumuşatma (hızlı başlar, yavaş biter).</summary>
        public static float EaseOut(float t)
        {
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            return 1f - (1f - t) * (1f - t) * (1f - t);
        }
    }
}
