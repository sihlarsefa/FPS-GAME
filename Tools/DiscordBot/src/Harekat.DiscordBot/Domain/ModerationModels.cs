namespace Harekat.DiscordBot.Domain;

public enum ModerationActionType
{
    Warn,
    Mute,
    Kick,
    Ban,
    Unban,
    Note
}

public sealed class ModerationLogEntry
{
    public required string EntryId { get; init; }
    public required ModerationActionType Action { get; init; }
    public required ulong TargetUserId { get; init; }
    public required string TargetDisplayName { get; init; }
    public required ulong ModeratorUserId { get; init; }
    public required string ModeratorDisplayName { get; init; }
    public required string Reason { get; init; }
    public TimeSpan? Duration { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
