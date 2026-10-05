using Harekat.DiscordBot.Domain;

namespace Harekat.DiscordBot.Services;

public interface IServerStatusService
{
    Task<IReadOnlyList<GameServerStatus>> GetAllAsync(CancellationToken ct = default);
    Task<ServerStatusSummary> GetSummaryAsync(CancellationToken ct = default);
}

public sealed record ServerStatusSummary(
    int OnlineServers,
    int TotalServers,
    int ActivePlayers,
    int Capacity,
    IReadOnlyList<GameServerStatus> Servers);

public sealed class ServerStatusService(InMemoryGameDataStore store, PerformanceMetrics metrics) : IServerStatusService
{
    public Task<IReadOnlyList<GameServerStatus>> GetAllAsync(CancellationToken ct = default) =>
        metrics.MeasureAsync("servers.all", () =>
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(store.GetServers());
        });

    public Task<ServerStatusSummary> GetSummaryAsync(CancellationToken ct = default) =>
        metrics.MeasureAsync("servers.summary", () =>
        {
            ct.ThrowIfCancellationRequested();
            var servers = store.GetServers();
            var online = servers.Where(s => s.IsOnline).ToArray();
            return Task.FromResult(new ServerStatusSummary(
                online.Length,
                servers.Count,
                online.Sum(s => s.PlayerCount),
                online.Sum(s => s.MaxPlayers),
                servers));
        });
}
