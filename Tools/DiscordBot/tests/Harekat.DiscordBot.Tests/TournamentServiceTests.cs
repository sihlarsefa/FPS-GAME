using FluentAssertions;
using Harekat.DiscordBot.Domain;
using Harekat.DiscordBot.Services;

namespace Harekat.DiscordBot.Tests;

public class TournamentServiceTests
{
    private readonly TournamentService _sut = new(new InMemoryGameDataStore(), new PerformanceMetrics());

    [Fact]
    public void CreateBracket_four_teams_has_semi_and_final()
    {
        var t = _sut.CreateBracket("Kupası", ["A", "B", "C", "D"]);
        t.Status.Should().Be(TournamentStatus.InProgress);
        t.Matches.Count.Should().Be(3); // 2 semi + 1 final
        t.Matches.Count(m => m.Round == 1).Should().Be(2);
        t.Matches.Count(m => m.Round == 2).Should().Be(1);
        t.Matches.Where(m => m.Round == 1).Should().OnlyContain(m => m.Status == MatchSlotStatus.Ready);
    }

    [Fact]
    public void SubmitResult_advances_and_completes()
    {
        var t = _sut.CreateBracket("T", ["Alpha", "Bravo", "Charlie", "Delta"]);
        var round1 = t.Matches.Where(m => m.Round == 1).OrderBy(m => m.SlotIndex).ToList();
        round1.Should().HaveCount(2);

        var r1 = _sut.SubmitResult(t.TournamentId, round1[0].MatchId, round1[0].TeamA!);
        r1.Ok.Should().BeTrue();

        var r2 = _sut.SubmitResult(t.TournamentId, round1[1].MatchId, round1[1].TeamB!);
        r2.Ok.Should().BeTrue();

        var final = r2.Tournament!.Matches.Single(m => m.Round == 2);
        final.TeamA.Should().NotBeNull();
        final.TeamB.Should().NotBeNull();
        final.Status.Should().Be(MatchSlotStatus.Ready);

        var rf = _sut.SubmitResult(t.TournamentId, final.MatchId, final.TeamA!);
        rf.TournamentCompleted.Should().BeTrue();
        rf.Tournament!.Status.Should().Be(TournamentStatus.Completed);
        rf.Tournament.WinnerTeam.Should().Be(final.TeamA);
    }

    [Fact]
    public void Bye_auto_advances_for_three_teams()
    {
        var t = _sut.CreateBracket("Odd", ["X", "Y", "Z"]);
        // 4 slot with one BYE
        t.Matches.Where(m => m.Round == 1).Should().Contain(m => m.Status == MatchSlotStatus.Bye);
        var final = t.Matches.Single(m => m.Round == 2);
        // One finalist already advanced via bye
        (final.TeamA is not null || final.TeamB is not null).Should().BeTrue();
    }

    [Fact]
    public void Invalid_winner_rejected()
    {
        var t = _sut.CreateBracket("T", ["A", "B"]);
        var m = t.Matches.Single(x => x.Round == 1);
        var r = _sut.SubmitResult(t.TournamentId, m.MatchId, "YOK");
        r.Ok.Should().BeFalse();
    }

    [Fact]
    public void Requires_at_least_two_teams()
    {
        var act = () => _sut.CreateBracket("T", ["Only"]);
        act.Should().Throw<ArgumentException>();
    }
}
