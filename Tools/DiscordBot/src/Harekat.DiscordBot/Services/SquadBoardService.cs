using Harekat.DiscordBot.Domain;

namespace Harekat.DiscordBot.Services;

public interface ISquadBoardService
{
    Task<IReadOnlyList<SquadInfo>> GetLookingForMembersAsync(CancellationToken ct = default);
    Task<SquadInfo?> FindByNameAsync(string name, CancellationToken ct = default);
}

public sealed class SquadBoardService(InMemoryGameDataStore store) : ISquadBoardService
{
    public Task<IReadOnlyList<SquadInfo>> GetLookingForMembersAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var open = store.GetSquads()
            .Where(s => !s.IsFull)
            .OrderByDescending(s => s.OpenSlots)
            .ThenBy(s => s.Name)
            .ToArray();
        return Task.FromResult<IReadOnlyList<SquadInfo>>(open);
    }

    public Task<SquadInfo?> FindByNameAsync(string name, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(name))
            return Task.FromResult<SquadInfo?>(null);
        return Task.FromResult(store.FindSquadByName(name.Trim()));
    }
}
