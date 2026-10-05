namespace Harekat.DiscordBot.Domain;

public sealed class GameServerStatus
{
    public required string ServerId { get; init; }
    public required string Region { get; init; }
    public required string Name { get; init; }
    public int PlayerCount { get; set; }
    public int MaxPlayers { get; set; }
    public bool IsOnline { get; set; }
    public int PingMs { get; set; }
    public DateTimeOffset LastHeartbeat { get; set; } = DateTimeOffset.UtcNow;

    public double FillRatio => MaxPlayers <= 0 ? 0 : (double)PlayerCount / MaxPlayers;
}
