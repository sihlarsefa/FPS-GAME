namespace Harekat.Domain.Entities;

public sealed class Squad
{
    public const int MaxMembers = 10;

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public Guid LeaderId { get; set; }
    public List<Guid> MemberIds { get; set; } = [];
    public HashSet<Guid> ReadyMemberIds { get; set; } = [];
    public string InviteCode { get; set; } = GenerateInviteCode();
    public string Region { get; set; } = "tr";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsInQueue { get; set; }
    public bool IsInMatch { get; set; }

    public bool IsFull => MemberIds.Count >= MaxMembers;
    public int OpenSlots => MaxMembers - MemberIds.Count;
    public bool AllReady => MemberIds.Count > 0 && MemberIds.All(id => ReadyMemberIds.Contains(id));

    public bool TryAddMember(Guid playerId)
    {
        if (IsFull || MemberIds.Contains(playerId))
            return false;
        MemberIds.Add(playerId);
        return true;
    }

    public bool RemoveMember(Guid playerId)
    {
        ReadyMemberIds.Remove(playerId);
        var removed = MemberIds.Remove(playerId);
        if (removed && LeaderId == playerId && MemberIds.Count > 0)
            LeaderId = MemberIds[0];
        return removed;
    }

    public static string GenerateInviteCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        Span<char> buffer = stackalloc char[6];
        var rng = Random.Shared;
        for (var i = 0; i < buffer.Length; i++)
            buffer[i] = chars[rng.Next(chars.Length)];
        return new string(buffer);
    }
}
