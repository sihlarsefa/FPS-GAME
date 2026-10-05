namespace Harekat.DiscordBot.Domain;

public sealed class PlayerProfile
{
    public required string PlayerId { get; init; }
    public required string DisplayName { get; set; }
    public ulong? DiscordUserId { get; set; }
    public CareerStats Stats { get; set; } = new();
    public string? SquadId { get; set; }
    public int SeasonXp { get; set; }
    public int SeasonKills { get; set; }
    public int WeeklyKills { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
