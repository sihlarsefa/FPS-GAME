using Harekat.DiscordBot.Domain;

namespace Harekat.DiscordBot.Services;

public interface IModerationService
{
    Task<ModerationLogEntry> LogActionAsync(
        ModerationActionType action,
        ulong targetUserId,
        string targetDisplayName,
        ulong moderatorUserId,
        string moderatorDisplayName,
        string reason,
        TimeSpan? duration = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<ModerationLogEntry>> GetRecentAsync(int take = 20, CancellationToken ct = default);
}

public sealed class ModerationService(InMemoryGameDataStore store) : IModerationService
{
    public Task<ModerationLogEntry> LogActionAsync(
        ModerationActionType action,
        ulong targetUserId,
        string targetDisplayName,
        ulong moderatorUserId,
        string moderatorDisplayName,
        string reason,
        TimeSpan? duration = null,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Sebep zorunlu.", nameof(reason));
        if (string.IsNullOrWhiteSpace(targetDisplayName))
            throw new ArgumentException("Hedef adı zorunlu.", nameof(targetDisplayName));
        if (string.IsNullOrWhiteSpace(moderatorDisplayName))
            throw new ArgumentException("Moderatör adı zorunlu.", nameof(moderatorDisplayName));

        if (action == ModerationActionType.Mute && duration is null)
            throw new ArgumentException("Susturma için süre gerekli.", nameof(duration));

        var entry = new ModerationLogEntry
        {
            EntryId = $"mod-{Guid.NewGuid():N}"[..16],
            Action = action,
            TargetUserId = targetUserId,
            TargetDisplayName = targetDisplayName.Trim(),
            ModeratorUserId = moderatorUserId,
            ModeratorDisplayName = moderatorDisplayName.Trim(),
            Reason = reason.Trim(),
            Duration = duration
        };
        store.AddModerationLog(entry);
        return Task.FromResult(entry);
    }

    public Task<IReadOnlyList<ModerationLogEntry>> GetRecentAsync(int take = 20, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        take = Math.Clamp(take, 1, 100);
        return Task.FromResult(store.GetModerationLog(take));
    }
}
