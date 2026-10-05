using Harekat.Domain.Enums;

namespace Harekat.Domain.Catalogs;

/// <summary>XP eşiklerine göre rütbe terfisi.</summary>
public static class RankCatalog
{
    /// <summary>Her rütbe için minimum XP (Er=0 … Albay).</summary>
    public static readonly IReadOnlyDictionary<MilitaryRank, int> XpThresholds = new Dictionary<MilitaryRank, int>
    {
        [MilitaryRank.Er] = 0,
        [MilitaryRank.Onbasi] = 500,
        [MilitaryRank.Cavus] = 1_200,
        [MilitaryRank.SozlesmeliEr] = 2_200,
        [MilitaryRank.UzmanOnbasi] = 3_500,
        [MilitaryRank.UzmanCavus] = 5_000,
        [MilitaryRank.AstsubayCavus] = 7_000,
        [MilitaryRank.AstsubayKidemliCavus] = 9_500,
        [MilitaryRank.AstsubayUstcavus] = 12_500,
        [MilitaryRank.AstsubayKidemliUstcavus] = 16_000,
        [MilitaryRank.AstsubayBascavus] = 20_000,
        [MilitaryRank.AstsubayKidemliBascavus] = 25_000,
        [MilitaryRank.Astegmen] = 31_000,
        [MilitaryRank.Tegmen] = 38_000,
        [MilitaryRank.Ustegmen] = 46_000,
        [MilitaryRank.Yuzbasi] = 55_000,
        [MilitaryRank.Binbasi] = 66_000,
        [MilitaryRank.Yarbay] = 80_000,
        [MilitaryRank.Albay] = 100_000
    };

    public static MilitaryRank RankForXp(int experience)
    {
        MilitaryRank result = MilitaryRank.Er;
        foreach (var (rank, threshold) in XpThresholds.OrderBy(kv => (int)kv.Key))
        {
            if (experience >= threshold)
                result = rank;
            else
                break;
        }
        return result;
    }

    /// <summary>Maç XP formülü: kills*100 + headshots*25 + (teamCount-teamPlacement)*150 + galibiyet 1000.</summary>
    public static int CalculateMatchXp(int kills, int headshots, int teamCount, int teamPlacement, bool isWin)
    {
        var placementBonus = Math.Max(0, teamCount - teamPlacement) * 150;
        var winBonus = isWin ? 1000 : 0;
        return kills * 100 + headshots * 25 + placementBonus + winBonus;
    }
}
