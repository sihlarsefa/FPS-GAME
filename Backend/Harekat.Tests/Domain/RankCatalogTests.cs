using Harekat.Domain.Catalogs;
using Harekat.Domain.Enums;
using Harekat.Domain.Entities;
using FluentAssertions;

namespace Harekat.Tests.Domain;

public class RankCatalogTests
{
    [Theory]
    [InlineData(0, MilitaryRank.Er)]
    [InlineData(499, MilitaryRank.Er)]
    [InlineData(500, MilitaryRank.Onbasi)]
    [InlineData(100_000, MilitaryRank.Albay)]
    [InlineData(99_999, MilitaryRank.Yarbay)]
    public void RankForXp_ReturnsExpected(int xp, MilitaryRank expected)
    {
        RankCatalog.RankForXp(xp).Should().Be(expected);
    }

    [Fact]
    public void CalculateMatchXp_UsesDocumentedFormula()
    {
        var xp = RankCatalog.CalculateMatchXp(5, 2, 10, 1, true);
        xp.Should().Be(5 * 100 + 2 * 25 + (10 - 1) * 150 + 1000);
        xp.Should().Be(2900);
    }

    [Fact]
    public void CalculateMatchXp_NoWinBonusWhenNotFirst()
    {
        var xp = RankCatalog.CalculateMatchXp(1, 0, 10, 5, false);
        xp.Should().Be(100 + 0 + (10 - 5) * 150 + 0);
        xp.Should().Be(850);
    }

    [Fact]
    public void MilitaryRank_MatchesUnityOrdering()
    {
        ((int)MilitaryRank.Er).Should().Be(0);
        ((int)MilitaryRank.Onbasi).Should().Be(1);
        ((int)MilitaryRank.Astegmen).Should().Be(12);
        ((int)MilitaryRank.Albay).Should().Be(18);
        Enum.GetValues<MilitaryRank>().Should().HaveCount(19);
    }

    [Fact]
    public void WeaponIds_MatchUnityCatalog()
    {
        WeaponIds.All.Should().Contain([
            "pistol_sar9", "pistol_tp9", "smg_sar109t", "ar_mpt55", "ar_mpt76",
            "ar_g3a7", "dmr_knt76", "sr_jng90", "lmg_pmt76", "sg_escort"
        ]);
        WeaponIds.All.Should().HaveCount(10);
    }
}

public class SquadTests
{
    [Fact]
    public void Squad_MaxMembersIs10()
    {
        Squad.MaxMembers.Should().Be(10);
        var s = new Squad { MemberIds = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToList() };
        s.IsFull.Should().BeTrue();
        s.TryAddMember(Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void Squad_InviteCode_IsSixChars()
    {
        Squad.GenerateInviteCode().Should().HaveLength(6);
    }
}
