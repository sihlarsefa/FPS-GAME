using FluentAssertions;
using Harekat.DiscordBot.Domain;
using Harekat.DiscordBot.Services;

namespace Harekat.DiscordBot.Tests;

public class SquadRecruitmentServiceTests
{
    private readonly SquadRecruitmentService _sut = new(new InMemoryGameDataStore(), new PerformanceMetrics());

    [Fact]
    public async Task Create_adds_creator_as_first_member()
    {
        var r = await _sut.CreateAsync(1, "Komutan", "Alfa", "gece raid");
        r.MemberCount.Should().Be(1);
        r.OpenSlots.Should().Be(9);
        r.MemberDiscordIds.Should().Contain(1UL);
        r.Description.Should().Be("gece raid");
    }

    [Fact]
    public async Task Join_fills_to_ten_and_closes()
    {
        var r = await _sut.CreateAsync(1, "Lider", "Bravo", null);
        for (ulong i = 2; i <= 10; i++)
        {
            var (ok, updated, error) = await _sut.JoinAsync(r.RecruitmentId, i);
            ok.Should().BeTrue(error);
            updated!.MemberCount.Should().Be((int)i);
        }

        var full = await _sut.GetAsync(r.RecruitmentId);
        full!.IsFull.Should().BeTrue();
        full.IsClosed.Should().BeTrue();

        var (fail, _, err) = await _sut.JoinAsync(r.RecruitmentId, 99);
        fail.Should().BeFalse();
        err.Should().Match(e => e!.Contains("dolu") || e.Contains("kapatılmış"));
    }

    [Fact]
    public async Task Duplicate_join_rejected()
    {
        var r = await _sut.CreateAsync(5, "X", "Charlie", null);
        var (ok, _, error) = await _sut.JoinAsync(r.RecruitmentId, 5);
        ok.Should().BeFalse();
        error.Should().Contain("Zaten");
    }

    [Fact]
    public async Task Create_requires_names()
    {
        var act = async () => await _sut.CreateAsync(1, "a", " ", null);
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
