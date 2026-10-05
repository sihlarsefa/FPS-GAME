using FluentAssertions;
using Harekat.DiscordBot.Domain;

namespace Harekat.DiscordBot.Tests;

public class RankCatalogTests
{
    [Fact]
    public void All_ranks_match_game_enum_order()
    {
        RankCatalog.AllRanks.Should().HaveCount(19);
        RankCatalog.AllRanks[0].Should().Be(MilitaryRank.Er);
        RankCatalog.AllRanks[^1].Should().Be(MilitaryRank.Albay);
        ((int)MilitaryRank.Albay).Should().Be(18);
    }

    [Theory]
    [InlineData(0, MilitaryRank.Er)]
    [InlineData(499, MilitaryRank.Er)]
    [InlineData(500, MilitaryRank.Onbasi)]
    [InlineData(55_000, MilitaryRank.Yuzbasi)]
    [InlineData(100_000, MilitaryRank.Albay)]
    [InlineData(-10, MilitaryRank.Er)]
    public void RankForXp_returns_expected(int xp, MilitaryRank expected) =>
        RankCatalog.RankForXp(xp).Should().Be(expected);

    [Fact]
    public void Display_and_role_names_are_turkish()
    {
        RankCatalog.GetDisplayName(MilitaryRank.Yuzbasi).Should().Be("Yüzbaşı");
        RankCatalog.GetDiscordRoleName(MilitaryRank.Tegmen).Should().Contain("Teğmen");
        RankCatalog.GetDiscordRoleName(MilitaryRank.Tegmen).Should().StartWith("HAREKÂT");
    }
}
