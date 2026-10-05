using Harekat.Application.Abstractions;
using Harekat.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Harekat.Infrastructure.Repositories;

public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly Persistence.HarekatDbContext _db;
    public EfUnitOfWork(Persistence.HarekatDbContext db) => _db = db;
    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}

public sealed class EfPlayerRepository : IPlayerRepository
{
    private readonly Persistence.HarekatDbContext _db;
    public EfPlayerRepository(Persistence.HarekatDbContext db) => _db = db;

    public Task<Player?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Players.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Player?> GetByUsernameAsync(string username, CancellationToken ct = default) =>
        _db.Players.FirstOrDefaultAsync(p => p.Username.ToLower() == username.ToLower(), ct);

    public Task<Player?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        _db.Players.FirstOrDefaultAsync(p => p.Email == email.ToLower(), ct);

    public Task<Player?> GetByRefreshTokenHashAsync(string hash, CancellationToken ct = default) =>
        _db.Players.FirstOrDefaultAsync(p => p.RefreshTokenHash == hash, ct);

    public async Task<IReadOnlyList<Player>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var set = ids.ToHashSet();
        return await _db.Players.Where(p => set.Contains(p.Id)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Player>> GetLeaderboardAsync(string metric, int take, CancellationToken ct = default)
    {
        var q = _db.Players.AsQueryable();
        q = metric switch
        {
            "kills" => q.OrderByDescending(p => p.Stats.Kills),
            "wins" => q.OrderByDescending(p => p.Stats.Wins),
            "elo" => q.OrderByDescending(p => p.EloRating),
            "headshots" => q.OrderByDescending(p => p.Stats.Headshots),
            "season" => q.OrderByDescending(p => p.SeasonXp),
            _ => q.OrderByDescending(p => p.Stats.Experience)
        };
        return await q.Take(take).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Player>> GetSeasonLeaderboardAsync(int season, int take, CancellationToken ct = default) =>
        await _db.Players.OrderByDescending(p => p.SeasonXp).Take(take).ToListAsync(ct);

    public async Task AddAsync(Player player, CancellationToken ct = default)
    {
        await _db.Players.AddAsync(player, ct);
    }

    public Task UpdateAsync(Player player, CancellationToken ct = default)
    {
        _db.Players.Update(player);
        return Task.CompletedTask;
    }

    public Task<int> CountAsync(CancellationToken ct = default) => _db.Players.CountAsync(ct);
}

public sealed class EfSquadRepository : ISquadRepository
{
    private readonly Persistence.HarekatDbContext _db;
    public EfSquadRepository(Persistence.HarekatDbContext db) => _db = db;

    public Task<Squad?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Squads.FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<Squad?> GetByInviteCodeAsync(string code, CancellationToken ct = default) =>
        _db.Squads.FirstOrDefaultAsync(s => s.InviteCode == code, ct);

    public Task<Squad?> GetByMemberIdAsync(Guid playerId, CancellationToken ct = default) =>
        _db.Squads.AsEnumerable().FirstOrDefault(s => s.MemberIds.Contains(playerId)) is { } s
            ? Task.FromResult<Squad?>(s)
            : Task.FromResult<Squad?>(null);

    public async Task AddAsync(Squad squad, CancellationToken ct = default) => await _db.Squads.AddAsync(squad, ct);
    public Task UpdateAsync(Squad squad, CancellationToken ct = default) { _db.Squads.Update(squad); return Task.CompletedTask; }
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var s = await GetByIdAsync(id, ct);
        if (s is not null) _db.Squads.Remove(s);
    }
}

public sealed class EfMatchTicketRepository : IMatchTicketRepository
{
    private readonly Persistence.HarekatDbContext _db;
    public EfMatchTicketRepository(Persistence.HarekatDbContext db) => _db = db;

    public Task<MatchTicket?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.MatchTickets.FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<MatchTicket?> GetActiveBySquadIdAsync(Guid squadId, CancellationToken ct = default) =>
        _db.MatchTickets.FirstOrDefaultAsync(t => t.SquadId == squadId && t.Status == MatchTicketStatus.Queued, ct);

    public async Task<IReadOnlyList<MatchTicket>> GetQueuedByRegionAsync(string region, CancellationToken ct = default) =>
        await _db.MatchTickets.Where(t => t.Region == region && t.Status == MatchTicketStatus.Queued)
            .OrderBy(t => t.EnqueuedAt).ToListAsync(ct);

    public async Task AddAsync(MatchTicket ticket, CancellationToken ct = default) => await _db.MatchTickets.AddAsync(ticket, ct);
    public Task UpdateAsync(MatchTicket ticket, CancellationToken ct = default) { _db.MatchTickets.Update(ticket); return Task.CompletedTask; }
}

public sealed class EfMatchRepository : IMatchRepository
{
    private readonly Persistence.HarekatDbContext _db;
    public EfMatchRepository(Persistence.HarekatDbContext db) => _db = db;

