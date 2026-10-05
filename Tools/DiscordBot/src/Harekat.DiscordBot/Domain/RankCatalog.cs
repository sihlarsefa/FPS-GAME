namespace Harekat.DiscordBot.Domain;

/// <summary>
/// Rütbe görünen adları ve Discord rol adı eşlemesi.
/// </summary>
public static class RankCatalog
{
    private static readonly IReadOnlyDictionary<MilitaryRank, string> DisplayNames =
        new Dictionary<MilitaryRank, string>
        {
            [MilitaryRank.Er] = "Er",
            [MilitaryRank.Onbasi] = "Onbaşı",
            [MilitaryRank.Cavus] = "Çavuş",
            [MilitaryRank.SozlesmeliEr] = "Sözleşmeli Er",
            [MilitaryRank.UzmanOnbasi] = "Uzman Onbaşı",
            [MilitaryRank.UzmanCavus] = "Uzman Çavuş",
            [MilitaryRank.AstsubayCavus] = "Astsubay Çavuş",
            [MilitaryRank.AstsubayKidemliCavus] = "Astsubay Kıdemli Çavuş",
            [MilitaryRank.AstsubayUstcavus] = "Astsubay Üstçavuş",
            [MilitaryRank.AstsubayKidemliUstcavus] = "Astsubay Kıdemli Üstçavuş",
            [MilitaryRank.AstsubayBascavus] = "Astsubay Başçavuş",
            [MilitaryRank.AstsubayKidemliBascavus] = "Astsubay Kıdemli Başçavuş",
            [MilitaryRank.Astegmen] = "Asteğmen",
            [MilitaryRank.Tegmen] = "Teğmen",
            [MilitaryRank.Ustegmen] = "Üsteğmen",
            [MilitaryRank.Yuzbasi] = "Yüzbaşı",
            [MilitaryRank.Binbasi] = "Binbaşı",
            [MilitaryRank.Yarbay] = "Yarbay",
            [MilitaryRank.Albay] = "Albay"
        };

    /// <summary>XP eşikleri — RankForXp ile uyumlu basit basamaklar.</summary>
    private static readonly (int MinXp, MilitaryRank Rank)[] XpThresholds =
    [
        (0, MilitaryRank.Er),
        (500, MilitaryRank.Onbasi),
        (1_200, MilitaryRank.Cavus),
        (2_200, MilitaryRank.SozlesmeliEr),
        (3_500, MilitaryRank.UzmanOnbasi),
        (5_000, MilitaryRank.UzmanCavus),
        (7_000, MilitaryRank.AstsubayCavus),
        (9_500, MilitaryRank.AstsubayKidemliCavus),
        (12_500, MilitaryRank.AstsubayUstcavus),
        (16_000, MilitaryRank.AstsubayKidemliUstcavus),
        (20_000, MilitaryRank.AstsubayBascavus),
        (25_000, MilitaryRank.AstsubayKidemliBascavus),
        (31_000, MilitaryRank.Astegmen),
        (38_000, MilitaryRank.Tegmen),
        (46_000, MilitaryRank.Ustegmen),
        (55_000, MilitaryRank.Yuzbasi),
        (66_000, MilitaryRank.Binbasi),
        (80_000, MilitaryRank.Yarbay),
        (100_000, MilitaryRank.Albay)
    ];

    public static string GetDisplayName(MilitaryRank rank) =>
        DisplayNames.TryGetValue(rank, out var name) ? name : rank.ToString();

    /// <summary>Discord rol adı: "HAREKÂT — Yüzbaşı"</summary>
    public static string GetDiscordRoleName(MilitaryRank rank) =>
        $"HAREKÂT — {GetDisplayName(rank)}";

    public static MilitaryRank RankForXp(int experience)
    {
        if (experience < 0)
            experience = 0;

        var result = MilitaryRank.Er;
        foreach (var (minXp, rank) in XpThresholds)
        {
            if (experience >= minXp)
                result = rank;
            else
                break;
        }

        return result;
    }

    public static IReadOnlyList<MilitaryRank> AllRanks { get; } =
        Enum.GetValues<MilitaryRank>().OrderBy(r => (int)r).ToArray();
}
