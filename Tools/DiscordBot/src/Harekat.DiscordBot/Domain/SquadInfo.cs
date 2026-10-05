namespace Harekat.DiscordBot.Domain;

public sealed class SquadInfo
{
    public required string SquadId { get; init; }
    public required string Name { get; set; }
    public required string LeaderName { get; set; }
    public List<string> MemberNames { get; } = [];
    public int TotalKills { get; set; }
    public int WeeklyKills { get; set; }
    public int Wins { get; set; }
    public int SeasonScore { get; set; }

    public int MemberCount => MemberNames.Count;
    public bool IsFull => MemberCount >= 10;
    public int OpenSlots => Math.Max(0, 10 - MemberCount);
}
