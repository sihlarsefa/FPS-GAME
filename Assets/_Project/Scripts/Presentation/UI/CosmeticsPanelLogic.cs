using Project.Application.Services;

namespace Project.Presentation.UI
{
    /// <summary>Kozmetik vitrini saf mantığı (test edilebilir): nadirlik eşlemesi, filtreler, kaynak rozeti, açılma koşulu.</summary>
    public static class CosmeticsPanelLogic
    {
        public enum OwnFilter { All, Owned, Locked }

        public static UiRarity ToUiRarity(string rarity)
        {
            switch (rarity)
            {
                case CosmeticsService.RarityUncommon: return UiRarity.Uncommon;
                case CosmeticsService.RarityRare: return UiRarity.Rare;
                case CosmeticsService.RarityEpic: return UiRarity.Epic;
                case CosmeticsService.RarityLegendary: return UiRarity.Legendary;
                default: return UiRarity.Common;
            }
        }

        public static UiRarity RarityOf(CosmeticDefinition d) => ToUiRarity(CosmeticsService.RarityOf(d));

        /// <summary>Sahiplik filtresi (Tümü/Sahip/Kilitli) ve nadirlik filtresi (-1: tümü) birlikte uygulanır.</summary>
        public static bool Passes(CosmeticDefinition d, bool owned, OwnFilter own, int rarityFilter)
        {
            if (d == null) return false;
            if (own == OwnFilter.Owned && !owned) return false;
            if (own == OwnFilter.Locked && owned) return false;
            return rarityFilter < 0 || (int)RarityOf(d) == rarityFilter;
        }

        public static string OwnFilterName(OwnFilter f)
        {
            switch (f)
            {
                case OwnFilter.Owned: return "SAHİP";
                case OwnFilter.Locked: return "KİLİTLİ";
                default: return "TÜMÜ";
            }
        }

        /// <summary>Kaynak rozeti: SEZON / KARİYER / VARSAYILAN.</summary>
        public static string SourceBadge(CosmeticDefinition d)
        {
            if (d == null) return "";
            if (d.unlockMethod == CosmeticsService.MethodDefault) return "VARSAYILAN";
            if (d.unlockMethod == CosmeticsService.MethodCareerXp) return "KARİYER";
            return "SEZON";
        }

        /// <summary>Kilitli öğe için açılma koşulu metni (katalog verisinden); kariyer için eksik XP de yazılır.</summary>
        public static string LockText(CosmeticDefinition d, int xp)
        {
            if (d == null) return "";
            var rule = CosmeticsService.UnlockRuleText(d);
            if (d.unlockMethod == CosmeticsService.MethodCareerXp && xp < d.unlockXp)
                return "Kilitli: " + rule + " (eksik " + (d.unlockXp - xp) + ")";
            return "Kilitli: " + rule;
        }
    }
}