    public Task<Match?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Matches.FirstOrDefaultAsync(m => m.Id == id, ct);

    public async Task AddAsync(Match match, CancellationToken ct = default) => await _db.Matches.AddAsync(match, ct);
    public Task UpdateAsync(Match match, CancellationToken ct = default) { _db.Matches.Update(match); return Task.CompletedTask; }
}

public sealed class EfGameServerRepository : IGameServerRepository
{
    private readonly Persistence.HarekatDbContext _db;
    public EfGameServerRepository(Persistence.HarekatDbContext db) => _db = db;

    public Task<GameServer?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.GameServers.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<GameServer>> GetReadyByRegionAsync(string region, CancellationToken ct = default) =>
        await _db.GameServers.Where(s => s.Region == region && s.Status == GameServerStatus.Ready).ToListAsync(ct);

    public async Task AddAsync(GameServer server, CancellationToken ct = default) => await _db.GameServers.AddAsync(server, ct);
    public Task UpdateAsync(GameServer server, CancellationToken ct = default) { _db.GameServers.Update(server); return Task.CompletedTask; }
    public async Task<IReadOnlyList<GameServer>> GetAllAsync(CancellationToken ct = default) => await _db.GameServers.ToListAsync(ct);
}

public sealed class EfFriendshipRepository : IFriendshipRepository
{
    private readonly Persistence.HarekatDbContext _db;
    public EfFriendshipRepository(Persistence.HarekatDbContext db) => _db = db;

    public Task<Friendship?> GetAsync(Guid id, CancellationToken ct = default) =>
        _db.Friendships.FirstOrDefaultAsync(f => f.Id == id, ct);

    public Task<Friendship?> GetBetweenAsync(Guid a, Guid b, CancellationToken ct = default) =>
        _db.Friendships.FirstOrDefaultAsync(f =>
            (f.RequesterId == a && f.AddresseeId == b) || (f.RequesterId == b && f.AddresseeId == a), ct);

    public async Task<IReadOnlyList<Friendship>> GetForPlayerAsync(Guid playerId, CancellationToken ct = default) =>
        await _db.Friendships.Where(f => f.RequesterId == playerId || f.AddresseeId == playerId).ToListAsync(ct);

    public async Task AddAsync(Friendship friendship, CancellationToken ct = default) => await _db.Friendships.AddAsync(friendship, ct);
    public Task UpdateAsync(Friendship friendship, CancellationToken ct = default) { _db.Friendships.Update(friendship); return Task.CompletedTask; }
}

public sealed class EfSeasonRepository : ISeasonRepository
{
    private readonly Persistence.HarekatDbContext _db;
    public EfSeasonRepository(Persistence.HarekatDbContext db) => _db = db;

    public Task<Season?> GetActiveAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        return _db.Seasons.FirstOrDefaultAsync(s => s.StartsAt <= now && s.EndsAt > now, ct);
    }

    public Task<Season?> GetByNumberAsync(int number, CancellationToken ct = default) =>
        _db.Seasons.FirstOrDefaultAsync(s => s.Number == number, ct);

    public async Task<IReadOnlyList<Season>> GetAllAsync(CancellationToken ct = default) =>
        await _db.Seasons.OrderByDescending(s => s.Number).ToListAsync(ct);

    public async Task AddArchiveAsync(SeasonArchiveEntry entry, CancellationToken ct = default) =>
        await _db.SeasonArchives.AddAsync(entry, ct);

    public async Task<IReadOnlyList<SeasonArchiveEntry>> GetArchiveAsync(int season, CancellationToken ct = default) =>
        await _db.SeasonArchives.Where(a => a.SeasonNumber == season).OrderBy(a => a.Placement).ToListAsync(ct);
}

public sealed class EfModerationRepository : IModerationRepository
{
    private readonly Persistence.HarekatDbContext _db;
    public EfModerationRepository(Persistence.HarekatDbContext db) => _db = db;

    public async Task AddReportAsync(PlayerReport report, CancellationToken ct = default) =>
        await _db.Reports.AddAsync(report, ct);

    public async Task<IReadOnlyList<PlayerReport>> GetOpenReportsAsync(CancellationToken ct = default) =>
        await _db.Reports.Where(r => r.Status == ReportStatus.Open).OrderByDescending(r => r.CreatedAt).ToListAsync(ct);

    public Task UpdateReportAsync(PlayerReport report, CancellationToken ct = default)
    {
        _db.Reports.Update(report);
        return Task.CompletedTask;
    }

    public async Task AddAuditAsync(AuditLogEntry entry, CancellationToken ct = default) =>
        await _db.AuditLogs.AddAsync(entry, ct);

    public async Task<IReadOnlyList<AuditLogEntry>> GetAuditAsync(int take, CancellationToken ct = default) =>
        await _db.AuditLogs.OrderByDescending(a => a.CreatedAt).Take(take).ToListAsync(ct);
}
