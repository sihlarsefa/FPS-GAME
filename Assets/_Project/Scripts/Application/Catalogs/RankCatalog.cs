using System;
using System.Collections.Generic;
using Project.Core.Domain;
using Project.Core.Interfaces;

namespace Project.Application.Catalogs
{
    /// <summary>Rütbe sınıfı (TSK personel sınıflandırması).</summary>
    public enum RankCategory
    {
        Enlisted = 0,          // Er/Erbaş
        Specialist = 1,        // Uzman Erbaş
        NonCommissioned = 2,   // Astsubay
        Officer = 3            // Subay
    }

    /// <summary>
    /// Rütbe adları/kısaltmaları (Er, Onb., Çvş., Sözl.Er, Uzm.Onb., Uzm.Çvş., Astsb.Çvş., Astsb.Kd.Çvş., Astsb.Üçvş.,
    /// Astsb.Kd.Üçvş., Astsb.Bçvş., Astsb.Kd.Bçvş., Asteğmen, Teğmen, Üsteğmen, Yzb., Bnb., Yb., Alb.),
    /// sınıfı (Er/Erbaş, Uzman Erbaş, Astsubay, Subay), XP eşikleri (kariyer terfisi) ve
    /// 10 kişilik tim için rütbe dağılımı (komutan Yüzbaşı/Üsteğmen, yardımcı Astsubay, diğerleri Uzm.Çvş./Sözl.Er/Er).
    /// Sayısal enum değeri kıdem sırasıdır (büyük = kıdemli).
    /// </summary>
    public static class RankCatalog
    {
        private static readonly string[] FullNames =
        {
            "Er",                         // Er
            "Onbaşı",                     // Onbasi
            "Çavuş",                      // Cavus
            "Sözleşmeli Er",              // SozlesmeliEr
            "Uzman Onbaşı",               // UzmanOnbasi
            "Uzman Çavuş",                // UzmanCavus
            "Astsubay Çavuş",             // AstsubayCavus
            "Astsubay Kıdemli Çavuş",     // AstsubayKidemliCavus
            "Astsubay Üstçavuş",          // AstsubayUstcavus
            "Astsubay Kıdemli Üstçavuş",  // AstsubayKidemliUstcavus
            "Astsubay Başçavuş",          // AstsubayBascavus
            "Astsubay Kıdemli Başçavuş",  // AstsubayKidemliBascavus
            "Asteğmen",                   // Astegmen
            "Teğmen",                     // Tegmen
            "Üsteğmen",                   // Ustegmen
            "Yüzbaşı",                    // Yuzbasi
            "Binbaşı",                    // Binbasi
            "Yarbay",                     // Yarbay
            "Albay"                       // Albay
        };

        private static readonly string[] ShortNames =
        {
            "Er",
            "Onb.",
            "Çvş.",
            "Sözl.Er",
            "Uzm.Onb.",
            "Uzm.Çvş.",
            "Astsb.Çvş.",
            "Astsb.Kd.Çvş.",
            "Astsb.Üçvş.",
            "Astsb.Kd.Üçvş.",
            "Astsb.Bçvş.",
            "Astsb.Kd.Bçvş.",
            "Asteğmen",
            "Teğmen",
            "Üsteğmen",
            "Yzb.",
            "Bnb.",
            "Yb.",
            "Alb."
        };

        /// <summary>Her rütbeye terfi için gereken toplam tecrübe puanı (kesin artan).</summary>
        private static readonly int[] ExperienceThresholds =
        {
            0,       // Er
            500,     // Onbaşı
            1200,    // Çavuş
            2200,    // Sözleşmeli Er
            3500,    // Uzman Onbaşı
            5200,    // Uzman Çavuş
            7500,    // Astsubay Çavuş
            10000,   // Astsubay Kıdemli Çavuş
            13000,   // Astsubay Üstçavuş
            16500,   // Astsubay Kıdemli Üstçavuş
            20500,   // Astsubay Başçavuş
            25000,   // Astsubay Kıdemli Başçavuş
            30000,   // Asteğmen
            36000,   // Teğmen
            43000,   // Üsteğmen
            51000,   // Yüzbaşı
            62000,   // Binbaşı
            75000,   // Yarbay
            90000    // Albay
        };

        private static readonly IReadOnlyList<MilitaryRank> AllRanks = Array.AsReadOnly(BuildAllRanks());

        public const string CategoryEnlisted = "Er/Erbaş";
        public const string CategorySpecialist = "Uzman Erbaş";
        public const string CategoryNonCommissioned = "Astsubay";
        public const string CategoryOfficer = "Subay";

        public const MilitaryRank LowestRank = MilitaryRank.Er;
        public const MilitaryRank HighestRank = MilitaryRank.Albay;

        /// <summary>Tüm rütbeler, küçükten büyüğe.</summary>
        public static IReadOnlyList<MilitaryRank> All => AllRanks;

        public static string GetName(MilitaryRank rank) => FullNames[Index(rank)];

        public static string GetShortName(MilitaryRank rank) => ShortNames[Index(rank)];

        public static string GetCategory(MilitaryRank rank)
        {
            switch (GetCategoryKind(rank))
            {
                case RankCategory.Officer:
                    return CategoryOfficer;
                case RankCategory.NonCommissioned:
                    return CategoryNonCommissioned;
                case RankCategory.Specialist:
                    return CategorySpecialist;
                default:
                    return CategoryEnlisted;
            }
        }

        public static RankCategory GetCategoryKind(MilitaryRank rank)
        {
            var value = Index(rank);
            if (value >= (int)MilitaryRank.Astegmen)
                return RankCategory.Officer;
            if (value >= (int)MilitaryRank.AstsubayCavus)
                return RankCategory.NonCommissioned;
            if (value >= (int)MilitaryRank.UzmanOnbasi)
                return RankCategory.Specialist;
            return RankCategory.Enlisted;
        }

