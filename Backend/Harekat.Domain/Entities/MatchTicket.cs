namespace Harekat.Domain.Entities;

public enum MatchTicketStatus
{
    Queued = 0,
    Matched = 1,
    Cancelled = 2,
    Expired = 3
}

public sealed class MatchTicket
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SquadId { get; set; }
    public string Region { get; set; } = "tr";
    public int MaxPingMs { get; set; } = 120;
    public int AverageElo { get; set; } = 1000;
    public MatchTicketStatus Status { get; set; } = MatchTicketStatus.Queued;
    public DateTimeOffset EnqueuedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? MatchId { get; set; }

    /// <summary>Bekleme süresine göre Elo esneme penceresi (ms beklemeye göre genişler).</summary>
    public int EloTolerance
    {
        get
        {
            var waited = (DateTimeOffset.UtcNow - EnqueuedAt).TotalSeconds;
            return waited switch
            {
                < 15 => 100,
                < 45 => 200,
                < 90 => 350,
                _ => 600
            };
        }
    }
}
