namespace Harekat.Domain.Entities;

public sealed class PlayerReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReporterId { get; set; }
    public Guid ReportedPlayerId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? MatchId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ReportStatus Status { get; set; } = ReportStatus.Open;
}

public enum ReportStatus
{
    Open = 0,
    Reviewing = 1,
    ActionTaken = 2,
    Dismissed = 3
}

public sealed class AuditLogEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ActorId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
