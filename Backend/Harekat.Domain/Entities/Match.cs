namespace Harekat.Domain.Entities;

public enum MatchStatus
{
    Allocating = 0,
    Starting = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4
}

public sealed class MatchTeamSlot
{
    public Guid SquadId { get; set; }
    public string SquadName { get; set; } = string.Empty;
    public List<Guid> PlayerIds { get; set; } = [];
    public int BotCount { get; set; }
    public int Placement { get; set; }
    public bool IsBotFilled => BotCount > 0;
}

public sealed class Match
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Region { get; set; } = "tr";
    public MatchStatus Status { get; set; } = MatchStatus.Allocating;
    public Guid? GameServerId { get; set; }
    public string? ServerEndpoint { get; set; }
    public List<MatchTeamSlot> Teams { get; set; } = [];
    public int TargetTeamCount { get; set; } = 10;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int SeasonNumber { get; set; } = 1;

    public int TeamCount => Teams.Count;
}
