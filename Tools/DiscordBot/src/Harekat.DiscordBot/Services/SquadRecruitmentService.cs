using Harekat.DiscordBot.Domain;

namespace Harekat.DiscordBot.Services;

public interface ISquadRecruitmentService
{
    Task<SquadRecruitment> CreateAsync(ulong creatorId, string creatorName, string squadName, string? description, CancellationToken ct = default);
    Task<(bool Ok, SquadRecruitment? Recruitment, string? Error)> JoinAsync(string recruitmentId, ulong userId, CancellationToken ct = default);
    Task<IReadOnlyList<SquadRecruitment>> ListOpenAsync(CancellationToken ct = default);
    Task<SquadRecruitment?> GetAsync(string recruitmentId, CancellationToken ct = default);
}

public sealed class SquadRecruitmentService(InMemoryGameDataStore store, PerformanceMetrics metrics) : ISquadRecruitmentService
{
    public Task<SquadRecruitment> CreateAsync(
        ulong creatorId,
        string creatorName,
        string squadName,
        string? description,
        CancellationToken ct = default) =>
        metrics.MeasureAsync("recruitment.create", () =>
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(squadName))
                throw new ArgumentException("Tim adı zorunlu.", nameof(squadName));
            if (string.IsNullOrWhiteSpace(creatorName))
                throw new ArgumentException("Oluşturan adı zorunlu.", nameof(creatorName));

            var recruitment = new SquadRecruitment
            {
                RecruitmentId = $"rec-{Guid.NewGuid():N}"[..16],
                CreatorDiscordId = creatorId,
                CreatorDisplayName = creatorName.Trim(),
                SquadName = squadName.Trim(),
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim()
            };
            recruitment.MemberDiscordIds.Add(creatorId);
            return Task.FromResult(store.AddRecruitment(recruitment));
        });

    public Task<(bool Ok, SquadRecruitment? Recruitment, string? Error)> JoinAsync(
        string recruitmentId,
        ulong userId,
        CancellationToken ct = default) =>
        metrics.MeasureAsync("recruitment.join", () =>
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(recruitmentId))
                return Task.FromResult<(bool, SquadRecruitment?, string?)>((false, null, "İlan kimliği geçersiz."));

            var ok = store.TryJoinRecruitment(recruitmentId, userId, out var updated, out var error);
            return Task.FromResult((ok, updated, error));
        });

    public Task<IReadOnlyList<SquadRecruitment>> ListOpenAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(store.GetOpenRecruitments());
    }

    public Task<SquadRecruitment?> GetAsync(string recruitmentId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(store.GetRecruitment(recruitmentId));
    }
}
