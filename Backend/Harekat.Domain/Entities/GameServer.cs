namespace Harekat.Domain.Entities;

public enum GameServerStatus
{
    Starting = 0,
    Ready = 1,
    Allocated = 2,
    Draining = 3,
    Offline = 4
}

public sealed class GameServer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 7777;
    public string Region { get; set; } = "tr";
    public string ServerKeyHash { get; set; } = string.Empty;
    public GameServerStatus Status { get; set; } = GameServerStatus.Ready;
    public Guid? CurrentMatchId { get; set; }
    public int MaxPlayers { get; set; } = 100;
    public int CurrentPlayers { get; set; }
    public DateTimeOffset LastHeartbeatAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset RegisteredAt { get; set; } = DateTimeOffset.UtcNow;

    public string Endpoint => $"{Host}:{Port}";

    public bool IsHealthy(TimeSpan timeout) =>
        Status is GameServerStatus.Ready or GameServerStatus.Allocated &&
        DateTimeOffset.UtcNow - LastHeartbeatAt < timeout;
}
