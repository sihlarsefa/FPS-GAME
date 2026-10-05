namespace Harekat.DiscordBot.Domain;

public sealed class SquadRecruitment
{
    public required string RecruitmentId { get; init; }
    public required ulong CreatorDiscordId { get; init; }
    public required string CreatorDisplayName { get; init; }
    public required string SquadName { get; init; }
    public string? Description { get; set; }
    public List<ulong> MemberDiscordIds { get; } = [];
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public bool IsClosed { get; set; }

    public const int MaxMembers = 10;

    public int MemberCount => MemberDiscordIds.Count;
    public int OpenSlots => Math.Max(0, MaxMembers - MemberCount);
    public bool IsFull => MemberCount >= MaxMembers;
}
