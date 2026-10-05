using FluentAssertions;
using Harekat.DiscordBot.Domain;
using Harekat.DiscordBot.Services;

namespace Harekat.DiscordBot.Tests;

public class ModerationServiceTests
{
    private readonly ModerationService _sut = new(new InMemoryGameDataStore());

    [Fact]
    public async Task Log_and_list_recent()
    {
        await _sut.LogActionAsync(ModerationActionType.Warn, 1, "hedef", 2, "mod", "spam");
        await _sut.LogActionAsync(ModerationActionType.Ban, 3, "kötü", 2, "mod", "cheat");
        var list = await _sut.GetRecentAsync(10);
        list.Should().HaveCount(2);
        list[0].Action.Should().Be(ModerationActionType.Ban);
    }

    [Fact]
    public async Task Mute_requires_duration()
    {
        var act = async () => await _sut.LogActionAsync(
            ModerationActionType.Mute, 1, "a", 2, "b", "x");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Reason_required()
    {
        var act = async () => await _sut.LogActionAsync(
            ModerationActionType.Warn, 1, "a", 2, "b", " ");
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