        public static bool IsOfficer(MilitaryRank rank) => GetCategoryKind(rank) == RankCategory.Officer;

        public static bool IsNonCommissionedOfficer(MilitaryRank rank) => GetCategoryKind(rank) == RankCategory.NonCommissioned;

        /// <summary>a, b'den kıdemliyse pozitif; eşitse 0.</summary>
        public static int CompareSeniority(MilitaryRank a, MilitaryRank b) => Index(a).CompareTo(Index(b));

        public static int RequiredExperience(MilitaryRank rank) => ExperienceThresholds[Index(rank)];

        public static MilitaryRank RankForExperience(int experience)
        {
            if (experience <= 0)
                return MilitaryRank.Er;

            for (var i = ExperienceThresholds.Length - 1; i > 0; i--)
            {
                if (experience >= ExperienceThresholds[i])
                    return (MilitaryRank)i;
            }

            return MilitaryRank.Er;
        }

        /// <summary><see cref="RankForExperience"/> ile aynı (CareerStats belgelerindeki kısa ad).</summary>
        public static MilitaryRank RankForXp(int experience) => RankForExperience(experience);

        /// <summary><see cref="RequiredExperience"/> ile aynı (kısa ad).</summary>
        public static int XpForRank(MilitaryRank rank) => RequiredExperience(rank);

        /// <summary>Bir sonraki rütbe; en yüksek rütbedeyse false.</summary>
        public static bool TryGetNextRank(MilitaryRank rank, out MilitaryRank next)
        {
            var value = Index(rank);
            if (value >= (int)HighestRank)
            {
                next = HighestRank;
                return false;
            }

            next = (MilitaryRank)(value + 1);
            return true;
        }

        /// <summary>Sonraki rütbeye kalan tecrübe (en yüksek rütbede 0).</summary>
        public static int ExperienceToNextRank(int experience)
        {
            var rank = RankForExperience(experience);
            if (!TryGetNextRank(rank, out var next))
                return 0;

            var remaining = RequiredExperience(next) - Math.Max(0, experience);
            return remaining < 0 ? 0 : remaining;
        }

        /// <summary>Mevcut rütbeden sonrakine ilerleme oranı (0..1; en yüksek rütbede 1).</summary>
        public static float ProgressToNextRank(int experience)
        {
            var rank = RankForExperience(experience);
            if (!TryGetNextRank(rank, out var next))
                return 1f;

            var from = RequiredExperience(rank);
            var to = RequiredExperience(next);
            var span = to - from;
            if (span <= 0)
                return 1f;

            var progress = (Math.Max(0, experience) - from) / (float)span;
            return progress < 0f ? 0f : progress > 1f ? 1f : progress;
        }

        /// <summary>
        /// Timdeki slot (0 = komutan) için rütbe; rastgelelik ile hafif çeşitlilik. Dağılım slot sırasına göre
        /// kıdemden küçüğe doğru (artmayan) olduğundan kayıt sırası ile komuta zinciri uyumludur:
        /// 0 → Yüzbaşı/Üsteğmen, 1 → Astsb.Kd.Çvş./Astsb.Üçvş., 2-3 → Uzm.Çvş., 4-5 → Uzm.Onb./Sözl.Er,
        /// 6-9 (ve sonrası) → Sözl.Er/Çvş./Onb./Er.
        /// </summary>
        public static MilitaryRank RankForTeamSlot(int slotIndex, IRandom random)
        {
            if (slotIndex < 0)
                return MilitaryRank.Er;

            switch (slotIndex)
            {
                case 0:
                    return Roll(random, 0.6f) ? MilitaryRank.Yuzbasi : MilitaryRank.Ustegmen;
                case 1:
                    return Roll(random, 0.5f) ? MilitaryRank.AstsubayKidemliCavus : MilitaryRank.AstsubayUstcavus;
                case 2:
                case 3:
                    return MilitaryRank.UzmanCavus;
                case 4:
                case 5:
                    return Roll(random, 0.6f) ? MilitaryRank.UzmanOnbasi : MilitaryRank.SozlesmeliEr;
                default:
                    if (random == null)
                        return MilitaryRank.SozlesmeliEr;

                    switch (random.Next(0, 4))
                    {
                        case 0:
                            return MilitaryRank.SozlesmeliEr;
                        case 1:
                            return MilitaryRank.Cavus;
                        case 2:
                            return MilitaryRank.Onbasi;
                        default:
                            return MilitaryRank.Er;
                    }
            }
        }

        /// <summary>"Yzb. Kartal" gibi rütbeli görünen ad. Ad boşsa rütbenin tam adı döner.</summary>
        public static string FormatName(MilitaryRank rank, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return GetName(rank);

            return GetShortName(rank) + " " + name.Trim();
        }

        /// <summary>"Yüzbaşı Kartal" gibi tam rütbe adıyla görünen ad.</summary>
        public static string FormatFullName(MilitaryRank rank, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return GetName(rank);

            return GetName(rank) + " " + name.Trim();
        }

        private static bool Roll(IRandom random, float chance) => random == null || random.NextFloat() < chance;

        private static int Index(MilitaryRank rank)
        {
            var value = (int)rank;
            if (value < 0)
                return 0;
            return value > (int)HighestRank ? (int)HighestRank : value;
        }

        private static MilitaryRank[] BuildAllRanks()
        {
            var ranks = new MilitaryRank[(int)HighestRank + 1];
            for (var i = 0; i < ranks.Length; i++)
                ranks[i] = (MilitaryRank)i;
            return ranks;
        }
    }
}
