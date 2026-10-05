namespace Harekat.Domain.Entities;

public sealed class Season
{
    public int Number { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public bool IsActive => DateTimeOffset.UtcNow >= StartsAt && DateTimeOffset.UtcNow < EndsAt;
}

public sealed class SeasonArchiveEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int SeasonNumber { get; set; }
    public Guid PlayerId { get; set; }
    public string Username { get; set; } = string.Empty;
    public int SeasonXp { get; set; }
    public int Placement { get; set; }
    public string RewardBadge { get; set; } = string.Empty;
}
